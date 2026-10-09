using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Aethergram;

internal sealed partial class AethergramApp
{
    private const float LogoShadowReach = 0.95f;
    private const int LogoShadowCells = 8;

    private static readonly Vector4 LogoBone = new(1f, 0.894f, 0.894f, 1f);
    private static readonly Vector4 LogoShadow = new(0.11f, 0.004f, 0.02f, 0.75f);

    private int seasonApplied = -1;

    private static string HomeGlyph => SeasonalTheme.Halloween ? PhoneIcons.BuildingCastle : PhoneIcons.Home;
    private static string HomeActiveGlyph => SeasonalTheme.Halloween ? PhoneIcons.BuildingCastle : PhoneIcons.HomeFilled;
    private static string SearchGlyph => SeasonalTheme.Halloween ? PhoneIcons.Eye : PhoneIcons.Search;
    private static string SearchActiveGlyph => SeasonalTheme.Halloween ? PhoneIcons.EyeFilled : string.Empty;
    private static string MessagesGlyph => SeasonalTheme.Halloween ? PhoneIcons.Bat : PhoneIcons.Send;
    private static string MessagesActiveGlyph => SeasonalTheme.Halloween ? PhoneIcons.Bat : PhoneIcons.SendFilled;

    private void SyncSeason()
    {
        var season = SeasonalTheme.Halloween ? 1 : 0;
        if (season == seasonApplied)
        {
            return;
        }

        seasonApplied = season;
        ui.Palette = AethergramInk.CurrentPalette;
        FeedTabsStyle = FeedTabsStyleFor();
        ActivityUnreadWash = ActivityUnreadWashFor();
    }

    private static void DrawNight(Rect screen, float top)
    {
        if (!SeasonalTheme.Halloween)
        {
            return;
        }

        NightScene.BloodMoon(ImGui.GetWindowDrawList(), screen, top + AppHeader.Height * UiScale.Current * 0.5f);
    }

    private static Vector4 LogoInk(ImDrawListPtr drawList, Vector2 center, float size)
    {
        if (!SeasonalTheme.Halloween)
        {
            return Ink.AccentLink;
        }

        NightScene.Glow(drawList, center, size * LogoShadowReach, LogoShadow, LogoShadowCells);
        return LogoBone;
    }

    private static UnderlineTabStyle FeedTabsStyleFor() => new(FeedTabStyle, FeedTabIdleStyle,
        AethergramInk.Shared.TitleInk, AethergramInk.Shared.SegmentIdleInk, AethergramInk.Shared.TitleInk,
        FeedTabUnderline, CellPadX, Motion.Release);

    private static Vector4 ActivityUnreadWashFor() => Palette.WithAlpha(AethergramInk.Shared.Accent, 0.06f);
}
