using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Chirper;

internal sealed partial class ChirperApp
{
    private static readonly Vector4 MoonOrbTop = new(0.86f, 0.89f, 1f, 1f);
    private static readonly Vector4 MoonOrbBottom = new(0.44f, 0.52f, 0.80f, 1f);
    private static readonly Vector4 MoonOrbGlyph = new(0.05f, 0.07f, 0.21f, 1f);

    private const float ClawDuration = 0.9f;
    private const float ClawHold = 0.4f;
    private const float ClawDrawTime = 0.14f;
    private const int ClawSegments = 12;
    private const float MoonLogoGrow = 1.2f;

    private static readonly Vector4 GlowInk = new(0.8f, 0.86f, 1f, 1f);
    private static readonly Vector4 ClawInk = new(0.84f, 0.88f, 1f, 0.9f);
    private static readonly Vector4 ClawGlow = new(0.75f, 0.81f, 1f, 0.25f);

    private readonly SeasonIntro intro = new();
    private int seasonApplied = -1;

    private string OwnDisplayName => store.Me?.DisplayName ?? string.Empty;

    private string OwnHandle => store.Me?.Handle ?? string.Empty;

    private void OfferTreat(Rect area, int depth) => SocialSeason.OfferTreat(screenRect, area, depth,
        TreatSpot.ChirperFeed, TreatSpot.ChirperDeep, AppHeader.Height);

    private Vector2 clawAnchor;
    private double clawStart = -100d;

    private static LocString CaughtUpTitle => SeasonalTheme.Halloween ? L.Seasonal.ChirperCaughtUp : L.Social.FeedCaughtUp;
    private static LocString CaughtUpHint =>
        SeasonalTheme.Halloween ? L.Seasonal.ChirperCaughtUpHint : L.Social.FeedCaughtUpHint;

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
        var pullStyle = SeasonalTheme.Halloween ? PullStyle.Moon : PullStyle.Dots;
        foreach (var pull in pullToRefresh.Values)
        {
            pull.Style = pullStyle;
            pull.RefreshSound = SeasonalTheme.Halloween ? UiSound.HalloweenHoot : UiSound.Refresh;
            pull.ArmSound = SeasonalTheme.Halloween ? UiSound.HalloweenRise : null;
        }

        tabBar.TapSound = SocialSeason.Sound(UiSound.HalloweenKnock);
    }

    private void DrawNight(Rect screen, float top)
    {
        if (!SeasonalTheme.Halloween)
        {
            return;
        }

        NightScene.Moonlit(ImGui.GetWindowDrawList(), screen, top + AppHeader.Height * UiScale.Current * 0.5f,
            intro.ViewFor(screen));
    }

    private void BeginIntro()
    {
        if (intro.Begin(Id))
        {
            NightScene.Rouse();
        }
    }

    private void DrawMoonTap(float moonY)
    {
        if (!SeasonalTheme.Halloween)
        {
            return;
        }

        var center = NightScene.MoonlitMoonCenter(screenRect, moonY, intro.ViewFor(screenRect));
        if (SocialSeason.CharmTapped(center, NightScene.MoonlitMoonSize, UiSound.HalloweenChorus, toast,
                L.Seasonal.PackAnswers))
        {
            NightScene.Rouse();
        }
    }

    private void StartClaw(Vector2 anchor)
    {
        if (!SeasonalTheme.Halloween)
        {
            return;
        }

        clawAnchor = anchor;
        clawStart = ImGui.GetTime();
        UiInteract.PlayTap(UiSound.HalloweenClaw);
    }

    private void DrawClaw(Rect screen)
    {
        var age = (float)(ImGui.GetTime() - clawStart);
        if (age < 0f || age >= ClawDuration)
        {
            return;
        }

        var scale = UiScale.Current;
        var drawList = ImGui.GetForegroundDrawList();
        drawList.PushClipRect(screen.Min, screen.Max, false);
        var reach = Math.Clamp(age / ClawDrawTime, 0f, 1f);
        var fade = age < ClawHold ? 1f : 1f - (age - ClawHold) / (ClawDuration - ClawHold);
        var ink = ImGui.GetColorU32(ClawInk with { W = ClawInk.W * fade });
        var glow = ImGui.GetColorU32(ClawGlow with { W = ClawGlow.W * fade });
        for (var stroke = 0; stroke < 3; stroke++)
        {
            var start = new Vector2(screen.Min.X + (70f + stroke * 22f) * scale, clawAnchor.Y - 118f * scale);
            var bend = start + new Vector2(26f, 70f) * scale;
            var end = start + new Vector2(88f, 112f) * scale;
            var thickness = (3.2f - stroke * 0.5f) * scale;
            DrawStroke(drawList, start, bend, end, reach, glow, thickness * 3f);
            DrawStroke(drawList, start, bend, end, reach, ink, thickness);
        }

        drawList.PopClipRect();
    }

    private static void DrawStroke(ImDrawListPtr drawList, Vector2 start, Vector2 bend, Vector2 end, float reach,
        uint ink, float thickness)
    {
        var previous = start;
        for (var step = 1; step <= ClawSegments; step++)
        {
            var along = reach * step / ClawSegments;
            var rest = 1f - along;
            var point = rest * rest * start + 2f * rest * along * bend + along * along * end;
            drawList.AddLine(previous, point, ink, thickness * (1f - 0.6f * along));
            previous = point;
        }
    }

    private static UnderlineTabStyle FeedTabsStyleFor(SocialInk ink) => new(FeedTabStyle, FeedTabIdleStyle,
        ink.AccentLink, ink.SegmentIdleInk, ink.Accent, FeedTabUnderline, CellPadX, Motion.Release);

    private static ActionSheetStyle SheetStyleFor(SocialInk ink) =>
        new(ink.GlassPanel, ink.GlassStroke, ink.TitleInk, ink.Danger, ink.Accent, ink.Hairline);

    private static ScreenToastStyle ToastStyleFor(SocialInk ink) => new(ink.GlassPanel, ink.GlassStroke, ink.TitleInk);

    private static Vector4 UnreadTintFor(SocialInk ink) => Palette.WithAlpha(ink.Accent, 0.045f);
}
