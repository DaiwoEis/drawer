using System.Collections.Generic;
using UnityEngine;
using Features.Drawing.Domain.ValueObject;
using Features.Drawing.Domain.Network;
using Features.Drawing.Presentation; // Added for GhostOverlayRenderer

namespace Features.Drawing.Service.Network
{
    /// <summary>
    /// Manages the state of a single remote stroke being drawn (The "Ghost").
    /// Buffers points and handles rendering to the Ghost Layer.
    /// Implements Client-Side Prediction (Extrapolation) to smooth out network jitter.
    /// </summary>
    public class RemoteStrokeContext
    {
        public uint StrokeId { get; }
        public bool IsActive { get; private set; }
        public BeginStrokePacket Metadata { get; private set; }
        
        // Data Buffer
        private List<LogicPoint> _points = new List<LogicPoint>(512);
        private int _lastReceivedSequenceId = -1; // -1 means no packets received yet
        public int LastReceivedSequenceId => _lastReceivedSequenceId;
        
        // Rendering State
        private readonly GhostOverlayRenderer _ghostRenderer;
        private readonly bool _usePrediction;
        private Features.Drawing.Domain.BrushStrategy _strategy;
        private List<LogicPoint> _predictionBuffer = new List<LogicPoint>(512);
        private StrokeStampGenerator _incrementalStampGenerator = new StrokeStampGenerator();
        private List<StampData> _incrementalStampBuffer = new List<StampData>(256);
        private List<LogicPoint> _newPointsBuffer = new List<LogicPoint>(256);
        private int _lastRenderedIndex = 0;

        public RemoteStrokeContext(uint strokeId, GhostOverlayRenderer ghostRenderer, bool usePrediction)
        {
            StrokeId = strokeId;
            _ghostRenderer = ghostRenderer;
            _usePrediction = usePrediction;
            IsActive = true;
            _lastPacketTime = Time.time;
        }

        public void SetStrategy(Features.Drawing.Domain.BrushStrategy strategy)
        {
            _strategy = strategy;
            ConfigureIncrementalGenerator();
        }

        public void SetMetadata(BeginStrokePacket metadata)
        {
            Metadata = metadata;
            // Debug.Log($"[RemoteStrokeContext] SetMetadata: ID={Metadata.StrokeId}, BrushId={Metadata.BrushId}, Size={Metadata.Size}");
            _lastRenderedIndex = 0;
            _incrementalStampGenerator.Reset();
            ConfigureIncrementalGenerator();
        }

        // Prediction State
        private float _lastPacketTime;
        private Vector2 _velocity; // Pixels (LogicUnits) per second
        private const float PREDICTION_THRESHOLD = 0.033f; // Start predicting after 33ms silence
        private const float MAX_PREDICTION_TIME = 0.100f; // Max 100ms prediction

        public void ProcessUpdate(UpdateStrokePacket packet)
        {
            if (!IsActive) return;
            if (packet.Payload == null || packet.PayloadLength <= 0) return;

            // Packet Loss Handling with Redundancy
            int expectedSeq = _lastReceivedSequenceId + 1;
            int actualSeq = packet.Sequence;

            // If we missed a packet, try to recover from RedundantPayload
            if (actualSeq > expectedSeq)
            {
                if (actualSeq == expectedSeq + 1 && packet.RedundantPayload != null && packet.RedundantPayloadLength > 0)
                {
                    if (packet.RedundantPayloadLength > packet.RedundantPayload.Length) return;
                    LogicPoint recoveryOrigin = _points.Count > 0 ? _points[_points.Count - 1] : new LogicPoint(0, 0, 0);
                    StrokeDeltaCompressor.Decompress(recoveryOrigin, packet.RedundantPayload, 0, packet.RedundantPayloadLength, _points);
                    _lastReceivedSequenceId = actualSeq - 1; 
                }
            }
            
            if (actualSeq <= _lastReceivedSequenceId) return;

            LogicPoint origin = _points.Count > 0 ? _points[_points.Count - 1] : new LogicPoint(0, 0, 0);
            StrokeDeltaCompressor.Decompress(origin, packet.Payload, 0, packet.PayloadLength, _points);
            
            // Update velocity and state
            UpdateVelocity(packet.PayloadLength); // Simplified velocity check
            _lastReceivedSequenceId = actualSeq;
            _lastPacketTime = Time.time;
        }

        private void UpdateVelocity(int pointsAdded)
        {
             if (_points.Count < 2) return;
             
             LogicPoint curr = _points[_points.Count - 1];
             LogicPoint prev = _points[_points.Count - 2];
             
             float dt = Time.time - _lastPacketTime;
             if (dt > 0.001f)
             {
                 Vector2 displacement = new Vector2(curr.X - prev.X, curr.Y - prev.Y);
                 Vector2 instantVel = displacement / dt;
                 _velocity = Vector2.Lerp(_velocity, instantVel, 0.5f);
             }
        }


        public void Update(float deltaTime)
        {
            if (!IsActive) return;
            if (_ghostRenderer == null) return;
            if (!_usePrediction) return;

            // Prepare points to draw
            List<LogicPoint> pointsToDraw = _points;

            // Extrapolation Logic
            float timeSincePacket = Time.time - _lastPacketTime;
            
            if (timeSincePacket > PREDICTION_THRESHOLD && _points.Count > 0)
            {
                // Limit prediction
                float predictionTime = Mathf.Min(timeSincePacket - PREDICTION_THRESHOLD, MAX_PREDICTION_TIME);
                
                if (predictionTime > 0 && _velocity.sqrMagnitude > 1f)
                {
                    LogicPoint lastPoint = _points[_points.Count - 1];
                    Vector2 predictedPos = new Vector2(lastPoint.X, lastPoint.Y) + _velocity * predictionTime;
                    
                    // Clamp to valid range (0-65535)
                    predictedPos.x = Mathf.Clamp(predictedPos.x, 0, 65535);
                    predictedPos.y = Mathf.Clamp(predictedPos.y, 0, 65535);
                    
                    LogicPoint predictedPoint = new LogicPoint((ushort)predictedPos.x, (ushort)predictedPos.y, lastPoint.Pressure);
                    
                    // Reuse buffer to avoid per-frame allocations
                    _predictionBuffer.Clear();
                    _predictionBuffer.AddRange(_points);
                    _predictionBuffer.Add(predictedPoint);
                    pointsToDraw = _predictionBuffer;
                }
            }

            // Draw
            // Need Color and Size from Metadata
            Color color = Color.black; // Default
            float size = 10f;
            bool isEraser = false;

            if (Metadata.StrokeId != 0) // Valid metadata
            {
                // Metadata.Color is uint. Need conversion helper.
                // Assuming Utils exist or manual conversion.
                // RGBA packed?
                // Let's assume DrawingConstants or similar has ColorFromUInt.
                // Or just use default for now if helper missing.
                // Actually DrawingNetworkService had ColorToUInt.
                // We'll interpret it here.
                color = DrawingNetworkService.UIntToColor(Metadata.Color);
                size = Metadata.Size;
                isEraser = Metadata.BrushId == Common.Constants.DrawingConstants.ERASER_BRUSH_ID;
            }

            _ghostRenderer.DrawGhostStroke(pointsToDraw, size, color, isEraser, _strategy);
        }

        public void RenderIncremental()
        {
            if (!IsActive) return;
            if (_ghostRenderer == null) return;
            if (_usePrediction) return;

            if (_points.Count <= _lastRenderedIndex) return;

            Color color = Color.black;
            float size = 10f;
            bool isEraser = false;

            if (Metadata.StrokeId != 0)
            {
                color = DrawingNetworkService.UIntToColor(Metadata.Color);
                size = Metadata.Size;
                isEraser = Metadata.BrushId == Common.Constants.DrawingConstants.ERASER_BRUSH_ID;
            }

            _newPointsBuffer.Clear();
            for (int i = _lastRenderedIndex; i < _points.Count; i++)
            {
                _newPointsBuffer.Add(_points[i]);
            }

            UpdateIncrementalGeneratorScale();
            _incrementalStampGenerator.ProcessPoints(_newPointsBuffer, size, _incrementalStampBuffer);
            _ghostRenderer.DrawGhostStamps(_incrementalStampBuffer, color, isEraser, _strategy);
            _incrementalStampBuffer.Clear();
            _lastRenderedIndex = _points.Count;
        }

        public void RenderFull()
        {
            if (!IsActive) return;
            if (_ghostRenderer == null) return;

            Color color = Color.black;
            float size = 10f;
            bool isEraser = false;

            if (Metadata.StrokeId != 0)
            {
                color = DrawingNetworkService.UIntToColor(Metadata.Color);
                size = Metadata.Size;
                isEraser = Metadata.BrushId == Common.Constants.DrawingConstants.ERASER_BRUSH_ID;
            }

            _ghostRenderer.DrawGhostStroke(_points, size, color, isEraser, _strategy);
        }

        public List<LogicPoint> GetFullPoints()
        {
            return _points;
        }

        public void Finish()
        {
            IsActive = false;
        }

        private void ConfigureIncrementalGenerator()
        {
            if (_strategy == null) return;
            _incrementalStampGenerator.RotationMode = _strategy.RotationMode;
            _incrementalStampGenerator.SpacingRatio = _strategy.SpacingRatio;
            _incrementalStampGenerator.AngleJitter = _strategy.AngleJitter;
        }

        private void UpdateIncrementalGeneratorScale()
        {
            if (_ghostRenderer == null) return;
            _incrementalStampGenerator.SetCanvasResolution(_ghostRenderer.Resolution);
            _incrementalStampGenerator.SetSizeScale(_ghostRenderer.GetBrushSizeScale());
        }
    }
}
