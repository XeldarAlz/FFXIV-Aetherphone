using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Strip;

internal static class ClubTierArt
{
    private static readonly LocString[] TierNames =
    {
        L.Strip.ClubBronze,
        L.Strip.ClubSilver,
        L.Strip.ClubGold,
        L.Strip.ClubPlatinum,
        L.Strip.ClubDiamond,
        L.Strip.ClubRoyal,
        L.Strip.ClubObsidian,
    };

    private static readonly Vector4[] TierTints =
    {
        new(0.80f, 0.50f, 0.28f, 1f),
        new(0.76f, 0.79f, 0.84f, 1f),
        CasinoColors.Money,
        new(0.62f, 0.86f, 0.92f, 1f),
        new(0.55f, 0.80f, 1f, 1f),
        CasinoColors.LightA,
        new(0.42f, 0.36f, 0.58f, 1f),
    };

    public static LocString TierName(int tier) => TierNames[CasinoClubTiers.Clamp(tier)];

    public static Vector4 TierTint(int tier) => TierTints[CasinoClubTiers.Clamp(tier)];
}
