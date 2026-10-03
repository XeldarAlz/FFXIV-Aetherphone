using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Clock;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Interface;

namespace Aetherphone.Apps.Clock.Widgets;

internal sealed class AlarmWidget : IHomeWidget
{
    private const string AlarmsRoute = "clock.tab.alarms";
    private const int MediumRows = 3;
    private const int StopControl = 1;
    private const int SnoozeControl = 2;
    private const int ToggleControlBase = 10;
    private const float ToggleUnits = 30f;
    private const float CapsuleUnits = 32f;
    private const float RelevanceWindowMinutes = 60f;
    private const byte EveryDay = 0x7F;
    private const byte Weekdays = 0x3E;

    private struct AlarmRow
    {
        public int Hour;
        public int Minute;
        public byte RepeatDays;
        public bool Enabled;
        public string Label;
        public DateTime Next;
        public int Source;
    }

    private static readonly int[] SampleHours = { 7, 12, 21 };
    private static readonly int[] SampleMinutes = { 0, 30, 0 };
    private static readonly byte[] SampleRepeats = { Weekdays, 0, EveryDay };
    private static readonly bool[] SampleEnabled = { true, true, false };

    private static readonly LocString[] SampleLabels =
    {
        L.WidgetsTime.SampleAlarm, L.WidgetsTime.SampleAlarmMidday, L.WidgetsTime.SampleAlarmLater,
    };

    private readonly Configuration configuration;
    private readonly AlarmRinger ringer;
    private readonly CachedText[] subtitles = new CachedText[MediumRows];
    private AlarmRow[] rows = new AlarmRow[4];
    private int rowCount;
    private CachedText until;
    private CachedText snoozedUntil;

    public AlarmWidget(Configuration configuration, AlarmRinger ringer)
    {
        this.configuration = configuration;
        this.ringer = ringer;
    }

    public string Id => "clock.alarm";
    public string DisplayName => Loc.T(L.Clock.Alarm);
    public string Description => Loc.T(L.WidgetsTime.AlarmDescription);
    public string AppId => "clock";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium;

    public WidgetRoute Target(in WidgetContext context) => WidgetRoute.Tab(AppId, AlarmsRoute);

    public float Relevance(string config)
    {
        if (IsRinging)
        {
            return 1f;
        }

        if (ringer.IsSnoozed)
        {
            return 0.9f;
        }

        var now = DateTime.Now;
        var alarms = configuration.Alarms;
        var soonest = double.MaxValue;
        for (var index = 0; index < alarms.Count; index++)
        {
            if (!alarms[index].Enabled)
            {
                continue;
            }

            soonest = Math.Min(soonest, (AlarmSchedule.NextOccurrence(alarms[index], now) - now).TotalMinutes);
        }

        if (soonest > RelevanceWindowMinutes)
        {
            return 0f;
        }

        return 0.5f + 0.4f * (1f - (float)soonest / RelevanceWindowMinutes);
    }

    private bool IsRinging => ringer.IsRinging && ringer.Kind == AlarmRingKind.Alarm;

    public void Draw(in WidgetContext context)
    {
        if (context.Opacity <= 0f)
        {
            return;
        }

        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var now = DateTime.Now;
        Build(now, context.Preview);
        if (IsRinging && !context.Preview)
        {
            DrawRinging(context, ink, now);
            return;
        }

        if (context.Size == WidgetSize.Small)
        {
            DrawSmall(context, ink, now);
            return;
        }

        DrawMedium(context, ink, now);
    }

    private void Build(DateTime now, bool preview)
    {
        var alarms = configuration.Alarms;
        var sample = preview && alarms.Count == 0;
        var count = sample ? SampleHours.Length : alarms.Count;
        if (rows.Length < count)
        {
            rows = new AlarmRow[Math.Max(count, rows.Length * 2)];
        }

        for (var index = 0; index < count; index++)
        {
            rows[index] = sample ? SampleRow(index, now) : RealRow(alarms[index], index, now);
        }

        rowCount = count;
        for (var index = 1; index < rowCount; index++)
        {
            var current = rows[index];
            var target = index - 1;
            while (target >= 0 && Before(current, rows[target]))
            {
                rows[target + 1] = rows[target];
                target--;
            }

            rows[target + 1] = current;
        }
    }

    private static AlarmRow RealRow(AlarmEntry alarm, int index, DateTime now) => new()
    {
        Hour = alarm.Hour,
        Minute = alarm.Minute,
        RepeatDays = alarm.RepeatDays,
        Enabled = alarm.Enabled,
        Label = alarm.Label,
        Next = AlarmSchedule.NextOccurrence(alarm, now),
        Source = index,
    };

    private static AlarmRow SampleRow(int index, DateTime now)
    {
        var today = new DateTime(now.Year, now.Month, now.Day, SampleHours[index], SampleMinutes[index], 0);
        return new AlarmRow
        {
            Hour = SampleHours[index],
            Minute = SampleMinutes[index],
            RepeatDays = SampleRepeats[index],
            Enabled = SampleEnabled[index],
            Label = Loc.T(SampleLabels[index]),
            Next = today > now ? today : today.AddDays(1),
            Source = -1,
        };
    }

    private static bool Before(in AlarmRow left, in AlarmRow right)
    {
        if (left.Enabled != right.Enabled)
        {
            return left.Enabled;
        }

        return left.Next < right.Next;
    }

    private void DrawSmall(in WidgetContext context, in WidgetInk ink, DateTime now)
    {
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        var drawList = context.DrawList;
        var accent = AppAccents.For(AppId);
        var snoozed = SnoozedMoment(out var snoozeLocal);
        var headerBottom = WidgetChrome.Header(context, ink, AppId,
            snoozed ? L.WidgetsTime.Snoozed : L.Clock.Alarm, accent);
        var hasNext = rowCount > 0 && rows[0].Enabled;
        if (!snoozed && !hasNext)
        {
            DrawEmpty(context, ink, new Rect(new Vector2(content.Min.X, headerBottom), content.Max));
            return;
        }

        var moment = snoozed ? snoozeLocal : rows[0].Next;
        var label = snoozed ? SnoozedLabel() : LabelOf(rows[0]);
        var clock = TimeText.Clock(moment);
        var hero = WidgetText.FitStyle(clock, WidgetType.DisplayCompact, content.Width, true);
        var heroHeight = Typography.Measure(clock, hero).Y;
        var labelHeight = Typography.Measure("A", WidgetType.Headline).Y;
        var whenHeight = Typography.Measure("A", WidgetType.Caption).Y;
        var whenTop = content.Max.Y - whenHeight;
        var labelTop = whenTop - WidgetMetrics.RowGap * scale - labelHeight;
        var heroTop = labelTop - heroHeight;
        TimeWidgetParts.Clock(drawList, new Vector2(content.Min.X, heroTop), clock, ink.Primary, hero,
            WidgetType.Title, ink.Secondary);
        Marquee.DrawLeftAuto(drawList, new MarqueeId("clock.alarm.label", context.InstanceKey), label, content.Min.X,
            labelTop, content.Width, WidgetType.Headline, ink.Primary);
        WidgetText.Draw(drawList, new Vector2(content.Min.X, whenTop), Until(moment, now), ink.Secondary,
            WidgetType.Caption, content.Width);
    }

    private void DrawMedium(in WidgetContext context, in WidgetInk ink, DateTime now)
    {
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        var drawList = context.DrawList;
        var accent = AppAccents.For(AppId);
        var headerBottom = WidgetChrome.Header(context, ink, AppId, L.Clock.TabAlarms, accent);
        if (SnoozedMoment(out var snoozeLocal))
        {
            var text = SnoozedUntil(snoozeLocal);
            var width = MathF.Min(content.Width * 0.5f, Typography.Measure(text, WidgetType.Caption).X);
            WidgetText.Draw(drawList, new Vector2(content.Max.X - width, content.Min.Y), text, ink.Accent(accent),
                WidgetType.Caption, width);
        }

        var listTop = headerBottom + WidgetMetrics.Gutter * 0.5f * scale;
        if (rowCount == 0)
        {
            DrawEmpty(context, ink, new Rect(new Vector2(content.Min.X, listTop), content.Max));
            return;
        }

        var visible = Math.Min(MediumRows, rowCount);
        var rowHeight = (content.Max.Y - listTop) / MediumRows;
        var timeColumn = 0f;
        for (var index = 0; index < visible; index++)
        {
            timeColumn = MathF.Max(timeColumn,
                TimeWidgetParts.ClockWidth(TimeText.Clock(rows[index].Next), WidgetType.Title, WidgetType.Caption));
        }

        for (var index = 0; index < visible; index++)
        {
            var top = listTop + index * rowHeight;
            if (index > 0)
            {
                WidgetChrome.Separator(context, ink, content.Min.X, content.Max.X, top);
            }

            DrawRow(context, ink, new Rect(new Vector2(content.Min.X, top), new Vector2(content.Max.X, top + rowHeight)),
                index, timeColumn, accent);
        }
    }

    private void DrawRow(in WidgetContext context, in WidgetInk ink, Rect row, int index, float timeColumn,
        Vector4 accent)
    {
        var scale = context.Scale;
        var drawList = context.DrawList;
        var entry = rows[index];
        var clock = TimeText.Clock(entry.Next);
        var clockHeight = Typography.Measure(clock, WidgetType.Title).Y;
        var primary = entry.Enabled ? ink.Primary : ink.Tertiary;
        TimeWidgetParts.Clock(drawList, new Vector2(row.Min.X, row.Center.Y - clockHeight * 0.5f), clock, primary,
            WidgetType.Title, WidgetType.Caption, entry.Enabled ? ink.Secondary : ink.Tertiary);

        var toggleRadius = ToggleUnits * 0.5f * scale;
        var toggleCenter = new Vector2(row.Max.X - toggleRadius, row.Center.Y);
        var icon = entry.Enabled ? FontAwesomeIcon.Bell : FontAwesomeIcon.BellSlash;
        var next = WidgetControls.Toggle(context, ink, ToggleControlBase + index, toggleCenter, ToggleUnits, icon,
            entry.Enabled, accent);
        if (next != entry.Enabled && entry.Source >= 0 && entry.Source < configuration.Alarms.Count)
        {
            configuration.Alarms[entry.Source].Enabled = next;
            configuration.Save();
        }

        var textLeft = row.Min.X + timeColumn + WidgetMetrics.Gutter * 1.5f * scale;
        var textWidth = MathF.Max(1f, toggleCenter.X - toggleRadius - WidgetMetrics.Gutter * scale - textLeft);
        var labelHeight = Typography.Measure("A", WidgetType.Headline).Y;
        var subtitleHeight = Typography.Measure("A", WidgetType.Caption).Y;
        var top = row.Center.Y - (labelHeight + WidgetMetrics.RowGap * 0.5f * scale + subtitleHeight) * 0.5f;
        Marquee.DrawLeftAuto(drawList, new MarqueeId(context.InstanceKey, index), LabelOf(entry), textLeft, top,
            textWidth, WidgetType.Headline, entry.Enabled ? ink.Primary : ink.Secondary);
        WidgetText.Draw(drawList, new Vector2(textLeft, top + labelHeight + WidgetMetrics.RowGap * 0.5f * scale),
            Subtitle(index, entry), ink.Secondary, WidgetType.Caption, textWidth);
    }

    private void DrawRinging(in WidgetContext context, in WidgetInk ink, DateTime now)
    {
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        var drawList = context.DrawList;
        var accent = AppAccents.For(AppId);
        var headerBottom = WidgetChrome.Header(context, ink, AppId, L.WidgetsTime.Ringing, accent);
        var label = ringer.Label.Length > 0 ? ringer.Label : Loc.T(L.Clock.Alarm);
        var capsule = CapsuleUnits * scale;
        var titleTop = headerBottom + WidgetMetrics.Gutter * scale;
        var small = context.Size == WidgetSize.Small;
        var textWidth = small ? content.Width : content.Width * 0.5f;
        var titleHeight = WidgetText.Wrapped(drawList, new Vector2(content.Min.X, titleTop), label, ink.Primary,
            WidgetType.Title, textWidth, small ? 1 : 2);
        WidgetText.Draw(drawList, new Vector2(content.Min.X, titleTop + titleHeight + WidgetMetrics.RowGap * scale),
            TimeText.Clock(now), ink.Secondary, WidgetType.Caption, textWidth);

        if (small)
        {
            var stopRect = new Rect(new Vector2(content.Min.X, content.Max.Y - capsule), content.Max);
            if (WidgetControls.Button(context, ink, StopControl, stopRect, FontAwesomeIcon.Stop, Loc.T(L.Clock.Stop),
                    accent))
            {
                ringer.Stop();
            }

            return;
        }

        var gap = WidgetMetrics.Gutter * scale;
        var columnLeft = content.Min.X + content.Width * 0.5f + gap;
        var snoozeRect = new Rect(new Vector2(columnLeft, content.Max.Y - capsule * 2f - gap),
            new Vector2(content.Max.X, content.Max.Y - capsule - gap));
        var stopRectMedium = new Rect(new Vector2(columnLeft, content.Max.Y - capsule), content.Max);
        if (WidgetControls.Button(context, ink, SnoozeControl, snoozeRect, FontAwesomeIcon.Moon,
                Loc.T(L.Clock.Snooze)))
        {
            ringer.Snooze(DateTime.UtcNow);
        }

        if (WidgetControls.Button(context, ink, StopControl, stopRectMedium, FontAwesomeIcon.Stop,
                Loc.T(L.Clock.Stop), accent))
        {
            ringer.Stop();
        }
    }

    private void DrawEmpty(in WidgetContext context, in WidgetInk ink, Rect area)
    {
        var anyAlarms = configuration.Alarms.Count > 0;
        WidgetChrome.Message(context, ink, area, FontAwesomeIcon.BellSlash, default,
            Loc.T(anyAlarms ? L.WidgetsTime.AlarmsOff : L.WidgetsTime.NoAlarms), Loc.T(L.WidgetsTime.NoAlarmsHint));
    }

    private bool SnoozedMoment(out DateTime local)
    {
        if (ringer.SnoozeEndsUtc is not { } snoozeEnd)
        {
            local = default;
            return false;
        }

        local = snoozeEnd.ToLocalTime();
        return true;
    }

    private string SnoozedLabel() =>
        ringer.SnoozedLabel.Length > 0 ? ringer.SnoozedLabel : Loc.T(L.Clock.Alarm);

    private static string LabelOf(in AlarmRow row) => row.Label.Length > 0 ? row.Label : Loc.T(L.Clock.Alarm);

    private string Until(DateTime moment, DateTime now)
    {
        var span = moment - now;
        var key = (long)Math.Ceiling(span.TotalMinutes);
        return until.IsCurrent(key) ? until.Value : until.Store(key, TimeText.Until(TimeSpan.FromMinutes(key)));
    }

    private string SnoozedUntil(DateTime local)
    {
        var key = TimeWidgetParts.MinuteKey(local);
        return snoozedUntil.IsCurrent(key)
            ? snoozedUntil.Value
            : snoozedUntil.Store(key, Loc.T(L.WidgetsTime.SnoozedUntil, TimeText.Clock(local)));
    }

    private string Subtitle(int index, in AlarmRow row)
    {
        var dayIndex = (row.Next.Date - DateTime.Today).Days;
        var key = row.RepeatDays != 0 ? row.RepeatDays : 1000L + dayIndex;
        ref var cache = ref subtitles[index];
        if (cache.IsCurrent(key))
        {
            return cache.Value;
        }

        if (row.RepeatDays == 0)
        {
            return cache.Store(key, TimeWidgetParts.RelativeDay(row.Next));
        }

        var probe = new AlarmEntry { RepeatDays = row.RepeatDays };
        return cache.Store(key, AlarmSchedule.RepeatLabel(probe));
    }

    public void Dispose()
    {
    }
}
