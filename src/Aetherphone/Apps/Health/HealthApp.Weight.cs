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
    private const float WeightChartHeader = 72f;
    private const float WeightPlotHeight = 150f;
    private const float WeightAxisHeight = 22f;
    private const float WeightRangeWidth = 176f;
    private const float AxisLabelGap = 8f;
    private const float LogPillHeight = Button.LargeHeight;
    private const float HistoryRowHeight = 48f;
    private const float SheetPadX = 20f;
    private const float SheetTitleHeight = 30f;
    private const float SheetValueHeight = 64f;
    private const float SheetRulerHeight = 72f;
    private const float SheetGap = 14f;
    private const float SheetButtonHeight = Button.LargeHeight;
    private const float SheetStepRadius = 20f;
    private const float RulerStepPixels = 9f;
    private const float RulerMajorHeight = 26f;
    private const float RulerHalfHeight = 18f;
    private const float RulerMinorHeight = 11f;
    private const float RulerNeedleWidth = 3f;
    private const double DisplayStep = 0.1d;
    private const int MajorEvery = 10;
    private const int HalfEvery = 5;

    private static readonly StepperIds SheetWeightIds = StepperIds.For("health.weight.sheet");

    private readonly WeightDigest weightDigest = new();
    private readonly Sheet weightSheet = new();
    private readonly string[] weightRangeLabels = new string[WeightHistory.RangeCount];
    private readonly List<string> weightRemoveIds = new();
    private WeightRange weightRange = WeightRange.Month;
    private double weightDraft = WeightHistory.DefaultKilograms;
    private bool rulerDragging;
    private float rulerGrabX;
    private double rulerGrabValue;
    private readonly Dictionary<long, string> rulerLabels = new();
    private CachedText draftText;

    private void DrawWeight(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        weightDigest.Refresh(tracker, weightRange);
        using (UiScope.PushId("health.weight"))
        using (AppSurface.Begin(navBar.Body))
        {
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = origin.Y;
            if (Profile.WeightLog.Count == 0)
            {
                cursorY += HealthArt.State(drawList, ui, origin, width, FontAwesomeIcon.Weight, HealthArt.WeightTint,
                    Loc.T(L.Health.WeightEmptyTitle), Loc.T(L.Health.WeightEmptyBody), scale);
            }
            else
            {
                cursorY = DrawWeightChart(drawList, origin, width, scale);
            }

            cursorY += HealthArt.TileGap * scale;
            var pill = new Rect(new Vector2(origin.X, cursorY),
                new Vector2(origin.X + width, cursorY + LogPillHeight * scale));
            if (Button.Draw(pill, Loc.T(L.Health.LogWeight), ui.Ink))
            {
                OpenWeightSheet();
            }

            cursorY = DrawWeightHistory(drawList, origin.X, pill.Max.Y, width, scale);
            cursorY += Footnote(new Vector2(origin.X, cursorY), width, Loc.T(L.Health.WeightNote), scale);
            ReserveTo(origin, width, cursorY + HealthArt.BottomBreathing * scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "health.nav.weight", Loc.T(L.Health.Weight), NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, DisplayName, back);
    }

    private float DrawWeightChart(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var pad = HealthArt.CardPad * scale;
        var height = pad * 2f + (WeightChartHeader + WeightPlotHeight + WeightAxisHeight) * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var left = origin.X + pad;
        var right = max.X - pad;
        var headerTop = origin.Y + pad;
        var stripWidth = MathF.Min(WeightRangeWidth * scale, (right - left) * 0.6f);
        var strip = new Rect(new Vector2(right - stripWidth, headerTop),
            new Vector2(right, headerTop + RangeStripHeight * scale));
        weightRangeLabels[0] = Loc.T(L.Health.RangeWeek);
        weightRangeLabels[1] = Loc.T(L.Health.RangeMonth);
        weightRangeLabels[2] = Loc.T(L.Health.RangeHalfYear);
        weightRangeLabels[3] = Loc.T(L.Health.RangeYear);
        var selected = SegmentStrip.Draw("health.weight.range", strip, weightRangeLabels, (int)weightRange, ui.Palette,
            RangeStripHeight);
        if (selected != (int)weightRange)
        {
            UiFeedback.Play(UiSound.Tap);
            weightRange = (WeightRange)selected;
            weightDigest.Refresh(tracker, weightRange);
        }

        var count = weightDigest.Buckets;
        var labelWidth = MathF.Max(WidgetText.TabularWidth(weightDigest.HighText, TextStyles.Footnote),
            WidgetText.TabularWidth(weightDigest.LowText, TextStyles.Footnote));
        var plotTop = headerTop + WeightChartHeader * scale;
        var plot = new Rect(new Vector2(left, plotTop),
            new Vector2(right - labelWidth - AxisLabelGap * scale, plotTop + WeightPlotHeight * scale));
        var stepX = count > 1 ? plot.Width / (count - 1) : plot.Width;
        var scrub = -1;
        var hover = new Rect(new Vector2(plot.Min.X - stepX * 0.5f, plot.Min.Y),
            new Vector2(plot.Max.X + stepX * 0.5f, plot.Max.Y));
        if (UiInteract.Hover(hover.Min, hover.Max))
        {
            scrub = Math.Clamp((int)MathF.Round((ImGui.GetMousePos().X - plot.Min.X) / stepX), 0, count - 1);
            if (!weightDigest.Present[scrub])
            {
                scrub = -1;
            }
        }

        DrawWeightGuides(drawList, plot, right, scale);
        HealthArt.Line(drawList, plot, weightDigest.Values.AsSpan(0, count), weightDigest.Present.AsSpan(0, count),
            weightDigest.Low, weightDigest.High, HealthArt.WeightTint, scrub, scale);
        if (scrub >= 0)
        {
            HealthArt.ScrubLine(drawList, plot.Min.X + stepX * scrub, plot.Min.Y, plot.Max.Y, ui.TitleInk, scale);
        }

        var headerWidth = MathF.Max(1f, strip.Min.X - left - HealthArt.TileGap * scale);
        var label = scrub >= 0 ? weightDigest.BucketLabels[scrub] : weightDigest.LatestDate;
        var value = scrub >= 0 ? weightDigest.BucketValues[scrub] : weightDigest.LatestValue;
        var unit = scrub >= 0 ? string.Empty : HealthFormat.WeightUnit(Units);
        Typography.Draw(drawList, new Vector2(left, headerTop),
            Typography.FitText(Loc.Upper(label), headerWidth, TextStyles.FootnoteEmphasized), ui.MutedInk,
            TextStyles.FootnoteEmphasized);
        var valueTop = headerTop + Typography.LineHeight(TextStyles.FootnoteEmphasized);
        HealthArt.ValueWithUnit(drawList, new Vector2(left, valueTop), headerWidth, value, unit, ui.TitleInk,
            HealthArt.WeightTint, TextStyles.Title1);
        if (scrub < 0 && weightDigest.ChangeText.Length > 0)
        {
            Typography.Draw(drawList, new Vector2(left, valueTop + Typography.LineHeight(TextStyles.Title1)),
                Typography.FitText(weightDigest.ChangeText, right - left, TextStyles.Footnote), ui.MutedInk,
                TextStyles.Footnote);
        }

        var axisTop = plot.Max.Y + Metrics.Space.Xs * scale;
        Typography.Draw(drawList, new Vector2(plot.Min.X, axisTop), weightDigest.AxisStart, ui.MutedInk,
            TextStyles.Footnote);
        var today = Loc.T(L.Time.Today);
        Typography.Draw(drawList, new Vector2(plot.Max.X - Typography.Measure(today, TextStyles.Footnote).X, axisTop),
            today, ui.MutedInk, TextStyles.Footnote);
        return max.Y;
    }

    private void DrawWeightGuides(ImDrawListPtr drawList, Rect plot, float right, float scale)
    {
        if (weightDigest.Filled == 0)
        {
            return;
        }

        var span = MathF.Max(0.001f, weightDigest.High - weightDigest.Low);
        var guideInk = Palette.WithAlpha(ui.TitleInk, 0.12f);
        DrawGuide(drawList, plot, right, weightDigest.HighText, weightDigest.DataHigh, span, guideInk, scale);
        DrawGuide(drawList, plot, right, weightDigest.LowText, weightDigest.DataLow, span, guideInk, scale);
    }

    private void DrawGuide(ImDrawListPtr drawList, Rect plot, float right, string text, float value, float span,
        Vector4 ink, float scale)
    {
        var y = plot.Max.Y - (value - weightDigest.Low) / span * plot.Height;
        Market.MarketArt.DashedLine(drawList, plot.Min.X, plot.Max.X, y, ink, scale);
        var height = Typography.LineHeight(TextStyles.Footnote);
        WidgetText.TabularRight(drawList, right, y - height * 0.5f, text, ui.MutedInk, TextStyles.Footnote);
    }

    private float DrawWeightHistory(ImDrawListPtr drawList, float left, float top, float width, float scale)
    {
        var rows = weightDigest.Rows;
        if (rows == 0)
        {
            return top;
        }

        var cursorY = SectionTop(drawList, left, top, width, Loc.T(L.Health.History), scale);
        var rowHeight = HistoryRowHeight * scale;
        var max = new Vector2(left + width, cursorY + rowHeight * rows);
        ui.Card(drawList, new Vector2(left, cursorY), max, Metrics.Radius.Grouped * scale);
        while (weightRemoveIds.Count < rows)
        {
            weightRemoveIds.Add("health.weight.remove." + weightRemoveIds.Count);
        }

        var pad = HealthArt.CardPad * scale;
        WeightEntry? removed = null;
        for (var row = 0; row < rows; row++)
        {
            var rowTop = cursorY + row * rowHeight;
            if (row > 0)
            {
                drawList.AddLine(new Vector2(left + pad, rowTop), new Vector2(max.X, rowTop),
                    ImGui.GetColorU32(ui.Hairline), Metrics.Stroke.Hairline);
            }

            var centerY = rowTop + rowHeight * 0.5f;
            var removeCenter = new Vector2(max.X - pad - RemoveRadius * scale, centerY);
            if (GlyphButton(drawList, weightRemoveIds[row], removeCenter, RemoveRadius * scale,
                    FontAwesomeIcon.Times, ui.Accent, ButtonStyle.Plain, true, Loc.T(L.Health.RemoveEntry), ui.MutedInk))
            {
                removed = weightDigest.RowEntries[row];
            }

            var rowRect = new Rect(new Vector2(left + pad, rowTop),
                new Vector2(removeCenter.X - RemoveRadius * scale - Metrics.Space.Md * scale, rowTop + rowHeight));
            var valueSize = Typography.Measure(weightDigest.RowValues[row], TextStyles.BodyEmphasized);
            Typography.Draw(drawList, new Vector2(rowRect.Max.X - valueSize.X, centerY - valueSize.Y * 0.5f),
                weightDigest.RowValues[row], ui.TitleInk, TextStyles.BodyEmphasized);
            RowLabel(drawList, rowRect, weightDigest.RowDates[row], valueSize.X + HealthArt.TileGap * scale,
                ui.MutedInk);
        }

        if (removed is not null)
        {
            tracker.RemoveWeight(removed);
            UiFeedback.Play(UiSound.ToggleOff);
        }

        return max.Y;
    }

    private void OpenWeightSheet()
    {
        var latest = Profile.WeightLog.Count > 0
            ? Profile.WeightLog[Profile.WeightLog.Count - 1].Kilograms
            : WeightHistory.DefaultKilograms;
        weightDraft = WeightHistory.Snap(HealthFormat.WeightFromKg(latest, Units), DisplayStep);
        rulerDragging = false;
        weightSheet.Open();
    }

    private void DrawWeightSheet(Rect screen)
    {
        if (!weightSheet.CapturesPointer)
        {
            return;
        }

        var scale = UiScale.Current;
        var height = (SheetMetrics.GrabberZone + SheetTitleHeight + SheetGap + SheetValueHeight + SheetGap +
                      SheetRulerHeight + SheetGap * 2f + SheetButtonHeight + Metrics.Size.HomeIndicatorInset) * scale;
        using var layer = ScreenLayer.Begin("health.weightSheet", screen, false);
        var frame = weightSheet.Begin(ImGui.GetWindowDrawList(), screen, theme, SheetDetents.Fitted(height),
            SheetMetrics.AppVeil);
        if (!frame.Visible)
        {
            return;
        }

        var drawList = frame.DrawList;
        var content = frame.Content;
        var ink = frame.Ink with { W = frame.Ink.W * frame.Opacity };
        var left = content.Min.X + SheetPadX * scale;
        var right = content.Max.X - SheetPadX * scale;
        var y = content.Min.Y;
        Typography.DrawCentered(drawList, new Vector2(content.Center.X, y + SheetTitleHeight * scale * 0.5f),
            Loc.T(L.Health.LogWeight), ink, TextStyles.Headline);
        y += (SheetTitleHeight + SheetGap) * scale;
        var range = DisplayRange();
        var valueRow = new Rect(new Vector2(left, y), new Vector2(right, y + SheetValueHeight * scale));
        var delta = Stepper(drawList, valueRow, SheetWeightIds, DraftText(), weightDraft > range.Min,
            weightDraft < range.Max, HealthArt.WeightTint, ink, TextStyles.WidgetDisplayCompact, scale);
        if (delta != 0)
        {
            weightDraft = Math.Clamp(WeightHistory.Snap(weightDraft + delta * DisplayStep, DisplayStep), range.Min,
                range.Max);
        }

        y += (SheetValueHeight + SheetGap) * scale;
        var ruler = new Rect(new Vector2(content.Min.X, y), new Vector2(content.Max.X, y + SheetRulerHeight * scale));
        DrawRuler(drawList, ruler, ink, range, frame.Interactive, scale);
        y += (SheetRulerHeight + SheetGap * 2f) * scale;
        var button = new Rect(new Vector2(left, y), new Vector2(right, y + SheetButtonHeight * scale));
        if (frame.Interactive && Button.Draw(button, Loc.T(L.Health.Add), ui.Ink))
        {
            tracker.LogWeight(HealthFormat.WeightToKg(weightDraft, Units));
            UiFeedback.Play(UiSound.Success);
            weightSheet.Close();
        }

        weightSheet.End(in frame);
    }

    private (double Min, double Max) DisplayRange() =>
        (Math.Ceiling(HealthFormat.WeightFromKg(WeightHistory.MinKilograms, Units)),
            Math.Floor(HealthFormat.WeightFromKg(WeightHistory.MaxKilograms, Units)));

    private void DrawRuler(ImDrawListPtr drawList, Rect ruler, Vector4 ink, (double Min, double Max) range,
        bool interactive, float scale)
    {
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(ruler.Min);
        ImGui.InvisibleButton("##health.ruler", ruler.Size, ImGuiButtonFlags.MouseButtonLeft);
        var active = interactive && ImGui.IsItemActive();
        var hovered = interactive && ImGui.IsItemHovered() && UiInteract.Hover(ruler.Min, ruler.Max);
        ImGui.SetCursorScreenPos(cursor);
        var stepPixels = RulerStepPixels * scale;
        if (hovered && ImGui.IsItemActivated())
        {
            rulerDragging = true;
            rulerGrabX = ImGui.GetMousePos().X;
            rulerGrabValue = weightDraft;
        }

        if (rulerDragging && active)
        {
            weightSheet.YieldPointer();
            var travel = ImGui.GetMousePos().X - rulerGrabX;
            weightDraft = Math.Clamp(rulerGrabValue - travel / stepPixels * DisplayStep, range.Min, range.Max);
        }
        else if (rulerDragging)
        {
            rulerDragging = false;
            weightDraft = WeightHistory.Snap(weightDraft, DisplayStep);
            UiFeedback.Play(UiSound.Tap);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);
            var wheel = ImGui.GetIO().MouseWheel;
            if (wheel != 0f && !rulerDragging)
            {
                weightDraft = Math.Clamp(WeightHistory.Snap(weightDraft + MathF.Sign(wheel) * DisplayStep, DisplayStep),
                    range.Min, range.Max);
            }
        }

        var centerX = ruler.Center.X;
        var halfWidth = ruler.Width * 0.5f;
        var baseY = ruler.Center.Y + RulerMajorHeight * 0.5f * scale;
        var firstStep = (long)Math.Floor((weightDraft - halfWidth / stepPixels * DisplayStep) / DisplayStep);
        var lastStep = (long)Math.Ceiling((weightDraft + halfWidth / stepPixels * DisplayStep) / DisplayStep);
        for (var step = firstStep; step <= lastStep; step++)
        {
            var value = step * DisplayStep;
            if (value < range.Min - 0.0001d || value > range.Max + 0.0001d)
            {
                continue;
            }

            var x = centerX + (float)((value - weightDraft) / DisplayStep) * stepPixels;
            var fade = Math.Clamp(1f - MathF.Abs(x - centerX) / halfWidth, 0f, 1f);
            var major = step % MajorEvery == 0;
            var half = !major && step % HalfEvery == 0;
            var tickHeight = (major ? RulerMajorHeight : half ? RulerHalfHeight : RulerMinorHeight) * scale;
            drawList.AddLine(new Vector2(x, baseY - tickHeight), new Vector2(x, baseY),
                ImGui.GetColorU32(Palette.WithAlpha(ink, ink.W * fade * (major ? 0.8f : 0.4f))),
                Metrics.Stroke.Thin * scale);
            if (!major)
            {
                continue;
            }

            var label = RulerLabel(step / MajorEvery);
            var size = Typography.Measure(label, TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(x - size.X * 0.5f, baseY + Metrics.Space.Xxs * scale), label,
                Palette.WithAlpha(ink, ink.W * fade * 0.7f), TextStyles.Footnote);
        }

        var needleTop = baseY - (RulerMajorHeight + Metrics.Space.Xs) * scale;
        drawList.AddRectFilled(new Vector2(centerX - RulerNeedleWidth * 0.5f * scale, needleTop),
            new Vector2(centerX + RulerNeedleWidth * 0.5f * scale, baseY + Metrics.Space.Xxs * scale),
            ImGui.GetColorU32(HealthArt.WeightTint), RulerNeedleWidth * scale);
    }

    private string RulerLabel(long whole)
    {
        if (rulerLabels.TryGetValue(whole, out var cached))
        {
            return cached;
        }

        var label = whole.ToString(Loc.Culture);
        rulerLabels[whole] = label;
        return label;
    }

    private string DraftText()
    {
        var key = (long)Math.Round(weightDraft * 10d) * 4L + (long)Units;
        return draftText.IsCurrent(key)
            ? draftText.Value
            : draftText.Store(key,
                string.Concat(weightDraft.ToString("0.0", Loc.Culture), " ", HealthFormat.WeightUnit(Units)));
    }
}
