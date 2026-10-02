using Aetherphone.Core;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Housing;

internal sealed partial class HousingApp
{
    private static float SheetHeightFor(bool reminderPicker, float scale)
    {
        var height = 18f * scale;
        height += Typography.LineHeight(TextStyles.Title3) + 2f * scale;
        height += Typography.LineHeight(TextStyles.Footnote) + 9f * scale;
        height += HousingChrome.ChipHeight * scale + 10f * scale;
        height += Typography.LineHeight(TextStyles.Caption2) + 2f * scale +
                  Typography.LineHeight(TextStyles.Title3) + 12f * scale;
        height += HousingChrome.StatRowHeight * scale * 3f + 10f * scale;
        height += reminderPicker
            ? Typography.LineHeight(TextStyles.Caption1) + 12f * scale + 30f * scale + 22f * scale + 32f * scale
            : 72f * scale;
        return height + 16f * scale;
    }

    private void DrawSheet(Rect area, Rect viewport, float scale)
    {
        var progress = sheetSpring.Value;
        if (progress <= 0.005f)
        {
            return;
        }

        var plot = FindPlot(selectedPlot);
        if (plot is null)
        {
            sheetOpen = false;
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var height = MathF.Min(SheetHeightFor(reminderPickerOpen, scale), area.Height * 0.72f);
        var travel = height * (1f - progress);
        var sheet = new Rect(new Vector2(area.Min.X, area.Max.Y - height + travel),
            new Vector2(area.Max.X, area.Max.Y + travel));
        drawList.PushClipRect(area.Min, area.Max, true);
        HousingChrome.SheetChrome(drawList, sheet, viewport, progress, ui);
        drawList.PopClipRect();
        if (progress < 0.35f)
        {
            return;
        }

        var pad = 16f * scale;
        var contentLeft = sheet.Min.X + pad;
        var contentRight = sheet.Max.X - pad;
        var contentWidth = contentRight - contentLeft;
        var y = sheet.Min.Y + 18f * scale;

        var titleStyle = TextStyles.Title3;
        var title = HousingFormat.PlotTitle(plot);
        var closeRadius = 11f * scale;
        var closeCenter = new Vector2(contentRight - closeRadius, y + closeRadius);
        Typography.Draw(drawList, new Vector2(contentLeft, y),
            Typography.FitText(title, contentWidth - closeRadius * 2.6f, titleStyle), ui.TitleInk, titleStyle);
        if (HousingChrome.CloseButton(closeCenter, closeRadius, ui, reminderPickerOpen))
        {
            sheetOpen = false;
            reminderPickerOpen = false;
        }

        y += Typography.LineHeight(titleStyle) + 2f * scale;
        var place = string.Concat(HousingFormat.Place(housing.DistrictName, plot.Key.Ward), " · ",
            HousingFormat.DivisionLabel(plot.IsSubdivision));
        Typography.Draw(drawList, new Vector2(contentLeft, y),
            Typography.FitText(place, contentWidth, TextStyles.Footnote), ui.MutedInk, TextStyles.Footnote);
        y += Typography.LineHeight(TextStyles.Footnote) + 9f * scale;

        var freshness = FreshnessOf(plot);
        var chipX = contentLeft;
        var chipLabel = HousingFormat.FreshnessLabel(freshness);
        HousingChrome.Chip(drawList, new Vector2(chipX, y), chipLabel,
            HousingChrome.FreshnessHue(freshness, ui.Accent), false);
        chipX += HousingChrome.MeasureChip(chipLabel) + 6f * scale;
        var phaseLabel = HousingFormat.PhaseLabel(plot.Phase);
        HousingChrome.Chip(drawList, new Vector2(chipX, y), phaseLabel,
            HousingMarkers.PhaseColor(plot.Phase, ui.Accent), false);
        chipX += HousingChrome.MeasureChip(phaseLabel) + 6f * scale;
        if (housing.Watch.IsWatched(plot.Key))
        {
            HousingChrome.Chip(drawList, new Vector2(chipX, y), Loc.T(L.Housing.Watching),
                AppPalettes.HousingBrass, false);
        }

        y += HousingChrome.ChipHeight * scale + 10f * scale;

        var now = DateTime.UtcNow;
        var countdown = HousingFormat.PhaseCountdown(plot, now);
        Typography.Draw(drawList, new Vector2(contentLeft, y),
            Loc.Culture.TextInfo.ToUpper(HousingFormat.PhaseLabel(plot.Phase)), ui.HeaderInk, TextStyles.Caption2);
        var countdownStyle = TextStyles.Title3;
        Typography.Draw(drawList, new Vector2(contentLeft, y + 11f * scale),
            Typography.FitText(countdown, contentWidth * 0.62f, countdownStyle), ui.TitleInk, countdownStyle);
        var scanText = HousingFormat.ScanAge(plot.LastSeenUtc, now);
        var scanSize = Typography.Measure(scanText, TextStyles.Footnote);
        Typography.Draw(drawList, new Vector2(contentRight - scanSize.X, y + 15f * scale), scanText,
            freshness == HousingDataFreshness.Stale ? AppPalettes.HousingResults : ui.MutedInk, TextStyles.Footnote);
        y += Typography.LineHeight(TextStyles.Caption2) + 2f * scale + Typography.LineHeight(TextStyles.Title3) +
             12f * scale;

        var rowHeight = HousingChrome.StatRowHeight * scale;
        HousingChrome.StatRow(drawList, new Rect(new Vector2(contentLeft, y), new Vector2(contentRight, y + rowHeight)),
            Loc.T(L.Housing.EntriesLabel), HousingFormat.Entries(plot.Entries), ui, true);
        y += rowHeight;
        HousingChrome.StatRow(drawList, new Rect(new Vector2(contentLeft, y), new Vector2(contentRight, y + rowHeight)),
            Loc.T(L.Housing.PriceLabel), HousingFormat.Price(plot.Price), ui);
        y += rowHeight;
        HousingChrome.StatRow(drawList, new Rect(new Vector2(contentLeft, y), new Vector2(contentRight, y + rowHeight)),
            Loc.T(L.Housing.EligibilityLabel), HousingFormat.EligibilityLabel(plot.Eligibility), ui);
        y += rowHeight + 10f * scale;

        if (reminderPickerOpen)
        {
            DrawReminderPicker(drawList, plot, contentLeft, contentRight, y, scale);
            return;
        }

        DrawSheetActions(plot, contentLeft, contentRight, y, scale);
    }

    private void DrawSheetActions(HousingPlot plot, float left, float right, float travelTop, float scale)
    {
        var height = 32f * scale;
        var gap = 8f * scale;
        var travelRow = new Rect(new Vector2(left, travelTop), new Vector2(right, travelTop + height));
        if (HousingChrome.PillButton(travelRow, Loc.T(L.Housing.TravelHere), true, ui, false))
        {
            TravelTo(plot.Key);
        }

        var top = travelRow.Max.Y + gap;
        var watched = housing.Watch.IsWatched(plot.Key);
        var reminder = housing.Watch.FindReminder(plot.Key);
        var hasDeadline = plot.PhaseEndsUtc is not null;
        var watchLabel = Loc.T(watched ? L.Housing.Watching : L.Housing.Watch);
        var remindLabel = reminder is { Notified: false }
            ? Loc.T(L.Housing.ReminderSet)
            : Loc.T(L.Housing.RemindMe);
        var detailsLabel = Loc.T(L.Housing.DetailsAction);
        Span<string> labels = [watchLabel, remindLabel, detailsLabel];
        Span<Rect> rects = stackalloc Rect[3];
        HousingChrome.LayoutPills(new Rect(new Vector2(left, top), new Vector2(right, top + height)), labels, gap,
            rects);
        var watchRect = rects[0];
        var remindRect = rects[1];
        var detailsRect = rects[2];
        if (HousingChrome.PillButton(watchRect, watchLabel, watched, ui, false))
        {
            var nowWatched = housing.Watch.ToggleWatch(plot, housing.WorldNameOf(plot.Key.WorldId));
            ShowToast(Loc.T(nowWatched ? L.Housing.Watching : L.Housing.Unwatch));
            InvalidateCache();
        }

        if (HousingChrome.PillButton(remindRect, remindLabel, reminder is { Notified: false }, ui, false,
                hasDeadline))
        {
            ShowOverlay(HousingOverlay.ReminderPicker);
            reminderChoice = IndexOfLeadTime(reminder?.OffsetMinutes ?? configuration.HousingReminderMinutes);
        }

        if (!hasDeadline)
        {
            HoverTooltip.Show("housing.remind.disabled", remindRect, Loc.T(L.Housing.ReminderUnavailable),
                HoverLabelSide.Above);
        }

        if (HousingChrome.PillButton(detailsRect, detailsLabel, false, ui, false))
        {
            Push(HousingRoute.Details, plot.Key);
        }
    }

    private void DrawReminderPicker(ImDrawListPtr drawList, HousingPlot plot, float left, float right, float top,
        float scale)
    {
        HousingChrome.SectionLabel(drawList, new Vector2(left, top), right - left, Loc.T(L.Housing.ReminderPrompt), ui);
        var y = top + Typography.LineHeight(TextStyles.Caption1) + 6f * scale;
        var choices = HousingDefaults.ReminderChoices;
        for (var index = 0; index < choices.Length; index++)
        {
            reminderLabels[index] = HousingFormat.LeadTime(choices[index]);
            reminderActive[index] = index == reminderChoice;
        }

        var leadRow = new Rect(new Vector2(left, y), new Vector2(right, y + ChipRail.RowHeight * scale));
        var leadTapped = reminderRail.Draw(leadRow, ui, reminderLabels, reminderActive, true);
        if (leadTapped >= 0)
        {
            reminderChoice = leadTapped;
        }

        var buttonHeight = 32f * scale;
        var buttonTop = leadRow.Max.Y + 10f * scale;
        var gap = 8f * scale;
        var existing = housing.Watch.FindReminder(plot.Key);
        var confirmLabel = Loc.T(L.Housing.ReminderSet);
        var removeLabel = Loc.T(L.Housing.CancelReminder);
        var dismissLabel = Loc.T(L.Common.Cancel);
        var buttonRow = new Rect(new Vector2(left, buttonTop), new Vector2(right, buttonTop + buttonHeight));
        Span<Rect> rects = stackalloc Rect[3];
        Rect confirmRect;
        Rect? cancelReminderRect = null;
        Rect dismissRect;
        if (existing is null)
        {
            Span<string> twoLabels = [confirmLabel, dismissLabel];
            HousingChrome.LayoutPills(buttonRow, twoLabels, gap, rects);
            confirmRect = rects[0];
            dismissRect = rects[1];
        }
        else
        {
            Span<string> threeLabels = [confirmLabel, removeLabel, dismissLabel];
            HousingChrome.LayoutPills(buttonRow, threeLabels, gap, rects);
            confirmRect = rects[0];
            cancelReminderRect = rects[1];
            dismissRect = rects[2];
        }

        if (HousingChrome.PillButton(confirmRect, confirmLabel, true, ui, true))
        {
            var minutes = choices[Math.Clamp(reminderChoice, 0, choices.Length - 1)];
            if (housing.Watch.SetReminder(plot, housing.WorldNameOf(plot.Key.WorldId), minutes))
            {
                configuration.HousingReminderMinutes = minutes;
                configuration.Save();
                reminderPickerOpen = false;
                ShowToast(Loc.T(L.Housing.ReminderConfirmed, HousingFormat.LeadTime(minutes),
                    HousingFormat.PhaseLabel(plot.Phase),
                    HousingFormat.Place(HousingDistricts.DisplayName(plot.Key.DistrictId), plot.Key.Ward),
                    plot.Key.Plot));
            }
            else
            {
                ShowToast(Loc.T(L.Housing.ReminderUnavailable));
            }
        }

        if (cancelReminderRect is { } cancelRect &&
            HousingChrome.PillButton(cancelRect, removeLabel, false, ui, true))
        {
            housing.Watch.CancelReminder(plot.Key);
            reminderPickerOpen = false;
            ShowToast(Loc.T(L.Housing.CancelReminder));
        }

        if (HousingChrome.PillButton(dismissRect, dismissLabel, false, ui, true))
        {
            reminderPickerOpen = false;
        }
    }

    private void DrawWardPicker(Rect area, Rect viewport, float scale)
    {
        if (!wardPickerOpen)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var district = HousingDistricts.Resolve(housing.DistrictId);
        var columns = 5;
        var rows = (district.Wards + columns - 1) / columns;
        var pad = 14f * scale;
        var gap = 6f * scale;
        var available = area.Width - pad * 4f - (columns - 1) * gap;
        var cell = MathF.Max(30f * scale, MathF.Min(40f * scale, available / columns));
        var width = columns * cell + (columns - 1) * gap + pad * 2f;
        var titleHeight = HousingSelection.TitleHeight(scale);
        var legendHeight = Typography.LineHeight(HousingSelection.LegendStyle) + gap;
        var height = rows * cell + (rows - 1) * gap + pad * 2f + titleHeight + legendHeight;
        var center = new Vector2(area.Center.X, viewport.Center.Y);
        var min = new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f);
        var max = new Vector2(min.X + width, min.Y + height);
        var panel = new Rect(min, max);
        Material.Veil(drawList, area.Min, area.Max, 0.34f);
        PopoverSurface.Draw(drawList, min, max, Metrics.Radius.Card * scale, frameTheme, scale);
        var gridTop = HousingSelection.Title(drawList, panel, Loc.T(L.Housing.ChooseWard), min.Y + pad, ui, scale);
        Span<int> counts = stackalloc int[district.Wards];
        housing.CollectWardOpenings(counts);
        var current = housing.Ward;
        for (var index = 0; index < district.Wards; index++)
        {
            var column = index % columns;
            var row = index / columns;
            var cellMin = new Vector2(min.X + pad + column * (cell + gap), gridTop + row * (cell + gap));
            var cellMax = cellMin + new Vector2(cell, cell);
            var bounds = new Rect(cellMin, cellMax);
            var ward = index + 1;
            var selected = ward == current;
            var hovered = HousingChrome.Hover(cellMin, cellMax, true);
            HousingSelection.Surface(drawList, bounds, selected, hovered, true, ui, scale);
            if (!selected && !hovered)
            {
                Squircle.Fill(drawList, cellMin, cellMax, HousingSelection.Radius(scale),
                    ImGui.GetColorU32(ui.FieldSurface));
            }

            var ink = HousingSelection.Ink(selected, hovered, true, ui);
            Typography.DrawCentered(drawList, new Vector2(bounds.Center.X, cellMin.Y + cell * 0.42f),
                ward.ToString(Loc.Culture), ink, HousingSelection.TitleStyle);
            if (counts[index] > 0)
            {
                HousingSelection.Marker(drawList, new Vector2(bounds.Center.X, cellMax.Y - 7f * scale),
                    selected ? HousingSelection.StrongInk : ui.Accent, scale);
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (!UiInteract.Click(cellMin, cellMax, hovered))
            {
                continue;
            }

            housing.SelectWard(ward);
            wardPickerOpen = false;
            sheetOpen = false;
            selectedPlot = default;
            InvalidateCache();
        }

        var legendY = gridTop + rows * cell + (rows - 1) * gap + gap;
        Typography.DrawCentered(drawList, new Vector2(panel.Center.X, legendY), Loc.T(L.Housing.WardLegend),
            ui.MutedInk, HousingSelection.LegendStyle);

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsMouseHoveringRect(min, max, false))
        {
            wardPickerOpen = false;
        }
    }
}
