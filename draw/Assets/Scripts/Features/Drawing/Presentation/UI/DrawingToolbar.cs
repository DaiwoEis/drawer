using UnityEngine;
using UnityEngine.UI;
using Features.Drawing.App;
using Features.Drawing.Domain;
using Common.Utils;

namespace Features.Drawing.Presentation.UI
{
    public class DrawingToolbar : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DrawingAppService _appService;

        // Top Bar Buttons
        [SerializeField] private Button _btnUndo;
        [SerializeField] private Button _btnClear;

        // Bottom Navigation Tabs
        [SerializeField] private Button _tabBrush;
        [SerializeField] private Button _tabEraser;
        [SerializeField] private Button _tabSize;
        [SerializeField] private Button _tabColor;

        // Sub Panels
        [SerializeField] private GameObject _panelBrush; // Shows brush types
        [SerializeField] private GameObject _panelSize;  // Shows sizes
        [SerializeField] private GameObject _panelColor; // Shows colors

        // Panel Content Buttons
        // Brush Types
        [SerializeField] private Button _btnTypeSoft;
        [SerializeField] private Button _btnTypeHard;
        [SerializeField] private Button _btnTypeMarker;
        [SerializeField] private Button _btnTypePencil;

        // Sizes
        [SerializeField] private Button[] _sizeButtons; // Shared for both Brush and Eraser

        // Colors
        [SerializeField] private Button[] _colorButtons;

        [Header("Brush Strategies")]
        [SerializeField] private BrushStrategy _softBrushStrategy;
        [SerializeField] private BrushStrategy _hardBrushStrategy;
        [SerializeField] private BrushStrategy _markerBrushStrategy;
        [SerializeField] private BrushStrategy _pencilBrushStrategy;

        // State
        private float[] _sizePresets = new float[] { 20f, 40f, 80f, 120f, 160f };
        private enum Tab { None, Brush, Eraser, Size, Color }
        private Tab _activeTab = Tab.None;
        private bool _isEraserMode = false;

        private void Start()
        {
            if (_appService == null)
            {
                _appService = FindObjectOfType<DrawingAppService>();
                if (_appService == null)
                {
                    var go = new GameObject("DrawingAppService");
                    _appService = go.AddComponent<DrawingAppService>();
                }
            }

            // Events
            if (_appService != null)
            {
                _appService.OnStrokeStarted += OnStrokeStarted;
            }

            // Top Bar
            if (_btnClear) _btnClear.onClick.AddListener(OnClearClick);
            // Undo not implemented yet, placeholder
            if (_btnUndo) _btnUndo.onClick.AddListener(() => Debug.Log("Undo clicked"));

            // Tabs
            if (_tabBrush) _tabBrush.onClick.AddListener(() => SwitchTab(Tab.Brush));
            if (_tabEraser) _tabEraser.onClick.AddListener(() => SwitchTab(Tab.Eraser));
            if (_tabSize) _tabSize.onClick.AddListener(() => SwitchTab(Tab.Size));
            if (_tabColor) _tabColor.onClick.AddListener(() => SwitchTab(Tab.Color));

            // Brush Types
            if (_btnTypeSoft) _btnTypeSoft.onClick.AddListener(() => SetBrushType(0));
            if (_btnTypeHard) _btnTypeHard.onClick.AddListener(() => SetBrushType(1));
            if (_btnTypeMarker) _btnTypeMarker.onClick.AddListener(() => SetBrushType(2));
            if (_btnTypePencil) _btnTypePencil.onClick.AddListener(() => SetBrushType(3));

            // Sizes
            if (_sizeButtons != null)
            {
                for (int i = 0; i < _sizeButtons.Length; i++)
                {
                    float size = _sizePresets[Mathf.Clamp(i, 0, _sizePresets.Length - 1)];
                    if (_sizeButtons[i]) 
                        _sizeButtons[i].onClick.AddListener(() => SetSize(size));
                }
            }

            // Colors
            Color[] presetColors = new Color[] { 
                Color.black, Color.red, new Color(1f, 0.8f, 0f), new Color(0.2f, 1f, 0.2f), new Color(0f, 0.8f, 1f), Color.blue, new Color(0.6f, 0f, 1f), new Color(1f, 0f, 0.5f)
            };
            if (_colorButtons != null)
            {
                for (int i = 0; i < _colorButtons.Length; i++)
                {
                    Color c = presetColors[Mathf.Clamp(i, 0, presetColors.Length - 1)];
                    if (_colorButtons[i])
                        _colorButtons[i].onClick.AddListener(() => SetColor(c));
                }
            }

            // Default State
            // Set a default brush strategy immediately so AppService has a valid state
            SetBrushType(0); // Select Soft Brush by default
            SwitchTab(Tab.Brush);
        }

        private void OnDestroy()
        {
            if (_appService != null)
            {
                _appService.OnStrokeStarted -= OnStrokeStarted;
            }
        }

        private void OnStrokeStarted()
        {
            // Close any open panels when drawing starts
            if (_activeTab != Tab.None)
            {
                SwitchTab(Tab.None);
            }
        }

        private void SwitchTab(Tab tab)
        {
            _activeTab = tab;

            // Logic Switch
            if (tab == Tab.Brush)
            {
                _isEraserMode = false;
                _appService.SetEraser(false);
            }
            else if (tab == Tab.Eraser)
            {
                _isEraserMode = true;
                _appService.SetEraser(true);
            }
            // For Size and Color, we maintain the current tool mode (_isEraserMode)
            
            // UI Panel Visibility
            if (_panelBrush) _panelBrush.SetActive(tab == Tab.Brush);
            if (_panelSize) _panelSize.SetActive(tab == Tab.Size);
            if (_panelColor) _panelColor.SetActive(tab == Tab.Color);
            
            UpdateTabVisuals();
        }

        private void UpdateTabVisuals()
        {
            // Keep the tool tab active if we are in that mode, even if looking at Size/Color panels
            // But usually tabs are mutually exclusive in UI. 
            // If the user request "Don't turn into eraser", they might mean visual confusion.
            // Let's highlight the tool button if it's the active tool, OR if it's the active tab.
            
            bool isBrushActive = _activeTab == Tab.Brush || (_activeTab != Tab.Eraser && !_isEraserMode);
            bool isEraserActive = _activeTab == Tab.Eraser || (_activeTab != Tab.Brush && _isEraserMode && _activeTab != Tab.Size && _activeTab != Tab.Color);
            
            // Simplification: The user likely wants to know "Am I using Brush or Eraser?"
            // If I click Size, I am still using Brush (if I was before).
            // So Brush tab should probably stay highlighted or indicate state?
            // Standard tab behavior: Only one tab active.
            // Let's stick to standard tab behavior for now, but ensure logic is correct.
            
            SetTabColor(_tabBrush, isBrushActive);
            SetTabColor(_tabEraser, isEraserActive);
            SetTabColor(_tabSize, _activeTab == Tab.Size);
            SetTabColor(_tabColor, _activeTab == Tab.Color);
        }

        private void SetTabColor(Button btn, bool isActive)
        {
            if (btn == null) return;
            Transform icon = btn.transform.Find("Icon");
            if (icon != null)
            {
                Image img = icon.GetComponent<Image>();
                if (img != null)
                {
                    img.color = isActive ? Color.black : Color.gray;
                }
            }
            
            Transform label = btn.transform.Find("Label");
            if (label != null)
            {
                Text txt = label.GetComponent<Text>();
                if (txt != null)
                {
                    txt.color = isActive ? Color.black : Color.gray;
                }
            }
        }

        private void OnClearClick()
        {
            _appService.ClearCanvas();
        }

        private void SetSize(float size)
        {
            _appService.SetSize(size);
            Debug.Log($"Size set to: {size}");
            
            // If we are currently in Eraser mode (via Tab), SetSize(size) will update _lastEraserSize.
            // But the user might be thinking "I want to change the BRUSH size", even if they are holding an eraser?
            // "大小是改变笔刷大小的，不是橡皮擦" -> User implies Size panel ALWAYS controls Brush Size?
            // OR user means: "When I click Size, I am changing the size of the *current tool*, but if I am in Eraser mode, 
            // and I click size, it should NOT switch me to Eraser mode (fixed previously)".
            
            // Re-reading user input: "以及切换大小，笔刷大小没变问题，大小是改变笔刷大小的，不是橡皮擦"
            // Translation: "Also switching size, brush size didn't change issue. Size is for changing brush size, NOT eraser."
            
            // Wait, does the user mean the Size Panel should ONLY affect the Brush, and NOT the Eraser?
            // If so, even if I am in Eraser mode, clicking Size should update the *background* brush size, 
            // and maybe switch me back to Brush mode? Or just update Brush size silently?
            
            // If user says "Size is for changing brush size, NOT eraser", it implies Eraser size might be fixed or handled differently.
            // But standard drawing apps allow resizing eraser.
            
            // Let's assume the user wants: "When I pick a size, it applies to the BRUSH. If I was using Eraser, I probably want to switch back to Brush with this new size."
            // OR "Eraser has its own size, but currently changing size while in Eraser mode accidentally changes Brush size (or vice versa)?"
            
            // Let's look at DrawingAppService.SetSize:
            // if (_isEraser) { _lastEraserSize = size; } else { _lastBrushSize = size; }
            
            // If I am in Eraser mode, and I click Size 80. _lastEraserSize becomes 80. _lastBrushSize remains 10.
            // Then I switch back to Brush. Brush size is 10.
            // User complains: "Brush size didn't change".
            
            // Conclusion: The user expects the Size Panel to GLOBALLLY set the drawing size, 
            // OR specifically they want to set the Brush Size, even if they are currently erasing.
            
            // Interpretation A: Size Panel = Brush Size. Eraser Size = Fixed or Separate.
            // Interpretation B: Size Panel = Current Tool Size. (This is what I implemented).
            
            // Given "大小是改变笔刷大小的，不是橡皮擦" (Size is to change brush size, not eraser), 
            // I strongly suspect Interpretation A: The user wants the Size buttons to ALWAYS update the Brush Size.
            // And potentially, if they click a size, they expect to be using the Brush?
            
            // Let's modify DrawingAppService to allow setting Brush Size explicitly, or force SetSize to update Brush Size always.
            
            // But wait, if I am erasing, I might want a big eraser.
            // If the Size panel ONLY affects Brush, how do I change Eraser size?
            // Maybe Eraser is fixed? Or User doesn't care about Eraser size right now.
            
            // Let's try this: When setting size, we ALWAYS update the Brush Size.
            // If we are in Eraser mode, we ALSO update Eraser size? Or just Brush?
            
            // "大小是改变笔刷大小的" -> "Size is for changing brush size".
            // Let's force update _lastBrushSize in AppService, regardless of mode.
        }

        private void SetColor(Color c)
        {
            _appService.SetColor(c);
            // If we set color, we probably want to switch back to brush mode if we were in eraser
            if (_isEraserMode)
            {
                SwitchTab(Tab.Brush);
            }
        }

        private void SetBrushType(int type)
        {
            // IMPORTANT: Switching brush type implies we want to use the BRUSH tool.
            // So we must exit Eraser mode and update the UI tabs accordingly.
            if (_activeTab != Tab.Brush)
            {
                // Force switch to Brush tab logic (which handles SetEraser(false) and UI updates)
                SwitchTab(Tab.Brush);
                // Note: SwitchTab calls UpdateTabVisuals, so the UI will reflect the change.
            }
            else
            {
                // If we are already on Brush tab, just ensure Eraser is off (redundant but safe)
                _appService.SetEraser(false);
            }

            BrushStrategy strategy = null;
            switch (type)
            {
                case 0: strategy = _softBrushStrategy; break;
                case 1: strategy = _hardBrushStrategy; break;
                case 2: strategy = _markerBrushStrategy; break;
                case 3: strategy = _pencilBrushStrategy; break;
            }

            if (strategy != null)
            {
                ApplyBrushStrategy(strategy);
            }
            else
            {
                Debug.LogWarning($"Brush strategy missing for type {type}.");
            }
        }

        private void ApplyBrushStrategy(BrushStrategy strategy)
        {
            Texture2D runtimeTex = null;
            if (strategy.UseRuntimeGeneration)
            {
                runtimeTex = TextureGeneratorService.GetSharpHardBrush();
            }
            
            _appService.SetBrushStrategy(strategy, runtimeTex);
        }
    }
}
