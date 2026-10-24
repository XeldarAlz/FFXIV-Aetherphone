using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Health;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Health;

internal sealed partial class HealthApp
{
    private const float StatTileHeight = 100f;
    private const float StatTileGlyph = 30f;
    private const float StatRowGlyph = 28f;
    private const float LiveDotRadius = 4.5f;
    private const float LiveGlowRadius = 9f;
    private const int StepGoalStep = 500;
    private const int MinStepGoal = 500;
    private const int MaxStepGoal = 100_000;
    private const int StatSlots = 8;

    private static readonly StepperIds StepGoalIds = StepperIds.For("health.steps.goal");

    private static readonly string[] MetricNavIds =
    {
        "health.nav.metric.water",
        "health.nav.metric.steps",
        "health.nav.metric.distance",
        "health.nav.metric.swimming",
        "health.nav.metric.active",
    };

    private static readonly string[] MetricScrollIds =
    {
        "health.metric.water",
        "health.metric.steps",
        "health.metric.distance",
        "health.metric.swimming",
        "health.metric.active",
    };

    private readonly CachedText[] statTexts = new CachedText[StatSlots];
    private CachedText stepGoalText;

    private void DrawMetric(Rect area, HealthMetric metric)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        using (UiScope.PushId(MetricScrollIds[(int)metric]))
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var goal = metric == HealthMetric.Steps ? Profile.DailyStepGoal : 0d;
            var cursorY = DrawDayChart(drawList, origin, width, metric, goal, scale);
            cursorY = metric switch
            {
                HealthMetric.Steps => DrawStepsDetail(drawList, origin.X, cursorY, width, scale),
                HealthMetric.Distance => DrawDistanceDetail(drawList, origin.X, cursorY, width, scale),
                HealthMetric.Swimming => DrawSwimDetail(drawList, origin.X, cursorY, width, scale),
                _ => DrawActiveDetail(drawList, origin.X, cursorY, width, scale),
            };
            cursorY += Footnote(new Vector2(origin.X, cursorY), width, Loc.T(MetricNote(metric)), scale);
            ReserveTo(origin, width, cursorY + HealthArt.BottomBreathing * scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, MetricNavIds[(int)metric], HealthText.Name(metric),
            NavBarStyle.From(ui), ReadOnlySpan<NavBarButton>.Empty, DisplayName, back);
    }

    private static LocString MetricNote(HealthMetric metric) => metric switch
    {
        HealthMetric.Steps => L.Health.StepsNote,
        HealthMetric.Distance => L.Health.TeleportHint,
        HealthMetric.Swimming => L.Health.SwimNote,
        _ => L.Health.ActiveNote,
    };

    private float DrawStepsDetail(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var cursorY = top + HealthArt.TileGap * scale;
        var bottom = RowsCard(drawList, new Vector2(left, cursorY), width, 2, scale, out var card);
        var goal = Profile.DailyStepGoal;
        var delta = StepperRow(drawList, CardRow(drawList, card, 0, scale), StepGoalIds, Loc.T(L.Health.DailyGoal),
            StepGoalText(goal), goal > MinStepGoal, goal < MaxStepGoal, scale);
        if (delta != 0)
        {
            Profile.DailyStepGoal = Math.Clamp((goal + delta * StepGoalStep) / StepGoalStep * StepGoalStep, MinStepGoal,
                MaxStepGoal);
            tracker.SaveNow();
        }

        DrawStatusRow(drawList, CardRow(drawList, card, 1, scale), scale);
        cursorY = SectionTop(drawList, left, bottom, width, Loc.T(L.Health.Records), scale);
        var gap = HealthArt.TileGap * scale;
        var tileWidth = (width - gap) * 0.5f;
        var height = StatTileHeight * scale;
        var streak = new Rect(new Vector2(left, cursorY), new Vector2(left + tileWidth, cursorY + height));
        var record = new Rect(new Vector2(streak.Max.X + gap, cursorY), new Vector2(left + width, cursorY + height));
        DrawStatTile(drawList, streak, AccentRing.Orange, FontAwesomeIcon.Fire, Loc.T(L.Health.CurrentStreak),
            Cached(0, Profile.StreakDays, StatKind.Streak), Profile.StreakDays > 0, scale);
        DrawStatTile(drawList, record, AccentRing.Violet, FontAwesomeIcon.Trophy, Loc.T(L.Health.MostStepsInDay),
            Cached(1, Profile.RecordStepsInDay, StatKind.Count), false, scale);
        return streak.Max.Y + Footnote(new Vector2(left, streak.Max.Y), width, Loc.T(L.Health.StreakHint), scale);
    }

    private float DrawDistanceDetail(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var day = digest.Today;
        var cursorY = SectionTop(drawList, left, top, width, Loc.T(L.Time.Today), scale);
        var bottom = RowsCard(drawList, new Vector2(left, cursorY), width, 5, scale, out var card);
        IconRow(drawList, CardRow(drawList, card, 0, scale), HealthArt.Tint(HealthMetric.Distance),
            FontAwesomeIcon.Walking, Loc.T(L.Health.Walking), Cached(0, day?.WalkYalms ?? 0d, StatKind.Distance), scale);
        IconRow(drawList, CardRow(drawList, card, 1, scale), HealthArt.Tint(HealthMetric.Steps),
            FontAwesomeIcon.Running, Loc.T(L.Health.Running), Cached(1, day?.RunYalms ?? 0d, StatKind.Distance), scale);
        IconRow(drawList, CardRow(drawList, card, 2, scale), AccentRing.Gold, FontAwesomeIcon.Horse,
            Loc.T(L.Health.Mounted), Cached(2, day?.MountYalms ?? 0d, StatKind.Distance), scale);
        IconRow(drawList, CardRow(drawList, card, 3, scale), AccentRing.Teal, FontAwesomeIcon.Feather,
            Loc.T(L.Health.Flying), Cached(3, day?.FlyYalms ?? 0d, StatKind.Distance), scale);
        IconRow(drawList, CardRow(drawList, card, 4, scale), HealthArt.TeleportTint, FontAwesomeIcon.LocationArrow,
            Loc.T(L.Health.Teleports), Cached(4, day?.Teleports ?? 0, StatKind.Count), scale);
        cursorY = SectionTop(drawList, left, bottom, width, Loc.T(L.Health.ThisSession), scale);
        bottom = RowsCard(drawList, new Vector2(left, cursorY), width, 3, scale, out card);
        IconRow(drawList, CardRow(drawList, card, 0, scale), HealthArt.Tint(HealthMetric.Distance),
            FontAwesomeIcon.ShoePrints, Loc.T(L.Health.OnFoot), Cached(5, tracker.SessionOnFootYalms, StatKind.Distance),
            scale);
        IconRow(drawList, CardRow(drawList, card, 1, scale), HealthArt.Tint(HealthMetric.Swimming),
            FontAwesomeIcon.Swimmer, Loc.T(L.Health.Swimming), Cached(6, tracker.SessionSwimYalms, StatKind.Distance),
            scale);
        IconRow(drawList, CardRow(drawList, card, 2, scale), HealthArt.TeleportTint, FontAwesomeIcon.LocationArrow,
            Loc.T(L.Health.Teleports), Cached(7, tracker.SessionTeleports, StatKind.Count), scale);
        return bottom;
    }

    private float DrawSwimDetail(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var day = digest.Today;
        var cursorY = SectionTop(drawList, left, top, width, Loc.T(L.Time.Today), scale);
        var bottom = RowsCard(drawList, new Vector2(left, cursorY), width, 2, scale, out var card);
        IconRow(drawList, CardRow(drawList, card, 0, scale), HealthArt.Tint(HealthMetric.Swimming),
            FontAwesomeIcon.Swimmer, Loc.T(L.Health.Swimming), Cached(0, day?.SwimYalms ?? 0d, StatKind.Distance),
            scale);
        IconRow(drawList, CardRow(drawList, card, 1, scale), AccentRing.Indigo, FontAwesomeIcon.Water,
            Loc.T(L.Health.Diving), Cached(1, day?.DiveYalms ?? 0d, StatKind.Distance), scale);
        cursorY = SectionTop(drawList, left, bottom, width, Loc.T(L.Health.Records), scale);
        bottom = RowsCard(drawList, new Vector2(left, cursorY), width, 2, scale, out card);
        IconRow(drawList, CardRow(drawList, card, 0, scale), AccentRing.Violet, FontAwesomeIcon.Trophy,
            Loc.T(L.Health.LongestSwimSession), Cached(2, Profile.LongestSwimSessionSeconds, StatKind.Duration), scale);
        IconRow(drawList, CardRow(drawList, card, 1, scale), AccentRing.Violet, FontAwesomeIcon.Trophy,
            Loc.T(L.Health.MostSwumInDay), Cached(3, Profile.RecordSwimYalmsInDay, StatKind.Distance), scale);
        return bottom;
    }

    private float DrawActiveDetail(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var day = digest.Today;
        var energy = Profile.CaloriesEnabled && Profile.WeightKg is not null;
        var cursorY = SectionTop(drawList, left, top, width, Loc.T(L.Health.Records), scale);
        var bottom = RowsCard(drawList, new Vector2(left, cursorY), width, energy ? 3 : 2, scale, out var card);
        IconRow(drawList, CardRow(drawList, card, 0, scale), AccentRing.Violet, FontAwesomeIcon.Trophy,
            Loc.T(L.Health.MostActiveDay), Cached(0, Profile.RecordActiveSecondsInDay, StatKind.Duration), scale);
        IconRow(drawList, CardRow(drawList, card, 1, scale), AccentRing.Violet, FontAwesomeIcon.Trophy,
            Loc.T(L.Health.LongestOnFootSession), Cached(1, Profile.LongestOnFootSessionSeconds, StatKind.Duration),
            scale);
        if (energy)
        {
            IconRow(drawList, CardRow(drawList, card, 2, scale), HealthArt.EnergyTint, FontAwesomeIcon.Fire,
                Loc.T(L.Health.EstEnergyToday), Cached(2, day?.Calories ?? 0d, StatKind.Energy), scale);
        }

        return bottom;
    }

    private void DrawStatusRow(ImDrawListPtr drawList, Rect row, float scale)
    {
        var moving = tracker.IsMoving;
        var center = new Vector2(row.Min.X + LiveGlowRadius * scale, row.Center.Y);
        var tint = moving ? AccentRing.Green : ui.MutedInk;
        if (moving)
        {
            drawList.AddCircleFilled(center, LiveGlowRadius * scale * (0.7f + 0.3f * Pulse.Wave(Pulse.Calm)),
                ImGui.GetColorU32(Palette.WithAlpha(tint, 0.25f)), 20);
        }

        drawList.AddCircleFilled(center, LiveDotRadius * scale, ImGui.GetColorU32(tint), 16);
        var textLeft = center.X + LiveGlowRadius * scale + Metrics.Space.Sm * scale;
        var text = Typography.FitText(tracker.TrackingStatus, MathF.Max(1f, row.Max.X - textLeft), TextStyles.Subheadline);
        var height = Typography.Measure(text, TextStyles.Subheadline).Y;
        Typography.Draw(drawList, new Vector2(textLeft, row.Center.Y - height * 0.5f), text, ui.MutedInk,
            TextStyles.Subheadline);
    }

    private void IconRow(ImDrawListPtr drawList, Rect row, Vector4 tint, FontAwesomeIcon icon, string label,
        string value, float scale)
    {
        var glyph = StatRowGlyph * scale;
        HealthArt.GlyphTile(drawList, new Vector2(row.Min.X + glyph * 0.5f, row.Center.Y), glyph, tint, icon);
        var labelRow = new Rect(new Vector2(row.Min.X + glyph + Metrics.Space.Md * scale, row.Min.Y), row.Max);
        var valueSize = Typography.Measure(value, TextStyles.Body);
        RowValue(drawList, labelRow, value, ui.MutedInk);
        RowLabel(drawList, labelRow, label, MathF.Min(valueSize.X, labelRow.Width * 0.55f) + HealthArt.TileGap * scale,
            ui.TitleInk);
    }

    private void DrawStatTile(ImDrawListPtr drawList, Rect rect, Vector4 tint, FontAwesomeIcon icon, string label,
        string value, bool lit, float scale)
    {
        var radius = Metrics.Radius.Grouped * scale;
        ui.Card(drawList, rect.Min, rect.Max, radius);
        if (lit)
        {
            Material.TopGlow(drawList, rect.Min, rect.Max, radius, tint, 0.6f, 0.14f);
        }

        var pad = HealthArt.CardPad * scale;
        var glyph = StatTileGlyph * scale;
        HealthArt.GlyphTile(drawList, new Vector2(rect.Min.X + pad + glyph * 0.5f, rect.Min.Y + pad + glyph * 0.5f),
            glyph, tint, icon);
        var textWidth = MathF.Max(1f, rect.Width - pad * 2f);
        var valueHeight = Typography.LineHeight(TextStyles.Title3);
        var labelHeight = Typography.LineHeight(TextStyles.Footnote);
        var valueTop = rect.Max.Y - pad - valueHeight;
        Typography.Draw(drawList, new Vector2(rect.Min.X + pad, valueTop - labelHeight),
            Typography.FitText(label, textWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(rect.Min.X + pad, valueTop),
            Typography.FitText(value, textWidth, TextStyles.Title3), ui.TitleInk, TextStyles.Title3);
    }

    private enum StatKind : byte
    {
        Count,
        Distance,
        Duration,
        Streak,
        Energy,
    }

    private string Cached(int slot, double value, StatKind kind)
    {
        var quantized = kind == StatKind.Duration ? Math.Floor(value / 60d) : Math.Round(value);
        var key = ((long)quantized << 8) | ((long)kind << 4) | (long)Units;
        if (statTexts[slot].IsCurrent(key))
        {
            return statTexts[slot].Value;
        }

        return statTexts[slot].Store(key, kind switch
        {
            StatKind.Distance => HealthFormat.Distance(value, Units),
            StatKind.Duration => HealthFormat.Duration(value),
            StatKind.Streak => Loc.Plural(L.Health.StreakDayCount, (int)value),
            StatKind.Energy => Loc.T(L.Health.Kcal, value.ToString("0", Loc.Culture)),
            _ => HealthFormat.Number((long)value),
        });
    }

    private string StepGoalText(int goal)
    {
        return stepGoalText.IsCurrent(goal)
            ? stepGoalText.Value
            : stepGoalText.Store(goal, HealthFormat.Number(goal));
    }
}
