using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Clock;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Clock;

internal sealed partial class ClockApp
{
    private const int AlarmLabelMaxLength = 40;
    private const float KindHeight = 32f;
    private const float EditorGap = 14f;
    private const float WheelCardPad = 8f;
    private const float WheelColumnWidth = 70f;
    private const float WheelColumnGap = 14f;
    private const float WheelBandInset = 8f;
    private const float WheelBandRadius = 10f;
    private const float WheelBandAlpha = 0.08f;
    private const float RingsLineGap = 8f;
    private const float OptionRowHeight = 50f;
    private const float DayRowHeight = 52f;
    private const float DayChipDiameter = 36f;
    private const float LabelFieldFraction = 0.62f;
    private const float StepperRadius = RoundButton.SmallRadius;
    private const float StepperGlyph = 12f;
    private const float StepperValueGap = 10f;

    private static readonly DayOfWeek[] WeekOrder =
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
        DayOfWeek.Saturday, DayOfWeek.Sunday,
    };

    private static readonly string[] Hours24 = BuildPairs(24);
    private static readonly string[] Minutes60 = BuildPairs(60);

    private readonly AlarmEntry draft = new();
    private byte draftLocalRepeatDays;
    private readonly ClockWheel alarmHourWheel = new("alarm.hour");
    private readonly ClockWheel alarmMinuteWheel = new("alarm.minute");
    private readonly ClockWheel alarmMeridiemWheel = new("alarm.meridiem");
    private readonly string[] kindOptions = new string[2];
    private readonly string[] hours12 = new string[12];
    private readonly string[] meridiems = new string[2];
    private int labelFormat = -1;
    private CultureInfo? labelCulture;
    private string[] dayNames = Array.Empty<string>();
    private CultureInfo? dayCulture;
    private Guid editAlarmId;
    private bool editIsNew;
    private string editLabel = string.Empty;
    private bool editorPrimed;
    private int editorWheelMode = -1;
    private bool editorWheelHovered;
    private CachedText ringsLine;
    private CachedText repeatSummary;
    private CachedText snoozeValue;

    private static string[] BuildPairs(int count)
    {
        var labels = new string[count];
        for (var index = 0; index < count; index++)
        {
            labels[index] = ClockArt.Pair(index);
        }

        return labels;
    }

    private void StartNewAlarm()
    {
        var now = DateTime.Now;
        editIsNew = true;
        editAlarmId = Guid.Empty;
        draft.Hour = now.Hour;
        draft.Minute = now.Minute;
        draft.RepeatDays = 0;
        draft.Eorzea = false;
        draft.SnoozeMinutes = AlarmSchedule.DefaultSnoozeMinutes;
        editLabel = string.Empty;
        OpenEditor();
    }

    private void StartEditAlarm(AlarmEntry alarm)
    {
        editIsNew = false;
        editAlarmId = alarm.Id;
        draft.Hour = alarm.Hour;
        draft.Minute = alarm.Minute;
        draft.RepeatDays = alarm.RepeatDays;
        draft.Eorzea = alarm.Eorzea;
        draft.SnoozeMinutes = alarm.SnoozeMinutes;
        editLabel = alarm.Label;
        OpenEditor();
    }

    private void OpenEditor()
    {
        draftLocalRepeatDays = draft.Eorzea ? (byte)0 : draft.RepeatDays;
        editorPrimed = false;
        editorWheelMode = -1;
        router.Push(ClockScreen.EditAlarm);
    }

    private void DrawAlarmEditor(in PhoneContext context)
    {
        var scale = UiScale.Current;
        RefreshEditorLabels();
        var navBar = AppHeader.BeginLargeTitle(context);
        using (ImRaii.PushId("clock.editor"))
        using (var surface = AppSurface.Begin(navBar.Body, editorWheelHovered))
        {
            var width = ImGui.GetContentRegionAvail().X;
            DrawKindPicker(width, scale);
            var wheelsActive = DrawAlarmWheels(width, scale);
            DrawRingsLine(width, scale);
            DrawRepeatBlock(width, scale);
            DrawAlarmDetails(width, scale);
            if (!editIsNew)
            {
                DrawDeleteAlarm(width, scale);
            }

            ImGui.Dummy(new Vector2(0f, ClockArt.BottomPad * scale));
            if (wheelsActive)
            {
                surface.CancelDrag();
            }
        }

        navButtons[0] = new NavBarButton(PhoneIcons.Check, Loc.T(L.Clock.Save));
        UiAnchors.Report("clock.alarm.save", AppHeader.LargeTitleButtonRect(in navBar, 0, 1));
        var title = Loc.T(editIsNew ? L.Clock.NewAlarm : L.Clock.EditAlarm);
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "clock.editor.nav", title, NavBarStyle.From(ui),
            navButtons.AsSpan(0, 1), Loc.T(L.Clock.TabAlarms), back);
        if (pressed == 0)
        {
            CommitAlarm();
        }
    }

    private void RefreshEditorLabels()
    {
        kindOptions[0] = Loc.T(L.Clock.LocalTime);
        kindOptions[1] = Loc.T(L.Clock.EorzeaTime);
        if (labelFormat == TimeText.FormatVersion && ReferenceEquals(labelCulture, Loc.Culture))
        {
            return;
        }

        labelFormat = TimeText.FormatVersion;
        labelCulture = Loc.Culture;
        for (var hour = 0; hour < hours12.Length; hour++)
        {
            hours12[hour] = TimeText.HourLabel(hour);
        }

        meridiems[0] = TimeText.MeridiemLabel(false);
        meridiems[1] = TimeText.MeridiemLabel(true);
        editorWheelMode = -1;
    }

    private bool TwelveHourWheels => !draft.Eorzea && !TimeText.Use24Hour;

    private void DrawKindPicker(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = KindHeight * scale;
        var rect = new Rect(origin, origin + new Vector2(width, height));
        var selected = SegmentStrip.Draw("clock.alarm.kind", rect, kindOptions, draft.Eorzea ? 1 : 0, ui.Palette);
        var eorzea = selected == 1;
        if (eorzea != draft.Eorzea)
        {
            draft.Eorzea = eorzea;
            if (eorzea)
            {
                draftLocalRepeatDays = draft.RepeatDays;
                if (draft.Repeats)
                {
                    draft.RepeatDays = AlarmSchedule.EveryDayMask;
                }
            }
            else if (draft.Repeats && draftLocalRepeatDays != 0)
            {
                draft.RepeatDays = draftLocalRepeatDays;
            }
        }

        ClockArt.Advance(origin, width, height, EditorGap, scale);
    }

    private bool DrawAlarmWheels(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var pad = WheelCardPad * scale;
        var height = ClockWheel.HeightUnits * scale + pad * 2f;
        var card = new Rect(origin, origin + new Vector2(width, height));
        UiAnchors.Report("clock.alarm.time", card);
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, card.Min, card.Max, Metrics.Radius.Grouped * scale);
        DrawWheelBand(drawList, card, scale);

        var twelve = TwelveHourWheels;
        var mode = twelve ? 1 : 0;
        if (!editorPrimed || mode != editorWheelMode)
        {
            editorPrimed = true;
            editorWheelMode = mode;
            alarmHourWheel.Snap(twelve ? draft.Hour % 12 : draft.Hour);
            alarmMinuteWheel.Snap(draft.Minute);
            alarmMeridiemWheel.Snap(draft.Hour >= 12 ? 1 : 0);
        }

        var columns = twelve ? 3 : 2;
        var columnWidth = WheelColumnWidth * scale;
        var gap = WheelColumnGap * scale;
        var total = columns * columnWidth + (columns - 1) * gap;
        var left = card.Center.X - total * 0.5f;
        var top = card.Min.Y + pad;
        var bottom = card.Max.Y - pad;
        var style = new WheelStyle(ui.TitleInk, ui.MutedInk, TextStyles.Title2);
        var hourRect = new Rect(new Vector2(left, top), new Vector2(left + columnWidth, bottom));
        var minuteRect = hourRect.Translate(new Vector2(columnWidth + gap, 0f));
        if (twelve)
        {
            var pm = draft.Hour >= 12;
            var hour = alarmHourWheel.Draw(drawList, hourRect, draft.Hour % 12, hours12, true, style);
            var meridiemRect = minuteRect.Translate(new Vector2(columnWidth + gap, 0f));
            var meridiem = alarmMeridiemWheel.Draw(drawList, meridiemRect, pm ? 1 : 0, meridiems, false, style);
            draft.Hour = hour + (meridiem == 1 ? 12 : 0);
        }
        else
        {
            draft.Hour = alarmHourWheel.Draw(drawList, hourRect, draft.Hour, Hours24, true, style);
        }

        draft.Minute = alarmMinuteWheel.Draw(drawList, minuteRect, draft.Minute, Minutes60, true, style);
        editorWheelHovered = alarmHourWheel.Hovered || alarmMinuteWheel.Hovered ||
                             (twelve && alarmMeridiemWheel.Hovered);
        ClockArt.Advance(origin, width, height, 0f, scale);
        return alarmHourWheel.Active || alarmMinuteWheel.Active || (twelve && alarmMeridiemWheel.Active);
    }

    private void DrawWheelBand(ImDrawListPtr drawList, Rect card, float scale)
    {
        var inset = WheelBandInset * scale;
        var half = ClockWheel.RowUnits * scale * 0.5f;
        drawList.AddRectFilled(new Vector2(card.Min.X + inset, card.Center.Y - half),
            new Vector2(card.Max.X - inset, card.Center.Y + half),
            ImGui.GetColorU32(Palette.WithAlpha(ui.TitleInk, WheelBandAlpha)), WheelBandRadius * scale);
    }

    private void DrawRingsLine(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var lineHeight = Typography.LineHeight(TextStyles.Footnote);
        var height = lineHeight + RingsLineGap * 2f * scale;
        var now = DateTime.Now;
        var key = ((now.Ticks / TimeSpan.TicksPerMinute) << 20) ^ ((long)draft.MinuteOfDay << 8) ^
                  ((long)draft.RepeatDays << 1) ^ (draft.Eorzea ? 1L : 0L);
        var text = ringsLine.IsCurrent(key) ? ringsLine.Value : ringsLine.Store(key, RingsText(now));
        var fitted = Typography.FitText(text, width, TextStyles.Footnote);
        var textWidth = Typography.Measure(fitted, TextStyles.Footnote).X;
        Typography.Draw(ImGui.GetWindowDrawList(),
            new Vector2(origin.X + (width - textWidth) * 0.5f, origin.Y + RingsLineGap * scale), fitted, ui.MutedInk,
            TextStyles.Footnote);
        ClockArt.Advance(origin, width, height, EditorGap - RingsLineGap, scale);
    }

    private string RingsText(DateTime now)
    {
        var next = AlarmSchedule.NextOccurrence(draft, now);
        var until = TimeText.Until(next - now);
        return draft.Eorzea
            ? Loc.T(L.Clock.RingsAtLocal, TimeText.Clock(next), until)
            : Loc.T(L.Clock.RingsIn, until);
    }

    private void DrawRepeatBlock(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var rowHeight = OptionRowHeight * scale;
        var height = draft.Eorzea ? rowHeight : rowHeight + DayRowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + height);
        UiAnchors.Report("clock.alarm.repeat", new Rect(origin, max));
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var inset = Metrics.Space.Lg * scale;
        var left = origin.X + inset;
        var right = max.X - inset;
        var titleRow = new Rect(origin, new Vector2(max.X, origin.Y + rowHeight));
        if (draft.Eorzea)
        {
            DrawOptionTitle(drawList, left, titleRow, Loc.T(L.Clock.RepeatEorzeaDaily));
            var toggleSize = new Vector2(Metrics.Size.ToggleWidth, Metrics.Size.ToggleHeight) * scale;
            var toggleMin = new Vector2(right - toggleSize.X, titleRow.Center.Y - toggleSize.Y * 0.5f);
            var repeats = Toggle.Draw("clock.alarm.eorzeaRepeat", new Rect(toggleMin, toggleMin + toggleSize),
                draft.Repeats, theme);
            draft.RepeatDays = repeats ? AlarmSchedule.EveryDayMask : (byte)0;
            ClockArt.Advance(origin, width, height, EditorGap, scale);
            return;
        }

        DrawOptionTitle(drawList, left, titleRow, Loc.T(L.Clock.Repeat));
        var summary = repeatSummary.IsCurrent(draft.RepeatDays)
            ? repeatSummary.Value
            : repeatSummary.Store(draft.RepeatDays, AlarmSchedule.RepeatLabel(draft));
        var summaryWidth = MathF.Min(Typography.Measure(summary, TextStyles.Subheadline).X, width * 0.55f);
        var summaryHeight = Typography.LineHeight(TextStyles.Subheadline);
        Typography.Draw(drawList, new Vector2(right - summaryWidth, titleRow.Center.Y - summaryHeight * 0.5f),
            Typography.FitText(summary, summaryWidth, TextStyles.Subheadline), ui.MutedInk, TextStyles.Subheadline);
        DrawDayChips(drawList, left, right, titleRow.Max.Y, scale);
        ClockArt.Advance(origin, width, height, EditorGap, scale);
    }

    private void DrawOptionTitle(ImDrawListPtr drawList, float left, Rect row, string title)
    {
        var height = Typography.LineHeight(TextStyles.Body);
        Typography.Draw(drawList, new Vector2(left, row.Center.Y - height * 0.5f), title, ui.TitleInk,
            TextStyles.Body);
    }

    private void DrawDayChips(ImDrawListPtr drawList, float left, float right, float top, float scale)
    {
        var names = DayNames();
        var diameter = DayChipDiameter * scale;
        var radius = diameter * 0.5f;
        var step = (right - left - diameter) / (WeekOrder.Length - 1);
        var centerY = top + DayRowHeight * scale * 0.5f - Metrics.Space.Xxs * scale;
        var tapTarget = Metrics.Size.TapTarget * scale;
        var hit = new Vector2(MathF.Min(tapTarget, step), tapTarget) * 0.5f;
        for (var index = 0; index < WeekOrder.Length; index++)
        {
            var day = WeekOrder[index];
            var center = new Vector2(left + radius + index * step, centerY);
            var corner = new Vector2(radius, radius);
            var active = draft.RepeatsOn(day);
            var hovered = UiInteract.Hover(center - hit, center + hit);
            var face = RoundButton.Surface(drawList, new Rect(center - corner, center + corner), ui.Ink,
                active ? ButtonStyle.Prominent : ButtonStyle.Gray, true, hovered, ImGui.GetID($"clock.day.{index}"));
            var label = Typography.FitText(names[(int)day], diameter * 0.8f, TextStyles.FootnoteEmphasized);
            Typography.DrawCentered(drawList, center, label, face.LabelInk, TextStyles.FootnoteEmphasized);
            if (UiInteract.Click(center - hit, center + hit, hovered))
            {
                draft.ToggleDay(day);
            }
        }
    }

    private string[] DayNames()
    {
        if (ReferenceEquals(dayCulture, Loc.Culture))
        {
            return dayNames;
        }

        dayCulture = Loc.Culture;
        dayNames = Loc.Culture.DateTimeFormat.ShortestDayNames;
        return dayNames;
    }

    private void DrawAlarmDetails(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var rowHeight = OptionRowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight * 2f);
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var inset = Metrics.Space.Lg * scale;
        var left = origin.X + inset;
        var right = max.X - inset;
        var labelRow = new Rect(origin, new Vector2(max.X, origin.Y + rowHeight));
        var snoozeRow = labelRow.Translate(new Vector2(0f, rowHeight));
        FeedCell.Hairline(drawList, left, max.X, snoozeRow.Min.Y, ui.Hairline);
        DrawOptionTitle(drawList, left, labelRow, Loc.T(L.Clock.Label));
        DrawLabelInput(labelRow, right, width, "##clockAlarmLabel", Loc.T(L.Clock.AlarmLabelHint), ref editLabel);
        DrawOptionTitle(drawList, left, snoozeRow, Loc.T(L.Clock.Snooze));
        DrawSnoozeStepper(drawList, snoozeRow, right, scale);
        ClockArt.Advance(origin, width, max.Y - origin.Y, EditorGap, scale);
    }

    private void DrawLabelInput(Rect row, float right, float width, string id, string hint, ref string text)
    {
        var fieldWidth = width * LabelFieldFraction;
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(new Vector2(right - fieldWidth, row.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(fieldWidth);
        Plugin.Fonts.NoticeText(text);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.FrameBgHovered, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.FrameBgActive, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        using (ImRaii.PushColor(ImGuiCol.TextDisabled, ui.MutedInk))
        {
            ImGui.InputTextWithHint(id, hint, ref text, AlarmLabelMaxLength, ImGuiInputTextFlags.None);
        }

        ImGui.SetCursorScreenPos(cursor);
    }

    private void DrawSnoozeStepper(ImDrawListPtr drawList, Rect row, float right, float scale)
    {
        var radius = StepperRadius * scale;
        var plusCenter = new Vector2(right - radius, row.Center.Y);
        var value = draft.SnoozeMinutes;
        var text = snoozeValue.IsCurrent(value)
            ? snoozeValue.Value
            : snoozeValue.Store(value, value <= 0 ? Loc.T(L.Clock.SnoozeOff) : Loc.T(L.Clock.DurationMinutes, value));
        var textWidth = Typography.Measure(text, TextStyles.Body).X;
        var textRight = plusCenter.X - radius - StepperValueGap * scale;
        var minusCenter = new Vector2(textRight - textWidth - StepperValueGap * scale - radius, row.Center.Y);
        if (StepperButton(drawList, "clock.snooze.minus", minusCenter, radius, false, value > 0, scale))
        {
            draft.SnoozeMinutes = Math.Max(0, value - 1);
        }

        if (StepperButton(drawList, "clock.snooze.plus", plusCenter, radius, true,
                value < AlarmSchedule.MaxSnoozeMinutes, scale))
        {
            draft.SnoozeMinutes = Math.Min(AlarmSchedule.MaxSnoozeMinutes, value + 1);
        }

        var textHeight = Typography.LineHeight(TextStyles.Body);
        Typography.Draw(drawList, new Vector2(textRight - textWidth, row.Center.Y - textHeight * 0.5f), text,
            value > 0 ? ui.TitleInk : ui.MutedInk, TextStyles.Body);
    }

    private bool StepperButton(ImDrawListPtr drawList, string id, Vector2 center, float radius, bool plus,
        bool enabled, float scale) =>
        RoundButton.FontIcon(drawList, id, center, radius, plus ? FontAwesomeIcon.Plus : FontAwesomeIcon.Minus,
            StepperGlyph * scale, ui.Ink, ButtonStyle.Gray, enabled: enabled);

    private void DrawDeleteAlarm(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = Metrics.Size.Pill * scale;
        var rect = new Rect(origin, origin + new Vector2(width, height));
        if (ui.DangerGhostButton(rect, Loc.T(L.Clock.DeleteAlarm)))
        {
            AskDeleteAlarm(editAlarmId, true);
        }

        ClockArt.Advance(origin, width, height, 0f, scale);
    }

    private void CommitAlarm()
    {
        var target = editIsNew ? new AlarmEntry() : configuration.Alarms.Find(entry => entry.Id == editAlarmId);
        if (target is null)
        {
            router.Pop();
            return;
        }

        target.Hour = draft.Hour;
        target.Minute = draft.Minute;
        target.Eorzea = draft.Eorzea;
        target.RepeatDays = draft.Eorzea && draft.Repeats ? AlarmSchedule.EveryDayMask : draft.RepeatDays;
        target.SnoozeMinutes = draft.SnoozeMinutes;
        target.Label = editLabel.Trim();
        target.Enabled = true;
        var now = DateTime.Now;
        AlarmSchedule.Arm(target, now, now.ToUniversalTime());
        if (editIsNew)
        {
            configuration.Alarms.Add(target);
        }

        configuration.Save();
        alarmsDirty = true;
        UiFeedback.Play(UiSound.Success);
        ShowRingsToast(target, now);
        router.Pop();
    }
}
