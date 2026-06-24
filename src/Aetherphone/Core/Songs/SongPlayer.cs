using System.IO;
using System.Threading;
using Aetherphone.Core.Net;
using NAudio.MediaFoundation;
using NAudio.Wave;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace Aetherphone.Core.Songs;

internal enum SongPlaybackState : byte
{
    Stopped,
    Resolving,
    Buffering,
    Playing,
    Failed,
}

internal sealed class SongPlayer : IDisposable
{
    private static readonly TimeSpan CacheMaxAge = TimeSpan.FromDays(14);

    private readonly YoutubeClient youtube;
    private readonly DiskCache cache;
    private readonly object gate = new();

    private CancellationTokenSource? cancellation;
    private Thread? worker;

    private volatile SongPlaybackState state = SongPlaybackState.Stopped;
    private volatile string currentVideoId = string.Empty;
    private volatile string currentTitle = string.Empty;
    private volatile string currentAuthor = string.Empty;
    private volatile string currentThumbnail = string.Empty;
    private float volume = 0.6f;
    private float positionSeconds;
    private float durationSeconds;
    private int pendingSeekMs = -1;

    private Song[] queue = Array.Empty<Song>();
    private int queueIndex = -1;

    public SongPlayer(YoutubeClient youtube, DiskCache cache)
    {
        this.youtube = youtube;
        this.cache = cache;
        MediaFoundationApi.Startup();
    }

    public SongPlaybackState State => state;

    public string CurrentVideoId => currentVideoId;

    public string CurrentTitle => currentTitle;

    public string CurrentAuthor => currentAuthor;

    public string CurrentThumbnail => currentThumbnail;

    public bool HasQueue => queue.Length > 1;

    public float Position => positionSeconds;

    public float Duration => durationSeconds;

    public float Volume
    {
        get => volume;
        set => volume = Math.Clamp(value, 0f, 1f);
    }

    public void Play(Song[] songs, int index)
    {
        if (songs is null || songs.Length == 0)
        {
            return;
        }

        var start = Math.Clamp(index, 0, songs.Length - 1);
        lock (gate)
        {
            queue = songs;
            queueIndex = start;
        }

        StartSong(songs[start]);
    }

    public void Next() => Skip(1);

    public void Previous() => Skip(-1);

    private void Skip(int direction)
    {
        Song song;
        lock (gate)
        {
            if (queue.Length == 0)
            {
                return;
            }

            queueIndex = ((queueIndex + direction) % queue.Length + queue.Length) % queue.Length;
            song = queue[queueIndex];
        }

        StartSong(song);
    }

    public void Seek(float seconds)
    {
        var milliseconds = (int)(Math.Max(0f, seconds) * 1000f);
        Interlocked.Exchange(ref pendingSeekMs, milliseconds);
    }

    private void StartSong(Song song)
    {
        CancelWorker();

        lock (gate)
        {
            currentVideoId = song.VideoId;
            currentTitle = song.Title;
            currentAuthor = song.Author;
            currentThumbnail = song.ThumbnailUrl;
            positionSeconds = 0f;
            durationSeconds = song.DurationSeconds;
            Interlocked.Exchange(ref pendingSeekMs, -1);
            state = SongPlaybackState.Resolving;
            cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            var videoId = song.VideoId;
            worker = new Thread(() => Run(videoId, token))
            {
                IsBackground = true,
                Name = "Aetherphone.Song",
            };
            worker.Start();
        }
    }

    public void Stop()
    {
        CancelWorker();
        state = SongPlaybackState.Stopped;
        currentVideoId = string.Empty;
        currentTitle = string.Empty;
        currentAuthor = string.Empty;
        currentThumbnail = string.Empty;
        positionSeconds = 0f;
        durationSeconds = 0f;
    }

    private void CancelWorker()
    {
        Thread? toJoin;
        CancellationTokenSource? toDispose;
        lock (gate)
        {
            toJoin = worker;
            toDispose = cancellation;
            worker = null;
            cancellation = null;
        }

        toDispose?.Cancel();
        if (toJoin is not null && toJoin.IsAlive && toJoin != Thread.CurrentThread)
        {
            toJoin.Join(TimeSpan.FromSeconds(3));
        }

        toDispose?.Dispose();
    }

    private void Run(string videoId, CancellationToken token)
    {
        MemoryStream? audio = null;
        StreamMediaFoundationReader? reader = null;
        WaveOutEvent? output = null;

        try
        {
            var bytes = cache.Get(videoId, CacheMaxAge) ?? Download(videoId, token);
            if (bytes is null || bytes.Length == 0)
            {
                state = SongPlaybackState.Failed;
                return;
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            state = SongPlaybackState.Buffering;
            audio = new MemoryStream(bytes, false);
            reader = new StreamMediaFoundationReader(audio);
            durationSeconds = (float)reader.TotalTime.TotalSeconds;

            output = new WaveOutEvent { Volume = volume };
            output.Init(reader);
            output.Play();
            state = SongPlaybackState.Playing;

            while (!token.IsCancellationRequested)
            {
                if (output.PlaybackState == NAudio.Wave.PlaybackState.Stopped)
                {
                    break;
                }

                var seek = Interlocked.Exchange(ref pendingSeekMs, -1);
                if (seek >= 0)
                {
                    var clamped = Math.Min(seek, reader.TotalTime.TotalMilliseconds);
                    output.Pause();
                    reader.CurrentTime = TimeSpan.FromMilliseconds(clamped);
                    output.Play();
                }

                positionSeconds = (float)reader.CurrentTime.TotalSeconds;
                output.Volume = volume;
                Thread.Sleep(80);
            }

            if (!token.IsCancellationRequested)
            {
                output.Stop();
                AdvanceAfterCompletion();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            state = SongPlaybackState.Failed;
            AepLog.Warning($"Song playback failed: {exception.Message}");
        }
        finally
        {
            output?.Dispose();
            reader?.Dispose();
            audio?.Dispose();
        }
    }

    private byte[]? Download(string videoId, CancellationToken token)
    {
        var manifest = youtube.Videos.Streams.GetManifestAsync(videoId, token).GetAwaiter().GetResult();
        var best = SelectAudioStream(manifest);
        if (best is null)
        {
            return null;
        }

        using var source = youtube.Videos.Streams.GetAsync(best, token).GetAwaiter().GetResult();
        using var memory = new MemoryStream();
        source.CopyToAsync(memory, token).GetAwaiter().GetResult();
        var bytes = memory.ToArray();
        cache.Set(videoId, bytes);
        return bytes;
    }

    private static AudioOnlyStreamInfo? SelectAudioStream(StreamManifest manifest)
    {
        var streams = manifest.GetAudioOnlyStreams().ToArray();
        AudioOnlyStreamInfo? best = null;
        for (var index = 0; index < streams.Length; index++)
        {
            var candidate = streams[index];
            if (!string.Equals(candidate.Container.Name, "mp4", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (best is null || candidate.Bitrate.BitsPerSecond > best.Bitrate.BitsPerSecond)
            {
                best = candidate;
            }
        }

        return best;
    }

    private void AdvanceAfterCompletion()
    {
        Song next;
        lock (gate)
        {
            if (queue.Length == 0 || queueIndex + 1 >= queue.Length)
            {
                state = SongPlaybackState.Stopped;
                currentVideoId = string.Empty;
                currentTitle = string.Empty;
                currentAuthor = string.Empty;
                currentThumbnail = string.Empty;
                positionSeconds = 0f;
                durationSeconds = 0f;
                return;
            }

            queueIndex++;
            next = queue[queueIndex];
        }

        StartSong(next);
    }

    public void Dispose()
    {
        Stop();
        MediaFoundationApi.Shutdown();
    }
}
