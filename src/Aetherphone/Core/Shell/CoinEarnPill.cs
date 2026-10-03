using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell;

internal sealed class CoinEarnPill : IDisposable
{
    private const float TopGap = 12f;
    private const float SideMargin = 14f;
    private const float PadX = 13f;
    private const float PadY = 7f;
    private const float IconGap = 8f;
    private const float SlideDistance = 10f;
    private const float HoldSeconds = 3.2f;
    private const float MinIntervalSeconds = 8f;
    private const float RingSeconds = 0.8f;
    private const float RingGrowth = 26f;
    private const float MinTitleWidth = 40f;

    private readonly CoinStore store;
    private readonly Configuration configuration;
    private Spring presence;
    private long pendingDelta;
    private long shownDelta;
    private float holdClock;
    private float sinceLastShow = MinIntervalSeconds;
    private float ringClock;

    public CoinEarnPill(CoinStore store, Configuration configuration)
    {
        this.store = store;
        this.configuration = configuration;
        store.Earned += OnEarned;
    }

    public bool IsVisible => holdClock > 0f || presence.Value > 0.02f;

    public void Draw(Rect screen, PhoneTheme theme, float delta, bool suppressed)
    {
        sinceLastShow += delta;
        if (holdClock > 0f)
        {
            holdClock -= delta;
        }

        if (ringClock > 0f)
        {
            ringClock -= delta;
        }

        if (!suppressed && !configuration.DoNotDisturb && holdClock <= 0f
            && sinceLastShow >= MinIntervalSeconds && Interlocked.Read(ref pendingDelta) > 0)
        {
            shownDelta = Interlocked.Exchange(ref pendingDelta, 0);
            holdClock = HoldSeconds;
            ringClock = RingSeconds;
            sinceLastShow = 0f;
        }

        var wanted = !suppressed && holdClock > 0f;
        presence.Step(wanted ? 1f : 0f, Motion.Appear, delta);
        var alpha = Math.Clamp(presence.Value, 0f, 1f);
        if (alpha < 0.02f)
        {
            return;
        }

        DrawPill(screen, theme, alpha);
    }

    private void DrawPill(Rect screen, PhoneTheme theme, float alpha)
    {
        var scale = UiScale.Current;
        var accent = AppAccents.For("coin");
        var title = Loc.T(L.Coin.RollupTitle, NumberText.Group(shownDelta));

        var titleHeight = Typography.Measure(title, TextStyles.FootnoteEmphasized).Y;
        var iconSize = titleHeight * 0.98f;
        var fixedWidth = PadX * 2f * scale + iconSize + IconGap * scale;
        var available = screen.Width - SideMargin * 2f * scale;
        var fitted = Typography.FitText(title, MathF.Max(MinTitleWidth * scale, available - fixedWidth),
            TextStyles.FootnoteEmphasized);
        var titleWidth = Typography.Measure(fitted, TextStyles.FootnoteEmphasized).X;
        var width = MathF.Min(fixedWidth + titleWidth, available);
        var height = titleHeight + PadY * 2f * scale;

        var island = StatusBar.BaseIsland(screen);
        var top = island.Max.Y + TopGap * scale - SlideDistance * scale * (1f - alpha);
        var bounds = new Rect(new Vector2(screen.Center.X - width * 0.5f, top),
            new Vector2(screen.Center.X + width * 0.5f, top + height));
        var rounding = height * 0.5f;
        var drawList = ImGui.GetForegroundDrawList();
        drawList.PushClipRect(screen.Min, screen.Max, true);
        Elevation.Draw(drawList, bounds.Min, bounds.Max, rounding, scale, 4f, 2f, 0.18f * alpha);
        drawList.AddRectFilled(bounds.Min, bounds.Max,
            ImGui.GetColorU32(Palette.WithAlpha(theme.Glass, alpha * theme.Glass.W)), rounding);
        drawList.AddRect(bounds.Min, bounds.Max, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.42f * alpha)), rounding,
            ImDrawFlags.RoundCornersAll, Metrics.Stroke.Thin * scale);

        var iconCenter = new Vector2(bounds.Min.X + PadX * scale + iconSize * 0.5f, bounds.Center.Y);
        if (ringClock > 0f)
        {
            var eased = Easing.Clamp01(1f - ringClock / RingSeconds);
            var radius = iconSize * 0.5f + eased * RingGrowth * scale;
            drawList.AddCircle(iconCenter, radius,
                ImGui.GetColorU32(Palette.WithAlpha(accent, (1f - eased) * 0.55f * alpha)), 32,
                Metrics.Stroke.Thin * scale);
        }

        CurrencyGlyph.Draw(drawList, CurrencyKind.Coins, iconCenter, iconSize, alpha);
        var titleLeft = iconCenter.X + iconSize * 0.5f + IconGap * scale;
        Typography.Draw(drawList, new Vector2(titleLeft, bounds.Center.Y - titleHeight * 0.5f), fitted,
            Palette.WithAlpha(ChromeInk.TextStrong, alpha), TextStyles.FootnoteEmphasized);
        drawList.PopClipRect();
    }

    private void OnEarned(long delta, CoinLedgerEntryDto[] entries)
    {
        if (delta > 0)
        {
            Interlocked.Add(ref pendingDelta, delta);
        }
    }

    public void Dispose()
    {
        store.Earned -= OnEarned;
    }
}
