using System.Collections;
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

using Features.Drawing.Domain.Context;

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
        public static bool DebugMode = true; // Static switch for other components

        private IStructuredLogger _logger;
        private PerformanceMonitor _perfMonitor;
        private TraceContext _activeStrokeTrace;

        // State Management
        private InputStateManager _inputState;
        private DrawingSessionContext _sessionContext = new DrawingSessionContext();
        
        // Input Processing (Extracted)
        private Features.Drawing.Service.Input.StrokeInputProcessor _inputProcessor = new Features.Drawing.Service.Input.StrokeInputProcessor();
        private Features.Drawing.Service.Network.RemoteStrokeHandler _remoteHandler;

        // Public Accessors for UI/Preview
        public bool IsEraser => _inputState?.IsEraser ?? false;
        public float CurrentSize => _inputState?.CurrentSize ?? 10f;
        public BrushStrategy EraserStrategy => _eraserStrategy;

        // Optimization State - Moved to StrokeInputProcessor
        // private LogicPoint _lastAddedPoint;
        // private Vector2 _currentStabilizedPos;
        // _nextSequenceId moved to DrawingSessionContext

        
        // Services
        private IStrokeRenderer _renderer;
        private StrokeSmoothingService _smoothingService;
        private VisualDrawingHistoryManager _historyManager;

        // Buffers
        // _currentStrokeRaw moved to DrawingSessionContext

        // Current stroke state capturing
        // _currentStroke moved to DrawingSessionContext

        private StrokeCollisionService _collisionService;
        
        private float _logicToWorldRatio = DrawingConstants.LOGIC_TO_WORLD_RATIO;

        // Network Integration
        private Features.Drawing.Service.Network.DrawingNetworkService _networkService;

        // Events
        public event System.Action OnStrokeStarted;

        private IEnumerator Start()
        {
            // Performance: Limit frame rate to 60 FPS
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            if (_enableDiagnostics)
            {
                // Create default logger
                IStructuredLogger logger = new StructuredLogger("DrawingApp", 10, true);
                _perfMonitor = gameObject.AddComponent<PerformanceMonitor>();
                _perfMonitor.Initialize(logger);
                
                // Temporary DI setup for Logger if needed
            }

            // 1. Resolve Renderer
            if (_concreteRenderer == null) 
                _concreteRenderer = FindObjectOfType<Features.Drawing.Presentation.CanvasRenderer>();

            // 2. Async Initialize Renderer (Wait for Shader Warmup & RT Allocation)
            if (_concreteRenderer != null)
            {
                yield return _concreteRenderer.InitializeAsync();
            }
                
            IStrokeRenderer renderer = _concreteRenderer as IStrokeRenderer;
            
            if (renderer == null)
            {
                Debug.LogError("DrawingAppService: CanvasRenderer does not implement IStrokeRenderer!");
            }
            
            // 3. Initialize App Logic
            // Note: We pass null for dependencies to trigger internal default creation if not already injected
            Initialize(renderer, null, null, null, _enableDiagnostics ? new StructuredLogger("DrawingApp", 10, true) : null);
        }

        private void Awake()
        {
            if (Application.platform == RuntimePlatform.IPhonePlayer && !Debug.isDebugBuild)
            {
                _enableDiagnostics = false;
            }
            DebugMode = _enableDiagnostics;
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
            VisualDrawingHistoryManager historyManager = null,
            IStructuredLogger logger = null)
        {
            // Only set if not null (allow partial injection logic if needed, though usually all or nothing)
            if (_renderer == null) _renderer = renderer;
            
            // Diagnostics
            if (_logger == null) _logger = logger;

            // Lazy init services if not provided
            var effectiveSmoothingService = smoothingService ?? new StrokeSmoothingService();
            
            // Inject SmoothingService into Renderer if it's CanvasRenderer
            if (_renderer is Features.Drawing.Presentation.CanvasRenderer cr)
            {
                cr.SetSmoothingService(effectiveSmoothingService);
            }

            if (_collisionService == null) 
                _collisionService = collisionService ?? new StrokeCollisionService();
            
            // HistoryManager depends on others
            if (_historyManager == null) 
                _historyManager = historyManager ?? new VisualDrawingHistoryManager(_renderer);

            // Init State Manager
            _inputState = new InputStateManager(_renderer, _eraserStrategy);

            // Init Remote Handler
            _remoteHandler = new Features.Drawing.Service.Network.RemoteStrokeHandler(
                _renderer, 
                _historyManager, 
                _collisionService, 
                this, 
                _eraserStrategy
            );

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
        public void ReplaceHistory(List<ICommand> remoteHistory)
        {
            _collisionService?.Clear();
            _historyManager.ReplaceHistory(remoteHistory);
        }

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
            var cmd = new ClearCanvasCommand(_sessionContext.GetNextSequenceId());
            
            // Execute immediately
            cmd.Execute(_renderer);
            
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
                    { "color", _inputState.CurrentColor },
                    { "point", point.ToString() }
                };
                _logger.Info("StrokeStarted", _activeStrokeTrace, meta);
            }
            if (_enableDiagnostics) Debug.Log($"[App] StartStroke ID:{_activeStrokeTrace.TraceId} Point:{point} Size:{_inputState.CurrentSize}");

            // Notify listeners (e.g. UI to close panels)
            OnStrokeStarted?.Invoke();
            
            // CRITICAL FIX: Force sync Renderer state with Service state.
            _inputState.SyncToRenderer();

            // Create Domain Entity
            uint id = (uint)Random.Range(0, int.MaxValue); // Simple random ID
            uint colorInt = ColorToUInt(_inputState.CurrentColor);
            
            // Resolve Brush ID
            ushort brushId = GetBrushId(_inputState.CurrentStrategy);
            
            _sessionContext.StartStroke(id, brushId, colorInt, _inputState.CurrentSize);

            // Network Sync: Begin Stroke
            if (_networkService != null && _networkService.isActiveAndEnabled)
            {
                _networkService.OnLocalStrokeStarted(id, brushId, _inputState.CurrentColor, _inputState.CurrentSize, _inputState.IsEraser);
            }

            _inputProcessor.Reset(point);
            AddPoint(point);

            // Network Sync: Send the first point immediately
            // This is critical because BeginStrokePacket does not contain coordinates.
            if (_networkService != null && _networkService.isActiveAndEnabled)
            {
                _networkService.OnLocalStrokeMoved(point);
            }
        }

        public void MoveStroke(LogicPoint point)
        {
            var result = _inputProcessor.Process(
                point,
                _inputState.IsEraser,
                _inputState.CurrentSize,
                _inputState.CurrentStrategy,
                _logicToWorldRatio
            );

            if (!result.ShouldAdd) return;

            LogicPoint pointToAdd = result.PointToAdd;
            AddPoint(pointToAdd);
            
            // Log every 10th point or if distance is large? Just log count.
            if (_enableDiagnostics && _sessionContext.RawPoints.Count % 10 == 0)
            {
                 Debug.Log($"[App] MoveStroke ID:{_sessionContext.CurrentStroke?.Id} Count:{_sessionContext.RawPoints.Count} Last:{pointToAdd}");
            }

            // Network Sync: Move Stroke
            if (_networkService != null && _networkService.isActiveAndEnabled)
            {
                _networkService.OnLocalStrokeMoved(pointToAdd);
            }
        }

        public void EndStroke(LogicPoint endPoint)
        {
            if (!_sessionContext.IsDrawing) return;
            AddPoint(endPoint);

            // Network Sync: Ensure the last point is sent
            if (_networkService != null && _networkService.isActiveAndEnabled)
            {
                _networkService.OnLocalStrokeMoved(endPoint);
            }

            EndStroke();
        }

        public void EndStroke()
        {
            if (!_sessionContext.IsDrawing) return;

            // 1. Finish Renderer
            _renderer.EndStroke();
            
            // 2. Finalize Stroke Entity
            var stroke = _sessionContext.EndStroke();

            int pointCount = stroke.Points.Count;
            if (!_inputState.IsEraser && pointCount > 0 && pointCount < 4)
            {
                _renderer.DrawPoints(stroke.Points);
            }

            if (_enableDiagnostics) Debug.Log($"[App] EndStroke ID:{stroke.Id} Points:{pointCount}");

            // FIX: Don't add empty strokes to history
            if (pointCount > 0)
            {
                // OPTIMIZATION: Discard eraser strokes that don't intersect with any existing ink.
                if (_inputState.IsEraser)
                {
                    bool isEffective = _collisionService.IsEraserStrokeEffective(stroke, _historyManager.ActiveStrokeIds);
                    
                    if (!isEffective)
                    {
                        Debug.Log($"[Optimization] Eraser stroke discarded [ID: {stroke.Id}] - Redundant (covered area or no ink).");
                        _renderer.EndStroke();
                        return;
                    }
                }

                // Create Command
                // Note: We copy the points from the domain entity (or raw list).
                // stroke.Points is List<LogicPoint>.
                // We pass the current state configuration.
                
                // Fix: Eraser should use _eraserStrategy if available
                var strategyToUse = _inputState.IsEraser ? _eraserStrategy : _inputState.CurrentStrategy;

                var cmd = new DrawStrokeCommand(
                    stroke.Id.ToString(),
                    stroke.SequenceId,
                    new List<LogicPoint>(stroke.Points),
                    strategyToUse,
                    _inputState.CurrentRuntimeTexture,
                    _inputState.CurrentColor,
                    stroke.Size,
                    _inputState.IsEraser
                );
                
                _historyManager.AddCommand(cmd);
                
                // Spatial Indexing
                _collisionService.Insert(stroke);

                // Network Sync: End Stroke
                if (_networkService != null && _networkService.isActiveAndEnabled)
                {
                    uint checksum = Features.Drawing.Service.Network.DrawingNetworkService.ComputeStrokeChecksum(stroke.Points);
                    _networkService.OnLocalStrokeEnded(checksum, stroke.Points.Count);
                }
            }
            
            // Serialization Check (Debug)
            // var bytes = StrokeSerializer.Serialize(stroke);
            // Debug.Log($"[Stroke] Ended. Bytes: {bytes.Length}");
            
            _renderer.EndStroke();
            _activeStrokeTrace = default;
        }

        public void ForceEndCurrentStroke()
        {
            if (_sessionContext.IsDrawing)
            {
                // Force end the current stroke and add it to history
                // We use the parameterless EndStroke to avoid adding a duplicate point.
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
            uint colorInt = ColorToUInt(color);
            ushort brushId = isEraser ? DrawingConstants.ERASER_BRUSH_ID : (ushort)0;
            
            _sessionContext.StartStroke(id, brushId, colorInt, size);

            // 4. Replay existing points
            
            if (existingPoints != null && existingPoints.Count > 0)
            {
                // Setup stabilization state from last point
                _inputProcessor.Reset(existingPoints[existingPoints.Count - 1]);

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
            if (_sessionContext.IsDrawing)
            {
                foreach (var p in points)
                {
                    _sessionContext.AddPoint(p);
                }
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
            if (_sessionContext.IsDrawing)
            {
                _sessionContext.AddPoint(point);
                
                StrokeDrawHelper.DrawIncremental(
                    new StrokeDrawContext(_renderer, _smoothingService),
                    _sessionContext.RawPoints,
                    _sessionContext.RawPoints.Count - 1,
                    _inputState.IsEraser
                );
            }
        }

        // --- Network Sync Helpers (Proposed) ---

        public void CommitRemoteStroke(StrokeEntity stroke)
        {
            _remoteHandler?.CommitRemoteStroke(stroke);
        }

        public void ReceiveRemoteStroke(StrokeEntity stroke)
        {
            _remoteHandler?.ReceiveRemoteStroke(stroke);
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
