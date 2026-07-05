using Concentus;
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

        public Playout()
        {
            Buffer = new BufferedWaveProvider(new WaveFormat(OpusAudio.SampleRate, 16, OpusAudio.Channels))
            {
                BufferDuration = TimeSpan.FromMilliseconds(600), DiscardOnBufferOverflow = true, ReadFully = true,
            };
            Sample = Buffer.ToSampleProvider();
        }
    }

    private const float LevelDecay = 0.6f;
    private readonly WaveFormat format = WaveFormat.CreateIeeeFloatWaveFormat(OpusAudio.SampleRate, OpusAudio.Channels);
    private readonly object gate = new();
    private readonly Dictionary<int, Playout> playouts = new();
    private readonly short[] decodeBuffer = new short[OpusAudio.FrameSamples * 6];
    private readonly byte[] pcmBytes = new byte[OpusAudio.FrameSamples * 6 * sizeof(short)];
    private float[] mixScratch = Array.Empty<float>();
    private WaveOutEvent? output;
    private float volume = 0.85f;
    public WaveFormat WaveFormat => format;

    public float Volume
    {
        get => volume;
        set
        {
            volume = Math.Clamp(value, 0f, 1f);
            var device = output;
            if (device is not null)
            {
                device.Volume = volume;
            }
        }
    }

    public void Start(int deviceNumber, float startVolume)
    {
        Stop();
        volume = Math.Clamp(startVolume, 0f, 1f);
        try
        {
            var device = new WaveOutEvent { DeviceNumber = deviceNumber, DesiredLatency = 140, Volume = volume, };
            device.Init(this.ToWaveProvider16());
            device.Play();
            output = device;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Voice output failed to start: {exception.Message}");
        }
    }

    public void Stop()
    {
        var device = output;
        output = null;
        if (device is not null)
        {
            try
            {
                device.Stop();
            }
            catch (Exception exception)
            {
                AepLog.Warning($"Voice output failed to stop: {exception.Message}");
            }

            device.Dispose();
        }

        lock (gate)
        {
            foreach (var playout in playouts.Values)
            {
                (playout.Decoder as IDisposable)?.Dispose();
            }

            playouts.Clear();
        }
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

                double sum = 0;
                for (var index = 0; index < read; index++)
                {
                    var sample = mixScratch[index];
                    buffer[offset + index] += sample;
                    sum += sample * sample;
                }

                var rms = (float)Math.Sqrt(sum / read);
                playout.Level = MathF.Max(rms, playout.Level * LevelDecay);
            }
        }

        for (var index = 0; index < count; index++)
        {
            buffer[offset + index] = Math.Clamp(buffer[offset + index], -1f, 1f);
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
