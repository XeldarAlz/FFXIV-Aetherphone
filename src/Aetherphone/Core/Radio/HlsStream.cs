using System.Text;

namespace Aetherphone.Core.Radio;

internal sealed class HlsStream : Stream
{
    private const int LiveStartSegments = 3;
    private const int MaxConsecutiveFailures = 5;
    private const int MaxStalledReloads = 8;
    private const int InitialOutputCapacity = 256 * 1024;

    private readonly Func<Uri, CancellationToken, byte[]> fetch;
    private readonly CancellationToken token;
    private readonly Uri mediaUri;
    private readonly Queue<HlsSegment> pending = new();
    private readonly ByteQueue output = new(InitialOutputCapacity);
    private readonly TsDemuxer demuxer = new();
    private long lastQueued = -1;
    private bool endList;
    private double targetDuration;
    private long lastReloadTick;
    private int stalledReloads;
    private int failures;
    private bool needsReset;

    public HlsStream(Uri playlistUri, string playlistText, Func<Uri, CancellationToken, byte[]> fetch,
        CancellationToken token)
    {
        this.fetch = fetch;
        this.token = token;
        var uri = playlistUri;
        var text = playlistText;
        if (HlsPlaylist.IsMaster(text))
        {
            var master = HlsPlaylist.ParseMaster(text, playlistUri);
            uri = HlsPlaylist.SelectAudio(master, HlsPlaylist.DefaultBitrateCap)
                ?? throw new NotSupportedException("The HLS station lists no playable variant");
            text = Encoding.UTF8.GetString(fetch(uri, token));
        }

        mediaUri = uri;
        lastReloadTick = Environment.TickCount64;
        Apply(HlsPlaylist.ParseMedia(text, uri));
    }

    public StreamCodec Codec { get; private set; }

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
        while (output.Count == 0)
        {
            if (token.IsCancellationRequested)
            {
                return 0;
            }

            if (pending.Count > 0)
            {
                ProcessSegment(pending.Dequeue());
                continue;
            }

            if (endList || !Reload())
            {
                return 0;
            }
        }

        return output.Read(buffer, offset, count);
    }

    private int Apply(HlsMediaPlaylist playlist)
    {
        if (playlist.Encrypted)
        {
            throw new NotSupportedException("Encrypted HLS streams are not supported");
        }

        if (playlist.Fragmented)
        {
            throw new NotSupportedException("Fragmented MP4 HLS streams are not supported");
        }

        targetDuration = playlist.TargetDuration;
        endList = playlist.EndList;
        var segments = playlist.Segments;
        if (segments.Length == 0)
        {
            return 0;
        }

        var first = 0;
        if (lastQueued < 0)
        {
            first = endList ? 0 : Math.Max(0, segments.Length - LiveStartSegments);
        }
        else if (segments[^1].Sequence < lastQueued - segments.Length)
        {
            first = Math.Max(0, segments.Length - LiveStartSegments);
            lastQueued = segments[first].Sequence - 1;
            needsReset = true;
        }
        else if (segments[0].Sequence > lastQueued + 1)
        {
            needsReset = true;
        }

        var added = 0;
        for (var index = first; index < segments.Length; index++)
        {
            var segment = segments[index];
            if (lastQueued >= 0 && segment.Sequence <= lastQueued)
            {
                continue;
            }

            pending.Enqueue(segment);
            lastQueued = segment.Sequence;
            added++;
        }

        return added;
    }

    // Reloads are spaced half a target duration apart, which is what the HLS spec asks of a client
    // that found nothing new, and a playlist that stops growing for long enough means the station
    // has gone, not that it is slow.
    private bool Reload()
    {
        var spacing = (long)(targetDuration * 500);
        var elapsed = Environment.TickCount64 - lastReloadTick;
        if (elapsed < spacing && token.WaitHandle.WaitOne((int)(spacing - elapsed)))
        {
            return false;
        }

        lastReloadTick = Environment.TickCount64;
        try
        {
            var text = Encoding.UTF8.GetString(fetch(mediaUri, token));
            failures = 0;
            if (Apply(HlsPlaylist.ParseMedia(text, mediaUri)) > 0)
            {
                stalledReloads = 0;
                return true;
            }
        }
        catch (Exception exception) when (exception is not NotSupportedException && !token.IsCancellationRequested)
        {
            failures++;
            AepLog.Debug(exception, "[Radio] an HLS playlist reload failed");
            if (failures >= MaxConsecutiveFailures)
            {
                throw;
            }

            return true;
        }

        stalledReloads++;
        return stalledReloads < MaxStalledReloads || endList;
    }

    private void ProcessSegment(in HlsSegment segment)
    {
        byte[] data;
        try
        {
            data = fetch(segment.Uri, token);
            failures = 0;
        }
        catch (Exception exception) when (!token.IsCancellationRequested)
        {
            failures++;
            AepLog.Debug(exception, "[Radio] an HLS segment fetch failed");
            if (failures >= MaxConsecutiveFailures)
            {
                throw;
            }

            needsReset = true;
            return;
        }

        if (segment.Discontinuity || needsReset)
        {
            demuxer.Reset();
            needsReset = false;
        }

        if (TsDemuxer.LooksLikeTransportStream(data))
        {
            demuxer.Demux(data, output);
            if (demuxer.Codec != StreamCodec.Unknown)
            {
                Codec = demuxer.Codec;
            }
            else if (demuxer.HasUnsupportedAudio)
            {
                throw new NotSupportedException("The HLS station uses an audio codec we cannot decode");
            }

            return;
        }

        if (IsFragmentedMp4(data))
        {
            throw new NotSupportedException("Fragmented MP4 HLS streams are not supported");
        }

        output.Append(data.AsSpan(Math.Min(SkipId3Tags(data), data.Length)));
    }

    private static int SkipId3Tags(ReadOnlySpan<byte> data)
    {
        var offset = 0;
        while (offset < data.Length)
        {
            var tag = StreamSniffer.Id3TagLength(data[offset..]);
            if (tag == 0)
            {
                break;
            }

            offset += tag;
        }

        return offset;
    }

    private static bool IsFragmentedMp4(ReadOnlySpan<byte> data)
    {
        if (data.Length < 8)
        {
            return false;
        }

        var box = data.Slice(4, 4);
        return box.SequenceEqual("ftyp"u8) || box.SequenceEqual("styp"u8) || box.SequenceEqual("moof"u8)
            || box.SequenceEqual("sidx"u8);
    }

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
