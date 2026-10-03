namespace Aetherphone.Core.SystemMedia;

[Flags]
internal enum MediaSessionControls : byte
{
    None = 0,
    Play = 1 << 0,
    Pause = 1 << 1,
    PlayPauseToggle = 1 << 2,
    Next = 1 << 3,
    Previous = 1 << 4,
    Seek = 1 << 5,
}
