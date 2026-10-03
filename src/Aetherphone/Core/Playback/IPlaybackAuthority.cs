using Aetherphone.Core.Songs;

namespace Aetherphone.Core.Playback;

internal enum PlaybackIntentKind : byte
{
    TogglePlayPause,
    Next,
    Previous,
    Seek,
    PlayNext,
    PlayLast,
    PlaySongs,
    JumpTo,
    RemoveQueued,
    MoveQueued,
    Stop,
    TrackEnded,
}

internal readonly struct PlaybackIntent
{
    public readonly PlaybackIntentKind Kind;
    public readonly Song Song;
    public readonly Song[] Songs;
    public readonly int Index;
    public readonly int EntryId;
    public readonly float Seconds;

    public PlaybackIntent(PlaybackIntentKind kind, in Song song = default, Song[]? songs = null, int index = 0,
        int entryId = 0, float seconds = 0f)
    {
        Kind = kind;
        Song = song;
        Songs = songs ?? Array.Empty<Song>();
        Index = index;
        EntryId = entryId;
        Seconds = seconds;
    }
}

internal interface IPlaybackAuthority
{
    bool TryHandle(in PlaybackIntent intent);
}
