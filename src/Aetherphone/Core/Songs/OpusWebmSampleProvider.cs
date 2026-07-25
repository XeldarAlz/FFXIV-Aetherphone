using Aetherphone.Core.Telephony.Audio;
using Concentus;
using NAudio.Wave;

namespace Aetherphone.Core.Songs;

internal sealed class OpusWebmSampleProvider : ISampleProvider, ISongAudioReader
{
    private const int MaxOpusFrameSamples = 5760; // 120ms at 48kHz, the largest possible Opus frame.

    private readonly Func<Stream> openSource;
    private readonly int channels;
    private readonly int sampleRate;
    private readonly float[] pending;
    private Stream source = null!;
    private WebmOpusDemuxer demuxer = null!;
    private IOpusDecoder decoder = null!;
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

    public OpusWebmSampleProvider(Func<Stream> openSource, int channels = 2, int sampleRate = 48_000)
    {
        this.openSource = openSource;
        this.channels = channels;
        this.sampleRate = sampleRate;
        pending = new float[MaxOpusFrameSamples * channels];
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        Reopen();
        TotalTime = TimeSpan.FromSeconds(demuxer.DurationSeconds ?? 0);
    }

    private void Reopen()
    {
        source?.Dispose();
        decoder?.Dispose();
        source = openSource();
        demuxer = new WebmOpusDemuxer(source);
        demuxer.ReadHeader();
        decoder = OpusCodecFactory.CreateDecoder(OpusAudio.SampleRate, channels);
        pendingOffset = 0;
        pendingCount = 0;
        positionSeconds = 0;
        endOfStream = false;
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

        var decodedPerChannel = decoder.Decode(packet.Value.Span, pending.AsSpan(), MaxOpusFrameSamples, false);
        pendingCount = decodedPerChannel * channels;
        pendingOffset = 0;
        positionSeconds += (double)decodedPerChannel / decoder.SampleRate;
        return pendingCount > 0;
    }

    private void SeekTo(double targetSeconds)
    {
        Reopen();
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
