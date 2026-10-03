namespace Aetherphone.Core.Radio;

internal enum StreamCodec : byte
{
    Unknown,
    Mp3,
    Aac,
    Opus,
}

internal static class StreamSniffer
{
    public const int HeadLength = 4096;
    private const int Id3HeaderLength = 10;

    // The bytes win over every label: a Shoutcast server announcing audio/mpeg while sending AAC is
    // common, and a directory codec column is older than either. Two frames back to back are needed
    // because a single sync word turns up by chance in any compressed payload.
    public static StreamCodec Detect(string? contentType, string? declaredCodec, ReadOnlySpan<byte> head)
    {
        var sniffed = FromBytes(head);
        if (sniffed != StreamCodec.Unknown)
        {
            return sniffed;
        }

        var announced = FromContentType(contentType);
        if (announced != StreamCodec.Unknown)
        {
            return announced;
        }

        var declared = FromDeclared(declaredCodec);
        return declared != StreamCodec.Unknown ? declared : StreamCodec.Mp3;
    }

    public static StreamCodec FromContentType(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            return StreamCodec.Unknown;
        }

        if (contentType.Contains("mpeg", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("mp3", StringComparison.OrdinalIgnoreCase))
        {
            return StreamCodec.Mp3;
        }

        if (contentType.Contains("aac", StringComparison.OrdinalIgnoreCase))
        {
            return StreamCodec.Aac;
        }

        if (contentType.Contains("ogg", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("opus", StringComparison.OrdinalIgnoreCase))
        {
            return StreamCodec.Opus;
        }

        return StreamCodec.Unknown;
    }

    public static StreamCodec FromDeclared(string? declaredCodec)
    {
        if (string.IsNullOrEmpty(declaredCodec))
        {
            return StreamCodec.Unknown;
        }

        if (declaredCodec.Contains("aac", StringComparison.OrdinalIgnoreCase))
        {
            return StreamCodec.Aac;
        }

        if (declaredCodec.Contains("opus", StringComparison.OrdinalIgnoreCase)
            || declaredCodec.Contains("ogg", StringComparison.OrdinalIgnoreCase))
        {
            return StreamCodec.Opus;
        }

        if (declaredCodec.Contains("mp3", StringComparison.OrdinalIgnoreCase))
        {
            return StreamCodec.Mp3;
        }

        return StreamCodec.Unknown;
    }

    public static StreamCodec FromBytes(ReadOnlySpan<byte> head)
    {
        if (head.Length >= 4 && head[0] == 'O' && head[1] == 'g' && head[2] == 'g' && head[3] == 'S')
        {
            return StreamCodec.Opus;
        }

        var start = Id3TagLength(head);
        if (start >= head.Length)
        {
            return StreamCodec.Unknown;
        }

        var body = head[start..];
        for (var offset = 0; offset + AdtsReader.HeaderLength <= body.Length; offset++)
        {
            if (body[offset] != 0xFF)
            {
                continue;
            }

            var candidate = body[offset..];
            if (AdtsReader.TryParseHeader(candidate, out var adts) && FollowedByAdts(candidate, adts.Length))
            {
                return StreamCodec.Aac;
            }

            if (MpegAudioHeader.TryParse(candidate, out var mpegLength) && FollowedByMpeg(candidate, mpegLength))
            {
                return StreamCodec.Mp3;
            }
        }

        return StreamCodec.Unknown;
    }

    public static int Id3TagLength(ReadOnlySpan<byte> data)
    {
        if (data.Length < Id3HeaderLength || data[0] != 'I' || data[1] != 'D' || data[2] != '3')
        {
            return 0;
        }

        var size = ((data[6] & 0x7F) << 21) | ((data[7] & 0x7F) << 14) | ((data[8] & 0x7F) << 7) | (data[9] & 0x7F);
        var footer = (data[5] & 0x10) != 0 ? Id3HeaderLength : 0;
        return Id3HeaderLength + size + footer;
    }

    private static bool FollowedByAdts(ReadOnlySpan<byte> candidate, int length)
    {
        return length + AdtsReader.HeaderLength <= candidate.Length
            && AdtsReader.TryParseHeader(candidate[length..], out _);
    }

    private static bool FollowedByMpeg(ReadOnlySpan<byte> candidate, int length)
    {
        return length + MpegAudioHeader.HeaderLength <= candidate.Length
            && MpegAudioHeader.TryParse(candidate[length..], out _);
    }
}

internal static class MpegAudioHeader
{
    public const int HeaderLength = 4;

    private static readonly int[] Version1Layer1 = { 0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448 };
    private static readonly int[] Version1Layer2 = { 0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384 };
    private static readonly int[] Version1Layer3 = { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320 };
    private static readonly int[] Version2Layer1 = { 0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256 };
    private static readonly int[] Version2Layer23 = { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160 };
    private static readonly int[] SampleRates = { 44100, 48000, 32000 };

    public static bool TryParse(ReadOnlySpan<byte> header, out int frameLength)
    {
        frameLength = 0;
        if (header.Length < HeaderLength || header[0] != 0xFF || (header[1] & 0xE0) != 0xE0)
        {
            return false;
        }

        var version = (header[1] >> 3) & 0x03;
        var layer = (header[1] >> 1) & 0x03;
        var bitrateIndex = header[2] >> 4;
        var rateIndex = (header[2] >> 2) & 0x03;
        if (version == 1 || layer == 0 || bitrateIndex == 0 || bitrateIndex == 15 || rateIndex == 3)
        {
            return false;
        }

        var isVersion1 = version == 3;
        var sampleRate = SampleRates[rateIndex] >> (isVersion1 ? 0 : version == 2 ? 1 : 2);
        var bitrate = BitrateTable(isVersion1, layer)[bitrateIndex] * 1000;
        var padding = (header[2] >> 1) & 0x01;
        frameLength = layer switch
        {
            3 => (12 * bitrate / sampleRate + padding) * 4,
            1 when !isVersion1 => 72 * bitrate / sampleRate + padding,
            _ => 144 * bitrate / sampleRate + padding,
        };

        return frameLength > HeaderLength;
    }

    private static int[] BitrateTable(bool isVersion1, int layer)
    {
        if (isVersion1)
        {
            return layer switch
            {
                3 => Version1Layer1,
                2 => Version1Layer2,
                _ => Version1Layer3,
            };
        }

        return layer == 3 ? Version2Layer1 : Version2Layer23;
    }
}
