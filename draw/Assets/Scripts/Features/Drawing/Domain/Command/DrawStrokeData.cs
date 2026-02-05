using UnityEngine;
using System.Collections.Generic;
using Features.Drawing.Domain.Interface;
using Features.Drawing.Domain.ValueObject;

namespace Features.Drawing.Domain.Command
{
    [System.Serializable]
    public class DrawStrokeData : ICommandData
    {
        // Protected set to allow subclasses or deserializers to set
        public string Id { get; protected set; }
        public long SequenceId { get; protected set; }
        
        public List<LogicPoint> Points { get; protected set; }
        public Color Color { get; protected set; }
        public float Size { get; protected set; }
        public bool IsEraser { get; protected set; }
        
        // Replaces BrushStrategy object with a string ID for data persistence
        public string BrushId { get; protected set; } 

        public DrawStrokeData(string id, long sequenceId, List<LogicPoint> points, string brushId, Color color, float size, bool isEraser)
        {
            Id = id;
            SequenceId = sequenceId;
            Points = points; 
            BrushId = brushId;
            Color = color;
            Size = size;
            IsEraser = isEraser;
        }
        
        protected DrawStrokeData() { } 
    }
}