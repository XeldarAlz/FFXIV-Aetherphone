namespace Aetherphone.Core.SystemMedia;

internal enum MediaSessionPlayback : byte
{
    Closed = 0,
    Opened = 1,
    Changing = 2,
    Stopped = 3,
    Playing = 4,
    Paused = 5,
}
