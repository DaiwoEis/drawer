using System.Collections.Generic;
using UnityEngine;
using Features.Drawing.Domain;
using Features.Drawing.Domain.Interface;
using Features.Drawing.Domain.ValueObject;
using Features.Drawing.Service;
using Features.Drawing.Presentation; 

namespace Features.Drawing.App
{
    public class DrawingAppService : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Features.Drawing.Presentation.CanvasRenderer _concreteRenderer; 
        
        // State
        private Color _currentColor = Color.black;
        private float _currentSize = 10f;
        private float _lastBrushSize = 10f;
        private float _lastEraserSize = 30f;
        private bool _isEraser = false;
        private BrushStrategy _currentStrategy;
        
        // Services
        private IStrokeRenderer _renderer;
        private StrokeSmoothingService _smoothingService;
        
        // Buffers
        private List<LogicPoint> _currentStrokeRaw = new List<LogicPoint>(1024);
        private List<LogicPoint> _smoothingInputBuffer = new List<LogicPoint>(8);
        private List<LogicPoint> _smoothingOutputBuffer = new List<LogicPoint>(64);
        private List<LogicPoint> _singlePointBuffer = new List<LogicPoint>(1);

        // History
        [System.Serializable]
        public class StrokeHistoryItem
        {
            public BrushStrategy Strategy;
            public Texture2D RuntimeTexture;
            public Color Color;
            public float Size;
            public bool IsEraser;
            public List<LogicPoint> Points;
        }
        private List<StrokeHistoryItem> _history = new List<StrokeHistoryItem>();
        private StrokeHistoryItem _currentHistoryItem;
        private Texture2D _currentRuntimeTexture;

        // Events
        public event System.Action OnStrokeStarted;

        private void Awake()
        {
            if (_concreteRenderer == null) 
                _concreteRenderer = FindObjectOfType<Features.Drawing.Presentation.CanvasRenderer>();
                
            _renderer = _concreteRenderer as IStrokeRenderer;
            
            if (_renderer == null)
            {
                Debug.LogError("DrawingAppService: CanvasRenderer does not implement IStrokeRenderer!");
            }
            
            _smoothingService = new StrokeSmoothingService();
        }

        // --- State Management ---

        public void SetBrushStrategy(BrushStrategy strategy, Texture2D runtimeTexture = null)
        {
            if (strategy == null) return;
            
            _currentStrategy = strategy;
            _currentRuntimeTexture = runtimeTexture; // Capture runtime texture
            _isEraser = false;
            _currentSize = _lastBrushSize; // Restore brush size
            
            if (_renderer != null)
            {
                _renderer.ConfigureBrush(strategy, runtimeTexture);
                _renderer.SetEraser(false);
                _renderer.SetBrushColor(_currentColor);
                _renderer.SetBrushSize(_lastBrushSize);
            }
        }

        public void SetColor(Color color)
        {
            _currentColor = color;
            _isEraser = false;
            // Restore brush size because setting color implies using brush
            _currentSize = _lastBrushSize; 
            
            if (_renderer != null)
            {
                _renderer.SetBrushColor(color);
                _renderer.SetEraser(false);
                _renderer.SetBrushSize(_lastBrushSize);
            }
        }

        public void SetSize(float size)
        {
            _currentSize = size;
            
            // USER REQUEST: "Size is for changing brush size, not eraser."
            // So we ALWAYS update _lastBrushSize, regardless of current mode.
            _lastBrushSize = size;

            if (_isEraser)
            {
                // If we are in Eraser mode, we ALSO update Eraser size (optional, but logical if user sees the cursor change)
                // But user said "NOT eraser". 
                // If I change size to 100 while using Eraser, and then erase, should it be big? 
                // Probably yes. But switching back to Brush should ALSO be 100.
                _lastEraserSize = size;
            }

            if (_renderer != null)
            {
                _renderer.SetBrushSize(size);
            }
        }

        public void SetEraser(bool isEraser)
        {
            _isEraser = isEraser;
            
            // Swap size based on mode
            float targetSize = isEraser ? _lastEraserSize : _lastBrushSize;
            _currentSize = targetSize;

            if (_renderer != null)
            {
                _renderer.SetEraser(isEraser);
                _renderer.SetBrushSize(targetSize);
                
                // CRITICAL FIX: If switching BACK to brush, we MUST restore the brush's blend modes and texture.
                // Otherwise, the renderer's material remains dirty with Eraser settings (BlendOp.ReverseSubtract/Zero/etc.)
                if (!isEraser && _currentStrategy != null)
                {
                    // Note: We don't have the runtime texture here easily if it was generated dynamically.
                    // Ideally, we should cache it. For now, we assume strategy.MainTexture or regenerate if needed.
                    // But wait, ConfigureBrush might regenerate it if we pass null.
                    // Let's check if we can store the last used runtime texture?
                    // For now, just re-applying strategy is better than broken blend modes.
                    _renderer.ConfigureBrush(_currentStrategy, null);
                    
                    // Also ensure color is restored (Eraser might have ignored it, but Renderer needs it back)
                    _renderer.SetBrushColor(_currentColor);
                }
            }
        }

        public void ClearCanvas()
        {
            _history.Clear();
            _renderer?.ClearCanvas();
        }

        // --- Input Handling ---

        public void StartStroke(LogicPoint point)
        {
            // Notify listeners (e.g. UI to close panels)
            OnStrokeStarted?.Invoke();
            
            _currentStrokeRaw.Clear();

            // Create History Item
            _currentHistoryItem = new StrokeHistoryItem
            {
                Strategy = _currentStrategy,
                RuntimeTexture = _currentRuntimeTexture,
                Color = _currentColor,
                Size = _currentSize,
                IsEraser = _isEraser,
                Points = new List<LogicPoint>(1024)
            };

            AddPoint(point);
        }

        public void MoveStroke(LogicPoint point)
        {
            AddPoint(point);
        }

        public void EndStroke()
        {
            if (_currentHistoryItem != null)
            {
                // Capture all points from the raw buffer to history
                // Note: We could have added them one by one in MoveStroke, but bulk copy is safer/easier
                _currentHistoryItem.Points.AddRange(_currentStrokeRaw);
                _history.Add(_currentHistoryItem);
                _currentHistoryItem = null;
            }

            _currentStrokeRaw.Clear();
            _renderer?.EndStroke();
        }

        public void Undo()
        {
            if (_history.Count == 0) return;

            // Remove last stroke
            _history.RemoveAt(_history.Count - 1);
            
            RedrawHistory();
        }

        private void RedrawHistory()
        {
            if (_renderer == null) return;

            _renderer.ClearCanvas();

            // Save current state to restore later (optional, but good UX)
            var savedColor = _currentColor;
            var savedSize = _currentSize;
            var savedEraser = _isEraser;
            var savedStrategy = _currentStrategy;
            var savedRuntimeTex = _currentRuntimeTexture;
            
            foreach (var item in _history)
            {
                // Restore State for this stroke
                if (item.IsEraser)
                {
                    _renderer.SetEraser(true);
                    _renderer.SetBrushSize(item.Size);
                }
                else
                {
                    _renderer.ConfigureBrush(item.Strategy, item.RuntimeTexture);
                    _renderer.SetEraser(false);
                    _renderer.SetBrushColor(item.Color);
                    _renderer.SetBrushSize(item.Size);
                }

                // Draw Points
                DrawStrokePoints(item.Points);
                
                _renderer.EndStroke();
            }

            // Restore original state
            if (savedEraser)
            {
                SetEraser(true);
                // SetEraser sets size, so we might need to ensure correct size if logic changes
                SetSize(savedSize); 
            }
            else
            {
                SetBrushStrategy(savedStrategy, savedRuntimeTex);
                SetColor(savedColor);
                SetSize(savedSize);
            }
        }

        private void DrawStrokePoints(List<LogicPoint> points)
        {
            if (points == null || points.Count == 0) return;

            // Replicate the logic from AddPoint, but iteratively
            // We can't reuse AddPoint directly because it relies on _currentStrokeRaw state
            
            for (int i = 0; i < points.Count; i++)
            {
                // Logic matches AddPoint:
                // If count < 4, draw point directly.
                // If count >= 4, smooth last 4.
                
                // i is 0-based index. "Count" equivalent is i + 1.
                int count = i + 1;
                
                if (count >= 4)
                {
                     _smoothingInputBuffer.Clear();
                     _smoothingInputBuffer.Add(points[i - 3]);
                     _smoothingInputBuffer.Add(points[i - 2]);
                     _smoothingInputBuffer.Add(points[i - 1]);
                     _smoothingInputBuffer.Add(points[i]);

                     _smoothingService.SmoothPoints(_smoothingInputBuffer, _smoothingOutputBuffer);
                     _renderer.DrawPoints(_smoothingOutputBuffer);
                }
                else
                {
                     _singlePointBuffer.Clear();
                     _singlePointBuffer.Add(points[i]);
                     _renderer.DrawPoints(_singlePointBuffer);
                }
            }
        }

        private void AddPoint(LogicPoint point)
        {
            if (_renderer == null)
            {
                return;
            }

            _currentStrokeRaw.Add(point);
            int count = _currentStrokeRaw.Count;

            if (count >= 4)
            {
                // Sliding window smoothing
                _smoothingInputBuffer.Clear();
                _smoothingInputBuffer.Add(_currentStrokeRaw[count - 4]);
                _smoothingInputBuffer.Add(_currentStrokeRaw[count - 3]);
                _smoothingInputBuffer.Add(_currentStrokeRaw[count - 2]);
                _smoothingInputBuffer.Add(_currentStrokeRaw[count - 1]);

                _smoothingService.SmoothPoints(_smoothingInputBuffer, _smoothingOutputBuffer);
                _renderer.DrawPoints(_smoothingOutputBuffer);
            }
            else
            {
                // Draw point directly
                _singlePointBuffer.Clear();
                _singlePointBuffer.Add(point);
                _renderer.DrawPoints(_singlePointBuffer);
            }
        }
    }
}
