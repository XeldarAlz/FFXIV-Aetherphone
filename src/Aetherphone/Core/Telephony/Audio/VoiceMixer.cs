using Aetherphone.Core.Audio;
using Concentus;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Aetherphone.Core.Telephony.Audio;

internal sealed class VoiceMixer : ISampleProvider, IDisposable
{
    private sealed class Playout
    {
        public readonly IOpusDecoder Decoder = OpusAudio.CreateDecoder();
        public readonly BufferedWaveProvider Buffer;
        public readonly ISampleProvider Sample;
        public float Level;
        public float Gain = 1f;

        public Playout()
        {
            Buffer = new BufferedWaveProvider(new WaveFormat(OpusAudio.SampleRate, 16, OpusAudio.Channels))
            {
                BufferDuration = TimeSpan.FromMilliseconds(600), DiscardOnBufferOverflow = true, ReadFully = true,
            };
            Sample = Buffer.ToSampleProvider();
        }
    }

    public const float MaximumGain = 2f;
    private const int OutputLatencyMilliseconds = 140;
    private const float LevelDecay = 0.6f;
    private readonly WaveFormat format = WaveFormat.CreateIeeeFloatWaveFormat(OpusAudio.SampleRate, OpusAudio.Channels);
    private readonly object gate = new();
    private readonly object outputGate = new();
    private readonly object switchGate = new();
    private readonly Dictionary<int, Playout> playouts = new();
    private readonly short[] decodeBuffer = new short[OpusAudio.FrameSamples * 6];
    private readonly byte[] pcmBytes = new byte[OpusAudio.FrameSamples * 6 * sizeof(short)];
    private float[] mixScratch = Array.Empty<float>();
    private IWavePlayer? output;
    private MMDevice? outputDevice;
    private bool closed;
    private volatile float volume = 1f;
    public WaveFormat WaveFormat => format;

    public float Volume
    {
        get => volume;
        set => volume = Math.Clamp(value, 0f, MaximumGain);
    }

    public void Start(string deviceName, float startVolume)
    {
        Volume = startVolume;
        lock (switchGate)
        {
            lock (outputGate)
            {
                closed = false;
            }

            Open(deviceName);
        }
    }

    public void SwitchOutput(string deviceName)
    {
        lock (switchGate)
        {
            IWavePlayer? previous;
            MMDevice? previousDevice;
            lock (outputGate)
            {
                if (closed)
                {
                    return;
                }

                previous = output;
                previousDevice = outputDevice;
                output = null;
                outputDevice = null;
            }

            Release(previous, previousDevice);
            Open(deviceName);
        }
    }

    public void Stop()
    {
        IWavePlayer? device;
        MMDevice? endpoint;
        lock (outputGate)
        {
            closed = true;
            device = output;
            endpoint = outputDevice;
            output = null;
            outputDevice = null;
        }

        Release(device, endpoint);
        lock (gate)
        {
            foreach (var playout in playouts.Values)
            {
                (playout.Decoder as IDisposable)?.Dispose();
            }

            playouts.Clear();
        }
    }

    private void Open(string deviceName)
    {
        var target = AudioDevices.ResolveOutput(deviceName);
        var endpoint = target.Endpoint;
        IWavePlayer device;
        try
        {
            device = target.WaveOutDevice >= 0
                ? AudioOutputFactory.CreateWaveOut(OutputLatencyMilliseconds, target.WaveOutDevice)
                : AudioOutputFactory.Create(OutputLatencyMilliseconds, endpoint);
            device.Init(this.ToWaveProvider16());
            device.Play();
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Voice output failed to start");
            endpoint?.Dispose();
            return;
        }

        lock (outputGate)
        {
            if (!closed)
            {
                output = device;
                outputDevice = endpoint;
                return;
            }
        }

        Release(device, endpoint);
    }

    private static void Release(IWavePlayer? device, MMDevice? endpoint)
    {
        if (device is not null)
        {
            try
            {
                device.Stop();
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Voice output failed to stop");
            }

            device.Dispose();
        }

        endpoint?.Dispose();
    }

    public void AddParticipant(int slot)
    {
        lock (gate)
        {
            if (!playouts.ContainsKey(slot))
            {
                playouts[slot] = new Playout();
            }
        }
    }

    public void RemoveParticipant(int slot)
    {
        lock (gate)
        {
            if (playouts.Remove(slot, out var playout))
            {
                (playout.Decoder as IDisposable)?.Dispose();
            }
        }
    }

    public void SetGain(int slot, float gain)
    {
        lock (gate)
        {
            if (!playouts.TryGetValue(slot, out var playout))
            {
                playout = new Playout();
                playouts[slot] = playout;
            }

            playout.Gain = Math.Clamp(gain, 0f, MaximumGain);
        }
    }

    public float LevelOf(int slot)
    {
        lock (gate)
        {
            return playouts.TryGetValue(slot, out var playout) ? playout.Level : 0f;
        }
    }

    public void Push(int slot, ReadOnlySpan<byte> opus)
    {
        Playout playout;
        lock (gate)
        {
            if (!playouts.TryGetValue(slot, out var existing))
            {
                existing = new Playout();
                playouts[slot] = existing;
            }

            playout = existing;
        }

        var samples = playout.Decoder.Decode(opus, decodeBuffer, decodeBuffer.Length, false);
        if (samples <= 0)
        {
            return;
        }

        Buffer.BlockCopy(decodeBuffer, 0, pcmBytes, 0, samples * sizeof(short));
        playout.Buffer.AddSamples(pcmBytes, 0, samples * sizeof(short));
    }

    public int Read(float[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);
        EnsureScratch(count);
        lock (gate)
        {
            if (playouts.Count == 0)
            {
                return count;
            }

            foreach (var playout in playouts.Values)
            {
                var read = playout.Sample.Read(mixScratch, 0, count);
                if (read <= 0)
                {
                    playout.Level *= LevelDecay;
                    continue;
                }

                var gain = playout.Gain;
                double sum = 0;
                for (var index = 0; index < read; index++)
                {
                    var sample = mixScratch[index];
                    buffer[offset + index] += sample * gain;
                    sum += sample * sample;
                }

                var rms = (float)Math.Sqrt(sum / read);
                playout.Level = MathF.Max(rms, playout.Level * LevelDecay);
            }
        }

        var master = volume;
        for (var index = 0; index < count; index++)
        {
            buffer[offset + index] = Math.Clamp(buffer[offset + index] * master, -1f, 1f);
        }

        return count;
    }

    private void EnsureScratch(int count)
    {
        if (mixScratch.Length < count)
        {
            mixScratch = new float[count];
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
