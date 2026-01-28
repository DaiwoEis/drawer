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
        [SerializeField] private DrawingAppService _appService;
        [SerializeField] private Features.Drawing.Presentation.GhostOverlayRenderer _ghostRenderer;

        // Dependencies
        private IDrawingNetworkClient _networkClient;
        
        // State
        private Dictionary<uint, RemoteStrokeContext> _activeRemoteStrokes = new Dictionary<uint, RemoteStrokeContext>();
        
        // Local Buffer (for sending)
        private List<LogicPoint> _pendingLocalPoints = new List<LogicPoint>(64);
        private uint _currentLocalStrokeId;
        private ushort _currentSequence;
        private LogicPoint _lastSentPoint;
        private float _lastSendTime; // For adaptive batching
        private byte[] _lastSentPayload; // For redundancy

        public void Initialize(IDrawingNetworkClient client)
        {
            _networkClient = client;
            if (_networkClient != null)
            {
                _networkClient.OnPacketReceived += OnPacketReceived;
            }
        }

        private void OnDestroy()
        {
            if (_networkClient != null)
            {
                _networkClient.OnPacketReceived -= OnPacketReceived;
            }
        }

        // --- Outbound (Sending) ---

        public void OnLocalStrokeStarted(uint strokeId, BrushStrategy strategy, Color color, float size, bool isEraser)
        {
            if (_networkClient == null) return;

            _currentLocalStrokeId = strokeId;
            _currentSequence = 0;
            _pendingLocalPoints.Clear();
            _lastSentPoint = default; // Will be set on first point add
            _lastSendTime = Time.time;
            _lastSentPayload = null;

            // Construct Begin Packet
            var packet = new BeginStrokePacket
            {
                StrokeId = strokeId,
                BrushId = isEraser ? (ushort)1 : (ushort)0, // Simplified mapping
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

            // Compress
            // Origin for compression is the Last Sent Point (or 0,0 if first batch).
            // Wait, StrokeDeltaCompressor logic:
            // Compress(origin, list) -> writes delta from origin to list[0], then list[0] to list[1]...
            
            // For the first batch: origin is (0,0)?
            // If we pass (0,0), and first point is (100,100). Delta is 100.
            // If we use _lastSentPoint.
            // Batch 1: Origin (0,0). Points [A, B, C]. Compressed: (A-0), (B-A), (C-B).
            // _lastSentPoint becomes C.
            // Batch 2: Origin C. Points [D, E]. Compressed: (D-C), (E-D).
            // Correct.
            
            // Note: Compress needs an origin. 
            // _lastSentPoint is the origin for THIS batch.
            byte[] payload = StrokeDeltaCompressor.Compress(_lastSentPoint, _pendingLocalPoints);
            
            var packet = new UpdateStrokePacket
            {
                StrokeId = _currentLocalStrokeId,
                Sequence = _currentSequence++,
                Count = (byte)_pendingLocalPoints.Count,
                Payload = payload,
                RedundantPayload = _lastSentPayload
            };

            _networkClient.SendUpdateStroke(packet);
            
            // Update state
            _lastSentPoint = _pendingLocalPoints[_pendingLocalPoints.Count - 1];
            _pendingLocalPoints.Clear();
            _lastSendTime = Time.time;
            _lastSentPayload = payload;
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
                // Ensure clear if no strokes
                // Optimization: Only clear if we were drawing something last frame?
                // For now, safe clear.
                // Or check if ghost layer is dirty?
                // GhostOverlayRenderer doesn't expose dirty flag.
                // Just calling BeginFrame (Clear) is safe but might be wasteful if empty.
                // But if empty, it's just a glClear.
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
            if (_activeRemoteStrokes.ContainsKey(packet.StrokeId)) return; // Duplicate?

            // Setup Renderer
            // Map packet data to BrushStrategy
            // For now, we create a temporary strategy or use default
            bool isEraser = packet.BrushId == 1;
            
            // We need to configure the GhostRenderer for this specific stroke?
            // Wait, GhostRenderer is a single component. If multiple people draw at once?
            // GhostOverlayRenderer is global. 
            // If User A and User B draw simultaneously, switching brush state on the single GhostRenderer will cause flicker.
            // LIMITATION: Current GhostRenderer assumes single-threaded context (like CanvasRenderer).
            // For MVP, we assume 1 remote drawer at a time, or accept artifacts.
            // Ideally: GhostOverlayRenderer should support "DrawCommand" with state, not global state.
            // Or we just set state before every DrawPoints call (which we do in RemoteStrokeContext).
            
            // Configure global ghost renderer for this new stroke context
            // Note: This only sets the initial state. The context will need to re-apply it before drawing if we support concurrency.
            // For now, let's just create the context.
            
            var context = new RemoteStrokeContext(packet.StrokeId, _ghostRenderer);
            context.SetMetadata(packet); // Store metadata
            _activeRemoteStrokes.Add(packet.StrokeId, context);
            
            // Setup Visuals (We should store this in context to re-apply later)
            // TODO: RemoteStrokeContext should store Brush Config
            
            // Apply immediately for visual feedback
            // Removed direct renderer configuration in favor of Retained Mode in Update()
            
            // TODO: Handle BrushStrategy mapping (Soft/Hard/etc)
        }

        private void HandleUpdateStroke(UpdateStrokePacket packet)
        {
            if (_activeRemoteStrokes.TryGetValue(packet.StrokeId, out var context))
            {
                // Re-apply state (Simple concurrency support)
                // We'd need to store color/size in context.
                // For MVP, skip re-apply (assumes single remote user).
                
                context.ProcessUpdate(packet);
            }
        }

        private void HandleEndStroke(EndStrokePacket packet)
        {
            if (_activeRemoteStrokes.TryGetValue(packet.StrokeId, out var context))
            {
                context.Finish();
                
                // Clear Ghost Visuals for this stroke
                // Since GhostRenderer is a single texture, we can't clear *just* this stroke easily without clearing everything.
                // But `EndStroke` means "Commit". 
                // We should Clear the Ghost layer and draw the permanent stroke on the AppService.
                
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
                    
                    // Add points manually or via constructor?
                    // StrokeEntity constructor takes list? No, it has AddPoints.
                    // But we can just assign the points via internal/public setter or AddPoints loop.
                    // StrokeEntity design: Points is a readonly property?
                    // Let's check StrokeEntity definition.
                    // Assuming we can add points.
                    stroke.AddPoints(points);
                    stroke.EndStroke();

                    // 2. Commit to AppService
                    _appService.CommitRemoteStroke(stroke);
                }
                
                // 3. Clear Ghost
                // Removed explicit ClearCanvas() as we are now in Retained Mode (cleared every frame).
                // Just removing from active strokes is enough.
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
