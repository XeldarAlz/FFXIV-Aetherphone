using Aetherphone.Core.Hunts;

namespace Aetherphone.Core.Maps;

internal static class HuntsMapMarkerIcons
{
    public const uint CandidateIconId = 60557u;
    public const uint SightedIconId = 60444u;
    public const uint ConfirmedIconId = 60403u;
    public const uint ActiveMinionIconId = 60424u;
    public const uint SsSpawnIconId = 60422u;
    public const uint FateInactiveIconId = 63936u;
    public const uint FateActiveIconId = 63939u;

    public static uint IconFor(HuntsMapMarkerState state) => state switch
    {
        HuntsMapMarkerState.Sighted => SightedIconId,
        HuntsMapMarkerState.Confirmed => ConfirmedIconId,
        HuntsMapMarkerState.ActiveMinion => ActiveMinionIconId,
        HuntsMapMarkerState.SsSpawn => SsSpawnIconId,
        HuntsMapMarkerState.FateInactive => FateInactiveIconId,
        HuntsMapMarkerState.FateActive => FateActiveIconId,
        _ => CandidateIconId,
    };
}
