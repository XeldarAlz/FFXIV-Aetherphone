using Aetherphone.Core.Localization;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Songs;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Playback;

internal sealed class PlaybackHub : IDisposable
{
    public const int AutoplayBatch = 25;
    private const float RestartThresholdSeconds = 3f;
    private const int AutoplayRetryMilliseconds = 60_000;

    private readonly RadioPlayer radio;
    private readonly SongPlayer songs;
    private readonly LibraryStore library;
    private readonly SongLinkResolver resolver;
    private readonly Configuration configuration;
    private readonly IFramework? framework;
    private readonly PlayQueue queue = new();
    private float volume;
    private int completedSignals;
    private int nearEndSignals;
    private int crossfadeSignals;
    private volatile Song[]? autoplayResult;
    private string autoplaySeedId = string.Empty;
    private bool autoplayFetching;
    private long autoplayRetryAt;
    private long sleepDeadline;
    private bool sleepAtTrackEnd;
    private ListeningClock listening;
    private Song listeningSong;

    public PlaybackHub(RadioPlayer radio, SongPlayer songs, LibraryStore library, SongLinkResolver resolver,
        Configuration configuration, IFramework? framework)
    {
        this.radio = radio;
        this.songs = songs;
        this.library = library;
        this.resolver = resolver;
        this.configuration = configuration;
        this.framework = framework;
        volume = Math.Clamp(configuration.MusicVolume, 0f, 1f);
        radio.Volume = volume;
        songs.Volume = volume;
        songs.CrossfadeSeconds = configuration.MusicCrossfadeSeconds;
        songs.Library = library;
        songs.SoundCheckEnabled = configuration.MusicSoundCheck;
        queue.SetShuffle(configuration.MusicShuffle);
        songs.TrackCompleted += OnTrackCompleted;
        songs.TrackNearEnd += OnTrackNearEnd;
        songs.CrossfadePoint += OnCrossfadePoint;
        if (framework is not null)
        {
            framework.Update += OnFrameworkUpdate;
        }
    }

    public event Action? TrackStarted;

    public RadioPlayer Radio => radio;
    public SongPlayer Songs => songs;
    public PlayQueue Queue => queue;
    public IPlaybackAuthority? Authority { get; set; }
    public int TrackVersion { get; private set; }

    public bool SongActive => songs.State != SongPlaybackState.Stopped;
    public bool RadioActive => radio.State != RadioPlaybackState.Stopped;
    public bool IsActive => SongActive || RadioActive;

    public bool IsPlaying =>
        SongActive
            ? songs.State == SongPlaybackState.Playing && !songs.IsPaused
            : radio.State == RadioPlaybackState.Playing;

    public bool IsPaused => SongActive ? songs.IsPaused : radio.State == RadioPlaybackState.Paused;
    public bool IsBuffering => SongActive
        ? songs.State is SongPlaybackState.Resolving or SongPlaybackState.Buffering
        : radio.State is RadioPlaybackState.Buffering or RadioPlaybackState.Reconnecting;

    public Song CurrentSong => songs.CurrentSong;
    public string Title => SongActive ? songs.CurrentTitle : radio.CurrentStation;
    public string Subtitle => SongActive ? SongSubtitle() : RadioSubtitle();
    public string ArtworkUrl => SongActive ? songs.CurrentThumbnail : radio.CurrentStationInfo.ArtworkUrl ?? string.Empty;
    public string RadioNowPlaying => radio.NowPlaying;
    public bool HasQueue => SongActive ? queue.QueuedCount > 0 || queue.HistoryCount > 0 : radio.HasQueue;
    public float Position => SongActive ? songs.Position : 0f;
    public float Duration => SongActive ? songs.Duration : 0f;
    public bool CanSeek => SongActive && songs.Duration > 0f;

    public float Volume
    {
        get => volume;
        set
        {
            volume = Math.Clamp(value, 0f, 1f);
            radio.Volume = volume;
            songs.Volume = volume;
            configuration.MusicVolume = volume;
        }
    }

    public SongRepeatMode RepeatMode => (SongRepeatMode)Math.Clamp(configuration.MusicRepeat, 0, 2);
    public bool ShuffleEnabled => queue.Shuffled;
    public bool AutoplayEnabled => configuration.MusicAutoplay;
    public float CrossfadeSeconds => songs.CrossfadeSeconds;
    public bool SoundCheckEnabled => configuration.MusicSoundCheck;
    public bool SleepTimerActive => sleepDeadline > 0 || sleepAtTrackEnd;
    public bool SleepAtTrackEnd => sleepAtTrackEnd;

    public float SleepRemainingSeconds =>
        sleepDeadline > 0 ? Math.Max(0f, (sleepDeadline - Environment.TickCount64) / 1000f) : 0f;

    public void CommitVolume()
    {
        configuration.Save();
    }

    public void ToggleRepeat()
    {
        var next = RepeatMode switch
        {
            SongRepeatMode.Off => SongRepeatMode.All,
            SongRepeatMode.All => SongRepeatMode.One,
            _ => SongRepeatMode.Off,
        };
        configuration.MusicRepeat = (int)next;
        configuration.Save();
    }

    public void ToggleShuffle()
    {
        SetShuffle(!queue.Shuffled);
    }

    public void SetAutoplay(bool enabled)
    {
        configuration.MusicAutoplay = enabled;
        configuration.Save();
        if (!enabled)
        {
            queue.ClearAutoplay();
        }
    }

    public void SetCrossfade(float seconds)
    {
        songs.CrossfadeSeconds = seconds;
        configuration.MusicCrossfadeSeconds = songs.CrossfadeSeconds;
        configuration.Save();
    }

    public void SetSoundCheck(bool enabled)
    {
        configuration.MusicSoundCheck = enabled;
        songs.SoundCheckEnabled = enabled;
        configuration.Save();
    }

    public void SetSleepTimer(int minutes)
    {
        sleepAtTrackEnd = false;
        sleepDeadline = minutes > 0 ? Environment.TickCount64 + minutes * 60_000L : 0;
    }

    public void SetSleepAtTrackEnd()
    {
        sleepDeadline = 0;
        sleepAtTrackEnd = true;
    }

    public void CancelSleepTimer()
    {
        sleepDeadline = 0;
        sleepAtTrackEnd = false;
    }

    public void PlaySongsShuffled(Song[] list, string contextId = "", string contextTitle = "")
    {
        if (list.Length == 0)
        {
            return;
        }

        SetShuffle(true);
        PlaySongs(list, Random.Shared.Next(list.Length), contextId, contextTitle);
    }

    public void PlaySongs(Song[] list, int index, string contextId = "", string contextTitle = "")
    {
        if (list.Length == 0 || Delegate(new PlaybackIntent(PlaybackIntentKind.PlaySongs, songs: list, index: index)))
        {
            return;
        }

        radio.Stop();
        if (!queue.SetContext(list, index, queue.Shuffled, contextId, contextTitle))
        {
            return;
        }

        StartCurrent(false);
    }

    public void PlayStations(RadioStation[] stations, int index)
    {
        StopSongs();
        radio.Play(stations, index);
    }

    public void PlayNext(in Song song)
    {
        if (Delegate(new PlaybackIntent(PlaybackIntentKind.PlayNext, song)))
        {
            return;
        }

        var wasIdle = !queue.HasCurrent || !SongActive;
        if (wasIdle)
        {
            PlaySongs(new[] { song }, 0);
            return;
        }

        queue.PlayNext(song);
    }

    public void PlayLast(in Song song)
    {
        if (Delegate(new PlaybackIntent(PlaybackIntentKind.PlayLast, song)))
        {
            return;
        }

        if (!queue.HasCurrent || !SongActive)
        {
            PlaySongs(new[] { song }, 0);
            return;
        }

        queue.PlayLast(song);
    }

    public void JumpTo(int entryId)
    {
        if (Delegate(new PlaybackIntent(PlaybackIntentKind.JumpTo, entryId: entryId)))
        {
            return;
        }

        if (queue.JumpTo(entryId, out _))
        {
            StartCurrent(false);
        }
    }

    public void RemoveQueued(int entryId)
    {
        if (Delegate(new PlaybackIntent(PlaybackIntentKind.RemoveQueued, entryId: entryId)))
        {
            return;
        }

        queue.Remove(entryId);
    }

    public void MoveQueued(int entryId, int targetIndex)
    {
        if (Delegate(new PlaybackIntent(PlaybackIntentKind.MoveQueued, entryId: entryId, index: targetIndex)))
        {
            return;
        }

        queue.Move(entryId, targetIndex);
    }

    public void ClearQueued()
    {
        queue.ClearQueued();
    }

    public void Seek(float seconds)
    {
        if (!SongActive || Delegate(new PlaybackIntent(PlaybackIntentKind.Seek, seconds: seconds)))
        {
            return;
        }

        songs.Seek(seconds);
    }

    public void Next()
    {
        if (Delegate(new PlaybackIntent(PlaybackIntentKind.Next)))
        {
            return;
        }

        if (!SongActive)
        {
            radio.Next();
            return;
        }

        AdvanceSong(false, true);
    }

    public void Previous()
    {
        if (Delegate(new PlaybackIntent(PlaybackIntentKind.Previous)))
        {
            return;
        }

        if (!SongActive)
        {
            radio.Previous();
            return;
        }

        if (songs.Position > RestartThresholdSeconds || !queue.TryBack(out _))
        {
            songs.Seek(0f);
            return;
        }

        StartCurrent(false);
    }

    public void Stop()
    {
        if (Delegate(new PlaybackIntent(PlaybackIntentKind.Stop)))
        {
            return;
        }

        radio.Stop();
        StopSongs();
    }

    public void TogglePlayPause()
    {
        if (Delegate(new PlaybackIntent(PlaybackIntentKind.TogglePlayPause)))
        {
            return;
        }

        ApplyTogglePlayPause();
    }

    public void ApplyTogglePlayPause()
    {
        if (SongActive)
        {
            if (songs.IsPaused)
            {
                songs.Resume();
            }
            else
            {
                songs.Pause();
            }

            return;
        }

        if (radio.State == RadioPlaybackState.Paused)
        {
            radio.Resume();
            return;
        }

        if (RadioActive)
        {
            radio.Pause();
        }
    }

    public void PlayRemote(in Song song, double positionSeconds, bool paused)
    {
        radio.Stop();
        if (!string.Equals(songs.CurrentVideoId, song.VideoId, StringComparison.Ordinal) || !SongActive)
        {
            queue.SetContext(new[] { song }, 0, false, string.Empty, string.Empty);
            songs.Play(song, positionSeconds);
            OnStarted();
        }

        if (paused != songs.IsPaused)
        {
            ApplyTogglePlayPause();
        }
    }

    public void SetRate(float rate)
    {
        songs.Rate = rate;
    }

    public void Dispose()
    {
        CreditListening();
        songs.TrackCompleted -= OnTrackCompleted;
        songs.TrackNearEnd -= OnTrackNearEnd;
        songs.CrossfadePoint -= OnCrossfadePoint;
        if (framework is not null)
        {
            framework.Update -= OnFrameworkUpdate;
        }
    }

    public void Tick()
    {
        listening.Observe(songs.Position,
            SongActive && songs.State == SongPlaybackState.Playing && !songs.IsPaused);
        songs.SoundCheckEnabled = configuration.MusicSoundCheck;
        if (Interlocked.Exchange(ref crossfadeSignals, 0) > 0 && SongActive && RepeatMode != SongRepeatMode.One &&
            !sleepAtTrackEnd && Authority is null)
        {
            AdvanceSong(true, false);
        }

        if (Interlocked.Exchange(ref completedSignals, 0) > 0)
        {
            HandleCompletion();
        }

        if (Interlocked.Exchange(ref nearEndSignals, 0) > 0)
        {
            PrefetchNext();
        }

        if (autoplayResult is { } fetched)
        {
            autoplayResult = null;
            autoplayFetching = false;
            if (configuration.MusicAutoplay && queue.HasCurrent &&
                string.Equals(queue.Current.Song.VideoId, autoplaySeedId, StringComparison.Ordinal))
            {
                queue.SetAutoplay(fetched);
            }
        }

        RequestAutoplayIfNeeded();
        if (sleepDeadline > 0 && Environment.TickCount64 >= sleepDeadline)
        {
            sleepDeadline = 0;
            if (!Delegate(new PlaybackIntent(PlaybackIntentKind.Stop)))
            {
                PauseEverything();
            }
        }
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        Tick();
    }

    private void OnTrackCompleted() => Interlocked.Increment(ref completedSignals);
    private void OnTrackNearEnd() => Interlocked.Increment(ref nearEndSignals);
    private void OnCrossfadePoint() => Interlocked.Increment(ref crossfadeSignals);

    private bool Delegate(in PlaybackIntent intent)
    {
        return Authority is { } authority && authority.TryHandle(intent);
    }

    private void HandleCompletion()
    {
        if (sleepAtTrackEnd)
        {
            sleepAtTrackEnd = false;
            if (!Delegate(new PlaybackIntent(PlaybackIntentKind.Stop)))
            {
                StopSongs();
            }

            return;
        }

        if (Delegate(new PlaybackIntent(PlaybackIntentKind.TrackEnded)))
        {
            return;
        }

        if (RepeatMode == SongRepeatMode.One && queue.HasCurrent)
        {
            StartCurrent(false);
            return;
        }

        AdvanceSong(false, false);
    }

    private void AdvanceSong(bool crossfade, bool userInitiated)
    {
        var repeat = RepeatMode == SongRepeatMode.One && userInitiated ? SongRepeatMode.All : RepeatMode;
        if (!queue.TryAdvance(repeat, out _))
        {
            if (!crossfade)
            {
                StopSongs();
            }

            return;
        }

        StartCurrent(crossfade);
    }

    private void StartCurrent(bool crossfade)
    {
        if (!queue.HasCurrent)
        {
            return;
        }

        songs.Play(queue.Current.Song, 0, crossfade);
        OnStarted();
    }

    private void OnStarted()
    {
        CreditListening();
        listeningSong = queue.Current.Song;
        TrackVersion++;
        library.RecordPlay(queue.Current.Song);
        if (!string.Equals(autoplaySeedId, queue.Current.Song.VideoId, StringComparison.Ordinal) &&
            queue.AutoplayCount == 0)
        {
            autoplayRetryAt = 0;
        }

        TrackStarted?.Invoke();
    }

    private void CreditListening()
    {
        var seconds = listening.Take();
        library.RecordListening(listeningSong, seconds);
        listeningSong = default;
    }

    private void StopSongs()
    {
        CreditListening();
        songs.Stop();
        queue.Clear();
        TrackVersion++;
    }

    private void PauseEverything()
    {
        if (SongActive && !songs.IsPaused)
        {
            songs.Pause();
        }

        if (radio.State == RadioPlaybackState.Playing)
        {
            radio.Pause();
        }
    }

    private void PrefetchNext()
    {
        if (queue.QueuedCount > 0)
        {
            songs.Prefetch(queue.QueuedAt(0).Song);
            return;
        }

        if (queue.AutoplayCount > 0)
        {
            songs.Prefetch(queue.AutoplayAt(0).Song);
        }
    }

    private void RequestAutoplayIfNeeded()
    {
        if (!configuration.MusicAutoplay || autoplayFetching || !queue.NeedsAutoplay || !SongActive ||
            RepeatMode != SongRepeatMode.Off || !resolver.IsInstalled || Authority is not null)
        {
            return;
        }

        var now = Environment.TickCount64;
        if (now < autoplayRetryAt)
        {
            return;
        }

        autoplayRetryAt = now + AutoplayRetryMilliseconds;
        autoplayFetching = true;
        var seed = queue.Current.Song.VideoId;
        autoplaySeedId = seed;
        _ = Task.Run(() =>
        {
            try
            {
                var entries = resolver.FetchMix(seed, AutoplayBatch, CancellationToken.None);
                autoplayResult = entries is null ? Array.Empty<Song>() : ToSongs(entries);
            }
            catch (Exception exception)
            {
                AepLog.Debug(exception, "Autoplay fetch failed");
                autoplayResult = Array.Empty<Song>();
            }
        });
    }

    private static Song[] ToSongs(SongSearchEntry[] entries)
    {
        var result = new Song[entries.Length];
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            result[index] = new Song(entry.VideoId, entry.Title, entry.Author, entry.ThumbnailUrl,
                entry.DurationSeconds, entry.ChannelId);
        }

        return result;
    }

    private void SetShuffle(bool enabled)
    {
        queue.SetShuffle(enabled);
        configuration.MusicShuffle = enabled;
        configuration.Save();
    }

    private string SongSubtitle()
    {
        return songs.State switch
        {
            SongPlaybackState.Resolving => Loc.T(L.Common.Loading),
            SongPlaybackState.Buffering => Loc.T(L.Music.Buffering),
            SongPlaybackState.Failed => Loc.T(L.Music.PlaybackFailed),
            _ => songs.CurrentAuthor,
        };
    }

    private string RadioSubtitle()
    {
        var track = radio.NowPlaying;
        return radio.State == RadioPlaybackState.Playing && track.Length > 0
            ? track
            : RadioStateLabel(radio.State);
    }

    private static string RadioStateLabel(RadioPlaybackState state)
    {
        return state switch
        {
            RadioPlaybackState.Buffering => Loc.T(L.Music.Buffering),
            RadioPlaybackState.Reconnecting => Loc.T(L.Music.Reconnecting),
            RadioPlaybackState.Playing => Loc.T(L.Music.NowPlayingState),
            RadioPlaybackState.Paused => Loc.T(L.Music.Paused),
            RadioPlaybackState.Failed => Loc.T(L.Music.ConnectionLost),
            _ => string.Empty,
        };
    }
}
