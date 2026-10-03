using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Game;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Timers;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Timers.Widgets;

internal sealed class ResetsWidget : IHomeWidget
{
    private const int Daily = 0;
    private const int GrandCompany = 1;
    private const int Weekly = 2;
    private const int Cactpot = 3;
    private const int Fashion = 4;
    private const int Ocean = 5;
    private const int ResetCount = 3;
    private const int RowCount = 6;
    private const float RingTextWidth = 1.3f;
    private const float RowGlyphUnits = 28f;
    private const float RowGlyphFraction = 0.5f;
    private const float RowWashAlpha = 0.18f;
    private const float ColumnLabelGap = 7f;
    private const float RelevanceSoonMinutes = 10f;
    private const float RelevanceWindowMinutes = 60f;

    private static readonly LocString[] Names =
    {
        L.Timers.DailyReset, L.Timers.GrandCompanyReset, L.Timers.WeeklyReset, L.Timers.JumboCactpot,
        L.Timers.FashionReport, L.Timers.OceanFishing,
    };

    private static readonly FontAwesomeIcon[] Icons =
    {
        FontAwesomeIcon.Sun, FontAwesomeIcon.Flag, FontAwesomeIcon.CalendarAlt, FontAwesomeIcon.Coins,
        FontAwesomeIcon.Tshirt, FontAwesomeIcon.Fish,
    };

    private static readonly Vector4[] Colors =
    {
        Accent.Amber, AccentRing.Orange, Accent.Blue, AccentRing.Gold, Accent.Pink, Accent.Teal,
    };

    private static readonly TimeSpan[] Periods =
    {
        TimeSpan.FromDays(1), TimeSpan.FromDays(1), TimeSpan.FromDays(7), TimeSpan.FromDays(7), TimeSpan.FromDays(7),
        TimeSpan.FromHours(2),
    };

    private readonly CachedText[] countdowns = new CachedText[RowCount];
    private readonly CachedText[] details = new CachedText[RowCount];
    private readonly DateTime[] moments = new DateTime[RowCount];
    private readonly GameTimers timers;

    public ResetsWidget(GameTimers timers)
    {
        this.timers = timers;
    }

    public string Id => "timers.resets";
    public string DisplayName => Loc.T(L.WidgetsTime.Resets);
    public string Description => Loc.T(L.WidgetsTime.ResetsDescription);
    public string AppId => "timers";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium | WidgetSizeSet.Large;

    public float Relevance(string config)
    {
        var utcNow = DateTime.UtcNow;
        var soonest = Soonest(GameSchedule.NextDailyReset(utcNow), GameSchedule.NextGrandCompanyReset(utcNow),
            GameSchedule.NextWeeklyReset(utcNow));
        var minutes = (float)(soonest - utcNow).TotalMinutes;
        if (minutes <= RelevanceSoonMinutes)
        {
            return 0.9f;
        }

        return minutes <= RelevanceWindowMinutes ? 0.6f : 0f;
    }

    public void Draw(in WidgetContext context)
    {
        if (context.Opacity <= 0f)
        {
            return;
        }

        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var utcNow = DateTime.UtcNow;
        moments[Daily] = GameSchedule.NextDailyReset(utcNow);
        moments[GrandCompany] = GameSchedule.NextGrandCompanyReset(utcNow);
        moments[Weekly] = GameSchedule.NextWeeklyReset(utcNow);
        switch (context.Size)
        {
            case WidgetSize.Small:
                DrawSmall(context, ink, utcNow);
                return;
            case WidgetSize.Medium:
                DrawMedium(context, ink, utcNow);
                return;
            default:
                DrawLarge(context, ink, utcNow);
                return;
        }
    }

    private void DrawSmall(in WidgetContext context, in WidgetInk ink, DateTime utcNow)
    {
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        var next = Daily;
        for (var index = 1; index < ResetCount; index++)
        {
            if (moments[index] < moments[next])
            {
                next = index;
            }
        }

        var headerBottom = WidgetChrome.Header(context, ink, AppId, Names[next], Colors[next]);
        var top = headerBottom + WidgetMetrics.Gutter * scale;
        var radius = MathF.Min(content.Width, content.Max.Y - top) * 0.5f;
        DrawRing(context, ink, next, new Vector2(content.Center.X, top + radius), radius, utcNow, true);
    }

    private void DrawMedium(in WidgetContext context, in WidgetInk ink, DateTime utcNow)
    {
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        var drawList = context.DrawList;
        var columnWidth = content.Width / ResetCount;
        var labelHeight = WidgetText.EyebrowHeight();
        var detailHeight = Typography.Measure("A", WidgetType.Caption).Y;
        var labelBlock = ColumnLabelGap * scale + labelHeight + WidgetMetrics.RowGap * scale + detailHeight;
        var radius = MathF.Min(columnWidth * 0.5f - WidgetMetrics.Gutter * scale, (content.Height - labelBlock) * 0.5f);
        var blockTop = content.Min.Y + (content.Height - radius * 2f - labelBlock) * 0.5f;
        for (var index = 0; index < ResetCount; index++)
        {
            var centerX = content.Min.X + columnWidth * (index + 0.5f);
            var center = new Vector2(centerX, blockTop + radius);
            DrawRing(context, ink, index, center, radius, utcNow, false);
            var labelTop = center.Y + radius + ColumnLabelGap * scale;
            var label = Loc.T(Names[index]);
            var maxWidth = columnWidth - WidgetMetrics.RowGap * 2f * scale;
            var labelWidth = MathF.Min(maxWidth, WidgetText.EyebrowWidth(label, scale));
            WidgetText.EyebrowMarquee(drawList, new MarqueeId(context.InstanceKey, index), label,
                new Vector2(centerX - labelWidth * 0.5f, labelTop), labelWidth, ink.Secondary, scale);
            var detail = Detail(index);
            var fitted = WidgetText.Fit(detail, maxWidth, WidgetType.Caption, out var detailScale);
            var detailWidth = Typography.Measure(fitted, detailScale, WidgetType.Caption.Weight).X;
            Typography.Draw(drawList,
                new Vector2(centerX - detailWidth * 0.5f, labelTop + labelHeight + WidgetMetrics.RowGap * scale),
                fitted, ink.Tertiary, detailScale, WidgetType.Caption.Weight);
        }
    }

    private void DrawLarge(in WidgetContext context, in WidgetInk ink, DateTime utcNow)
    {
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        var headerBottom = WidgetChrome.Header(context, ink, AppId, L.WidgetsTime.Resets, AppAccents.For(AppId));
        var listTop = headerBottom + WidgetMetrics.Gutter * scale;
        var rowHeight = (content.Max.Y - listTop) / RowCount;
        var cactpot = GameSchedule.NextJumboCactpot(utcNow, timers.RegionCode);
        var fashion = GameSchedule.FashionReport(utcNow);
        var ocean = GameSchedule.OceanFishing(utcNow, OceanRoute.Indigo);
        moments[Cactpot] = cactpot;
        moments[Fashion] = fashion.NextChangeUtc;
        moments[Ocean] = ocean.NextBoardingUtc;
        for (var index = 0; index < RowCount; index++)
        {
            var top = listTop + index * rowHeight;
            if (index > 0)
            {
                WidgetChrome.Separator(context, ink, content.Min.X + (RowGlyphUnits + WidgetMetrics.Gutter) * scale,
                    content.Max.X, top);
            }

            var row = new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, top + rowHeight));
            var detail = index switch
            {
                Fashion => Loc.T(fashion.Active ? L.Timers.Open : L.Timers.Closed),
                Ocean => ocean.Route,
                _ => Detail(index),
            };
            var boarding = index == Ocean && ocean.BoardingNow;
            DrawRow(context, ink, row, index, detail, boarding, utcNow);
        }
    }

    private void DrawRow(in WidgetContext context, in WidgetInk ink, Rect row, int index, string detail,
        bool boarding, DateTime utcNow)
    {
        var scale = context.Scale;
        var drawList = context.DrawList;
        var glyph = RowGlyphUnits * scale;
        var color = ink.Accent(Colors[index]);
        var glyphCenter = new Vector2(row.Min.X + glyph * 0.5f, row.Center.Y);
        drawList.AddCircleFilled(glyphCenter, glyph * 0.5f, ImGui.GetColorU32(color with { W = color.W * RowWashAlpha }),
            28);
        ProgressRing.CenterIcon(drawList, glyphCenter, Icons[index], color, glyph * RowGlyphFraction);

        var value = boarding ? Loc.T(L.Timers.BoardingNow) : WidgetText.Countdown(ref countdowns[index], moments[index] - utcNow);
        var valueStyle = WidgetType.Headline;
        var valueWidth = boarding ? Typography.Measure(value, valueStyle).X : WidgetText.TabularWidth(value, valueStyle);
        var valueHeight = Typography.Measure(value, valueStyle).Y;
        var valuePosition = new Vector2(row.Max.X - valueWidth, row.Center.Y - valueHeight * 0.5f);
        if (boarding)
        {
            Typography.Draw(drawList, valuePosition, value, color, valueStyle);
        }
        else
        {
            WidgetText.Tabular(drawList, valuePosition, value, ink.Primary, valueStyle);
        }

        var textLeft = row.Min.X + glyph + WidgetMetrics.Gutter * scale;
        var textWidth = MathF.Max(1f, valuePosition.X - WidgetMetrics.Gutter * scale - textLeft);
        var nameHeight = Typography.Measure("A", WidgetType.Headline).Y;
        var detailHeight = Typography.Measure("A", WidgetType.Caption).Y;
        var top = row.Center.Y - (nameHeight + WidgetMetrics.RowGap * 0.5f * scale + detailHeight) * 0.5f;
        WidgetText.Draw(drawList, new Vector2(textLeft, top), Loc.T(Names[index]), ink.Primary, WidgetType.Headline,
            textWidth);
        WidgetText.Draw(drawList, new Vector2(textLeft, top + nameHeight + WidgetMetrics.RowGap * 0.5f * scale), detail,
            ink.Secondary, WidgetType.Caption, textWidth);
    }

    private void DrawRing(in WidgetContext context, in WidgetInk ink, int index, Vector2 center, float radius,
        DateTime utcNow, bool withDetail)
    {
        var scale = context.Scale;
        var drawList = context.DrawList;
        var remaining = moments[index] - utcNow;
        var fraction = 1f - (float)(remaining.TotalSeconds / Periods[index].TotalSeconds);
        var thickness = WidgetChrome.RingThickness(radius, scale);
        var ringRadius = radius - thickness * 0.5f;
        WidgetChrome.Ring(drawList, ink, center, ringRadius, thickness, fraction, Colors[index]);
        var innerWidth = (ringRadius - thickness) * RingTextWidth;
        var text = WidgetText.Countdown(ref countdowns[index], remaining);
        var style = WidgetText.FitStyle(text, WidgetType.Title, innerWidth, true);
        if (!withDetail)
        {
            WidgetText.TabularCentered(drawList, center, text, ink.Primary, style, innerWidth);
            return;
        }

        var detail = Detail(index);
        var textHeight = Typography.Measure(text, style).Y;
        var detailHeight = Typography.Measure(detail, WidgetType.Caption).Y;
        var top = center.Y - (textHeight + detailHeight) * 0.5f;
        WidgetText.TabularCentered(drawList, new Vector2(center.X, top + textHeight * 0.5f), text, ink.Primary,
            style, innerWidth);
        var fitted = WidgetText.Fit(detail, innerWidth, WidgetType.Caption, out var detailScale);
        var detailWidth = Typography.Measure(fitted, detailScale, WidgetType.Caption.Weight).X;
        Typography.Draw(drawList, new Vector2(center.X - detailWidth * 0.5f, top + textHeight), fitted, ink.Secondary,
            detailScale, WidgetType.Caption.Weight);
    }

    private string Detail(int index)
    {
        var local = moments[index].ToLocalTime();
        var key = TimeWidgetParts.DayAndClockKey(local);
        ref var cache = ref details[index];
        return cache.IsCurrent(key) ? cache.Value : cache.Store(key, TimeWidgetParts.DayAndClock(local));
    }

    private static DateTime Soonest(DateTime first, DateTime second, DateTime third)
    {
        var soonest = first < second ? first : second;
        return soonest < third ? soonest : third;
    }

    public void Dispose()
    {
    }
}
