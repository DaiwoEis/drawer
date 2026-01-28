namespace Common.Constants
{
    /// <summary>
    /// Global drawing constants ensuring cross-platform consistency.
    /// </summary>
    public static class DrawingConstants
    {
        /// <summary>
        /// Logical canvas width (0-65535 space mapped to this aspect ratio if needed, 
        /// but usually we treat 0-65535 as normalized 0.0-1.0 range internally).
        /// </summary>
        public const int LOGICAL_RESOLUTION = 65536;

        /// <summary>
        /// Maximum pressure value (byte).
        /// </summary>
        public const int MAX_PRESSURE = 255;
        
        /// <summary>
        /// Default chunk size for network packets (points per packet).
        /// </summary>
        public const int POINTS_PER_PACKET = 16;

        /// <summary>
        /// Reserved Brush ID for Eraser.
        /// </summary>
        public const ushort ERASER_BRUSH_ID = 0xFFFF;

        /// <summary>
        /// Reserved Brush ID for Unknown/Unregistered brushes (Fallback).
        /// </summary>
        public const ushort UNKNOWN_BRUSH_ID = 0xFFFE;

        /// <summary>
        /// Approximate scaling factor to convert pixel size to LogicPoint coordinate space (0-65535).
        /// Based on a reference resolution of 2000px.
        /// </summary>
        public const float LOGIC_TO_WORLD_RATIO = 65535f / 2000f;
    }
}
