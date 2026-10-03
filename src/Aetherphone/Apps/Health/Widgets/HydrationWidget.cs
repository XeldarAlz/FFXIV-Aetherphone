using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Health;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Interface;

namespace Aetherphone.Apps.Health.Widgets;

internal sealed class HydrationWidget : IHomeWidget
{
    public const string HydrationIntent = "health.tab.hydration";

    private const int LogControl = 1;
    private const double GlassMillilitres = 250d;
    private const float SmallRingUnits = 60f;
    private const float SmallButtonUnits = 38f;
    private const float MediumButtonUnits = 44f;
    private const float HeaderIconUnits = 12f;
    private const float RowIconUnits = 12f;
    private const float RingThicknessFactor = 0.2f;
    private const int SampleDrinks = 3;
    private const int SampleGoal = 4;
    private const double SampleWalk = 1420d;
    private const double SampleRun = 610d;
    private const int DayStartHour = 8;
    private const int DayLengthHours = 12;
    private const int LateHour = 14;
    private const string DateKeyFormat = "yyyy-MM-dd";
    private static readonly Vector4 Water = new(0.25f, 0.66f, 1f, 1f);

    private sealed class Glass
    {
        public WidgetEase Ring;
    }

    private readonly HealthTracker health;
    private readonly WidgetStates<Glass> glasses = new();
    private CachedText countText;
    private CachedText volumeText;
    private CachedText drinksText;
    private CachedText walkText;
    private CachedText runText;
    private DateTime keyDay;
    private string todayKey = string.Empty;

    public HydrationWidget(HealthTracker health)
    {
        this.health = health;
    }

    public string Id => "health.hydration";
    public string DisplayName => Loc.T(L.Health.Hydration);
    public string Description => Loc.T(L.WidgetsLife.HydrationDescription);
    public string AppId => "health";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium;

    public WidgetRoute Target(in WidgetContext context) => WidgetRoute.Tab(AppId, HydrationIntent);

    public float Relevance(string config)
    {
        if (!health.IsTracking)
        {
            return 0f;
        }

        var hour = DateTime.Now.Hour;
        if (hour < LateHour)
        {
            return 0f;
        }

        var goal = Math.Max(1, health.Profile.DailyHydrationGoal);
        var expected = goal * Math.Clamp((hour - DayStartHour) / (float)DayLengthHours, 0f, 1f);
        var behind = expected - (Today()?.DrinkCount ?? 0);
        return behind < 0.5f ? 0f : Math.Clamp(0.45f + behind * 0.15f, 0f, 0.85f);
    }

    public void Draw(in WidgetContext context)
    {
        WidgetChrome.Container(context);
        var ink = WidgetInk.From(context);
        var water = ink.Accent(Water);
        var headerBottom = DrawHeader(context, ink, water);
        var tracking = health.IsTracking;
        var sample = !tracking && context.Preview;
        if (!tracking && !sample)
        {
            WidgetChrome.Message(context, ink, WidgetMetrics.Below(context, headerBottom), Loc.T(L.WidgetsLife.HydrationUnavailable),
                string.Empty);
            return;
        }

        var day = sample ? null : Today();
        var drinks = sample ? SampleDrinks : day?.DrinkCount ?? 0;
        var goal = sample ? SampleGoal : Math.Max(1, health.Profile.DailyHydrationGoal);
        var glass = glasses.For(context.InstanceKey);
        var fraction = glass.Ring.Step(drinks / (float)goal, context.Delta, !context.Preview);
        var content = WidgetMetrics.Content(context);
        if (context.Size == WidgetSize.Small)
        {
            DrawSmall(context, ink, water, content, headerBottom, fraction, drinks, goal, day, sample);
            return;
        }

        DrawMedium(context, ink, water, content, headerBottom, fraction, drinks, goal, day, sample);
    }

    private float DrawHeader(in WidgetContext context, in WidgetInk ink, Vector4 water)
    {
        var content = WidgetMetrics.Content(context);
        var icon = HeaderIconUnits * context.Scale;
        var eyebrowHeight = WidgetText.EyebrowHeight();
        var rowHeight = MathF.Max(icon, eyebrowHeight);
        var centerY = content.Min.Y + rowHeight * 0.5f;
        ProgressRing.CenterIcon(context.DrawList, new Vector2(content.Min.X + icon * 0.5f, centerY),
            FontAwesomeIcon.Tint, water, icon);
        var left = content.Min.X + icon + WidgetMetrics.Gutter * 0.5f * context.Scale;
        WidgetText.EyebrowFit(context.DrawList, new Vector2(left, centerY - eyebrowHeight * 0.5f),
            Loc.T(L.Health.Hydration), MathF.Max(1f, content.Max.X - left), water, context.Scale);
        return content.Min.Y + rowHeight;
    }

    private void DrawSmall(in WidgetContext context, in WidgetInk ink, Vector4 water, Rect content,
        float headerBottom, float fraction, int drinks, int goal, HealthDay? day, bool sample)
    {
        var scale = context.Scale;
        WidgetText.Draw(context.DrawList, new Vector2(content.Min.X, headerBottom + WidgetMetrics.RowGap * scale),
            Volume(day, sample), ink.Secondary, WidgetType.Caption, content.Width);
        var radius = SmallRingUnits * 0.5f * scale;
        var center = new Vector2(content.Min.X + radius, content.Max.Y - radius);
        DrawRing(context, ink, water, center, radius, fraction, drinks, goal);
        var button = SmallButtonUnits * 0.5f * scale;
        LogButton(context, ink, new Vector2(content.Max.X - button, content.Max.Y - button), SmallButtonUnits,
            sample);
    }

    private void DrawMedium(in WidgetContext context, in WidgetInk ink, Vector4 water, Rect content,
        float headerBottom, float fraction, int drinks, int goal, HealthDay? day, bool sample)
    {
        var scale = context.Scale;
        var drawList = context.DrawList;
        var ringTop = headerBottom + WidgetMetrics.Gutter * scale;
        var radius = MathF.Max(1f, (content.Max.Y - ringTop) * 0.5f);
        var center = new Vector2(content.Min.X + radius, ringTop + radius);
        DrawRing(context, ink, water, center, radius, fraction, drinks, goal);
        var button = MediumButtonUnits * 0.5f * scale;
        LogButton(context, ink, new Vector2(content.Max.X - button, content.Max.Y - button), MediumButtonUnits,
            sample);
        var left = center.X + radius + WidgetMetrics.Gutter * 2f * scale;
        var width = MathF.Max(1f, content.Max.X - button * 2f - WidgetMetrics.Gutter * scale - left);
        var top = ringTop;
        top += WidgetText.Draw(drawList, new Vector2(left, top), Volume(day, sample), ink.Primary, WidgetType.Title,
            width);
        var drinksLine = drinks >= goal ? Loc.T(L.WidgetsLife.GoalReached) : DrinksLine(drinks, goal);
        top += WidgetText.Draw(drawList, new Vector2(left, top), drinksLine, drinks >= goal ? water : ink.Secondary,
            WidgetType.Caption, width);
        var rowWidth = MathF.Max(1f, content.Max.X - left);
        var rowHeight = Typography.Measure("Ag", WidgetType.Headline).Y;
        var runTop = content.Max.Y - rowHeight;
        var walkTop = runTop - rowHeight - WidgetMetrics.RowGap * scale;
        if (walkTop < top + WidgetMetrics.RowGap * scale)
        {
            return;
        }

        var walk = sample ? SampleWalk : day?.WalkYalms ?? 0d;
        var run = sample ? SampleRun : day?.RunYalms ?? 0d;
        DistanceRow(context, ink, new Vector2(left, walkTop), rowWidth - button * 2f, FontAwesomeIcon.Walking,
            Distance(ref walkText, walk));
        DistanceRow(context, ink, new Vector2(left, runTop), rowWidth - button * 2f, FontAwesomeIcon.Running,
            Distance(ref runText, run));
    }

    private static void DistanceRow(in WidgetContext context, in WidgetInk ink, Vector2 position, float width,
        FontAwesomeIcon icon, string value)
    {
        var size = RowIconUnits * context.Scale;
        var height = Typography.Measure("Ag", WidgetType.Headline).Y;
        ProgressRing.CenterIcon(context.DrawList, new Vector2(position.X + size * 0.5f, position.Y + height * 0.5f),
            icon, ink.Secondary, size);
        var left = position.X + size + WidgetMetrics.Gutter * 0.75f * context.Scale;
        WidgetText.Draw(context.DrawList, new Vector2(left, position.Y), value, ink.Primary, WidgetType.Headline,
            MathF.Max(1f, position.X + width - left));
    }

    private void DrawRing(in WidgetContext context, in WidgetInk ink, Vector4 water, Vector2 center, float radius,
        float fraction, int drinks, int goal)
    {
        var thickness = radius * RingThicknessFactor;
        var track = ink.KeepsOwnColors || ink.Mode == WidgetMode.Dark ? water with { W = water.W * 0.22f } : ink.Fill;
        WidgetChrome.Ring(context.DrawList, center, radius - thickness * 0.5f, thickness, fraction, water, track);
        var key = drinks * 1_000L + goal;
        var text = countText.IsCurrent(key)
            ? countText.Value
            : countText.Store(key, string.Concat(drinks.ToString(Loc.Culture), "/", goal.ToString(Loc.Culture)));
        var style = WidgetType.Headline;
        var inner = (radius - thickness) * 1.6f;
        var width = WidgetText.TabularWidth(text, style);
        if (width > inner)
        {
            style = new TextStyle(style.Scale * MathF.Max(0.6f, inner / width), style.Weight);
            width = WidgetText.TabularWidth(text, style);
        }

        var height = Typography.Measure(text, style).Y;
        WidgetText.Tabular(context.DrawList, new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f), text,
            ink.Primary, style);
    }

    private void LogButton(in WidgetContext context, in WidgetInk ink, Vector2 center, float diameterUnits,
        bool sample)
    {
        if (!WidgetControls.Button(context, ink, LogControl, center, diameterUnits, FontAwesomeIcon.Plus, Water) ||
            sample)
        {
            return;
        }

        health.LogDrink(DrinkKeys.Water, string.Empty, GlassMillilitres);
    }

    private HealthDay? Today()
    {
        var today = DateTime.Today;
        if (today != keyDay || todayKey.Length == 0)
        {
            keyDay = today;
            todayKey = today.ToString(DateKeyFormat, CultureInfo.InvariantCulture);
        }

        var latest = health.Profile.LatestDay;
        return latest is not null && latest.Date == todayKey ? latest : null;
    }

    private string Volume(HealthDay? day, bool sample)
    {
        var millilitres = sample ? SampleDrinks * GlassMillilitres : day?.DrinkMillilitres ?? 0d;
        var units = health.Profile.Units;
        var key = (long)Math.Round(millilitres) * 4L + (long)units;
        return volumeText.IsCurrent(key)
            ? volumeText.Value
            : volumeText.Store(key, HealthFormat.Volume(millilitres, units));
    }

    private string DrinksLine(int drinks, int goal)
    {
        var key = drinks * 1_000L + goal;
        return drinksText.IsCurrent(key)
            ? drinksText.Value
            : drinksText.Store(key, Loc.T(L.WidgetsLife.DrinksOf, drinks, goal));
    }

    private string Distance(ref CachedText cache, double yalms)
    {
        var units = health.Profile.Units;
        var key = (long)Math.Round(yalms) * 4L + (long)units;
        return cache.IsCurrent(key) ? cache.Value : cache.Store(key, HealthFormat.Distance(yalms, units));
    }

    public void Dispose()
    {
    }
}
