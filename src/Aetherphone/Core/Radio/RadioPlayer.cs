using Aetherphone.Core.Audio;
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
    Reconnecting,
}

internal sealed class RadioPlayer : IDisposable
{
    private static readonly TimeSpan BufferDuration = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan PrebufferThreshold = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan BackpressureThreshold = TimeSpan.FromSeconds(15);
    private static readonly int[] ReconnectDelaysMilliseconds = { 1000, 2000, 5000, 10000, 20000, 20000 };
    private const long StableSessionMilliseconds = 30000;
    private const string StreamUserAgent = "Aetherphone";
    private readonly HttpClient client;
    private readonly object gate = new();
    private CancellationTokenSource? cancellation;
    private Thread? worker;
    private HttpResponseMessage? activeResponse;
    private int session;
    private volatile RadioPlaybackState state = RadioPlaybackState.Stopped;
    private volatile string currentStation = string.Empty;
    private volatile string nowPlaying = string.Empty;
    private RadioStation currentStationInfo;
    private float volume = 0.6f;
    private RadioStation[] queue = Array.Empty<RadioStation>();
    private int queueIndex = -1;

    public RadioPlayer()
    {
        client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(StreamUserAgent);
    }

    public RadioPlaybackState State => state;
    public string CurrentStation => currentStation;

    /// The track the station says it is playing, from the metadata spliced into the stream. Empty
    /// when the station sends none, which plenty do.
    public string NowPlaying => nowPlaying;

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
            nowPlaying = string.Empty;
            state = RadioPlaybackState.Buffering;
            cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            var workerSession = session;
            worker = new Thread(() => Stream(station, token, workerSession))
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
        nowPlaying = string.Empty;
        lock (gate)
        {
            currentStationInfo = default;
        }
    }

    public void Pause()
    {
        if (state is not (RadioPlaybackState.Buffering or RadioPlaybackState.Playing
            or RadioPlaybackState.Reconnecting))
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

    // A community DJ's connection blips far more often than a professional station's, and every
    // blip ends the stream for every listener, so community mounts get retried while the station
    // is still meant to be on air. Directory stations keep the original one shot behaviour.
    private void Stream(RadioStation station, CancellationToken token, int workerSession)
    {
        var attempt = 0;
        while (!token.IsCancellationRequested)
        {
            var outcome = StreamOnce(station.StreamUrl, station.Codec, token, workerSession);
            if (token.IsCancellationRequested || !ShouldReconnect(station.IsCommunity, outcome.Stalled))
            {
                return;
            }

            if (outcome.Stable)
            {
                attempt = 0;
            }

            if (attempt >= ReconnectDelaysMilliseconds.Length)
            {
                TrySetState(workerSession, RadioPlaybackState.Failed);
                return;
            }

            TrySetState(workerSession, RadioPlaybackState.Reconnecting);
            if (token.WaitHandle.WaitOne(ReconnectDelaysMilliseconds[attempt]))
            {
                return;
            }

            attempt++;
        }
    }

    internal static bool ShouldReconnect(bool isCommunity, bool stalled) => isCommunity || stalled;

    private readonly record struct StreamOutcome(bool Stable, bool Stalled);

    private void PublishTitle(int workerSession, string title)
    {
        lock (gate)
        {
            if (workerSession == session)
            {
                nowPlaying = title;
            }
        }
    }

    private StreamOutcome StreamOnce(string url, string declaredCodec, CancellationToken token, int workerSession)
    {
        IWavePlayer? output = null;
        VolumeSampleProvider? volumeProvider = null;
        IStreamDecoder? decoder = null;
        BufferedWaveProvider? buffer = null;
        RadioConnection? connection = null;
        var decoded = new byte[Mp3StreamDecoder.MaxDecodedBytes];
        var playingSinceTick = 0L;
        Action<string> onTitle = title => PublishTitle(workerSession, title);
        try
        {
            connection = RadioStreamOpener.Open(client, url, declaredCodec,
                response => TryPublishResponse(response, workerSession), onTitle, token);
            if (connection is null)
            {
                return default;
            }

            decoder = StreamDecoders.Create(connection.Codec, connection.Audio, onTitle);
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

                var count = decoder.Read(decoded);
                if (count <= 0)
                {
                    break;
                }

                var format = decoder.WaveFormat!;
                if (buffer is not null && !buffer.WaveFormat.Equals(format))
                {
                    output?.Stop();
                    output?.Dispose();
                    output = null;
                    volumeProvider = null;
                    buffer = null;
                }

                buffer ??= new BufferedWaveProvider(format)
                {
                    BufferDuration = BufferDuration, DiscardOnBufferOverflow = true,
                };
                buffer.AddSamples(decoded, 0, count);
                if (output is null && buffer.BufferedDuration >= PrebufferThreshold)
                {
                    volumeProvider = new VolumeSampleProvider(buffer.ToSampleProvider()) { Volume = volume };
                    output = AudioOutputFactory.Create();
                    output.Init(volumeProvider, true);
                    output.Play();
                    if (playingSinceTick == 0L)
                    {
                        playingSinceTick = Environment.TickCount64;
                    }

                    TrySetState(workerSession, RadioPlaybackState.Playing);
                }

                if (volumeProvider is not null)
                {
                    volumeProvider.Volume = volume;
                }
            }

            if (!token.IsCancellationRequested)
            {
                TrySetState(workerSession,
                    connection.Stalled ? RadioPlaybackState.Reconnecting : RadioPlaybackState.Stopped);
            }
        }
        catch (Exception) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is RadioStallException || connection?.Stalled == true)
        {
            TrySetState(workerSession, RadioPlaybackState.Reconnecting);
            AepLog.Info(exception, "Radio stream went silent, reconnecting");
        }
        catch (Exception exception)
        {
            TrySetState(workerSession, RadioPlaybackState.Failed);
            AepLog.Warning(exception, "Radio playback failed");
        }
        finally
        {
            output?.Stop();
            output?.Dispose();
            decoder?.Dispose();
            lock (gate)
            {
                if (workerSession == session)
                {
                    activeResponse = null;
                }
            }

            connection?.Dispose();
        }

        var stable = playingSinceTick != 0L && Environment.TickCount64 - playingSinceTick >= StableSessionMilliseconds;
        return new StreamOutcome(stable, connection?.Stalled ?? false);
    }

    public void Dispose()
    {
        Stop();
        client.Dispose();
    }
}
