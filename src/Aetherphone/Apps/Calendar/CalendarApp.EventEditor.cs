using Aetherphone.Core;
using Aetherphone.Core.Calendar;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Calendar;

internal sealed partial class CalendarApp
{
    private const int TitleMaxLength = 60;
    private const int HoursPerDay = 24;
    private const int MinutesPerHour = 60;
    private const int MinuteStep = 5;
    private const float FieldLabelGap = 4f;
    private static readonly int LeadOptionCount = CalendarReminder.LeadOptionsMinutes.Length;

    private Guid editEventId;
    private bool editIsNew;
    private string editTitle = string.Empty;
    private DateTime editDate;
    private int editHour;
    private int editMinute;
    private int editLeadIndex;
    private int editGroupIndex;

    private int GroupChoiceCount => configuration.CalendarGroups.Count + 1;

    private void StartNewEvent()
    {
        editIsNew = true;
        editEventId = Guid.Empty;
        editTitle = string.Empty;
        editDate = selectedDate;
        var now = DateTime.Now;
        editHour = now.Hour;
        editMinute = now.Minute / MinuteStep * MinuteStep;
        editLeadIndex = CalendarReminder.LeadIndexOf(CalendarReminder.AtEventTime);
        editGroupIndex = 0;
        router.Push(CalendarScreen.EditEvent);
    }

    private void StartEditEvent(Guid id)
    {
        var item = FindCustomEvent(id);
        if (item is null)
        {
            return;
        }

        editIsNew = false;
        editEventId = id;
        editTitle = item.Title;
        editDate = item.When.Date;
        editHour = item.When.Hour;
        editMinute = item.When.Minute;
        editLeadIndex = CalendarReminder.LeadIndexOf(item.ReminderMinutesBefore);
        editGroupIndex = GroupIndexOf(item.GroupId) + 1;
        router.Push(CalendarScreen.EditEvent);
    }

    private int GroupIndexOf(Guid groupId)
    {
        if (groupId == Guid.Empty)
        {
            return -1;
        }

        var groups = configuration.CalendarGroups;
        for (var index = 0; index < groups.Count; index++)
        {
            if (groups[index].Id == groupId)
            {
                return index;
            }
        }

        return -1;
    }

    private void DrawEventEditor(Rect content, float scale)
    {
        var context = new PhoneContext(content, theme, navigation);
        AppHeader.Draw(context, Loc.T(editIsNew ? L.Calendar.NewEvent : L.Calendar.EditEvent), back);
        var margin = Metrics.Space.Lg * scale;
        var left = content.Min.X + margin;
        var right = content.Max.X - margin;
        var top = content.Min.Y + AppHeader.Height * scale + margin;
        var fieldHeight = EditorFieldHeight * scale;
        var gap = Metrics.Space.Md * scale;
        var labelHeight = Typography.Measure("A", TextStyles.Footnote).Y + FieldLabelGap * scale;

        var titleRect = new Rect(new Vector2(left, top), new Vector2(right, top + fieldHeight));
        DrawTextField(titleRect, scale, "##calEventTitle", Loc.T(L.Calendar.TitlePlaceholder), ref editTitle,
            TitleMaxLength);

        var dateRect = DrawFieldLabel(left, right, titleRect.Max.Y + gap, labelHeight, fieldHeight, L.Calendar.EventDate);
        StepperField.Draw(ui, dateRect, editDate.ToString("dddd, MMM d", Loc.Culture), scale, stepDateBack,
            stepDateForward);

        var timeRect = DrawFieldLabel(left, right, dateRect.Max.Y + gap, labelHeight, fieldHeight, L.Calendar.EventTime);
        var half = timeRect.Width * 0.5f - gap * 0.5f;
        var hourRect = new Rect(timeRect.Min, new Vector2(timeRect.Min.X + half, timeRect.Max.Y));
        var minuteRect = new Rect(new Vector2(timeRect.Max.X - half, timeRect.Min.Y), timeRect.Max);
        StepperField.Draw(ui, hourRect, editHour.ToString("D2"), scale, stepHourBack, stepHourForward);
        StepperField.Draw(ui, minuteRect, editMinute.ToString("D2"), scale, stepMinuteBack, stepMinuteForward);

        var alertRect = DrawFieldLabel(left, right, timeRect.Max.Y + gap, labelHeight, fieldHeight, L.Calendar.Alert);
        StepperField.Draw(ui, alertRect, Loc.T(CalendarReminder.LeadLabelAt(editLeadIndex)), scale, stepLeadBack,
            stepLeadForward);

        var groups = configuration.CalendarGroups;
        if (groups.Count > 0)
        {
            var groupRect = DrawFieldLabel(left, right, alertRect.Max.Y + gap, labelHeight, fieldHeight, L.Calendar.Group);
            StepperField.Draw(ui, groupRect, GroupChoiceLabel(groups), scale, stepGroupBack, stepGroupForward);
        }

        var saveRect = new Rect(new Vector2(left, content.Max.Y - margin - fieldHeight),
            new Vector2(right, content.Max.Y - margin));
        var enabled = HasText(editTitle);
        if (ui.AccentPill(saveRect, Loc.T(L.Calendar.Save), enabled, TextStyles.Headline) && enabled)
        {
            CommitEvent();
        }
    }

    private Rect DrawFieldLabel(float left, float right, float labelY, float labelHeight, float fieldHeight,
        LocString label)
    {
        Typography.Draw(new Vector2(left, labelY), Loc.T(label), ui.MutedInk, TextStyles.Footnote);
        var fieldTop = labelY + labelHeight;
        return new Rect(new Vector2(left, fieldTop), new Vector2(right, fieldTop + fieldHeight));
    }

    private string GroupChoiceLabel(IReadOnlyList<CalendarEventGroup> groups)
    {
        if (editGroupIndex <= 0 || editGroupIndex > groups.Count)
        {
            return Loc.T(L.Calendar.NoGroup);
        }

        return groups[editGroupIndex - 1].Name;
    }

    private void CommitEvent()
    {
        var title = editTitle.Trim();
        if (title.Length == 0)
        {
            return;
        }

        var when = new DateTime(editDate.Year, editDate.Month, editDate.Day, editHour, editMinute, 0);
        var groups = configuration.CalendarGroups;
        var groupId = editGroupIndex > 0 && editGroupIndex <= groups.Count ? groups[editGroupIndex - 1].Id : Guid.Empty;
        var reminderMinutesBefore = CalendarReminder.LeadOptionsMinutes[editLeadIndex];
        if (editIsNew)
        {
            configuration.CalendarCustomEvents.Add(new CalendarCustomEvent
            {
                Title = title,
                When = when,
                GroupId = groupId,
                ReminderMinutesBefore = reminderMinutesBefore,
            });
        }
        else
        {
            var item = FindCustomEvent(editEventId);
            if (item is not null)
            {
                item.Title = title;
                item.When = when;
                item.GroupId = groupId;
                item.ReminderMinutesBefore = reminderMinutesBefore;
                if (CalendarReminder.TryFireTime(item, out var fireTime) && fireTime > DateTime.Now)
                {
                    item.Notified = false;
                }
            }
        }

        SaveCalendar();
        selectedDate = when.Date;
        var today = DateTime.Today;
        monthOffset = ((when.Year - today.Year) * 12) + when.Month - today.Month;
        router.Pop();
    }
}
