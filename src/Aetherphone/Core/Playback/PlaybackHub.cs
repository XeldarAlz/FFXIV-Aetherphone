using Aetherphone.Core.Localization;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Songs;

namespace Aetherphone.Core.Playback;

internal sealed class PlaybackHub
{
    private readonly RadioPlayer radio;
    private readonly SongPlayer songs;
    private readonly Configuration configuration;
    private float volume;

    public PlaybackHub(RadioPlayer radio, SongPlayer songs, Configuration configuration)
    {
        this.radio = radio;
        this.songs = songs;
        this.configuration = configuration;
        volume = Math.Clamp(configuration.MusicVolume, 0f, 1f);
        radio.Volume = volume;
        songs.Volume = volume;
        songs.Repeat = configuration.MusicRepeat == 0 ? SongRepeatMode.Off : SongRepeatMode.One;
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

    public SongRepeatMode RepeatMode => songs.Repeat;

    public void ToggleRepeat()
    {
        var next = songs.Repeat == SongRepeatMode.Off ? SongRepeatMode.One : SongRepeatMode.Off;
        songs.Repeat = next;
        configuration.MusicRepeat = (int)next;
        configuration.Save();
    }

    public void PlayStations(RadioStation[] stations, int index)
    {
        songs.Stop();
        radio.Play(stations, index);
    }

    public void PlaySongs(Song[] list, int index)
    {
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

        radio.Next();
    }

    public void Previous()
    {
        if (SongActive)
        {
            songs.Previous();
            return;
        }

        radio.Previous();
    }

    public void Stop()
    {
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
            return;
        }

        if (RadioActive)
        {
            radio.Pause();
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
