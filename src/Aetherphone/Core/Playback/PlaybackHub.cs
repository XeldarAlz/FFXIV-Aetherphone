using Aetherphone.Core.Analytics;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Songs;

namespace Aetherphone.Core.Playback;

internal sealed class PlaybackHub
{
    private const long MinListenTicks = 3000;
    private readonly RadioPlayer radio;
    private readonly IAnalyticsService analytics;
    private readonly SongPlayer songs;
    private readonly Configuration configuration;
    private float volume;
    private string listenStation = string.Empty;
    private long listenStartTicks;

    public PlaybackHub(RadioPlayer radio, SongPlayer songs, IAnalyticsService analytics, Configuration configuration)
    {
        this.radio = radio;
        this.songs = songs;
        this.analytics = analytics;
        this.configuration = configuration;
        volume = Math.Clamp(configuration.MusicVolume, 0f, 1f);
        radio.Volume = volume;
        songs.Volume = volume;
    }

    public RadioPlayer Radio => radio;
    public SongPlayer Songs => songs;
    public bool SongActive => songs.State != SongPlaybackState.Stopped;
    public bool RadioActive => radio.State != RadioPlaybackState.Stopped;
    public bool IsActive => SongActive || RadioActive;

    public bool IsPlaying =>
        SongActive
            ? songs.State == SongPlaybackState.Playing && !songs.IsPaused
            : radio.State == RadioPlaybackState.Playing;

    public bool IsPaused => SongActive ? songs.IsPaused : radio.State == RadioPlaybackState.Paused;

    public string Title => SongActive ? songs.CurrentTitle : radio.CurrentStation;
    public string Subtitle => SongActive ? SongSubtitle() : RadioStateLabel(radio.State);
    public bool HasQueue => SongActive ? songs.HasQueue : radio.HasQueue;

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

    public void CommitVolume()
    {
        configuration.Save();
    }

    public void PlayStations(RadioStation[] stations, int index)
    {
        FlushRadioListen();
        songs.Stop();
        radio.Play(stations, index);
        BeginRadioListen();
    }

    public void PlaySongs(Song[] list, int index)
    {
        FlushRadioListen();
        radio.Stop();
        songs.Play(list, index);
    }

    public void Next()
    {
        if (SongActive)
        {
            songs.Next();
            return;
        }

        FlushRadioListen();
        radio.Next();
        BeginRadioListen();
    }

    public void Previous()
    {
        if (SongActive)
        {
            songs.Previous();
            return;
        }

        FlushRadioListen();
        radio.Previous();
        BeginRadioListen();
    }

    public void Stop()
    {
        FlushRadioListen();
        radio.Stop();
        songs.Stop();
    }

    public void TogglePlayPause()
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
            BeginRadioListen();
            return;
        }

        if (RadioActive)
        {
            FlushRadioListen();
            radio.Pause();
        }
    }

    private void BeginRadioListen()
    {
        listenStation = radio.CurrentStation;
        listenStartTicks = Environment.TickCount64;
    }

    private void FlushRadioListen()
    {
        if (listenStartTicks == 0)
        {
            return;
        }

        var elapsedTicks = Environment.TickCount64 - listenStartTicks;
        listenStartTicks = 0;
        if (elapsedTicks >= MinListenTicks && listenStation.Length > 0)
        {
            analytics.Track(AnalyticsEvents.MusicListen(listenStation, elapsedTicks / 1000d));
        }
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

    private static string RadioStateLabel(RadioPlaybackState state)
    {
        return state switch
        {
            RadioPlaybackState.Buffering => Loc.T(L.Music.Buffering),
            RadioPlaybackState.Playing => Loc.T(L.Music.NowPlayingState),
            RadioPlaybackState.Paused => Loc.T(L.Music.Paused),
            RadioPlaybackState.Failed => Loc.T(L.Music.ConnectionLost),
            _ => string.Empty,
        };
    }
}
