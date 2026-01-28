using System.Collections.Generic;
using Features.Drawing.Domain.ValueObject;
using Features.Drawing.Domain.Network;
using Features.Drawing.Domain.Interface;

namespace Features.Drawing.Service.Network
{
    /// <summary>
    /// Manages the state of a single remote stroke being drawn (The "Ghost").
    /// Buffers points and handles rendering to the Ghost Layer.
    /// </summary>
    public class RemoteStrokeContext
    {
        public uint StrokeId { get; }
        public bool IsActive { get; private set; }
        public BeginStrokePacket Metadata { get; private set; }
        
        // Data Buffer
        private List<LogicPoint> _points = new List<LogicPoint>(512);
        private ushort _lastSequenceId = 0;
        
        // Rendering State
        private readonly IStrokeRenderer _ghostRenderer;
        private LogicPoint _lastRenderedPoint;
        private bool _hasRenderedFirstPoint = false;

        public RemoteStrokeContext(uint strokeId, IStrokeRenderer ghostRenderer)
        {
            StrokeId = strokeId;
            _ghostRenderer = ghostRenderer;
            IsActive = true;
        }

        public void SetMetadata(BeginStrokePacket metadata)
        {
            Metadata = metadata;
        }

        public void ProcessUpdate(UpdateStrokePacket packet)
        {
            if (!IsActive) return;

            // Decompress payload
            // We need the origin point for delta decompression.
            // If this is the first packet, origin is implicit? 
            // Or we assume the first point in the list is absolute?
            // Protocol spec says: "First point of stroke ... is absolute".
            // So if _points is empty, the first point in decompressed list MUST be absolute.
            
            // Wait, DeltaCompressor needs an origin.
            // If we have points, origin is _points.Last().
            // If we don't, the packet payload MUST start with an absolute point (or 0,0 relative to 0,0).
            
            LogicPoint origin = _points.Count > 0 ? _points[_points.Count - 1] : new LogicPoint(0, 0, 0);
            
            var newPoints = StrokeDeltaCompressor.Decompress(origin, packet.Payload);
            
            if (newPoints.Count == 0) return;

            // Add to buffer
            _points.AddRange(newPoints);
            _lastSequenceId = packet.Sequence;

            // Render to Ghost Layer immediately
            // Optimization: Only draw the new segment
            // We need to connect from the last rendered point to the new points.
            
            // If this is the very first batch, just draw them.
            // If subsequent batch, we need to include the last point of previous batch to ensure continuity.
            
            if (_hasRenderedFirstPoint && _points.Count > newPoints.Count)
            {
                // Prepend the last known point to the draw list for continuity
                // But DrawPoints expects a list. 
                // Let's create a temp list for rendering?
                // Or just rely on the fact that IStrokeRenderer implementations usually handle line strips?
                // CanvasRenderer.DrawPoints handles interpolation.
                // If we pass [A, B, C], it draws A->B->C.
                // Next time we pass [C, D, E], it draws C->D->E.
                // We need to pass C again.
                
                var drawBatch = new List<LogicPoint>(newPoints.Count + 1);
                drawBatch.Add(_lastRenderedPoint);
                drawBatch.AddRange(newPoints);
                _ghostRenderer.DrawPoints(drawBatch);
            }
            else
            {
                _ghostRenderer.DrawPoints(newPoints);
            }

            _lastRenderedPoint = newPoints[newPoints.Count - 1];
            _hasRenderedFirstPoint = true;
        }

        public List<LogicPoint> GetFullPoints()
        {
            return _points;
        }

        public void Finish()
        {
            IsActive = false;
        }
    }
}
