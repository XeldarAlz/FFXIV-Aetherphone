using Concentus.Structs;
using NAudio.Wave;

namespace Aetherphone.Core.Songs;

/// <summary>
/// Decodes a WebM/Opus audio stream (from disk or a live network stream) into PCM samples,
/// without depending on Windows Media Foundation. Replaces MediaFoundationReader for songs
/// so playback no longer relies on Wine's partial/stub MF implementation, which is the source
/// of the "constantly buffering" behaviour reported on Linux.
///
/// Seeking is implemented as decode-and-discard up to the target time: correct, but O(n) in
/// the seek distance. Acceptable for a song player where seeks are infrequent and songs are a
/// few minutes long; revisit if that assumption stops holding.
/// </summary>
internal sealed class OpusWebmSampleProvider : ISampleProvider, ISongAudioReader
{
    private const int OpusFrameSizeSamples = 960; // 20ms at 48kHz, matches YouTube's Opus streams.

    private readonly Stream source;
    private readonly WebmOpusDemuxer demuxer;
    private readonly OpusDecoder decoder;
    private readonly int channels;
    private float[] pending = Array.Empty<float>();
    private int pendingOffset;
    private int pendingCount;
    private double positionSeconds;
    private bool endOfStream;

    public WaveFormat WaveFormat { get; }

    public TimeSpan TotalTime { get; }

    public TimeSpan CurrentTime
    {
        get => TimeSpan.FromSeconds(positionSeconds);
        set => SeekTo(value.TotalSeconds);
    }

    public OpusWebmSampleProvider(Stream source, int channels = 2, int sampleRate = 48_000)
    {
        this.source = source;
        this.channels = channels;
        demuxer = new WebmOpusDemuxer(source);
        demuxer.ReadHeader();
#pragma warning disable CS0618 // Obsolete in favor of OpusCodecFactory, which P/Invokes a native
                              // libopus when present. That's the exact native/Wine-fragile
                              // dependency this class exists to avoid, so the plain managed
                              // constructor is the deliberate choice here, not an oversight.
        decoder = new OpusDecoder(sampleRate, channels);
#pragma warning restore CS0618
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        TotalTime = TimeSpan.FromSeconds(demuxer.DurationSeconds ?? 0);
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var written = 0;
        while (written < count)
        {
            if (pendingOffset >= pendingCount && !TryDecodeNextFrame())
            {
                break;
            }

            var available = pendingCount - pendingOffset;
            var toCopy = Math.Min(available, count - written);
            Array.Copy(pending, pendingOffset, buffer, offset + written, toCopy);
            pendingOffset += toCopy;
            written += toCopy;
        }

        return written;
    }

    private bool TryDecodeNextFrame()
    {
        if (endOfStream)
        {
            return false;
        }

        var packet = demuxer.ReadNextPacket();
        if (packet is null)
        {
            endOfStream = true;
            return false;
        }

        var samplesNeeded = OpusFrameSizeSamples * channels;
        if (pending.Length < samplesNeeded)
        {
            pending = new float[samplesNeeded];
        }

        var decodedPerChannel = decoder.Decode(packet.AsSpan(), pending.AsSpan(0, samplesNeeded), OpusFrameSizeSamples);
        pendingCount = decodedPerChannel * channels;
        pendingOffset = 0;
        positionSeconds += (double)decodedPerChannel / decoder.SampleRate;
        return pendingCount > 0;
    }

    private void SeekTo(double targetSeconds)
    {
        if (targetSeconds < positionSeconds)
        {
            // Can't seek backwards without re-opening the stream from the start; the caller
            // (SongPlayer) only seeks forward in practice (scrubbing ahead, resuming after a
            // retry), so this is a documented limitation rather than a bug fix target here.
            throw new NotSupportedException("Backward seeking is not supported by OpusWebmSampleProvider.");
        }

        while (positionSeconds < targetSeconds)
        {
            if (!TryDecodeNextFrame())
            {
                break;
            }

            pendingOffset = pendingCount; // Discard this frame's audio; only position matters here.
        }
    }

    public ISampleProvider ToSampleProvider() => this;

    public void Dispose()
    {
        decoder.Dispose();
        source.Dispose();
    }
}
