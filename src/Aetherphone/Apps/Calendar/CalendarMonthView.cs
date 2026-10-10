using System.Collections.Frozen;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Calendar;

internal static class CalendarMonthView
{
    public const float WeekdayRowHeight = 26f;
    public const float RootCellHeight = 54f;
    public const float PickerCellHeight = 40f;
    private const int DaysPerWeek = 7;
    private const int MaxDots = 3;
    private const float RootDiscRadius = 16f;
    private const float PickerDiscRadius = 15f;
    private const float RootNumberCenter = 22f;
    private const float DotRadius = 2.8f;
    private const float DotGap = 4f;
    private const float DotOffset = 21f;
    private const float HoverWashAlpha = 0.07f;
    private const float PumpkinPerch = 0.78f;
    private const float PumpkinSize = 1.05f;
    private const float TreatSize = 0.38f;

    private static readonly LocString[] WeekdayInitials =
    {
        L.Calendar.WeekSun, L.Calendar.WeekMon, L.Calendar.WeekTue, L.Calendar.WeekWed,
        L.Calendar.WeekThu, L.Calendar.WeekFri, L.Calendar.WeekSat,
    };

    private static readonly string[] DayNumbers = BuildDayNumbers();

    public static int WeekStart => (int)Loc.Culture.DateTimeFormat.FirstDayOfWeek;

    public static int Rows(DateTime month)
    {
        var first = new DateTime(month.Year, month.Month, 1);
        var leading = ((int)first.DayOfWeek - WeekStart + DaysPerWeek) % DaysPerWeek;
        var total = leading + DateTime.DaysInMonth(month.Year, month.Month);
        return (total + DaysPerWeek - 1) / DaysPerWeek;
    }

    public static void DrawWeekdays(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, float scale)
    {
        var cellWidth = width / DaysPerWeek;
        var weekStart = WeekStart;
        var centerY = origin.Y + WeekdayRowHeight * scale * 0.5f;
        for (var column = 0; column < DaysPerWeek; column++)
        {
            var weekday = (column + weekStart) % DaysPerWeek;
            var ink = IsWeekend(weekday) ? Palette.WithAlpha(ui.MutedInk, ui.MutedInk.W * 0.7f) : ui.MutedInk;
            Typography.DrawCentered(drawList, new Vector2(origin.X + (column + 0.5f) * cellWidth, centerY),
                Loc.T(WeekdayInitials[weekday]), ink, TextStyles.FootnoteEmphasized);
        }
    }

    public static bool Draw(ImDrawListPtr drawList, AppSkin ui, Vector2 origin, float width, DateTime month,
        DateTime selected, FrozenDictionary<long, ParsedEvent[]>? events, bool picker, float alpha,
        out DateTime tapped, float scale)
    {
        tapped = default;
        var first = new DateTime(month.Year, month.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
        var leading = ((int)first.DayOfWeek - WeekStart + DaysPerWeek) % DaysPerWeek;
        var rows = (leading + daysInMonth + DaysPerWeek - 1) / DaysPerWeek;
        var cellWidth = width / DaysPerWeek;
        var cellHeight = (picker ? PickerCellHeight : RootCellHeight) * scale;
        var discRadius = MathF.Min((picker ? PickerDiscRadius : RootDiscRadius) * scale, cellWidth * 0.46f);
        var numberOffset = picker ? cellHeight * 0.5f : RootNumberCenter * scale;
        var today = DateTime.Today;
        var hairline = ImGui.GetColorU32(ui.Hairline with { W = ui.Hairline.W * alpha });
        var interactive = alpha > 0.5f;
        var tappedAny = false;
        for (var row = 0; row < rows; row++)
        {
            var rowTop = origin.Y + row * cellHeight;
            if (!picker)
            {
                drawList.AddLine(new Vector2(origin.X, rowTop), new Vector2(origin.X + width, rowTop), hairline,
                    Metrics.Stroke.Hairline);
            }

            for (var column = 0; column < DaysPerWeek; column++)
            {
                var day = row * DaysPerWeek + column - leading + 1;
                if (day < 1 || day > daysInMonth)
                {
                    continue;
                }

                var date = first.AddDays(day - 1);
                var cellMin = new Vector2(origin.X + column * cellWidth, rowTop);
                var cellMax = new Vector2(cellMin.X + cellWidth, rowTop + cellHeight);
                var center = new Vector2(cellMin.X + cellWidth * 0.5f, rowTop + numberOffset);
                var hovered = interactive && UiInteract.Hover(cellMin, cellMax);
                var isToday = date == today;
                var isSelected = date == selected;
                var ink = DayInk(ui, date, isToday, isSelected);
                if (isSelected)
                {
                    var fill = isToday ? ui.Accent : ui.TitleInk;
                    drawList.AddCircleFilled(center, discRadius, ImGui.GetColorU32(fill with { W = fill.W * alpha }),
                        40);
                }
                else if (hovered)
                {
                    drawList.AddCircleFilled(center, discRadius,
                        ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, HoverWashAlpha * alpha)), 40);
                }

                var style = isToday || isSelected ? TextStyles.Headline : TextStyles.Body;
                Typography.DrawCentered(drawList, center, DayNumbers[day], ink with { W = ink.W * alpha }, style);
                if (SeasonalTheme.IsHalloweenNight(date))
                {
                    var perch = discRadius * PumpkinPerch;
                    PhoneIcon.Draw(drawList, center + new Vector2(perch, -perch), PhoneIcons.Pumpkin,
                        Spooks.Pumpkin with { W = alpha }, discRadius * PumpkinSize);
                    if (!picker && interactive)
                    {
                        Treats.OfferAt(drawList, TreatSpot.Calendar, center + new Vector2(-perch, -perch),
                            discRadius * TreatSize);
                    }
                }

                if (!picker && events is not null)
                {
                    DrawDots(drawList, events, date, new Vector2(center.X, rowTop + numberOffset + DotOffset * scale),
                        alpha, scale);
                }

                if (hovered)
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }

                if (interactive && UiInteract.Click(cellMin, cellMax, hovered))
                {
                    tapped = date;
                    tappedAny = true;
                }
            }
        }

        return tappedAny;
    }

    public static float Height(DateTime month, bool picker, float scale) =>
        Rows(month) * (picker ? PickerCellHeight : RootCellHeight) * scale;

    private static Vector4 DayInk(AppSkin ui, DateTime date, bool isToday, bool isSelected)
    {
        if (isSelected)
        {
            return isToday ? AccentRing.Ink : ui.Theme.AppBackground with { W = 1f };
        }

        if (isToday)
        {
            return ui.Accent;
        }

        return IsWeekend((int)date.DayOfWeek) ? ui.MutedInk : ui.TitleInk;
    }

    private static void DrawDots(ImDrawListPtr drawList, FrozenDictionary<long, ParsedEvent[]> events,
        DateTime date, Vector2 center, float alpha, float scale)
    {
        if (!events.TryGetValue(date.Ticks, out var dayEvents) || dayEvents.Length == 0)
        {
            return;
        }

        Span<Vector4> colors = stackalloc Vector4[MaxDots];
        var count = 0;
        for (var index = 0; index < dayEvents.Length && count < MaxDots; index++)
        {
            var color = dayEvents[index].Color;
            var seen = false;
            for (var existing = 0; existing < count; existing++)
            {
                if (colors[existing] == color)
                {
                    seen = true;
                    break;
                }
            }

            if (!seen)
            {
                colors[count++] = color;
            }
        }

        var radius = DotRadius * scale;
        var step = radius * 2f + DotGap * 0.5f * scale;
        var startX = center.X - (count - 1) * step * 0.5f;
        for (var index = 0; index < count; index++)
        {
            var color = colors[index];
            drawList.AddCircleFilled(new Vector2(startX + index * step, center.Y), radius,
                ImGui.GetColorU32(color with { W = color.W * alpha }), 16);
        }
    }

    private static bool IsWeekend(int weekday) =>
        weekday == (int)DayOfWeek.Saturday || weekday == (int)DayOfWeek.Sunday;

    private static string[] BuildDayNumbers()
    {
        var numbers = new string[32];
        for (var index = 0; index < numbers.Length; index++)
        {
            numbers[index] = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return numbers;
    }
}
