using System.Runtime.InteropServices;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Aetherphone.Core.Emulation.Libretro;

internal sealed class LibretroAudioOutput : IDisposable
{
    private const int MaximumDirectSampleRate = 192000;
    private const int ResampledOutputRate = 48000;
    private const double ResampleEpsilon = 0.000000001;

    private readonly BufferedWaveProvider buffer;
    private readonly VolumeSampleProvider volume;
    private readonly WaveOutEvent output;
    private readonly bool resampleHighRateAudio;
    private readonly double inputFramesPerOutputFrame;
    private short[] samples = Array.Empty<short>();
    private byte[] bytes = Array.Empty<byte>();
    private int playbackSpeed = 1;
    private int sourcePhase;
    private double inputFramesUntilOutput;
    private double accumulatedLeft;
    private double accumulatedRight;
    private double accumulatedWeight;

    public LibretroAudioOutput(double sampleRate)
    {
        var sourceRate = NormalizeSampleRate(sampleRate);
        var outputRate = sourceRate > MaximumDirectSampleRate
            ? ResampledOutputRate
            : Math.Clamp((int)Math.Round(sourceRate), 8000, MaximumDirectSampleRate);

        resampleHighRateAudio = sourceRate > MaximumDirectSampleRate;
        inputFramesPerOutputFrame = resampleHighRateAudio ? sourceRate / outputRate : 1.0;
        inputFramesUntilOutput = inputFramesPerOutputFrame;

        buffer = new BufferedWaveProvider(new WaveFormat(outputRate, 16, 2))
        {
            BufferDuration = TimeSpan.FromMilliseconds(180),
            DiscardOnBufferOverflow = true,
        };
        volume = new VolumeSampleProvider(buffer.ToSampleProvider());
        output = new WaveOutEvent { DesiredLatency = 90, NumberOfBuffers = 3, };
        output.Init(volume);
        output.Play();
    }

    public float Volume
    {
        get => volume.Volume;
        set => volume.Volume = Math.Clamp(value, 0f, 1f);
    }

    public int PlaybackSpeed
    {
        get => playbackSpeed;
        set
        {
            var next = Math.Clamp(value, 1, 8);
            if (playbackSpeed == next)
            {
                return;
            }

            playbackSpeed = next;
            ResetProcessingState();
            buffer.ClearBuffer();
        }
    }

    public void Push(IntPtr data, int frameCount)
    {
        if (data == IntPtr.Zero || frameCount <= 0)
        {
            return;
        }

        var sampleCount = checked(frameCount * 2);
        if (samples.Length < sampleCount)
        {
            samples = new short[sampleCount];
        }

        EnsureByteCapacity(checked(frameCount * 2 * sizeof(short)));
        Marshal.Copy(data, samples, 0, sampleCount);

        var outputFrames = 0;
        for (var frame = 0; frame < frameCount; frame++)
        {
            var sample = frame * 2;
            outputFrames = ProcessInputFrame(samples[sample], samples[sample + 1], outputFrames);
        }

        if (outputFrames > 0)
        {
            buffer.AddSamples(bytes, 0, outputFrames * 2 * sizeof(short));
        }
    }

    public void Push(short left, short right)
    {
        EnsureByteCapacity(2 * sizeof(short));
        var outputFrames = ProcessInputFrame(left, right, 0);
        if (outputFrames > 0)
        {
            buffer.AddSamples(bytes, 0, outputFrames * 2 * sizeof(short));
        }
    }

    public void Clear()
    {
        ResetProcessingState();
        buffer.ClearBuffer();
    }

    public void Dispose()
    {
        try
        {
            output.Stop();
        }
        catch (Exception)
        {
        }

        output.Dispose();
    }

    private int ProcessInputFrame(short left, short right, int outputFrames)
    {
        if (!resampleHighRateAudio)
        {
            return EmitFrame(left, right, outputFrames);
        }

        var remainingInputFrame = 1.0;
        while (remainingInputFrame > ResampleEpsilon)
        {
            var weight = Math.Min(remainingInputFrame, inputFramesUntilOutput);
            accumulatedLeft += left * weight;
            accumulatedRight += right * weight;
            accumulatedWeight += weight;
            remainingInputFrame -= weight;
            inputFramesUntilOutput -= weight;

            if (inputFramesUntilOutput > ResampleEpsilon)
            {
                continue;
            }

            var averagedLeft = ClampToInt16(accumulatedLeft / Math.Max(accumulatedWeight, ResampleEpsilon));
            var averagedRight = ClampToInt16(accumulatedRight / Math.Max(accumulatedWeight, ResampleEpsilon));
            outputFrames = EmitFrame(averagedLeft, averagedRight, outputFrames);

            accumulatedLeft = 0;
            accumulatedRight = 0;
            accumulatedWeight = 0;
            inputFramesUntilOutput = inputFramesPerOutputFrame;
        }

        return outputFrames;
    }

    private int EmitFrame(short left, short right, int outputFrames)
    {
        if (playbackSpeed == 1 || sourcePhase == 0)
        {
            var outputOffset = outputFrames * 2 * sizeof(short);
            bytes[outputOffset] = (byte)left;
            bytes[outputOffset + 1] = (byte)(left >> 8);
            bytes[outputOffset + 2] = (byte)right;
            bytes[outputOffset + 3] = (byte)(right >> 8);
            outputFrames++;
        }

        if (playbackSpeed > 1)
        {
            sourcePhase = (sourcePhase + 1) % playbackSpeed;
        }

        return outputFrames;
    }

    private void ResetProcessingState()
    {
        sourcePhase = 0;
        accumulatedLeft = 0;
        accumulatedRight = 0;
        accumulatedWeight = 0;
        inputFramesUntilOutput = inputFramesPerOutputFrame;
    }

    private void EnsureByteCapacity(int required)
    {
        if (bytes.Length < required)
        {
            bytes = new byte[required];
        }
    }

    private static double NormalizeSampleRate(double sampleRate)
    {
        if (!double.IsFinite(sampleRate) || sampleRate < 8000)
        {
            return ResampledOutputRate;
        }

        return Math.Min(sampleRate, 8000000);
    }

    private static short ClampToInt16(double sample)
    {
        var rounded = (int)Math.Round(sample);
        return (short)Math.Clamp(rounded, short.MinValue, short.MaxValue);
    }
}
