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
            return Compress(origin, points, 0, points.Count, targetBuffer, bufferOffset);
        }

        public static int Compress(LogicPoint origin, List<LogicPoint> points, int startIndex, int count, byte[] targetBuffer, int bufferOffset = 0)
        {
            if (points == null || points.Count == 0 || count <= 0) return 0;
            if (targetBuffer == null) return 0;

            int offset = bufferOffset;
            LogicPoint prev = origin;
            int maxOffset = targetBuffer.Length;
            int endIndex = startIndex + count;

            for (int i = startIndex; i < endIndex; i++)
            {
                var p = points[i];

                int required = GetCoordinateSize(p.X, prev.X) + GetCoordinateSize(p.Y, prev.Y) + 1;
                if (offset + required > maxOffset)
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

        private static int GetCoordinateSize(ushort current, ushort previous)
        {
            int diff = (int)current - (int)previous;
            return diff >= -127 && diff <= 127 ? 1 : 3;
        }


        /// <summary>
        /// Decompresses byte array back into LogicPoints.
        /// Populates the provided list to avoid allocation.
        /// </summary>
        public static void Decompress(LogicPoint origin, byte[] data, List<LogicPoint> targetList)
        {
            if (data == null || data.Length == 0) return;
            Decompress(origin, data, 0, data.Length, targetList);
        }

        public static void Decompress(LogicPoint origin, byte[] data, int offset, int length, List<LogicPoint> targetList)
        {
            if (data == null || length <= 0) return;
            if (targetList == null) return;

            int end = offset + length;
            if (offset < 0 || end > data.Length) return;

            LogicPoint prev = origin;

            while (offset < end)
            {
                // Safety check: Min 3 bytes needed (1x + 1y + 1p)
                if (end - offset < 3) break;

                ushort x;
                int readX;
                if (!TryReadCoordinate(data, offset, end, prev.X, out x, out readX)) break;
                offset += readX;

                ushort y;
                int readY;
                if (!TryReadCoordinate(data, offset, end, prev.Y, out y, out readY)) break;
                offset += readY;

                if (offset >= end) break;

                byte p = data[offset++];
                var point = new LogicPoint(x, y, p);
                targetList.Add(point);
                prev = point;
            }
        }

        private static bool TryReadCoordinate(byte[] buffer, int offset, int end, ushort previous, out ushort result, out int bytesRead)
        {
            result = 0;
            bytesRead = 0;

            if (offset >= end) return false;

            sbyte val = (sbyte)buffer[offset];
            if (val == ESCAPE_MARKER)
            {
                if (offset + 2 >= end) return false;
                int b1 = buffer[offset + 1];
                int b2 = buffer[offset + 2];
                result = (ushort)(b1 | (b2 << 8));
                bytesRead = 3;
                return true;
            }

            result = (ushort)((int)previous + val);
            bytesRead = 1;
            return true;
        }
    }
}
