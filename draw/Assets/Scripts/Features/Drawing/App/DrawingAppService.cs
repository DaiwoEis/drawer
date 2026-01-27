using System.Collections.Generic;
using UnityEngine;
using Features.Drawing.Domain;
using Features.Drawing.Domain.Interface;
using Features.Drawing.Domain.ValueObject;
using Features.Drawing.Service;
using Features.Drawing.Presentation; 
using Features.Drawing.Domain.Algorithm;
using Features.Drawing.Domain.Data;
using Features.Drawing.Domain.Entity;
using Common.Constants;
using Features.Drawing.App.Command;
using Features.Drawing.App.Interface;

namespace Features.Drawing.App
{
    /// <summary>
    /// Facade service that coordinates input, domain logic, and rendering.
    /// This is the main entry point for the drawing feature.
    /// </summary>
    public class DrawingAppService : MonoBehaviour, IInputHandler
    {
        [Header("References")]
        [SerializeField] private Features.Drawing.Presentation.CanvasRenderer _concreteRenderer; 
        [SerializeField] private BrushStrategy _eraserStrategy; // Hard brush for eraser
        
        // State
        private Color _currentColor = Color.black;
        private float _currentSize = 10f;
        private float _lastBrushSize = 10f;
        private float _lastEraserSize = 30f;
        private bool _isEraser = false;
        private BrushStrategy _currentStrategy;
        private Vector2 _currentStabilizedPos;
        private long _nextSequenceId = 1;
        
        // Public Accessors for UI/Preview
        public bool IsEraser => _isEraser;
        public float CurrentSize => _currentSize;
        public BrushStrategy EraserStrategy => _eraserStrategy;

        // Optimization State
        private LogicPoint _lastAddedPoint;

        
        // Services
        private IStrokeRenderer _renderer;
        private StrokeSmoothingService _smoothingService;
        private DrawingHistoryManager _historyManager;

        // Buffers
        private List<LogicPoint> _currentStrokeRaw = new List<LogicPoint>(1024);
        private List<LogicPoint> _smoothingInputBuffer = new List<LogicPoint>(8);
        private List<LogicPoint> _smoothingOutputBuffer = new List<LogicPoint>(64);
        private List<LogicPoint> _singlePointBuffer = new List<LogicPoint>(1);

        // Current stroke state capturing
        private Texture2D _currentRuntimeTexture;
        private StrokeEntity _currentStroke;

        private StrokeCollisionService _collisionService;
        
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
            _collisionService = new StrokeCollisionService();
            _historyManager = new DrawingHistoryManager(_renderer, _smoothingService, _collisionService);
        }

        // --- Synchronization / Serialization Support ---

        /// <summary>
        /// Gets the complete history (Archived + Active) for synchronization or saving.
        /// The result is the Source of Truth.
        /// </summary>
        public List<ICommand> GetFullHistory() => _historyManager.GetFullHistory();

        /// <summary>
        /// Replaces the current local history with a remote authoritative history.
        /// This is a "Stop the World" full sync operation.
        /// </summary>
        public void ReplaceHistory(List<ICommand> remoteHistory) => _historyManager.ReplaceHistory(remoteHistory);

        /// <summary>
        /// Generates a lightweight checksum (hash) of the current history state.
        /// Clients can exchange this string to detect desync.
        /// </summary>
        public string GetHistoryChecksum() => _historyManager.GetHistoryChecksum();

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
                // If we are in Eraser mode, we ALSO update Eraser size
                _lastEraserSize = size;
            }

            if (_renderer != null)
            {
                _renderer.SetBrushSize(size);
            }
        }

        public void SetStabilization(float factor)
        {
            if (_currentStrategy != null)
            {
                _currentStrategy.StabilizationFactor = Mathf.Clamp(factor, 0f, 0.95f);
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
                if (isEraser)
                {
                    // Force Eraser to use Hard Brush Strategy if available
                    if (_eraserStrategy != null)
                    {
                        _renderer.ConfigureBrush(_eraserStrategy);
                    }
                }

                _renderer.SetEraser(isEraser);
                _renderer.SetBrushSize(targetSize);
                
                // CRITICAL FIX: If switching BACK to brush, we MUST restore the brush's blend modes and texture.
                if (!isEraser && _currentStrategy != null)
                {
                    _renderer.ConfigureBrush(_currentStrategy, _currentRuntimeTexture); // Restore runtime texture if available
                    _renderer.SetBrushColor(_currentColor);
                }
            }
        }

        public void ClearCanvas()
        {
            // Create a clear command
            var cmd = new ClearCanvasCommand(_nextSequenceId++);
            
            // Execute immediately
            cmd.Execute(_renderer, _smoothingService);
            
            // Add to history
            _historyManager.AddCommand(cmd);
        }

        // --- Input Handling ---

        public void StartStroke(LogicPoint point)
        {
            // Notify listeners (e.g. UI to close panels)
            OnStrokeStarted?.Invoke();
            
            // CRITICAL FIX: Force sync Renderer state with Service state.
            // Undo/Redo operations (RedrawHistory) may have left the renderer in a different state 
            // (e.g. Eraser mode, wrong brush, wrong size).
            if (_renderer != null)
            {
                if (_isEraser)
                {
                    if (_eraserStrategy != null) _renderer.ConfigureBrush(_eraserStrategy);
                    _renderer.SetEraser(true);
                    _renderer.SetBrushSize(_currentSize); // _currentSize is already set to eraser size
                }
                else
                {
                    if (_currentStrategy != null) _renderer.ConfigureBrush(_currentStrategy, _currentRuntimeTexture);
                    _renderer.SetEraser(false);
                    _renderer.SetBrushColor(_currentColor);
                    _renderer.SetBrushSize(_currentSize);
                }
            }

            _currentStrokeRaw.Clear();

            // Create Domain Entity
            uint id = (uint)Random.Range(0, int.MaxValue); // Simple random ID
            uint seed = (uint)Random.Range(0, int.MaxValue);
            uint colorInt = ColorToUInt(_currentColor);
            
            // Use reserved ID for eraser to allow network clients to identify it
            ushort brushId = _isEraser ? DrawingConstants.ERASER_BRUSH_ID : (ushort)0;
            
            _currentStroke = new StrokeEntity(id, 0, brushId, seed, colorInt, _currentSize, _nextSequenceId++);

            _lastAddedPoint = point;
            AddPoint(point);
            _currentStabilizedPos = point.ToNormalized();
        }

        public void MoveStroke(LogicPoint point)
        {
            // Optimization: Eraser Deduplication (User Requirement)
            // "Eraser repeated drawing positions can be not recorded"
            // Filter out points that are too close to the last added point to avoid redundant collision checks and history data.
            if (_isEraser)
            {
                // LogicPoint uses 0-65535. 
                // Convert size (pixels) to approximate logical units.
                // Assuming 1920px screen ~ 65535 units => factor ~ 34.
                // Threshold: 10% of brush size.
                // If brush is 20px, threshold is 2px ~ 70 units.
                float scale = 65535f / 2000f; // Approx
                float threshold = (_currentSize * 0.1f) * scale;
                
                // Use squared distance for perf
                float sqrDist = LogicPoint.SqrDistance(_lastAddedPoint, point);
                if (sqrDist < threshold * threshold)
                {
                    return; // Skip this point
                }
            }

            LogicPoint pointToAdd = point;
            
            // Apply Stabilization (Anti-Shake)
            if (!_isEraser && _currentStrategy != null && _currentStrategy.StabilizationFactor > 0.001f)
            {
                Vector2 target = point.ToNormalized();
                float dist = Vector2.Distance(target, _currentStabilizedPos);
                
                const float MIN_SPEED_THRESHOLD = 0.002f; 
                const float MAX_SPEED_THRESHOLD = 0.05f;

                float speedT = Mathf.InverseLerp(MIN_SPEED_THRESHOLD, MAX_SPEED_THRESHOLD, dist);
                float dynamicFactor = Mathf.Lerp(_currentStrategy.StabilizationFactor, _currentStrategy.StabilizationFactor * 0.2f, speedT);
                
                float t = Mathf.Clamp01(1.0f - dynamicFactor);
                _currentStabilizedPos = Vector2.Lerp(_currentStabilizedPos, target, t);
                
                pointToAdd = LogicPoint.FromNormalized(_currentStabilizedPos, point.GetNormalizedPressure());
            }
            else
            {
                _currentStabilizedPos = point.ToNormalized();
            }

            AddPoint(pointToAdd);
            _lastAddedPoint = pointToAdd;
        }



        public void EndStroke()
        {
            if (_currentStroke == null) return;
            
            _currentStroke.EndStroke();
            _renderer.EndStroke();

            // FIX: Don't add empty strokes to history
            if (_currentStroke.Points.Count > 0)
            {
                // OPTIMIZATION: Discard eraser strokes that don't intersect with any existing ink.
                if (_isEraser)
                {
                    bool isEffective = _collisionService.IsEraserStrokeEffective(_currentStroke, _historyManager.ActiveStrokeIds);
                    
                    if (!isEffective)
                    {
                        Debug.Log($"[Optimization] Eraser stroke discarded [ID: {_currentStroke.Id}] - Redundant (covered area or no ink).");
                        _currentStroke = null;
                        return;
                    }
                }

                // Create Command
                // Note: We copy the points from the domain entity (or raw list).
                // _currentStroke.Points is List<LogicPoint>.
                // We pass the current state configuration.
                
                // Fix: Eraser should use _eraserStrategy if available
                var strategyToUse = _isEraser ? _eraserStrategy : _currentStrategy;

                var cmd = new DrawStrokeCommand(
                    _currentStroke.Id.ToString(),
                    _currentStroke.SequenceId,
                    new List<LogicPoint>(_currentStroke.Points),
                    strategyToUse,
                    _currentRuntimeTexture,
                    _currentColor,
                    _currentSize,
                    _isEraser
                );
                
                _historyManager.AddCommand(cmd);
                
                // Spatial Indexing
                _collisionService.Insert(_currentStroke);
            }
            
            // Serialization Check (Debug)
            // var bytes = StrokeSerializer.Serialize(_currentStroke);
            // Debug.Log($"[Stroke] Ended. Bytes: {bytes.Length}");
            
            _currentStroke = null;
        }

        public void Undo()
        {
            if (!_historyManager.CanUndo) return;

            // Save state
            var savedColor = _currentColor;
            var savedSize = _currentSize;
            var savedEraser = _isEraser;
            var savedStrategy = _currentStrategy;
            var savedRuntimeTex = _currentRuntimeTexture;

            _historyManager.Undo();
            
            // Restore state
            RestoreState(savedColor, savedSize, savedEraser, savedStrategy, savedRuntimeTex);
        }

        public void Redo()
        {
            if (!_historyManager.CanRedo) return;

            // Save state
            var savedColor = _currentColor;
            var savedSize = _currentSize;
            var savedEraser = _isEraser;
            var savedStrategy = _currentStrategy;
            var savedRuntimeTex = _currentRuntimeTexture;

            _historyManager.Redo();
            
            // Restore state
            RestoreState(savedColor, savedSize, savedEraser, savedStrategy, savedRuntimeTex);
        }

        /// <summary>
        /// Rebuilds the BakedRT from the logical archive.
        /// Call this when:
        /// 1. Resolution changes (and we want to keep high-quality vector strokes)
        /// 2. Synchronization correction is needed (Source of Truth mismatch)
        /// 3. Joining a room and receiving full history
        /// </summary>
        public void RebuildBackBuffer()
        {
            // Save state
            var savedColor = _currentColor;
            var savedSize = _currentSize;
            var savedEraser = _isEraser;
            var savedStrategy = _currentStrategy;
            var savedRuntimeTex = _currentRuntimeTexture;

            _historyManager.RebuildBackBuffer();
            
            // Restore state
            RestoreState(savedColor, savedSize, savedEraser, savedStrategy, savedRuntimeTex);
        }

        private void RestoreState(Color color, float size, bool isEraser, BrushStrategy strategy, Texture2D runtimeTex)
        {
            if (isEraser)
            {
                SetEraser(true);
                SetSize(size); 
            }
            else
            {
                SetBrushStrategy(strategy, runtimeTex);
                SetColor(color);
                SetSize(size);
            }
        }

        private uint ColorToUInt(Color color)
        {
            Color32 c32 = color;
            return (uint)((c32.r << 24) | (c32.g << 16) | (c32.b << 8) | c32.a);
        }

        private void AddPoint(LogicPoint point)
        {
            if (_renderer == null) return;

            _currentStrokeRaw.Add(point);
            
            if (_currentStroke != null)
            {
                _currentStroke.AddPoints(new LogicPoint[] { point });
            }

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
                _singlePointBuffer.Clear();
                _singlePointBuffer.Add(point);
                _renderer.DrawPoints(_singlePointBuffer);
            }
        }

        // --- Network Sync Helpers (Proposed) ---

        public void ReceiveRemoteStroke(StrokeEntity stroke)
        {
            if (stroke == null) return;

            bool isEraser = stroke.BrushId == DrawingConstants.ERASER_BRUSH_ID;
            
            if (isEraser)
            {
                _renderer.SetEraser(true);
                if (_eraserStrategy != null) _renderer.ConfigureBrush(_eraserStrategy);
            }
            else
            {
                _renderer.SetEraser(false);
                // Note: Strategy lookup by ID is not implemented here, using current for demo
                if (_currentStrategy != null) _renderer.ConfigureBrush(_currentStrategy, _currentRuntimeTexture);
            }
            
            _renderer.SetBrushSize(stroke.Size);
            
            // Convert UInt color back to Color
            // ... (Omitted for brevity)

            // Draw points (Smoothing logic needed here too ideally)
            // For now just draw raw
            _renderer.DrawPoints(stroke.Points);
            
            _renderer.EndStroke();
        }
    }
}
