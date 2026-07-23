using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Aetherphone.Core.Radio;

internal enum RadioPlaybackState : byte
{
    Stopped,
    Buffering,
    Playing,
    Paused,
    Failed,
}

internal sealed class RadioPlayer : IDisposable
{
    private static readonly TimeSpan BufferDuration = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan PrebufferThreshold = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan BackpressureThreshold = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(20);
    private readonly HttpClient client;
    private readonly object gate = new();
    private CancellationTokenSource? cancellation;
    private Thread? worker;
    private HttpResponseMessage? activeResponse;
    private int session;
    private volatile RadioPlaybackState state = RadioPlaybackState.Stopped;
    private volatile string currentStation = string.Empty;
    private RadioStation currentStationInfo;
    private float volume = 0.6f;
    private RadioStation[] queue = Array.Empty<RadioStation>();
    private int queueIndex = -1;

    public RadioPlayer()
    {
        client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            $"Aetherphone/{AepConstants.Version} (+https://github.com/XeldarAlz/FFXIV-Aetherphone)");
    }

    public RadioPlaybackState State => state;
    public string CurrentStation => currentStation;

    public RadioStation CurrentStationInfo
    {
        get
        {
            lock (gate)
            {
                return currentStationInfo;
            }
        }
    }

    public bool HasQueue => queue.Length > 1;

    public float Volume
    {
        get => volume;
        set => volume = Math.Clamp(value, 0f, 1f);
    }

    public void Play(RadioStation[] stations, int index)
    {
        if (stations is null || stations.Length == 0)
        {
            return;
        }

        var start = Math.Clamp(index, 0, stations.Length - 1);
        lock (gate)
        {
            queue = stations;
            queueIndex = start;
        }

        StartStation(stations[start]);
    }

    public void Next() => Skip(1);
    public void Previous() => Skip(-1);

    private void Skip(int direction)
    {
        RadioStation station;
        lock (gate)
        {
            if (queue.Length == 0)
            {
                return;
            }

            queueIndex = ((queueIndex + direction) % queue.Length + queue.Length) % queue.Length;
            station = queue[queueIndex];
        }

        StartStation(station);
    }

    private void StartStation(RadioStation station)
    {
        Stop();
        lock (gate)
        {
            currentStation = station.Name;
            currentStationInfo = station;
            state = RadioPlaybackState.Buffering;
            cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            var url = station.StreamUrl;
            var workerSession = session;
            worker = new Thread(() => Stream(url, token, workerSession))
            {
                IsBackground = true, Name = "Aetherphone.Radio",
            };
            worker.Start();
        }
    }

    public void Stop()
    {
        Suspend();
        state = RadioPlaybackState.Stopped;
        currentStation = string.Empty;
        lock (gate)
        {
            currentStationInfo = default;
        }
    }

    public void Pause()
    {
        if (state is not (RadioPlaybackState.Buffering or RadioPlaybackState.Playing))
        {
            return;
        }

        var station = currentStation;
        Suspend();
        state = RadioPlaybackState.Paused;
        currentStation = station;
    }

    public void Resume()
    {
        if (state != RadioPlaybackState.Paused)
        {
            return;
        }

        RadioStation station;
        lock (gate)
        {
            if (queueIndex < 0 || queue.Length == 0)
            {
                return;
            }

            station = queue[queueIndex];
        }

        StartStation(station);
    }

    private void Suspend()
    {
        CancellationTokenSource? toCancel;
        HttpResponseMessage? toAbort;
        lock (gate)
        {
            session++;
            toCancel = cancellation;
            toAbort = activeResponse;
            worker = null;
            cancellation = null;
            activeResponse = null;
        }

        toCancel?.Cancel();
        toAbort?.Dispose();
        toCancel?.Dispose();
    }

    private void TrySetState(int workerSession, RadioPlaybackState value)
    {
        lock (gate)
        {
            if (workerSession == session)
            {
                state = value;
            }
        }
    }

    private bool TryPublishResponse(HttpResponseMessage response, int workerSession)
    {
        lock (gate)
        {
            if (workerSession != session)
            {
                return false;
            }

            activeResponse = response;
            return true;
        }
    }

    private void Stream(string url, CancellationToken token, int workerSession)
    {
        IWavePlayer? output = null;
        VolumeSampleProvider? volumeProvider = null;
        IMp3FrameDecompressor? decompressor = null;
        BufferedWaveProvider? buffer = null;
        HttpResponseMessage? response = null;
        var decoded = new byte[16384 * 4];
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using (var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                connectTimeout.CancelAfter(ConnectTimeout);
                response = client.Send(request, HttpCompletionOption.ResponseHeadersRead, connectTimeout.Token);
            }
            if (!TryPublishResponse(response, workerSession))
            {
                return;
            }

            response.EnsureSuccessStatusCode();
            using var network = response.Content.ReadAsStream(token);
            var source = new ReadFullyStream(network);
            while (!token.IsCancellationRequested)
            {
                if (buffer is not null && buffer.BufferedDuration > BackpressureThreshold)
                {
                    Thread.Sleep(200);
                    if (volumeProvider is not null)
                    {
                        volumeProvider.Volume = volume;
                    }

                    continue;
                }

                Mp3Frame? frame;
                try
                {
                    frame = Mp3Frame.LoadFromStream(source);
                }
                catch (EndOfStreamException)
                {
                    break;
                }

                if (frame is null)
                {
                    break;
                }

                if (decompressor is null)
                {
                    decompressor = CreateDecompressor(frame);
                    buffer = new BufferedWaveProvider(decompressor.OutputFormat)
                    {
                        BufferDuration = BufferDuration, DiscardOnBufferOverflow = true,
                    };
                }

                var count = decompressor.DecompressFrame(frame, decoded, 0);
                buffer!.AddSamples(decoded, 0, count);
                if (output is null && buffer.BufferedDuration >= PrebufferThreshold)
                {
                    volumeProvider = new VolumeSampleProvider(buffer.ToSampleProvider()) { Volume = volume };
                    output = AudioOutputFactory.Create();
                    output.Init(volumeProvider, true);
                    output.Play();
                    TrySetState(workerSession, RadioPlaybackState.Playing);
                }

                if (volumeProvider is not null)
                {
                    volumeProvider.Volume = volume;
                }
            }

            if (!token.IsCancellationRequested)
            {
                TrySetState(workerSession, RadioPlaybackState.Stopped);
            }
        }
        catch (Exception) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            TrySetState(workerSession, RadioPlaybackState.Failed);
            AepLog.Warning($"Radio playback failed: {exception.Message}");
        }
        finally
        {
            output?.Stop();
            output?.Dispose();
            decompressor?.Dispose();
            lock (gate)
            {
                if (ReferenceEquals(activeResponse, response))
                {
                    activeResponse = null;
                }
            }

            response?.Dispose();
        }
    }

    private static IMp3FrameDecompressor CreateDecompressor(Mp3Frame frame)
    {
        var format = new Mp3WaveFormat(frame.SampleRate, frame.ChannelMode == ChannelMode.Mono ? 1 : 2,
            frame.FrameLength, frame.BitRate);
        return new AcmMp3FrameDecompressor(format);
    }

    public void Dispose()
    {
        Stop();
        client.Dispose();
    }
}

internal sealed class ReadFullyStream : Stream
{
    private readonly Stream source;

    public ReadFullyStream(Stream source)
    {
        this.source = source;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => 0;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var total = 0;
        while (total < count)
        {
            var read = source.Read(buffer, offset + total, count - total);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return total;
    }

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
