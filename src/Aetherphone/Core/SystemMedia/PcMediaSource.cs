using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core.SystemMedia;

internal sealed class PcMediaSource : IDisposable
{
    private readonly Configuration configuration;
    private readonly WindowsMediaSessions sessions;
    private readonly PcMediaArtwork artwork = new();

    public PcMediaSource(Configuration configuration, WindowsMediaSessions sessions)
    {
        this.configuration = configuration;
        this.sessions = sessions;
    }

    public ref readonly MediaSessionSnapshot Current
    {
        get
        {
            if (!configuration.ShowWindowsMedia)
            {
                return ref MediaSessionSnapshot.Empty;
            }

            return ref sessions.Current;
        }
    }

    public static bool IsLive(in MediaSessionSnapshot snapshot) =>
        snapshot.HasSession && snapshot.Playback is MediaSessionPlayback.Playing or MediaSessionPlayback.Paused;

    public IDalamudTextureWrap? Artwork(in MediaSessionSnapshot snapshot, float drawnPixels) =>
        artwork.Get(snapshot, drawnPixels);

    public void TogglePlayPause(in MediaSessionSnapshot snapshot)
    {
        var controls = snapshot.Controls;
        if ((controls & MediaSessionControls.PlayPauseToggle) != 0)
        {
            sessions.TogglePlayPause();
            return;
        }

        if (snapshot.IsPlaying && (controls & MediaSessionControls.Pause) != 0)
        {
            sessions.Pause();
            return;
        }

        sessions.Play();
    }

    public MediaSourceOption[] Sources => sessions.Sources;

    public string PinnedAppId => configuration.WindowsMediaSource;

    public bool IsEnabled => configuration.ShowWindowsMedia;

    public void Select(string appId)
    {
        if (configuration.ShowWindowsMedia &&
            string.Equals(configuration.WindowsMediaSource, appId, StringComparison.Ordinal))
        {
            return;
        }

        configuration.ShowWindowsMedia = true;
        configuration.WindowsMediaSource = appId;
        configuration.Save();
        sessions.Refresh();
    }

    public void TurnOff() => configuration.SetWindowsMedia(false, configuration.PublishToWindowsMedia);

    public void ToggleShuffle(in MediaSessionSnapshot snapshot) => sessions.SetShuffle(!snapshot.ShuffleActive);

    public void CycleRepeat(in MediaSessionSnapshot snapshot) => sessions.SetRepeat(NextRepeat(snapshot.Repeat));

    public void SetVolume(float volume) => sessions.SetVolume(volume);

    public static MediaSessionRepeat NextRepeat(MediaSessionRepeat repeat) => repeat switch
    {
        MediaSessionRepeat.None => MediaSessionRepeat.List,
        MediaSessionRepeat.List => MediaSessionRepeat.Track,
        _ => MediaSessionRepeat.None,
    };

    public void Next() => sessions.Next();

    public void Previous() => sessions.Previous();

    public void Seek(TimeSpan position) => sessions.Seek(position);

    public void Dispose() => artwork.Dispose();
}
