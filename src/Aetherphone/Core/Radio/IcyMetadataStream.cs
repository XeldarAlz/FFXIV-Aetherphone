using System.Text;

namespace Aetherphone.Core.Radio;

/// Shoutcast style metadata arrives inside the audio stream: every icy-metaint bytes the server
/// splices in a block naming the current track. This strips those blocks back out so the decoder
/// only ever sees audio, which is why it wraps the stream instead of living in a decoder.
internal sealed class IcyMetadataStream : Stream
{
    public const string RequestHeader = "Icy-MetaData";
    public const string IntervalHeader = "icy-metaint";

    private const string TitleKey = "StreamTitle='";
    private const int LengthUnit = 16;
    private const int MaxMetadataBytes = 255 * LengthUnit;

    private readonly Stream source;
    private readonly int interval;
    private readonly Action<string> onTitle;
    private readonly byte[] metadata = new byte[MaxMetadataBytes];
    private int untilMetadata;

    public IcyMetadataStream(Stream source, int interval, Action<string> onTitle)
    {
        this.source = source;
        this.interval = interval;
        this.onTitle = onTitle;
        untilMetadata = interval;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => 0;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (untilMetadata == 0)
        {
            if (!ReadMetadata())
            {
                return 0;
            }

            untilMetadata = interval;
        }

        var take = Math.Min(count, untilMetadata);
        var read = source.Read(buffer, offset, take);
        if (read > 0)
        {
            untilMetadata -= read;
        }

        return read;
    }

    private bool ReadMetadata()
    {
        var lengthByte = source.ReadByte();
        if (lengthByte < 0)
        {
            return false;
        }

        // Nearly every block is empty: the server only spends bytes when the track changes.
        if (lengthByte == 0)
        {
            return true;
        }

        var length = lengthByte * LengthUnit;
        var filled = 0;
        while (filled < length)
        {
            var read = source.Read(metadata, filled, length - filled);
            if (read <= 0)
            {
                return false;
            }

            filled += read;
        }

        if (TryParseTitle(metadata.AsSpan(0, length), out var title))
        {
            onTitle(title);
        }

        return true;
    }

    public static bool TryParseTitle(ReadOnlySpan<byte> block, out string title)
    {
        title = string.Empty;
        var text = Encoding.UTF8.GetString(block).TrimEnd('\0');
        var start = text.IndexOf(TitleKey, StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        start += TitleKey.Length;
        var end = text.IndexOf("';", start, StringComparison.Ordinal);
        if (end < 0)
        {
            end = text.LastIndexOf('\'');
            if (end <= start)
            {
                return false;
            }
        }

        title = text[start..end].Trim();
        return title.Length > 0;
    }

    public static int ParseInterval(string? header)
    {
        return int.TryParse(header, out var interval) && interval > 0 ? interval : 0;
    }

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
