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

    private readonly object gate = new();
    private readonly Dictionary<string, float[]> clips = new(StringComparer.OrdinalIgnoreCase);
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

    public void Play(string fileName, float gain)
    {
        lock (gate)
        {
            if (disposed || Volatile.Read(ref activeVoices) >= MaxVoices || !TryLoadClip(fileName, out var clip))
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
                mixer!.AddMixerInput((ISampleProvider)new ClipSampleProvider(clip, Math.Clamp(gain, 0f, 1f)));
            }
            catch
            {
                Interlocked.Decrement(ref activeVoices);
                throw;
            }

            lastPlayTicks = Environment.TickCount64;
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

        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            var built = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, ChannelCount))
            {
                ReadFully = true,
            };
            built.MixerInputEnded += OnMixerInputEnded;
            var builtBus = new VolumeSampleProvider(built) { Volume = busVolume };
            var builtOutput = AudioOutputFactory.Create(80);
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
        private int position;

        public ClipSampleProvider(float[] clip, float gain)
        {
            this.clip = clip;
            this.gain = gain;
        }

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, ChannelCount);

        public int Read(float[] buffer, int offset, int count)
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
