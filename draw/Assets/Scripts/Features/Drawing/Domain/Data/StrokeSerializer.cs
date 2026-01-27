using UnityEngine;
using Features.Drawing.Domain.ValueObject;

namespace Features.Drawing.Domain.Data
{
    /// <summary>
    /// Utility for compressing LogicPoints for network transmission.
    /// Quantizes float coordinates to short (Fixed Point).
    /// </summary>
    public static class StrokeSerializer
    {
        // 0 to 65535 maps to 0.0 to 1.0 in normalized canvas space?
        // OR: Assume a maximum canvas size (e.g. 8192) and map to shorts.
        // Approach: Use a fixed precision factor (e.g. 100).
        // 123.45f -> 12345 (short)
        
        private const float PRECISION = 10.0f; // 0.1 pixel precision is enough for drawing
        
        // Struct to hold compressed data
        public struct CompressedPoint
        {
            public ushort x;
            public ushort y;
            public byte pressure; // 0-255
        }

        public static CompressedPoint Compress(LogicPoint point)
        {
            return new CompressedPoint
            {
                x = point.X,
                y = point.Y,
                pressure = point.Pressure
            };
        }

        public static LogicPoint Decompress(CompressedPoint cPoint)
        {
            return new LogicPoint(
                cPoint.x,
                cPoint.y,
                cPoint.pressure
            );
        }
    }
}
