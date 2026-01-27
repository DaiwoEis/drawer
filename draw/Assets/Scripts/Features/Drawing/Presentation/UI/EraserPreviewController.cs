using UnityEngine;
using UnityEngine.UI;
using Features.Drawing.App;
using Features.Drawing.Domain;
using Features.Drawing.Presentation;

namespace Features.Drawing.Presentation.UI
{
    /// <summary>
    /// Displays a visual preview of the eraser tool position and size.
    /// Renders a semi-transparent red overlay that follows the mouse/stylus.
    /// </summary>
    public class EraserPreviewController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DrawingAppService _appService;
        [SerializeField] private CanvasRenderer _canvasRenderer;
        [SerializeField] private RectTransform _inputArea;
        
        [Header("Visual Settings")]
        [SerializeField] private Color _previewColor = new Color(1f, 0f, 0f, 0.3f);
        [SerializeField] private Sprite _defaultCircleSprite;

        private GameObject _previewObj;
        private RectTransform _previewRect;
        private Image _previewImage;
        private Texture2D _lastTexture;
        private Sprite _generatedSprite;

        private void Start()
        {
            InitializeReferences();
            CreatePreviewObject();
        }

        private void InitializeReferences()
        {
            if (_appService == null)
                _appService = FindObjectOfType<DrawingAppService>();

            if (_canvasRenderer == null)
                _canvasRenderer = FindObjectOfType<CanvasRenderer>();

            // If input area is not assigned, try to find the one used by MouseInputProvider or default to this transform if it's a RectTransform
            if (_inputArea == null)
            {
                var inputProvider = FindObjectOfType<MouseInputProvider>();
                if (inputProvider != null)
                {
                    _inputArea = inputProvider.InputArea;
                }
                
                if (_inputArea == null)
                {
                     _inputArea = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
                }
            }
        }

        private void CreatePreviewObject()
        {
            if (_previewObj != null) return;

            _previewObj = new GameObject("EraserPreview");
            _previewObj.transform.SetParent(_inputArea != null ? _inputArea : transform, false);
            
            // Ensure it's last sibling to render on top of drawing
            _previewObj.transform.SetAsLastSibling();

            _previewRect = _previewObj.AddComponent<RectTransform>();
            _previewRect.anchorMin = new Vector2(0.5f, 0.5f);
            _previewRect.anchorMax = new Vector2(0.5f, 0.5f);
            _previewRect.pivot = new Vector2(0.5f, 0.5f);

            _previewImage = _previewObj.AddComponent<Image>();
            _previewImage.color = _previewColor;
            _previewImage.raycastTarget = false; // Pass through input
            
            // Generate default circle if needed
            if (_defaultCircleSprite == null)
            {
                _defaultCircleSprite = GenerateCircleSprite();
            }
            _previewImage.sprite = _defaultCircleSprite;
            
            _previewObj.SetActive(false);
        }

        private Sprite GenerateCircleSprite()
        {
            int res = 64;
            Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            Color[] colors = new Color[res * res];
            float center = res * 0.5f;
            float radius = res * 0.45f;
            float radiusSqr = radius * radius;

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distSqr = dx * dx + dy * dy;
                    
                    // Simple AA circle
                    float alpha = 1.0f;
                    if (distSqr > radiusSqr)
                    {
                        alpha = Mathf.Clamp01(radius + 1.0f - Mathf.Sqrt(distSqr));
                    }
                    
                    colors[y * res + x] = new Color(1, 1, 1, alpha);
                }
            }
            
            tex.SetPixels(colors);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f));
        }

        private void Update()
        {
            if (_appService == null || _canvasRenderer == null || _previewObj == null || _inputArea == null) return;

            bool show = _appService.IsEraser;
            
            // Also check if mouse is inside input area? 
            // The user requirement says "follow mouse", usually we only show it when cursor is valid.
            // But simple on/off based on tool is a good start.
            
            if (show)
            {
                if (!_previewObj.activeSelf) _previewObj.SetActive(true);
                UpdatePreview();
            }
            else
            {
                if (_previewObj.activeSelf) _previewObj.SetActive(false);
            }
        }

        private void UpdatePreview()
        {
            // 1. Update Position
            Vector2 screenPos = Input.mousePosition;
            Camera worldCam = null;
            if (_inputArea.GetComponentInParent<Canvas>().renderMode != RenderMode.ScreenSpaceOverlay)
            {
                worldCam = _inputArea.GetComponentInParent<Canvas>().worldCamera;
                if (worldCam == null) worldCam = Camera.main;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_inputArea, screenPos, worldCam, out Vector2 localPos))
            {
                _previewRect.anchoredPosition = localPos;
            }

            // 2. Update Size
            // Calculate scale ratio
            Vector2Int rtRes = _canvasRenderer.Resolution;
            if (rtRes.x <= 0 || rtRes.y <= 0) return;

            Rect uiRect = _inputArea.rect;
            float scaleX = uiRect.width / rtRes.x;
            float scaleY = uiRect.height / rtRes.y;
            
            // Use the smaller scale to fit (aspect ratio fit)
            // Or assume uniform scale if aspect ratios match.
            // In CanvasRenderer, we fit RT into UI.
            float scale = Mathf.Min(scaleX, scaleY);
            
            float brushSizePixels = _appService.CurrentSize;
            
            // Apply BrushStrategy Size Multiplier if needed?
            // DrawingAppService.CurrentSize already includes it?
            // Checking DrawingAppService: SetSize sets _currentSize. 
            // CanvasRenderer.SetBrushSize sets _baseBrushSize, then multiplies by _sizeMultiplier.
            // DrawingAppService doesn't know about _sizeMultiplier inside renderer, 
            // BUT DrawingAppService calls _renderer.SetBrushSize(size).
            // AND CanvasRenderer applies multiplier internally.
            
            // So _appService.CurrentSize is the BASE size.
            // We need to apply the strategy multiplier to get the true visual size.
            float multiplier = 1.0f;
            BrushStrategy strategy = _appService.EraserStrategy;
            if (strategy != null)
            {
                multiplier = strategy.SizeMultiplier;
                
                // 3. Update Sprite if strategy changes
                UpdateSprite(strategy);
            }
            
            float finalSize = brushSizePixels * multiplier * scale;
            _previewRect.sizeDelta = new Vector2(finalSize, finalSize);
        }

        private void UpdateSprite(BrushStrategy strategy)
        {
            Texture2D tex = strategy.MainTexture;
            
            // If strategy uses procedural, we might want to stick with default circle
            // If strategy has a texture, use it.
            if (tex != null && tex != _lastTexture)
            {
                _lastTexture = tex;
                if (_generatedSprite != null) Destroy(_generatedSprite); // Cleanup previous
                
                _generatedSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                _previewImage.sprite = _generatedSprite;
            }
            else if (tex == null && _lastTexture != null)
            {
                // Revert to default
                _lastTexture = null;
                _previewImage.sprite = _defaultCircleSprite;
            }
        }

        private void OnDestroy()
        {
            if (_generatedSprite != null) Destroy(_generatedSprite);
            if (_defaultCircleSprite != null) Destroy(_defaultCircleSprite);
            // Note: Don't destroy _previewObj if it's part of the scene, but here we created it dynamically.
            // If we created it, we should destroy it.
            if (_previewObj != null) Destroy(_previewObj);
        }
    }
}
