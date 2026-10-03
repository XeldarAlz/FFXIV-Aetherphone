using Aetherphone.Core.Media;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Songs;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Music.Components;

internal sealed class MusicKit
{
    public MusicKit(AppSkin ui, RemoteImageCache images, PlaybackHub playback, LibraryStore library)
    {
        Ui = ui;
        Images = images;
        Playback = playback;
        Library = library;
    }

    public AppSkin Ui { get; }

    public RemoteImageCache Images { get; }

    public PlaybackHub Playback { get; }

    public LibraryStore Library { get; }

    public DownloadStore? Downloads { get; init; }

    public float Clock { get; private set; }

    public void Tick(float delta) => Clock += delta;

    public bool IsCurrent(in Song song)
    {
        return Playback.SongActive && !song.IsEmpty &&
               string.Equals(Playback.CurrentSong.VideoId, song.VideoId, StringComparison.Ordinal);
    }
}
