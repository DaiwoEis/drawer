using UnityEngine;
using Common.Constants;

namespace Features.Drawing.Domain.ValueObject
{
    /// <summary>
    /// Represents a point in the logical coordinate system (0-65535).
    /// Immutable struct to ensure value semantics.
    /// </summary>
    [System.Serializable]
    public struct LogicPoint
    {
        public readonly ushort X;
        public readonly ushort Y;
        public readonly byte Pressure; // 0-255

        public LogicPoint(ushort x, ushort y, byte pressure)
        {
            X = x;
            Y = y;
            Pressure = pressure;
        }

        /// <summary>
        /// Converts Unity normalized coordinates (0.0-1.0) to LogicPoint.
        /// </summary>
        /// <param name="normalizedPos">Position in 0-1 range</param>
        /// <param name="pressure">Pressure in 0-1 range</param>
        public static LogicPoint FromNormalized(Vector2 normalizedPos, float pressure)
        {
            ushort x = (ushort)(Mathf.Clamp01(normalizedPos.x) * (DrawingConstants.LOGICAL_RESOLUTION - 1));
            ushort y = (ushort)(Mathf.Clamp01(normalizedPos.y) * (DrawingConstants.LOGICAL_RESOLUTION - 1));
            byte p = (byte)(Mathf.Clamp01(pressure) * DrawingConstants.MAX_PRESSURE);
            return new LogicPoint(x, y, p);
        }

        /// <summary>
        /// Converts LogicPoint back to Unity normalized coordinates (0.0-1.0).
        /// </summary>
        public Vector2 ToNormalized()
        {
            float u = X / (float)(DrawingConstants.LOGICAL_RESOLUTION - 1);
            float v = Y / (float)(DrawingConstants.LOGICAL_RESOLUTION - 1);
            return new Vector2(u, v);
        }

        /// <summary>
        /// Get normalized pressure (0.0-1.0).
        /// </summary>
        public float GetNormalizedPressure()
        {
            return Pressure / (float)DrawingConstants.MAX_PRESSURE;
        }
        
        public override string ToString()
        {
            return $"({X}, {Y}, p={Pressure})";
        }
    }
}
