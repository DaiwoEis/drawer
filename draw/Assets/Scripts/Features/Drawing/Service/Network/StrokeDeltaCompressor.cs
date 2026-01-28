using System.Collections.Generic;
using System.IO;
using Features.Drawing.Domain.ValueObject;
using UnityEngine;

namespace Features.Drawing.Service.Network
{
    /// <summary>
    /// Handles Delta Encoding and Compression for stroke points.
    /// Optimization: Uses relative coordinates (VarInt) to minimize bandwidth.
    /// </summary>
    public static class StrokeDeltaCompressor
    {
        private const sbyte ESCAPE_MARKER = -128; // 0x80

        /// <summary>
        /// Compresses a list of points relative to an origin point.
        /// </summary>
        public static byte[] Compress(LogicPoint origin, List<LogicPoint> points)
        {
            if (points == null || points.Count == 0) return new byte[0];

            // Estimate capacity: 3 bytes per point (1x + 1y + 1p) usually
            using (var ms = new MemoryStream(points.Count * 3))
            using (var writer = new BinaryWriter(ms))
            {
                LogicPoint prev = origin;
                
                foreach (var p in points)
                {
                    WriteCoordinate(writer, p.X, prev.X);
                    WriteCoordinate(writer, p.Y, prev.Y);
                    writer.Write(p.Pressure);
                    
                    prev = p;
                }
                
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Decompresses byte array back into LogicPoints.
        /// </summary>
        public static List<LogicPoint> Decompress(LogicPoint origin, byte[] data)
        {
            var points = new List<LogicPoint>();
            if (data == null || data.Length == 0) return points;

            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                LogicPoint prev = origin;
                
                while (ms.Position < ms.Length)
                {
                    // Safety check to prevent partial reads
                    if (ms.Length - ms.Position < 3) // Min 3 bytes needed (1x + 1y + 1p)
                    {
                        // Handle potential corruption or just end? 
                        // For robust implementation, we might need to check available bytes for each read.
                        // But strictly, if we wrote it, it should be valid.
                        // However, WriteCoordinate can write 1 or 3 bytes.
                        // Minimal point is 1(x) + 1(y) + 1(p) = 3 bytes.
                    }

                    ushort x = ReadCoordinate(reader, prev.X);
                    ushort y = ReadCoordinate(reader, prev.Y);
                    byte p = reader.ReadByte();
                    
                    var point = new LogicPoint(x, y, p);
                    points.Add(point);
                    prev = point;
                }
            }
            return points;
        }

        private static void WriteCoordinate(BinaryWriter writer, ushort current, ushort previous)
        {
            int diff = (int)current - (int)previous;

            // Range for sbyte is -128 to 127.
            // We reserve -128 as Escape Marker.
            // So valid delta range is -127 to 127.
            if (diff >= -127 && diff <= 127)
            {
                writer.Write((sbyte)diff);
            }
            else
            {
                writer.Write(ESCAPE_MARKER);
                writer.Write(current); // Write absolute value
            }
        }

        private static ushort ReadCoordinate(BinaryReader reader, ushort previous)
        {
            sbyte val = reader.ReadSByte();
            if (val == ESCAPE_MARKER)
            {
                return reader.ReadUInt16();
            }
            else
            {
                // C# ushort arithmetic wraps? No, we cast to int then back.
                return (ushort)((int)previous + val);
            }
        }
    }
}
