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
    Off = 0,
    One = 1,
    All = 2,
}

internal sealed class SongPlayer : IDisposable
{
    public const float PrefetchLeadSeconds = 25f;
    public const float MaximumCrossfadeSeconds = 12f;
    private const int StreamedThresholdSeconds = 600;
    private const int PlaybackAttempts = 2;
    private const int MonitorIntervalMilliseconds = 40;
    private const float DeclickSeconds = 0.04f;
    private const int DrainTimeoutMilliseconds = 1500;
    private const long OutputRecoveryIntervalMilliseconds = 2000;
    private const float LevelRampSeconds = 1.5f;
    private static readonly TimeSpan CacheMaxAge = TimeSpan.FromDays(14);

    private readonly YoutubeClient youtube;
    private readonly DiskCache cache;
    private readonly SongLinkResolver linkResolver;
    private readonly object gate = new();
    private readonly MixingSampleProvider mixer;
    private readonly VolumeSampleProvider master;
    private readonly HashSet<string> prefetching = new(StringComparer.Ordinal);
    private readonly HashSet<string> measuring = new(StringComparer.Ordinal);
    private IWavePlayer? output;
    private CancellationTokenSource? cancellation;
    private TrackVoice? currentVoice;
    private int session;
    private int liveVoices;
    private long lastOutputRecoveryAt = long.MinValue / 2;
    private volatile SongPlaybackState state = SongPlaybackState.Stopped;
    private volatile bool paused;
    private Song currentSong;
    private float positionSeconds;
    private float durationSeconds;
    private float crossfadeSeconds;
    private float rate = 1f;
    private volatile bool soundCheckEnabled;

    public SongPlayer(YoutubeClient youtube, DiskCache cache, SongLinkResolver linkResolver)
    {
        this.youtube = youtube;
        this.cache = cache;
        this.linkResolver = linkResolver;
        mixer = new MixingSampleProvider(
            WaveFormat.CreateIeeeFloatWaveFormat(TrackVoice.OutputSampleRate, TrackVoice.OutputChannels))
        {
            ReadFully = true,
        };
        master = new VolumeSampleProvider(mixer) { Volume = 0.6f };
        if (OperatingSystem.IsWindows())
        {
            MediaFoundationApi.Startup();
        }
    }

    public Func<string, SongResolvedAudio?>? OfflineSource { get; set; }

    public LibraryStore? Library { get; set; }

    public bool SoundCheckEnabled
    {
        get => soundCheckEnabled;
        set
        {
            if (soundCheckEnabled == value)
            {
                return;
            }

            soundCheckEnabled = value;
            var voice = currentVoice;
            voice?.SetLevel(LevelFor(CurrentVideoId), LevelRampSeconds);
        }
    }

    public event Action? TrackCompleted;
    public event Action? TrackNearEnd;
    public event Action? CrossfadePoint;

    public SongPlaybackState State => state;
    public bool IsPaused => paused;
    public Song CurrentSong => currentSong;
    public string CurrentVideoId => currentSong.VideoId ?? string.Empty;
    public string CurrentTitle => currentSong.Title ?? string.Empty;
    public string CurrentAuthor => currentSong.Author ?? string.Empty;
    public string CurrentThumbnail => currentSong.ThumbnailUrl ?? string.Empty;

    public float Position
    {
        get
        {
            var voice = currentVoice;
            return voice is null ? positionSeconds : (float)voice.PositionSeconds;
        }
    }

    public float Duration => durationSeconds;

    public float Volume
    {
        get => master.Volume;
        set => master.Volume = Math.Clamp(value, 0f, 1f);
    }

    public float CrossfadeSeconds
    {
        get => crossfadeSeconds;
        set => crossfadeSeconds = Math.Clamp(value, 0f, MaximumCrossfadeSeconds);
    }

    public float Rate
    {
        get => rate;
        set
        {
            rate = Math.Clamp(value, TrackVoice.MinimumRate, TrackVoice.MaximumRate);
            var voice = currentVoice;
            if (voice is not null)
            {
                voice.Rate = rate;
            }
        }
    }

    public void Play(in Song song, double startSeconds = 0, bool crossfade = false)
    {
        if (song.IsEmpty)
        {
            return;
        }

        var fade = crossfade ? crossfadeSeconds : 0f;
        lock (gate)
        {
            DetachCurrent(fade > 0f ? fade : DeclickSeconds, cancelWorker: fade <= 0f);
            session++;
            currentSong = song;
            positionSeconds = (float)Math.Max(0, startSeconds);
            durationSeconds = song.DurationSeconds;
            paused = false;
            state = SongPlaybackState.Resolving;
            cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            var workerSession = session;
            var target = song;
            var thread = new Thread(() => Run(target, startSeconds, fade, token, workerSession))
            {
                IsBackground = true,
                Name = "Aetherphone.Song",
            };
            thread.Start();
        }

        ResumeOutput();
    }

    public void Seek(float seconds)
    {
        positionSeconds = Math.Max(0f, seconds);
        currentVoice?.Seek(positionSeconds);
    }

    public void Pause()
    {
        paused = true;
        lock (gate)
        {
            output?.Pause();
        }
    }

    public void Resume()
    {
        paused = false;
        ResumeOutput();
    }

    public void Stop()
    {
        lock (gate)
        {
            DetachCurrent(DeclickSeconds, cancelWorker: true);
            session++;
            paused = false;
            ResetTrackState();
        }

        ReleaseOutputIfIdle();
    }

    public void Prefetch(in Song song)
    {
        if (song.IsEmpty || song.DurationSeconds > StreamedThresholdSeconds || !linkResolver.IsInstalled)
        {
            return;
        }

        var videoId = song.VideoId;
        lock (prefetching)
        {
            if (!prefetching.Add(videoId))
            {
                return;
            }
        }

        _ = Task.Run(() =>
        {
            try
            {
                if (OfflineSource?.Invoke(videoId) is not null ||
                    cache.Get(OpusCacheKey(videoId), CacheMaxAge) is not null ||
                    cache.Get(videoId, CacheMaxAge) is not null)
                {
                    return;
                }

                FillCacheThroughResolver(videoId, CancellationToken.None);
            }
            catch (Exception exception)
            {
                AepLog.Debug(exception, "Song prefetch failed");
            }
            finally
            {
                lock (prefetching)
                {
                    prefetching.Remove(videoId);
                }
            }
        });
    }

    private void DetachCurrent(float fadeSeconds, bool cancelWorker)
    {
        currentVoice?.FadeOut(fadeSeconds);
        currentVoice = null;
        if (!cancelWorker)
        {
            cancellation = null;
            return;
        }

        var toCancel = cancellation;
        cancellation = null;
        if (toCancel is null)
        {
            return;
        }

        toCancel.Cancel();
        toCancel.Dispose();
    }

    private void ResetTrackState()
    {
        state = SongPlaybackState.Stopped;
        currentSong = default;
        positionSeconds = 0f;
        durationSeconds = 0f;
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

    private void Run(Song song, double startSeconds, float fadeInSeconds, CancellationToken token, int workerSession)
    {
        var resumeSeconds = startSeconds;
        var resolverUsed = false;
        var attemptsRemaining = PlaybackAttempts;
        while (attemptsRemaining > 0 && !token.IsCancellationRequested)
        {
            attemptsRemaining--;
            var allowStreaming = attemptsRemaining == PlaybackAttempts - 1 ||
                                 song.DurationSeconds > StreamedThresholdSeconds;
            try
            {
                var outcome = PlayOnce(song, resumeSeconds, fadeInSeconds, allowStreaming, token, workerSession);
                if (outcome == VoiceOutcome.Completed && IsCurrent(workerSession))
                {
                    TrackCompleted?.Invoke();
                }

                if (outcome != VoiceOutcome.Faulted)
                {
                    return;
                }

                resumeSeconds = positionSeconds;
                fadeInSeconds = DeclickSeconds;
                if (attemptsRemaining == 0)
                {
                    TrySetState(workerSession, SongPlaybackState.Failed);
                    return;
                }

                TrySetState(workerSession, SongPlaybackState.Buffering);
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
                if (!FillCacheThroughResolver(song.VideoId, token))
                {
                    TrySetState(workerSession, SongPlaybackState.Failed);
                    AepLog.Warning("Song playback failed: the link resolver could not fetch the audio");
                    return;
                }

                attemptsRemaining = PlaybackAttempts;
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

    private enum VoiceOutcome : byte
    {
        Completed,
        Detached,
        Faulted,
        Unavailable,
    }

    private VoiceOutcome PlayOnce(in Song song, double startSeconds, float fadeInSeconds, bool allowStreaming,
        CancellationToken token, int workerSession)
    {
        var reader = OpenReader(song, allowStreaming, token, workerSession, out var fullAudio);
        if (reader is null)
        {
            if (!token.IsCancellationRequested)
            {
                TrySetState(workerSession, SongPlaybackState.Failed);
            }

            return VoiceOutcome.Unavailable;
        }

        var voice = new TrackVoice(reader, startSeconds, fadeInSeconds) { Rate = rate };
        voice.SetLevel(LevelFor(song.VideoId), 0f);
        lock (gate)
        {
            if (workerSession != session || token.IsCancellationRequested)
            {
                reader.Dispose();
                return VoiceOutcome.Detached;
            }

            try
            {
                EnsureOutput();
            }
            catch
            {
                reader.Dispose();
                throw;
            }

            currentVoice = voice;
            durationSeconds = (float)voice.DurationSeconds;
            Interlocked.Increment(ref liveVoices);
            state = SongPlaybackState.Playing;
        }

        mixer.AddMixerInput(voice);

        if (fullAudio is { } audio)
        {
            BeginMeasure(song.VideoId, audio);
        }

        try
        {
            return Monitor(voice, token, workerSession);
        }
        finally
        {
            mixer.RemoveMixerInput(voice);
            voice.DisposeReader();
            Interlocked.Decrement(ref liveVoices);
            ReleaseOutputIfIdle();
        }
    }

    private VoiceOutcome Monitor(TrackVoice voice, CancellationToken token, int workerSession)
    {
        var nearEndRaised = false;
        var crossfadeRaised = false;
        var drainDeadline = long.MaxValue;
        while (!voice.Finished)
        {
            if (token.IsCancellationRequested && drainDeadline == long.MaxValue)
            {
                voice.FadeOut(DeclickSeconds);
                drainDeadline = Environment.TickCount64 + DrainTimeoutMilliseconds;
            }

            if (Environment.TickCount64 > drainDeadline)
            {
                return VoiceOutcome.Detached;
            }

            if (drainDeadline == long.MaxValue)
            {
                voice.ServicePendingSeek();
            }

            if (voice.Faulted)
            {
                return IsCurrent(workerSession) ? VoiceOutcome.Faulted : VoiceOutcome.Detached;
            }

            if (IsCurrent(workerSession))
            {
                var position = (float)voice.PositionSeconds;
                positionSeconds = position;
                var duration = durationSeconds;
                if (duration > 0f && !paused)
                {
                    var remaining = duration - position;
                    if (!nearEndRaised && remaining <= PrefetchLeadSeconds)
                    {
                        nearEndRaised = true;
                        TrackNearEnd?.Invoke();
                    }

                    var fade = crossfadeSeconds;
                    if (!crossfadeRaised && fade > 0f && remaining <= fade && duration > fade * 2f)
                    {
                        crossfadeRaised = true;
                        CrossfadePoint?.Invoke();
                    }
                }
            }

            voice.WaitForWork(MonitorIntervalMilliseconds);
        }

        if (voice.Faulted)
        {
            return IsCurrent(workerSession) ? VoiceOutcome.Faulted : VoiceOutcome.Detached;
        }

        return voice.SourceEnded && !voice.FadingOut ? VoiceOutcome.Completed : VoiceOutcome.Detached;
    }

    private void EnsureOutput()
    {
        if (output is not null)
        {
            return;
        }

        var created = AudioOutputFactory.Create();
        try
        {
            created.Init(master, true);
        }
        catch
        {
            created.Dispose();
            throw;
        }

        created.PlaybackStopped += OnOutputStopped;
        output = created;
        if (!paused)
        {
            created.Play();
        }
    }

    private void ResumeOutput()
    {
        lock (gate)
        {
            if (paused)
            {
                return;
            }

            if (output is null)
            {
                RecoverOutput();
                return;
            }

            if (output.PlaybackState != PlaybackState.Playing)
            {
                output.Play();
            }
        }
    }

    private void OnOutputStopped(object? sender, StoppedEventArgs arguments)
    {
        if (arguments.Exception is not null)
        {
            AepLog.Warning(arguments.Exception, "Song output device stopped");
        }

        IWavePlayer stopped;
        lock (gate)
        {
            if (output is null || !ReferenceEquals(sender, output))
            {
                return;
            }

            stopped = DetachOutput(output);
            var now = Environment.TickCount64;
            if (arguments.Exception is not null && !paused &&
                now - lastOutputRecoveryAt >= OutputRecoveryIntervalMilliseconds)
            {
                lastOutputRecoveryAt = now;
                RecoverOutput();
            }
        }

        DisposeInBackground(stopped);
    }

    private IWavePlayer DetachOutput(IWavePlayer detached)
    {
        detached.PlaybackStopped -= OnOutputStopped;
        output = null;
        return detached;
    }

    private static void DisposeInBackground(IWavePlayer player)
    {
        _ = Task.Run(player.Dispose);
    }

    private void RecoverOutput()
    {
        if (Volatile.Read(ref liveVoices) == 0)
        {
            return;
        }

        try
        {
            EnsureOutput();
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Song output device could not be reopened");
        }
    }

    private void ReleaseOutputIfIdle()
    {
        IWavePlayer idle;
        lock (gate)
        {
            if (output is null || Volatile.Read(ref liveVoices) > 0 || state != SongPlaybackState.Stopped)
            {
                return;
            }

            idle = DetachOutput(output);
        }

        DisposeInBackground(idle);
    }

    private float LevelFor(string videoId)
    {
        return soundCheckEnabled && Library is { } library && library.TryGetLoudnessGain(videoId, out var decibels)
            ? SoundCheckGain.ToLinear(decibels)
            : 1f;
    }

    private void BeginMeasure(string videoId, SongResolvedAudio audio)
    {
        if (Library is not { } library || string.IsNullOrEmpty(videoId) || library.TryGetLoudnessGain(videoId, out _))
        {
            return;
        }

        lock (measuring)
        {
            if (!measuring.Add(videoId))
            {
                return;
            }
        }

        _ = Task.Run(() =>
        {
            try
            {
                var bytes = audio.Bytes;
                using ISongAudioReader reader = audio.IsOpus
                    ? new OpusWebmSampleProvider(() => new MemoryStream(bytes, false))
                    : new MediaFoundationSongReader(new StreamMediaFoundationReader(new MemoryStream(bytes, false)));
                var result = LoudnessMeter.Measure(reader.ToSampleProvider(), CancellationToken.None);
                var decibels = SoundCheckGain.Decibels(result.IntegratedLufs, result.SamplePeak);
                library.RecordLoudnessGain(videoId, decibels);
                var voice = currentVoice;
                if (voice is not null && string.Equals(CurrentVideoId, videoId, StringComparison.Ordinal))
                {
                    voice.SetLevel(LevelFor(videoId), LevelRampSeconds);
                }
            }
            catch (Exception exception)
            {
                AepLog.Debug(exception, "Song loudness measurement failed");
            }
            finally
            {
                lock (measuring)
                {
                    measuring.Remove(videoId);
                }
            }
        });
    }

    private ISongAudioReader? OpenReader(in Song song, bool allowStreaming, CancellationToken token,
        int workerSession, out SongResolvedAudio? fullAudio)
    {
        fullAudio = null;
        var videoId = song.VideoId;
        var offline = OfflineSource?.Invoke(videoId);
        var bytes = offline?.Bytes ?? cache.Get(OpusCacheKey(videoId), CacheMaxAge);
        var bytesAreOpus = offline?.IsOpus ?? bytes is not null;
        bytes ??= cache.Get(videoId, CacheMaxAge);
        if (bytes is null && linkResolver.IsInstalled && !token.IsCancellationRequested)
        {
            TrySetState(workerSession, SongPlaybackState.Buffering);
            try
            {
                if (song.DurationSeconds > StreamedThresholdSeconds)
                {
                    var streamed = OpenResolverStreamedReader(videoId, token);
                    if (streamed is not null)
                    {
                        return streamed;
                    }
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
            }
        }

        if (bytes is null && !allowStreaming)
        {
            var downloaded = Download(videoId, token);
            bytes = downloaded?.Bytes;
            bytesAreOpus = downloaded?.IsOpus ?? false;
        }

        if (token.IsCancellationRequested)
        {
            return null;
        }

        if (bytes is { Length: > 0 })
        {
            TrySetState(workerSession, SongPlaybackState.Buffering);
            var captured = bytes;
            fullAudio = new SongResolvedAudio(captured, bytesAreOpus);
            return bytesAreOpus
                ? new OpusWebmSampleProvider(() => new MemoryStream(captured, false))
                : new MediaFoundationSongReader(new StreamMediaFoundationReader(new MemoryStream(captured, false)));
        }

        if (!allowStreaming)
        {
            return null;
        }

        var reader = OpenStreamedReader(videoId, token);
        if (reader is not null && song.DurationSeconds is > 0 and <= StreamedThresholdSeconds)
        {
            BeginCacheFill(videoId, token);
        }

        return reader;
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

    private void BeginCacheFill(string videoId, CancellationToken token)
    {
        _ = Task.Run(() =>
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

    private ISongAudioReader? OpenResolverStreamedReader(string videoId, CancellationToken token)
    {
        if (linkResolver.ResolveStreamUrl(videoId, token) is not { } resolved || token.IsCancellationRequested)
        {
            return null;
        }

        if (resolved.IsOpus)
        {
            var streamUrl = resolved.Url;
            return new OpusWebmSampleProvider(() =>
                new ForwardSeekableStream(SongLinkResolver.OpenHttpStream(streamUrl, token)));
        }

        return new MediaFoundationSongReader(new MediaFoundationReader(resolved.Url));
    }

    private ISongAudioReader? OpenStreamedReader(string videoId, CancellationToken token)
    {
        var manifest = youtube.Videos.Streams.GetManifestAsync(videoId, token).AsTask().GetAwaiter().GetResult();
        var best = SelectAudioStream(manifest);
        if (best is null || token.IsCancellationRequested)
        {
            return null;
        }

        if (IsOpus(best))
        {
            return new OpusWebmSampleProvider(() =>
                new ForwardSeekableStream(youtube.Videos.Streams.GetAsync(best, token).AsTask().GetAwaiter()
                    .GetResult()));
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

    internal static string OpusCacheKey(string videoId) => videoId + ".opus";

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

    public void Dispose()
    {
        Stop();
        var deadline = Environment.TickCount64 + 2000;
        while (Volatile.Read(ref liveVoices) > 0 && Environment.TickCount64 < deadline)
        {
            Thread.Sleep(20);
        }

        IWavePlayer? remaining = null;
        lock (gate)
        {
            if (output is not null)
            {
                remaining = DetachOutput(output);
            }
        }

        remaining?.Dispose();
        if (Volatile.Read(ref liveVoices) > 0)
        {
            AepLog.Warning("Song worker did not exit in time; skipping MediaFoundation shutdown.");
            return;
        }

        MediaFoundationApi.Shutdown();
    }
}
