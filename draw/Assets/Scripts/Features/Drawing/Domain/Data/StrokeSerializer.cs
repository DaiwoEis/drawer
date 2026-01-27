using System.IO;
using System.Collections.Generic;
using Features.Drawing.Domain.Entity;
using Features.Drawing.Domain.ValueObject;

namespace Features.Drawing.Domain.Data
{
    public static class StrokeSerializer
    {
        private const uint MAGIC_HEADER = 0x5354524B; // 'STRK'
        private const byte VERSION = 1;

        public static byte[] Serialize(StrokeEntity stroke)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // Header
                writer.Write(MAGIC_HEADER);
                writer.Write(VERSION);

                // Metadata
                writer.Write(stroke.Id);
                writer.Write(stroke.AuthorId);
                writer.Write(stroke.BrushId);
                writer.Write(stroke.Seed);
                writer.Write(stroke.ColorRGBA);
                writer.Write(stroke.Size); // Serialize Size

                // Points
                var points = stroke.Points;
                int count = points.Count;
                WriteVarInt(writer, count);

                if (count > 0)
                {
                    // First point absolute
                    LogicPoint p0 = points[0];
                    writer.Write(p0.X);
                    writer.Write(p0.Y);
                    writer.Write(p0.Pressure);

                    // Subsequent points delta encoded
                    for (int i = 1; i < count; i++)
                    {
                        LogicPoint prev = points[i - 1];
                        LogicPoint curr = points[i];

                        int dx = curr.X - prev.X;
                        int dy = curr.Y - prev.Y;
                        int dp = curr.Pressure - prev.Pressure;

                        WriteZigZagVarInt(writer, dx);
                        WriteZigZagVarInt(writer, dy);
                        // Pressure delta usually fits in sbyte, but use ZigZag to be safe and consistent
                        WriteZigZagVarInt(writer, dp);
                    }
                }

                return ms.ToArray();
            }
        }

        public static StrokeEntity Deserialize(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                // Header
                uint magic = reader.ReadUInt32();
                if (magic != MAGIC_HEADER) throw new InvalidDataException("Invalid magic header");
                
                byte version = reader.ReadByte();
                if (version != VERSION) throw new InvalidDataException($"Unsupported version: {version}");

                // Metadata
                uint id = reader.ReadUInt32();
                ushort authorId = reader.ReadUInt16();
                ushort brushId = reader.ReadUInt16();
                uint seed = reader.ReadUInt32();
                uint color = reader.ReadUInt32();
                float size = reader.ReadSingle(); // Deserialize Size

                var stroke = new StrokeEntity(id, authorId, brushId, seed, color, size);

                // Points
                int count = ReadVarInt(reader);
                if (count > 0)
                {
                    var points = new List<LogicPoint>(count);

                    // First point
                    ushort x = reader.ReadUInt16();
                    ushort y = reader.ReadUInt16();
                    byte p = reader.ReadByte();
                    points.Add(new LogicPoint(x, y, p));

                    LogicPoint prev = points[0];

                    for (int i = 1; i < count; i++)
                    {
                        int dx = ReadZigZagVarInt(reader);
                        int dy = ReadZigZagVarInt(reader);
                        int dp = ReadZigZagVarInt(reader);

                        ushort newX = (ushort)(prev.X + dx);
                        ushort newY = (ushort)(prev.Y + dy);
                        byte newP = (byte)(prev.Pressure + dp); // Handles wrapping if any, but logic should be safe

                        LogicPoint curr = new LogicPoint(newX, newY, newP);
                        points.Add(curr);
                        prev = curr;
                    }

                    stroke.AddPoints(points);
                }
                
                stroke.EndStroke(); // Assuming serialized strokes are complete
                return stroke;
            }
        }

        // --- VarInt Helpers ---

        private static void WriteVarInt(BinaryWriter writer, int value)
        {
            uint v = (uint)value;
            while (v >= 0x80)
            {
                writer.Write((byte)(v | 0x80));
                v >>= 7;
            }
            writer.Write((byte)v);
        }

        private static int ReadVarInt(BinaryReader reader)
        {
            int value = 0;
            int shift = 0;
            byte b;
            do
            {
                if (shift >= 35) throw new InvalidDataException("VarInt too long");
                b = reader.ReadByte();
                value |= (b & 0x7F) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);
            return value;
        }

        private static void WriteZigZagVarInt(BinaryWriter writer, int value)
        {
            // ZigZag encode: (n << 1) ^ (n >> 31)
            int encoded = (value << 1) ^ (value >> 31);
            WriteVarInt(writer, encoded);
        }

        private static int ReadZigZagVarInt(BinaryReader reader)
        {
            int encoded = ReadVarInt(reader);
            // ZigZag decode: (n >> 1) ^ -(n & 1)
            return (encoded >> 1) ^ -(encoded & 1);
        }
    }
}
