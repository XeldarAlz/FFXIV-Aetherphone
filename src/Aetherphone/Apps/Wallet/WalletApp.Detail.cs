using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Wallet;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Wallet;

internal sealed partial class WalletApp
{
    private const int ChartDays = 30;
    private const int DetailLines = 30;
    private const float DetailDialSize = 88f;
    private const float DetailGap = 12f;
    private const float DetailGlowCoverage = 0.7f;
    private const float DetailGlowStrength = 0.14f;
    private const float ChartHeight = 110f;
    private const float WeeklyBarHeight = 8f;
    private const float WeeklyBarTrackAlpha = 0.12f;

    private readonly long[] chartValues = new long[ChartDays];
    private RollingValue detailRoll;
    private Spring detailFill;
    private int chartRevision = -1;
    private uint chartItem;
    private int chartDay;
    private int chartFilled;
    private CachedText chartTitle;
    private CachedText chartNet;

    private void PrimeDetail(uint itemId)
    {
        detailRoll = default;
        detailFill.SnapTo(0f);
        chartRevision = -1;
        chartItem = itemId;
    }

    private void DrawDetail(Rect area, uint itemId, int depth)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        var backTitle = router.TryGetView(depth - 2, out var previous) && previous.Kind == WalletViewKind.Activity
            ? Loc.T(L.Wallet.ActivityTitle)
            : DisplayName;
        if (!wallet.TryGetEntry(itemId, out var entry))
        {
            DrawSignedOut(navBar.Body);
            AppHeader.EndLargeTitle(in navBar, context, "wallet.detail.nav", DisplayName, NavBarStyle.From(ui),
                ReadOnlySpan<NavBarButton>.Empty, backTitle, back);
            return;
        }

        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawDetailHero(drawList, origin, width, entry, scale);
            cursorY = DrawDetailWeekly(drawList, new Vector2(origin.X, cursorY), width, entry, scale);
            cursorY = DrawChart(drawList, new Vector2(origin.X, cursorY), width, entry, scale);
            cursorY = DrawDetailActivity(drawList, new Vector2(origin.X, cursorY), width, entry, scale);
            ReserveTo(origin, width, cursorY + BottomPad * scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "wallet.detail.nav", entry.Name, NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, backTitle, back);
    }

    private float DrawDetailHero(ImDrawListPtr drawList, Vector2 origin, float width, WalletEntry entry, float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var gap = DetailGap * scale;
        var dialSize = DetailDialSize * scale;
        var amountStyle = TextStyles.WidgetDisplayCompact;
        var amountHeight = Typography.LineHeight(amountStyle);
        var lineHeight = Typography.LineHeight(TextStyles.Subheadline);
        var capLine = text.CapDetail(entry);
        var summary = text.Summary(wallet, entry.ItemId);
        var lines = (capLine.Length > 0 ? 1 : 0) + 1;
        var height = pad * 2f + dialSize + gap + amountHeight + gap * 0.5f +
                     lines * (lineHeight + WalletArt.LineGap * scale);
        var max = new Vector2(origin.X + width, origin.Y + height);
        var level = entry.Level;
        var tint = WalletArt.LevelInk(level, ui.Accent);
        var radius = Metrics.Radius.Widget * scale;
        ui.Card(drawList, origin, max, radius, true);
        Material.TopGlow(drawList, origin, max, radius, tint, DetailGlowCoverage, DetailGlowStrength);

        var centerX = origin.X + width * 0.5f;
        var dialCenter = new Vector2(centerX, origin.Y + pad + dialSize * 0.5f);
        var fraction = Math.Clamp(Step(ref detailFill, entry.Fraction), 0f, 1f);
        WalletArt.Dial(drawList, textures, entry.IconId, dialCenter, dialSize, fraction, tint, level != CapLevel.None,
            WalletArt.Backing(ui.TitleInk), scale);

        var target = (int)Math.Clamp(entry.Amount, 0, int.MaxValue);
        detailRoll.Update(target, MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
        var amount = NumberText.Group(detailRoll.Display);
        var amountTop = dialCenter.Y + dialSize * 0.5f + gap;
        WidgetText.TabularCentered(drawList, new Vector2(centerX, amountTop + amountHeight * 0.5f), amount,
            ui.TitleInk, amountStyle, width - pad * 2f);

        var lineTop = amountTop + amountHeight + gap * 0.5f;
        if (capLine.Length > 0)
        {
            DrawCenteredLine(drawList, centerX, lineTop, width - pad * 2f, capLine,
                WalletMath.NeedsAttention(level) ? WalletArt.GoldInk : ui.BodyInk);
            lineTop += lineHeight + WalletArt.LineGap * scale;
        }

        DrawCenteredLine(drawList, centerX, lineTop, width - pad * 2f, summary, ui.MutedInk);
        return max.Y;
    }

    private static void DrawCenteredLine(ImDrawListPtr drawList, float centerX, float top, float width, string line,
        Vector4 ink)
    {
        var fitted = Typography.FitText(line, width, TextStyles.Subheadline);
        var size = Typography.Measure(fitted, TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(centerX - size.X * 0.5f, top), fitted, ink, TextStyles.Subheadline);
    }

    private float DrawDetailWeekly(ImDrawListPtr drawList, Vector2 origin, float width, WalletEntry entry,
        float scale)
    {
        if (!entry.HasWeeklyCap)
        {
            return origin.Y;
        }

        var top = origin.Y + Metrics.Space.Md * scale;
        var pad = Metrics.Space.Lg * scale;
        var eyebrowHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var headlineHeight = Typography.LineHeight(TextStyles.Headline);
        var statusHeight = Typography.LineHeight(TextStyles.Subheadline);
        var barHeight = WeeklyBarHeight * scale;
        var gap = Metrics.Space.Sm * scale;
        var height = pad * 2f + eyebrowHeight + headlineHeight + gap + barHeight + gap + statusHeight;
        var min = new Vector2(origin.X, top);
        var max = new Vector2(origin.X + width, top + height);
        WalletArt.Card(drawList, ui, min, max, scale);
        var done = entry.WeeklyAmount >= entry.WeeklyCap;
        var tint = done ? WalletArt.GoldInk : ui.Accent;
        var left = min.X + pad;
        var right = max.X - pad;
        var lineTop = min.Y + pad;
        Typography.Draw(drawList, new Vector2(left, lineTop),
            Typography.FitText(Loc.T(L.Wallet.WeeklyLimit), right - left, TextStyles.FootnoteEmphasized), ui.MutedInk,
            TextStyles.FootnoteEmphasized);
        lineTop += eyebrowHeight;
        Typography.Draw(drawList, new Vector2(left, lineTop),
            Typography.FitText(entry.WeeklyCapText, right - left, TextStyles.Headline), ui.TitleInk,
            TextStyles.Headline);
        lineTop += headlineHeight + gap;
        WalletArt.Bar(drawList, new Vector2(left, lineTop), new Vector2(right, lineTop + barHeight),
            WalletMath.Fraction(entry.WeeklyAmount, entry.WeeklyCap), Palette.WithAlpha(ui.TitleInk,
                WeeklyBarTrackAlpha), tint);
        lineTop += barHeight + gap;
        Typography.Draw(drawList, new Vector2(left, lineTop),
            Typography.FitText(WeeklyStatus(entry, done), right - left, TextStyles.Subheadline),
            done ? WalletArt.GoldInk : ui.MutedInk, TextStyles.Subheadline);
        return max.Y;
    }

    private float DrawChart(ImDrawListPtr drawList, Vector2 origin, float width, WalletEntry entry, float scale)
    {
        SyncChart(entry.ItemId);
        var top = origin.Y + Metrics.Space.Md * scale;
        var pad = Metrics.Space.Lg * scale;
        var headlineHeight = Typography.LineHeight(TextStyles.Headline);
        var hasChart = chartFilled >= 2;
        var emptyText = Loc.T(L.Wallet.ChartEmpty);
        var bodyHeight = hasChart
            ? ChartHeight * scale
            : Typography.MeasureWrappedBlock(emptyText, TextStyles.Subheadline, width - pad * 2f).Y;
        var height = pad * 2f + headlineHeight + Metrics.Space.Md * scale + bodyHeight;
        var min = new Vector2(origin.X, top);
        var max = new Vector2(origin.X + width, top + height);
        if (!ImGui.IsRectVisible(min, max))
        {
            return max.Y;
        }

        WalletArt.Card(drawList, ui, min, max, scale);
        var left = min.X + pad;
        var right = max.X - pad;
        var netWidth = 0f;
        if (hasChart)
        {
            var net = chartValues[chartFilled - 1] - chartValues[0];
            var netText = ChartNet(net);
            netWidth = WidgetText.TabularWidth(netText, TextStyles.Headline) + WalletArt.ValueGap * scale;
            WidgetText.TabularRight(drawList, right, min.Y + pad, netText, net > 0 ? WalletArt.GainInk : ui.MutedInk,
                TextStyles.Headline);
        }

        Typography.Draw(drawList, new Vector2(left, min.Y + pad),
            Typography.FitText(ChartTitle(), MathF.Max(1f, right - left - netWidth), TextStyles.Headline),
            ui.TitleInk, TextStyles.Headline);
        var bodyTop = min.Y + pad + headlineHeight + Metrics.Space.Md * scale;
        if (!hasChart)
        {
            Typography.DrawWrappedLeft(new Vector2(left, bodyTop), emptyText, ui.MutedInk, TextStyles.Subheadline,
                right - left);
            return max.Y;
        }

        WalletArt.Chart(drawList, new Rect(new Vector2(left, bodyTop), new Vector2(right, bodyTop + ChartHeight * scale)),
            chartValues.AsSpan(0, chartFilled), WalletArt.LevelInk(entry.Level, ui.Accent), ui.TitleInk, scale);
        return max.Y;
    }

    private void SyncChart(uint itemId)
    {
        var today = WalletText.Today();
        if (chartRevision == wallet.HistoryRevision && chartItem == itemId && chartDay == today)
        {
            return;
        }

        chartRevision = wallet.HistoryRevision;
        chartItem = itemId;
        chartDay = today;
        chartFilled = WalletJournal.Series(wallet.History, itemId, today, chartValues);
    }

    private string ChartTitle()
    {
        var key = chartFilled >= ChartDays ? -1L : (long)chartDay - chartFilled + 1;
        if (chartTitle.IsCurrent(key))
        {
            return chartTitle.Value;
        }

        if (key < 0)
        {
            return chartTitle.Store(key, Loc.T(L.Wallet.ChartLastMonth));
        }

        var first = DateOnly.FromDayNumber((int)key).ToDateTime(TimeOnly.MinValue);
        var unix = new DateTimeOffset(first).ToUnixTimeSeconds();
        return chartTitle.Store(key, Loc.T(L.Wallet.ChartSince, TimeText.MonthDay(unix)));
    }

    private string ChartNet(long net) =>
        chartNet.IsCurrent(net) ? chartNet.Value : chartNet.Store(net, WalletText.Signed(net));

    private float DrawDetailActivity(ImDrawListPtr drawList, Vector2 origin, float width, WalletEntry entry,
        float scale)
    {
        var cursorY = origin.Y + WalletArt.SectionGap * scale;
        cursorY += DrawSectionTitle(drawList, new Vector2(origin.X, cursorY), width, Loc.T(L.Wallet.RecentTitle),
            string.Empty, out _, scale);
        cursorY += WalletArt.HeaderGap * scale;
        var count = 0;
        var first = -1;
        for (var index = 0; index < text.LineCount && count < DetailLines; index++)
        {
            if (text.Line(index).ItemId != entry.ItemId)
            {
                continue;
            }

            if (first < 0)
            {
                first = index;
            }

            count++;
        }

        if (count == 0)
        {
            var title = Loc.T(L.Wallet.RecentEmptyTitle);
            var body = Loc.T(L.Wallet.RecentEmptyBody);
            var height = WalletArt.PanelHeight(title, body, width, scale);
            WalletArt.Panel(drawList, ui, new Vector2(origin.X, cursorY), width, height, FontAwesomeIcon.Receipt, title,
                body, scale);
            return cursorY + height;
        }

        return DrawLinesCard(drawList, new Vector2(origin.X, cursorY), width, first, count, entry.ItemId, true, false,
            scale);
    }
}
