using NAudio.Dsp;
using NAudio.Wave;

namespace Aetherphone.Core.Songs;

internal sealed class TrackVoice : ISampleProvider
{
    public const int OutputSampleRate = 48_000;
    public const int OutputChannels = 2;
    public const float MinimumRate = 0.9f;
    public const float MaximumRate = 1.1f;

    private static readonly WaveFormat MixFormat =
        WaveFormat.CreateIeeeFloatWaveFormat(OutputSampleRate, OutputChannels);

    private readonly ISongAudioReader reader;
    private readonly ISampleProvider source;
    private readonly WdlResampler resampler = new();
    private readonly object gate = new();
    private readonly object readerGate = new();
    private readonly int sourceChannels;
    private readonly int sourceSampleRate;
    private float[] resampled = Array.Empty<float>();
    private long sourceFramesConsumed;
    private double baseSeconds;
    private double pendingSeekSeconds = -1;
    private float gain;
    private float targetGain = 1f;
    private float gainStep;
    private float level = 1f;
    private float levelTarget = 1f;
    private float levelStep;
    private bool audible;
    private float rate = 1f;
    private bool rateDirty = true;
    private volatile bool finished;
    private volatile bool sourceEnded;
    private volatile bool faulted;

    public TrackVoice(ISongAudioReader reader, double startSeconds, float fadeInSeconds)
    {
        this.reader = reader;
        source = reader.ToSampleProvider();
        sourceChannels = Math.Max(1, source.WaveFormat.Channels);
        sourceSampleRate = Math.Max(1, source.WaveFormat.SampleRate);
        resampler.SetMode(true, 2, false);
        resampler.SetFilterParms();
        resampler.SetFeedMode(false);
        if (startSeconds > 0)
        {
            pendingSeekSeconds = startSeconds;
        }

        if (fadeInSeconds > 0f)
        {
            gain = 0f;
            gainStep = 1f / (fadeInSeconds * OutputSampleRate);
        }
        else
        {
            gain = 1f;
        }
    }

    public WaveFormat WaveFormat => MixFormat;
    public bool Finished => finished;
    public bool SourceEnded => sourceEnded;
    public bool Faulted => faulted;
    public bool FadingOut => targetGain <= 0f;
    public double DurationSeconds => reader.TotalTime.TotalSeconds;

    public float Level
    {
        get
        {
            lock (gate)
            {
                return levelTarget;
            }
        }
    }

    public double PositionSeconds
    {
        get
        {
            lock (gate)
            {
                return pendingSeekSeconds >= 0
                    ? pendingSeekSeconds
                    : baseSeconds + (double)sourceFramesConsumed / sourceSampleRate;
            }
        }
    }

    public float Rate
    {
        get => rate;
        set
        {
            var clamped = Math.Clamp(value, MinimumRate, MaximumRate);
            lock (gate)
            {
                if (Math.Abs(clamped - rate) < 0.0005f)
                {
                    return;
                }

                rate = clamped;
                rateDirty = true;
            }
        }
    }

    public bool SeekPending
    {
        get
        {
            lock (gate)
            {
                return pendingSeekSeconds >= 0;
            }
        }
    }

    public void Seek(double seconds)
    {
        lock (gate)
        {
            pendingSeekSeconds = Math.Max(0, seconds);
            Monitor.PulseAll(gate);
        }
    }

    public void WaitForWork(int timeoutMilliseconds)
    {
        lock (gate)
        {
            if (pendingSeekSeconds >= 0 && !faulted)
            {
                return;
            }

            Monitor.Wait(gate, timeoutMilliseconds);
        }
    }

    public void ServicePendingSeek()
    {
        double requested;
        lock (gate)
        {
            if (pendingSeekSeconds < 0 || faulted || finished)
            {
                return;
            }

            requested = pendingSeekSeconds;
        }

        lock (readerGate)
        {
            double target;
            try
            {
                var duration = reader.TotalTime.TotalSeconds;
                target = duration > 0 ? Math.Min(requested, duration) : requested;
                reader.CurrentTime = TimeSpan.FromSeconds(target);
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Song voice seek failed");
                lock (gate)
                {
                    faulted = true;
                    sourceEnded = true;
                }

                return;
            }

            lock (gate)
            {
                baseSeconds = target;
                sourceFramesConsumed = 0;
                sourceEnded = false;
                resampler.Reset();
                if (pendingSeekSeconds.Equals(requested))
                {
                    pendingSeekSeconds = -1;
                }
            }
        }
    }

    public void SetLevel(float linear, float rampSeconds)
    {
        var target = Math.Max(0f, linear);
        lock (gate)
        {
            levelTarget = target;
            if (!audible || rampSeconds <= 0f)
            {
                level = target;
                levelStep = 0f;
                return;
            }

            levelStep = MathF.Abs(target - level) / (rampSeconds * OutputSampleRate);
        }
    }

    public void FadeOut(float seconds)
    {
        lock (gate)
        {
            targetGain = 0f;
            gainStep = seconds <= 0f ? 1f : 1f / (seconds * OutputSampleRate);
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        if (finished)
        {
            return 0;
        }

        var readerHeld = Monitor.TryEnter(readerGate);
        try
        {
            lock (gate)
            {
                if (!readerHeld || (pendingSeekSeconds >= 0 && !faulted))
                {
                    return WriteSilence(buffer, offset, count);
                }

                if (rateDirty)
                {
                    resampler.SetRates(sourceSampleRate * (double)rate, OutputSampleRate);
                    rateDirty = false;
                }

                var outputFrames = count / OutputChannels;
                var producedFrames = sourceEnded ? 0 : SafeResample(outputFrames);
                audible |= producedFrames > 0;
                WriteFrames(buffer, offset, producedFrames, outputFrames);
                if ((producedFrames == 0 && sourceEnded) || (targetGain <= 0f && gain <= 0f))
                {
                    finished = true;
                }

                return count;
            }
        }
        finally
        {
            if (readerHeld)
            {
                Monitor.Exit(readerGate);
            }
        }
    }

    public void DisposeReader()
    {
        lock (readerGate)
        {
            lock (gate)
            {
                finished = true;
                Monitor.PulseAll(gate);
                reader.Dispose();
            }
        }
    }

    private int WriteSilence(float[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);
        if (targetGain <= 0f)
        {
            gain = 0f;
            finished = true;
        }

        return count;
    }

    private int SafeResample(int outputFrames)
    {
        try
        {
            return Resample(outputFrames);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Song voice read failed");
            faulted = true;
            sourceEnded = true;
            return 0;
        }
    }

    private int Resample(int outputFrames)
    {
        var needed = outputFrames * sourceChannels;
        if (resampled.Length < needed)
        {
            resampled = new float[needed];
        }

        var framesWanted = resampler.ResamplePrepare(outputFrames, sourceChannels, out var inputBuffer,
            out var inputOffset);
        var samplesRead = source.Read(inputBuffer, inputOffset, framesWanted * sourceChannels);
        var framesRead = samplesRead / sourceChannels;
        if (framesRead == 0)
        {
            sourceEnded = true;
        }

        sourceFramesConsumed += framesRead;
        return resampler.ResampleOut(resampled, 0, framesRead, outputFrames, sourceChannels);
    }

    private void WriteFrames(float[] buffer, int offset, int producedFrames, int outputFrames)
    {
        for (var frame = 0; frame < outputFrames; frame++)
        {
            StepGain();
            StepLevel();
            var outputIndex = offset + frame * OutputChannels;
            if (frame >= producedFrames)
            {
                buffer[outputIndex] = 0f;
                buffer[outputIndex + 1] = 0f;
                continue;
            }

            var sourceIndex = frame * sourceChannels;
            var left = resampled[sourceIndex];
            var right = sourceChannels > 1 ? resampled[sourceIndex + 1] : left;
            var amplitude = gain * level;
            buffer[outputIndex] = left * amplitude;
            buffer[outputIndex + 1] = right * amplitude;
        }
    }

    private void StepLevel()
    {
        if (level < levelTarget)
        {
            level = Math.Min(levelTarget, level + levelStep);
        }
        else if (level > levelTarget)
        {
            level = Math.Max(levelTarget, level - levelStep);
        }
    }

    private void StepGain()
    {
        if (gain < targetGain)
        {
            gain = Math.Min(targetGain, gain + gainStep);
        }
        else if (gain > targetGain)
        {
            gain = Math.Max(targetGain, gain - gainStep);
        }
    }
}
