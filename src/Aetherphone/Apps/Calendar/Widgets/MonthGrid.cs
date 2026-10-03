using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Calendar.Widgets;

internal static class MonthGrid
{
    private const int DaysPerWeek = 7;
    private const int MaxDaysInMonth = 31;
    private const float TodayFraction = 0.46f;
    private const float DotUnits = 1.6f;
    private const float DotGapUnits = 2f;
    private const float InitialsGapUnits = 3f;

    private static readonly string[] DayNumbers = BuildDayNumbers();
    private static readonly string[] Initials = new string[DaysPerWeek];
    private static CultureInfo? initialsCulture;

    public static int RowCount(DateTime today)
    {
        var lead = LeadingBlanks(today);
        var days = DateTime.DaysInMonth(today.Year, today.Month);
        return (lead + days + DaysPerWeek - 1) / DaysPerWeek;
    }

    public static void Draw(ImDrawListPtr drawList, in WidgetInk ink, Rect area, DateTime today, Vector4 accent,
        uint dayMask, CalendarWidgetFeed? colors, bool dots, float scale)
    {
        var initials = InitialsFor(Loc.Culture);
        var firstDay = (int)Loc.Culture.DateTimeFormat.FirstDayOfWeek;
        var cellWidth = area.Width / DaysPerWeek;
        var initialsHeight = Typography.Measure("A", WidgetType.Eyebrow).Y;
        for (var column = 0; column < DaysPerWeek; column++)
        {
            var dayOfWeek = (firstDay + column) % DaysPerWeek;
            var weekend = dayOfWeek is (int)DayOfWeek.Saturday or (int)DayOfWeek.Sunday;
            Centered(drawList, initials[dayOfWeek], area.Min.X + cellWidth * (column + 0.5f), area.Min.Y,
                weekend ? ink.Tertiary : ink.Secondary, WidgetType.Eyebrow);
        }

        var gridTop = area.Min.Y + initialsHeight + InitialsGapUnits * scale;
        var rows = RowCount(today);
        var cellHeight = (area.Max.Y - gridTop) / rows;
        var lead = LeadingBlanks(today);
        var days = DateTime.DaysInMonth(today.Year, today.Month);
        var numberHeight = Typography.Measure("0", WidgetType.Caption).Y;
        var dotRadius = DotUnits * scale;
        var dotSpace = DotGapUnits * scale + dotRadius * 2f;
        var showDots = dots && cellHeight >= numberHeight + dotSpace;
        var todayStyle = WidgetType.Caption with { Weight = FontWeight.Bold };
        for (var day = 1; day <= days; day++)
        {
            var slot = lead + day - 1;
            var column = slot % DaysPerWeek;
            var row = slot / DaysPerWeek;
            var center = new Vector2(area.Min.X + cellWidth * (column + 0.5f),
                gridTop + cellHeight * (row + 0.5f) - (showDots ? dotSpace * 0.5f : 0f));
            var dayOfWeek = (firstDay + column) % DaysPerWeek;
            var weekend = dayOfWeek is (int)DayOfWeek.Saturday or (int)DayOfWeek.Sunday;
            if (day == today.Day)
            {
                var radius = MathF.Min(cellWidth, cellHeight) * TodayFraction;
                drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ink.Accent(accent)), 28);
                Centered(drawList, DayNumbers[day], center.X, center.Y - numberHeight * 0.5f, ink.OnAccent,
                    todayStyle);
            }
            else
            {
                Centered(drawList, DayNumbers[day], center.X, center.Y - numberHeight * 0.5f,
                    weekend ? ink.Secondary : ink.Primary, WidgetType.Caption);
            }

            if (!showDots || (dayMask & (1u << day)) == 0 || day == today.Day)
            {
                continue;
            }

            drawList.AddCircleFilled(new Vector2(center.X, center.Y + numberHeight * 0.5f + DotGapUnits * scale +
                                                           dotRadius),
                dotRadius, ImGui.GetColorU32(ink.Accent(colors is null ? accent : colors.DayColor(day))), 10);
        }
    }

    private static int LeadingBlanks(DateTime today)
    {
        var first = new DateTime(today.Year, today.Month, 1);
        var firstDay = (int)Loc.Culture.DateTimeFormat.FirstDayOfWeek;
        return ((int)first.DayOfWeek - firstDay + DaysPerWeek) % DaysPerWeek;
    }

    private static void Centered(ImDrawListPtr drawList, string text, float centerX, float top, Vector4 color,
        in TextStyle style)
    {
        var width = Typography.Measure(text, style).X;
        Typography.Draw(drawList, new Vector2(centerX - width * 0.5f, top), text, color, style);
    }

    private static string[] InitialsFor(CultureInfo culture)
    {
        if (ReferenceEquals(initialsCulture, culture))
        {
            return Initials;
        }

        var names = culture.DateTimeFormat.ShortestDayNames;
        for (var index = 0; index < DaysPerWeek; index++)
        {
            Initials[index] = Loc.Upper(names[index]);
        }

        initialsCulture = culture;
        return Initials;
    }

    private static string[] BuildDayNumbers()
    {
        var numbers = new string[MaxDaysInMonth + 1];
        for (var day = 0; day <= MaxDaysInMonth; day++)
        {
            numbers[day] = day.ToString(CultureInfo.InvariantCulture);
        }

        return numbers;
    }
}
