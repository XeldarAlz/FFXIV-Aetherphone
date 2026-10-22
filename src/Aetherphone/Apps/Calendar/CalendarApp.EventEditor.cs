using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Calendar;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Aetherphone.Windows.Widgets;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Calendar;

internal enum EditorPanel : byte
{
    None,
    Date,
    Time,
    Duration,
}

internal sealed partial class CalendarApp
{
    private const int TitleMaxLength = 60;
    private const int NotesMaxLength = 500;
    private const int MinutesPerHour = 60;
    private const int MinutesPerDay = 1440;
    private const int DurationStep = 15;
    private const float NotesHeight = 88f;
    private const float ValuePillHeight = 30f;
    private const float ValuePillPadX = 12f;
    private const float ValueFillAlpha = 0.14f;
    private const float PanelPad = 12f;
    private const float PickerHeaderHeight = 36f;
    private const float PickerChevronRadius = RoundButton.SmallRadius;
    private const float StepperHeight = 44f;
    private const float StepperRadius = RoundButton.RegularRadius;

    private const float MenuGlyphSize = 10f;
    private const float MenuGlyphGap = 6f;
    private const float PanelHideThreshold = 0.01f;

    private Guid editEventId;
    private bool editIsNew;
    private string editTitle = string.Empty;
    private string editNotes = string.Empty;
    private DateTime editDate;
    private int editMinuteOfDay;
    private int editDuration;
    private CalendarRepeat editRepeat;
    private int editLeadIndex;
    private int editGroupIndex;
    private string editBackTitle = string.Empty;
    private EditorPanel editPanel;
    private EditorPanel shownPanel;
    private Spring panelReveal;
    private DateTime pickerMonth;
    private CachedText editDateLabel;
    private CachedText editTimeLabel;
    private CachedText editDurationLabel;
    private CachedText pickerTitle;

    private void StartNewEvent(DateTime day, string backTitle)
    {
        editIsNew = true;
        editEventId = Guid.Empty;
        editTitle = string.Empty;
        editNotes = string.Empty;
        editDate = day.Date;
        var now = DateTime.Now;
        var nextHour = Math.Min(now.Hour + 1, 23);
        editMinuteOfDay = nextHour * MinutesPerHour;
        editDuration = MinutesPerHour;
        editRepeat = CalendarRepeat.None;
        editLeadIndex = CalendarReminder.LeadIndexOf(CalendarReminder.AtEventTime);
        editGroupIndex = 0;
        OpenEditor(backTitle);
    }

    private void StartEditEvent(Guid id, string backTitle)
    {
        var item = FindCustomEvent(id);
        if (item is null)
        {
            return;
        }

        editIsNew = false;
        editEventId = id;
        editTitle = item.Title;
        editNotes = item.Notes;
        editDate = item.When.Date;
        editMinuteOfDay = item.When.Hour * MinutesPerHour + item.When.Minute;
        editDuration = Math.Clamp(item.DurationMinutes, 0, MinutesPerDay);
        editRepeat = item.Repeat;
        editLeadIndex = CalendarReminder.LeadIndexOf(item.ReminderMinutesBefore);
        editGroupIndex = GroupIndexOf(item.GroupId) + 1;
        OpenEditor(backTitle);
    }

    private void OpenEditor(string backTitle)
    {
        editBackTitle = backTitle;
        editPanel = EditorPanel.None;
        shownPanel = EditorPanel.None;
        panelReveal.SnapTo(0f);
        pickerMonth = new DateTime(editDate.Year, editDate.Month, 1);
        Push(CalendarScreen.EditEvent);
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

    private void DrawEventEditor(Rect area)
    {
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        var scale = UiScale.Current;
        using (AppSurface.Begin(navBar.Body))
        {
            var drawList = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = ScrollLayout.StableContentWidth();
            var cursorY = DrawTextCard(drawList, origin, width, scale);
            cursorY = DrawWhenCard(drawList, new Vector2(origin.X, cursorY + CalendarArt.CardGap * scale), width,
                scale);
            cursorY = DrawOptionsCard(drawList, new Vector2(origin.X, cursorY + CalendarArt.CardGap * scale), width,
                scale);
            var saveTop = cursorY + CalendarArt.SectionGap * scale;
            var saveRect = new Rect(new Vector2(origin.X, saveTop),
                new Vector2(origin.X + width, saveTop + CalendarArt.PillHeight * scale));
            var enabled = HasText(editTitle);
            var label = Loc.T(editIsNew ? L.Calendar.AddEvent : L.Calendar.Save);
            if (ui.AccentPill(saveRect, label, enabled, TextStyles.Headline))
            {
                CommitEvent();
            }

            CalendarArt.Reserve(origin, width, saveRect.Max.Y + CalendarArt.BottomPad * scale);
        }

        AppHeader.EndLargeTitle(in navBar, context, "calendar.editor.nav",
            Loc.T(editIsNew ? L.Calendar.NewEvent : L.Calendar.EditEvent), NavBarStyle.From(ui),
            ReadOnlySpan<NavBarButton>.Empty, editBackTitle, back);
    }

    private float DrawTextCard(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var rowHeight = CalendarArt.FieldRowHeight * scale;
        var notesHeight = NotesHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight + notesHeight);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var pad = Metrics.Space.Lg * scale;
        var titleRow = new Rect(origin, new Vector2(max.X, origin.Y + rowHeight));
        ImGui.SetCursorScreenPos(new Vector2(titleRow.Min.X + pad, titleRow.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(titleRow.Width - pad * 2f);
        using (UiScope.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (UiScope.PushColor(ImGuiCol.FrameBgHovered, AppSkin.Transparent))
        using (UiScope.PushColor(ImGuiCol.FrameBgActive, AppSkin.Transparent))
        using (UiScope.PushColor(ImGuiCol.Text, ui.TitleInk))
        using (UiScope.PushColor(ImGuiCol.TextDisabled, ui.MutedInk))
        {
            var hint = Loc.T(L.Calendar.TitlePlaceholder);
            Plugin.Fonts.NoticeText(hint);
            Plugin.Fonts.NoticeText(editTitle);
            ImGui.InputTextWithHint("##calendarEventTitle", hint, ref editTitle, TitleMaxLength);
        }

        CalendarArt.Hairline(drawList, ui, origin.X + pad, max.X, titleRow.Max.Y);
        var notesMin = new Vector2(origin.X + pad, titleRow.Max.Y + Metrics.Space.Xs * scale);
        var notesSize = new Vector2(width - pad * 2f, max.Y - notesMin.Y - Metrics.Space.Xs * scale);
        var framePadding = ImGui.GetStyle().FramePadding;
        ImGui.SetCursorScreenPos(notesMin);
        using (UiScope.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (UiScope.PushColor(ImGuiCol.FrameBgHovered, AppSkin.Transparent))
        using (UiScope.PushColor(ImGuiCol.FrameBgActive, AppSkin.Transparent))
        using (UiScope.PushColor(ImGuiCol.Text, ui.TitleInk))
        using (Plugin.Fonts.Push(TextStyles.Body.Scale, TextStyles.Body.Weight))
        {
            var wrapWidth = notesSize.X - framePadding.X * 2f - Metrics.Space.Xxs * scale;
            SoftWrapField.Multiline("##calendarEventNotes", ref editNotes, NotesMaxLength, notesSize, wrapWidth);
        }

        if (editNotes.Length == 0)
        {
            Typography.DrawWrappedLeft(notesMin + framePadding, Loc.T(L.Calendar.NotesPlaceholder), ui.MutedInk,
                TextStyles.Body, notesSize.X - framePadding.X * 2f);
        }

        return max.Y;
    }

    private float DrawWhenCard(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        if (editPanel != EditorPanel.None)
        {
            shownPanel = editPanel;
        }

        var reveal = panelReveal.Step(editPanel == EditorPanel.None ? 0f : 1f, Motion.Sheet, delta);
        if (editPanel == EditorPanel.None && reveal < PanelHideThreshold)
        {
            shownPanel = EditorPanel.None;
        }

        var rowHeight = CalendarArt.FieldRowHeight * scale;
        var panelHeight = PanelHeight(shownPanel, scale) * reveal;
        var max = new Vector2(origin.X + width, origin.Y + rowHeight * 3f + panelHeight);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var pad = Metrics.Space.Lg * scale;
        var cursorY = origin.Y;
        cursorY = DrawWhenRow(drawList, origin.X, cursorY, width, EditorPanel.Date, Loc.T(L.Calendar.EventDate),
            EditDateLabel(), panelHeight, reveal, scale);
        CalendarArt.Hairline(drawList, ui, origin.X + pad, max.X, cursorY);
        cursorY = DrawWhenRow(drawList, origin.X, cursorY, width, EditorPanel.Time, Loc.T(L.Calendar.EventTime),
            EditTimeLabel(), panelHeight, reveal, scale);
        CalendarArt.Hairline(drawList, ui, origin.X + pad, max.X, cursorY);
        DrawWhenRow(drawList, origin.X, cursorY, width, EditorPanel.Duration, Loc.T(L.Calendar.Duration),
            EditDurationLabel(), panelHeight, reveal, scale);
        return max.Y;
    }

    private float DrawWhenRow(ImDrawListPtr drawList, float left, float top, float width, EditorPanel panel,
        string label, string value, float panelHeight, float reveal, float scale)
    {
        var row = new Rect(new Vector2(left, top),
            new Vector2(left + width, top + CalendarArt.FieldRowHeight * scale));
        var active = editPanel == panel;
        if (DrawValueRow(drawList, row, label, value, active, false, scale))
        {
            UiFeedback.Play(UiSound.Tap);
            if (active)
            {
                editPanel = EditorPanel.None;
            }
            else
            {
                editPanel = panel;
                panelReveal.SnapTo(0f);
                pickerMonth = new DateTime(editDate.Year, editDate.Month, 1);
            }
        }

        if (shownPanel != panel || panelHeight <= 0.5f)
        {
            return row.Max.Y;
        }

        var panelRect = new Rect(new Vector2(left, row.Max.Y), new Vector2(left + width, row.Max.Y + panelHeight));
        drawList.PushClipRect(panelRect.Min, panelRect.Max, true);
        var alpha = Math.Clamp(reveal * 1.4f - 0.4f, 0f, 1f);
        switch (panel)
        {
            case EditorPanel.Date:
                DrawDatePanel(drawList, panelRect, alpha, scale);
                break;
            case EditorPanel.Time:
                DrawTimePanel(panelRect, alpha, scale);
                break;
            case EditorPanel.Duration:
                DrawDurationPanel(drawList, panelRect, alpha, scale);
                break;
        }

        drawList.PopClipRect();
        return panelRect.Max.Y;
    }

    private bool DrawValueRow(ImDrawListPtr drawList, Rect row, string label, string value, bool active, bool menu,
        float scale)
    {
        var hovered = CalendarArt.RowWash(drawList, ui, row, scale);
        var pad = Metrics.Space.Lg * scale;
        var labelHeight = Typography.LineHeight(TextStyles.Body);
        var labelWidth = Typography.Measure(label, TextStyles.Body).X;
        Typography.Draw(drawList, new Vector2(row.Min.X + pad, row.Center.Y - labelHeight * 0.5f), label, ui.TitleInk,
            TextStyles.Body);
        var glyphSpan = menu ? (MenuGlyphSize + MenuGlyphGap) * scale : 0f;
        var maxValue = MathF.Max(1f, row.Width - pad * 3f - labelWidth - ValuePillPadX * 2f * scale - glyphSpan);
        var fitted = Typography.FitText(value, maxValue, TextStyles.Body);
        var valueWidth = Typography.Measure(fitted, TextStyles.Body).X;
        var ink = active || menu ? ui.Accent : ui.TitleInk;
        var pillMax = new Vector2(row.Max.X - pad, row.Center.Y + ValuePillHeight * 0.5f * scale);
        var pillMin = new Vector2(pillMax.X - valueWidth - ValuePillPadX * 2f * scale - glyphSpan,
            row.Center.Y - ValuePillHeight * 0.5f * scale);
        if (!menu)
        {
            var fill = active ? Palette.WithAlpha(ui.Accent, ValueFillAlpha) : Surfaces.Fill(ui.TitleInk, FillLevel.Tertiary);
            Squircle.Fill(drawList, pillMin, pillMax, (pillMax.Y - pillMin.Y) * 0.5f, ImGui.GetColorU32(fill));
        }

        Typography.Draw(drawList, new Vector2(pillMin.X + ValuePillPadX * scale, row.Center.Y - labelHeight * 0.5f),
            fitted, ink, TextStyles.Body);
        if (menu)
        {
            ProgressRing.CenterIcon(drawList,
                new Vector2(pillMax.X - ValuePillPadX * scale - MenuGlyphSize * 0.5f * scale, row.Center.Y),
                FontAwesomeIcon.ChevronDown, ink, MenuGlyphSize * scale);
        }

        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    private float PanelHeight(EditorPanel panel, float scale) => panel switch
    {
        EditorPanel.Date => (PanelPad * 2f + PickerHeaderHeight + CalendarMonthView.WeekdayRowHeight) * scale +
                            CalendarMonthView.Height(pickerMonth, true, scale),
        EditorPanel.Time => (PanelPad * 2f + StepperHeight) * scale,
        EditorPanel.Duration => (PanelPad * 2f + StepperHeight) * scale,
        _ => 0f,
    };

    private void DrawDatePanel(ImDrawListPtr drawList, Rect panel, float alpha, float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var left = panel.Min.X + pad;
        var right = panel.Max.X - pad;
        var top = panel.Min.Y + PanelPad * scale;
        var headerCenterY = top + PickerHeaderHeight * scale * 0.5f;
        var title = PickerTitle();
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(left, headerCenterY - titleHeight * 0.5f), title,
            ui.TitleInk with { W = alpha }, TextStyles.Headline);
        var radius = PickerChevronRadius * scale;
        var nextCenter = new Vector2(right - radius, headerCenterY);
        var previousCenter = new Vector2(nextCenter.X - radius * 2f - Metrics.Space.Sm * scale, headerCenterY);
        var interactive = alpha > 0.9f;
        if (interactive && CalendarArt.RoundIcon(drawList, ui, "calendar.picker.previous", previousCenter, radius,
                FontAwesomeIcon.ChevronLeft, Loc.T(L.Calendar.PreviousMonth), true))
        {
            UiFeedback.Play(UiSound.Tap);
            pickerMonth = pickerMonth.AddMonths(-1);
        }

        if (interactive && CalendarArt.RoundIcon(drawList, ui, "calendar.picker.next", nextCenter, radius,
                FontAwesomeIcon.ChevronRight, Loc.T(L.Calendar.NextMonth), true))
        {
            UiFeedback.Play(UiSound.Tap);
            pickerMonth = pickerMonth.AddMonths(1);
        }

        var weekdaysTop = top + PickerHeaderHeight * scale;
        CalendarMonthView.DrawWeekdays(drawList, ui, new Vector2(left, weekdaysTop), right - left, scale);
        var gridTop = weekdaysTop + CalendarMonthView.WeekdayRowHeight * scale;
        if (CalendarMonthView.Draw(drawList, ui, new Vector2(left, gridTop), right - left, pickerMonth, editDate, null,
                true, interactive ? alpha : MathF.Min(alpha, 0.5f), out var tapped, scale) && tapped != editDate)
        {
            UiFeedback.Play(UiSound.Tap);
            editDate = tapped;
        }
    }

    private void DrawTimePanel(Rect panel, float alpha, float scale)
    {
        if (alpha < 0.9f)
        {
            return;
        }

        var pad = Metrics.Space.Lg * scale;
        var top = panel.Min.Y + PanelPad * scale;
        var rect = new Rect(new Vector2(panel.Min.X + pad, top),
            new Vector2(panel.Max.X - pad, top + StepperHeight * scale));
        var updated = TimeOfDayField.Draw(ui, rect, editMinuteOfDay, scale);
        if (updated == editMinuteOfDay)
        {
            return;
        }

        UiFeedback.Play(UiSound.Tap);
        editMinuteOfDay = updated;
    }

    private void DrawDurationPanel(ImDrawListPtr drawList, Rect panel, float alpha, float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var top = panel.Min.Y + PanelPad * scale;
        var centerY = top + StepperHeight * scale * 0.5f;
        var radius = StepperRadius * scale;
        var minusCenter = new Vector2(panel.Min.X + pad + radius, centerY);
        var plusCenter = new Vector2(panel.Max.X - pad - radius, centerY);
        var interactive = alpha > 0.9f;
        if (interactive && CalendarArt.RoundIcon(drawList, ui, "calendar.duration.minus", minusCenter, radius,
                FontAwesomeIcon.Minus, Loc.T(L.Calendar.Shorter), editDuration > 0,
                HoverLabelSide.Above))
        {
            UiFeedback.Play(UiSound.Tap);
            editDuration = Math.Max(0, editDuration - DurationStep);
        }

        if (interactive && CalendarArt.RoundIcon(drawList, ui, "calendar.duration.plus", plusCenter, radius,
                FontAwesomeIcon.Plus, Loc.T(L.Calendar.Longer), editDuration < MinutesPerDay,
                HoverLabelSide.Above))
        {
            UiFeedback.Play(UiSound.Tap);
            editDuration = Math.Min(MinutesPerDay, editDuration + DurationStep);
        }

        var label = EditDurationLabel();
        var available = plusCenter.X - minusCenter.X - radius * 2f - Metrics.Space.Md * scale;
        Typography.DrawCentered(drawList, new Vector2((minusCenter.X + plusCenter.X) * 0.5f, centerY),
            Typography.FitText(label, available, TextStyles.Title3), ui.TitleInk with { W = alpha }, TextStyles.Title3);
    }

    private float DrawOptionsCard(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var groups = configuration.CalendarGroups;
        var rows = groups.Count > 0 ? 3 : 2;
        var rowHeight = CalendarArt.FieldRowHeight * scale;
        var max = new Vector2(origin.X + width, origin.Y + rows * rowHeight);
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var pad = Metrics.Space.Lg * scale;
        var cursorY = origin.Y;
        var repeatRow = new Rect(new Vector2(origin.X, cursorY), new Vector2(max.X, cursorY + rowHeight));
        if (DrawValueRow(drawList, repeatRow, Loc.T(L.Calendar.Repeat), Loc.T(CalendarReminder.RepeatLabel(editRepeat)),
                false, true, scale))
        {
            OpenMenu(CalendarMenu.Repeat, repeatRow);
        }

        cursorY += rowHeight;
        if (groups.Count > 0)
        {
            CalendarArt.Hairline(drawList, ui, origin.X + pad, max.X, cursorY);
            var groupRow = new Rect(new Vector2(origin.X, cursorY), new Vector2(max.X, cursorY + rowHeight));
            if (DrawValueRow(drawList, groupRow, Loc.T(L.Calendar.Group), GroupChoiceLabel(groups), false, true, scale))
            {
                OpenMenu(CalendarMenu.Group, groupRow);
            }

            cursorY += rowHeight;
        }

        CalendarArt.Hairline(drawList, ui, origin.X + pad, max.X, cursorY);
        var alertRow = new Rect(new Vector2(origin.X, cursorY), new Vector2(max.X, cursorY + rowHeight));
        UiAnchors.Report("calendar.editor.alert", alertRow);
        if (DrawValueRow(drawList, alertRow, Loc.T(L.Calendar.Alert),
                Loc.T(CalendarReminder.LeadLabelAt(editLeadIndex)), false, true, scale))
        {
            OpenMenu(CalendarMenu.Alert, alertRow);
        }

        return max.Y;
    }

    private string GroupChoiceLabel(IReadOnlyList<CalendarEventGroup> groups)
    {
        if (editGroupIndex <= 0 || editGroupIndex > groups.Count)
        {
            return Loc.T(L.Calendar.NoGroup);
        }

        return groups[editGroupIndex - 1].Name;
    }

    private string EditDateLabel()
    {
        var key = editDate.Ticks;
        return editDateLabel.IsCurrent(key)
            ? editDateLabel.Value
            : editDateLabel.Store(key, editDate.ToString("ddd, MMM d, yyyy", Loc.Culture));
    }

    private string EditTimeLabel()
    {
        var key = (long)editMinuteOfDay;
        return editTimeLabel.IsCurrent(key)
            ? editTimeLabel.Value
            : editTimeLabel.Store(key, TimeText.Clock(DateTime.Today.AddMinutes(editMinuteOfDay)));
    }

    private string EditDurationLabel()
    {
        var key = (long)editDuration;
        if (editDurationLabel.IsCurrent(key))
        {
            return editDurationLabel.Value;
        }

        return editDurationLabel.Store(key, DurationText(editDuration));
    }

    private static string DurationText(int minutes)
    {
        if (minutes <= 0)
        {
            return Loc.T(L.Calendar.NoDuration);
        }

        var hours = minutes / MinutesPerHour;
        var rest = minutes % MinutesPerHour;
        if (hours == 0)
        {
            return Loc.T(L.Calendar.DurationMinutes, rest);
        }

        return rest == 0 ? Loc.T(L.Calendar.DurationHours, hours) : Loc.T(L.Calendar.DurationHoursMinutes, hours, rest);
    }

    private string PickerTitle()
    {
        var key = pickerMonth.Ticks;
        return pickerTitle.IsCurrent(key) ? pickerTitle.Value : pickerTitle.Store(key, MonthTitleText(pickerMonth));
    }

    private static string MonthTitleText(DateTime month) =>
        Loc.Culture.TextInfo.ToTitleCase(month.ToString(Loc.Culture.DateTimeFormat.YearMonthPattern, Loc.Culture));

    private void CommitEvent()
    {
        var title = editTitle.Trim();
        if (title.Length == 0)
        {
            return;
        }

        var when = editDate.Date.AddMinutes(editMinuteOfDay);
        var groups = configuration.CalendarGroups;
        var groupId = editGroupIndex > 0 && editGroupIndex <= groups.Count ? groups[editGroupIndex - 1].Id : Guid.Empty;
        var reminderMinutesBefore = CalendarReminder.LeadOptionsMinutes[editLeadIndex];
        var notes = editNotes.Trim();
        var now = DateTime.Now;
        CalendarCustomEvent? item;
        if (editIsNew)
        {
            item = new CalendarCustomEvent();
            configuration.CalendarCustomEvents.Add(item);
        }
        else
        {
            item = FindCustomEvent(editEventId);
            if (item is null)
            {
                router.Pop();
                return;
            }
        }

        item.Title = title;
        item.When = when;
        item.GroupId = groupId;
        item.ReminderMinutesBefore = reminderMinutesBefore;
        item.DurationMinutes = editDuration;
        item.Repeat = editRepeat;
        item.Notes = notes;
        CalendarReminder.Arm(item, now, editIsNew);
        SaveCalendar();
        UiFeedback.Play(UiSound.Success);
        if (editIsNew)
        {
            mode = CalendarMode.Month;
            selectedDate = when.Date;
            monthOffset = MonthOffsetOf(when);
            router.Pop();
            return;
        }

        if (item.Repeat == CalendarRepeat.None)
        {
            detailOccurrence = item.When;
        }
        else
        {
            var index = CalendarRecurrence.FirstIndexOnOrAfter(item.When, item.Repeat, detailOccurrence.Date);
            detailOccurrence = CalendarRecurrence.Occurrence(item.When, item.Repeat, Math.Max(0, index));
        }

        router.Pop();
    }
}
