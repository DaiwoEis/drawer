using System.Collections.Generic;
using UnityEngine;
using Features.Drawing.Domain.Network;
using Features.Drawing.Domain.ValueObject;
using Features.Drawing.Domain.Interface;
using Features.Drawing.App;
using Features.Drawing.Domain;

namespace Features.Drawing.Service.Network
{
    /// <summary>
    /// Service that coordinates network packets and drawing actions.
    /// Acts as the bridge between DrawingAppService and the Network Layer.
    /// </summary>
    public class DrawingNetworkService : MonoBehaviour
    {
        [Header("References")]
        // Decoupled: Removed direct reference to DrawingAppService
        [SerializeField] private Features.Drawing.Presentation.GhostOverlayRenderer _ghostRenderer;

        [Header("Security")]
        [SerializeField] private int _maxActiveRemoteStrokes = 32;
        // _maxPointsPerUpdate removed as packet.Count (byte) limits us to 255 points, which is safe.

        // Dependencies
        private IDrawingNetworkClient _networkClient;
        private IBrushRegistry _brushRegistry;

        // Events
        public event System.Action<Features.Drawing.Domain.Entity.StrokeEntity> OnRemoteStrokeCommitted;
        
        // State
        private Dictionary<uint, RemoteStrokeContext> _activeRemoteStrokes = new Dictionary<uint, RemoteStrokeContext>();
        
        // Local Buffer (for sending)
        private List<LogicPoint> _pendingLocalPoints = new List<LogicPoint>(64);
        private uint _currentLocalStrokeId;
        private ushort _currentSequence;
        private LogicPoint _lastSentPoint;
        private float _lastSendTime; // For adaptive batching
        private byte[] _lastSentPayload; // For redundancy

        // Reusable Buffers for Zero-GC
        private byte[] _compressionBuffer = new byte[4096]; // 4KB should be enough for a batch of 10-20 points
        private List<LogicPoint> _decompressionBuffer = new List<LogicPoint>(64);

        public void Initialize(IDrawingNetworkClient client)
        {
            _networkClient = client;
            if (_networkClient != null)
            {
                _networkClient.OnPacketReceived += OnPacketReceived;
            }
        }

        public void InitializeBrushRegistry(IBrushRegistry registry)
        {
            _brushRegistry = registry;
        }

        private void OnDestroy()
        {
            if (_networkClient != null)
            {
                _networkClient.OnPacketReceived -= OnPacketReceived;
            }
        }

        // --- Outbound (Sending) ---

        public void OnLocalStrokeStarted(uint strokeId, ushort brushId, Color color, float size, bool isEraser)
        {
            if (_networkClient == null) return;

            _currentLocalStrokeId = strokeId;
            _currentSequence = 0;
            _pendingLocalPoints.Clear();
            _lastSentPoint = default; // Will be set on first point add
            _lastSendTime = Time.time;
            _lastSentPayload = null;

            // Construct Begin Packet
            ushort resolvedBrushId = isEraser
                ? Common.Constants.DrawingConstants.ERASER_BRUSH_ID
                : brushId;

            var packet = new BeginStrokePacket
            {
                StrokeId = strokeId,
                BrushId = resolvedBrushId,
                Color = ColorToUInt(color),
                Size = size,
                Seed = 0 // TODO: Add seed to app service if needed
            };

            _networkClient.SendBeginStroke(packet);
        }

        public void OnLocalStrokeMoved(LogicPoint point)
        {
            if (_networkClient == null) return;

            // If this is the very first point of the stroke, we must ensure 
            // the compression uses (0,0) as origin so the first point is encoded absolutely.
            if (_pendingLocalPoints.Count == 0 && _currentSequence == 0)
            {
                _lastSentPoint = new LogicPoint(0, 0, 0); 
            }

            _pendingLocalPoints.Add(point);

            // Adaptive Batching:
            // Send if we have enough points OR if enough time has passed (e.g. 33ms = 30Hz)
            bool timeThresholdMet = (Time.time - _lastSendTime) >= 0.033f;
            bool countThresholdMet = _pendingLocalPoints.Count >= 10;
            
            // Only send if we have at least 1 point and threshold is met
            // Exception: If count is very high (buffer overflow protection), send immediately
            if (_pendingLocalPoints.Count > 0 && (timeThresholdMet || countThresholdMet))
            {
                FlushPendingPoints();
            }
        }

        public void OnLocalStrokeEnded(uint checksum, int totalPoints)
        {
            if (_networkClient == null) return;

            // Flush remaining points
            FlushPendingPoints();

            var packet = new EndStrokePacket
            {
                StrokeId = _currentLocalStrokeId,
                TotalPoints = (ushort)totalPoints,
                Checksum = checksum
            };

            _networkClient.SendEndStroke(packet);
            _pendingLocalPoints.Clear();
        }

        private void FlushPendingPoints()
        {
            if (_pendingLocalPoints.Count == 0) return;

            // Handle potential overflow if called with > 255 points (though OnLocalStrokeMoved tries to prevent this)
            // We split into chunks of 255 max.
            int processedCount = 0;
            while (processedCount < _pendingLocalPoints.Count)
            {
                int remaining = _pendingLocalPoints.Count - processedCount;
                int batchSize = Mathf.Min(remaining, 255);
                
                // Get range slice
                var batchPoints = _pendingLocalPoints.GetRange(processedCount, batchSize);
                
                // Zero-GC Compression (using reusable buffer)
                // Ensure buffer is large enough? 4KB is plenty for 255 points
                
                int bytesWritten = StrokeDeltaCompressor.Compress(_lastSentPoint, batchPoints, _compressionBuffer, 0);
                
                byte[] payload = new byte[bytesWritten];
                System.Buffer.BlockCopy(_compressionBuffer, 0, payload, 0, bytesWritten);
                
                var packet = new UpdateStrokePacket
                {
                    StrokeId = _currentLocalStrokeId,
                    Sequence = _currentSequence++,
                    Count = (byte)batchSize,
                    Payload = payload,
                    RedundantPayload = _lastSentPayload
                };

                _networkClient.SendUpdateStroke(packet);
                
                // Update state for next batch
                _lastSentPoint = batchPoints[batchPoints.Count - 1];
                _lastSentPayload = payload;
                
                processedCount += batchSize;
            }
            
            _pendingLocalPoints.Clear();
            _lastSendTime = Time.time;
        }

        private void Update()
        {
            if (_activeRemoteStrokes.Count > 0)
            {
                // Prediction & Retained Rendering Loop
                _ghostRenderer.BeginFrame();
                
                foreach (var kvp in _activeRemoteStrokes)
                {
                    kvp.Value.Update(Time.deltaTime);
                }
            }
            else
            {
                 _ghostRenderer.BeginFrame();
            }
        }

        private void OnPacketReceived(object packetObj)
        {
            switch (packetObj)
            {
                case BeginStrokePacket begin:
                    HandleBeginStroke(begin);
                    break;
                case UpdateStrokePacket update:
                    HandleUpdateStroke(update);
                    break;
                case EndStrokePacket end:
                    HandleEndStroke(end);
                    break;
                case AbortStrokePacket abort:
                    HandleAbortStroke(abort);
                    break;
            }
        }

        private void HandleBeginStroke(BeginStrokePacket packet)
        {
            if (_activeRemoteStrokes.ContainsKey(packet.StrokeId)) return; // Duplicate

            // Security: Limit active strokes to prevent DoS (Memory Exhaustion)
            if (_activeRemoteStrokes.Count >= _maxActiveRemoteStrokes)
            {
                Debug.LogWarning($"[Security] Ignored remote stroke {packet.StrokeId}. Max active strokes ({_maxActiveRemoteStrokes}) reached.");
                return;
            }

            // Security: Basic Validation
            if (packet.Size < 0 || packet.Size > 500) // 500 is a reasonable max brush size
            {
                Debug.LogWarning($"[Security] Ignored remote stroke {packet.StrokeId}. Invalid size {packet.Size}.");
                return;
            }

            // Setup Renderer
            var context = new RemoteStrokeContext(packet.StrokeId, _ghostRenderer);
            context.SetMetadata(packet); // Store metadata
            
            // Resolve Strategy and pass to context
            var strategy = _brushRegistry?.GetBrushStrategy(packet.BrushId);
            context.SetStrategy(strategy);

            _activeRemoteStrokes.Add(packet.StrokeId, context);
        }

        private void HandleUpdateStroke(UpdateStrokePacket packet)
        {
            if (_activeRemoteStrokes.TryGetValue(packet.StrokeId, out var context))
            {
                // Security: Payload check
                if (packet.Payload == null) return;
                if (packet.Payload.Length > 1024 * 10) // 10KB limit per packet
                {
                     Debug.LogWarning($"[Security] Update packet too large for stroke {packet.StrokeId}.");
                     return;
                }
                
                context.ProcessUpdate(packet);
            }
        }

        private void HandleEndStroke(EndStrokePacket packet)
        {
            if (_activeRemoteStrokes.TryGetValue(packet.StrokeId, out var context))
            {
                context.Finish();
                
                // 1. Reconstruct full stroke entity
                var points = context.GetFullPoints();
                if (points.Count > 0)
                {
                    // Construct StrokeEntity
                    var meta = context.Metadata;
                    var stroke = new Features.Drawing.Domain.Entity.StrokeEntity(
                        meta.StrokeId,
                        0, // Local Sequence ID? Or Remote? For now 0
                        meta.BrushId,
                        meta.Seed,
                        meta.Color,
                        meta.Size,
                        0 // Sequence
                    );
                    
                    stroke.AddPoints(points);
                    stroke.EndStroke();

                    // 2. Notify Listeners (AppService)
                    OnRemoteStrokeCommitted?.Invoke(stroke);
                }
                
                // 3. Clear Ghost
                _activeRemoteStrokes.Remove(packet.StrokeId);
            }
        }

        private void HandleAbortStroke(AbortStrokePacket packet)
        {
            if (_activeRemoteStrokes.ContainsKey(packet.StrokeId))
            {
                _activeRemoteStrokes.Remove(packet.StrokeId);
            }
        }

        public static uint ColorToUInt(Color color)
        {
            Color32 c32 = color;
            return (uint)((c32.r << 24) | (c32.g << 16) | (c32.b << 8) | c32.a);
        }
        
        public static Color UIntToColor(uint color)
        {
            byte r = (byte)((color >> 24) & 0xFF);
            byte g = (byte)((color >> 16) & 0xFF);
            byte b = (byte)((color >> 8) & 0xFF);
            byte a = (byte)(color & 0xFF);
            return new Color32(r, g, b, a);
        }
    }
}
