namespace Aetherphone.Core.SystemMedia;

internal enum MediaTransportStatus : byte
{
    Closed = 0,
    Changing = 1,
    Stopped = 2,
    Playing = 3,
    Paused = 4,
}
