using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notes;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Notes.Widgets;

internal sealed class RemindersWidget : IHomeWidget
{
    private const string AppKey = "notes";
    private const string RemindersTab = "notes.tab.reminders";
    private const int RefreshMilliseconds = 1000;
    private const float CheckRadius = 10f;
    private const double CompletionGraceSeconds = 1.4;
    private const float CompletionFillSeconds = 0.2f;
    private const float RowMinimumHeight = 30f;
    private const float BadgeSize = 28f;
    private const int SampleDueHour = 20;
    private const int MaxRows = 12;
    private static readonly Vector4 OverdueColor = AccentRing.Red;
    private static readonly Comparison<Row> RowOrder = CompareRows;

    private readonly struct Row
    {
        public readonly ReminderItem? Item;
        public readonly LocString Sample;
        public readonly DateTime? Due;
        public readonly int Order;

        public Row(ReminderItem? item, LocString sample, DateTime? due, int order)
        {
            Item = item;
            Sample = sample;
            Due = due;
            Order = order;
        }

        public string Title => Item is null ? Loc.T(Sample) :
            Item.Title.Length > 0 ? Item.Title : Loc.T(L.Notes.ReminderHint);
    }

    private readonly Configuration configuration;
    private readonly List<Row> rows = new();
    private readonly List<Row> sampleRows = new();
    private readonly Dictionary<Guid, double> completedAt = new();
    private readonly Dictionary<Guid, CachedText> dueLabels = new();
    private readonly CachedText[] sampleDue = new CachedText[3];
    private WidgetRefresh refresh;
    private int seenCount = -1;
    private int openCount;
    private int dueTodayCount;
    private int overdueCount;
    private long sampleDay = -1;
    private CachedText countText;
    private CachedText summaryText;

    public RemindersWidget(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public string Id => "notes.reminders";
    public string DisplayName => Loc.T(L.WidgetsUtility.RemindersName);
    public string Description => Loc.T(L.WidgetsUtility.RemindersDescription);
    public string AppId => AppKey;
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium | WidgetSizeSet.Large;

    public WidgetRoute Target(in WidgetContext context) => WidgetRoute.Tab(AppKey, RemindersTab);

    public float Relevance(string config)
    {
        var now = DateTime.Now;
        var reminders = configuration.Reminders;
        var open = false;
        for (var index = 0; index < reminders.Count; index++)
        {
            var reminder = reminders[index];
            if (reminder.Done)
            {
                continue;
            }

            open = true;
            if (reminder.DueAt is { } due && due <= now.AddHours(1))
            {
                return 0.85f;
            }
        }

        return open ? 0.3f : 0f;
    }

    public void Draw(in WidgetContext context)
    {
        Refresh();
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var sample = context.Preview && configuration.Reminders.Count == 0;
        var source = sample ? Samples() : rows;
        var open = sample ? source.Count : openCount;
        if (context.Size == WidgetSize.Small)
        {
            DrawSmall(context, ink, source, open, sample);
            return;
        }

        DrawList(context, ink, source, open);
    }

    private void DrawSmall(in WidgetContext context, in WidgetInk ink, List<Row> source, int open, bool sample)
    {
        var drawList = context.DrawList;
        var scale = context.Scale;
        var content = WidgetMetrics.Content(context);
        var accent = ink.Accent(AppAccents.For(AppKey));
        var badge = BadgeSize * scale;
        var badgeCenter = new Vector2(content.Min.X + badge * 0.5f, content.Min.Y + badge * 0.5f);
        drawList.AddCircleFilled(badgeCenter, badge * 0.5f, ImGui.GetColorU32(accent), 32);
        ProgressRing.CenterIcon(drawList, badgeCenter, FontAwesomeIcon.ListUl, ink.OnAccent, badge * 0.46f);

        var count = WidgetText.Integer(ref countText, open);
        var countWidth = WidgetText.TabularWidth(count, WidgetType.DisplayCompact);
        var countHeight = Typography.Measure(count, WidgetType.DisplayCompact).Y;
        WidgetText.Tabular(drawList, new Vector2(content.Max.X - countWidth, badgeCenter.Y - countHeight * 0.5f),
            count, ink.Primary, WidgetType.DisplayCompact);

        var summary = sample ? source[0].Title : Summary(source);
        var summaryColor = !sample && overdueCount > 0 ? ink.Accent(OverdueColor) : ink.Secondary;
        var summaryHeight = WidgetText.SpacedLineHeight(WidgetType.Caption);
        var titleHeight = WidgetText.SpacedLineHeight(WidgetType.Headline);
        var summaryTop = content.Max.Y - summaryHeight;
        WidgetText.Draw(drawList, new Vector2(content.Min.X, summaryTop), summary, summaryColor,
            WidgetType.Caption, content.Width);
        WidgetText.Draw(drawList, new Vector2(content.Min.X, summaryTop - titleHeight),
            Loc.T(L.WidgetsUtility.RemindersName), accent, WidgetType.Headline, content.Width);
    }

    private void DrawList(in WidgetContext context, in WidgetInk ink, List<Row> source, int open)
    {
        var drawList = context.DrawList;
        var scale = context.Scale;
        var content = WidgetMetrics.Content(context);
        var accentRaw = AppAccents.For(AppKey);
        var accent = ink.Accent(accentRaw);
        var headerBottom = WidgetChrome.Header(context, ink, AppKey, Loc.T(L.WidgetsUtility.RemindersName),
            accentRaw, open > 0 ? WidgetText.Integer(ref countText, open) : string.Empty, accent);
        var body = new Rect(new Vector2(content.Min.X, headerBottom + WidgetMetrics.Gutter * 0.5f * scale),
            content.Max);
        if (source.Count == 0)
        {
            WidgetChrome.Message(context, ink, body, FontAwesomeIcon.CheckCircle, accentRaw,
                Loc.T(L.WidgetsUtility.AllDone), Loc.T(L.WidgetsUtility.AllDoneHint));
            return;
        }

        var titleHeight = WidgetText.SpacedLineHeight(WidgetType.Body);
        var dueHeight = WidgetText.SpacedLineHeight(WidgetType.Caption);
        var rowHeight = MathF.Max(RowMinimumHeight * scale, titleHeight + dueHeight);
        var capacity = Math.Clamp((int)(body.Height / rowHeight), 1, MaxRows);
        var count = Math.Min(capacity, source.Count);
        var radius = CheckRadius * scale;
        var textLeft = body.Min.X + radius * 2f + WidgetMetrics.Gutter * scale;
        var now = ImGui.GetTime();
        var route = WidgetRoute.Tab(AppKey, RemindersTab);
        for (var index = 0; index < count; index++)
        {
            var row = source[index];
            var rowTop = body.Min.Y + index * rowHeight;
            var rowRect = new Rect(new Vector2(body.Min.X, rowTop), new Vector2(body.Max.X, rowTop + rowHeight));
            var linkRect = new Rect(new Vector2(textLeft - WidgetMetrics.RowGap * scale, rowTop), rowRect.Max);
            WidgetControls.Link(context, ink, index * 2, linkRect, route);
            var circleCenter = new Vector2(body.Min.X + radius, rowRect.Center.Y);
            var hit = new Rect(circleCenter - new Vector2(radius * 1.6f), circleCenter + new Vector2(radius * 1.6f));
            var done = row.Item is { Done: true };
            if (WidgetControls.Pressable(context, index * 2 + 1, hit, out var hovered, out var pressScale) &&
                row.Item is { } item)
            {
                Toggle(item, now);
                done = item.Done;
            }

            var fill = done ? Fill(row.Item!, now) : 0f;
            var circleRadius = radius * pressScale * (hovered ? 1.06f : 1f);
            WidgetChrome.CheckCircle(drawList, ink, circleCenter, circleRadius, fill, accentRaw, scale);
            DrawRowText(context, ink, row, textLeft, rowRect, titleHeight, dueHeight, done);
            if (index < count - 1)
            {
                WidgetChrome.Separator(context, ink, textLeft, body.Max.X, rowRect.Max.Y);
            }
        }
    }

    private void DrawRowText(in WidgetContext context, in WidgetInk ink, in Row row, float left, Rect rowRect,
        float titleHeight, float dueHeight, bool done)
    {
        var drawList = context.DrawList;
        var width = rowRect.Max.X - left;
        var title = row.Title;
        if (row.Due is not { } due)
        {
            WidgetText.Draw(drawList, new Vector2(left, rowRect.Center.Y - titleHeight * 0.5f), title,
                done ? ink.Tertiary : ink.Primary, WidgetType.Body, width);
            return;
        }

        var top = rowRect.Center.Y - (titleHeight + dueHeight) * 0.5f;
        WidgetText.Draw(drawList, new Vector2(left, top), title, done ? ink.Tertiary : ink.Primary,
            WidgetType.Body, width);
        var overdue = !done && due < DateTime.Now;
        WidgetText.Draw(drawList, new Vector2(left, top + titleHeight), DueLabel(row, due),
            overdue ? ink.Accent(OverdueColor) : ink.Secondary, WidgetType.Caption, width);
    }

    private void Toggle(ReminderItem item, double now)
    {
        item.Done = !item.Done;
        if (item.Done)
        {
            item.Notified = true;
            completedAt[item.Id] = now;
        }
        else
        {
            completedAt.Remove(item.Id);
        }

        configuration.Save();
        refresh.Expire();
    }

    private float Fill(ReminderItem item, double now)
    {
        if (!completedAt.TryGetValue(item.Id, out var at))
        {
            return 1f;
        }

        return Math.Clamp((float)(now - at) / CompletionFillSeconds, 0f, 1f);
    }

    private void Refresh()
    {
        var reminders = configuration.Reminders;
        if (!refresh.Due(RefreshMilliseconds) && seenCount == reminders.Count)
        {
            return;
        }

        seenCount = reminders.Count;
        rows.Clear();
        openCount = 0;
        dueTodayCount = 0;
        overdueCount = 0;
        var now = DateTime.Now;
        var clock = ImGui.GetTime();
        var today = now.Date;
        for (var index = 0; index < reminders.Count; index++)
        {
            var reminder = reminders[index];
            if (reminder.Done)
            {
                if (completedAt.TryGetValue(reminder.Id, out var at) && clock - at < CompletionGraceSeconds)
                {
                    rows.Add(new Row(reminder, default, reminder.DueAt, index));
                }

                continue;
            }

            openCount++;
            if (reminder.DueAt is { } due)
            {
                if (due < now)
                {
                    overdueCount++;
                }
                else if (due.Date == today)
                {
                    dueTodayCount++;
                }
            }

            rows.Add(new Row(reminder, default, reminder.DueAt, index));
        }

        rows.Sort(RowOrder);
        PruneCompleted(clock);
    }

    private void PruneCompleted(double clock)
    {
        if (completedAt.Count == 0)
        {
            return;
        }

        foreach (var pair in completedAt)
        {
            if (clock - pair.Value >= CompletionGraceSeconds)
            {
                completedAt.Remove(pair.Key);
            }
        }
    }

    private List<Row> Samples()
    {
        var today = DateTime.Today;
        if (sampleDay == today.Ticks && sampleRows.Count > 0)
        {
            return sampleRows;
        }

        sampleDay = today.Ticks;
        sampleRows.Clear();
        var titles = WidgetSamples.Reminders;
        for (var index = 0; index < titles.Length; index++)
        {
            DateTime? due = index == 0 ? today.AddHours(SampleDueHour) : null;
            sampleRows.Add(new Row(null, titles[index], due, index));
        }

        return sampleRows;
    }

    private string Summary(List<Row> source)
    {
        var key = overdueCount * 100_000L + dueTodayCount;
        if (overdueCount + dueTodayCount > 0 && summaryText.IsCurrent(key))
        {
            return summaryText.Value;
        }

        if (overdueCount > 0)
        {
            return summaryText.Store(key, Loc.T(L.WidgetsUtility.Overdue, overdueCount));
        }

        if (dueTodayCount > 0)
        {
            return summaryText.Store(key, Loc.T(L.WidgetsUtility.DueToday, dueTodayCount));
        }

        if (source.Count > 0 && source[0].Item is { Done: false })
        {
            return source[0].Title;
        }

        return Loc.T(L.WidgetsUtility.AllDone);
    }

    private string DueLabel(in Row row, DateTime due)
    {
        if (row.Item is null)
        {
            ref var sampleCache = ref sampleDue[Math.Clamp(row.Order, 0, sampleDue.Length - 1)];
            return Format(ref sampleCache, due);
        }

        ref var cache = ref WidgetCaches.Slot(dueLabels, row.Item.Id);
        return Format(ref cache, due);
    }

    private static string Format(ref CachedText cache, DateTime due)
    {
        var today = DateTime.Today;
        var key = due.Ticks ^ (today.Ticks << 1);
        if (cache.IsCurrent(key))
        {
            return cache.Value;
        }

        if (due.Date == today)
        {
            return cache.Store(key, TimeText.Clock(due));
        }

        var dayLabel = Math.Abs((due.Date - today).TotalDays) < 7
            ? due.ToString("ddd", Loc.Culture)
            : due.ToString("d", Loc.Culture);
        return cache.Store(key, string.Concat(dayLabel, " ", TimeText.Clock(due)));
    }

    private static int CompareRows(Row left, Row right)
    {
        var leftDue = left.Due.HasValue;
        var rightDue = right.Due.HasValue;
        if (leftDue != rightDue)
        {
            return leftDue ? -1 : 1;
        }

        if (leftDue)
        {
            var byDue = left.Due!.Value.CompareTo(right.Due!.Value);
            if (byDue != 0)
            {
                return byDue;
            }
        }

        return left.Order.CompareTo(right.Order);
    }

    public void Dispose()
    {
    }
}
