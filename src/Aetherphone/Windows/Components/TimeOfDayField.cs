using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal static class TimeOfDayField
{
    private const int MinutesPerHour = 60;
    private const int HoursPerDay = 24;
    private const int Noon = 12;
    private const int MinuteStep = 5;
    private const float ChevronWidth = 30f;
    private const float MeridiemWidth = 52f;
    private const float IconScale = 0.65f;

    public static int Draw(AppSkin ui, Rect rect, int minuteOfDay, float scale)
    {
        var gap = Metrics.Space.Sm * scale;
        var meridiemSpan = TimeText.Use24Hour ? 0f : MeridiemWidth * scale + gap;
        var stepperWidth = (rect.Width - meridiemSpan - gap) * 0.5f;
        var hourRect = new Rect(rect.Min, new Vector2(rect.Min.X + stepperWidth, rect.Max.Y));
        var minuteRect = new Rect(new Vector2(hourRect.Max.X + gap, rect.Min.Y),
            new Vector2(hourRect.Max.X + gap + stepperWidth, rect.Max.Y));
        var hour = minuteOfDay / MinutesPerHour;
        var minute = minuteOfDay % MinutesPerHour;
        var hourDelta = Stepper(ui, hourRect, TimeText.HourLabel(hour), scale);
        var minuteDelta = Stepper(ui, minuteRect, TimeText.MinuteLabel(minute), scale);
        if (!TimeText.Use24Hour)
        {
            hour = Meridiem(ui, new Rect(new Vector2(minuteRect.Max.X + gap, rect.Min.Y), rect.Max), hour, scale);
        }

        return Wrap(hour + hourDelta, HoursPerDay) * MinutesPerHour
            + Wrap(SteppedMinute(minute, minuteDelta), MinutesPerHour);
    }

    private static int Meridiem(AppSkin ui, Rect rect, int hour, float scale)
    {
        var gap = Metrics.Space.Xxs * scale;
        var half = (rect.Height - gap) * 0.5f;
        var afternoon = hour >= Noon;
        var morningTapped = ui.Chip(new Rect(rect.Min, new Vector2(rect.Max.X, rect.Min.Y + half)),
            TimeText.MeridiemLabel(false), !afternoon);
        var afternoonTapped = ui.Chip(new Rect(new Vector2(rect.Min.X, rect.Max.Y - half), rect.Max),
            TimeText.MeridiemLabel(true), afternoon);
        if (morningTapped && afternoon)
        {
            return hour - Noon;
        }

        if (afternoonTapped && !afternoon)
        {
            return hour + Noon;
        }

        return hour;
    }

    private static int Stepper(AppSkin ui, Rect rect, string valueText, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, rect.Min, rect.Max, Metrics.Radius.Field * scale,
            ImGui.GetColorU32(ui.FieldSurface));
        var chevronWidth = ChevronWidth * scale;
        var backwards = Chevron(ui, drawList,
            new Rect(rect.Min, new Vector2(rect.Min.X + chevronWidth, rect.Max.Y)),
            FontAwesomeIcon.ChevronLeft, scale);
        var forwards = Chevron(ui, drawList,
            new Rect(new Vector2(rect.Max.X - chevronWidth, rect.Min.Y), rect.Max),
            FontAwesomeIcon.ChevronRight, scale);
        Typography.DrawCentered(drawList, rect.Center, valueText, ui.TitleInk, TextStyles.Title3);
        if (forwards)
        {
            return 1;
        }

        return backwards ? -1 : 0;
    }

    private static bool Chevron(AppSkin ui, ImDrawListPtr drawList, Rect rect, FontAwesomeIcon icon, float scale)
    {
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, Metrics.Radius.Sm * scale, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        AppSkin.Icon(drawList, rect.Center, icon.ToIconString(), hovered ? ui.TitleInk : ui.MutedInk, IconScale);
        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    private static int SteppedMinute(int minute, int delta)
    {
        if (delta > 0)
        {
            return (minute / MinuteStep + 1) * MinuteStep;
        }

        if (delta < 0)
        {
            return ((minute + MinuteStep - 1) / MinuteStep - 1) * MinuteStep;
        }

        return minute;
    }

    private static int Wrap(int value, int modulus)
    {
        var remainder = value % modulus;
        return remainder < 0 ? remainder + modulus : remainder;
    }
}
