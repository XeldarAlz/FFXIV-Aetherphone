using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Interface;

namespace Aetherphone.Core.Social;

[Flags]
internal enum AccountBadges
{
    None = 0,
    Verified = 1 << 0,
    Patreon = 1 << 1,
    Management = 1 << 2,
    Moderator = 1 << 3,
    Developer = 1 << 4,
    Support = 1 << 5,
    Contributor = 1 << 6,
    Aide = 1 << 7,
    Aurelia = 1 << 8,
}

internal readonly record struct RoleBadge(AccountBadges Flag, RoleKind Kind, FontAwesomeIcon Glyph, LocString Tooltip);

internal static class RoleBadges
{
    private static readonly RoleBadge[] ByPrecedence =
    {
        new(AccountBadges.Management, RoleKind.Management, FontAwesomeIcon.Crown, L.Social.RoleManagement),
        new(AccountBadges.Moderator, RoleKind.Moderator, FontAwesomeIcon.Gavel, L.Social.RoleModerator),
        new(AccountBadges.Aide, RoleKind.Aide, FontAwesomeIcon.Heart, L.Social.RoleAide),
        new(AccountBadges.Aurelia, RoleKind.Aurelia, FontAwesomeIcon.Gem, L.Social.RoleAurelia),
        new(AccountBadges.Patreon, RoleKind.Patreon, FontAwesomeIcon.Star, L.Social.RolePatreon),
        new(AccountBadges.Developer, RoleKind.Developer, FontAwesomeIcon.Code, L.Social.RoleDeveloper),
        new(AccountBadges.Support, RoleKind.Support, FontAwesomeIcon.HandHoldingHeart, L.Social.RoleSupport),
        new(AccountBadges.Contributor, RoleKind.Contributor, FontAwesomeIcon.PuzzlePiece, L.Social.RoleContributor),
        new(AccountBadges.Verified, RoleKind.Verified, FontAwesomeIcon.CheckCircle, L.Social.RoleVerified),
    };

    public static RoleBadge? Top(int badges)
    {
        var held = (AccountBadges)badges;
        for (var index = 0; index < ByPrecedence.Length; index++)
        {
            if ((held & ByPrecedence[index].Flag) != 0)
            {
                return ByPrecedence[index];
            }
        }

        return null;
    }

    public static int Count(int badges)
    {
        var held = (AccountBadges)badges;
        var count = 0;
        for (var index = 0; index < ByPrecedence.Length; index++)
        {
            if ((held & ByPrecedence[index].Flag) != 0)
            {
                count++;
            }
        }

        return count;
    }

    public static RoleBadge At(int badges, int position)
    {
        var held = (AccountBadges)badges;
        var seen = 0;
        for (var index = 0; index < ByPrecedence.Length; index++)
        {
            if ((held & ByPrecedence[index].Flag) == 0)
            {
                continue;
            }

            if (seen == position)
            {
                return ByPrecedence[index];
            }

            seen++;
        }

        return default;
    }
}
