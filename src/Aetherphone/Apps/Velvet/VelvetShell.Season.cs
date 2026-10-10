using Aetherphone.Apps.Velvet.Kit;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet;

internal sealed partial class VelvetShell
{
    private const float LogoFlareReach = 1.6f;
    private const float LogoFlareAlpha = 0.55f;
    private const int LogoFlareCells = 10;
    private const float DefaultMoonFraction = 0.46f;

    private static readonly Vector4 GlowInk = new(1f, 0.64f, 0.34f, 1f);

    private readonly SeasonIntro intro = new();
    private bool? halloweenApplied;

    private bool RewardedSelf(string userId) => SocialSeason.RewardedSelf(store.Me?.UserId, userId);

    private Spring moonSlide;
    private bool moonPlaced;

    private static string DiscoverGlyph => SeasonalTheme.Halloween ? PhoneIcons.CrystalBall : PhoneIcons.Compass;
    private static string FeedGlyph => SeasonalTheme.Halloween ? PhoneIcons.Candle : PhoneIcons.Photo;
    private static LocString FeedNoneTitle => SeasonalTheme.Halloween ? L.Seasonal.VelvetFeedNone : L.Velvet.FeedNone;
    private static UiSound ConnectSound => SocialSeason.Sound(UiSound.HalloweenHeartbeat);

    private static UiSound LikeSound(bool liked) => liked ? UiSound.Tap : SocialSeason.Sound(UiSound.HalloweenSparkle);

    private UiSound TitleSound => activeTab switch
    {
        VelvetPage.Discover => SocialSeason.Sound(UiSound.HalloweenCrystal),
        VelvetPage.Feed => SocialSeason.Sound(UiSound.HalloweenIgnite),
        _ => UiSound.Tap,
    };

    private void SyncSeason()
    {
        if (halloweenApplied == SeasonalTheme.Halloween)
        {
            return;
        }

        halloweenApplied = SeasonalTheme.Halloween;
        ui.Palette = SeasonalTheme.Halloween ? VelvetTheme.WitchingPalette : VelvetTheme.Palette;
        doubleTapLike.Crimson = SeasonalTheme.Halloween;
        pullToRefresh.Style = SeasonalTheme.Halloween ? PullStyle.Moon : PullStyle.Dots;
        pullToRefresh.RefreshSound = SeasonalTheme.Halloween ? UiSound.HalloweenIgnite : UiSound.Refresh;
        threadView.UseSendSound(SeasonalTheme.Halloween ? UiSound.HalloweenWhisper : UiSound.MessageSent);
    }

    private void DrawLogoTap(Vector2 logoCenter, float logoSize)
    {
        if (SocialSeason.CharmTapped(logoCenter, logoSize, UiSound.HalloweenFlare, toast, L.Seasonal.CandlesFlare))
        {
            NightScene.Kindle();
        }
    }

    private void DrawNight(Rect screen, float top)
    {
        if (!SeasonalTheme.Halloween)
        {
            return;
        }

        var fraction = moonPlaced ? moonSlide.Value : DefaultMoonFraction;
        var moon = new Vector2(screen.Min.X + screen.Width * fraction, top + VHeader.Height * UiScale.Current * 0.5f);
        NightScene.Witching(ImGui.GetWindowDrawList(), screen, moon, intro.ViewFor(screen));
    }

    private void BeginIntro()
    {
        if (intro.Begin(Id))
        {
            NightScene.Kindle();
        }
    }

    private void PlaceMoon(float titleRight, float iconsLeft)
    {
        if (!SeasonalTheme.Halloween || screenRect.Width <= 0f)
        {
            return;
        }

        var target = ((titleRight + iconsLeft) * 0.5f - screenRect.Min.X) / screenRect.Width;
        if (!moonPlaced)
        {
            moonSlide.SnapTo(target);
            moonPlaced = true;
            return;
        }

        moonSlide.Step(target, Motion.Release, ImGui.GetIO().DeltaTime);
    }

    private void DrawLogoFlare(ImDrawListPtr drawList, Vector2 logoCenter, float logoSize)
    {
        var flare = NightScene.Kindling;
        if (flare <= 0f)
        {
            return;
        }

        drawList.PushClipRect(screenRect.Min, screenRect.Max);
        NightScene.Glow(drawList, logoCenter, logoSize * LogoFlareReach, Spooks.Pumpkin with { W = LogoFlareAlpha * flare },
            LogoFlareCells);
        drawList.PopClipRect();
    }
}
