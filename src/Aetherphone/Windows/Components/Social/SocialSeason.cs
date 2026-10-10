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

    public static void Play(UiSound halloween)
    {
        if (SeasonalTheme.Halloween)
        {
            UiFeedback.Play(halloween);
        }
    }

    public static void Toast(ScreenToast toast, LocString halloween)
    {
        if (SeasonalTheme.Halloween)
        {
            toast.Show(Loc.T(halloween));
        }
    }

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
        if (!SeasonalTheme.Halloween || !UiInteract.HoverClick(center - reach, center + reach, sound))
        {
            return false;
        }

        toast.Show(Loc.T(message));
        return true;
    }

    public static Vector2 FitWordmark(string name, float maxWidth, float lineHeight, in TextStyle style,
        out string title, out bool gothic)
    {
        gothic = NightWordmark.Fits(name, maxWidth, lineHeight, out var gothicSize);
        title = gothic ? name : Typography.FitText(name, maxWidth, style);
        return gothic ? gothicSize : Typography.Measure(title, style);
    }

    public static void DrawWordmark(ImDrawListPtr drawList, Vector2 position, string title, Vector4 ink,
        float lineHeight, in TextStyle style, bool gothic)
    {
        if (gothic)
        {
            NightWordmark.Draw(drawList, position, title, ink, lineHeight);
            return;
        }

        Typography.Draw(drawList, position, title, ink, style);
    }
}
