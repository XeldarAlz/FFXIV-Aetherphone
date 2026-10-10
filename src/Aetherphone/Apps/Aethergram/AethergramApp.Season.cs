using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
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

    private const int FlightCapacity = 28;
    private const float FlightLife = 3f;
    private const float FlightLift = 60f;
    private const float FlightFlap = 22f;
    private const int SwarmSize = 22;
    private const int IntroSwarmSize = 7;

    private static readonly Vector4 GlowInk = new(1f, 0.29f, 0.37f, 1f);
    private static readonly Vector4 FlightInk = new(0.055f, 0.008f, 0.02f, 0.95f);
    private static readonly Vector4 FlightRim = new(1f, 0.275f, 0.353f, 0.35f);

    private readonly Flight[] flights = new Flight[FlightCapacity];
    private readonly SeasonIntro intro = new();
    private bool introSwarmPending;
    private int seasonApplied = -1;

    private string OwnDisplayName => store.Me?.DisplayName ?? string.Empty;

    private string OwnHandle => store.Me?.Handle ?? string.Empty;

    private void OfferTreat(Rect area, int depth) => SocialSeason.OfferTreat(screenRect, area, depth,
        TreatSpot.AethergramFeed, TreatSpot.AethergramDeep, AppHeader.Height);

    private static LocString CaughtUpTitle =>
        SeasonalTheme.Halloween ? L.Seasonal.AethergramCaughtUp : L.Social.FeedCaughtUp;
    private static LocString CaughtUpHint =>
        SeasonalTheme.Halloween ? L.Seasonal.AethergramCaughtUpHint : L.Social.FeedCaughtUpHint;

    private struct Flight
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public double Born;
        public float Size;
        public float Phase;
        public bool Active;
    }

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
        doubleTapLike.Crimson = SeasonalTheme.Halloween;
        var pullStyle = SeasonalTheme.Halloween ? PullStyle.Bat : PullStyle.Dots;
        var refreshSound = SeasonalTheme.Halloween ? UiSound.HalloweenOrgan : UiSound.Refresh;
        explorePull.Style = pullStyle;
        explorePull.RefreshSound = refreshSound;
        foreach (var pull in pullToRefresh.Values)
        {
            pull.Style = pullStyle;
            pull.RefreshSound = refreshSound;
        }

        tabBar.TapSound = SocialSeason.Sound(UiSound.HalloweenThump);
    }

    private void DrawNight(Rect screen, float top)
    {
        if (!SeasonalTheme.Halloween)
        {
            return;
        }

        var moonY = top + AppHeader.Height * UiScale.Current * 0.5f;
        var view = intro.ViewFor(screen);
        NightScene.BloodMoon(ImGui.GetWindowDrawList(), screen, moonY, view);
        if (!introSwarmPending)
        {
            return;
        }

        introSwarmPending = false;
        ReleaseSwarm(NightScene.BloodMoonCenter(screen, moonY, view), IntroSwarmSize);
    }

    private void BeginIntro()
    {
        introSwarmPending = intro.Begin(Id);
    }

    private void ReleaseSwarm(Vector2 moon, int count)
    {
        var scale = UiScale.Current;
        for (var index = 0; index < count; index++)
        {
            var angle = -MathF.PI * (0.05f + Random.Shared.NextSingle() * 0.55f);
            var speed = 140f + Random.Shared.NextSingle() * 220f;
            var velocity = new Vector2(MathF.Cos(angle) * speed + 60f, MathF.Sin(angle) * speed * 0.6f + 40f) * scale;
            var scatter = new Vector2(Random.Shared.NextSingle() - 0.5f, Random.Shared.NextSingle() - 0.5f) * 30f * scale;
            Launch(moon + scatter, velocity, 0.8f + Random.Shared.NextSingle() * 0.8f, Random.Shared.NextSingle() * 0.5f);
        }
    }

    private void DrawLogoTap(Vector2 logoCenter, float logoSize, float moonY)
    {
        if (!SeasonalTheme.Halloween)
        {
            return;
        }

        if (SocialSeason.CharmTapped(logoCenter, logoSize, UiSound.HalloweenSwarm, toast, L.Seasonal.NightTakesWing))
        {
            ReleaseSwarm(NightScene.BloodMoonCenter(screenRect, moonY, intro.ViewFor(screenRect)), SwarmSize);
        }
    }

    private void SendByBat(Vector2 from)
    {
        if (!SeasonalTheme.Halloween)
        {
            return;
        }

        Launch(from, new Vector2(300f, -260f) * UiScale.Current, 1.3f, 0f);
    }

    private void NoteSaved(bool saving)
    {
        if (saving && SeasonalTheme.Halloween)
        {
            toast.Show(Loc.T(L.Seasonal.SavedToCrypt));
        }
    }

    private void Launch(Vector2 from, Vector2 velocity, float size, float delay)
    {
        for (var index = 0; index < flights.Length; index++)
        {
            if (flights[index].Active)
            {
                continue;
            }

            flights[index] = new Flight
            {
                Position = from,
                Velocity = velocity,
                Born = ImGui.GetTime() + delay,
                Size = size,
                Phase = Random.Shared.NextSingle() * 6f,
                Active = true,
            };
            return;
        }
    }

    private void DrawFlights(Rect screen)
    {
        var now = ImGui.GetTime();
        var delta = ImGui.GetIO().DeltaTime;
        var scale = UiScale.Current;
        var drawList = ImGui.GetForegroundDrawList();
        drawList.PushClipRect(screen.Min, screen.Max, false);
        var ink = ImGui.GetColorU32(FlightInk);
        var rim = ImGui.GetColorU32(FlightRim);
        for (var index = 0; index < flights.Length; index++)
        {
            ref var flight = ref flights[index];
            if (!flight.Active || now < flight.Born)
            {
                continue;
            }

            flight.Velocity.Y -= FlightLift * scale * delta;
            flight.Position += flight.Velocity * delta;
            flight.Phase += FlightFlap * delta;
            if (now - flight.Born > FlightLife || flight.Position.X > screen.Max.X + 40f * scale ||
                flight.Position.Y < screen.Min.Y - 40f * scale)
            {
                flight.Active = false;
                continue;
            }

            var flap = MathF.Sin(flight.Phase);
            NightScene.DrawBat(drawList, flight.Position, flight.Size * 1.14f * scale, flap, rim);
            NightScene.DrawBat(drawList, flight.Position, flight.Size * scale, flap, ink);
        }

        drawList.PopClipRect();
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

    private static UnderlineTabStyle FeedTabsStyleFor(SocialInk ink) => new(FeedTabStyle, FeedTabIdleStyle,
        ink.TitleInk, ink.SegmentIdleInk, ink.TitleInk, FeedTabUnderline, CellPadX, Motion.Release);

    private static Vector4 ActivityUnreadWashFor(SocialInk ink) => Palette.WithAlpha(ink.Accent, 0.06f);
}
