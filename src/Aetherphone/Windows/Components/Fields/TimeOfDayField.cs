using System.Globalization;
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
    private const int NotTyped = -1;
    private const int EditBufferLength = 3;
    private const int FocusGraceFrames = 3;
    private const float ChevronWidth = 30f;
    private const float ChevronShare = 0.25f;
    private const float MeridiemWidth = 52f;
    private const float IconScale = 0.65f;
    private const string WidestValue = "00";

    private static int ordinalFrame = -1;
    private static int ordinal;
    private static uint editingId;
    private static int editStartFrame;
    private static string editText = string.Empty;

    public static int Draw(AppSkin ui, Rect rect, int minuteOfDay, float scale)
    {
        using var id = UiScope.PushId(NextOrdinal());
        var gap = Metrics.Space.Sm * scale;
        var meridiemSpan = TimeText.Use24Hour ? 0f : MeridiemWidth * scale + gap;
        var stepperWidth = (rect.Width - meridiemSpan - gap) * 0.5f;
        var hourRect = new Rect(rect.Min, new Vector2(rect.Min.X + stepperWidth, rect.Max.Y));
        var minuteRect = new Rect(new Vector2(hourRect.Max.X + gap, rect.Min.Y),
            new Vector2(hourRect.Max.X + gap + stepperWidth, rect.Max.Y));
        var hour = minuteOfDay / MinutesPerHour;
        var minute = minuteOfDay % MinutesPerHour;
        var hourDelta = Stepper(ui, hourRect, "##hour", TimeText.HourLabel(hour), scale, out var typedHour);
        var minuteDelta = Stepper(ui, minuteRect, "##minute", TimeText.MinuteLabel(minute), scale,
            out var typedMinute);
        if (typedHour != NotTyped)
        {
            hour = TypedHour(typedHour, hour);
        }

        if (typedMinute != NotTyped)
        {
            minute = Math.Clamp(typedMinute, 0, MinutesPerHour - 1);
        }

        if (!TimeText.Use24Hour)
        {
            hour = Meridiem(ui, new Rect(new Vector2(minuteRect.Max.X + gap, rect.Min.Y), rect.Max), hour, scale);
        }

        return Wrap(hour + hourDelta, HoursPerDay) * MinutesPerHour
            + Wrap(SteppedMinute(minute, minuteDelta), MinutesPerHour);
    }

    private static int NextOrdinal()
    {
        var frame = ImGui.GetFrameCount();
        if (frame != ordinalFrame)
        {
            ordinalFrame = frame;
            ordinal = 0;
        }

        return ordinal++;
    }

    private static int TypedHour(int typed, int currentHour)
    {
        if (TimeText.Use24Hour || typed is < 1 or > Noon)
        {
            return Math.Clamp(typed, 0, HoursPerDay - 1);
        }

        return typed % Noon + (currentHour >= Noon ? Noon : 0);
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

    private static int Stepper(AppSkin ui, Rect rect, string id, string valueText, float scale, out int typed)
    {
        typed = NotTyped;
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, rect.Min, rect.Max, Metrics.Radius.Field * scale,
            ImGui.GetColorU32(ui.FieldSurface));
        var chevronWidth = MathF.Min(ChevronWidth * scale, rect.Width * ChevronShare);
        var valueRect = new Rect(new Vector2(rect.Min.X + chevronWidth, rect.Min.Y),
            new Vector2(rect.Max.X - chevronWidth, rect.Max.Y));
        var key = ImGui.GetID(id);
        if (editingId == key)
        {
            typed = Edit(ui, drawList, valueRect, id, scale);
            return 0;
        }

        var backwards = Chevron(ui, drawList,
            new Rect(rect.Min, new Vector2(rect.Min.X + chevronWidth, rect.Max.Y)),
            FontAwesomeIcon.ChevronLeft, scale);
        var forwards = Chevron(ui, drawList,
            new Rect(new Vector2(rect.Max.X - chevronWidth, rect.Min.Y), rect.Max),
            FontAwesomeIcon.ChevronRight, scale);
        if (Pressable(ui, drawList, valueRect, scale, ImGuiMouseCursor.TextInput, out _))
        {
            editingId = key;
            editStartFrame = ImGui.GetFrameCount();
            editText = valueText;
        }

        Typography.DrawCentered(drawList, rect.Center, valueText, ui.TitleInk, TextStyles.Title3);
        if (forwards)
        {
            return 1;
        }

        return backwards ? -1 : 0;
    }

    private static int Edit(AppSkin ui, ImDrawListPtr drawList, Rect rect, string id, float scale)
    {
        Squircle.Stroke(drawList, rect.Min, rect.Max, Metrics.Radius.Sm * scale, ImGui.GetColorU32(ui.Accent),
            Metrics.Stroke.Ring * scale);
        if (ImGui.GetFrameCount() == editStartFrame + 1)
        {
            ImGui.SetKeyboardFocusHere();
        }

        bool active;
        using (Plugin.Fonts.Push(TextStyles.Title3.Scale, TextStyles.Title3.Weight))
        {
            var framePadding = ImGui.GetStyle().FramePadding.X * 2f;
            var inputWidth = MathF.Min(rect.Width,
                MathF.Max(Typography.Measure(editText, TextStyles.Title3).X,
                    Typography.Measure(WidestValue, TextStyles.Title3).X) + framePadding);
            ImGui.SetCursorScreenPos(new Vector2(rect.Center.X - inputWidth * 0.5f,
                rect.Center.Y - ImGui.GetFrameHeight() * 0.5f));
            ImGui.SetNextItemWidth(inputWidth);
            using (UiScope.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
            using (UiScope.PushColor(ImGuiCol.Text, ui.TitleInk))
            {
                ImGui.InputText(id, ref editText, EditBufferLength,
                    ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.AutoSelectAll);
            }

            active = ImGui.IsItemActive();
        }

        if (active || ImGui.GetFrameCount() <= editStartFrame + FocusGraceFrames)
        {
            return NotTyped;
        }

        editingId = 0;
        return int.TryParse(editText, NumberStyles.None, Loc.Culture, out var typed) ? typed : NotTyped;
    }

    private static bool Chevron(AppSkin ui, ImDrawListPtr drawList, Rect rect, FontAwesomeIcon icon, float scale)
    {
        var clicked = Pressable(ui, drawList, rect, scale, ImGuiMouseCursor.Hand, out var hovered);
        AppSkin.Icon(drawList, rect.Center, IconGlyph.Of(icon), hovered ? ui.TitleInk : ui.MutedInk, IconScale);
        return clicked;
    }

    private static bool Pressable(AppSkin ui, ImDrawListPtr drawList, Rect rect, float scale,
        ImGuiMouseCursor cursor, out bool hovered)
    {
        hovered = UiInteract.Hover(rect.Min, rect.Max);
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, Metrics.Radius.Sm * scale, ImGui.GetColorU32(ui.HoverTint));
            ImGui.SetMouseCursor(cursor);
        }

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
