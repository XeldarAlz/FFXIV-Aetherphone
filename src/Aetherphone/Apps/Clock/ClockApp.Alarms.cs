using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Clock;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Clock;

internal sealed partial class ClockApp
{
    private const float AlarmRowHeight = 68f;
    private const int AlarmLabelMaxLength = 40;

    private static readonly DayOfWeek[] WeekOrder =
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
        DayOfWeek.Saturday, DayOfWeek.Sunday,
    };

    private Guid editAlarmId;
    private int editHour;
    private int editMinute;
    private byte editRepeat;
    private string editLabel = string.Empty;
    private bool editIsNew;

    private void DrawAlarms(Rect body, float scale)
    {
        using (AppSurface.Begin(body))
        {
            var alarms = configuration.Alarms;
            if (alarms.Count == 0)
            {
                Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 70f * scale),
                    Loc.T(L.Clock.AlarmsEmpty), ui.MutedInk, TextStyles.Subheadline);
                return;
            }

            var card = BeginRowCard(alarms.Count, AlarmRowHeight, scale);
            for (var index = 0; index < alarms.Count; index++)
            {
                DrawAlarmRow(RowAt(card, index, AlarmRowHeight, scale), alarms[index]);
            }

            EndRowCard(card, 10f, scale);
        }
    }

    private void DrawAlarmRow(Rect row, AlarmEntry alarm)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var timeInk = alarm.Enabled ? ui.TitleInk : ui.MutedInk;
        var time = $"{alarm.Hour:D2}:{alarm.Minute:D2}";
        var timeSize = Typography.Measure(time, TextStyles.Title1);
        Typography.Draw(new Vector2(row.Min.X, row.Center.Y - timeSize.Y * 0.5f), time, timeInk, TextStyles.Title1);

        var subtitle = AlarmSchedule.RepeatLabel(alarm);
        if (alarm.Label.Length > 0)
        {
            subtitle = subtitle.Length > 0 ? $"{alarm.Label} · {subtitle}" : alarm.Label;
        }

        Typography.Draw(new Vector2(row.Min.X + timeSize.X + 12f * scale, row.Center.Y - 8f * scale), subtitle,
            ui.MutedInk, TextStyles.Footnote);

        var width = Metrics.Size.ToggleWidth * scale;
        var height = Metrics.Size.ToggleHeight * scale;
        var toggleMin = new Vector2(row.Max.X - width, row.Center.Y - height * 0.5f);
        var toggleRect = new Rect(toggleMin, toggleMin + new Vector2(width, height));
        var newValue = Toggle.Draw($"alarm.{alarm.Id}", toggleRect, alarm.Enabled, theme);
        if (newValue != alarm.Enabled)
        {
            alarm.Enabled = newValue;
            configuration.Save();
        }

        var tapRect = new Rect(row.Min, new Vector2(toggleMin.X - 8f * scale, row.Max.Y));
        if (UiInteract.HoverClick(tapRect.Min, tapRect.Max))
        {
            StartEditAlarm(alarm);
        }
    }

    private void StartNewAlarm()
    {
        var now = DateTime.Now;
        editIsNew = true;
        editAlarmId = Guid.Empty;
        editHour = now.Hour;
        editMinute = now.Minute;
        editRepeat = 0;
        editLabel = string.Empty;
        router.Push(ClockScreen.EditAlarm);
    }

    private void StartEditAlarm(AlarmEntry alarm)
    {
        editIsNew = false;
        editAlarmId = alarm.Id;
        editHour = alarm.Hour;
        editMinute = alarm.Minute;
        editRepeat = alarm.RepeatDays;
        editLabel = alarm.Label;
        router.Push(ClockScreen.EditAlarm);
    }

    private void DrawAlarmEditor(Rect content, float scale)
    {
        var context = new PhoneContext(content, theme, navigation);
        var title = editIsNew ? Loc.T(L.Clock.NewAlarm) : Loc.T(L.Clock.EditAlarm);
        AppHeader.Draw(context, title, back);

        var margin = Metrics.Space.Lg * scale;
        var gap = Metrics.Space.Md * scale;
        var top = content.Min.Y + AppHeader.Height * scale + Metrics.Space.Lg * scale;
        var left = content.Min.X + margin;
        var right = content.Max.X - margin;

        var timeHeight = 60f * scale;
        var timeRow = new Rect(new Vector2(left, top), new Vector2(right, top + timeHeight));
        var half = timeRow.Width * 0.5f - 12f * scale;
        var hourRect = new Rect(timeRow.Min, new Vector2(timeRow.Min.X + half, timeRow.Max.Y));
        var minuteRect = new Rect(new Vector2(timeRow.Max.X - half, timeRow.Min.Y), timeRow.Max);
        StepperField.Draw(ui, hourRect, editHour.ToString("D2"), scale, () => editHour = (editHour + 23) % 24,
            () => editHour = (editHour + 1) % 24);
        StepperField.Draw(ui, minuteRect, editMinute.ToString("D2"), scale, () => editMinute = (editMinute + 59) % 60,
            () => editMinute = (editMinute + 1) % 60);
        Typography.DrawCentered(ImGui.GetWindowDrawList(), timeRow.Center, ":", ui.TitleInk, TextStyles.Title1.Scale,
            TextStyles.Title1.Weight);

        var repeatLabelY = timeRow.Max.Y + gap;
        Typography.Draw(new Vector2(left, repeatLabelY), Loc.T(L.Clock.Repeat), ui.MutedInk, TextStyles.Footnote);
        var chipTop = repeatLabelY + Typography.Measure("A", TextStyles.Footnote).Y + Metrics.Space.Xs * scale;
        var chipHeight = 34f * scale;
        var chipGap = 6f * scale;
        var chipWidth = (timeRow.Width - chipGap * (WeekOrder.Length - 1)) / WeekOrder.Length;
        var abbreviations = Loc.Culture.DateTimeFormat.AbbreviatedDayNames;
        for (var index = 0; index < WeekOrder.Length; index++)
        {
            var day = WeekOrder[index];
            var chipMin = new Vector2(left + index * (chipWidth + chipGap), chipTop);
            var chipRect = new Rect(chipMin, chipMin + new Vector2(chipWidth, chipHeight));
            var active = (editRepeat & (1 << (int)day)) != 0;
            if (ui.Chip(chipRect, abbreviations[(int)day], active))
            {
                editRepeat = (byte)(editRepeat ^ (1 << (int)day));
            }
        }

        var labelTop = chipTop + chipHeight + gap;
        var labelHeight = Metrics.Size.Row * scale;
        var labelRect = new Rect(new Vector2(left, labelTop), new Vector2(right, labelTop + labelHeight));
        DrawAlarmLabelField(labelRect, scale);

        var buttonHeight = Metrics.Size.Row * scale;
        var saveTop = content.Max.Y - margin - buttonHeight;
        if (!editIsNew)
        {
            var deleteTop = saveTop - buttonHeight - gap;
            var deleteRect = new Rect(new Vector2(left, deleteTop), new Vector2(right, deleteTop + buttonHeight));
            if (DrawPillButton(deleteRect, Loc.T(L.Clock.DeleteAlarm), Palette.WithAlpha(theme.Danger, 0.16f),
                    theme.Danger))
            {
                AskDeleteAlarm(editAlarmId);
            }
        }

        var saveRect = new Rect(new Vector2(left, saveTop), new Vector2(right, saveTop + buttonHeight));
        if (DrawPillButton(saveRect, Loc.T(L.Clock.Save), ui.Accent, new Vector4(1f, 1f, 1f, 1f)))
        {
            CommitAlarm();
        }
    }

    private void DrawAlarmLabelField(Rect rect, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, rect.Min, rect.Max, Metrics.Radius.Md * scale);
        ImGui.SetCursorScreenPos(new Vector2(rect.Min.X + Metrics.Space.Md * scale,
            rect.Min.Y + rect.Height * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(rect.Width - Metrics.Space.Md * 2f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            ImGui.InputTextWithHint("##alarmLabel", Loc.T(L.Clock.AlarmLabelHint), ref editLabel, AlarmLabelMaxLength,
                ImGuiInputTextFlags.None);
        }
    }

    private void CommitAlarm()
    {
        if (editIsNew)
        {
            configuration.Alarms.Add(new AlarmEntry
            {
                Hour = editHour,
                Minute = editMinute,
                RepeatDays = editRepeat,
                Label = editLabel.Trim(),
                Enabled = true,
            });
        }
        else
        {
            var alarm = configuration.Alarms.Find(entry => entry.Id == editAlarmId);
            if (alarm is not null)
            {
                alarm.Hour = editHour;
                alarm.Minute = editMinute;
                alarm.RepeatDays = editRepeat;
                alarm.Label = editLabel.Trim();
                alarm.Enabled = true;
                alarm.LastFiredEpochMinute = 0;
            }
        }

        configuration.Save();
        router.Pop();
    }

    private void AskDeleteAlarm(Guid id)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Clock.DeleteAlarmConfirm),
            ConfirmLabel = Loc.T(L.Clock.Delete),
            CancelLabel = Loc.T(L.Clock.KeepIt),
            Confirm = () => DeleteAlarm(id),
        });
    }

    private void DeleteAlarm(Guid id)
    {
        configuration.Alarms.RemoveAll(entry => entry.Id == id);
        configuration.Save();
        router.Pop();
    }
}
