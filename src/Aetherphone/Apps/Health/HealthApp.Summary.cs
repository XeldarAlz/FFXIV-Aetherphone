using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Health;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Health;

internal sealed partial class HealthApp
{
    private const float HeroHeight = 156f;
    private const float VesselWidth = 62f;
    private const float VesselHeight = 100f;
    private const float HeroTextGap = 18f;
    private const float AddButtonRadius = RoundButton.RegularRadius;
    private const float HighlightGlyph = 22f;
    private const float HighlightBarHeight = 8f;
    private const float HighlightRowGap = 10f;
    private const float HighlightTrackAlpha = 0.14f;
    private const float HighlightPreviousAlpha = 0.45f;
    private const float MetricCardHeight = 92f;
    private const float MetricGlyph = 22f;
    private const float SparkWidth = 104f;
    private const float SparkHeight = 36f;
    private const float SparkBarWidth = 7f;
    private const float ChevronSize = 5f;
    private const float ChevronGap = 10f;

    private static readonly HealthMetric[] SummaryMetrics =
    {
        HealthMetric.Steps,
        HealthMetric.Distance,
        HealthMetric.Swimming,
        HealthMetric.ActiveTime,
    };

    private static readonly string[] MetricCardIds =
    {
        "health.card.water",
        "health.card.steps",
        "health.card.distance",
        "health.card.swimming",
        "health.card.active",
    };

    private static readonly string[] HighlightIds = { "health.highlight.0", "health.highlight.1", "health.highlight.2" };

    private readonly NavBarButton[] summaryButtons = new NavBarButton[2];
    private readonly CachedText[] cardValues = new CachedText[HealthSeries.MetricCount];
    private readonly CachedText[] cardUnits = new CachedText[HealthSeries.MetricCount];
    private readonly float[] sparkHeights = new float[HealthSeries.WeekDays];
    private CachedText heroVolume;
    private CachedText heroUnit;
    private CachedText heroLine;
    private CachedText addLabel;
    private CachedText weightCardValue;
    private Spring heroFill;

    private void SnapFills()
    {
        heroFill.SnapTo(0f);
        waterFill.SnapTo(0f);
    }

    private void DrawSummary(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (UiScope.PushId("health.summary"))
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawWaterHero(drawList, origin, width, scale);
            cursorY = DrawHighlights(drawList, origin.X, cursorY, width, scale);
            cursorY = DrawMetricCards(drawList, origin.X, cursorY, width, scale);
            cursorY += Footnote(new Vector2(origin.X, cursorY), width, Loc.T(L.Health.Disclaimer), scale);
            ReserveTo(origin, width, cursorY + HealthArt.BottomBreathing * scale);
        }

        summaryButtons[0] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.Bullseye), Loc.T(L.Health.Goals));
        summaryButtons[1] = new NavBarButton(PhoneIcons.Settings, Loc.T(L.Health.Settings));
        UiAnchors.Report("health.goals", AppHeader.LargeTitleButtonRect(in navBar, 0, 2));
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "health.nav.summary", DisplayName,
            NavBarStyle.From(ui), summaryButtons);
        if (pressed == 0)
        {
            Open(HealthView.Goals());
        }
        else if (pressed == 1)
        {
            Open(HealthView.Settings());
        }
    }

    private float DrawWaterHero(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + HeroHeight * scale));
        UiAnchors.Report("health.today", rect);
        var pad = HealthArt.CardPad * scale;
        var buttonRadius = AddButtonRadius * scale;
        var buttonCenter = new Vector2(rect.Max.X - pad - buttonRadius, rect.Max.Y - pad - buttonRadius);
        var overButton = UiInteract.Hover(buttonCenter - new Vector2(buttonRadius, buttonRadius),
            buttonCenter + new Vector2(buttonRadius, buttonRadius));
        if (PressableCard(drawList, rect, "health.hero", overButton, out var card))
        {
            Open(HealthView.Water());
        }

        var tint = HealthArt.Tint(HealthMetric.Water);
        var count = (int)digest.Live(HealthMetric.Water);
        var goal = Math.Max(1, Profile.DailyHydrationGoal);
        var fraction = heroFill.Step(count / (float)goal, Motion.Sheet, HealthArt.FrameDelta());
        var vesselSize = new Vector2(VesselWidth, VesselHeight) * scale;
        var vesselMin = new Vector2(card.Min.X + pad + Metrics.Space.Xs * scale, card.Center.Y - vesselSize.Y * 0.5f);
        HealthArt.Vessel(drawList, new Rect(vesselMin, vesselMin + vesselSize), fraction, tint, ui.TitleInk, scale);

        var textLeft = vesselMin.X + vesselSize.X + HeroTextGap * scale;
        var textWidth = MathF.Max(1f, card.Max.X - pad - textLeft);
        var eyebrowHeight = Typography.LineHeight(TextStyles.FootnoteEmphasized);
        var valueHeight = Typography.LineHeight(TextStyles.WidgetDisplayCompact);
        var top = card.Min.Y + pad;
        Typography.Draw(drawList, new Vector2(textLeft, top),
            Typography.FitText(Loc.Upper(Loc.T(L.Health.Water)), textWidth, TextStyles.FootnoteEmphasized), tint,
            TextStyles.FootnoteEmphasized);
        var millilitres = digest.Today?.DrinkMillilitres ?? 0d;
        var volumeKey = (long)Math.Round(millilitres) * 4L + (long)Units;
        if (!heroVolume.IsCurrent(volumeKey))
        {
            var (value, unit) = HealthFormat.VolumeParts(millilitres, Units);
            heroVolume.Store(volumeKey, value);
            heroUnit.Store(volumeKey, unit);
        }

        HealthArt.ValueWithUnit(drawList, new Vector2(textLeft, top + eyebrowHeight), textWidth, heroVolume.Value,
            heroUnit.Value, ui.TitleInk, tint, TextStyles.WidgetDisplayCompact);
        var reached = count >= goal;
        Typography.Draw(drawList, new Vector2(textLeft, top + eyebrowHeight + valueHeight),
            Typography.FitText(DrinksLine(count, goal), textWidth, TextStyles.Subheadline),
            reached ? tint : ui.MutedInk, TextStyles.Subheadline);
        if (AddButton(drawList, buttonCenter, buttonRadius, tint, "health.hero.add", AddLabel()))
        {
            LogServing();
        }

        return rect.Max.Y;
    }

    private string DrinksLine(int count, int goal)
    {
        var key = count * 10_000L + goal;
        if (heroLine.IsCurrent(key))
        {
            return heroLine.Value;
        }

        return heroLine.Store(key, count >= goal
            ? Loc.T(L.Health.GoalReached)
            : Loc.T(L.Health.DrinksOfGoal, count, goal));
    }

    private string AddLabel()
    {
        var key = (long)Math.Round(Profile.ServingMillilitres) * 64L + (long)Units * 8L +
                  Array.IndexOf(DrinkKeys.All, Profile.ServingKind);
        if (addLabel.IsCurrent(key))
        {
            return addLabel.Value;
        }

        return addLabel.Store(key, Loc.T(L.Health.AddServing, HealthFormat.Volume(Profile.ServingMillilitres, Units),
            DrinkName(Profile.ServingKind)));
    }

    private void LogServing()
    {
        tracker.LogDrink(Profile.ServingKind, string.Empty, Profile.ServingMillilitres);
        UiFeedback.Play(UiSound.Success);
    }

    private bool AddButton(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 tint, string id,
        string tooltip) =>
        GlyphButton(drawList, id, center, radius, FontAwesomeIcon.Plus, tint, ButtonStyle.Tinted, true, tooltip);

    private float DrawHighlights(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var cursorY = SectionTop(drawList, left, top, width, Loc.T(L.Health.Highlights), scale);
        if (digest.HighlightCount == 0)
        {
            return cursorY + HealthArt.State(drawList, ui, new Vector2(left, cursorY), width,
                FontAwesomeIcon.ChartBar, ui.Accent, Loc.T(L.Health.HighlightsEmptyTitle),
                Loc.T(L.Health.HighlightsEmptyBody), scale);
        }

        for (var index = 0; index < digest.HighlightCount; index++)
        {
            if (index > 0)
            {
                cursorY += HealthArt.TileGap * scale;
            }

            cursorY = DrawHighlight(drawList, new Vector2(left, cursorY), width, index, scale);
        }

        return cursorY;
    }

    private float DrawHighlight(ImDrawListPtr drawList, Vector2 origin, float width, int index, float scale)
    {
        ref readonly var highlight = ref digest.Highlights[index];
        var pad = HealthArt.CardPad * scale;
        var textWidth = MathF.Max(1f, width - pad * 2f);
        var headerHeight = HighlightGlyph * scale;
        var bodyHeight = Typography.MeasureWrappedBlock(highlight.Text, TextStyles.Headline, textWidth).Y;
        var labelHeight = Typography.LineHeight(TextStyles.Footnote);
        var rowHeight = labelHeight + HighlightBarHeight * scale;
        var height = pad * 2f + headerHeight + HealthArt.TileGap * scale + bodyHeight + HealthArt.TileGap * scale +
                     rowHeight * 2f + HighlightRowGap * scale;
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + height));
        if (PressableCard(drawList, rect, HighlightIds[index], false, out var card))
        {
            Open(highlight.IsWeight ? HealthView.Weight() : HealthView.Of((HealthMetric)highlight.Metric));
        }

        var tint = highlight.IsWeight ? HealthArt.WeightTint : HealthArt.Tint((HealthMetric)highlight.Metric);
        var icon = highlight.IsWeight ? FontAwesomeIcon.Weight : HealthArt.Icon((HealthMetric)highlight.Metric);
        var name = highlight.IsWeight ? Loc.T(L.Health.Weight) : HealthText.Name((HealthMetric)highlight.Metric);
        var left = card.Min.X + pad;
        var cursorY = card.Min.Y + pad;
        DrawCategory(drawList, new Vector2(left, cursorY), textWidth, tint, icon, name, string.Empty, scale);
        cursorY += headerHeight + HealthArt.TileGap * scale;
        cursorY += Typography.DrawWrappedLeft(new Vector2(left, cursorY), highlight.Text, ui.TitleInk,
            TextStyles.Headline, textWidth) + HealthArt.TileGap * scale;
        var recentLabel = Loc.T(highlight.IsWeight ? L.Health.Latest : L.Health.ThisWeekAverage);
        var previousLabel = Loc.T(highlight.IsWeight ? L.Health.PeriodStart : L.Health.LastWeekAverage);
        cursorY = DrawComparison(drawList, left, cursorY, textWidth, recentLabel, highlight.RecentValue,
            highlight.RecentFraction, tint, scale);
        cursorY += HighlightRowGap * scale;
        DrawComparison(drawList, left, cursorY, textWidth, previousLabel, highlight.PreviousValue,
            highlight.PreviousFraction, Palette.WithAlpha(ui.MutedInk, HighlightPreviousAlpha), scale);
        return rect.Max.Y;
    }

    private float DrawComparison(ImDrawListPtr drawList, float left, float top, float width, string label,
        string value, float fraction, Vector4 tint, float scale)
    {
        var labelHeight = Typography.LineHeight(TextStyles.Footnote);
        var valueSize = Typography.Measure(value, TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(left + width - valueSize.X, top), value, ui.TitleInk,
            TextStyles.FootnoteEmphasized);
        Typography.Draw(drawList, new Vector2(left, top),
            Typography.FitText(label, MathF.Max(1f, width - valueSize.X - HealthArt.TileGap * scale),
                TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        var barTop = top + labelHeight + HealthArt.LineGap * scale;
        var barHeight = HighlightBarHeight * scale;
        var rounding = barHeight * 0.5f;
        drawList.AddRectFilled(new Vector2(left, barTop), new Vector2(left + width, barTop + barHeight),
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, HighlightTrackAlpha)), rounding);
        var fill = Math.Clamp(fraction, 0f, 1f);
        if (fill > 0.001f)
        {
            drawList.AddRectFilled(new Vector2(left, barTop),
                new Vector2(left + MathF.Max(barHeight, width * fill), barTop + barHeight), ImGui.GetColorU32(tint),
                rounding);
        }

        return barTop + barHeight;
    }

    private void DrawCategory(ImDrawListPtr drawList, Vector2 origin, float width, Vector4 tint, FontAwesomeIcon icon,
        string name, string trailing, float scale)
    {
        var glyph = HighlightGlyph * scale;
        var centerY = origin.Y + glyph * 0.5f;
        HealthArt.GlyphTile(drawList, new Vector2(origin.X + glyph * 0.5f, centerY), glyph, tint, icon);
        var textLeft = origin.X + glyph + Metrics.Space.Sm * scale;
        var trailingWidth = 0f;
        if (trailing.Length > 0)
        {
            var trailingSize = Typography.Measure(trailing, TextStyles.Footnote);
            trailingWidth = trailingSize.X + Metrics.Space.Sm * scale;
            Typography.Draw(drawList, new Vector2(origin.X + width - trailingSize.X, centerY - trailingSize.Y * 0.5f),
                trailing, ui.MutedInk, TextStyles.Footnote);
        }

        var nameText = Typography.FitText(name, MathF.Max(1f, origin.X + width - trailingWidth - textLeft),
            TextStyles.FootnoteEmphasized);
        var nameHeight = Typography.Measure(nameText, TextStyles.FootnoteEmphasized).Y;
        Typography.Draw(drawList, new Vector2(textLeft, centerY - nameHeight * 0.5f), nameText, tint,
            TextStyles.FootnoteEmphasized);
    }

    private float DrawMetricCards(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var cursorY = SectionTop(drawList, left, top, width, Loc.T(L.Health.YourMetrics), scale);
        var sectionTop = cursorY;
        cursorY = DrawMetricCard(drawList, new Vector2(left, cursorY), width, SummaryMetrics[0], scale);
        cursorY = DrawWeightCard(drawList, new Vector2(left, cursorY + HealthArt.TileGap * scale), width, scale);
        for (var index = 1; index < SummaryMetrics.Length; index++)
        {
            cursorY = DrawMetricCard(drawList, new Vector2(left, cursorY + HealthArt.TileGap * scale), width,
                SummaryMetrics[index], scale);
        }

        UiAnchors.Report("health.metrics", new Rect(new Vector2(left, sectionTop), new Vector2(left + width, cursorY)));
        return cursorY;
    }

    private float DrawMetricCard(ImDrawListPtr drawList, Vector2 origin, float width, HealthMetric metric,
        float scale)
    {
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + MetricCardHeight * scale));
        if (!ImGui.IsRectVisible(rect.Min, rect.Max))
        {
            return rect.Max.Y;
        }

        if (PressableCard(drawList, rect, MetricCardIds[(int)metric], false, out var card))
        {
            Open(HealthView.Of(metric));
        }

        var tint = HealthArt.Tint(metric);
        var live = digest.Live(metric);
        var (value, unit) = CardText(metric, live);
        var series = digest.Series(metric, HealthSeries.WeekDays);
        var recorded = digest.SeriesRecorded(metric, HealthSeries.WeekDays);
        var peak = MathF.Max(1f, (float)HealthSeries.Max(series));
        for (var slot = 0; slot < HealthSeries.WeekDays; slot++)
        {
            sparkHeights[slot] = (float)(series[slot] / peak);
        }

        DrawCardFrame(drawList, card, tint, HealthArt.Icon(metric), HealthText.Name(metric), Loc.T(L.Time.Today),
            value, unit, scale, out var spark);
        HealthArt.Bars(drawList, spark, sparkHeights, recorded, HealthSeries.WeekDays - 1, -1f, tint, ui.MutedInk,
            SparkBarWidth * scale, scale);
        return rect.Max.Y;
    }

    private float DrawWeightCard(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var rect = new Rect(origin, new Vector2(origin.X + width, origin.Y + MetricCardHeight * scale));
        if (PressableCard(drawList, rect, "health.card.weight", false, out var card))
        {
            Open(HealthView.Weight());
        }

        var latest = digest.LatestWeight;
        var value = latest is null ? Loc.T(L.Health.NoData) : WeightCardValue(latest.Kilograms);
        var unit = latest is null ? string.Empty : HealthFormat.WeightUnit(Units);
        DrawCardFrame(drawList, card, HealthArt.WeightTint, FontAwesomeIcon.Weight, Loc.T(L.Health.Weight),
            digest.LatestWeightDate, value, unit, scale, out var spark);
        HealthArt.Line(drawList, spark, digest.WeightSpark, digest.WeightSparkPresent, digest.WeightSparkLow,
            digest.WeightSparkHigh, HealthArt.WeightTint, -1, scale);
        return rect.Max.Y;
    }

    private string WeightCardValue(double kilograms)
    {
        var key = (long)Math.Round(kilograms * 100d) * 4L + (long)Units;
        return weightCardValue.IsCurrent(key)
            ? weightCardValue.Value
            : weightCardValue.Store(key, HealthFormat.WeightNumber(kilograms, Units));
    }

    private void DrawCardFrame(ImDrawListPtr drawList, Rect card, Vector4 tint, FontAwesomeIcon icon, string name,
        string trailing, string value, string unit, float scale, out Rect spark)
    {
        var pad = HealthArt.CardPad * scale;
        var chevron = ChevronSize * scale;
        var right = card.Max.X - pad;
        HealthArt.Chevron(drawList, new Vector2(right - chevron * 0.5f, card.Min.Y + pad + HighlightGlyph * scale * 0.5f),
            chevron, ui.MutedInk, scale);
        var headerWidth = MathF.Max(1f, right - chevron - ChevronGap * scale - card.Min.X - pad);
        DrawCategory(drawList, new Vector2(card.Min.X + pad, card.Min.Y + pad), headerWidth, tint, icon, name,
            trailing, scale);
        var sparkSize = new Vector2(SparkWidth, SparkHeight) * scale;
        spark = new Rect(new Vector2(right - sparkSize.X, card.Max.Y - pad - sparkSize.Y),
            new Vector2(right, card.Max.Y - pad));
        var valueHeight = Typography.LineHeight(TextStyles.Title2);
        var valueWidth = MathF.Max(1f, spark.Min.X - HealthArt.TileGap * scale - card.Min.X - pad);
        HealthArt.ValueWithUnit(drawList, new Vector2(card.Min.X + pad, card.Max.Y - pad - valueHeight), valueWidth,
            value, unit, ui.TitleInk, ui.MutedInk, TextStyles.Title2);
    }

    private (string Value, string Unit) CardText(HealthMetric metric, double live)
    {
        var index = (int)metric;
        var key = (long)Math.Round(metric == HealthMetric.ActiveTime ? live / 60d : live) * 4L + (long)Units;
        if (!cardValues[index].IsCurrent(key))
        {
            var (value, unit) = HealthText.Metric(metric, live, Units, false);
            cardValues[index].Store(key, value);
            cardUnits[index].Store(key, unit);
        }

        return (cardValues[index].Value, cardUnits[index].Value);
    }

    private static string DrinkName(string kindKey) => Loc.T(kindKey switch
    {
        DrinkKeys.Tea => L.Health.DrinkKindTea,
        DrinkKeys.Coffee => L.Health.DrinkKindCoffee,
        DrinkKeys.Juice => L.Health.DrinkKindJuice,
        _ => L.Health.DrinkKindWater,
    });
}
