using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using Features.Drawing.Presentation;

namespace Features.Drawing.Presentation.UI
{
    public class DrawingToolbar : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CanvasRenderer _renderer;
        
        // Panels
        [SerializeField] private GameObject _panelEraser;
        [SerializeField] private GameObject _panelBrush;
        
        // Main Toolbar Buttons
        [SerializeField] private Button _btnMainEraser;
        [SerializeField] private Button _btnMainBrush;
        [SerializeField] private Button _btnMainClear;

        // Eraser Panel Elements
        [SerializeField] private Button[] _eraserSizeButtons; // 5 dots

        // Brush Panel Elements
        [SerializeField] private Button[] _brushSizeButtons; // 5 dots
        [SerializeField] private Button[] _brushColorButtons;
        [SerializeField] private Button _btnTypeSoft;
        [SerializeField] private Button _btnTypeHard;
        [SerializeField] private Button _btnTypeMarker;
        [SerializeField] private Button _btnTypePencil;

        [Header("Assets")]
        [SerializeField] private Texture2D _softBrushTex;
        [SerializeField] private Texture2D _hardBrushTex;
        [SerializeField] private Texture2D _markerBrushTex;
        [SerializeField] private Texture2D _pencilBrushTex;

        // State
        private float[] _sizePresets = new float[] { 10f, 30f, 50f, 80f, 120f };
        private Texture2D _runtimeHardBrush; // Cache for generated texture

        private Texture2D GenerateSharpHardBrush()
        {
            int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            
            Color[] colors = new Color[size * size];
            Vector2 center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    // Ultra sharp falloff (0.5px) to minimize overlap artifacts
                    float alpha = 1.0f - Mathf.SmoothStep(radius - 0.25f, radius + 0.25f, dist);
                    colors[y * size + x] = new Color(1, 1, 1, alpha);
                }
            }
            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }
        
        private void Start()
        {
            if (_renderer == null) _renderer = FindObjectOfType<CanvasRenderer>();

            // --- Main Toolbar Setup ---
            if (_btnMainEraser) _btnMainEraser.onClick.AddListener(OnMainEraserClick);
            if (_btnMainBrush) _btnMainBrush.onClick.AddListener(OnMainBrushClick);
            if (_btnMainClear) _btnMainClear.onClick.AddListener(OnClearClick);

            // --- Eraser Panel Setup ---
            if (_eraserSizeButtons != null)
            {
                for (int i = 0; i < _eraserSizeButtons.Length; i++)
                {
                    float size = _sizePresets[Mathf.Clamp(i, 0, _sizePresets.Length - 1)];
                    int idx = i; // capture
                    if (_eraserSizeButtons[i]) 
                        _eraserSizeButtons[i].onClick.AddListener(() => SetEraserSize(size));
                }
            }

            // --- Brush Panel Setup ---
            // 1. Sizes
            if (_brushSizeButtons != null)
            {
                for (int i = 0; i < _brushSizeButtons.Length; i++)
                {
                    float size = _sizePresets[Mathf.Clamp(i, 0, _sizePresets.Length - 1)];
                    if (_brushSizeButtons[i])
                        _brushSizeButtons[i].onClick.AddListener(() => SetBrushSize(size));
                }
            }

            // 2. Colors
            // Assuming buttons are assigned in order: Black, Red, Blue, Green, Orange...
            Color[] presetColors = new Color[] { 
                Color.black, Color.red, Color.blue, Color.green, new Color(1f, 0.5f, 0f) 
            };
            if (_brushColorButtons != null)
            {
                for (int i = 0; i < _brushColorButtons.Length; i++)
                {
                    Color c = presetColors[Mathf.Clamp(i, 0, presetColors.Length - 1)];
                    if (_brushColorButtons[i])
                        _brushColorButtons[i].onClick.AddListener(() => SetBrushColor(c));
                }
            }

            // 3. Types
            if (_btnTypeSoft) _btnTypeSoft.onClick.AddListener(() => SetBrushType(0));
            if (_btnTypeHard) _btnTypeHard.onClick.AddListener(() => SetBrushType(1));
            if (_btnTypeMarker) _btnTypeMarker.onClick.AddListener(() => SetBrushType(2));
            if (_btnTypePencil) _btnTypePencil.onClick.AddListener(() => SetBrushType(3));

            // Init state: Open Brush panel by default
            OnMainBrushClick();
        }

        private void OnMainEraserClick()
        {
            // Toggle UI
            if (_panelEraser) _panelEraser.SetActive(true);
            if (_panelBrush) _panelBrush.SetActive(false);
            
            // Logic
            _renderer.SetEraser(true);
            Debug.Log("Mode: Eraser");
        }

        private void OnMainBrushClick()
        {
            // Toggle UI
            if (_panelEraser) _panelEraser.SetActive(false);
            if (_panelBrush) _panelBrush.SetActive(true);
            
            // Logic
            _renderer.SetEraser(false);
            Debug.Log("Mode: Brush");
        }

        private void OnClearClick()
        {
            _renderer.Clear();
        }

        private void SetEraserSize(float size)
        {
            _renderer.SetBrushSize(size);
            Debug.Log($"Eraser Size: {size}");
        }

        private void SetBrushSize(float size)
        {
            _renderer.SetBrushSize(size);
            Debug.Log($"Brush Size: {size}");
        }

        private void SetBrushColor(Color c)
        {
            _renderer.SetBrushColor(c);
        }

        private void SetBrushType(int type)
        {
            _renderer.SetEraser(false);

            // 0=Soft, 1=Hard, 2=Marker, 3=Pencil
            switch (type)
            {
                case 0: // Soft
                    _renderer.SetBlendMode(BlendOp.Add, BlendMode.One, BlendMode.OneMinusSrcAlpha); // Premultiplied Standard
                    _renderer.SetBrushTexture(_softBrushTex);
                    _renderer.SetRotationMode(BrushRotationMode.None);
                    _renderer.SetBrushOpacity(1.0f);
                    _renderer.SetSpacingRatio(0.15f); // Standard
                    _renderer.SetAngleJitter(0f); // No jitter
                    break;
                case 1: // Hard
                    // Revert to Standard Blending (Max blend breaks dark-on-light layering)
                    _renderer.SetBlendMode(BlendOp.Add, BlendMode.One, BlendMode.OneMinusSrcAlpha); // Premultiplied Standard
                    
                    Texture2D hardTex = _hardBrushTex;
                    if (hardTex == null)
                    {
                        if (_runtimeHardBrush == null) _runtimeHardBrush = GenerateSharpHardBrush();
                        hardTex = _runtimeHardBrush;
                    }
                    _renderer.SetBrushTexture(hardTex);
                    
                    _renderer.SetRotationMode(BrushRotationMode.None);
                    _renderer.SetBrushOpacity(1.0f);
                    _renderer.SetSpacingRatio(0.025f); // Dense but safe
                    _renderer.SetAngleJitter(0f); // ABSOLUTELY NO JITTER
                    break;
                case 2: // Marker
                    _renderer.SetBlendMode(BlendOp.Add, BlendMode.One, BlendMode.OneMinusSrcAlpha); // Premultiplied Standard
                    _renderer.SetBrushTexture(_markerBrushTex);
                    _renderer.SetRotationMode(BrushRotationMode.Follow);
                    _renderer.SetBrushOpacity(0.5f);
                    _renderer.SetSpacingRatio(0.1f);
                    _renderer.SetAngleJitter(5f); // Slight jitter for organic feel
                    break;
                case 3: // Pencil
                    _renderer.SetBlendMode(BlendOp.Add, BlendMode.One, BlendMode.OneMinusSrcAlpha); // Premultiplied Standard
                    _renderer.SetBrushTexture(_pencilBrushTex);
                    _renderer.SetRotationMode(BrushRotationMode.None);
                    _renderer.SetBrushOpacity(0.8f); // Slightly transparent
                    _renderer.SetSpacingRatio(0.1f);
                    _renderer.SetAngleJitter(180f); // High jitter for noisy pencil
                    break;
            }
        }
    }
}
