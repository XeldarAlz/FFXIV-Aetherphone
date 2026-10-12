using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino.Strip;

internal sealed class ClubCapsule
{
    public const float Height = FloorCapsule.Height;

    private readonly CasinoTextCache texts = new();

    public FloorCapsuleContent Content(AppSkin ui, CasinoClubDto club, bool half)
    {
        var tier = CasinoClubTiers.Clamp(club.Tier);
        var tierName = Loc.T(ClubTierArt.TierName(tier));
        var title = half ? tierName : texts.Named(L.Strip.ClubTierTitle, tierName);
        var line = string.Empty;
        if (!half)
        {
            line = tier >= CasinoClubTiers.Obsidian || club.NextTierPoints <= club.Points
                ? Loc.T(L.Strip.ClubTop)
                : texts.NamedNumber(L.Strip.ClubPointsTo, Loc.T(ClubTierArt.TierName(tier + 1)),
                    Math.Max(0, club.NextTierPoints - club.Points));
        }

        return new FloorCapsuleContent("casino.club.capsule", FontAwesomeIcon.Crown, ClubTierArt.TierTint(tier), title,
            CasinoClubTiers.Progress(club), line, ui.BodyInk);
    }
}
