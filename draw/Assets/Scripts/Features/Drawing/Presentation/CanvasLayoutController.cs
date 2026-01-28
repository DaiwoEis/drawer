using UnityEngine;
using UnityEngine.UI;

namespace Features.Drawing.Presentation
{
    /// <summary>
    /// Manages the Canvas resolution, aspect ratio, and RenderTextures.
    /// Extracted from CanvasRenderer to separate layout logic from rendering logic.
    /// </summary>
    public class CanvasLayoutController
    {
        // Dependencies / Config
        private readonly RawImage _displayImage;
        private int _baseMaxDimension;
        
        // State
        private Vector2Int _resolution;
        private RenderTexture _activeRT;
        private RenderTexture _bakedRT;
        
        // Cache for detecting changes
        private Vector2Int _lastResolution;
        private float _lastDisplayWidth = -1f;
        private float _lastDisplayHeight = -1f;
        private float _lastCanvasScale = -1f;

        // Events
        public event System.Action OnLayoutChanged;

        // Public Accessors
        public Vector2Int Resolution => _resolution;
        public RenderTexture ActiveRT => _activeRT;
        public RenderTexture BakedRT => _bakedRT;

        public CanvasLayoutController(RawImage displayImage, Vector2Int initialResolution, int baseMaxDimension)
        {
            _displayImage = displayImage;
            _resolution = initialResolution;
            _baseMaxDimension = baseMaxDimension;
            
            if (_baseMaxDimension <= 0)
            {
                _baseMaxDimension = Mathf.Max(_resolution.x, _resolution.y);
            }
        }

        public void Initialize()
        {
            // Auto-adjust resolution to match screen aspect ratio
            float aspect = GetAspectRatio();
            
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

            RebuildRenderTexture(_resolution, false);
            RebuildBakedRenderTexture();
        }

        public void Release()
        {
            if (_activeRT != null) 
            {
                if (RenderTexture.active == _activeRT) RenderTexture.active = null;
                _activeRT.Release(); 
                Object.Destroy(_activeRT); 
                _activeRT = null; 
            }
            if (_bakedRT != null) 
            {
                if (RenderTexture.active == _bakedRT) RenderTexture.active = null;
                _bakedRT.Release(); 
                Object.Destroy(_bakedRT); 
                _bakedRT = null; 
            }
        }

        public void CheckLayoutChanges()
        {
            float displayWidth;
            float displayHeight;
            float canvasScale;
            GetBrushSizeScale(out displayWidth, out displayHeight, out canvasScale);

            UpdateRenderTextureIfNeeded(displayWidth, displayHeight);
            
            if (_lastResolution != _resolution ||
                !Mathf.Approximately(_lastDisplayWidth, displayWidth) ||
                !Mathf.Approximately(_lastDisplayHeight, displayHeight) ||
                !Mathf.Approximately(_lastCanvasScale, canvasScale))
            {
                _lastResolution = _resolution;
                _lastDisplayWidth = displayWidth;
                _lastDisplayHeight = displayHeight;
                _lastCanvasScale = canvasScale;
                
                OnLayoutChanged?.Invoke();
            }
        }

        private float GetAspectRatio()
        {
            if (_displayImage != null && _displayImage.rectTransform != null && _displayImage.rectTransform.rect.width > 0)
            {
                return _displayImage.rectTransform.rect.width / _displayImage.rectTransform.rect.height;
            }
            return (float)Screen.width / Screen.height;
        }

        public float GetBrushSizeScale(out float displayWidth, out float displayHeight, out float canvasScale)
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
        
        public float GetBrushSizeScale()
        {
            float w, h, s;
            return GetBrushSizeScale(out w, out h, out s);
        }

        private void UpdateRenderTextureIfNeeded(float displayWidth, float displayHeight)
        {
            if (displayWidth <= 0f || displayHeight <= 0f) return;

            float aspect = displayWidth / displayHeight;
            Vector2Int targetResolution = CalculateResolution(aspect);

            if (_activeRT == null || targetResolution != _resolution)
            {
                RebuildRenderTexture(targetResolution, _activeRT != null);
                RebuildBakedRenderTexture();
            }
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
            if (targetResolution.x <= 0 || targetResolution.y <= 0) return;

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
                Object.Destroy(_activeRT);
            }

            _activeRT = newRT;
            _resolution = targetResolution;

            if (_displayImage != null)
            {
                _displayImage.texture = _activeRT;
            }
        }

        public void RebuildBakedRenderTexture()
        {
            if (_bakedRT != null)
            {
                _bakedRT.Release();
                Object.Destroy(_bakedRT);
            }
            _bakedRT = new RenderTexture(_resolution.x, _resolution.y, 0, RenderTextureFormat.ARGB32);
            _bakedRT.filterMode = FilterMode.Bilinear;
            _bakedRT.useMipMap = false;
            _bakedRT.Create();
            
            var prev = RenderTexture.active;
            RenderTexture.active = _bakedRT;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = prev;
        }
        
        public void ClearActiveRT()
        {
            if (_activeRT == null) return;
            var prev = RenderTexture.active;
            RenderTexture.active = _activeRT;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = prev;
        }
    }
}
