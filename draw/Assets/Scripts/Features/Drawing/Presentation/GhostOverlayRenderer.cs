using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using System.Collections.Generic;
using Features.Drawing.Domain;
using Features.Drawing.Domain.Interface;
using Features.Drawing.Domain.ValueObject;

namespace Features.Drawing.Presentation
{
    /// <summary>
    /// Renders transient "Ghost" strokes from remote users.
    /// Overlays on top of the main canvas.
    /// </summary>
    public class GhostOverlayRenderer : MonoBehaviour, IStrokeRenderer
    {
        [Header("References")]
        [SerializeField] private CanvasRenderer _mainRenderer;
        [SerializeField] private RawImage _displayImage;
        [SerializeField] private Shader _brushShader;
        [SerializeField] private Texture2D _defaultBrushTip;

        [Header("Ghost Settings")]
        [SerializeField] private Color _eraserTrailColor = new Color(1f, 0f, 0f, 0.2f); // Semi-transparent red

        // State
        private CanvasLayoutController _layoutController;
        private Material _brushMaterial;
        private CommandBuffer _cmd;
        private Mesh _quadMesh;
        private MaterialPropertyBlock _props;
        
        private StrokeStampGenerator _stampGenerator = new StrokeStampGenerator();
        private List<StampData> _stampBuffer = new List<StampData>(1024);

        // Brush State
        private float _baseBrushSize = 10f;
        private float _currentSize = 10f;
        private float _sizeMultiplier = 1f;
        private Color _brushColor = Color.black;
        private bool _isEraser = false;
        private float _brushOpacity = 1f;

        private void Awake()
        {
            if (_mainRenderer == null)
                _mainRenderer = FindObjectOfType<CanvasRenderer>();
                
            InitializeGraphics();
        }

        private void Start()
        {
            // Sync with main renderer resolution
            if (_mainRenderer != null)
            {
                _mainRenderer.OnResolutionChanged += OnMainResolutionChanged;
                // Initial sync
                OnMainResolutionChanged(_mainRenderer.Resolution);
            }
        }

        private void OnDestroy()
        {
            if (_mainRenderer != null)
                _mainRenderer.OnResolutionChanged -= OnMainResolutionChanged;

            _layoutController?.Release();
            if (_cmd != null) _cmd.Release();
            if (_brushMaterial != null) Destroy(_brushMaterial);
            if (_quadMesh != null) Destroy(_quadMesh);
        }

        private void OnMainResolutionChanged(Vector2Int resolution)
        {
            // Re-initialize layout with new resolution
            // We use the same resolution as main canvas to ensure 1:1 mapping
            if (_layoutController == null)
            {
                _layoutController = new CanvasLayoutController(_displayImage, resolution, 0);
                _layoutController.Initialize(); // Create RTs
            }
            else
            {
                // Force resize logic if exposed, or just rely on CheckLayoutChanges
                // But CanvasLayoutController doesn't have public Resize. 
                // It checks displayImage size.
                // Actually, CanvasLayoutController constructor takes initialResolution.
                // We might need to recreate it or add a method to UpdateResolution.
                // For now, let's assume CheckLayoutChanges handles it if displayImage size changes?
                // But Resolution is logical.
                
                // Hack: Release and recreate for now to ensure sync
                _layoutController.Release();
                _layoutController = new CanvasLayoutController(_displayImage, resolution, 0);
                _layoutController.Initialize();
            }
            
            // Sync generator scale
            _stampGenerator.SetCanvasResolution(resolution);
        }

        private void InitializeGraphics()
        {
            if (_brushShader == null) _brushShader = Shader.Find("Drawing/BrushStamp");
            if (_brushShader == null)
            {
                Debug.LogError("[GhostOverlayRenderer] Brush Shader not found!");
                return;
            }

            _brushMaterial = new Material(_brushShader);
            if (_defaultBrushTip != null) _brushMaterial.mainTexture = _defaultBrushTip;

            _cmd = new CommandBuffer();
            _cmd.name = "GhostBuffer";

            _quadMesh = CreateQuad();
            _props = new MaterialPropertyBlock();
        }

        // --- IStrokeRenderer Implementation ---

        public void ConfigureBrush(BrushStrategy strategy, Texture2D runtimeTexture = null)
        {
            if (strategy == null) return;

            Texture2D tex = runtimeTexture != null ? runtimeTexture : strategy.MainTexture;
            if (tex == null) tex = _defaultBrushTip;
            
            if (_brushMaterial != null) _brushMaterial.mainTexture = tex;

            _brushOpacity = strategy.Opacity;
            _sizeMultiplier = strategy.SizeMultiplier;
            _currentSize = _baseBrushSize * _sizeMultiplier;

            _stampGenerator.RotationMode = strategy.RotationMode;
            _stampGenerator.SpacingRatio = strategy.SpacingRatio;
            _stampGenerator.AngleJitter = strategy.AngleJitter;
            _stampGenerator.Reset();
            
            // Procedural settings
             if (_brushMaterial != null)
            {
                _brushMaterial.SetInt("_BlendOp", (int)strategy.BlendOp);
                _brushMaterial.SetInt("_SrcBlend", (int)strategy.SrcBlend);
                _brushMaterial.SetInt("_DstBlend", (int)strategy.DstBlend);
                _brushMaterial.SetFloat("_UseProcedural", strategy.UseProceduralSDF ? 1.0f : 0.0f);
                _brushMaterial.SetFloat("_EdgeSoftness", strategy.EdgeSoftness);
            }
        }

        public void SetBrushSize(float size)
        {
            _baseBrushSize = size;
            _currentSize = _baseBrushSize * _sizeMultiplier;
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

        public void DrawPoints(IEnumerable<LogicPoint> points)
        {
            if (_layoutController == null || _layoutController.ActiveRT == null) return;

            _cmd.Clear();
            _cmd.SetRenderTarget(_layoutController.ActiveRT);

            // Special Ghost Eraser Handling
            if (_isEraser)
            {
                // Draw Red Trail instead of erasing
                _brushMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                _brushMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                _brushMaterial.SetInt("_BlendOp", (int)BlendOp.Add);
                
                _props.SetColor("_Color", _eraserTrailColor);
            }
            else
            {
                Color drawColor = _brushColor;
                drawColor.a *= _brushOpacity;
                _props.SetColor("_Color", drawColor);
            }

            _cmd.SetViewMatrix(Matrix4x4.identity);
            _cmd.SetProjectionMatrix(Matrix4x4.Ortho(0, _layoutController.Resolution.x, 0, _layoutController.Resolution.y, -1, 1));

            // Generate stamps
            _stampGenerator.ProcessPoints(points, _currentSize, _stampBuffer);

            if (_stampBuffer.Count > 0)
            {
                foreach (var stamp in _stampBuffer)
                {
                    Matrix4x4 matrix = Matrix4x4.TRS(
                        new Vector3(stamp.Position.x, stamp.Position.y, 0),
                        Quaternion.Euler(0, 0, stamp.Rotation),
                        new Vector3(stamp.Size, stamp.Size, 1)
                    );
                    _cmd.DrawMesh(_quadMesh, matrix, _brushMaterial, 0, 0, _props);
                }
                
                Graphics.ExecuteCommandBuffer(_cmd);
            }
        }

        public void EndStroke()
        {
            _stampGenerator.Reset();
        }

        public void ClearCanvas()
        {
            _layoutController.ClearActiveRT();
            _stampGenerator.Reset();
        }

        public void SetBakingMode(bool enabled) { /* No-op for ghost */ }
        public void RestoreFromBackBuffer() { /* No-op for ghost */ }

        // --- Helpers ---

        private Mesh CreateQuad()
        {
            Mesh mesh = new Mesh();
            mesh.vertices = new Vector3[] {
                new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0),
                new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0)
            };
            mesh.uv = new Vector2[] {
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 1), new Vector2(1, 1)
            };
            mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
            return mesh;
        }
    }
}
