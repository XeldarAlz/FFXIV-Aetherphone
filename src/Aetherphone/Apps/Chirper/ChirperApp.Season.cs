using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperApp
{
    private int seasonApplied = -1;

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
