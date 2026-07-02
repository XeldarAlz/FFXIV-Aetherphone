using System.Buffers.Binary;

namespace Aetherphone.Core.Telephony;

internal static class MediaFrame
{
    public const byte Version = 1;
    public const int HeaderSize = 1 + 16 + 1 + 2;

    public static byte[] Build(Guid callId, byte slot, ushort sequence, ReadOnlySpan<byte> opus)
    {
        var frame = new byte[HeaderSize + opus.Length];
        frame[0] = Version;
        callId.TryWriteBytes(frame.AsSpan(1, 16));
        frame[17] = slot;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(18, 2), sequence);
        opus.CopyTo(frame.AsSpan(HeaderSize));
        return frame;
    }

    public static bool TryParse(ReadOnlySpan<byte> frame, out Guid callId, out byte slot, out ushort sequence, out int payloadOffset, out int payloadLength)
    {
        callId = default;
        slot = 0;
        sequence = 0;
        payloadOffset = 0;
        payloadLength = 0;

        if (frame.Length < HeaderSize || frame[0] != Version)
        {
            return false;
        }

        callId = new Guid(frame.Slice(1, 16));
        slot = frame[17];
        sequence = BinaryPrimitives.ReadUInt16LittleEndian(frame.Slice(18, 2));
        payloadOffset = HeaderSize;
        payloadLength = frame.Length - HeaderSize;
        return true;
    }
}
