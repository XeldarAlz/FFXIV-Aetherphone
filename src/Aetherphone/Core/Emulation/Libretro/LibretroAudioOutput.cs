using System.Runtime.InteropServices;
using NAudio.Wave;

namespace Aetherphone.Core.Emulation.Libretro;

internal sealed class LibretroAudioOutput : IDisposable
{
    private readonly BufferedWaveProvider buffer;
    private readonly WaveOutEvent output;
    private short[] samples = Array.Empty<short>();
    private byte[] bytes = Array.Empty<byte>();
    private int playbackSpeed = 1;
    private int sourcePhase;

    public LibretroAudioOutput(double sampleRate)
    {
        var rate = Math.Clamp((int)Math.Round(sampleRate), 8000, 192000);
        buffer = new BufferedWaveProvider(new WaveFormat(rate, 16, 2))
        {
            BufferDuration = TimeSpan.FromMilliseconds(180),
            DiscardOnBufferOverflow = true,
        };
        output = new WaveOutEvent { DesiredLatency = 90, NumberOfBuffers = 3, };
        output.Init(buffer);
        output.Play();
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
            sourcePhase = 0;
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
        var byteCount = checked(sampleCount * sizeof(short));
        if (samples.Length < sampleCount)
        {
            samples = new short[sampleCount];
        }

        if (bytes.Length < byteCount)
        {
            bytes = new byte[byteCount];
        }

        Marshal.Copy(data, samples, 0, sampleCount);
        if (playbackSpeed == 1)
        {
            Buffer.BlockCopy(samples, 0, bytes, 0, byteCount);
            buffer.AddSamples(bytes, 0, byteCount);
            return;
        }

        var outputFrames = AcceleratedAudioSampler.Copy(samples, frameCount, bytes, playbackSpeed,
            ref sourcePhase);
        if (outputFrames > 0)
        {
            buffer.AddSamples(bytes, 0, outputFrames * 2 * sizeof(short));
        }
    }

    public void Push(short left, short right)
    {
        if (bytes.Length < 4)
        {
            bytes = new byte[4];
        }

        if (playbackSpeed == 1 || sourcePhase == 0)
        {
            bytes[0] = (byte)left;
            bytes[1] = (byte)(left >> 8);
            bytes[2] = (byte)right;
            bytes[3] = (byte)(right >> 8);
            buffer.AddSamples(bytes, 0, 4);
        }

        if (playbackSpeed > 1)
        {
            sourcePhase = (sourcePhase + 1) % playbackSpeed;
        }
    }

    public void Clear() => buffer.ClearBuffer();

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
}
