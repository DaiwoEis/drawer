using System.Collections.Generic;
using System.IO;
using Features.Drawing.Domain.ValueObject;
using UnityEngine;

namespace Features.Drawing.Service.Network
{
    /// <summary>
    /// Handles Delta Encoding and Compression for stroke points.
    /// Optimization: Uses relative coordinates (VarInt) to minimize bandwidth.
    /// Zero-GC implementation.
    /// </summary>
    public static class StrokeDeltaCompressor
    {
        private const sbyte ESCAPE_MARKER = -128; // 0x80

        /// <summary>
        /// Compresses a list of points relative to an origin point into a provided buffer.
        /// Returns the number of bytes written.
        /// </summary>
        public static int Compress(LogicPoint origin, List<LogicPoint> points, byte[] targetBuffer, int bufferOffset = 0)
        {
            if (points == null || points.Count == 0) return 0;
            if (targetBuffer == null) return 0;

            int offset = bufferOffset;
            LogicPoint prev = origin;
            int maxOffset = targetBuffer.Length;

            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                
                // Check buffer space (conservative check: max 3 bytes per coord + 1 byte pressure = 7 bytes)
                if (offset + 7 >= maxOffset)
                {
                    Debug.LogWarning("[StrokeDeltaCompressor] Buffer overflow risk. Truncating batch.");
                    break;
                }

                offset += WriteCoordinate(targetBuffer, offset, p.X, prev.X);
                offset += WriteCoordinate(targetBuffer, offset, p.Y, prev.Y);
                targetBuffer[offset++] = p.Pressure;

                prev = p;
            }

            return offset - bufferOffset;
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
        /// Populates the provided list to avoid allocation.
        /// </summary>
        public static void Decompress(LogicPoint origin, byte[] data, List<LogicPoint> targetList)
        {
            if (data == null || data.Length == 0) return;
            if (targetList == null) return;

            // targetList.Clear(); // Caller responsibility? Usually yes, let's assume caller wants to append or clear.
            // Documentation says "Populates", usually implies append or fill. Let's Append.
            
            int offset = 0;
            int length = data.Length;
            LogicPoint prev = origin;

            while (offset < length)
            {
                // Safety check: Min 3 bytes needed (1x + 1y + 1p)
                if (length - offset < 3) 
                {
                    break;
                }

                ushort x;
                int readX = ReadCoordinate(data, offset, prev.X, out x);
                offset += readX;
                
                // Check bounds again for Y (min 2 bytes remaining)
                if (length - offset < 2) break;

                ushort y;
                int readY = ReadCoordinate(data, offset, prev.Y, out y);
                offset += readY;

                // Check bounds for Pressure (1 byte)
                if (offset >= length) break;
                
                byte p = data[offset++];
                
                var point = new LogicPoint(x, y, p);
                targetList.Add(point);
                prev = point;
            }
        }

        private static int ReadCoordinate(byte[] buffer, int offset, ushort previous, out ushort result)
        {
            sbyte val = (sbyte)buffer[offset];
            if (val == ESCAPE_MARKER)
            {
                // Ensure we have enough bytes (checked in loop but good to be safe)
                // We need offset+1 and offset+2
                int b1 = buffer[offset + 1];
                int b2 = buffer[offset + 2];
                result = (ushort)(b1 | (b2 << 8));
                return 3;
            }
            else
            {
                result = (ushort)((int)previous + val);
                return 1;
            }
        }
    }
}
