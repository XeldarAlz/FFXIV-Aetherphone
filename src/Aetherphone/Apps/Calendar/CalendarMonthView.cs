using System.Collections.Frozen;
using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Calendar;

internal static class CalendarMonthView
{
    private const float DotRadius = 3f;
    private const float NavHeight = 36f;
    private const float DayHeaderHeight = 22f;
    private const float SidePadding = 6f;
    private const float NumberCenterFraction = 0.34f;
    private const float DotRowFraction = 0.72f;

    public static float Draw(AppSkin ui, Rect area, float targetHeight, ref int monthOffset,
        ref DateTime selectedDate, FrozenDictionary<long, ParsedEvent[]> events)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var referenceDate = DateTime.Today.AddMonths(monthOffset);
        var firstOfMonth = new DateTime(referenceDate.Year, referenceDate.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(referenceDate.Year, referenceDate.Month);
        var firstDayOfWeek = (int)firstOfMonth.DayOfWeek;
        var totalCells = firstDayOfWeek + daysInMonth;
        var totalRows = (int)MathF.Ceiling(totalCells / 7f);
        var today = DateTime.Today;

        var sidePad = SidePadding * scale;
        var gridWidth = area.Width - sidePad * 2f;
        var cellWidth = gridWidth / 7f;
        var reservedHeight = NavHeight * scale + DayHeaderHeight * scale;
        var rowHeight = Math.Clamp((targetHeight - reservedHeight) / totalRows, cellWidth * 0.90f, cellWidth * 1.75f);

        var origin = new Vector2(area.Min.X + sidePad, area.Min.Y);

        origin.Y += DrawNavigation(ui, drawList, origin, gridWidth, referenceDate, scale,
            ref monthOffset, ref selectedDate, today);

        var dayHeaderY = origin.Y;
        DrawDayHeaders(ui, drawList, origin, cellWidth, dayHeaderY, scale);

        var gridTop = dayHeaderY + DayHeaderHeight * scale;
        drawList.AddLine(new Vector2(area.Min.X, gridTop), new Vector2(area.Max.X, gridTop),
            ImGui.GetColorU32(ui.Theme.Separator), 1f);

        for (var row = 0; row < totalRows; row++)
        {
            var rowTop = gridTop + row * rowHeight;
            if (row > 0)
            {
                drawList.AddLine(new Vector2(area.Min.X, rowTop), new Vector2(area.Max.X, rowTop),
                    ImGui.GetColorU32(Palette.WithAlpha(ui.Theme.Separator, 0.5f)), 1f);
            }

            for (var column = 0; column < 7; column++)
            {
                var dayIndex = row * 7 + column;
                var cellDay = dayIndex - firstDayOfWeek + 1;
                var isCurrentMonth = cellDay >= 1 && cellDay <= daysInMonth;
                var cellX = origin.X + column * cellWidth;
                var cellY = rowTop;
                var cellMin = new Vector2(cellX, cellY);
                var cellMax = new Vector2(cellX + cellWidth, cellY + rowHeight);

                DateTime dayDate;
                if (isCurrentMonth)
                {
                    dayDate = new DateTime(referenceDate.Year, referenceDate.Month, cellDay);
                }
                else if (cellDay < 1)
                {
                    var prevMonth = firstOfMonth.AddMonths(-1);
                    var prevDays = DateTime.DaysInMonth(prevMonth.Year, prevMonth.Month);
                    dayDate = new DateTime(prevMonth.Year, prevMonth.Month, prevDays + cellDay);
                }
                else
                {
                    var nextMonth = firstOfMonth.AddMonths(1);
                    dayDate = new DateTime(nextMonth.Year, nextMonth.Month, cellDay - daysInMonth);
                }

                var isToday = dayDate == today;
                var isSelected = dayDate == selectedDate;
                var textColor = isCurrentMonth ? ui.TitleInk : ui.MutedInk;
                if (!isCurrentMonth)
                {
                    textColor = new Vector4(textColor.X, textColor.Y, textColor.Z, textColor.W * 0.4f);
                }

                var dayText = dayDate.Day.ToString();
                var fnScale = TextStyles.SubheadlineEmphasized.Scale;
                var fnWeight = TextStyles.SubheadlineEmphasized.Weight;
                var numberCenter = new Vector2(cellX + cellWidth * 0.5f, cellY + rowHeight * NumberCenterFraction);
                var textSize = Typography.Measure(dayText, fnScale, fnWeight);
                var minSpan = MathF.Min(cellWidth, rowHeight);
                var badgeRadius = Math.Clamp(textSize.X * 0.62f + 3f * scale, minSpan * 0.28f, minSpan * 0.38f);

                if (isToday && isCurrentMonth)
                {
                    drawList.AddCircleFilled(numberCenter, badgeRadius, ImGui.GetColorU32(ui.Accent), 32);
                    textColor = new Vector4(1f, 1f, 1f, 1f);
                }
                else if (isSelected && isCurrentMonth)
                {
                    drawList.AddCircleFilled(numberCenter, badgeRadius,
                        ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, 0.14f)), 32);
                    drawList.AddCircle(numberCenter, badgeRadius, ImGui.GetColorU32(ui.Accent), 32, 1.5f * scale);
                }

                ImGui.SetCursorScreenPos(cellMin);
                if (ImGui.InvisibleButton($"##calday{dayDate:yyyyMMdd}", cellMax - cellMin))
                {
                    selectedDate = dayDate;
                    if (!isCurrentMonth)
                    {
                        monthOffset = ((dayDate.Year - today.Year) * 12) + dayDate.Month - today.Month;
                    }
                }

                ImGui.SetItemAllowOverlap();

                Typography.DrawCentered(drawList, numberCenter, dayText, textColor, fnScale, fnWeight);
                DrawEventDots(ui, drawList, events, dayDate, cellX, cellY, cellWidth, rowHeight, isCurrentMonth,
                    scale);
            }
        }

        return gridTop + totalRows * rowHeight + 6f * scale;
    }

    private static float DrawNavigation(AppSkin ui, ImDrawListPtr drawList, Vector2 origin, float gridWidth,
        DateTime referenceDate, float scale, ref int monthOffset,
        ref DateTime selectedDate, DateTime today)
    {
        var monthName = referenceDate.ToString("MMMM yyyy", Loc.Culture);
        var navY = origin.Y;

        if (DrawNavChevron(ui, drawList, origin, "<", scale))
        {
            monthOffset--;
            selectedDate = new DateTime(referenceDate.Year, referenceDate.Month, 1).AddMonths(-1);
        }

        var monthSize = Typography.Measure(monthName, TextStyles.Title3);
        Typography.Draw(new Vector2(origin.X + gridWidth * 0.5f - monthSize.X * 0.5f,
                navY + NavHeight * scale * 0.5f - monthSize.Y * 0.5f),
            monthName, ui.TitleInk, TextStyles.Title3);

        var rightChevronOrigin = new Vector2(origin.X + gridWidth - 30f * scale, navY);
        if (DrawNavChevron(ui, drawList, rightChevronOrigin, ">", scale))
        {
            monthOffset++;
            selectedDate = new DateTime(referenceDate.Year, referenceDate.Month, 1).AddMonths(1);
        }

        var isCurrentMonth = referenceDate.Year == today.Year && referenceDate.Month == today.Month;
        if (!isCurrentMonth)
        {
            var rightChevronMinX = rightChevronOrigin.X + 4f * scale;
            DrawTodayButton(ui, drawList, rightChevronMinX, navY, scale, ref monthOffset, ref selectedDate);
        }

        return NavHeight * scale;
    }

    private static void DrawTodayButton(AppSkin ui, ImDrawListPtr drawList, float rightLimitX, float navY,
        float scale, ref int monthOffset, ref DateTime selectedDate)
    {
        var todayText = Loc.T(L.Calendar.Today);
        var feScale = TextStyles.FootnoteEmphasized.Scale;
        var feWeight = TextStyles.FootnoteEmphasized.Weight;
        var textSize = Typography.Measure(todayText, feScale, feWeight);
        var padX = 8f * scale;
        var gapX = 6f * scale;
        var height = NavHeight * scale * 0.68f;
        var max = new Vector2(rightLimitX - gapX, navY + NavHeight * scale * 0.5f + height * 0.5f);
        var min = new Vector2(max.X - textSize.X - padX * 2f, max.Y - height);
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        Squircle.Fill(drawList, min, max, height * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, hovered ? 0.24f : 0.14f)));
        Typography.DrawCentered(drawList, (min + max) * 0.5f, todayText, ui.Accent, feScale, feWeight);
        if (!UiInteract.HoverClick(min, max))
        {
            return;
        }

        monthOffset = 0;
        selectedDate = DateTime.Today;
    }

    private static bool DrawNavChevron(AppSkin ui, ImDrawListPtr drawList, Vector2 origin, string chevron,
        float scale)
    {
        var min = new Vector2(origin.X + 4f * scale, origin.Y);
        var max = new Vector2(min.X + 30f * scale, min.Y + NavHeight * scale);
        var mid = (min + max) * 0.5f;
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        if (hovered)
        {
            Squircle.Fill(drawList, min, max, (max.Y - min.Y) * 0.5f, ImGui.GetColorU32(ui.HoverTint));
        }

        var hlScale = TextStyles.Headline.Scale;
        var hlWeight = TextStyles.Headline.Weight;
        Typography.DrawCentered(drawList, mid, chevron, ui.MutedInk, hlScale, hlWeight);

        return UiInteract.HoverClick(min, max);
    }

    private static void DrawDayHeaders(AppSkin ui, ImDrawListPtr drawList, Vector2 origin, float cellWidth,
        float topY, float scale)
    {
        var headers = new[]
        {
            Loc.T(L.Calendar.WeekSun), Loc.T(L.Calendar.WeekMon), Loc.T(L.Calendar.WeekTue),
            Loc.T(L.Calendar.WeekWed), Loc.T(L.Calendar.WeekThu), Loc.T(L.Calendar.WeekFri),
            Loc.T(L.Calendar.WeekSat),
        };

        var c2Scale = TextStyles.FootnoteEmphasized.Scale;
        var c2Weight = TextStyles.FootnoteEmphasized.Weight;

        for (var column = 0; column < headers.Length; column++)
        {
            var cellX = origin.X + column * cellWidth;
            var headerColor = column == 0 ? ui.Accent : ui.MutedInk;
            Typography.DrawCentered(drawList,
                new Vector2(cellX + cellWidth * 0.5f, topY + DayHeaderHeight * scale * 0.5f),
                headers[column], headerColor, c2Scale, c2Weight);
        }
    }

    private static void DrawEventDots(AppSkin ui, ImDrawListPtr drawList,
        FrozenDictionary<long, ParsedEvent[]> events, DateTime day, float cellX, float cellY, float cellWidth,
        float rowHeight, bool isCurrentMonth, float scale)
    {
        var key = day.Date.Ticks;
        if (!events.TryGetValue(key, out var dayEvents) || dayEvents.Length == 0)
        {
            return;
        }

        var dotSpacing = DotRadius * 2.6f * scale;
        var maxWidth = cellWidth - 6f * scale;
        var visibleCount = (int)MathF.Min(dayEvents.Length, MathF.Floor(maxWidth / dotSpacing));
        var actualWidth = visibleCount * dotSpacing;
        var startX = cellX + cellWidth * 0.5f - actualWidth * 0.5f + dotSpacing * 0.5f;
        var dotY = cellY + rowHeight * DotRowFraction;

        for (var index = 0; index < visibleCount; index++)
        {
            var dotX = startX + index * dotSpacing;
            var color = isCurrentMonth ? dayEvents[index].Color : dayEvents[index].DimColor;
            drawList.AddCircleFilled(new Vector2(dotX, dotY), DotRadius * scale, ImGui.GetColorU32(color));
        }

        if (dayEvents.Length > visibleCount)
        {
            var plusX = startX + visibleCount * dotSpacing;
            Typography.Draw(drawList, new Vector2(plusX, dotY - 6f * scale),
                $"+{dayEvents.Length - visibleCount}", ui.MutedInk, TextStyles.Caption2.Scale,
                TextStyles.Caption2.Weight);
        }
    }
}
