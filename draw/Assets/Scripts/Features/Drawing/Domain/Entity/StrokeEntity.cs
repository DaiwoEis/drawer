using System.Collections.Generic;
using Features.Drawing.Domain.ValueObject;

namespace Features.Drawing.Domain.Entity
{
    /// <summary>
    /// Represents a complete stroke in the drawing session.
    /// Core entity for domain logic.
    /// </summary>
    public class StrokeEntity
    {
        public uint Id { get; private set; }
        public ushort AuthorId { get; private set; }
        public ushort BrushId { get; private set; }
        public uint Seed { get; private set; }
        public bool IsEnded { get; private set; }
        
        // Color encoded as integer (RGBA) for simple serialization
        public uint ColorRGBA { get; private set; }
        
        private readonly List<LogicPoint> _points;
        public IReadOnlyList<LogicPoint> Points => _points;

        public StrokeEntity(uint id, ushort authorId, ushort brushId, uint seed, uint colorRGBA)
        {
            Id = id;
            AuthorId = authorId;
            BrushId = brushId;
            Seed = seed;
            ColorRGBA = colorRGBA;
            _points = new List<LogicPoint>();
            IsEnded = false;
        }

        public void AddPoints(IEnumerable<LogicPoint> newPoints)
        {
            if (IsEnded) return; // Should throw domain exception in strict mode
            _points.AddRange(newPoints);
        }

        public void EndStroke()
        {
            IsEnded = true;
        }
    }
}
