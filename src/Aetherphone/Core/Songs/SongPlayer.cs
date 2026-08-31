using System.Linq;
using Aetherphone.Core.Audio;
using Aetherphone.Core.Net;
using NAudio.MediaFoundation;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using YoutubeExplode;
using YoutubeExplode.Exceptions;
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

internal enum SongRepeatMode : byte
{
    Off,
    One,
}

internal sealed class SongPlayer : IDisposable
{
    private const int StreamedThresholdSeconds = 600;
    private const int StreamedAttempts = 2;
    private static readonly TimeSpan CacheMaxAge = TimeSpan.FromDays(14);
    private readonly YoutubeClient youtube;
    private readonly DiskCache cache;
    private readonly SongLinkResolver linkResolver;
    private readonly object gate = new();
    private CancellationTokenSource? cancellation;
    private Thread? worker;
    private int session;
    private volatile SongPlaybackState state = SongPlaybackState.Stopped;
    private volatile bool paused;
    private volatile string currentVideoId = string.Empty;
    private volatile string currentTitle = string.Empty;
    private volatile string currentAuthor = string.Empty;
    private volatile string currentThumbnail = string.Empty;
    private volatile SongRepeatMode repeat = SongRepeatMode.Off;
    private float volume = 0.6f;
    private float positionSeconds;
    private float durationSeconds;
    private int pendingSeekMs = -1;
    private Song[] queue = Array.Empty<Song>();
    private int queueIndex = -1;
    private volatile bool shuffled;
    private int[] shuffleOrder = Array.Empty<int>();
    private int shufflePosition = -1;

    public SongPlayer(YoutubeClient youtube, DiskCache cache, SongLinkResolver linkResolver)
    {
        this.youtube = youtube;
        this.cache = cache;
        this.linkResolver = linkResolver;
        MediaFoundationApi.Startup();
    }

    public SongPlaybackState State => state;
    public bool IsPaused => paused;
    public string CurrentVideoId => currentVideoId;
    public string CurrentTitle => currentTitle;
    public string CurrentAuthor => currentAuthor;
    public string CurrentThumbnail => currentThumbnail;
    public bool HasQueue => queue.Length > 1;
    public float Position => positionSeconds;
    public float Duration => durationSeconds;

    public SongRepeatMode Repeat
    {
        get => repeat;
        set => repeat = value;
    }

    public bool Shuffled
    {
        get => shuffled;
        set
        {
            lock (gate)
            {
                if (shuffled == value)
                {
                    return;
                }

                shuffled = value;
                if (shuffled && queue.Length > 0)
                {
                    RebuildShuffleOrder(queueIndex);
                }
            }
        }
    }

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
            if (shuffled)
            {
                RebuildShuffleOrder(start);
            }
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

            if (shuffled && shuffleOrder.Length == queue.Length)
            {
                shufflePosition = ((shufflePosition + direction) % shuffleOrder.Length + shuffleOrder.Length) %
                                  shuffleOrder.Length;
                queueIndex = shuffleOrder[shufflePosition];
            }
            else
            {
                queueIndex = ((queueIndex + direction) % queue.Length + queue.Length) % queue.Length;
            }

            song = queue[queueIndex];
        }

        StartSong(song);
    }

    private void RebuildShuffleOrder(int startIndex)
    {
        var count = queue.Length;
        if (shuffleOrder.Length != count)
        {
            shuffleOrder = new int[count];
        }

        for (var index = 0; index < count; index++)
        {
            shuffleOrder[index] = index;
        }

        for (var index = count - 1; index > 0; index--)
        {
            var swapIndex = Random.Shared.Next(index + 1);
            (shuffleOrder[index], shuffleOrder[swapIndex]) = (shuffleOrder[swapIndex], shuffleOrder[index]);
        }

        for (var index = 0; index < count; index++)
        {
            if (shuffleOrder[index] == startIndex)
            {
                (shuffleOrder[0], shuffleOrder[index]) = (shuffleOrder[index], shuffleOrder[0]);
                break;
            }
        }

        shufflePosition = 0;
    }

    public void Seek(float seconds)
    {
        var milliseconds = (int)(Math.Max(0f, seconds) * 1000f);
        Interlocked.Exchange(ref pendingSeekMs, milliseconds);
    }

    public void Pause() => paused = true;

    public void Resume() => paused = false;

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
            paused = false;
            Interlocked.Exchange(ref pendingSeekMs, -1);
            state = SongPlaybackState.Resolving;
            cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            var videoId = song.VideoId;
            var knownDurationSeconds = song.DurationSeconds;
            var workerSession = session;
            worker = new Thread(() => Run(videoId, knownDurationSeconds, token, workerSession))
            {
                IsBackground = true, Name = "Aetherphone.Song",
            };
            worker.Start();
        }
    }

    public void Stop()
    {
        CancelWorker();
        paused = false;
        ResetTrackState();
    }

    private void ResetTrackState()
    {
        state = SongPlaybackState.Stopped;
        currentVideoId = string.Empty;
        currentTitle = string.Empty;
        currentAuthor = string.Empty;
        currentThumbnail = string.Empty;
        positionSeconds = 0f;
        durationSeconds = 0f;
    }

    private Thread? CancelWorker()
    {
        Thread? stopped;
        CancellationTokenSource? toCancel;
        lock (gate)
        {
            session++;
            stopped = worker;
            toCancel = cancellation;
            worker = null;
            cancellation = null;
        }

        if (toCancel is not null)
        {
            toCancel.Cancel();
            toCancel.Dispose();
        }

        return stopped;
    }

    private bool IsCurrent(int workerSession)
    {
        lock (gate)
        {
            return workerSession == session;
        }
    }

    private void TrySetState(int workerSession, SongPlaybackState value)
    {
        lock (gate)
        {
            if (workerSession == session)
            {
                state = value;
            }
        }
    }

    private void Run(string videoId, int knownDurationSeconds, CancellationToken token, int workerSession)
    {
        var resumeSeconds = -1f;
        var resolverUsed = false;
        var firstAttempt = true;
        var attemptsRemaining = StreamedAttempts;
        while (attemptsRemaining > 0)
        {
            attemptsRemaining--;
            var allowStreaming = firstAttempt || knownDurationSeconds > StreamedThresholdSeconds;
            firstAttempt = false;
            try
            {
                if (PlayOnce(videoId, knownDurationSeconds, allowStreaming, resumeSeconds, token, workerSession))
                {
                    AdvanceAfterCompletion(workerSession);
                }

                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (YoutubeExplodeException exception) when (!resolverUsed && linkResolver.IsInstalled)
            {
                resolverUsed = true;
                resumeSeconds = positionSeconds;
                TrySetState(workerSession, SongPlaybackState.Buffering);
                AepLog.Warning(exception, "Song stream refused by the source, fetching through the link resolver");
                if (!FillCacheThroughResolver(videoId, token))
                {
                    TrySetState(workerSession, SongPlaybackState.Failed);
                    AepLog.Warning("Song playback failed: the link resolver could not fetch the audio");
                    return;
                }

                attemptsRemaining = StreamedAttempts;
            }
            catch (Exception exception)
            {
                resumeSeconds = positionSeconds;
                if (attemptsRemaining == 0)
                {
                    TrySetState(workerSession, SongPlaybackState.Failed);
                    AepLog.Warning(exception, "Song playback failed");
                    return;
                }

                TrySetState(workerSession, SongPlaybackState.Buffering);
                AepLog.Warning(exception, "Song stream interrupted, retrying");
            }
        }
    }

    private bool FillCacheThroughResolver(string videoId, CancellationToken token)
    {
        if (linkResolver.Fetch(videoId, token) is not { Bytes.Length: > 0 } audio)
        {
            return false;
        }

        cache.Set(audio.IsOpus ? OpusCacheKey(videoId) : videoId, audio.Bytes);
        return true;
    }

    private bool PlayOnce(string videoId, int knownDurationSeconds, bool allowStreaming, float resumeSeconds,
        CancellationToken token, int workerSession)
    {
        MemoryStream? audio = null;
        ISongAudioReader? reader = null;
        IWavePlayer? output = null;
        try
        {
            var bytes = cache.Get(OpusCacheKey(videoId), CacheMaxAge);
            var bytesAreOpus = bytes is not null;
            if (bytes is null)
            {
                bytes = cache.Get(videoId, CacheMaxAge);
            }

            if (bytes is null && linkResolver.IsInstalled && !token.IsCancellationRequested)
            {
                TrySetState(workerSession, SongPlaybackState.Buffering);
                try
                {
                    if (knownDurationSeconds > StreamedThresholdSeconds)
                    {
                        reader = OpenResolverStreamedReader(videoId, token, workerSession);
                    }
                    else if (linkResolver.Fetch(videoId, token) is { Bytes.Length: > 0 } fetched)
                    {
                        cache.Set(fetched.IsOpus ? OpusCacheKey(videoId) : videoId, fetched.Bytes);
                        bytes = fetched.Bytes;
                        bytesAreOpus = fetched.IsOpus;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception) when (!token.IsCancellationRequested)
                {
                    AepLog.Warning(exception, "Song link resolver failed, using the built-in resolver");
                    reader = null;
                }
            }

            if (bytes is null && reader is null && !allowStreaming)
            {
                var downloaded = Download(videoId, token);
                bytes = downloaded?.Bytes;
                bytesAreOpus = downloaded?.IsOpus ?? false;
            }

            if (reader is null)
            {
                if (bytes is not null && bytes.Length > 0)
                {
                    TrySetState(workerSession, SongPlaybackState.Buffering);
                    if (bytesAreOpus)
                    {
                        reader = new OpusWebmSampleProvider(() => new MemoryStream(bytes, false));
                    }
                    else
                    {
                        audio = new MemoryStream(bytes, false);
                        reader = new MediaFoundationSongReader(new StreamMediaFoundationReader(audio));
                    }
                }
                else if (allowStreaming)
                {
                    reader = OpenStreamedReader(videoId, token, workerSession);
                    if (reader is not null && knownDurationSeconds > 0 &&
                        knownDurationSeconds <= StreamedThresholdSeconds)
                    {
                        BeginCacheFill(videoId, token);
                    }
                }
            }

            if (token.IsCancellationRequested)
            {
                return false;
            }

            if (reader is null)
            {
                TrySetState(workerSession, SongPlaybackState.Failed);
                return false;
            }

            if (IsCurrent(workerSession))
            {
                durationSeconds = (float)reader.TotalTime.TotalSeconds;
            }

            if (resumeSeconds > 0f)
            {
                var clampedResume = Math.Min(resumeSeconds, (float)reader.TotalTime.TotalSeconds);
                reader.CurrentTime = TimeSpan.FromSeconds(clampedResume);
            }

            var volumeProvider = new VolumeSampleProvider(reader.ToSampleProvider()) { Volume = volume };
            output = AudioOutputFactory.Create();
            output.Init(volumeProvider, true);
            output.Play();
            TrySetState(workerSession, SongPlaybackState.Playing);
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
                    if (!paused)
                    {
                        output.Play();
                    }
                }

                if (paused && output.PlaybackState == NAudio.Wave.PlaybackState.Playing)
                {
                    output.Pause();
                }
                else if (!paused && output.PlaybackState == NAudio.Wave.PlaybackState.Paused)
                {
                    output.Play();
                }

                if (IsCurrent(workerSession))
                {
                    positionSeconds = (float)reader.CurrentTime.TotalSeconds;
                }

                volumeProvider.Volume = volume;
                Thread.Sleep(80);
            }

            if (token.IsCancellationRequested)
            {
                return false;
            }

            output.Stop();
            return true;
        }
        finally
        {
            output?.Dispose();
            reader?.Dispose();
            audio?.Dispose();
        }
    }

    private void BeginCacheFill(string videoId, CancellationToken token)
    {
        Task.Run(() =>
        {
            try
            {
                Download(videoId, token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception) when (token.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Song cache fill failed");
            }
        }, CancellationToken.None);
    }

    private ISongAudioReader? OpenResolverStreamedReader(string videoId, CancellationToken token, int workerSession)
    {
        if (linkResolver.ResolveStreamUrl(videoId, token) is not { } resolved || token.IsCancellationRequested)
        {
            return null;
        }

        TrySetState(workerSession, SongPlaybackState.Buffering);
        if (resolved.IsOpus)
        {
            var streamUrl = resolved.Url;
            return new OpusWebmSampleProvider(() =>
                new ForwardSeekableStream(SongLinkResolver.OpenHttpStream(streamUrl, token)));
        }

        return new MediaFoundationSongReader(new MediaFoundationReader(resolved.Url));
    }

    private ISongAudioReader? OpenStreamedReader(string videoId, CancellationToken token, int workerSession)
    {
        var manifest = youtube.Videos.Streams.GetManifestAsync(videoId, token).AsTask().GetAwaiter().GetResult();
        var best = SelectAudioStream(manifest);
        if (best is null || token.IsCancellationRequested)
        {
            return null;
        }

        TrySetState(workerSession, SongPlaybackState.Buffering);
        if (IsOpus(best))
        {
            return new OpusWebmSampleProvider(() =>
                new ForwardSeekableStream(youtube.Videos.Streams.GetAsync(best, token).AsTask().GetAwaiter().GetResult()));
        }

        return new MediaFoundationSongReader(new MediaFoundationReader(best.Url));
    }

    private readonly record struct DownloadedAudio(byte[] Bytes, bool IsOpus);

    private DownloadedAudio? Download(string videoId, CancellationToken token)
    {
        var manifest = youtube.Videos.Streams.GetManifestAsync(videoId, token).AsTask().GetAwaiter().GetResult();
        var best = SelectAudioStream(manifest);
        if (best is null)
        {
            return null;
        }

        var isOpus = IsOpus(best);
        using var source = youtube.Videos.Streams.GetAsync(best, token).AsTask().GetAwaiter().GetResult();
        using var memory = new MemoryStream();
        source.CopyToAsync(memory, token).GetAwaiter().GetResult();
        var bytes = memory.ToArray();
        cache.Set(isOpus ? OpusCacheKey(videoId) : videoId, bytes);
        return new DownloadedAudio(bytes, isOpus);
    }

    private static string OpusCacheKey(string videoId) => videoId + ".opus";

    private static bool IsOpus(AudioOnlyStreamInfo stream) =>
        string.Equals(stream.AudioCodec, "opus", StringComparison.OrdinalIgnoreCase);

    private static AudioOnlyStreamInfo? SelectAudioStream(StreamManifest manifest)
    {
        var streams = manifest.GetAudioOnlyStreams().ToArray();
        AudioOnlyStreamInfo? bestOpus = null;
        AudioOnlyStreamInfo? bestMp4 = null;
        for (var index = 0; index < streams.Length; index++)
        {
            var candidate = streams[index];
            if (IsOpus(candidate))
            {
                if (bestOpus is null || candidate.Bitrate.BitsPerSecond > bestOpus.Bitrate.BitsPerSecond)
                {
                    bestOpus = candidate;
                }
            }
            else if (string.Equals(candidate.Container.Name, "mp4", StringComparison.OrdinalIgnoreCase))
            {
                if (bestMp4 is null || candidate.Bitrate.BitsPerSecond > bestMp4.Bitrate.BitsPerSecond)
                {
                    bestMp4 = candidate;
                }
            }
        }

        return bestOpus ?? bestMp4;
    }

    private void AdvanceAfterCompletion(int workerSession)
    {
        Song next;
        lock (gate)
        {
            if (workerSession != session)
            {
                return;
            }

            if (queue.Length == 0)
            {
                ResetTrackState();
                return;
            }

            if (repeat == SongRepeatMode.One)
            {
                next = queue[queueIndex];
            }
            else if (shuffled && shuffleOrder.Length == queue.Length)
            {
                if (shufflePosition + 1 >= shuffleOrder.Length)
                {
                    ResetTrackState();
                    return;
                }

                shufflePosition++;
                queueIndex = shuffleOrder[shufflePosition];
                next = queue[queueIndex];
            }
            else if (queueIndex + 1 < queue.Length)
            {
                queueIndex++;
                next = queue[queueIndex];
            }
            else
            {
                ResetTrackState();
                return;
            }
        }

        StartSong(next);
    }

    public void Dispose()
    {
        var stopped = CancelWorker();
        if (stopped is not null && stopped.IsAlive)
        {
            stopped.Join(TimeSpan.FromSeconds(2));
        }

        if (stopped is not null && stopped.IsAlive)
        {
            AepLog.Warning("Song worker did not exit in time; skipping MediaFoundation shutdown.");
            return;
        }

        MediaFoundationApi.Shutdown();
    }
}
