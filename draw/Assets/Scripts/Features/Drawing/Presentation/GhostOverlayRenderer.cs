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
    /// Uses Retained Mode (Clear & Redraw per frame) to support Extrapolation/Prediction.
    /// </summary>
    public class GhostOverlayRenderer : BaseStrokeRenderer
    {
        [Header("References")]
        [SerializeField] private CanvasRenderer _mainRenderer;
        [SerializeField] private RawImage _displayImage;
        // _brushShader and _defaultBrushTip are in BaseStrokeRenderer

        [Header("Ghost Settings")]
        [SerializeField] private Color _eraserTrailColor = new Color(1f, 0f, 0f, 0.2f); // Semi-transparent red

        // State
        private CanvasLayoutController _layoutController;
        // _brushMaterial, _cmd, _quadMesh, _props, _matrices are in BaseStrokeRenderer
        
        private StrokeStampGenerator _stampGenerator = new StrokeStampGenerator();
        private List<StampData> _stampBuffer = new List<StampData>(1024);

        // Brush State
        private float _brushOpacity = 1f;

        private void Awake()
        {
            if (_mainRenderer == null)
                _mainRenderer = FindObjectOfType<CanvasRenderer>();
                
            InitializeGraphics("GhostBuffer");
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

        protected override void OnDestroy()
        {
            if (_mainRenderer != null)
                _mainRenderer.OnResolutionChanged -= OnMainResolutionChanged;

            _layoutController?.Release();
            
            base.OnDestroy();
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

        public Vector2Int Resolution => _layoutController != null ? _layoutController.Resolution : Vector2Int.zero;

        public float GetBrushSizeScale()
        {
            return _layoutController != null ? _layoutController.GetBrushSizeScale() : 1f;
        }

        // InitializeGraphics is inherited from BaseStrokeRenderer

        // --- IStrokeRenderer Implementation ---

        public void ConfigureBrush(BrushStrategy strategy, Texture2D runtimeTexture = null)
        {
            if (strategy == null) return;

            Texture2D tex = runtimeTexture != null ? runtimeTexture : strategy.MainTexture;
            if (tex == null) tex = _defaultBrushTip;
            
            if (_brushMaterial != null) _brushMaterial.mainTexture = tex;

            _brushOpacity = strategy.Opacity;
            // _sizeMultiplier = strategy.SizeMultiplier; // Removed, applied on input size if needed, but here we just take raw size from network
            // _currentSize = _baseBrushSize * _sizeMultiplier; // Removed

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

        // Removed SetBrushSize/SetBrushColor as they are stateful and replaced by DrawGhostStroke arguments
        // public void SetBrushSize(float size) { ... }
        // public void SetBrushColor(Color color) { ... }

        // --- Retained Mode Support (For Extrapolation) ---

        public void BeginFrame()
        {
            if (_layoutController == null || _layoutController.ActiveRT == null) return;
            
            // Clear the Active RT completely
            _layoutController.ClearActiveRT();
        }

        public void DrawGhostStroke(IEnumerable<LogicPoint> points, float size, Color color, bool isEraser, BrushStrategy strategy)
        {
            if (_layoutController == null || _layoutController.ActiveRT == null) return;

            // Configure Brush immediately before drawing
            // This ensures we use the correct texture, spacing, and other settings
            if (strategy != null)
            {
                ConfigureBrush(strategy);
            }

            // Reset generator state for this fresh stroke draw
            _stampGenerator.Reset();
            
            // Generate stamps
            _stampGenerator.ProcessPoints(points, size, _stampBuffer);
            DrawStampsInternal(_stampBuffer, color, isEraser, strategy);
            _stampBuffer.Clear();
        }

        public void DrawGhostStamps(List<StampData> stamps, Color color, bool isEraser, BrushStrategy strategy)
        {
            if (_layoutController == null || _layoutController.ActiveRT == null) return;
            DrawStampsInternal(stamps, color, isEraser, strategy);
        }

        public void EndStroke()
        {
            _stampGenerator.Reset();
        }

        private void DrawStampsInternal(List<StampData> stamps, Color color, bool isEraser, BrushStrategy strategy)
        {
            if (stamps == null || stamps.Count == 0) return;

            if (strategy != null)
            {
                ConfigureBrush(strategy);
            }

            // Ghost strokes always use red trail for eraser
            Color finalColor = isEraser ? _eraserTrailColor : color;

            DrawStampsBatch(
                stamps,
                _layoutController.ActiveRT,
                _layoutController.Resolution,
                finalColor,
                _brushOpacity,
                isEraser,
                useEraserRedTrail: true
            );
        }

        // Unused legacy methods from IStrokeRenderer
        // public void ClearCanvas() { ... }
        // public void SetBakingMode(bool enabled) { ... }
        // public void RestoreFromBackBuffer() { ... }

        // --- Helpers ---

        // CreateQuad is inherited from BaseStrokeRenderer
    }
}
