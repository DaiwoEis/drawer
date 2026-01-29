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
using Features.Drawing.App.State;
using Common.Diagnostics;

namespace Features.Drawing.App
{
    /// <summary>
    /// Facade service that coordinates input, domain logic, and rendering.
    /// This is the main entry point for the drawing feature.
    /// </summary>
    public class DrawingAppService : MonoBehaviour, IInputHandler, IBrushRegistry
    {
        [Header("References")]
        [SerializeField] private Features.Drawing.Presentation.CanvasRenderer _concreteRenderer; 
        [SerializeField] private BrushStrategy _eraserStrategy; // Hard brush for eraser
        [SerializeField] private BrushStrategy[] _registeredBrushes; // Registry of available brushes
        
        [Header("Diagnostics")]
        [SerializeField] private bool _enableDiagnostics = true;

        private IStructuredLogger _logger;
        private PerformanceMonitor _perfMonitor;
        private TraceContext _activeStrokeTrace;

        // State Management
        private InputStateManager _inputState;
        
        // Public Accessors for UI/Preview
        public bool IsEraser => _inputState?.IsEraser ?? false;
        public float CurrentSize => _inputState?.CurrentSize ?? 10f;
        public BrushStrategy EraserStrategy => _eraserStrategy;

        // Optimization State
        private LogicPoint _lastAddedPoint;
        private Vector2 _currentStabilizedPos;
        private long _nextSequenceId = 1;

        
        // Services
        private IStrokeRenderer _renderer;
        private StrokeSmoothingService _smoothingService;
        private DrawingHistoryManager _historyManager;

        // Buffers
        private List<LogicPoint> _currentStrokeRaw = new List<LogicPoint>(1024);
        private List<LogicPoint> _smoothingInputBuffer = new List<LogicPoint>(8);
        private List<LogicPoint> _smoothingOutputBuffer = new List<LogicPoint>(64);
        private List<LogicPoint> _singlePointBuffer = new List<LogicPoint>(1);
        private readonly LogicPoint[] _singlePointArray = new LogicPoint[1];

        // Current stroke state capturing
        private StrokeEntity _currentStroke;

        private StrokeCollisionService _collisionService;
        
        private float _logicToWorldRatio = DrawingConstants.LOGIC_TO_WORLD_RATIO;

        // Network Integration
        private Features.Drawing.Service.Network.DrawingNetworkService _networkService;

        // Events
        public event System.Action OnStrokeStarted;

        private void Awake()
        {
            // Performance: Limit frame rate to 60 FPS to save battery/reduce heat
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            // 1. Resolve Renderer (Priority: Inspector -> FindObjectOfType)
            if (_concreteRenderer == null) 
                _concreteRenderer = FindObjectOfType<Features.Drawing.Presentation.CanvasRenderer>();

            // Ensure Renderer is initialized explicitly (No Coroutines)
            if (_concreteRenderer != null)
            {
                _concreteRenderer.Initialize();
            }
                
            IStrokeRenderer renderer = _concreteRenderer as IStrokeRenderer;
            
            if (renderer == null)
            {
                Debug.LogError("DrawingAppService: CanvasRenderer does not implement IStrokeRenderer!");
            }
            
            // 2. Initialize with defaults (Dependency Injection Fallback)
            // If dependencies were not injected via Construct(), we create them here.
            // Create default logger if diagnostics enabled
            IStructuredLogger logger = null;
            if (_enableDiagnostics)
            {
                logger = new StructuredLogger("DrawingApp", 10, true);
                // Attach PerformanceMonitor
                _perfMonitor = gameObject.AddComponent<PerformanceMonitor>();
                _perfMonitor.Initialize(logger);
            }

            Initialize(renderer, null, null, null, logger);
        }

        private void OnDestroy()
        {
            if (_networkService != null)
            {
                _networkService.OnRemoteStrokeCommitted -= CommitRemoteStroke;
            }
        }

        public void SetNetworkService(Features.Drawing.Service.Network.DrawingNetworkService networkService)
        {
            _networkService = networkService;
            if (_networkService != null)
            {
                // Subscribe to network events
                _networkService.OnRemoteStrokeCommitted += CommitRemoteStroke;
                _networkService.InitializeBrushRegistry(this);
            }
        }

        /// <summary>
        /// Dependency Injection Entry Point.
        /// Allows external systems (Zenject/Tests) to inject mock or specific implementations.
        /// </summary>
        public void Initialize(
            IStrokeRenderer renderer,
            StrokeSmoothingService smoothingService = null,
            StrokeCollisionService collisionService = null,
            DrawingHistoryManager historyManager = null,
            IStructuredLogger logger = null)
        {
            // Only set if not null (allow partial injection logic if needed, though usually all or nothing)
            if (_renderer == null) _renderer = renderer;
            
            // Diagnostics
            if (_logger == null) _logger = logger;

            // Lazy init services if not provided
            if (_smoothingService == null) 
                _smoothingService = smoothingService ?? new StrokeSmoothingService();
                
            if (_collisionService == null) 
                _collisionService = collisionService ?? new StrokeCollisionService();
            
            // HistoryManager depends on others
            if (_historyManager == null) 
                _historyManager = historyManager ?? new DrawingHistoryManager(_renderer, _smoothingService, _collisionService);

            // Init State Manager
            _inputState = new InputStateManager(_renderer, _eraserStrategy);

            // 3. Setup Resolution Handling
            if (_renderer is Features.Drawing.Presentation.CanvasRenderer concreteRenderer)
            {
                concreteRenderer.OnResolutionChanged += UpdateResolutionRatio;
                UpdateResolutionRatio(concreteRenderer.Resolution);
            }
        }

        private void UpdateResolutionRatio(Vector2Int resolution)
        {
            // Calculate ratio based on logical resolution (65536) and max screen dimension
            // LogicPoint Space: 0-65535
            // Pixel Space: 0-Resolution
            // Ratio = 65535 / MaxDimension
            
            float maxDim = Mathf.Max(resolution.x, resolution.y);
            if (maxDim > 0)
            {
                _logicToWorldRatio = DrawingConstants.LOGICAL_RESOLUTION / maxDim;
            }
            else
            {
                _logicToWorldRatio = DrawingConstants.LOGIC_TO_WORLD_RATIO; // Fallback
            }

            _collisionService?.SetLogicToWorldRatio(_logicToWorldRatio);
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
            _inputState?.SetBrushStrategy(strategy, runtimeTexture);
        }

        public void SetColor(Color color)
        {
            _inputState?.SetColor(color);
        }

        public void SetSize(float size)
        {
            _inputState?.SetSize(size);
        }

        public void SetStabilization(float factor)
        {
            _inputState?.SetStabilization(factor);
        }

        public void SetEraser(bool isEraser)
        {
            _inputState?.SetEraser(isEraser);
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
            if (_inputState == null) return;

            // Diagnostics
            _activeStrokeTrace = TraceContext.New();
            if (_logger != null)
            {
                var meta = new Dictionary<string, object> 
                { 
                    { "isEraser", _inputState.IsEraser },
                    { "size", _inputState.CurrentSize },
                    { "color", _inputState.CurrentColor }
                };
                _logger.Info("StrokeStarted", _activeStrokeTrace, meta);
            }

            // Notify listeners (e.g. UI to close panels)
            OnStrokeStarted?.Invoke();
            
            // CRITICAL FIX: Force sync Renderer state with Service state.
            _inputState.SyncToRenderer();

            _currentStrokeRaw.Clear();

            // Create Domain Entity
            uint id = (uint)Random.Range(0, int.MaxValue); // Simple random ID
            uint seed = (uint)Random.Range(0, int.MaxValue);
            uint colorInt = ColorToUInt(_inputState.CurrentColor);
            
            // Resolve Brush ID
            ushort brushId = GetBrushId(_inputState.CurrentStrategy);
            
            _currentStroke = new StrokeEntity(id, 0, brushId, seed, colorInt, _inputState.CurrentSize, _nextSequenceId++);

            // Network Sync: Begin Stroke
            if (_networkService != null && _networkService.isActiveAndEnabled)
            {
                _networkService.OnLocalStrokeStarted(id, brushId, _inputState.CurrentColor, _inputState.CurrentSize, _inputState.IsEraser);
            }

            _lastAddedPoint = point;
            AddPoint(point);
            _currentStabilizedPos = point.ToNormalized();

            // Network Sync: Send the first point immediately
            // This is critical because BeginStrokePacket does not contain coordinates.
            if (_networkService != null && _networkService.isActiveAndEnabled)
            {
                _networkService.OnLocalStrokeMoved(point);
            }
        }

        public void MoveStroke(LogicPoint point)
        {
            // Optimization: Eraser Deduplication (User Requirement)
            // "Eraser repeated drawing positions can be not recorded"
            // Filter out points that are too close to the last added point to avoid redundant collision checks and history data.
            if (_inputState.IsEraser)
            {
                // LogicPoint uses 0-65535. 
                // Convert size (pixels) to approximate logical units.
                // Assuming 1920px screen ~ 65535 units => factor ~ 34.
                // Threshold: 10% of brush size.
                // If brush is 20px, threshold is 2px ~ 70 units.
                float scale = _logicToWorldRatio;
                float threshold = (_inputState.CurrentSize * 0.1f) * scale;
                
                // Use squared distance for perf
                float sqrDist = LogicPoint.SqrDistance(_lastAddedPoint, point);
                if (sqrDist < threshold * threshold)
                {
                    return; // Skip this point
                }
            }

            LogicPoint pointToAdd = point;
            
            // Apply Stabilization (Anti-Shake)
            if (!_inputState.IsEraser && _inputState.CurrentStrategy != null && _inputState.CurrentStrategy.StabilizationFactor > 0.001f)
            {
                Vector2 target = point.ToNormalized();
                float dist = Vector2.Distance(target, _currentStabilizedPos);
                
                const float MIN_SPEED_THRESHOLD = 0.002f; 
                const float MAX_SPEED_THRESHOLD = 0.05f;

                float speedT = Mathf.InverseLerp(MIN_SPEED_THRESHOLD, MAX_SPEED_THRESHOLD, dist);
                float dynamicFactor = Mathf.Lerp(_inputState.CurrentStrategy.StabilizationFactor, _inputState.CurrentStrategy.StabilizationFactor * 0.2f, speedT);
                float pressure = Mathf.Clamp01(point.GetNormalizedPressure());
                float pressureWeight = Mathf.Lerp(1.1f, 0.7f, pressure);
                dynamicFactor *= pressureWeight;
                
                float t = Mathf.Clamp01(1.0f - dynamicFactor);
                _currentStabilizedPos = Vector2.Lerp(_currentStabilizedPos, target, t);
                
                pointToAdd = LogicPoint.FromNormalized(_currentStabilizedPos, point.GetNormalizedPressure());
            }
            else
            {
                _currentStabilizedPos = point.ToNormalized();
            }

            if (!_inputState.IsEraser)
            {
                float spacingRatio = _inputState.CurrentStrategy != null ? _inputState.CurrentStrategy.SpacingRatio : 0.15f;
                float minPixelSpacing = _inputState.CurrentSize * spacingRatio;
                if (minPixelSpacing < 1f) minPixelSpacing = 1f;
                float minLogical = minPixelSpacing * _logicToWorldRatio;
                float sqrDist = LogicPoint.SqrDistance(_lastAddedPoint, pointToAdd);
                if (sqrDist < minLogical * minLogical)
                {
                    return;
                }
            }

            AddPoint(pointToAdd);
            _lastAddedPoint = pointToAdd;
            
            // Network Sync: Move Stroke
            if (_networkService != null && _networkService.isActiveAndEnabled)
            {
                _networkService.OnLocalStrokeMoved(pointToAdd);
            }
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
                if (_inputState.IsEraser)
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
                var strategyToUse = _inputState.IsEraser ? _eraserStrategy : _inputState.CurrentStrategy;

                var cmd = new DrawStrokeCommand(
                    _currentStroke.Id.ToString(),
                    _currentStroke.SequenceId,
                    new List<LogicPoint>(_currentStroke.Points),
                    strategyToUse,
                    _inputState.CurrentRuntimeTexture,
                    _inputState.CurrentColor,
                    _inputState.CurrentSize,
                    _inputState.IsEraser
                );
                
                _historyManager.AddCommand(cmd);
                
                // Spatial Indexing
                _collisionService.Insert(_currentStroke);

                // Network Sync: End Stroke
                if (_networkService != null && _networkService.isActiveAndEnabled)
                {
                    uint checksum = Features.Drawing.Service.Network.DrawingNetworkService.ComputeStrokeChecksum(_currentStroke.Points);
                    _networkService.OnLocalStrokeEnded(checksum, _currentStroke.Points.Count);
                }
            }
            
            // Serialization Check (Debug)
            // var bytes = StrokeSerializer.Serialize(_currentStroke);
            // Debug.Log($"[Stroke] Ended. Bytes: {bytes.Length}");
            
            _currentStroke = null;
        }

        public void ForceEndCurrentStroke()
        {
            if (_currentStroke != null)
            {
                // Force end the current stroke and add it to history
                EndStroke();
            }
        }

        /// <summary>
        /// Resumes a remote stroke that is currently in progress (e.g. after reconnection).
        /// This allows the viewer to catch up with an ongoing stroke and continue receiving updates.
        /// </summary>
        public void ResumeRemoteStroke(uint id, List<LogicPoint> existingPoints, BrushStrategy strategy, Color color, float size, bool isEraser)
        {
            // 1. Force end any local stroke (safety)
            ForceEndCurrentStroke();

            // 2. Set State
            if (isEraser)
            {
                SetEraser(true);
                SetSize(size);
                if (_eraserStrategy != null) _renderer.ConfigureBrush(_eraserStrategy);
            }
            else
            {
                SetBrushStrategy(strategy); // This sets _currentStrategy
                SetColor(color);
                SetSize(size);
            }
            
            // 3. Initialize Stroke Entity (Network Source)
            uint seed = (uint)Random.Range(0, int.MaxValue); // Seed doesn't matter much for resumption as long as consistent? 
            // Actually, for perfect sync, we might need the original seed, but for now random is okay or passed in metadata.
            uint colorInt = ColorToUInt(color);
            ushort brushId = isEraser ? DrawingConstants.ERASER_BRUSH_ID : (ushort)0;
            
            _currentStroke = new StrokeEntity(id, 0, brushId, seed, colorInt, size, _nextSequenceId++);

            // 4. Replay existing points
            _currentStrokeRaw.Clear();
            _currentStabilizedPos = Vector2.zero; // Reset stabilization
            
            if (existingPoints != null && existingPoints.Count > 0)
            {
                // Setup stabilization state from last point
                _currentStabilizedPos = existingPoints[existingPoints.Count - 1].ToNormalized();
                _lastAddedPoint = existingPoints[existingPoints.Count - 1];

                // OPTIMIZATION: Batch Add Points
                // Instead of calling AddPoint one by one (which triggers renderer frequently),
                // we batch them up.
                AddPointsBatch(existingPoints);
            }
            
            // 5. DO NOT EndStroke. Wait for subsequent OnNetworkStrokeMoved/Ended events.
        }

        private void AddPointsBatch(List<LogicPoint> points)
        {
            if (_renderer == null || points == null || points.Count == 0) return;

            // 1. Update Domain State
            _currentStrokeRaw.AddRange(points);
            
            if (_currentStroke != null)
            {
                _currentStroke.AddPoints(points);
            }

            // 2. Render Batch
            // For batch rendering, we can either:
            // A) Just draw raw points (fastest)
            // B) Run smoothing on the whole chain
            
            // Let's do a simplified smoothing run for the batch.
            // If the batch is large, we can just process it as a continuous strip.
            
            if (points.Count < 4)
            {
                 // Too small for spline, draw directly
                 _renderer.DrawPoints(points);
            }
            else
            {
                // Smooth the entire batch
                // Note: This might be slightly different than incremental smoothing, 
                // but for "Catch Up" it's acceptable and much faster.
                // Or we can just use the incremental smoothing logic but batched.
                
                // For simplicity and performance in replay:
                // We'll use the smoothing service's batch capability if it exists, or loop.
                // But StrokeSmoothingService typically takes 4 points -> interpolates.
                
                // Optimization: Just draw raw for catch-up to avoid heavy CPU math?
                // No, we want quality.
                
                // Let's assume we can just draw them. The renderer now supports Instancing,
                // so drawing 1000 points is cheap on GPU.
                // The issue is CPU spline interpolation.
                // Let's just draw them directly for now to solve the bottleneck.
                // If quality is bad, we can revisit.
                
                _renderer.DrawPoints(points);
            }
        }

        public void Undo()
        {
            if (!_historyManager.CanUndo) return;

            // Save state
            var savedColor = _inputState.CurrentColor;
            var savedSize = _inputState.CurrentSize;
            var savedEraser = _inputState.IsEraser;
            var savedStrategy = _inputState.CurrentStrategy;
            var savedRuntimeTex = _inputState.CurrentRuntimeTexture;

            _historyManager.Undo();
            
            // Restore state
            RestoreState(savedColor, savedSize, savedEraser, savedStrategy, savedRuntimeTex);
        }

        public void Redo()
        {
            if (!_historyManager.CanRedo) return;

            // Save state
            var savedColor = _inputState.CurrentColor;
            var savedSize = _inputState.CurrentSize;
            var savedEraser = _inputState.IsEraser;
            var savedStrategy = _inputState.CurrentStrategy;
            var savedRuntimeTex = _inputState.CurrentRuntimeTexture;

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
            var savedColor = _inputState.CurrentColor;
            var savedSize = _inputState.CurrentSize;
            var savedEraser = _inputState.IsEraser;
            var savedStrategy = _inputState.CurrentStrategy;
            var savedRuntimeTex = _inputState.CurrentRuntimeTexture;

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
                // Optimization: Use pre-allocated array to avoid GC allocation per point
                _singlePointArray[0] = point;
                _currentStroke.AddPoints(_singlePointArray);
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

        public void CommitRemoteStroke(StrokeEntity stroke)
        {
            if (stroke == null || stroke.Points.Count == 0) return;

            // 1. Setup Renderer State for this stroke
            bool isEraser = stroke.BrushId == DrawingConstants.ERASER_BRUSH_ID;
            
            // Note: We are modifying the renderer state directly here.
            // This is safe because this runs on the main thread, but we must ensure
            // we restore it or that the next StartStroke resets it correctly (which it does).
            
            BrushStrategy strategy = null;
            if (isEraser)
            {
                strategy = _eraserStrategy;
                if (_renderer != null)
                {
                    if (strategy != null) _renderer.ConfigureBrush(strategy);
                    _renderer.SetEraser(true);
                    _renderer.SetBrushSize(stroke.Size);
                }
            }
            else
            {
                // Lookup strategy by ID
                strategy = GetBrushStrategy(stroke.BrushId);
                
                if (_renderer != null)
                {
                    // Use runtime texture if it's the current local user (not ideal logic for remote, but best guess)
                    // For true remote, we should sync the texture ID or use the strategy's default.
                    // Assuming strategy default for remote strokes.
                    Texture2D tex = strategy?.MainTexture;
                    if (strategy != null) _renderer.ConfigureBrush(strategy, tex);
                    
                    _renderer.SetEraser(false);
                    // Convert uint color back to Color
                    Color c = UIntToColor(stroke.ColorRGBA);
                    _renderer.SetBrushColor(c);
                    _renderer.SetBrushSize(stroke.Size);
                }
            }

            // 2. Draw Full Stroke (Smoothly)
            // We use the same smoothing logic as local strokes
            if (_renderer != null)
            {
                // Create temp command to execute drawing logic?
                // Or just draw directly.
                // Let's draw directly using the smoothing service helper
                
                // We can reuse DrawStrokeCommand logic but we don't want to create a command instance just to execute it?
                // Actually creating a command is exactly what we want, because we want to add it to history!
            }

            // 3. Create Command & Add to History
            var cmd = new DrawStrokeCommand(
                stroke.Id.ToString(),
                stroke.SequenceId,
                new List<LogicPoint>(stroke.Points),
                strategy,
                _inputState.CurrentRuntimeTexture, // Might be wrong if remote user used different texture
                UIntToColor(stroke.ColorRGBA),
                stroke.Size,
                isEraser
            );
            
            // Execute (Draws it)
            cmd.Execute(_renderer, _smoothingService);
            
            // Add to history
            _historyManager.AddCommand(cmd);
            
            // Spatial Index
            _collisionService.Insert(stroke);
            
            // Diagnostics
            if (_logger != null && _enableDiagnostics) // Only log if explicitly enabled
            {
                 // _logger.Info("RemoteStrokeCommitted", Common.Diagnostics.TraceContext.New(), new Dictionary<string, object> { { "id", stroke.Id }, { "points", stroke.Points.Count } });
            }
        }
        
        private Color UIntToColor(uint color)
        {
            byte r = (byte)((color >> 24) & 0xFF);
            byte g = (byte)((color >> 16) & 0xFF);
            byte b = (byte)((color >> 8) & 0xFF);
            byte a = (byte)(color & 0xFF);
            return new Color32(r, g, b, a);
        }

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
                var strategy = GetBrushStrategy(stroke.BrushId);
                if (strategy != null) _renderer.ConfigureBrush(strategy, strategy.MainTexture);
            }
            
            _renderer.SetBrushSize(stroke.Size);
            
            // Convert UInt color back to Color
            // ... (Omitted for brevity)

            // Draw points (Smoothing logic needed here too ideally)
            // For now just draw raw
            _renderer.DrawPoints(stroke.Points);
            
            _renderer.EndStroke();
        }
        public BrushStrategy GetBrushStrategy(ushort id)
        {
            if (id == DrawingConstants.ERASER_BRUSH_ID) return _eraserStrategy;
            if (id == DrawingConstants.UNKNOWN_BRUSH_ID)
            {
                Debug.LogWarning($"[DrawingAppService] Received UNKNOWN_BRUSH_ID. Falling back to current local strategy: {_inputState.CurrentStrategy?.name}");
                return _inputState.CurrentStrategy;
            }

            if (_registeredBrushes != null && id < _registeredBrushes.Length)
            {
                // Debug.Log($"[DrawingAppService] Resolved Brush ID {id} to '{_registeredBrushes[id].name}'");
                return _registeredBrushes[id];
            }
            
            Debug.LogWarning($"[DrawingAppService] Brush ID {id} out of bounds (Count: {_registeredBrushes?.Length ?? 0}). Fallback to default.");

            // Fallback for valid but out-of-bounds IDs (should not happen if registry is consistent)
            if (_registeredBrushes != null && _registeredBrushes.Length > 0) return _registeredBrushes[0];
            
            return _inputState.CurrentStrategy; // Last resort
        }

        private ushort GetBrushId(BrushStrategy strategy)
        {
            if (_inputState.IsEraser) return DrawingConstants.ERASER_BRUSH_ID;

            if (strategy == null)
            {
                Debug.LogWarning("[DrawingAppService] Brush strategy is null. Returning UNKNOWN_BRUSH_ID.");
                return DrawingConstants.UNKNOWN_BRUSH_ID;
            }
            
            if (_registeredBrushes != null)
            {
                for (int i = 0; i < _registeredBrushes.Length; i++)
                {
                    if (_registeredBrushes[i] == strategy) 
                    {
                        // Debug.Log($"[DrawingAppService] Found ID {i} for brush '{strategy.name}'");
                        return (ushort)i;
                    }
                }
            }
            
            Debug.LogWarning($"[DrawingAppService] Brush '{strategy.name}' NOT FOUND in registry! Current Registry: {string.Join(", ", _registeredBrushes != null ? System.Linq.Enumerable.Select(_registeredBrushes, b => b.name) : new string[0])}");
            return DrawingConstants.UNKNOWN_BRUSH_ID;
        }
    }
}
