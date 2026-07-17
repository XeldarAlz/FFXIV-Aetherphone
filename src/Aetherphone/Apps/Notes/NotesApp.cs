using Aetherphone.Core;
using Aetherphone.Core.Notes;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Notes;

internal sealed class NotesApp : IPhoneApp
{
    private enum NotesScreen : byte
    {
        List,
        EditNote,
        EditReminder,
    }

    private const float NoteRowHeight = 66f;
    private const float ReminderRowHeight = 56f;
    private const int NoteMaxLength = 8000;
    private const int ReminderMaxLength = 120;

    public string Id => "notes";
    public string DisplayName => Loc.T(L.Apps.Notes);
    public string Glyph => "N";
    public Vector4 Accent => AppAccents.For("notes");
    public int BadgeCount => 0;

    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private readonly AppSkin ui = new(AppPalettes.Notes(PhoneTheme.Default));
    private readonly ViewRouter<NotesScreen> router;
    private readonly RouterDraw<NotesScreen> drawView;
    private readonly Action back;
    private readonly string[] tabOptions = new string[2];
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;
    private int activeTab;

    private PhoneNote? editingNote;
    private string noteBuffer = string.Empty;
    private bool noteDirty;

    private Guid editingReminderId;
    private string reminderTitle = string.Empty;
    private bool reminderHasDue;
    private DateTime reminderDate;
    private int reminderHour;
    private int reminderMinute;

    public NotesApp(Configuration configuration, ConfirmService confirm)
    {
        this.configuration = configuration;
        this.confirm = confirm;
        router = new ViewRouter<NotesScreen>(NotesScreen.List, Id);
        drawView = DrawView;
        back = CloseEditor;
    }

    public void OnOpened()
    {
        router.Reset();
        editingNote = null;
    }

    public void OnClosed()
    {
        CommitNoteBuffer();
        router.Reset();
        editingNote = null;
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        ui.Theme = context.Theme;
        ui.Palette = AppPalettes.Notes(context.Theme);

        var scale = ImGuiHelpers.GlobalScale;
        var screen = SceneChrome.ScreenFrom(context.Content, context.Theme, scale);
        ui.Backdrop(screen);
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(NotesScreen screen, Rect area, int depth)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ui.Body(area);
        switch (screen)
        {
            case NotesScreen.EditNote:
                DrawNoteEditor(area, scale);
                return;
            case NotesScreen.EditReminder:
                DrawReminderEditor(area, scale);
                return;
            default:
                DrawList(area, scale);
                return;
        }
    }

    private void DrawList(Rect content, float scale)
    {
        if (GuideIntents.Consume("notes.tab.reminders"))
        {
            activeTab = 1;
        }

        DrawTopBar(content, scale);

        var segMargin = Metrics.Space.Lg * scale;
        var segTop = content.Min.Y + AppHeader.Height * scale + Metrics.Space.Sm * scale;
        var segRow = new Rect(new Vector2(content.Min.X + segMargin, segTop),
            new Vector2(content.Max.X - segMargin, segTop + 30f * scale));
        UiAnchors.Report("notes.tabs", segRow);
        UiAnchors.Report("notes.tab.reminders",
            new Rect(new Vector2(segRow.Center.X, segRow.Min.Y), segRow.Max));
        tabOptions[0] = Loc.T(L.Notes.TabNotes);
        tabOptions[1] = Loc.T(L.Notes.TabReminders);
        activeTab = SegmentStrip.Draw("notes.tabs", segRow, tabOptions, activeTab, theme);

        var body = new Rect(new Vector2(content.Min.X, segRow.Max.Y + 10f * scale), content.Max);
        using (AppSurface.Begin(body))
        {
            if (activeTab == 0)
            {
                DrawNotesList(body, scale);
            }
            else
            {
                DrawRemindersList(body, scale);
            }

            ImGui.Dummy(new Vector2(0f, 10f * scale));
        }
    }

    private void DrawTopBar(Rect content, float scale)
    {
        var centerY = content.Min.Y + AppHeader.Height * scale * 0.5f;
        Typography.DrawCentered(new Vector2(content.Center.X, centerY), DisplayName, ui.TitleInk, 1.15f,
            FontWeight.SemiBold);
        var radius = 15f * scale;
        var buttonCenter = new Vector2(content.Max.X - Metrics.Space.Lg * scale - radius, centerY);
        UiAnchors.Report("notes.new",
            new Rect(buttonCenter - new Vector2(radius, radius), buttonCenter + new Vector2(radius, radius)));
        var tooltip = activeTab == 0 ? Loc.T(L.Notes.NewNote) : Loc.T(L.Notes.NewReminder);
        if (ui.IconButton(buttonCenter, radius, FontAwesomeIcon.Plus.ToIconString(), ui.TitleInk,
                Palette.WithAlpha(ui.TitleInk, 0.12f), 0.6f, tooltip))
        {
            if (activeTab == 0)
            {
                StartNewNote();
            }
            else
            {
                StartNewReminder();
            }
        }
    }

    private void DrawNotesList(Rect body, float scale)
    {
        var notes = configuration.Notes;
        if (notes.Count == 0)
        {
            Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 70f * scale), Loc.T(L.Notes.NotesEmpty),
                ui.MutedInk, TextStyles.Subheadline);
            return;
        }

        var card = GroupCard.Begin(theme, notes.Count, NoteRowHeight);
        for (var index = 0; index < notes.Count; index++)
        {
            DrawNoteRow(card.NextRow(), notes[index], scale);
        }

        card.End();
    }

    private void DrawNoteRow(Rect row, PhoneNote note, float scale)
    {
        var title = note.Title();
        var hasTitle = title.Length > 0;
        var titleText = hasTitle ? title : Loc.T(L.Notes.Untitled);
        Typography.Draw(new Vector2(row.Min.X, row.Min.Y + 12f * scale), Ellipsize(titleText, row.Width, scale),
            hasTitle ? ui.TitleInk : ui.MutedInk, TextStyles.Headline);

        var preview = note.Preview();
        var meta = note.UpdatedAt.ToString("d", Loc.Culture);
        var secondLine = preview.Length > 0 ? $"{meta}  {preview}" : (hasTitle ? meta : Loc.T(L.Notes.NoAdditionalText));
        Typography.Draw(new Vector2(row.Min.X, row.Min.Y + 36f * scale), Ellipsize(secondLine, row.Width, scale),
            ui.MutedInk, TextStyles.Footnote);

        if (UiInteract.HoverClick(row.Min, row.Max))
        {
            StartEditNote(note);
        }
    }

    private void DrawRemindersList(Rect body, float scale)
    {
        var reminders = configuration.Reminders;
        if (reminders.Count == 0)
        {
            Typography.DrawCentered(new Vector2(body.Center.X, body.Min.Y + 70f * scale),
                Loc.T(L.Notes.RemindersEmpty), ui.MutedInk, TextStyles.Subheadline);
            return;
        }

        var card = GroupCard.Begin(theme, reminders.Count, ReminderRowHeight);
        for (var index = 0; index < reminders.Count; index++)
        {
            if (!reminders[index].Done)
            {
                DrawReminderRow(card.NextRow(), reminders[index], scale);
            }
        }

        for (var index = 0; index < reminders.Count; index++)
        {
            if (reminders[index].Done)
            {
                DrawReminderRow(card.NextRow(), reminders[index], scale);
            }
        }

        card.End();
    }

    private void DrawReminderRow(Rect row, ReminderItem reminder, float scale)
    {
        var circleCenter = new Vector2(row.Min.X + 11f * scale, row.Center.Y);
        if (DrawCheckCircle(circleCenter, 11f * scale, reminder.Done, scale))
        {
            reminder.Done = !reminder.Done;
            if (reminder.Done)
            {
                reminder.Notified = true;
            }

            configuration.Save();
        }

        var textLeft = circleCenter.X + 22f * scale;
        var textRect = new Rect(new Vector2(textLeft, row.Min.Y), row.Max);
        var titleInk = reminder.Done ? ui.MutedInk : ui.TitleInk;
        var hasDue = reminder.DueAt.HasValue;
        var titleY = hasDue ? row.Center.Y - 16f * scale : row.Center.Y - 9f * scale;
        var title = reminder.Title.Length > 0 ? reminder.Title : Loc.T(L.Notes.ReminderHint);
        Typography.Draw(new Vector2(textLeft, titleY), Ellipsize(title, textRect.Width, scale), titleInk,
            TextStyles.Body);
        if (hasDue)
        {
            var due = reminder.DueAt!.Value;
            var overdue = !reminder.Done && due < DateTime.Now;
            var dueColor = overdue ? theme.Danger : ui.MutedInk;
            Typography.Draw(new Vector2(textLeft, row.Center.Y + 4f * scale), DueLabel(due), dueColor,
                TextStyles.Footnote);
        }

        if (UiInteract.HoverClick(textRect.Min, textRect.Max))
        {
            StartEditReminder(reminder);
        }
    }

    private bool DrawCheckCircle(Vector2 center, float radius, bool done, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        if (done)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(ui.Accent), 24);
            var check = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f));
            var thickness = 2f * scale;
            drawList.AddLine(center + new Vector2(-radius * 0.42f, 0f), center + new Vector2(-radius * 0.08f, radius * 0.38f),
                check, thickness);
            drawList.AddLine(center + new Vector2(-radius * 0.08f, radius * 0.38f),
                center + new Vector2(radius * 0.46f, -radius * 0.36f), check, thickness);
        }
        else
        {
            drawList.AddCircle(center, radius, ImGui.GetColorU32(ui.MutedInk), 24, 1.6f * scale);
        }

        var hitMin = center - new Vector2(radius + 6f * scale, radius + 6f * scale);
        var hitMax = center + new Vector2(radius + 6f * scale, radius + 6f * scale);
        return UiInteract.HoverClick(hitMin, hitMax);
    }

    private void StartNewNote()
    {
        var note = new PhoneNote();
        configuration.Notes.Insert(0, note);
        StartEditNote(note);
    }

    private void StartEditNote(PhoneNote note)
    {
        editingNote = note;
        noteBuffer = note.Body;
        noteDirty = false;
        router.Push(NotesScreen.EditNote);
    }

    private void DrawNoteEditor(Rect content, float scale)
    {
        var context = new PhoneContext(content, theme, navigation);
        AppHeader.Draw(context, Loc.T(L.Notes.NoteTitle), back);
        if (editingNote is null)
        {
            return;
        }

        var radius = 15f * scale;
        var trashCenter = new Vector2(content.Max.X - Metrics.Space.Lg * scale - radius,
            content.Min.Y + AppHeader.Height * scale * 0.5f);
        if (ui.IconButton(trashCenter, radius, FontAwesomeIcon.TrashAlt.ToIconString(), theme.Danger,
                Palette.WithAlpha(theme.Danger, 0.14f), 0.55f, Loc.T(L.Notes.DeleteNote)))
        {
            AskDeleteNote(editingNote);
        }

        var margin = Metrics.Space.Lg * scale;
        var top = content.Min.Y + AppHeader.Height * scale + Metrics.Space.Sm * scale;
        var area = new Rect(new Vector2(content.Min.X + margin, top),
            new Vector2(content.Max.X - margin, content.Max.Y - margin));
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, area.Min, area.Max, Metrics.Radius.Md * scale, ImGui.GetColorU32(ui.FieldSurface));
        ImGui.SetCursorScreenPos(new Vector2(area.Min.X + Metrics.Space.Md * scale, area.Min.Y + Metrics.Space.Sm * scale));
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            var fieldSize = new Vector2(area.Width - Metrics.Space.Md * 2f * scale,
                area.Height - Metrics.Space.Sm * 2f * scale);
            if (ImGui.InputTextMultiline("##noteBody", ref noteBuffer, NoteMaxLength, fieldSize,
                    ImGuiInputTextFlags.None))
            {
                editingNote.Body = noteBuffer;
                editingNote.UpdatedAt = DateTime.Now;
                noteDirty = true;
            }
        }
    }

    private void StartNewReminder()
    {
        editingReminderId = Guid.Empty;
        reminderTitle = string.Empty;
        reminderHasDue = false;
        var now = DateTime.Now;
        reminderDate = now.Date;
        reminderHour = now.Hour;
        reminderMinute = now.Minute / 5 * 5;
        router.Push(NotesScreen.EditReminder);
    }

    private void StartEditReminder(ReminderItem reminder)
    {
        editingReminderId = reminder.Id;
        reminderTitle = reminder.Title;
        reminderHasDue = reminder.DueAt.HasValue;
        var due = reminder.DueAt ?? DateTime.Now;
        reminderDate = due.Date;
        reminderHour = due.Hour;
        reminderMinute = due.Minute;
        router.Push(NotesScreen.EditReminder);
    }

    private void DrawReminderEditor(Rect content, float scale)
    {
        var context = new PhoneContext(content, theme, navigation);
        var isExisting = editingReminderId != Guid.Empty;
        var headerTitle = isExisting ? Loc.T(L.Notes.EditReminder) : Loc.T(L.Notes.NewReminder);
        AppHeader.Draw(context, headerTitle, back);
        if (isExisting)
        {
            var radius = 15f * scale;
            var trashCenter = new Vector2(content.Max.X - Metrics.Space.Lg * scale - radius,
                content.Min.Y + AppHeader.Height * scale * 0.5f);
            if (ui.IconButton(trashCenter, radius, FontAwesomeIcon.TrashAlt.ToIconString(), theme.Danger,
                    Palette.WithAlpha(theme.Danger, 0.14f), 0.55f, Loc.T(L.Notes.DeleteReminder)))
            {
                AskDeleteReminder(editingReminderId);
            }
        }

        var margin = Metrics.Space.Lg * scale;
        var top = content.Min.Y + AppHeader.Height * scale + Metrics.Space.Lg * scale;
        var fieldHeight = Metrics.Size.Row * scale;
        var gap = Metrics.Space.Md * scale;
        var labelHeight = Typography.Measure("A", TextStyles.Footnote).Y;

        var titleRect = new Rect(new Vector2(content.Min.X + margin, top),
            new Vector2(content.Max.X - margin, top + fieldHeight));
        DrawTitleField(titleRect, scale);

        var toggleTop = titleRect.Max.Y + gap;
        var toggleRect = new Rect(new Vector2(content.Min.X + margin, toggleTop),
            new Vector2(content.Max.X - margin, toggleTop + fieldHeight));
        DrawRemindToggle(toggleRect, scale);

        var saveTop = content.Max.Y - margin - fieldHeight;
        if (reminderHasDue)
        {
            var dateLabelY = toggleRect.Max.Y + gap;
            Typography.Draw(new Vector2(content.Min.X + margin, dateLabelY), Loc.T(L.Notes.ReminderDate), ui.MutedInk,
                TextStyles.Footnote);
            var dateTop = dateLabelY + labelHeight + Metrics.Space.Xxs * scale;
            var dateRect = new Rect(new Vector2(content.Min.X + margin, dateTop),
                new Vector2(content.Max.X - margin, dateTop + fieldHeight));
            StepperField.Draw(ui, dateRect, reminderDate.ToString("dddd, MMM d", Loc.Culture), scale,
                () => reminderDate = reminderDate.AddDays(-1), () => reminderDate = reminderDate.AddDays(1));

            var timeLabelY = dateRect.Max.Y + gap;
            Typography.Draw(new Vector2(content.Min.X + margin, timeLabelY), Loc.T(L.Notes.ReminderTime), ui.MutedInk,
                TextStyles.Footnote);
            var timeTop = timeLabelY + labelHeight + Metrics.Space.Xxs * scale;
            var timeRow = new Rect(new Vector2(content.Min.X + margin, timeTop),
                new Vector2(content.Max.X - margin, timeTop + fieldHeight));
            var half = timeRow.Width * 0.5f - gap * 0.5f;
            var hourRect = new Rect(timeRow.Min, new Vector2(timeRow.Min.X + half, timeRow.Max.Y));
            var minuteRect = new Rect(new Vector2(timeRow.Max.X - half, timeRow.Min.Y), timeRow.Max);
            StepperField.Draw(ui, hourRect, reminderHour.ToString("D2"), scale,
                () => reminderHour = (reminderHour + 23) % 24, () => reminderHour = (reminderHour + 1) % 24);
            StepperField.Draw(ui, minuteRect, reminderMinute.ToString("D2"), scale,
                () => reminderMinute = (reminderMinute + 55) % 60, () => reminderMinute = (reminderMinute + 5) % 60);
        }

        var saveRect = new Rect(new Vector2(content.Min.X + margin, saveTop),
            new Vector2(content.Max.X - margin, saveTop + fieldHeight));
        var enabled = reminderTitle.Trim().Length > 0;
        if (DrawSaveButton(saveRect, Loc.T(L.Notes.Save), enabled) && enabled)
        {
            CommitReminder();
        }
    }

    private void DrawTitleField(Rect rect, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, rect.Min, rect.Max, Metrics.Radius.Md * scale);
        ImGui.SetCursorScreenPos(new Vector2(rect.Min.X + Metrics.Space.Md * scale,
            rect.Min.Y + rect.Height * 0.5f - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(rect.Width - Metrics.Space.Md * 2f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            ImGui.InputTextWithHint("##reminderTitle", Loc.T(L.Notes.AddReminderHint), ref reminderTitle,
                ReminderMaxLength, ImGuiInputTextFlags.None);
        }
    }

    private void DrawRemindToggle(Rect rect, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, rect.Min, rect.Max, Metrics.Radius.Md * scale);
        Typography.Draw(new Vector2(rect.Min.X + Metrics.Space.Md * scale, rect.Center.Y - 9f * scale),
            Loc.T(L.Notes.RemindMe), ui.TitleInk, TextStyles.Body);
        var width = Metrics.Size.ToggleWidth * scale;
        var height = Metrics.Size.ToggleHeight * scale;
        var min = new Vector2(rect.Max.X - Metrics.Space.Md * scale - width, rect.Center.Y - height * 0.5f);
        var toggleRect = new Rect(min, min + new Vector2(width, height));
        reminderHasDue = Toggle.Draw("notes.remind", toggleRect, reminderHasDue, theme);
    }

    private bool DrawSaveButton(Rect rect, string label, bool enabled)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = enabled && ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var fill = !enabled ? Palette.WithAlpha(ui.Accent, 0.35f) :
            hovered ? Palette.Mix(ui.Accent, new Vector4(0f, 0f, 0f, 1f), 0.12f) : ui.Accent;
        Squircle.Fill(drawList, rect.Min, rect.Max, rect.Height * 0.5f, ImGui.GetColorU32(fill));
        Typography.DrawCentered(drawList, rect.Center, label, new Vector4(1f, 1f, 1f, 1f), TextStyles.Headline.Scale,
            TextStyles.Headline.Weight);
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private void CommitReminder()
    {
        var title = reminderTitle.Trim();
        if (title.Length == 0)
        {
            return;
        }

        DateTime? due = reminderHasDue
            ? new DateTime(reminderDate.Year, reminderDate.Month, reminderDate.Day, reminderHour, reminderMinute, 0)
            : null;

        if (editingReminderId == Guid.Empty)
        {
            configuration.Reminders.Insert(0, new ReminderItem { Title = title, DueAt = due });
        }
        else
        {
            var reminder = configuration.Reminders.Find(entry => entry.Id == editingReminderId);
            if (reminder is not null)
            {
                var dueChanged = reminder.DueAt != due;
                reminder.Title = title;
                reminder.DueAt = due;
                if (dueChanged)
                {
                    reminder.Notified = due.HasValue && due.Value <= DateTime.Now && reminder.Done;
                }
            }
        }

        configuration.Save();
        router.Pop();
    }

    private void CloseEditor()
    {
        CommitNoteBuffer();
        router.Pop();
    }

    private void CommitNoteBuffer()
    {
        if (editingNote is null)
        {
            return;
        }

        if (noteBuffer.Trim().Length == 0)
        {
            configuration.Notes.Remove(editingNote);
            configuration.Save();
        }
        else if (noteDirty)
        {
            editingNote.Body = noteBuffer;
            configuration.Save();
        }

        editingNote = null;
        noteDirty = false;
    }

    private void AskDeleteNote(PhoneNote note)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Notes.DeleteNoteConfirm),
            ConfirmLabel = Loc.T(L.Notes.Delete),
            CancelLabel = Loc.T(L.Notes.KeepIt),
            Confirm = () => DeleteNote(note),
        });
    }

    private void DeleteNote(PhoneNote note)
    {
        configuration.Notes.Remove(note);
        configuration.Save();
        editingNote = null;
        noteDirty = false;
        router.Pop();
    }

    private void AskDeleteReminder(Guid id)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Notes.DeleteReminderConfirm),
            ConfirmLabel = Loc.T(L.Notes.Delete),
            CancelLabel = Loc.T(L.Notes.KeepIt),
            Confirm = () => DeleteReminder(id),
        });
    }

    private void DeleteReminder(Guid id)
    {
        configuration.Reminders.RemoveAll(entry => entry.Id == id);
        configuration.Save();
        router.Pop();
    }

    private string DueLabel(DateTime due)
    {
        var dayLabel = RelativeDay(due.Date);
        return $"{dayLabel}  {due.ToString("t", Loc.Culture)}";
    }

    private static string RelativeDay(DateTime day)
    {
        var today = DateTime.Today;
        if (day == today)
        {
            return Loc.T(L.Clock.DayToday);
        }

        if (day == today.AddDays(1))
        {
            return Loc.T(L.Clock.DayTomorrow);
        }

        if (day == today.AddDays(-1))
        {
            return Loc.T(L.Clock.DayYesterday);
        }

        return day.ToString("ddd, MMM d", Loc.Culture);
    }

    private static string Ellipsize(string text, float maxWidth, float scale)
    {
        if (Typography.Measure(text, TextStyles.Body).X <= maxWidth)
        {
            return text;
        }

        var ellipsis = "…";
        for (var length = text.Length - 1; length > 0; length--)
        {
            var candidate = text.Substring(0, length).TrimEnd() + ellipsis;
            if (Typography.Measure(candidate, TextStyles.Body).X <= maxWidth)
            {
                return candidate;
            }
        }

        return ellipsis;
    }

    public void Dispose()
    {
    }
}
