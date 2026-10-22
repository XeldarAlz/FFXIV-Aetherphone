using Aetherphone.Core.Audio;
using Aetherphone.Core.Playback;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NLayer.NAudioSupport;

namespace Aetherphone.Core.Notifications;

internal sealed class SoundEffectPlayer : IDisposable
{
    private readonly object gate = new();
    private readonly List<OneShot> oneShots = new();
    private IWavePlayer? loopOutput;
    private WaveStream? loopReader;
    private string? loopPath;
    private bool disposed;

    public void PlayOnce(string path, float volume) => PlayOnce(path, volume, TimeSpan.Zero);

    public void PlayOnce(string path, float volume, TimeSpan leadIn)
    {
        var clamped = Math.Clamp(volume, 0f, 1f);
        var thread = new Thread(() => RunOnce(path, clamped, leadIn))
        {
            IsBackground = true, Name = "Aetherphone.Sound",
        };
        thread.Start();
    }

    public void StopOneShots() => StopOneShots(null);

    public bool Release(string path)
    {
        StopOneShots(path);
        bool looping;
        lock (gate)
        {
            looping = string.Equals(loopPath, path, StringComparison.OrdinalIgnoreCase);
        }

        if (looping)
        {
            StopLoop();
        }

        return looping;
    }

    private void StopOneShots(string? path)
    {
        List<IWavePlayer>? stopping = null;
        lock (gate)
        {
            for (var index = oneShots.Count - 1; index >= 0; index--)
            {
                if (path is not null &&
                    !string.Equals(oneShots[index].Path, path, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                stopping ??= new List<IWavePlayer>();
                stopping.Add(oneShots[index].Output);
                oneShots.RemoveAt(index);
            }
        }

        if (stopping is null)
        {
            return;
        }

        for (var index = 0; index < stopping.Count; index++)
        {
            try
            {
                stopping[index].Stop();
            }
            catch (Exception exception)
            {
                AepLog.Debug(exception, "[Sound] stopping a one shot failed");
            }
        }
    }

    public void PlayLoop(string path, float volume)
    {
        StopLoop();
        WaveStream reader;
        IWavePlayer output;
        try
        {
            reader = OpenReader(path);
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
            AepLog.Warning(exception, "[Sound] ringtone loop failed");
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
            loopPath = path;
        }

        output.Play();
    }

    public void StopLoop()
    {
        IWavePlayer? output;
        WaveStream? reader;
        lock (gate)
        {
            output = loopOutput;
            reader = loopReader;
            loopOutput = null;
            loopReader = null;
            loopPath = null;
        }

        if (output is null)
        {
            return;
        }

        try
        {
            output.Stop();
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[Sound] stopping the ringtone loop failed");
        }

        output.Dispose();
        reader?.Dispose();
    }

    private void RunOnce(string path, float volume, TimeSpan leadIn)
    {
        WaveStream? reader = null;
        IWavePlayer? output = null;
        Exception? failure = null;
        try
        {
            reader = OpenReader(path);
            var source = WithLeadIn(new VolumeSampleProvider(reader.ToSampleProvider()) { Volume = volume }, leadIn);
            output = AudioOutputFactory.Create();
            output.PlaybackStopped += (_, stopped) => failure = stopped.Exception;
            output.Init(source, true);
            lock (gate)
            {
                if (disposed)
                {
                    return;
                }

                oneShots.Add(new OneShot(output, path));
            }

            output.Play();
            while (output.PlaybackState == NAudio.Wave.PlaybackState.Playing && failure is null)
            {
                Thread.Sleep(50);
            }

            if (failure is not null)
            {
                AepLog.Warning(failure, $"[Sound] playback of {Path.GetFileName(path)} failed");
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[Sound] playback of {Path.GetFileName(path)} failed");
        }
        finally
        {
            if (output is not null)
            {
                lock (gate)
                {
                    RemoveOneShot(output);
                }

                output.Dispose();
            }

            reader?.Dispose();
        }
    }

    internal static ISampleProvider WithLeadIn(ISampleProvider source, TimeSpan leadIn) =>
        leadIn > TimeSpan.Zero
            ? new SilenceProvider(source.WaveFormat).ToSampleProvider().Take(leadIn).FollowedBy(source)
            : source;

    private void RemoveOneShot(IWavePlayer output)
    {
        for (var index = 0; index < oneShots.Count; index++)
        {
            if (ReferenceEquals(oneShots[index].Output, output))
            {
                oneShots.RemoveAt(index);
                return;
            }
        }
    }

    internal static WaveStream OpenReader(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".wav" => OpenWaveReader(path),
            ".mp3" => OpenMp3Reader(path),
            _ => new MediaFoundationReader(path),
        };
    }

    private static readonly Guid PcmSubFormat = new("00000001-0000-0010-8000-00aa00389b71");
    private static readonly Guid IeeeFloatSubFormat = new("00000003-0000-0010-8000-00aa00389b71");

    private static WaveStream OpenMp3Reader(string path)
    {
        try
        {
            return new Mp3FileReaderBase(path, waveFormat => new Mp3FrameDecompressor(waveFormat));
        }
        catch (Exception exception)
        {
            AepLog.Debug(exception, $"[Sound] NAudio mp3 reader rejected {path}; falling back to MediaFoundation");
            return new MediaFoundationReader(path);
        }
    }

    private static WaveStream OpenWaveReader(string path)
    {
        WaveFileReader reader;
        try
        {
            reader = new WaveFileReader(path);
        }
        catch (Exception exception)
        {
            AepLog.Debug(exception, $"[Sound] NAudio wave reader rejected {path}; falling back to MediaFoundation");
            return new MediaFoundationReader(path);
        }

        if (IsSupportedPcmFormat(reader.WaveFormat))
        {
            return reader;
        }

        reader.Dispose();
        return new MediaFoundationReader(path);
    }

    private static bool IsSupportedPcmFormat(WaveFormat format)
    {
        if (format.Encoding is WaveFormatEncoding.Pcm or WaveFormatEncoding.IeeeFloat)
        {
            return true;
        }

        if (format.Encoding is not WaveFormatEncoding.Extensible)
        {
            return false;
        }

        if (format is not WaveFormatExtraData extraData || extraData.ExtraData.Length < 22)
        {
            return false;
        }

        var subFormat = new Guid(extraData.ExtraData[6..22]);
        return subFormat == PcmSubFormat || subFormat == IeeeFloatSubFormat;
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

    private readonly record struct OneShot(IWavePlayer Output, string Path);
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
