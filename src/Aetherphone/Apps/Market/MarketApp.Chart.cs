using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Market;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Market;

internal sealed partial class MarketApp
{
    private const float ChartHeaderHeight = 48f;
    private const float ChartHeight = 128f;
    private const float VolumeHeight = 24f;
    private const float VolumeGap = 6f;
    private const float AxisRowHeight = 20f;
    private const float RangeStripWidth = 138f;
    private const float RangeStripHeight = 28f;
    private const float ChartLine = 2.2f;
    private const float ChartDot = 3.8f;
    private const float ChartFillAlpha = 0.24f;
    private const float ChartPadding = 0.08f;
    private const float VolumeAlpha = 0.28f;
    private const float VolumeBarFraction = 0.62f;
    private const float AxisLabelGap = 8f;
    private const float ScrubLineAlpha = 0.45f;
    private const float GridAlpha = 0.10f;
    private const float ScrubDotHalo = 1.3f;
    private const int RecomputeSeconds = 60;
    private const int MaxBuckets = 30;

    private readonly float[] chartMedians = new float[MaxBuckets];
    private readonly int[] chartVolumes = new int[MaxBuckets];
    private readonly string[] rangeLabels = new string[3];
    private long[] chartScratch = new long[512];
    private MarketRange chartRange = MarketRange.Week;
    private MarketHistory? chartSource;
    private bool chartHq;
    private MarketRange chartComputedRange;
    private long chartComputedAt;
    private long chartFrom;
    private long chartTo;
    private int chartBuckets;
    private int chartFilled;
    private int chartRevision;
    private long chartMedianWeek;
    private long chartMedianRange;
    private int chartUnits;
    private float chartLow;
    private float chartHigh;
    private int maxVolume;
    private bool chartLoading;
    private CachedText lastLabel;
    private CachedText scrubLine;
    private CachedText axisStart;
    private CachedText lowLabel;
    private CachedText highLabel;

    private string RangeLabel(MarketRange range) => range switch
    {
        MarketRange.Day => Loc.T(L.Market.RangeDay),
        MarketRange.Week => Loc.T(L.Market.RangeWeek),
        _ => Loc.T(L.Market.RangeMonth),
    };

    private void SyncChart(MarketHistory? history, bool hq)
    {
        chartLoading = history is null;
        if (history is null)
        {
            chartFilled = 0;
            chartMedianWeek = 0;
            chartMedianRange = 0;
            chartUnits = 0;
            chartSource = null;
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (ReferenceEquals(history, chartSource) && hq == chartHq && chartRange == chartComputedRange &&
            now - chartComputedAt < RecomputeSeconds)
        {
            return;
        }

        chartSource = history;
        chartHq = hq;
        chartComputedRange = chartRange;
        chartComputedAt = now;
        chartRevision++;
        var trades = history.Trades;
        if (chartScratch.Length < trades.Length)
        {
            chartScratch = new long[Math.Max(trades.Length, chartScratch.Length * 2)];
        }

        chartBuckets = MarketTrend.Buckets(chartRange);
        chartTo = now;
        chartFrom = now - MarketTrend.Seconds(chartRange);
        chartFilled = MarketTrend.Bucket(trades, hq, chartFrom, chartTo, chartMedians.AsSpan(0, chartBuckets),
            chartVolumes.AsSpan(0, chartBuckets), chartScratch);
        chartMedianWeek = MarketTrend.MedianPrice(trades, hq, now - MarketTrend.WatchSeconds, chartScratch);
        chartMedianRange = MarketTrend.MedianPrice(trades, hq, chartFrom, chartScratch);
        chartUnits = 0;
        maxVolume = 0;
        chartLow = float.MaxValue;
        chartHigh = 0f;
        for (var bucketIndex = 0; bucketIndex < chartBuckets; bucketIndex++)
        {
            chartUnits += chartVolumes[bucketIndex];
            maxVolume = Math.Max(maxVolume, chartVolumes[bucketIndex]);
            chartLow = MathF.Min(chartLow, chartMedians[bucketIndex]);
            chartHigh = MathF.Max(chartHigh, chartMedians[bucketIndex]);
        }
    }

    private float DrawChart(ImDrawListPtr drawList, Vector2 origin, float width, MarketHistoryEntry entry,
        float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var hasLine = chartFilled >= 2;
        var height = pad * 2f + ChartHeaderHeight * scale + ChartHeight * scale +
                     (hasLine ? (VolumeGap + VolumeHeight + AxisRowHeight) * scale : 0f);
        var max = new Vector2(origin.X + width, origin.Y + height);
        UiAnchors.Report("market.chart", new Rect(origin, max));
        if (!ImGui.IsRectVisible(origin, max))
        {
            return max.Y;
        }

        MarketArt.Card(drawList, ui, origin, max, scale);
        var left = origin.X + pad;
        var right = max.X - pad;
        var headerTop = origin.Y + pad;
        var stripWidth = MathF.Min(RangeStripWidth * scale, (right - left) * 0.5f);
        var strip = new Rect(new Vector2(right - stripWidth, headerTop),
            new Vector2(right, headerTop + RangeStripHeight * scale));
        rangeLabels[0] = Loc.T(L.Market.RangeDay);
        rangeLabels[1] = Loc.T(L.Market.RangeWeek);
        rangeLabels[2] = Loc.T(L.Market.RangeMonth);
        var selected = SegmentStrip.Draw("market.range", strip, rangeLabels, (int)chartRange, ui.Palette,
            RangeStripHeight);
        if (selected != (int)chartRange)
        {
            UiFeedback.Play(UiSound.Tap);
            chartRange = (MarketRange)selected;
            chartComputedAt = 0;
        }

        var plotTop = headerTop + ChartHeaderHeight * scale;
        var plot = new Rect(new Vector2(left, plotTop), new Vector2(right, plotTop + ChartHeight * scale));
        var headerWidth = MathF.Max(1f, strip.Min.X - left - MarketArt.ValueGap * scale);
        if (!hasLine)
        {
            DrawChartHeader(drawList, left, headerTop, headerWidth, Loc.T(L.Market.ChartTitle), string.Empty,
                ui.MutedInk);
            DrawChartEmpty(drawList, plot, entry, scale);
            return max.Y;
        }

        var lowText = ChartPrice(ref lowLabel, chartLow);
        var highText = ChartPrice(ref highLabel, chartHigh);
        var labelWidth = MathF.Max(WidgetText.TabularWidth(lowText, TextStyles.Footnote),
            WidgetText.TabularWidth(highText, TextStyles.Footnote));
        var lineRect = new Rect(plot.Min, new Vector2(plot.Max.X - labelWidth - AxisLabelGap * scale, plot.Max.Y));
        var count = chartBuckets;
        var stepX = lineRect.Width / (count - 1);
        var spread = MathF.Max(1f, chartHigh - chartLow);
        var low = chartLow - spread * ChartPadding;
        var high = chartHigh + spread * ChartPadding;
        Span<Vector2> points = stackalloc Vector2[MaxBuckets];
        for (var bucketIndex = 0; bucketIndex < count; bucketIndex++)
        {
            var normalized = (chartMedians[bucketIndex] - low) / (high - low);
            points[bucketIndex] = new Vector2(lineRect.Min.X + stepX * bucketIndex,
                lineRect.Max.Y - normalized * lineRect.Height);
        }

        var change = MarketTrend.Change((long)chartMedians[count - 1], (long)chartMedians[0]);
        var ink = MarketArt.TrendInk(theme, change, ui.Accent);
        WidgetText.TabularRight(drawList, plot.Max.X, lineRect.Min.Y, highText, ui.MutedInk, TextStyles.Footnote);
        WidgetText.TabularRight(drawList, plot.Max.X, lineRect.Max.Y - Typography.LineHeight(TextStyles.Footnote),
            lowText, ui.MutedInk, TextStyles.Footnote);
        MarketArt.DashedLine(drawList, lineRect.Min.X, lineRect.Max.X, lineRect.Min.Y,
            Palette.WithAlpha(ui.TitleInk, GridAlpha), scale);
        MarketArt.DashedLine(drawList, lineRect.Min.X, lineRect.Max.X, lineRect.Max.Y,
            Palette.WithAlpha(ui.TitleInk, GridAlpha), scale);
        MarketArt.GradientFill(drawList, points[..count], lineRect.Max.Y, ink with { W = ChartFillAlpha },
            ink with { W = 0f });
        var lineColor = ImGui.GetColorU32(ink);
        for (var bucketIndex = 0; bucketIndex < count - 1; bucketIndex++)
        {
            drawList.AddLine(points[bucketIndex], points[bucketIndex + 1], lineColor, ChartLine * scale);
        }

        var volumeTop = plot.Max.Y + VolumeGap * scale;
        var volumeRect = new Rect(new Vector2(lineRect.Min.X, volumeTop),
            new Vector2(lineRect.Max.X, volumeTop + VolumeHeight * scale));
        var hoverRect = new Rect(new Vector2(lineRect.Min.X - stepX * 0.5f, plot.Min.Y),
            new Vector2(lineRect.Max.X + stepX * 0.5f, volumeRect.Max.Y));
        var scrubbing = UiInteract.Hover(hoverRect.Min, hoverRect.Max);
        var scrubIndex = -1;
        if (scrubbing)
        {
            scrubIndex = Math.Clamp((int)MathF.Round((ImGui.GetMousePos().X - lineRect.Min.X) / stepX), 0, count - 1);
        }

        DrawVolumes(drawList, volumeRect, stepX, count, scrubIndex, ink, scale);
        var axisTop = volumeRect.Max.Y + Metrics.Space.Xxs * scale;
        Typography.Draw(drawList, new Vector2(lineRect.Min.X, axisTop), AxisStart(), ui.MutedInk,
            TextStyles.Footnote);
        var nowText = Loc.T(L.Market.Now);
        Typography.Draw(drawList,
            new Vector2(lineRect.Max.X - Typography.Measure(nowText, TextStyles.Footnote).X, axisTop), nowText,
            ui.MutedInk, TextStyles.Footnote);
        if (scrubIndex < 0)
        {
            drawList.AddCircleFilled(points[count - 1], ChartDot * scale, lineColor, 16);
            var title = ChartPrice(ref lastLabel, chartMedians[count - 1]);
            DrawChartHeader(drawList, left, headerTop, headerWidth, title, ChangeLine(change), ink);
            return max.Y;
        }

        var point = points[scrubIndex];
        drawList.AddLine(new Vector2(point.X, plot.Min.Y), new Vector2(point.X, volumeRect.Max.Y),
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, ScrubLineAlpha)), Metrics.Stroke.Thin * scale);
        drawList.AddCircleFilled(point, ChartDot * scale * ScrubDotHalo, ImGui.GetColorU32(ui.TitleInk), 16);
        drawList.AddCircleFilled(point, ChartDot * scale, lineColor, 16);
        DrawChartHeader(drawList, left, headerTop, headerWidth,
            MarketText.Price((long)chartMedians[scrubIndex]), ScrubLine(scrubIndex), ui.MutedInk);
        return max.Y;
    }

    private void DrawChartHeader(ImDrawListPtr drawList, float left, float top, float width, string title,
        string subtitle, Vector4 subtitleInk)
    {
        var fitted = WidgetText.FitStyle(title, TextStyles.Title3, width, true);
        WidgetText.Tabular(drawList, new Vector2(left, top), title, ui.TitleInk, fitted);
        if (subtitle.Length == 0)
        {
            return;
        }

        Typography.Draw(drawList, new Vector2(left, top + Typography.LineHeight(TextStyles.Title3)),
            Typography.FitText(subtitle, width, TextStyles.FootnoteEmphasized), subtitleInk,
            TextStyles.FootnoteEmphasized);
    }

    private void DrawChartEmpty(ImDrawListPtr drawList, Rect plot, MarketHistoryEntry entry, float scale)
    {
        if (chartLoading && entry.State is MarketState.Loading or MarketState.Idle)
        {
            Skeleton.Bar(drawList, plot.Min, plot.Max, Metrics.Radius.Md * scale);
            return;
        }

        var message = entry.State == MarketState.Failed
            ? Loc.T(L.Market.HistoryUnavailable)
            : Loc.T(L.Market.NotEnoughSales);
        Typography.DrawWrappedCentered(drawList, message, TextStyles.Subheadline, ui.MutedInk,
            new Vector2(plot.Center.X, plot.Center.Y - Typography.LineHeight(TextStyles.Subheadline)),
            plot.Width - Metrics.Space.Lg * 2f * scale);
    }

    private void DrawVolumes(ImDrawListPtr drawList, Rect area, float stepX, int count, int scrubIndex, Vector4 ink,
        float scale)
    {
        if (maxVolume <= 0)
        {
            return;
        }

        var barWidth = MathF.Max(Metrics.Stroke.Ring * scale, stepX * VolumeBarFraction);
        var rest = ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, VolumeAlpha));
        var active = ImGui.GetColorU32(ink);
        for (var bucketIndex = 0; bucketIndex < count; bucketIndex++)
        {
            var volume = chartVolumes[bucketIndex];
            if (volume <= 0)
            {
                continue;
            }

            var centerX = area.Min.X + stepX * bucketIndex;
            var barHeight = MathF.Max(Metrics.Stroke.Ring * scale, area.Height * volume / maxVolume);
            var min = new Vector2(MathF.Max(area.Min.X, centerX - barWidth * 0.5f), area.Max.Y - barHeight);
            var max = new Vector2(MathF.Min(area.Max.X, centerX + barWidth * 0.5f), area.Max.Y);
            drawList.AddRectFilled(min, max, bucketIndex == scrubIndex ? active : rest, barWidth * 0.25f);
        }
    }

    private static string ChartPrice(ref CachedText cache, float value)
    {
        var rounded = (long)MathF.Round(value);
        return cache.IsCurrent(rounded) ? cache.Value : cache.Store(rounded, MarketText.Price(rounded));
    }

    private string ChangeLine(double change)
    {
        var key = ((long)chartRevision << 8) | (byte)chartRange;
        if (scrubLine.IsCurrent(-key - 1))
        {
            return scrubLine.Value;
        }

        return scrubLine.Store(-key - 1, Loc.T(L.Market.ChartChange, MarketText.Change(change), RangeLabel(chartRange)));
    }

    private string ScrubLine(int bucketIndex)
    {
        var key = ((long)chartRevision << 16) | ((long)bucketIndex << 2) | (byte)chartRange;
        if (scrubLine.IsCurrent(key))
        {
            return scrubLine.Value;
        }

        var start = chartFrom + (chartTo - chartFrom) * bucketIndex / chartBuckets;
        var moment = chartRange switch
        {
            MarketRange.Day => TimeText.Clock(start),
            MarketRange.Week => string.Concat(TimeText.MonthDay(start), " ", TimeText.Clock(start)),
            _ => TimeText.MonthDay(start),
        };
        return scrubLine.Store(key, Loc.T(L.Market.ScrubSold, moment, MarketFormat.Gil(chartVolumes[bucketIndex])));
    }

    private string AxisStart()
    {
        var key = ((long)chartRevision << 2) | (byte)chartRange;
        if (axisStart.IsCurrent(key))
        {
            return axisStart.Value;
        }

        var text = chartRange == MarketRange.Day ? TimeText.Clock(chartFrom) : TimeText.MonthDay(chartFrom);
        return axisStart.Store(key, text);
    }
}
