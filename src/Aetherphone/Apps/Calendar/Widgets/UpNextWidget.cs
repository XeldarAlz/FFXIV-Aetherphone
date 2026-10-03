using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Calendar.Widgets;

internal sealed class UpNextWidget : IHomeWidget
{
    private const int MediumRows = 3;
    private const int LargeRows = 3;
    private const float RowUnits = 34f;
    private const float BarUnits = 3.5f;
    private const float BarGapUnits = 7f;
    private const float DateColumnFraction = 0.34f;
    private const float GridFraction = 0.52f;
    private const int RedactedRows = 2;

    private readonly CalendarWidgetFeed feed;
    private CachedText weekday;
    private CachedText dayNumber;
    private CachedText monthName;
    private CachedText monthTitle;

    public UpNextWidget(CalendarWidgetFeed feed)
    {
        this.feed = feed;
    }

    public string Id => "calendar.upcoming";
    public string DisplayName => Loc.T(L.WidgetsTime.UpNext);
    public string Description => Loc.T(L.WidgetsTime.UpNextDescription);
    public string AppId => "calendar";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium | WidgetSizeSet.Large;

    public float Relevance(string config)
    {
        feed.Update(AppAccents.For(AppId));
        return feed.Relevance();
    }

    public void Draw(in WidgetContext context)
    {
        if (context.Opacity <= 0f)
        {
            return;
        }

        var accent = AppAccents.For(AppId);
        feed.Update(accent);
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var sample = feed.Count == 0 && context.Preview;
        var rows = sample ? feed.Samples(accent) : feed.Upcoming;
        var loading = !sample && feed.IsLoading;
        var now = DateTime.Now;
        switch (context.Size)
        {
            case WidgetSize.Small:
                DrawSmall(context, ink, rows, loading, now, accent);
                return;
            case WidgetSize.Medium:
                DrawMedium(context, ink, rows, loading, now, accent);
                return;
            default:
                DrawLarge(context, ink, rows, loading, sample, now, accent);
                return;
        }
    }

    private void DrawSmall(in WidgetContext context, in WidgetInk ink, ReadOnlySpan<UpcomingEvent> rows,
        bool loading, DateTime now, Vector4 accent)
    {
        var content = WidgetMetrics.Content(context);
        var dateBottom = DrawDate(context, ink, content.Min, now, accent, WidgetType.DisplayCompact);
        var area = new Rect(new Vector2(content.Min.X, dateBottom + WidgetMetrics.Gutter * 0.5f * context.Scale),
            content.Max);
        DrawList(context, ink, area, rows, loading, 2, true);
    }

    private void DrawMedium(in WidgetContext context, in WidgetInk ink, ReadOnlySpan<UpcomingEvent> rows,
        bool loading, DateTime now, Vector4 accent)
    {
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        DrawDate(context, ink, content.Min, now, accent, WidgetType.Display);
        var month = MonthName(now);
        var monthHeight = Typography.Measure(month, WidgetType.Body).Y;
        var columnWidth = content.Width * DateColumnFraction;
        WidgetText.Draw(context.DrawList, new Vector2(content.Min.X, content.Max.Y - monthHeight), month,
            ink.Secondary, WidgetType.Body, columnWidth - WidgetMetrics.Gutter * scale);
        var area = new Rect(new Vector2(content.Min.X + columnWidth, content.Min.Y), content.Max);
        DrawList(context, ink, area, rows, loading, MediumRows, false);
    }

    private void DrawLarge(in WidgetContext context, in WidgetInk ink, ReadOnlySpan<UpcomingEvent> rows,
        bool loading, bool sample, DateTime now, Vector4 accent)
    {
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        var drawList = context.DrawList;
        var title = MonthTitle(now);
        var eyebrowHeight = WidgetText.EyebrowHeight();
        WidgetText.EyebrowFit(drawList, content.Min, title, content.Width, ink.Accent(accent), scale);
        var gridTop = content.Min.Y + eyebrowHeight + WidgetMetrics.Gutter * scale;
        var gridBottom = content.Min.Y + content.Height * GridFraction;
        var mask = sample ? CalendarWidgetFeed.SampleMask(now.Date) : feed.DayMask;
        MonthGrid.Draw(drawList, ink, new Rect(new Vector2(content.Min.X, gridTop), new Vector2(content.Max.X, gridBottom)),
            now.Date, accent, mask, sample ? null : feed, true, scale);

        var separatorY = gridBottom + WidgetMetrics.Gutter * scale;
        WidgetChrome.Separator(context, ink, content.Min.X, content.Max.X, separatorY);
        var labelTop = separatorY + WidgetMetrics.Gutter * scale;
        WidgetText.Eyebrow(drawList, new Vector2(content.Min.X, labelTop), L.WidgetsTime.UpNext, ink.Secondary,
            scale);
        var area = new Rect(new Vector2(content.Min.X, labelTop + eyebrowHeight + WidgetMetrics.RowGap * scale),
            content.Max);
        DrawList(context, ink, area, rows, loading, LargeRows, false);
    }

    private float DrawDate(in WidgetContext context, in WidgetInk ink, Vector2 origin, DateTime now, Vector4 accent,
        in TextStyle numberStyle)
    {
        var scale = context.Scale;
        var drawList = context.DrawList;
        WidgetText.Eyebrow(drawList, origin, Weekday(now), ink.Accent(accent), scale);
        var number = DayNumber(now);
        var numberTop = origin.Y + WidgetText.EyebrowHeight();
        Typography.Draw(drawList, new Vector2(origin.X - WidgetMetrics.RowGap * 0.5f * scale, numberTop), number,
            ink.Primary, numberStyle);
        return numberTop + Typography.Measure(number, numberStyle).Y;
    }

    private void DrawList(in WidgetContext context, in WidgetInk ink, Rect area, ReadOnlySpan<UpcomingEvent> rows,
        bool loading, int maxRows, bool bottomAligned)
    {
        var scale = context.Scale;
        var rowHeight = RowUnits * scale;
        var fit = Math.Max(1, Math.Min(maxRows, (int)(area.Height / rowHeight)));
        if (!bottomAligned)
        {
            rowHeight = MathF.Min(rowHeight * 1.2f, area.Height / maxRows);
        }

        if (loading)
        {
            DrawRedacted(context, ink, area, rowHeight, bottomAligned);
            return;
        }

        if (rows.Length == 0)
        {
            WidgetChrome.Message(context, ink, area, FontAwesomeIcon.CalendarAlt, default, Loc.T(L.Home.NoEvents),
                string.Empty);
            return;
        }

        var showHeading = rows[0].Begin.Date != DateTime.Today && !rows[0].Ongoing;
        var headingHeight = showHeading ? Typography.Measure("A", WidgetType.Caption).Y + WidgetMetrics.RowGap * scale
            : 0f;
        var available = area.Height - headingHeight;
        var count = Math.Min(rows.Length, Math.Max(1, Math.Min(fit, (int)(available / rowHeight))));
        var blockHeight = headingHeight + count * rowHeight;
        var top = bottomAligned ? area.Max.Y - blockHeight : area.Min.Y;
        if (showHeading)
        {
            WidgetText.Draw(context.DrawList, new Vector2(area.Min.X, top), Loc.T(L.WidgetsTime.NoMoreToday),
                ink.Secondary, WidgetType.Caption, area.Width);
            top += headingHeight;
        }

        for (var index = 0; index < count; index++)
        {
            var rowTop = top + index * rowHeight;
            DrawRow(context, ink, new Rect(new Vector2(area.Min.X, rowTop), new Vector2(area.Max.X, rowTop + rowHeight)),
                rows[index], index);
        }
    }

    private static void DrawRow(in WidgetContext context, in WidgetInk ink, Rect row, in UpcomingEvent entry,
        int index)
    {
        var scale = context.Scale;
        var drawList = context.DrawList;
        var nameHeight = Typography.Measure("A", WidgetType.Headline).Y;
        var whenHeight = Typography.Measure("A", WidgetType.Caption).Y;
        var blockHeight = nameHeight + WidgetMetrics.RowGap * 0.5f * scale + whenHeight;
        var top = row.Center.Y - blockHeight * 0.5f;
        var bar = new Rect(new Vector2(row.Min.X, top + WidgetMetrics.RowGap * 0.5f * scale),
            new Vector2(row.Min.X + BarUnits * scale, top + blockHeight - WidgetMetrics.RowGap * 0.5f * scale));
        Squircle.Fill(drawList, bar.Min, bar.Max, bar.Width * 0.5f, ImGui.GetColorU32(ink.Accent(entry.Color)));
        var textLeft = bar.Max.X + BarGapUnits * scale;
        var textWidth = MathF.Max(1f, row.Max.X - textLeft);
        Marquee.DrawLeftAuto(drawList, new MarqueeId(context.InstanceKey, index), entry.Name, textLeft, top,
            textWidth, WidgetType.Headline, ink.Primary);
        WidgetText.Draw(drawList, new Vector2(textLeft, top + nameHeight + WidgetMetrics.RowGap * 0.5f * scale),
            entry.When, ink.Secondary, WidgetType.Caption, textWidth);
    }

    private static void DrawRedacted(in WidgetContext context, in WidgetInk ink, Rect area, float rowHeight,
        bool bottomAligned)
    {
        var scale = context.Scale;
        var barHeight = WidgetMetrics.Gutter * scale;
        var count = Math.Max(1, Math.Min(RedactedRows, (int)(area.Height / rowHeight)));
        var top = bottomAligned ? area.Max.Y - count * rowHeight : area.Min.Y;
        for (var index = 0; index < count; index++)
        {
            var rowTop = top + index * rowHeight + (rowHeight - barHeight * 2.5f) * 0.5f;
            WidgetChrome.Redacted(context.DrawList,
                new Rect(new Vector2(area.Min.X, rowTop), new Vector2(area.Min.X + area.Width * 0.72f, rowTop + barHeight)),
                ink);
            var secondTop = rowTop + barHeight * 1.5f;
            WidgetChrome.Redacted(context.DrawList,
                new Rect(new Vector2(area.Min.X, secondTop),
                    new Vector2(area.Min.X + area.Width * 0.42f, secondTop + barHeight)), ink);
        }
    }

    private string Weekday(DateTime now)
    {
        var key = now.Date.Ticks;
        return weekday.IsCurrent(key) ? weekday.Value : weekday.Store(key, now.ToString("dddd", Loc.Culture));
    }

    private string DayNumber(DateTime now)
    {
        var key = now.Date.Ticks;
        return dayNumber.IsCurrent(key) ? dayNumber.Value : dayNumber.Store(key, now.Day.ToString(Loc.Culture));
    }

    private string MonthName(DateTime now)
    {
        var key = now.Date.Ticks;
        return monthName.IsCurrent(key) ? monthName.Value : monthName.Store(key, now.ToString("MMMM", Loc.Culture));
    }

    private string MonthTitle(DateTime now)
    {
        var key = now.Date.Ticks;
        return monthTitle.IsCurrent(key)
            ? monthTitle.Value
            : monthTitle.Store(key, now.ToString(Loc.Culture.DateTimeFormat.YearMonthPattern, Loc.Culture));
    }

    public void Dispose()
    {
    }
}
