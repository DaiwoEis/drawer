using System.Collections.Generic;
using Features.Drawing.Domain.Entity;
using Features.Drawing.Domain.ValueObject;

namespace Features.Drawing.Domain.Context
{
    /// <summary>
    /// Holds the transient state of the current drawing session.
    /// Responsible for managing the "in-progress" stroke.
    /// </summary>
    public class DrawingSessionContext
    {
        // The stroke currently being built (Half-finished product)
        public StrokeEntity CurrentStroke { get; private set; }
        
        // Raw input points buffer (for smoothing algorithm)
        private readonly List<LogicPoint> _rawPointsBuffer = new List<LogicPoint>(1024);
        public List<LogicPoint> RawPoints => _rawPointsBuffer;

        // State flag
        public bool IsDrawing => CurrentStroke != null;
        
        // Sequence ID generator (Moved here from AppService)
        private long _nextSequenceId = 1;

        public long GetNextSequenceId() => _nextSequenceId++;

        public void StartStroke(uint id, ushort brushId, uint color, float size)
        {
            long seqId = GetNextSequenceId();
            // Create new entity
            CurrentStroke = new StrokeEntity(id, 0, brushId, 0, color, size, seqId);
            _rawPointsBuffer.Clear();
        }

        public void AddPoint(LogicPoint point)
        {
            if (!IsDrawing) return;
            _rawPointsBuffer.Add(point);
            // Note: StrokeEntity might also need AddPoints, or we build it at the end
            // Based on existing logic, we add to Entity in real-time
            CurrentStroke.AddPoints(new [] { point });
        }

        public StrokeEntity EndStroke()
        {
            if (!IsDrawing) return null;
            
            var result = CurrentStroke;
            result.EndStroke();
            
            CurrentStroke = null;
            return result;
        }
    }
}