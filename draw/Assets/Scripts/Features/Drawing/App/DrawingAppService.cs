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

namespace Features.Drawing.App
{
    /// <summary>
    /// Facade service that coordinates input, domain logic, and rendering.
    /// This is the main entry point for the drawing feature.
    /// </summary>
    public class DrawingAppService : MonoBehaviour
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
        
        // Services
        private IStrokeRenderer _renderer;
        private StrokeSmoothingService _smoothingService;
        
        // Buffers
        private List<LogicPoint> _currentStrokeRaw = new List<LogicPoint>(1024);
        private List<LogicPoint> _smoothingInputBuffer = new List<LogicPoint>(8);
        private List<LogicPoint> _smoothingOutputBuffer = new List<LogicPoint>(64);
        private List<LogicPoint> _singlePointBuffer = new List<LogicPoint>(1);

        // History
        private List<ICommand> _history = new List<ICommand>();
        private List<ICommand> _redoHistory = new List<ICommand>();
        
        // Archive (The Source of Truth for BakedRT)
        // Keeps track of commands that have been baked into the texture.
        // Allows us to rebuild the BakedRT if resolution changes or sync is needed.
        private List<ICommand> _archivedHistory = new List<ICommand>();

        // Current stroke state capturing
        private Texture2D _currentRuntimeTexture;
        private StrokeEntity _currentStroke;

        private StrokeSpatialIndex _spatialIndex;

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
            _spatialIndex = new StrokeSpatialIndex();
        }

        // --- Synchronization / Serialization Support ---

        /// <summary>
        /// Gets the complete history (Archived + Active) for synchronization or saving.
        /// The result is the Source of Truth.
        /// </summary>
        public List<ICommand> GetFullHistory()
        {
            var fullList = new List<ICommand>(_archivedHistory.Count + _history.Count);
            fullList.AddRange(_archivedHistory);
            fullList.AddRange(_history);
            return fullList;
        }

        /// <summary>
        /// Replaces the current local history with a remote authoritative history.
        /// This is a "Stop the World" full sync operation.
        /// </summary>
        public void ReplaceHistory(List<ICommand> remoteHistory)
        {
            // 1. Clear everything
            _history.Clear();
            _redoHistory.Clear();
            _archivedHistory.Clear();
            _renderer.ClearCanvas();
            _spatialIndex.Clear(); // Assuming we expose Clear on SpatialIndex or recreate it

            // 2. Replay all commands
            // Optimize: If list is huge, we should batch them or use the "Baking" optimization
            // For now, simple replay
            foreach (var cmd in remoteHistory)
            {
                // Execute without adding to history (we will add manually)
                cmd.Execute(_renderer, _smoothingService);
                
                // Add to appropriate list based on logic (or just put all in _archivedHistory if we treat them as 'done')
                // But to keep undo working for the last 50 steps, we should split them
            }

            // 3. Rebuild internal lists
            int total = remoteHistory.Count;
            int activeCount = Mathf.Min(total, 50); // Keep last 50 active
            int archiveCount = total - activeCount;

            if (archiveCount > 0)
            {
                _archivedHistory.AddRange(remoteHistory.GetRange(0, archiveCount));
            }
            
            if (activeCount > 0)
            {
                _history.AddRange(remoteHistory.GetRange(archiveCount, activeCount));
            }
            
            // 4. Force Bake/Rebuild visual state if needed
            // Since we executed them above, the visual state is correct (drawn on active RenderTexture)
            // Ideally, we should "Bake" the archived part to the BackBuffer
            // But for simplicity, the current Execute() calls draw to the active buffer.
            // If we have a separate BackBuffer, we might need to handle that.
            // For now, assuming Execute() draws to the visible canvas.
        }

        /// <summary>
        /// Generates a lightweight checksum (hash) of the current history state.
        /// Clients can exchange this string to detect desync.
        /// </summary>
        public string GetHistoryChecksum()
        {
            // Simple hash based on Command IDs
            // In production, use a better hash (CRC32/MD5) over the IDs
            long hash = 0;
            foreach (var cmd in _archivedHistory)
            {
                hash = (hash * 31) + cmd.Id.GetHashCode();
            }
            foreach (var cmd in _history)
            {
                hash = (hash * 31) + cmd.Id.GetHashCode();
            }
            return hash.ToString("X");
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
            var cmd = new ClearCanvasCommand();
            
            // Execute immediately
            cmd.Execute(_renderer, _smoothingService);
            
            // Add to history
            AddToHistory(cmd);
        }

        // --- Input Handling ---

        public void StartStroke(LogicPoint point)
        {
            // Notify listeners (e.g. UI to close panels)
            OnStrokeStarted?.Invoke();
            
            _currentStrokeRaw.Clear();

            // Create Domain Entity
            uint id = (uint)Random.Range(0, int.MaxValue); // Simple random ID
            uint seed = (uint)Random.Range(0, int.MaxValue);
            uint colorInt = ColorToUInt(_currentColor);
            
            // Use reserved ID for eraser to allow network clients to identify it
            ushort brushId = _isEraser ? DrawingConstants.ERASER_BRUSH_ID : (ushort)0;
            
            _currentStroke = new StrokeEntity(id, 0, brushId, seed, colorInt, _currentSize);

            AddPoint(point);
            _currentStabilizedPos = point.ToNormalized();
        }

        public void MoveStroke(LogicPoint point)
        {
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
                    Rect bounds = CalculateStrokeBounds(_currentStroke);
                    var candidates = _spatialIndex.Query(bounds);
                    
                    bool hitInk = false;
                    foreach (var s in candidates)
                    {
                        // Check if it's a normal brush (not eraser)
                        // Note: Erasing an eraser stroke is meaningless (erasing nothing).
                        // We only care if we hit actual ink.
                        if (s.BrushId != DrawingConstants.ERASER_BRUSH_ID)
                        {
                            hitInk = true;
                            break;
                        }
                    }

                    if (!hitInk)
                    {
                        Debug.Log($"[Optimization] Eraser stroke discarded [ID: {_currentStroke.Id}] - No intersection with ink.");
                        _currentStroke = null;
                        return;
                    }
                }

                // Create Command
                // Note: We copy the points from the domain entity (or raw list).
                // _currentStroke.Points is List<LogicPoint>.
                // We pass the current state configuration.
                var cmd = new DrawStrokeCommand(
                    _currentStroke.Id.ToString(),
                    new List<LogicPoint>(_currentStroke.Points),
                    _currentStrategy,
                    _currentRuntimeTexture,
                    _currentColor,
                    _currentSize,
                    _isEraser
                );
                
                AddToHistory(cmd);
                
                // Spatial Indexing
                _spatialIndex.Insert(_currentStroke);
            }
            
            // Serialization Check (Debug)
            // var bytes = StrokeSerializer.Serialize(_currentStroke);
            // Debug.Log($"[Stroke] Ended. Bytes: {bytes.Length}");
            
            _currentStroke = null;
        }

        private Rect CalculateStrokeBounds(StrokeEntity stroke)
        {
             if (stroke.Points == null || stroke.Points.Count == 0) return Rect.zero;
             
             float minX = float.MaxValue, minY = float.MaxValue;
             float maxX = float.MinValue, maxY = float.MinValue;
             
             foreach(var p in stroke.Points)
             {
                 if (p.X < minX) minX = p.X;
                 if (p.X > maxX) maxX = p.X;
                 if (p.Y < minY) minY = p.Y;
                 if (p.Y > maxY) maxY = p.Y;
             }
             
             // Add padding to be safe (account for brush size approximation)
             // LogicPoint uses 0-65535 space. 
             // 1000 units is ~1.5% of the canvas width, which is a safe buffer.
             float padding = 1000f; 
             
             return new Rect(minX - padding, minY - padding, (maxX - minX) + padding * 2, (maxY - minY) + padding * 2);
        }

        private void AddToHistory(ICommand cmd)
        {
            Debug.Log($"[History] Added command: {cmd.GetType().Name} [ID: {cmd.Id}]. Count: {_history.Count + 1}");
            _history.Add(cmd);
            _redoHistory.Clear();
            
            // Maintain sliding window (FIFO - First In First Out)
            // Remove the oldest commands (index 0) to keep only the most recent 50
            while (_history.Count > 50)
            {
                var removedCmd = _history[0];
                
                // Archive it (Logical Save)
                _archivedHistory.Add(removedCmd);
                
                // Optimization: If the baked command is a ClearCanvas, 
                // we can safely discard all previous archive history to save RAM.
                if (removedCmd is ClearCanvasCommand)
                {
                    // Everything before a Clear is visually irrelevant.
                    // (Unless we want to support "Undo the Clear" even after it falls off the 50 stack? 
                    //  No, usually if it falls off stack, it's finalized).
                    // Actually, keeping the Clear command itself is enough as a starting point.
                    _archivedHistory.Clear();
                    _archivedHistory.Add(removedCmd);
                }

                // Bake the command into the back buffer before removing it (Visual Save)
                if (_renderer != null)
                {
                    _renderer.SetBakingMode(true);
                    removedCmd.Execute(_renderer, _smoothingService);
                    _renderer.SetBakingMode(false);
                }
                
                // Debug.Log($"[History] Removed old command [ID: {removedCmd.Id}] to maintain limit.");
                _history.RemoveAt(0);
            }
        }

        public void Undo()
        {
            if (_history.Count == 0) return;

            // Remove last command
            var cmd = _history[_history.Count - 1];
            Debug.Log($"[Undo] Reverting command [ID: {cmd.Id}]");
            _history.RemoveAt(_history.Count - 1);
            
            // Add to Redo history
            _redoHistory.Add(cmd);
            
            RedrawHistory();
        }

        public void Redo()
        {
            if (_redoHistory.Count == 0) return;

            // Remove last redo item
            var cmd = _redoHistory[_redoHistory.Count - 1];
            Debug.Log($"[Redo] Restoring command [ID: {cmd.Id}]");
            _redoHistory.RemoveAt(_redoHistory.Count - 1);
            
            // Add back to history
            _history.Add(cmd);
            
            RedrawHistory();
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
            if (_renderer == null) return;
            
            // 1. Clear the BackBuffer
            _renderer.SetBakingMode(true);
            _renderer.ClearCanvas();
            
            // 2. Replay all archived commands
            foreach (var cmd in _archivedHistory)
            {
                cmd.Execute(_renderer, _smoothingService);
            }
            
            _renderer.SetBakingMode(false);
            
            // 3. Trigger a normal redraw to composite BackBuffer + Active History
            RedrawHistory();
            
            Debug.Log($"[History] Rebuilt BackBuffer from {_archivedHistory.Count} archived commands.");
        }

        private void RedrawHistory()
        {
            if (_renderer == null) return;

            // Save current state to restore later
            var savedColor = _currentColor;
            var savedSize = _currentSize;
            var savedEraser = _isEraser;
            var savedStrategy = _currentStrategy;
            var savedRuntimeTex = _currentRuntimeTexture;
            
            // 1. Determine start state
            int startIndex = 0;
            bool fullClear = false;

            // Check if we have a ClearCanvasCommand in history
            for (int i = _history.Count - 1; i >= 0; i--)
            {
                if (_history[i] is ClearCanvasCommand)
                {
                    startIndex = i;
                    fullClear = true;
                    break;
                }
            }

            // 2. Prepare Canvas
            if (fullClear)
            {
                // If we found a Clear command, we can just clear the active canvas
                // The replay will start FROM that clear command.
                // Note: We don't need the BackBuffer in this case because the Clear command wipes everything anyway.
                _renderer.ClearCanvas();
            }
            else
            {
                // If no clear command found, we must start from the BackBuffer (Snapshot)
                // This restores all the "forgotten" history items.
                _renderer.RestoreFromBackBuffer();
            }
            
            // 3. Replay commands
            for (int i = startIndex; i < _history.Count; i++)
            {
                _history[i].Execute(_renderer, _smoothingService);
            }

            // Restore original state
            if (savedEraser)
            {
                SetEraser(true);
                SetSize(savedSize); 
            }
            else
            {
                SetBrushStrategy(savedStrategy, savedRuntimeTex);
                SetColor(savedColor);
                SetSize(savedSize);
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
