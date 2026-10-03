using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Coin.Widgets;

internal sealed class CoinBalanceWidget : IHomeWidget
{
    private const int FreshMilliseconds = 30000;
    private const int CheckInControl = 1;
    private const int ShopControl = 2;
    private const float BarUnits = 6f;
    private const float SmallBarUnits = 4f;
    private const float CapsuleHeightUnits = 30f;
    private const float CapsuleWidthUnits = 112f;
    private const float ShopWidthUnits = 70f;
    private const float StreakIconUnits = 11f;
    private const int SampleStreak = 6;

    private sealed class Purse
    {
        public RollingValue Balance;
        public WidgetFrame BalanceFrame;
        public WidgetEase Bar;
        public CachedText BalanceText;
    }

    private readonly CoinStore coins;
    private readonly AethernetSession session;
    private readonly WidgetStates<Purse> purses = new();
    private WidgetRefresh freshCadence;
    private CachedText todayText;
    private CachedText streakText;
    private bool checkInPending;

    public CoinBalanceWidget(CoinStore coins, AethernetSession session)
    {
        this.coins = coins;
        this.session = session;
    }

    public string Id => "coin.balance";
    public string DisplayName => Loc.T(L.Coin.Balance);
    public string Description => Loc.T(L.WidgetsLife.CoinDescription);
    public string AppId => "coin";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium;

    public float Relevance(string config) =>
        coins.Wallet is { CheckInAvailable: true, Paused: false, FrozenUntilUnix: null } ? 0.9f : 0f;

    public void Draw(in WidgetContext context)
    {
        Maintain();
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var accent = ink.Accent(AppAccents.For(AppId));
        var headerBottom = WidgetChrome.Header(context, ink, AppId, L.Coin.Balance, AppAccents.For(AppId));
        var wallet = coins.Wallet;
        var signedIn = session.IsSignedIn;
        var sample = context.Preview && (!signedIn || wallet is null);
        if (!signedIn && !sample)
        {
            WidgetChrome.Message(context, ink, WidgetMetrics.Below(context, headerBottom), Loc.T(L.Coin.SignInTitle),
                context.Size == WidgetSize.Small ? string.Empty : Loc.T(L.Coin.SignInHint));
            return;
        }

        var content = WidgetMetrics.Content(context);
        if (wallet is null && !sample)
        {
            DrawPlaceholder(context, ink, content, headerBottom);
            return;
        }

        var balance = sample ? WidgetSamples.CoinBalance : wallet!.Balance;
        var earned = sample ? WidgetSamples.CoinEarnedToday : wallet!.EarnedToday;
        var cap = sample ? WidgetSamples.CoinDailyCap : wallet!.DailyCap;
        var checkIn = !sample && wallet!.CheckInAvailable && !wallet.Paused && wallet.FrozenUntilUnix is null;
        var paused = !sample && wallet!.Paused;
        var streak = sample ? SampleStreak : wallet!.StreakDays;
        var purse = purses.For(context.InstanceKey);
        purse.Balance.Update((int)Math.Clamp(balance, 0, int.MaxValue), purse.BalanceFrame.Delta(context.Delta));
        var fraction = cap > 0 ? Math.Clamp(earned / (float)cap, 0f, 1f) : 0f;
        var barFraction = purse.Bar.Step(fraction, context.Delta, !context.Preview);
        if (context.Size == WidgetSize.Small)
        {
            DrawSmall(context, ink, accent, content, purse, barFraction, paused, earned, cap, checkIn);
            return;
        }

        DrawMedium(context, ink, accent, content, headerBottom, purse, barFraction, paused, streak, earned, cap,
            checkIn);
    }

    private void Maintain()
    {
        if (checkInPending && !coins.CheckingIn)
        {
            checkInPending = false;
            coins.TakeCheckInResult();
        }

        if (session.IsSignedIn && freshCadence.Due(FreshMilliseconds))
        {
            coins.EnsureFresh();
        }
    }

    private void DrawSmall(in WidgetContext context, in WidgetInk ink, Vector4 accent, Rect content, Purse purse,
        float barFraction, bool paused, long earned, long cap, bool checkIn)
    {
        var scale = context.Scale;
        var captionHeight = Typography.Measure("Ag", WidgetType.Caption).Y;
        var captionTop = content.Max.Y - captionHeight;
        var status = Status(paused, earned, cap, checkIn);
        WidgetText.Draw(context.DrawList, new Vector2(content.Min.X, captionTop), status,
            checkIn ? accent : ink.Secondary, WidgetType.Caption, content.Width);
        var barHeight = SmallBarUnits * scale;
        var barTop = captionTop - WidgetMetrics.RowGap * 2f * scale - barHeight;
        DrawBar(context.DrawList, ink, accent, new Rect(new Vector2(content.Min.X, barTop),
            new Vector2(content.Max.X, barTop + barHeight)), barFraction);
        var heroStyle = WidgetType.DisplayCompact;
        var heroHeight = Typography.Measure("0", heroStyle).Y;
        DrawBalance(context, ink, purse, new Vector2(content.Min.X, barTop - WidgetMetrics.Gutter * scale - heroHeight),
            content.Width, heroStyle);
    }

    private void DrawMedium(in WidgetContext context, in WidgetInk ink, Vector4 accent, Rect content,
        float headerBottom, Purse purse, float barFraction, bool paused, int streak, long earned, long cap,
        bool checkIn)
    {
        var scale = context.Scale;
        var drawList = context.DrawList;
        var captionHeight = Typography.Measure("Ag", WidgetType.Caption).Y;
        var barHeight = BarUnits * scale;
        var barTop = content.Max.Y - barHeight;
        DrawBar(drawList, ink, accent, new Rect(new Vector2(content.Min.X, barTop), content.Max), barFraction);
        var labelTop = barTop - WidgetMetrics.RowGap * 2f * scale - captionHeight;
        var status = Status(paused, earned, cap, false);
        WidgetText.Draw(drawList, new Vector2(content.Min.X, labelTop), status, ink.Secondary, WidgetType.Caption,
            content.Width);
        var heroTop = headerBottom + WidgetMetrics.RowGap * 2f * scale;
        var heroHeight = DrawBalance(context, ink, purse, new Vector2(content.Min.X, heroTop), content.Width * 0.58f,
            WidgetType.Display);
        if (streak > 0)
        {
            var streakTop = heroTop + heroHeight;
            var icon = StreakIconUnits * scale;
            var lineHeight = Typography.Measure("Ag", WidgetType.Caption).Y;
            if (streakTop + lineHeight <= labelTop - WidgetMetrics.RowGap * scale)
            {
                ProgressRing.CenterIcon(drawList, new Vector2(content.Min.X + icon * 0.5f, streakTop + lineHeight * 0.5f),
                    FontAwesomeIcon.Fire, accent, icon);
                var text = streakText.IsCurrent(streak)
                    ? streakText.Value
                    : streakText.Store(streak, Loc.T(L.Coin.StreakDays, streak));
                WidgetText.Draw(drawList, new Vector2(content.Min.X + icon + WidgetMetrics.RowGap * 2f * scale,
                    streakTop), text, ink.Secondary, WidgetType.Caption, content.Width * 0.5f);
            }
        }

        var capsuleHeight = CapsuleHeightUnits * scale;
        var capsuleTop = heroTop + (heroHeight - capsuleHeight) * 0.5f;
        if (checkIn)
        {
            var width = MathF.Min(CapsuleWidthUnits * scale, content.Width * 0.4f);
            var rect = new Rect(new Vector2(content.Max.X - width, capsuleTop),
                new Vector2(content.Max.X, capsuleTop + capsuleHeight));
            if (WidgetControls.Button(context, ink, CheckInControl, rect, FontAwesomeIcon.CalendarCheck,
                    Loc.T(L.Coin.CheckIn), AppAccents.For(AppId)) && !coins.CheckingIn)
            {
                checkInPending = true;
                coins.CheckIn();
            }

            return;
        }

        var shopWidth = MathF.Min(ShopWidthUnits * scale, content.Width * 0.3f);
        var shop = new Rect(new Vector2(content.Max.X - shopWidth, capsuleTop),
            new Vector2(content.Max.X, capsuleTop + capsuleHeight));
        Squircle.Fill(drawList, shop.Min, shop.Max, capsuleHeight * 0.5f, ImGui.GetColorU32(ink.Fill));
        WidgetControls.Link(context, ink, ShopControl, shop, WidgetRoute.Tab(AppId, "coin.tab.shop"));
        var label = Loc.T(L.Coin.TabShop);
        var labelSize = Typography.Measure(label, WidgetType.Headline);
        var chevron = StreakIconUnits * 0.8f * scale;
        var total = labelSize.X + WidgetMetrics.RowGap * scale + chevron;
        var left = shop.Center.X - total * 0.5f;
        WidgetText.Draw(drawList, new Vector2(left, shop.Center.Y - labelSize.Y * 0.5f), label, ink.Primary,
            WidgetType.Headline, shop.Width - chevron);
        ProgressRing.CenterIcon(drawList, new Vector2(left + total - chevron * 0.5f, shop.Center.Y),
            FontAwesomeIcon.ChevronRight, ink.Secondary, chevron);
    }

    private static float DrawBalance(in WidgetContext context, in WidgetInk ink, Purse purse, Vector2 position,
        float maxWidth, in TextStyle style)
    {
        var text = WidgetText.Number(ref purse.BalanceText, purse.Balance.Display);
        var heroStyle = new TextStyle(style.Scale * purse.Balance.PopScale, style.Weight);
        var width = WidgetText.TabularWidth(text, heroStyle);
        if (width > maxWidth)
        {
            heroStyle = new TextStyle(heroStyle.Scale * MathF.Max(WidgetType.MinimumFit * 0.8f, maxWidth / width),
                style.Weight);
        }

        WidgetText.Tabular(context.DrawList, position, text, ink.Primary, heroStyle);
        return Typography.Measure("0", style).Y;
    }

    private static void DrawBar(ImDrawListPtr drawList, in WidgetInk ink, Vector4 accent, Rect bar, float fraction) =>
        WidgetChrome.Bar(drawList, bar, fraction <= 0.005f ? 0f : fraction, ink.Fill, accent);

    private string Status(bool paused, long earned, long cap, bool checkIn)
    {
        if (checkIn)
        {
            return Loc.T(L.WidgetsLife.CheckInReady);
        }

        if (paused)
        {
            return Loc.T(L.Coin.PausedTitle);
        }

        if (cap > 0 && earned >= cap)
        {
            return Loc.T(L.Coin.CapReached);
        }

        var key = earned * 1_000_003L + cap;
        return todayText.IsCurrent(key)
            ? todayText.Value
            : todayText.Store(key, Loc.T(L.Coin.CapProgress, NumberText.Group(earned), NumberText.Group(cap)));
    }

    private static void DrawPlaceholder(in WidgetContext context, in WidgetInk ink, Rect content, float headerBottom)
    {
        var scale = context.Scale;
        var heroHeight = Typography.Measure("0", WidgetType.Hero(context.Size)).Y;
        var heroTop = context.Size == WidgetSize.Small
            ? content.Max.Y - heroHeight - WidgetMetrics.Gutter * 3f * scale
            : headerBottom + WidgetMetrics.RowGap * 2f * scale;
        WidgetChrome.Redacted(context.DrawList, new Rect(new Vector2(content.Min.X, heroTop + heroHeight * 0.2f),
            new Vector2(content.Min.X + content.Width * 0.55f, heroTop + heroHeight * 0.8f)), ink);
        var barHeight = BarUnits * scale;
        WidgetChrome.Redacted(context.DrawList, new Rect(new Vector2(content.Min.X, content.Max.Y - barHeight),
            content.Max), ink);
    }

    public void Dispose()
    {
    }
}
