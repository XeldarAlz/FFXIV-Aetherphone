namespace Aetherphone.Core.SystemMedia;

internal enum MediaSessionCommandKind : byte
{
    Play,
    Pause,
    TogglePlayPause,
    Next,
    Previous,
    Seek,
}

internal readonly record struct MediaSessionCommand(MediaSessionCommandKind Kind, long PositionTicks);
