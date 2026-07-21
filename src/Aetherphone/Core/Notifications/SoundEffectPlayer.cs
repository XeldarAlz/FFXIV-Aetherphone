using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Aetherphone.Core.Notifications;

internal sealed class SoundEffectPlayer : IDisposable
{
    private readonly object gate = new();
    private readonly List<IWavePlayer> oneShots = new();
    private IWavePlayer? loopOutput;
    private MediaFoundationReader? loopReader;
    private bool disposed;

    public void PlayOnce(string path, float volume)
    {
        var clamped = Math.Clamp(volume, 0f, 1f);
        var thread = new Thread(() => RunOnce(path, clamped))
        {
            IsBackground = true, Name = "Aetherphone.Sound",
        };
        thread.Start();
    }

    public void StopOneShots()
    {
        IWavePlayer[] snapshot;
        lock (gate)
        {
            if (oneShots.Count == 0)
            {
                return;
            }

            snapshot = oneShots.ToArray();
            oneShots.Clear();
        }

        for (var index = 0; index < snapshot.Length; index++)
        {
            try
            {
                snapshot[index].Stop();
            }
            catch (Exception)
            {
            }
        }
    }

    public void PlayLoop(string path, float volume)
    {
        StopLoop();
        MediaFoundationReader reader;
        IWavePlayer output;
        try
        {
            reader = new MediaFoundationReader(path);
            var loop = new LoopStream(reader);
            var volumeProvider = new VolumeSampleProvider(loop.ToSampleProvider())
            {
                Volume = Math.Clamp(volume, 0f, 1f),
            };
            output = AudioOutputFactory.Create();
            output.Init(volumeProvider, true);
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Sound] ringtone loop failed: {exception.Message}");
            return;
        }

        lock (gate)
        {
            if (disposed)
            {
                output.Dispose();
                reader.Dispose();
                return;
            }

            loopOutput = output;
            loopReader = reader;
        }

        output.Play();
    }

    public void StopLoop()
    {
        IWavePlayer? output;
        MediaFoundationReader? reader;
        lock (gate)
        {
            output = loopOutput;
            reader = loopReader;
            loopOutput = null;
            loopReader = null;
        }

        if (output is null)
        {
            return;
        }

        try
        {
            output.Stop();
        }
        catch (Exception)
        {
        }

        output.Dispose();
        reader?.Dispose();
    }

    private void RunOnce(string path, float volume)
    {
        MediaFoundationReader? reader = null;
        IWavePlayer? output = null;
        try
        {
            reader = new MediaFoundationReader(path);
            var volumeProvider = new VolumeSampleProvider(reader.ToSampleProvider()) { Volume = volume };
            output = AudioOutputFactory.Create();
            output.Init(volumeProvider, true);
            lock (gate)
            {
                if (disposed)
                {
                    return;
                }

                oneShots.Add(output);
            }

            output.Play();
            while (output.PlaybackState == NAudio.Wave.PlaybackState.Playing)
            {
                Thread.Sleep(50);
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning($"[Sound] playback failed: {exception.Message}");
        }
        finally
        {
            if (output is not null)
            {
                lock (gate)
                {
                    oneShots.Remove(output);
                }

                output.Dispose();
            }

            reader?.Dispose();
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
        }

        StopLoop();
        StopOneShots();
    }
}

internal sealed class LoopStream : WaveStream
{
    private readonly WaveStream source;

    public LoopStream(WaveStream source)
    {
        this.source = source;
    }

    public override WaveFormat WaveFormat => source.WaveFormat;
    public override long Length => source.Length;

    public override long Position
    {
        get => source.Position;
        set => source.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var total = 0;
        while (total < count)
        {
            var read = source.Read(buffer, offset + total, count - total);
            if (read == 0)
            {
                if (source.Position == 0)
                {
                    break;
                }

                source.Position = 0;
                continue;
            }

            total += read;
        }

        return total;
    }
}
