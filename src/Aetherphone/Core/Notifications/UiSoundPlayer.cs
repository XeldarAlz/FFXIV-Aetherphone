using Aetherphone.Core.Audio;
using Aetherphone.Core.Playback;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Aetherphone.Core.Notifications;

internal sealed class UiSoundPlayer : IDisposable
{
    private const int SampleRate = 48000;
    private const int ChannelCount = 2;
    private const int MaxVoices = 8;
    private const long IdleCloseMilliseconds = 20_000;
    private const int OutputLatencyMilliseconds = 60;
    private const float MinimumRate = 0.5f;
    private const float MaximumRate = 2f;

    private readonly object gate = new();
    private readonly Dictionary<string, float[]> clips = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string FileName, HauntDepth Depth), float[]> hauntedClips = new();
    private int hauntGeneration;
    private readonly DirectoryInfo root;
    private MixingSampleProvider? mixer;
    private VolumeSampleProvider? bus;
    private IWavePlayer? output;
    private float busVolume = 1f;
    private int activeVoices;
    private long lastPlayTicks;
    private bool disposed;

    public UiSoundPlayer(DirectoryInfo root)
    {
        this.root = root;
    }

    public void Play(string fileName, float gain, float rate, HauntDepth haunt)
    {
        lock (gate)
        {
            if (disposed || Volatile.Read(ref activeVoices) >= MaxVoices || !TryLoadClip(fileName, haunt, out var clip))
            {
                return;
            }

            if (!EnsureOutput())
            {
                return;
            }

            Interlocked.Increment(ref activeVoices);
            try
            {
                mixer!.AddMixerInput((ISampleProvider)new ClipSampleProvider(clip, Math.Clamp(gain, 0f, 1f),
                    Math.Clamp(rate, MinimumRate, MaximumRate)));
            }
            catch
            {
                Interlocked.Decrement(ref activeVoices);
                throw;
            }

            lastPlayTicks = Environment.TickCount64;
        }
    }

    public void ClearHaunted()
    {
        lock (gate)
        {
            hauntedClips.Clear();
            hauntGeneration++;
        }
    }

    public void SetBusVolume(float volume)
    {
        lock (gate)
        {
            busVolume = Math.Clamp(volume, 0f, 1f);
            if (bus is not null)
            {
                bus.Volume = busVolume;
            }
        }
    }

    public void CloseIfIdle()
    {
        IWavePlayer? stale;
        lock (gate)
        {
            if (output is null || Volatile.Read(ref activeVoices) > 0 ||
                Environment.TickCount64 - lastPlayTicks < IdleCloseMilliseconds)
            {
                return;
            }

            stale = output;
            output = null;
            mixer = null;
            bus = null;
        }

        DisposeOutput(stale);
    }

    private void OnMixerInputEnded(object? sender, SampleProviderEventArgs eventArgs)
    {
        Interlocked.Decrement(ref activeVoices);
    }

    private bool TryLoadClip(string fileName, HauntDepth haunt, out float[] clip)
    {
        if (haunt == HauntDepth.None)
        {
            return TryLoadClip(fileName, out clip);
        }

        var key = (fileName, haunt);
        if (hauntedClips.TryGetValue(key, out clip!))
        {
            return clip.Length > 0;
        }

        if (!TryLoadClip(fileName, out clip))
        {
            return false;
        }

        var plain = clip;
        var generation = hauntGeneration;
        hauntedClips[key] = plain;
        _ = Task.Run(() => BuildHaunted(key, plain, generation));
        return true;
    }

    private void BuildHaunted((string FileName, HauntDepth Depth) key, float[] plain, int generation)
    {
        float[] haunted;
        try
        {
            haunted = HauntFilter.Apply(plain, key.Depth);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[UiSound] haunting {key.FileName} failed");
            haunted = plain;
        }

        lock (gate)
        {
            if (disposed || generation != hauntGeneration)
            {
                return;
            }

            hauntedClips[key] = haunted;
        }
    }

    private bool TryLoadClip(string fileName, out float[] clip)
    {
        if (clips.TryGetValue(fileName, out clip!))
        {
            return clip.Length > 0;
        }

        clip = Decode(fileName);
        clips[fileName] = clip;
        return clip.Length > 0;
    }

    private float[] Decode(string fileName)
    {
        if (Path.IsPathRooted(fileName) || fileName.Contains(".."))
        {
            AepLog.Warning($"[UiSound] rejected clip path {fileName}");
            return Array.Empty<float>();
        }

        var path = Path.Combine(root.FullName, fileName);
        if (!File.Exists(path))
        {
            AepLog.Warning($"[UiSound] missing clip {fileName}");
            return Array.Empty<float>();
        }

        try
        {
            using var reader = SoundEffectPlayer.OpenReader(path);
            var samples = reader.ToSampleProvider();
            if (samples.WaveFormat.SampleRate != SampleRate)
            {
                samples = new WdlResamplingSampleProvider(samples, SampleRate);
            }

            if (samples.WaveFormat.Channels == 1)
            {
                samples = new MonoToStereoSampleProvider(samples);
            }

            var estimated = (int)(reader.TotalTime.TotalSeconds * SampleRate * ChannelCount) + SampleRate;
            var buffer = new float[estimated];
            var total = 0;
            while (total < buffer.Length)
            {
                var read = samples.Read(buffer, total, Math.Min(4096, buffer.Length - total));
                if (read == 0)
                {
                    break;
                }

                total += read;
            }

            if (total == buffer.Length)
            {
                AepLog.Warning($"[UiSound] clip {fileName} exceeded the decode budget; truncated");
            }

            var trimmed = new float[total];
            Array.Copy(buffer, trimmed, total);
            return trimmed;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[UiSound] decoding {fileName} failed");
            return Array.Empty<float>();
        }
    }

    private bool EnsureOutput()
    {
        if (output is not null && mixer is not null)
        {
            return true;
        }

        try
        {
            var built = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, ChannelCount))
            {
                ReadFully = true,
            };
            built.MixerInputEnded += OnMixerInputEnded;
            var builtBus = new VolumeSampleProvider(built) { Volume = busVolume };
            var builtOutput = AudioOutputFactory.Create(OutputLatencyMilliseconds);
            builtOutput.Init(builtBus, true);
            builtOutput.Play();
            mixer = built;
            bus = builtBus;
            output = builtOutput;
            Volatile.Write(ref activeVoices, 0);
            return true;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[UiSound] opening the interface sound output failed");
            mixer = null;
            bus = null;
            output = null;
            return false;
        }
    }

    private static void DisposeOutput(IWavePlayer? stale)
    {
        if (stale is null)
        {
            return;
        }

        try
        {
            stale.Stop();
        }
        catch (Exception exception)
        {
            AepLog.Debug(exception, "[UiSound] stopping the interface sound output failed");
        }

        stale.Dispose();
    }

    public void Dispose()
    {
        IWavePlayer? stale;
        lock (gate)
        {
            disposed = true;
            stale = output;
            output = null;
            mixer = null;
            bus = null;
        }

        DisposeOutput(stale);
    }

    private sealed class ClipSampleProvider : ISampleProvider
    {
        private readonly float[] clip;
        private readonly float gain;
        private readonly float rate;
        private int position;
        private double framePosition;

        public ClipSampleProvider(float[] clip, float gain, float rate)
        {
            this.clip = clip;
            this.gain = gain;
            this.rate = rate;
        }

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, ChannelCount);

        public int Read(float[] buffer, int offset, int count)
        {
            return rate == 1f ? ReadDirect(buffer, offset, count) : ReadResampled(buffer, offset, count);
        }

        private int ReadResampled(float[] buffer, int offset, int count)
        {
            var frameCount = clip.Length / ChannelCount;
            var written = 0;
            while (written + ChannelCount <= count)
            {
                var frameIndex = (int)framePosition;
                if (frameIndex + 1 >= frameCount)
                {
                    break;
                }

                var fraction = (float)(framePosition - frameIndex);
                var sampleIndex = frameIndex * ChannelCount;
                for (var channel = 0; channel < ChannelCount; channel++)
                {
                    var current = clip[sampleIndex + channel];
                    var next = clip[sampleIndex + ChannelCount + channel];
                    buffer[offset + written + channel] = (current + (next - current) * fraction) * gain;
                }

                written += ChannelCount;
                framePosition += rate;
            }

            return written;
        }

        private int ReadDirect(float[] buffer, int offset, int count)
        {
            var available = clip.Length - position;
            if (available <= 0)
            {
                return 0;
            }

            var toCopy = Math.Min(count, available);
            for (var index = 0; index < toCopy; index++)
            {
                buffer[offset + index] = clip[position + index] * gain;
            }

            position += toCopy;
            return toCopy;
        }
    }
}
