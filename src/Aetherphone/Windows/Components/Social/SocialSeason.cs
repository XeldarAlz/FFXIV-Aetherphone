using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class SocialSeason
{
    private const float CharmReach = 0.6f;

    public static UiSound Sound(UiSound halloween) => SeasonalTheme.Halloween ? halloween : UiSound.Tap;

    public static bool RewardedSelf(string? signedInId, string userId) =>
        Treats.Rewarded && !string.IsNullOrEmpty(signedInId) &&
        string.Equals(signedInId, userId, StringComparison.Ordinal);

    public static void OfferTreat(Rect screen, Rect area, int depth, TreatSpot home, TreatSpot deep,
        float headerHeight) =>
        Treats.Offer(ImGui.GetWindowDrawList(), depth > 1 ? deep : home,
            TreatBand.Header(screen, area.Min.Y, headerHeight * UiScale.Current));

    public static bool CharmTapped(Vector2 center, float size, UiSound sound, ScreenToast toast, LocString message)
    {
        var reach = new Vector2(size * CharmReach, size * CharmReach);
        if (!UiInteract.HoverClick(center - reach, center + reach, sound))
        {
            return false;
        }

        toast.Show(Loc.T(message));
        return true;
    }
}
