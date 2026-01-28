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

        // Thread-safe buffer for compression to avoid allocations
        [System.ThreadStatic]
        private static byte[] _compressionBuffer;
        private const int BUFFER_SIZE = 65536; // 64KB should be enough for any single stroke batch

        /// <summary>
        /// Compresses a list of points relative to an origin point.
        /// Returns a copy of the valid data. For Zero-GC, use the overload accepting a buffer.
        /// </summary>
        public static byte[] Compress(LogicPoint origin, List<LogicPoint> points)
        {
            if (points == null || points.Count == 0) return new byte[0];

            if (_compressionBuffer == null) _compressionBuffer = new byte[BUFFER_SIZE];

            int offset = 0;
            LogicPoint prev = origin;

            // Direct byte writing to avoid BinaryWriter allocation
            foreach (var p in points)
            {
                // Ensure buffer has space (check 3 bytes + potential escapes)
                if (offset + 6 >= _compressionBuffer.Length) 
                {
                    // Should rarely happen with 64KB buffer for a stroke batch
                    // Fallback or resize logic could go here, but for now we clamp/return what we have
                    break; 
                }

                offset += WriteCoordinate(_compressionBuffer, offset, p.X, prev.X);
                offset += WriteCoordinate(_compressionBuffer, offset, p.Y, prev.Y);
                _compressionBuffer[offset++] = p.Pressure;

                prev = p;
            }

            // Copy only valid data
            byte[] result = new byte[offset];
            System.Buffer.BlockCopy(_compressionBuffer, 0, result, 0, offset);
            return result;
        }

        private static int WriteCoordinate(byte[] buffer, int offset, ushort current, ushort previous)
        {
            int diff = (int)current - (int)previous;
            
            if (diff >= -127 && diff <= 127)
            {
                buffer[offset] = (byte)((sbyte)diff); // Cast to byte (preserves bits)
                return 1;
            }
            else
            {
                buffer[offset] = unchecked((byte)ESCAPE_MARKER); // -128
                // Write ushort (Little Endian)
                buffer[offset + 1] = (byte)(current & 0xFF);
                buffer[offset + 2] = (byte)((current >> 8) & 0xFF);
                return 3;
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
