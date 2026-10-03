using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Housing;

internal sealed partial class HousingApp
{
    private const int WardColumns = 6;
    private const float SheetPad = 16f;
    private const float SheetBottomPad = 28f;
    private const float DistrictTileSize = 44f;
    private const float WardCellHeight = 44f;
    private const float WardCellGap = 6f;
    private const float SectionLabelGap = 8f;
    private const float LeadTileHeight = 64f;
    private const float LeadTileGap = 8f;
    private const float ActionHeight = 44f;
    private const float SelectedRing = 2f;
    private const float WardLegendDot = 3.5f;

    private readonly Sheet locationSheet = new();
    private readonly Sheet reminderSheet = new();
    private readonly Sheet filterSheet = new();
    private PhoneTheme? sheetThemeSource;
    private PhoneTheme sheetTheme = PhoneTheme.Default;
    private HousingPlotKey reminderPlot;
    private int reminderChoice = 2;
    private readonly CachedText[] leadUnits = new CachedText[HousingDefaults.ReminderChoices.Length];
    private CachedText wardHeadingText;
    private CachedText reminderSubtitle;
    private CachedText reminderCountdown;

    private string ReminderSubtitle(HousingPlot? plot)
    {
        if (plot is null)
        {
            return Loc.T(L.Housing.ReminderUnavailable);
        }

        var countdown = HousingText.Countdown(ref reminderCountdown, plot.PhaseEndsUtc, DateTime.UtcNow);
        var key = ((long)plot.Key.GetHashCode() << 20) ^ countdown.GetHashCode();
        return reminderSubtitle.IsCurrent(key)
            ? reminderSubtitle.Value
            : reminderSubtitle.Store(key, Loc.T(L.Housing.ReminderSubtitle,
                Loc.T(L.Housing.PlotAndWard, plot.Key.Plot, plot.Key.Ward), HousingFormat.PhaseLabel(plot.Phase),
                countdown));
    }

    private bool ModalOpen =>
        locationSheet.CapturesPointer || reminderSheet.CapturesPointer || filterSheet.CapturesPointer;

    private PhoneTheme SheetTheme()
    {
        if (ReferenceEquals(sheetThemeSource, frameTheme))
        {
            return sheetTheme;
        }

        var source = frameTheme;
        sheetThemeSource = source;
        sheetTheme = new PhoneTheme
        {
            Case = source.Case,
            CaseKind = source.CaseKind,
            CaseTextureId = source.CaseTextureId,
            ScreenBase = source.ScreenBase,
            LightWallpaperId = source.LightWallpaperId,
            DarkWallpaperId = source.DarkWallpaperId,
            AppBackground = ui.Palette.BackdropTop,
            GroupedCard = source.GroupedCard,
            Separator = source.Separator,
            Hairline = ui.Hairline,
            HoverWash = source.HoverWash,
            ToggleOn = ui.Accent,
            ToggleOff = source.ToggleOff,
            Surface = source.Surface,
            SurfaceMuted = source.SurfaceMuted,
            TextStrong = ui.TitleInk,
            TextMuted = ui.MutedInk,
            Accent = ui.Accent,
            Danger = source.Danger,
            RailWidth = source.RailWidth,
            MetalWidth = source.MetalWidth,
            GlassWidth = source.GlassWidth,
            DeviceRounding = source.DeviceRounding,
            TopZoneHeight = source.TopZoneHeight,
            BottomZoneHeight = source.BottomZoneHeight,
            SidePadding = source.SidePadding,
        };
        return sheetTheme;
    }

    private void DrawModalSheets(Rect area)
    {
        if (!ModalOpen)
        {
            return;
        }

        var scale = UiScale.Current;
        var screen = SceneChrome.ScreenFrom(area, frameTheme, scale);
        using var layer = ScreenLayer.Begin("housing.sheets", screen, false);
        var drawList = ImGui.GetWindowDrawList();
        var theme = SheetTheme();
        if (locationSheet.CapturesPointer)
        {
            DrawLocationSheet(drawList, screen, theme, scale);
        }

        if (reminderSheet.CapturesPointer)
        {
            DrawReminderSheet(drawList, screen, theme, scale);
        }

        if (filterSheet.CapturesPointer)
        {
            DrawFilterSheet(drawList, screen, theme, scale);
        }
    }

    private void OpenLocationSheet()
    {
        legendOpen = false;
        menu.Close();
        locationSheet.Open();
    }

    private float LocationSheetHeight(float scale)
    {
        var rows = (HousingDistricts.Resolve(housing.DistrictId).Wards + WardColumns - 1) / WardColumns;
        return SheetMetrics.GrabberZone * scale + HousingArt.SheetHeaderHeight * scale +
               DistrictTileSize * scale + Typography.LineHeight(TextStyles.Caption1) * 2f +
               HousingArt.SectionGap * scale + Typography.LineHeight(TextStyles.FootnoteEmphasized) +
               SectionLabelGap * scale + rows * WardCellHeight * scale + (rows - 1) * WardCellGap * scale +
               SheetBottomPad * scale;
    }

    private void DrawLocationSheet(ImDrawListPtr drawList, Rect screen, PhoneTheme theme, float scale)
    {
        var detents = SheetDetents.Fitted(MathF.Min(LocationSheetHeight(scale),
            screen.Height * SheetMetrics.LargeFraction));
        var frame = locationSheet.Begin(drawList, screen, theme, detents, SheetMetrics.AppVeil);
        if (!frame.Visible)
        {
            return;
        }

        var content = frame.Content;
        if (HousingArt.SheetHeader(drawList, content, Loc.T(L.Housing.LocationTitle), Loc.T(L.Housing.Done),
                ui.TitleInk, ui.Accent, scale) && frame.Interactive)
        {
            locationSheet.Close();
        }

        var pad = SheetPad * scale;
        var left = content.Min.X + pad;
        var right = content.Max.X - pad;
        var y = content.Min.Y + HousingArt.SheetHeaderHeight * scale;
        y = DrawDistrictPicker(drawList, left, right, y, frame.Interactive, scale);
        y += HousingArt.SectionGap * scale;
        var wardHeading = wardHeadingText.IsCurrent(0L)
            ? wardHeadingText.Value
            : wardHeadingText.Store(0L, Loc.Upper(Loc.T(L.Housing.WardLabel)));
        Typography.Draw(drawList, new Vector2(left, y), wardHeading, ui.MutedInk, TextStyles.FootnoteEmphasized);
        var headingWidth = Typography.Measure(wardHeading, TextStyles.FootnoteEmphasized).X;
        DrawWardLegend(drawList, left + headingWidth + HousingArt.TextGap * scale, right,
            y + Typography.LineHeight(TextStyles.FootnoteEmphasized) * 0.5f, scale);
        y += Typography.LineHeight(TextStyles.FootnoteEmphasized) + SectionLabelGap * scale;
        DrawWardGrid(drawList, left, right, y, frame.Interactive, scale);
        locationSheet.End(in frame);
    }

    private void DrawWardLegend(ImDrawListPtr drawList, float left, float right, float centerY, float scale)
    {
        var dot = WardLegendDot * scale;
        var gap = Metrics.Space.Sm * scale;
        var available = right - left - dot * 2f - gap;
        if (available <= 0f)
        {
            return;
        }

        var label = Typography.FitText(Loc.T(L.Housing.WardLegend), available, TextStyles.Footnote);
        var labelSize = Typography.Measure(label, TextStyles.Footnote);
        var labelLeft = right - labelSize.X;
        drawList.AddCircleFilled(new Vector2(labelLeft - gap - dot, centerY), dot, ImGui.GetColorU32(ui.Accent), 12);
        Typography.Draw(drawList, new Vector2(labelLeft, centerY - labelSize.Y * 0.5f), label, ui.MutedInk,
            TextStyles.Footnote);
    }

    private float DrawDistrictPicker(ImDrawListPtr drawList, float left, float right, float top, bool interactive,
        float scale)
    {
        var districts = HousingDistricts.All;
        var column = (right - left) / districts.Count;
        var tile = DistrictTileSize * scale;
        var captionHeight = Typography.LineHeight(TextStyles.Caption1);
        var height = tile + captionHeight * 2f;
        for (var index = 0; index < districts.Count; index++)
        {
            var id = districts[index].Id;
            var cellMin = new Vector2(left + column * index, top);
            var cellMax = new Vector2(cellMin.X + column, top + height);
            var hovered = interactive && UiInteract.HoverWindowOnly(cellMin, cellMax);
            var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
            var press = PressFx.Scale(unchecked(ImGui.GetID("housing.district") + (uint)index), down,
                Motion.PressScaleControl);
            var center = new Vector2((cellMin.X + cellMax.X) * 0.5f, top + tile * 0.5f);
            var selected = id == housing.DistrictId;
            if (selected)
            {
                var ring = tile * 0.5f + SelectedRing * 2f * scale;
                Squircle.Stroke(drawList, center - new Vector2(ring, ring), center + new Vector2(ring, ring),
                    ring * Metrics.Radius.TileFactor * 2f, ImGui.GetColorU32(ui.Accent), SelectedRing * scale);
            }

            HousingArt.DistrictTile(drawList, center, tile * press, id);
            var name = HousingDistricts.ShortDisplayName(id);
            Typography.DrawCentered(drawList, new Vector2(center.X, top + tile + captionHeight * 0.5f + 2f * scale),
                Typography.FitText(name, column - 4f * scale, TextStyles.Caption1),
                selected ? ui.TitleInk : ui.BodyInk, TextStyles.Caption1);
            var open = DistrictOpenCount(id);
            if (open >= 0)
            {
                Typography.DrawCentered(drawList,
                    new Vector2(center.X, top + tile + captionHeight * 1.5f + 2f * scale), HousingText.Count(open),
                    open > 0 ? ui.Accent : ui.MutedInk, TextStyles.Caption1);
            }

            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (!UiInteract.Click(cellMin, cellMax, hovered) || selected)
            {
                continue;
            }

            housing.SelectDistrict(id);
            showSubdivision = false;
            ClosePlotCard(true);
            ResetMapView();
            InvalidateCache();
            UiFeedback.Play(UiSound.Tap);
        }

        return top + height;
    }

    private int DistrictOpenCount(uint districtId) =>
        housing.Lookup(housing.WorldId, districtId) is { } snapshot ? snapshot.Plots.Count : -1;

    private void DrawWardGrid(ImDrawListPtr drawList, float left, float right, float top, bool interactive,
        float scale)
    {
        var district = HousingDistricts.Resolve(housing.DistrictId);
        Span<int> counts = stackalloc int[district.Wards];
        housing.CollectWardOpenings(counts);
        var gap = WardCellGap * scale;
        var cellWidth = (right - left - gap * (WardColumns - 1)) / WardColumns;
        var cellHeight = WardCellHeight * scale;
        var current = housing.Ward;
        var rounding = Metrics.Radius.Md * scale;
        var numberHeight = Typography.LineHeight(TextStyles.SubheadlineEmphasized);
        var countHeight = Typography.LineHeight(TextStyles.Caption2);
        for (var index = 0; index < district.Wards; index++)
        {
            var column = index % WardColumns;
            var row = index / WardColumns;
            var cellMin = new Vector2(left + column * (cellWidth + gap), top + row * (cellHeight + gap));
            var cellMax = cellMin + new Vector2(cellWidth, cellHeight);
            var ward = index + 1;
            var selected = ward == current;
            var hovered = interactive && UiInteract.HoverWindowOnly(cellMin, cellMax);
            var fill = selected ? ui.Accent : hovered ? Palette.Mix(ui.FieldSurface, ui.TitleInk, 0.06f) : ui.FieldSurface;
            Squircle.Fill(drawList, cellMin, cellMax, rounding, ImGui.GetColorU32(fill));
            var centerX = (cellMin.X + cellMax.X) * 0.5f;
            var hasOpenings = counts[index] > 0;
            var blockHeight = numberHeight + (hasOpenings ? countHeight : 0f);
            var blockTop = (cellMin.Y + cellMax.Y - blockHeight) * 0.5f;
            Typography.DrawCentered(drawList, new Vector2(centerX, blockTop + numberHeight * 0.5f),
                HousingText.Count(ward), selected ? AccentRing.Ink : hasOpenings ? ui.TitleInk : ui.MutedInk,
                TextStyles.SubheadlineEmphasized);
            if (hasOpenings)
            {
                Typography.DrawCentered(drawList, new Vector2(centerX, blockTop + numberHeight + countHeight * 0.5f),
                    HousingText.Count(counts[index]), selected ? AccentRing.Ink : ui.Accent, TextStyles.Caption2);
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
            ClosePlotCard(true);
            InvalidateCache();
            UiFeedback.Play(UiSound.Tap);
            locationSheet.Close();
        }
    }

    private void OpenReminderSheet(HousingPlotKey key)
    {
        reminderPlot = key;
        var existing = housing.Watch.FindReminder(key);
        reminderChoice = IndexOfLeadTime(existing?.OffsetMinutes ?? configuration.HousingReminderMinutes);
        menu.Close();
        reminderSheet.Open();
    }

    private static int IndexOfLeadTime(int minutes)
    {
        var choices = HousingDefaults.ReminderChoices;
        for (var index = 0; index < choices.Length; index++)
        {
            if (choices[index] == minutes)
            {
                return index;
            }
        }

        return 2;
    }

    private float ReminderSheetHeight(string subtitle, float width, float scale) =>
        SheetMetrics.GrabberZone * scale + HousingArt.SheetHeaderHeight * scale +
        Typography.MeasureWrappedBlock(subtitle, TextStyles.Subheadline, width).Y + HousingArt.SectionGap * scale +
        LeadTileHeight * scale + HousingArt.SectionGap * scale + ActionHeight * scale +
        (housing.Watch.FindReminder(reminderPlot) is null ? 0f : ActionHeight * scale) + SheetBottomPad * scale;

    private void DrawReminderSheet(ImDrawListPtr drawList, Rect screen, PhoneTheme theme, float scale)
    {
        var plot = FindPlot(reminderPlot);
        var pad = SheetPad * scale;
        var width = screen.Width - pad * 2f;
        var subtitle = ReminderSubtitle(plot);
        var detents = SheetDetents.Fitted(MathF.Min(ReminderSheetHeight(subtitle, width, scale),
            screen.Height * SheetMetrics.LargeFraction));
        var frame = reminderSheet.Begin(drawList, screen, theme, detents, SheetMetrics.AppVeil);
        if (!frame.Visible)
        {
            return;
        }

        var content = frame.Content;
        if (HousingArt.SheetHeader(drawList, content, Loc.T(L.Housing.RemindMe), Loc.T(L.Common.Cancel),
                ui.TitleInk, ui.Accent, scale) && frame.Interactive)
        {
            reminderSheet.Close();
        }

        var left = content.Min.X + pad;
        var right = content.Max.X - pad;
        var y = content.Min.Y + HousingArt.SheetHeaderHeight * scale;
        y += Typography.DrawWrappedLeft(new Vector2(left, y), subtitle, ui.MutedInk, TextStyles.Subheadline,
            right - left);
        y += HousingArt.SectionGap * scale;
        y = DrawLeadTiles(drawList, left, right, y, frame.Interactive, scale);
        y += HousingArt.SectionGap * scale;
        var existing = housing.Watch.FindReminder(reminderPlot);
        var primary = new Rect(new Vector2(left, y), new Vector2(right, y + ActionHeight * scale));
        if (HousingChrome.PillButton(primary, Loc.T(L.Housing.SetReminder), true, ui, true,
                plot is not null && frame.Interactive) && plot is not null)
        {
            SaveReminder(plot);
        }

        if (existing is not null)
        {
            y = primary.Max.Y;
            var removeLabel = Loc.T(L.Housing.CancelReminder);
            var removeSize = Typography.Measure(removeLabel, TextStyles.Body);
            var hitMin = new Vector2(content.Center.X - removeSize.X * 0.5f - pad, y);
            var hitMax = new Vector2(content.Center.X + removeSize.X * 0.5f + pad, y + ActionHeight * scale);
            var hovered = frame.Interactive && UiInteract.HoverWindowOnly(hitMin, hitMax);
            Typography.DrawCentered(drawList, (hitMin + hitMax) * 0.5f, removeLabel,
                hovered ? Palette.Lighten(frameTheme.Danger, 0.15f) : frameTheme.Danger, TextStyles.Body);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(hitMin, hitMax, hovered))
            {
                housing.Watch.CancelReminder(reminderPlot);
                UiFeedback.Play(UiSound.ToggleOff);
                ShellToast.Show(Loc.T(L.Housing.ReminderRemoved));
                reminderSheet.Close();
            }
        }

        reminderSheet.End(in frame);
    }

    private float DrawLeadTiles(ImDrawListPtr drawList, float left, float right, float top, bool interactive,
        float scale)
    {
        var choices = HousingDefaults.ReminderChoices;
        var gap = LeadTileGap * scale;
        var width = (right - left - gap * (choices.Length - 1)) / choices.Length;
        var height = LeadTileHeight * scale;
        var rounding = Metrics.Radius.Card * scale;
        for (var index = 0; index < choices.Length; index++)
        {
            var min = new Vector2(left + index * (width + gap), top);
            var max = new Vector2(min.X + width, top + height);
            var selected = index == reminderChoice;
            var hovered = interactive && UiInteract.HoverWindowOnly(min, max);
            var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
            var press = PressFx.Scale(unchecked(ImGui.GetID("housing.lead") + (uint)index), down,
                Motion.PressScaleControl);
            var center = (min + max) * 0.5f;
            var half = (max - min) * 0.5f * press;
            var fill = selected ? ui.Accent : hovered ? Palette.Mix(ui.FieldSurface, ui.TitleInk, 0.06f) : ui.FieldSurface;
            Squircle.Fill(drawList, center - half, center + half, rounding, ImGui.GetColorU32(fill));
            var minutes = choices[index];
            var hours = minutes >= 60 && minutes % 60 == 0;
            var value = HousingText.Count(hours ? minutes / 60 : minutes);
            var unitKey = hours ? -minutes : minutes;
            var unit = leadUnits[index].IsCurrent(unitKey)
                ? leadUnits[index].Value
                : leadUnits[index].Store(unitKey,
                    hours ? Loc.Plural(L.Housing.UnitHours, minutes / 60) : Loc.Plural(L.Housing.UnitMinutes, minutes));
            var valueHeight = Typography.LineHeight(TextStyles.Title2);
            var unitHeight = Typography.LineHeight(TextStyles.Caption1);
            var blockTop = center.Y - (valueHeight + unitHeight) * 0.5f;
            var ink = selected ? AccentRing.Ink : ui.TitleInk;
            Typography.DrawCentered(drawList, new Vector2(center.X, blockTop + valueHeight * 0.5f), value, ink,
                TextStyles.Title2);
            Typography.DrawCentered(drawList, new Vector2(center.X, blockTop + valueHeight + unitHeight * 0.5f),
                Typography.FitText(unit, width - 6f * scale, TextStyles.Caption1),
                selected ? AccentRing.Ink : ui.MutedInk, TextStyles.Caption1);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, max, hovered) && reminderChoice != index)
            {
                reminderChoice = index;
                UiFeedback.Play(UiSound.Tap);
            }
        }

        return top + height;
    }

    private void SaveReminder(HousingPlot plot)
    {
        var choices = HousingDefaults.ReminderChoices;
        var minutes = choices[Math.Clamp(reminderChoice, 0, choices.Length - 1)];
        if (!housing.Watch.SetReminder(plot, housing.WorldNameOf(plot.Key.WorldId), minutes))
        {
            UiFeedback.Play(UiSound.Blocked);
            ShellToast.Show(Loc.T(L.Housing.ReminderUnavailable));
            return;
        }

        configuration.HousingReminderMinutes = minutes;
        configuration.Save();
        UiFeedback.Play(UiSound.Success);
        ShellToast.Show(Loc.T(L.Housing.ReminderConfirmed, HousingFormat.LeadTime(minutes),
            HousingFormat.PhaseLabel(plot.Phase),
            HousingFormat.Place(HousingDistricts.DisplayName(plot.Key.DistrictId), plot.Key.Ward), plot.Key.Plot));
        reminderSheet.Close();
    }
}
