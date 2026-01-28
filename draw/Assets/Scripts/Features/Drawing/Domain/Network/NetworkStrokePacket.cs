using System;

namespace Features.Drawing.Domain.Network
{
    public enum StrokePacketType : byte
    {
        BeginStroke = 0x01,
        UpdateStroke = 0x02,
        EndStroke = 0x03,
        AbortStroke = 0x04
    }

    [Serializable]
    public struct BeginStrokePacket
    {
        public uint StrokeId;
        public ushort BrushId;
        public uint Color; // RGBA packed
        public float Size;
        public uint Seed;
    }

    [Serializable]
    public struct UpdateStrokePacket
    {
        public uint StrokeId;
        public ushort Sequence;
        public byte Count;
        public byte[] Payload; // Compressed Delta Points
        public int PayloadLength;
        public byte[] RedundantPayload; // Previous batch points (for packet loss recovery)
        public int RedundantPayloadLength;
        public bool PayloadIsPooled;
        public bool RedundantPayloadIsPooled;
    }

    [Serializable]
    public struct EndStrokePacket
    {
        public uint StrokeId;
        public ushort TotalPoints;
        public uint Checksum;
    }

    [Serializable]
    public struct AbortStrokePacket
    {
        public uint StrokeId;
    }
}
