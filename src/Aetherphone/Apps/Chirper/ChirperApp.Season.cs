using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperApp
{
    private static readonly Vector4 MoonOrbTop = new(0.86f, 0.89f, 1f, 1f);
    private static readonly Vector4 MoonOrbBottom = new(0.44f, 0.52f, 0.80f, 1f);
    private static readonly Vector4 MoonOrbGlyph = new(0.05f, 0.07f, 0.21f, 1f);

    private int seasonApplied = -1;

    private static string HomeGlyph => SeasonalTheme.Halloween ? PhoneIcons.Trees : PhoneIcons.Home;
    private static string HomeActiveGlyph => SeasonalTheme.Halloween ? PhoneIcons.Trees : PhoneIcons.HomeFilled;
    private static string ExploreGlyph => SeasonalTheme.Halloween ? PhoneIcons.CrystalBall : PhoneIcons.Search;
    private static string AlertsGlyph => SeasonalTheme.Halloween ? PhoneIcons.Paw : PhoneIcons.Bell;
    private static string AlertsActiveGlyph => SeasonalTheme.Halloween ? PhoneIcons.PawFilled : PhoneIcons.BellFilled;
    private static Vector4 FabTop => SeasonalTheme.Halloween ? MoonOrbTop : ChirperInk.Accent;
    private static Vector4 FabBottom => SeasonalTheme.Halloween ? MoonOrbBottom : ChirperInk.AccentDeep;
    private static Vector4? FabGlyph => SeasonalTheme.Halloween ? MoonOrbGlyph : null;

    private void SyncSeason()
    {
        var season = SeasonalTheme.Halloween ? 1 : 0;
        if (season == seasonApplied)
        {
            return;
        }

        seasonApplied = season;
        ui.Palette = ChirperInk.CurrentPalette;
        ChipControls = ChipControlsFor();
        FeedTabsStyle = FeedTabsStyleFor();
        SheetStyle = SheetStyleFor();
        ToastStyle = ToastStyleFor();
        UnreadTint = UnreadTintFor();
    }

    private static void DrawNight(Rect screen, float top)
    {
        if (!SeasonalTheme.Halloween)
        {
            return;
        }

        NightScene.Moonlit(ImGui.GetWindowDrawList(), screen, top + AppHeader.Height * UiScale.Current * 0.5f);
    }

    private static ControlInk ChipControlsFor() =>
        new(ChirperInk.Accent, ChirperInk.TitleInk, ChirperInk.MutedInk, ChirperInk.Danger);

    private static UnderlineTabStyle FeedTabsStyleFor() => new(FeedTabStyle, FeedTabIdleStyle, ChirperInk.AccentLink,
        ChirperInk.SegmentIdleInk, ChirperInk.Accent, FeedTabUnderline, CellPadX, Motion.Release);

    private static ActionSheetStyle SheetStyleFor() => new(ChirperInk.GlassPanel, ChirperInk.GlassStroke,
        ChirperInk.CurrentPalette.TitleInk, ChirperInk.Danger, ChirperInk.CurrentPalette.Accent, ChirperInk.Hairline);

    private static ScreenToastStyle ToastStyleFor() =>
        new(ChirperInk.GlassPanel, ChirperInk.GlassStroke, ChirperInk.CurrentPalette.TitleInk);

    private static Vector4 UnreadTintFor() => Palette.WithAlpha(ChirperInk.Accent, 0.045f);
}
