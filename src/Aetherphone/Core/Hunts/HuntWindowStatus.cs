namespace Aetherphone.Core.Hunts;

internal enum HuntWindowStatus : byte
{
    Unknown,
    Closed,
    Open,
    Capped,

    Unmet,

    Spawned,

    Scheduled,
}
