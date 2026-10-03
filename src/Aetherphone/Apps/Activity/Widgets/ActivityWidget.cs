using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Activity;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Activity.Widgets;

internal sealed class ActivityWidget : IHomeWidget
{
    private const int RingCount = 3;
    private const int WeekDays = 7;
    private const int WeekRefreshMilliseconds = 2000;
    private const float ThicknessFactor = 0.22f;
    private const float GapFactor = 0.06f;
    private const float SmallRingUnits = 66f;
    private const float BarWidthUnits = 4f;
    private const float BarGapUnits = 2f;
    private const float SampleLevels = 0.72f;
    private const int SampleDuties = 2;
    private const long SampleGil = 44000;
    private const string DateKeyFormat = "yyyy-MM-dd";

    private static readonly float[] SampleWeek =
    {
        0.9f, 0.6f, 0.4f, 1f, 1f, 0.8f, 0.3f, 0.5f, 0.2f, 0.7f, 0.9f, 1f, 1f, 1f, 0.6f, 0.4f, 0.3f, 0.2f, 0.72f,
        0.66f, 0.88f,
    };

    private sealed class Rings
    {
        public readonly WidgetEase[] Values = new WidgetEase[RingCount];
        public readonly WidgetEase[] Bars = new WidgetEase[WeekDays * RingCount];
    }

    private readonly ActivityTracker tracker;
    private readonly Configuration configuration;
    private readonly WidgetStates<Rings> rings = new();
    private readonly float[] week = new float[WeekDays * RingCount];
    private readonly string[] weekKeys = new string[WeekDays];
    private readonly CachedText[] dayLabels = new CachedText[WeekDays];
    private readonly CachedText[] values = new CachedText[RingCount];
    private readonly float[] fractions = new float[RingCount];
    private WidgetRefresh weekCadence;
    private DateTime weekAnchor;

    public ActivityWidget(ActivityTracker tracker, Configuration configuration)
    {
        this.tracker = tracker;
        this.configuration = configuration;
    }

    public string Id => "character.rings";
    public string DisplayName => Loc.T(L.Character.Activity);
    public string Description => Loc.T(L.Widgets.ActivityDescription);
    public string AppId => "character";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium | WidgetSizeSet.Large;

    public float Relevance(string config)
    {
        if (!tracker.IsTracking || DateTime.Now.Hour < 18)
        {
            return 0f;
        }

        var today = tracker.Today;
        return ActivityGoals.AllClosed(configuration, today) ? 0f : 0.4f;
    }

    public void Draw(in WidgetContext context)
    {
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var tracking = tracker.IsTracking;
        var sample = !tracking && context.Preview;
        var state = rings.For(context.InstanceKey);
        Measure(sample, tracking);
        var animate = !context.Preview;
        for (var ring = 0; ring < RingCount; ring++)
        {
            fractions[ring] = state.Values[ring].Step(fractions[ring], context.Delta, animate);
        }

        if (!tracking && !sample)
        {
            DrawUnavailable(context, ink);
            return;
        }

        var content = WidgetMetrics.Content(context);
        switch (context.Size)
        {
            case WidgetSize.Small:
                DrawSmall(context, ink, content, sample);
                return;
            case WidgetSize.Medium:
                DrawSummary(context, ink, content, sample);
                return;
            default:
                var split = content.Min.Y + content.Height * 0.46f;
                DrawSummary(context, ink, new Rect(content.Min, new Vector2(content.Max.X, split)), sample);
                DrawWeek(context, ink, state, new Rect(new Vector2(content.Min.X,
                    split + WidgetMetrics.Gutter * 1.5f * context.Scale), content.Max), sample);
                return;
        }
    }

    private void Measure(bool sample, bool tracking)
    {
        if (sample)
        {
            fractions[0] = SampleLevels / MathF.Max(0.01f, configuration.ActivityGoalLevels);
            fractions[1] = SampleDuties / (float)Math.Max(1, configuration.ActivityGoalDuties);
            fractions[2] = SampleGil / (float)Math.Max(1L, configuration.ActivityGoalGil);
            return;
        }

        if (!tracking)
        {
            fractions[0] = 0f;
            fractions[1] = 0f;
            fractions[2] = 0f;
            return;
        }

        var today = tracker.Today;
        fractions[0] = ActivityGoals.ProgressFraction(configuration, today);
        fractions[1] = ActivityGoals.AdventureFraction(configuration, today);
        fractions[2] = ActivityGoals.FortuneFraction(configuration, today);
    }

    private void DrawSmall(in WidgetContext context, in WidgetInk ink, Rect content, bool sample)
    {
        var radius = SmallRingUnits * 0.5f * context.Scale;
        DrawRings(context, ink, new Vector2(content.Min.X + radius, content.Min.Y + radius), radius);
        var lineHeight = Typography.Measure("Ag", WidgetType.Headline).Y;
        var top = content.Max.Y - lineHeight * RingCount;
        for (var ring = 0; ring < RingCount; ring++)
        {
            WidgetText.Draw(context.DrawList, new Vector2(content.Min.X, top + lineHeight * ring), Value(ring, sample),
                ink.Accent(Tint(ring)), WidgetType.Headline, content.Width);
        }
    }

    private void DrawSummary(in WidgetContext context, in WidgetInk ink, Rect area, bool sample)
    {
        var scale = context.Scale;
        var radius = area.Height * 0.5f;
        DrawRings(context, ink, new Vector2(area.Min.X + radius, area.Center.Y), radius);
        var left = area.Min.X + radius * 2f + WidgetMetrics.Gutter * 2f * scale;
        var width = MathF.Max(1f, area.Max.X - left);
        var rowHeight = area.Height / RingCount;
        var eyebrowHeight = WidgetText.EyebrowHeight();
        var valueHeight = Typography.Measure("Ag", WidgetType.Title).Y;
        var blockHeight = eyebrowHeight + WidgetMetrics.RowGap * scale + valueHeight;
        for (var ring = 0; ring < RingCount; ring++)
        {
            var top = area.Min.Y + rowHeight * ring + (rowHeight - blockHeight) * 0.5f;
            WidgetText.EyebrowFit(context.DrawList, new Vector2(left, top), Loc.T(Label(ring)), width,
                ink.Accent(Tint(ring)), scale);
            WidgetText.Draw(context.DrawList,
                new Vector2(left, top + eyebrowHeight + WidgetMetrics.RowGap * scale), Value(ring, sample),
                ink.Primary, WidgetType.Title, width);
        }
    }

    private void DrawRings(in WidgetContext context, in WidgetInk ink, Vector2 center, float radius)
    {
        var thickness = radius * ThicknessFactor;
        var step = thickness + radius * GapFactor;
        var ringRadius = radius - thickness * 0.5f;
        for (var ring = 0; ring < RingCount; ring++)
        {
            var color = ink.Accent(Tint(ring));
            var track = ink.KeepsOwnColors || ink.Mode == WidgetMode.Dark
                ? color with { W = color.W * 0.22f }
                : ink.Fill;
            WidgetChrome.Ring(context.DrawList, center, ringRadius - step * ring, thickness, fractions[ring], color,
                track);
        }
    }

    private void DrawWeek(in WidgetContext context, in WidgetInk ink, Rings state, Rect area, bool sample)
    {
        RefreshWeek(sample);
        var scale = context.Scale;
        var drawList = context.DrawList;
        var animate = !context.Preview;
        WidgetText.Eyebrow(drawList, area.Min, L.WidgetsLife.ThisWeek, ink.Secondary, scale);
        var top = area.Min.Y + WidgetText.EyebrowHeight() + WidgetMetrics.Gutter * scale;
        var labelHeight = Typography.Measure("Ag", WidgetType.Caption).Y;
        var barBottom = area.Max.Y - labelHeight - WidgetMetrics.RowGap * 2f * scale;
        var barHeight = MathF.Max(1f, barBottom - top);
        var columnWidth = area.Width / WeekDays;
        var barWidth = BarWidthUnits * scale;
        var barGap = BarGapUnits * scale;
        var groupWidth = barWidth * RingCount + barGap * (RingCount - 1);
        for (var day = 0; day < WeekDays; day++)
        {
            var centerX = area.Min.X + columnWidth * (day + 0.5f);
            var groupLeft = centerX - groupWidth * 0.5f;
            for (var ring = 0; ring < RingCount; ring++)
            {
                var slot = day * RingCount + ring;
                var value = state.Bars[slot].Step(Math.Clamp(week[slot], 0f, 1f), context.Delta, animate);
                var left = groupLeft + (barWidth + barGap) * ring;
                var color = ink.Accent(Tint(ring));
                Squircle.Fill(drawList, new Vector2(left, top), new Vector2(left + barWidth, barBottom),
                    barWidth * 0.5f, ImGui.GetColorU32(color with { W = color.W * 0.2f }));
                if (value <= 0.01f)
                {
                    continue;
                }

                var fillTop = barBottom - MathF.Max(barWidth, barHeight * value);
                Squircle.Fill(drawList, new Vector2(left, fillTop), new Vector2(left + barWidth, barBottom),
                    barWidth * 0.5f, ImGui.GetColorU32(color));
            }

            var isToday = day == WeekDays - 1;
            var label = DayLabel(day);
            var labelStyle = isToday ? WidgetType.Headline : WidgetType.Caption;
            var labelSize = Typography.Measure(label, labelStyle);
            Typography.Draw(drawList, new Vector2(centerX - labelSize.X * 0.5f, area.Max.Y - labelSize.Y), label,
                isToday ? ink.Primary : ink.Secondary, labelStyle);
        }
    }

    private void RefreshWeek(bool sample)
    {
        var today = DateTime.Today;
        if (today != weekAnchor)
        {
            weekAnchor = today;
            for (var day = 0; day < WeekDays; day++)
            {
                weekKeys[day] = today.AddDays(day - (WeekDays - 1)).ToString(DateKeyFormat,
                    CultureInfo.InvariantCulture);
            }

            weekCadence.Expire();
        }

        if (sample)
        {
            Array.Copy(SampleWeek, week, week.Length);
            return;
        }

        if (!weekCadence.Due(WeekRefreshMilliseconds))
        {
            return;
        }

        Array.Clear(week);
        var days = tracker.Days;
        for (var index = days.Count - 1; index >= 0 && index >= days.Count - WeekDays * 2; index--)
        {
            var entry = days[index];
            var slot = Array.IndexOf(weekKeys, entry.Date);
            if (slot < 0)
            {
                continue;
            }

            week[slot * RingCount] = ActivityGoals.ProgressFraction(configuration, entry);
            week[slot * RingCount + 1] = ActivityGoals.AdventureFraction(configuration, entry);
            week[slot * RingCount + 2] = ActivityGoals.FortuneFraction(configuration, entry);
        }
    }

    private string DayLabel(int day)
    {
        var date = weekAnchor.AddDays(day - (WeekDays - 1));
        var key = date.Ticks;
        return dayLabels[day].IsCurrent(key)
            ? dayLabels[day].Value
            : dayLabels[day].Store(key, Loc.Culture.DateTimeFormat.GetShortestDayName(date.DayOfWeek));
    }

    private string Value(int ring, bool sample)
    {
        switch (ring)
        {
            case 0:
                var levels = sample ? SampleLevels : tracker.Today.LevelUnitsGained;
                var levelGoal = configuration.ActivityGoalLevels;
                var levelKey = (long)MathF.Round(levels * 10f) * 1_000_003L + (long)MathF.Round(levelGoal * 10f);
                return values[0].IsCurrent(levelKey)
                    ? values[0].Value
                    : values[0].Store(levelKey, Loc.T(L.WidgetsLife.LevelsOf, levels.ToString("0.#", Loc.Culture),
                        levelGoal.ToString("0.#", Loc.Culture)));
            case 1:
                long duties = sample ? SampleDuties : tracker.Today.DutiesCompleted;
                long dutyGoal = configuration.ActivityGoalDuties;
                var dutyKey = duties * 1_000_003L + dutyGoal;
                return values[1].IsCurrent(dutyKey)
                    ? values[1].Value
                    : values[1].Store(dutyKey, Loc.T(L.WidgetsLife.DutiesOf, duties, dutyGoal));
            default:
                var gil = sample ? SampleGil : tracker.Today.GilEarned;
                var gilGoal = configuration.ActivityGoalGil;
                var gilKey = unchecked(gil * 1_000_003L + gilGoal);
                return values[2].IsCurrent(gilKey)
                    ? values[2].Value
                    : values[2].Store(gilKey, Loc.T(L.WidgetsLife.GilOf, NumberText.Compact(gil),
                        NumberText.Compact(gilGoal)));
        }
    }

    private void DrawUnavailable(in WidgetContext context, in WidgetInk ink)
    {
        var content = WidgetMetrics.Content(context);
        var radius = (context.Size == WidgetSize.Small ? SmallRingUnits * 0.36f : SmallRingUnits * 0.5f) *
                     context.Scale;
        DrawRings(context, ink, new Vector2(content.Min.X + radius, content.Min.Y + radius), radius);
        WidgetChrome.Message(context, ink, WidgetMetrics.Below(context, content.Min.Y + radius * 2f + WidgetMetrics.Gutter * context.Scale),
            Loc.T(L.WidgetsLife.ActivityUnavailable), string.Empty);
    }

    private static LocString Label(int ring) => ring switch
    {
        0 => L.Character.RingProgress,
        1 => L.Character.RingAdventure,
        _ => L.Character.RingFortune,
    };

    private static Vector4 Tint(int ring) => ring switch
    {
        0 => ActivityRings.RingOneTint,
        1 => ActivityRings.RingTwoTint,
        _ => ActivityRings.RingThreeTint,
    };

    public void Dispose()
    {
    }
}
