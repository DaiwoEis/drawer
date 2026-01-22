using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using System.Collections.Generic;
using Features.Drawing.Domain;
using Features.Drawing.Domain.ValueObject;
using Common.Constants;

namespace Features.Drawing.Presentation
{
    /// <summary>
    /// Defines how the brush texture rotates.
    /// </summary>
    public enum BrushRotationMode
    {
        None = 0,       // Always 0 degrees
        Follow = 1,     // Follows stroke direction (Snake-like)
        Fixed = 2       // Fixed angle (Calligraphy-like), currently defaults to 45 deg or 0
    }

    /// <summary>
    /// Handles the low-level GPU drawing using CommandBuffers.
    /// Implements the "Mesh Stamping" technique.
    /// </summary>
    public class CanvasRenderer : MonoBehaviour, Features.Drawing.Domain.Interface.IStrokeRenderer
    {
        [Header("Settings")]
        [SerializeField] private Vector2Int _resolution = new Vector2Int(2048, 2048);
        [SerializeField] private RawImage _displayImage; // UI to display the RT
        [SerializeField] private Shader _brushShader;
        [SerializeField] private Texture2D _defaultBrushTip;

        [Header("Runtime Debug")]
        [SerializeField] private float _brushSize = 50.0f;
        [SerializeField] private Color _brushColor = Color.black;
        [SerializeField] private bool _isEraser = false;
        [SerializeField] private float _brushOpacity = 1.0f; // 0-1
        [SerializeField] private BrushRotationMode _rotationMode = BrushRotationMode.None;

        private RenderTexture _activeRT;
        private Material _brushMaterial;
        private CommandBuffer _cmd;
        private Mesh _quadMesh; // Reused quad mesh
        private MaterialPropertyBlock _props; // Property block for colors

        // Interpolation Logic Delegated to Generator
        private StrokeStampGenerator _stampGenerator = new StrokeStampGenerator();
        private List<StampData> _stampBuffer = new List<StampData>(1024);
        
        // Cache for batching (optimization)
        private const int BATCH_SIZE = 1023; // Max for DrawMeshInstanced
        private Vector2Int _lastResolution;
        private float _lastDisplayWidth = -1f;
        private float _lastDisplayHeight = -1f;
        private float _lastCanvasScale = -1f;
        private float _lastSizeScale = -1f;
        private int _baseMaxDimension = 0;

        private void Awake()
        {
            // Fix background color (User requirement: White, not Blue)
            if (Camera.main != null)
            {
                Camera.main.backgroundColor = Color.white;
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
            }

            if (_baseMaxDimension <= 0)
            {
                _baseMaxDimension = Mathf.Max(_resolution.x, _resolution.y);
            }

            InitializeGraphics();
            UpdateStampGeneratorScaleIfNeeded();
            
            // Fix UI Premultiplied Alpha Issue
            // Reverted: Changing RenderMode broke Input. Using Material Blend Mode fix instead.
            // if (_displayImage != null) ...
        }

        private void OnRectTransformDimensionsChange()
        {
            UpdateStampGeneratorScaleIfNeeded();
        }

        private void OnDestroy()
        {
            if (_activeRT != null) _activeRT.Release();
            if (_activeRT != null) Destroy(_activeRT);
            if (_cmd != null) _cmd.Release();
            if (_brushMaterial != null) Destroy(_brushMaterial);
            // Don't destroy _quadMesh if it's a primitive, but if we created it:
            if (_quadMesh != null) Destroy(_quadMesh);
        }

        private void InitializeGraphics()
        {
            // 0. Auto-adjust resolution to match screen aspect ratio (if not fixed)
            // ...
            float aspect = 1.0f;
            if (_displayImage != null && _displayImage.rectTransform != null && _displayImage.rectTransform.rect.width > 0)
            {
                aspect = _displayImage.rectTransform.rect.width / _displayImage.rectTransform.rect.height;
            }
            else
            {
                aspect = (float)Screen.width / Screen.height;
            }

            // ... (Resolution logic)
            int maxDim = _baseMaxDimension > 0 ? _baseMaxDimension : Mathf.Max(_resolution.x, _resolution.y);
            if (maxDim < 2048) maxDim = 2048; 

            if (aspect >= 1f)
            {
                _resolution.x = maxDim;
                _resolution.y = Mathf.RoundToInt(maxDim / aspect);
            }
            else
            {
                _resolution.y = maxDim;
                _resolution.x = Mathf.RoundToInt(maxDim * aspect);
            }

            // 1. Setup RenderTexture
            RebuildRenderTexture(_resolution, false);

            // 2. Setup Material
            if (_brushShader == null) 
                _brushShader = Shader.Find("Drawing/BrushStamp");
            
            if (_brushShader == null)
            {
                Debug.LogError("[CanvasRenderer] CRITICAL: Brush Shader NOT FOUND!");
            }

            _brushMaterial = new Material(_brushShader);
            if (_defaultBrushTip != null)
                _brushMaterial.mainTexture = _defaultBrushTip;

            // 3. Setup CommandBuffer
            _cmd = new CommandBuffer();
            _cmd.name = "DrawingBuffer";

            // 4. Create Quad Mesh for stamping
            _quadMesh = CreateQuad();
            
            // 5. Init props
            _props = new MaterialPropertyBlock();
        }

        private void ApplyStampGeneratorScale()
        {
            float displayWidth;
            float displayHeight;
            float canvasScale;
            float sizeScale = GetBrushSizeScale(out displayWidth, out displayHeight, out canvasScale);

            _stampGenerator.SetCanvasResolution(_resolution);
            _stampGenerator.SetSizeScale(sizeScale);

            _lastResolution = _resolution;
            _lastDisplayWidth = displayWidth;
            _lastDisplayHeight = displayHeight;
            _lastCanvasScale = canvasScale;
            _lastSizeScale = sizeScale;
        }

        private Vector2Int CalculateResolution(float aspect)
        {
            int maxDim = _baseMaxDimension > 0 ? _baseMaxDimension : Mathf.Max(_resolution.x, _resolution.y);
            if (maxDim < 2048) maxDim = 2048;

            if (aspect >= 1f)
            {
                return new Vector2Int(maxDim, Mathf.RoundToInt(maxDim / aspect));
            }

            return new Vector2Int(Mathf.RoundToInt(maxDim * aspect), maxDim);
        }

        private void RebuildRenderTexture(Vector2Int targetResolution, bool preserveContent)
        {
            if (targetResolution.x <= 0 || targetResolution.y <= 0)
            {
                return;
            }

            RenderTexture newRT = new RenderTexture(targetResolution.x, targetResolution.y, 0, RenderTextureFormat.ARGB32);
            newRT.filterMode = FilterMode.Bilinear;
            newRT.useMipMap = false;
            newRT.antiAliasing = 1;
            newRT.Create();

            if (preserveContent && _activeRT != null)
            {
                Graphics.Blit(_activeRT, newRT);
            }
            else
            {
                Graphics.SetRenderTarget(newRT);
                GL.Clear(true, true, Color.clear);
            }

            if (_activeRT != null)
            {
                _activeRT.Release();
                Destroy(_activeRT);
            }

            _activeRT = newRT;
            _resolution = targetResolution;

            if (_displayImage != null)
            {
                _displayImage.texture = _activeRT;
            }
        }

        private void UpdateRenderTextureIfNeeded(float displayWidth, float displayHeight)
        {
            if (displayWidth <= 0f || displayHeight <= 0f)
            {
                return;
            }

            float aspect = displayWidth / displayHeight;
            Vector2Int targetResolution = CalculateResolution(aspect);

            if (_activeRT == null || targetResolution != _resolution)
            {
                RebuildRenderTexture(targetResolution, _activeRT != null);
            }
        }

        private float GetBrushSizeScale(out float displayWidth, out float displayHeight, out float canvasScale)
        {
            displayWidth = 0f;
            displayHeight = 0f;
            canvasScale = 1f;

            if (_displayImage != null && _displayImage.rectTransform != null)
            {
                Rect rect = _displayImage.rectTransform.rect;
                if (rect.width > 0f && rect.height > 0f)
                {
                    Canvas canvas = _displayImage.canvas;
                    if (canvas != null)
                    {
                        canvasScale = canvas.scaleFactor;
                    }

                    displayWidth = rect.width * canvasScale;
                    displayHeight = rect.height * canvasScale;
                }
            }

            if (displayWidth <= 0f || displayHeight <= 0f)
            {
                displayWidth = Screen.width;
                displayHeight = Screen.height;
            }

            if (displayWidth <= 0f || displayHeight <= 0f)
            {
                return 1f;
            }

            return Mathf.Min(_resolution.x / displayWidth, _resolution.y / displayHeight);
        }

        private void UpdateStampGeneratorScaleIfNeeded()
        {
            float displayWidth;
            float displayHeight;
            float canvasScale;
            float sizeScale = GetBrushSizeScale(out displayWidth, out displayHeight, out canvasScale);

            UpdateRenderTextureIfNeeded(displayWidth, displayHeight);

            sizeScale = GetBrushSizeScale(out displayWidth, out displayHeight, out canvasScale);

            if (_lastResolution != _resolution ||
                !Mathf.Approximately(_lastDisplayWidth, displayWidth) ||
                !Mathf.Approximately(_lastDisplayHeight, displayHeight) ||
                !Mathf.Approximately(_lastCanvasScale, canvasScale) ||
                !Mathf.Approximately(_lastSizeScale, sizeScale))
            {
                ApplyStampGeneratorScale();
            }
        }

        private Mesh CreateQuad()
        {
            Mesh mesh = new Mesh();
            mesh.vertices = new Vector3[] {
                new Vector3(-0.5f, -0.5f, 0),
                new Vector3( 0.5f, -0.5f, 0),
                new Vector3(-0.5f,  0.5f, 0),
                new Vector3( 0.5f,  0.5f, 0)
            };
            mesh.uv = new Vector2[] {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };
            mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
            return mesh;
        }

        public void SetBrushSize(float size)
        {
            Debug.Log($"[CanvasRenderer] SetBrushSize: {size}");
            _brushSize = Mathf.Max(1.0f, size);
        }

        public void SetBrushColor(Color color)
        {
            _brushColor = color;
            _isEraser = false;
        }

        public void SetEraser(bool isEraser)
        {
            _isEraser = isEraser;
        }

        /// <summary>
        /// Configures the brush appearance and behavior based on a Strategy object.
        /// </summary>
        public void ConfigureBrush(BrushStrategy strategy, Texture2D runtimeTexture = null)
        {
            if (strategy == null) return;

            // 1. Texture
            Texture2D tex = runtimeTexture != null ? runtimeTexture : strategy.MainTexture;
            
            // If texture is still null (e.g. strategy has no texture and UseRuntimeGeneration is false),
            // fallback to default
            if (tex == null && _defaultBrushTip != null)
            {
                tex = _defaultBrushTip;
            }

            if (tex != null && _brushMaterial != null)
            {
                _brushMaterial.mainTexture = tex;
            }
            
            // 2. Parameters
            // If strategy.Opacity is 0, we should fallback to 1? Or trust the strategy?
            // Usually Opacity 0 means invisible. Let's assume strategy is correct but debug if it's suspicious.
            _brushOpacity = strategy.Opacity;
            _rotationMode = strategy.RotationMode;
            
            // CRITICAL: Ensure _stampGenerator is initialized before accessing
            if (_stampGenerator == null) _stampGenerator = new StrokeStampGenerator();

            _stampGenerator.RotationMode = strategy.RotationMode;
            _stampGenerator.SpacingRatio = strategy.SpacingRatio;
            _stampGenerator.AngleJitter = strategy.AngleJitter;
            
            // Reset generator state
            _stampGenerator.Reset();

            // 3. Blend Mode
            if (_brushMaterial != null)
            {
                _brushMaterial.SetInt("_BlendOp", (int)strategy.BlendOp);
                _brushMaterial.SetInt("_SrcBlend", (int)strategy.SrcBlend);
                _brushMaterial.SetInt("_DstBlend", (int)strategy.DstBlend);
                
                // Force update material keywords if needed (Standard shader relies on this, custom shader might not)
                // But let's log to be sure
                Debug.Log($"[CanvasRenderer] Applied Brush: {strategy.name}, Op: {strategy.BlendOp}, Tex: {(tex ? tex.name : "null")}");
            }
        }

        /// <summary>
        /// Draws a batch of points to the RenderTexture.
        /// </summary>
        public void DrawPoints(IEnumerable<LogicPoint> points)
        {
            if (_activeRT == null)
            {
                Debug.LogError("[CanvasRenderer] ActiveRT is null!");
                return;
            }

            UpdateStampGeneratorScaleIfNeeded();
            _cmd.Clear();
            
            // Setup Eraser vs Brush state
            if (_isEraser)
            {
                // Eraser: Zero OneMinusSrcAlpha (Subtract alpha from destination)
                _cmd.SetExecutionFlags(CommandBufferExecutionFlags.None);
                
                _brushMaterial.SetInt("_SrcBlend", (int)BlendMode.Zero);
                _brushMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                _brushMaterial.SetInt("_BlendOp", (int)BlendOp.Add); // Ensure Op is Add for eraser
            }
            // ELSE: Do NOT force override blend modes. Trust the state set by ConfigureBrush.
            
            _cmd.SetRenderTarget(_activeRT);

            // Prepare PropertyBlock for color
            Color drawColor = _isEraser ? new Color(0,0,0,1) : _brushColor; 
            // Apply Opacity
            drawColor.a *= _brushOpacity;
            
            _props.SetColor("_Color", drawColor);

            _cmd.SetViewMatrix(Matrix4x4.identity);
            _cmd.SetProjectionMatrix(Matrix4x4.Ortho(0, _resolution.x, 0, _resolution.y, -1, 1));

            // Generate stamps
            _stampGenerator.ProcessPoints(points, _brushSize, _stampBuffer);
            
            // Draw stamps
            foreach (var stamp in _stampBuffer)
            {
                DrawStamp(stamp.Position, stamp.Size, stamp.Rotation);
            }

            Graphics.ExecuteCommandBuffer(_cmd);
        }

        private void DrawStamp(Vector2 pos, float size, float angle)
        {
            Matrix4x4 matrix = Matrix4x4.TRS(
                new Vector3(pos.x, pos.y, 0), 
                Quaternion.Euler(0, 0, angle),
                new Vector3(size, size, 1)
            );
            _cmd.DrawMesh(_quadMesh, matrix, _brushMaterial, 0, 0, _props);
        }
        
        public void ClearCanvas()
        {
            Graphics.SetRenderTarget(_activeRT);
            GL.Clear(true, true, Color.clear);
            
            // Reset state
            _stampGenerator.Reset();
        }

        public void EndStroke()
        {
            _stampGenerator.Reset();
        }
    }
}
