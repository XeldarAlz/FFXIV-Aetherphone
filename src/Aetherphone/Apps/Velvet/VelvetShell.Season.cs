using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private int seasonApplied = -1;

    private static string DiscoverGlyph => SeasonalTheme.Halloween ? PhoneIcons.CrystalBall : PhoneIcons.Compass;
    private static string FeedGlyph => SeasonalTheme.Halloween ? PhoneIcons.Candle : PhoneIcons.Photo;
    private static LocString FeedNoneTitle => SeasonalTheme.Halloween ? L.Seasonal.VelvetFeedNone : L.Velvet.FeedNone;
    private static UiSound ConnectSound => SeasonalTheme.Halloween ? UiSound.HalloweenHeartbeat : UiSound.Tap;

    private void SyncSeason()
    {
        var season = SeasonalTheme.Halloween ? 1 : 0;
        if (season == seasonApplied)
        {
            return;
        }

        seasonApplied = season;
        ui.Palette = SeasonalTheme.Halloween ? VelvetTheme.WitchingPalette : VelvetTheme.Palette;
        doubleTapLike.Crimson = SeasonalTheme.Halloween;
        pullToRefresh.Style = SeasonalTheme.Halloween ? PullStyle.Moon : PullStyle.Dots;
        pullToRefresh.RefreshSound = SeasonalTheme.Halloween ? UiSound.HalloweenIgnite : UiSound.Refresh;
    }

    private static void DrawNight(Rect screen, float top)
    {
        if (!SeasonalTheme.Halloween)
        {
            return;
        }

        NightScene.Witching(ImGui.GetWindowDrawList(), screen, top + VHeader.Height * UiScale.Current * 0.5f);
    }

    private void NoteConnected()
    {
        if (SeasonalTheme.Halloween)
        {
            toast.Show(Loc.T(L.Seasonal.VelvetConnected));
        }
    }
}
