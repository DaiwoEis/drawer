using UnityEngine;

namespace Features.Drawing.Presentation
{
    public struct StampData
    {
        public Vector2 Position;
        public float Size;
        public float Rotation;

        public StampData(Vector2 position, float size, float rotation)
        {
            Position = position;
            Size = size;
            Rotation = rotation;
        }
    }
}
