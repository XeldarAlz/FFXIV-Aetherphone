using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notes;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Notes;

internal sealed partial class NotesApp
{
    private const int ReminderMaxLength = 120;
    private const int QuickDateCount = 4;
    private const int DaysPerWeek = 7;
    private const float TitleCardHeight = 58f;
    private const float DateHeaderHeight = 62f;
    private const float DateTileSize = 30f;
    private const float DateTileGap = 12f;
    private const float CardGap = 18f;
    private const float RevealPad = 12f;
    private const float QuickRailGap = 10f;
    private const float MonthHeaderHeight = 40f;
    private const float WeekdayRowHeight = 24f;
    private const float DayCellHeight = 38f;
    private const float DayCircleRadius = 16f;
    private const float MonthArrowRadius = 16f;
    private const float TimeRowHeight = 56f;
    private const float TimeFieldHeight = 44f;
    private const float TimeFieldWidthFraction = 0.62f;
    private const float DeleteRowHeight = 50f;
    private const float PastDayAlpha = 0.45f;
    private const float RevealEpsilon = 0.5f;

    private static readonly string[] DayNumbers = BuildDayNumbers();

    private readonly NavBarButton[] reminderButtons = new NavBarButton[1];
    private readonly ChipRail quickRail = new();
    private readonly string[] quickLabels = new string[QuickDateCount];
    private readonly bool[] quickActive = new bool[QuickDateCount];
    private readonly Action closeReminderEditor;
    private Guid editingReminderId;
    private string reminderTitle = string.Empty;
    private bool reminderHasDue;
    private DateTime reminderDate;
    private int reminderMinute;
    private DateTime calendarMonth;
    private Spring dateReveal;
    private bool snapReveal;
    private bool focusReminderTitle;
    private long monthTitleKey = long.MinValue;
    private string monthTitle = string.Empty;
    private long summaryKey = long.MinValue;
    private string summaryText = string.Empty;

    private static string[] BuildDayNumbers()
    {
        var numbers = new string[32];
        for (var day = 0; day < numbers.Length; day++)
        {
            numbers[day] = day.ToString(CultureInfo.InvariantCulture);
        }

        return numbers;
    }

    private void StartNewReminder()
    {
        var now = DateTime.Now;
        editingReminderId = Guid.Empty;
        reminderTitle = string.Empty;
        reminderHasDue = false;
        reminderDate = now.Date;
        reminderMinute = ReminderBoard.DefaultMinuteOfDay(now);
        calendarMonth = new DateTime(now.Year, now.Month, 1);
        snapReveal = true;
        focusReminderTitle = true;
        router.Push(NotesScreen.EditReminder);
    }

    private void StartEditReminder(ReminderItem reminder)
    {
        var now = DateTime.Now;
        editingReminderId = reminder.Id;
        reminderTitle = reminder.Title;
        reminderHasDue = reminder.DueAt.HasValue;
        var due = reminder.DueAt ?? now;
        reminderDate = due.Date;
        reminderMinute = reminder.DueAt.HasValue ? due.Hour * 60 + due.Minute : ReminderBoard.DefaultMinuteOfDay(now);
        calendarMonth = new DateTime(due.Year, due.Month, 1);
        snapReveal = true;
        focusReminderTitle = false;
        router.Push(NotesScreen.EditReminder);
    }

    private void DrawReminderEditor(Rect area)
    {
        var scale = UiScale.Current;
        var context = new PhoneContext(area, theme, navigation);
        var navBar = AppHeader.BeginLargeTitle(context);
        var isExisting = editingReminderId != Guid.Empty;
        using (AppSurface.Begin(navBar.Body))
        {
            var width = ScrollLayout.StableContentWidth();
            DrawReminderTitleCard(width, scale);
            ImGui.Dummy(new Vector2(0f, CardGap * scale - ImGui.GetStyle().ItemSpacing.Y));
            DrawDateCard(width, scale);
            if (isExisting)
            {
                ImGui.Dummy(new Vector2(0f, CardGap * scale - ImGui.GetStyle().ItemSpacing.Y));
                DrawDeleteReminderRow(scale);
            }

            ImGui.Dummy(new Vector2(0f, BottomBreathing * scale));
        }

        var canSave = !string.IsNullOrWhiteSpace(reminderTitle);
        reminderButtons[0] = new NavBarButton(IconGlyph.Of(FontAwesomeIcon.Check), Loc.T(L.Notes.Done));
        var title = isExisting ? Loc.T(L.Notes.Details) : Loc.T(L.Notes.NewReminder);
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "notes.reminder.nav", title, NavBarStyle.From(ui),
            canSave ? reminderButtons : ReadOnlySpan<NavBarButton>.Empty, Loc.T(L.Notes.TabReminders),
            closeReminderEditor);
        if (pressed == 0)
        {
            CommitReminder();
            router.Pop();
        }
    }

    private void DrawReminderTitleCard(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = TitleCardHeight * scale;
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, origin, origin + new Vector2(width, height), Metrics.Radius.Grouped * scale);
        var pad = Metrics.Space.Lg * scale;
        var radius = CheckRadius * scale;
        var checkCenter = new Vector2(origin.X + pad + radius, origin.Y + height * 0.5f);
        drawList.AddCircle(checkCenter, radius, ImGui.GetColorU32(ui.MutedInk), 32, CheckStroke * scale);

        var fieldLeft = checkCenter.X + radius + CheckTextGap * scale - ImGui.GetStyle().FramePadding.X;
        var fieldWidth = origin.X + width - pad - fieldLeft;
        using (UiScope.PushColor(ImGuiCol.FrameBg, AppSkin.Transparent))
        using (UiScope.PushColor(ImGuiCol.Text, ui.TitleInk))
        using (UiScope.PushColor(ImGuiCol.TextDisabled, ui.MutedInk))
        using (UiScope.PushColor(ImGuiCol.TextSelectedBg, Palette.WithAlpha(ui.Accent, SelectionAlpha)))
        using (Plugin.Fonts.Push(TextStyles.Body.Scale, TextStyles.Body.Weight))
        {
            ImGui.SetCursorScreenPos(new Vector2(fieldLeft, origin.Y + (height - ImGui.GetFrameHeight()) * 0.5f));
            ImGui.SetNextItemWidth(MathF.Max(1f, fieldWidth));
            if (focusReminderTitle && !router.IsTransitioning)
            {
                ImGui.SetKeyboardFocusHere();
                focusReminderTitle = false;
            }

            ImGui.InputTextWithHint("##reminderTitle", Loc.T(L.Notes.AddReminderHint), ref reminderTitle,
                ReminderMaxLength);
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private void DrawDateCard(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var headerHeight = DateHeaderHeight * scale;
        var expanded = ExpandedDateHeight(scale);
        if (snapReveal)
        {
            snapReveal = false;
            dateReveal.SnapTo(reminderHasDue ? expanded : 0f);
        }

        var reveal = dateReveal.Step(reminderHasDue ? expanded : 0f, Motion.Sheet,
            MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds));
        if (reveal < RevealEpsilon)
        {
            reveal = 0f;
        }

        var height = headerHeight + reveal;
        var drawList = ImGui.GetWindowDrawList();
        var cardMax = origin + new Vector2(width, height);
        ui.Card(drawList, origin, cardMax, Metrics.Radius.Grouped * scale);
        DrawDateHeader(drawList, new Rect(origin, origin + new Vector2(width, headerHeight)), scale);
        if (reveal > 0f)
        {
            var revealTop = origin.Y + headerHeight;
            var clipMin = new Vector2(origin.X, revealTop);
            ImGui.PushClipRect(clipMin, cardMax, true);
            drawList.AddLine(new Vector2(origin.X + Metrics.Space.Lg * scale, revealTop),
                new Vector2(cardMax.X, revealTop), ImGui.GetColorU32(ui.Hairline), Metrics.Stroke.Hairline);
            DrawDatePicker(drawList, new Vector2(origin.X, revealTop), width, scale);
            ImGui.PopClipRect();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    private float ExpandedDateHeight(float scale) =>
        (RevealPad * 2f + ChipRail.RowHeight + QuickRailGap + MonthHeaderHeight + WeekdayRowHeight +
         CalendarRows() * DayCellHeight + TimeRowHeight) * scale;

    private int CalendarRows()
    {
        var offset = MonthOffset(calendarMonth);
        var days = DateTime.DaysInMonth(calendarMonth.Year, calendarMonth.Month);
        return (offset + days + DaysPerWeek - 1) / DaysPerWeek;
    }

    private static int MonthOffset(DateTime monthStart)
    {
        var firstDay = (int)Loc.Culture.DateTimeFormat.FirstDayOfWeek;
        return ((int)monthStart.DayOfWeek - firstDay + DaysPerWeek) % DaysPerWeek;
    }

    private void DrawDateHeader(ImDrawListPtr drawList, Rect rect, float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var tile = DateTileSize * scale;
        var tileMin = new Vector2(rect.Min.X + pad, rect.Center.Y - tile * 0.5f);
        IconTile.FillShaded(drawList, tileMin, tileMin + new Vector2(tile, tile), tile * Metrics.Radius.TileFactor,
            theme.Danger);
        PhoneIcon.Draw(drawList, tileMin + new Vector2(tile, tile) * 0.5f, PhoneIcons.CalendarFilled, White,
            tile * TileGlyphFraction);

        var toggleWidth = Metrics.Size.ToggleWidth * scale;
        var toggleHeight = Metrics.Size.ToggleHeight * scale;
        var toggleMin = new Vector2(rect.Max.X - pad - toggleWidth, rect.Center.Y - toggleHeight * 0.5f);
        var toggleRect = new Rect(toggleMin, toggleMin + new Vector2(toggleWidth, toggleHeight));
        UiAnchors.Report("notes.reminder.remind", toggleRect);

        var labelLeft = tileMin.X + tile + DateTileGap * scale;
        var labelWidth = MathF.Max(1f, toggleMin.X - Metrics.Space.Sm * scale - labelLeft);
        var label = Typography.FitText(Loc.T(L.Notes.ReminderDate), labelWidth, TextStyles.Body);
        var bodyHeight = Typography.LineHeight(TextStyles.Body);
        if (reminderHasDue)
        {
            var footHeight = Typography.LineHeight(TextStyles.Footnote);
            var top = rect.Center.Y - (bodyHeight + footHeight) * 0.5f;
            Typography.Draw(drawList, new Vector2(labelLeft, top), label, ui.TitleInk, TextStyles.Body);
            var summary = Typography.FitText(DateSummary(), labelWidth, TextStyles.Footnote);
            Typography.Draw(drawList, new Vector2(labelLeft, top + bodyHeight), summary, ui.Accent,
                TextStyles.Footnote);
        }
        else
        {
            Typography.Draw(drawList, new Vector2(labelLeft, rect.Center.Y - bodyHeight * 0.5f), label, ui.TitleInk,
                TextStyles.Body);
        }

        var toggled = Toggle.Draw("notes.remind", toggleRect, reminderHasDue, theme);
        if (toggled == reminderHasDue)
        {
            return;
        }

        reminderHasDue = toggled;
        if (toggled)
        {
            calendarMonth = new DateTime(reminderDate.Year, reminderDate.Month, 1);
        }
    }

    private string DateSummary()
    {
        var due = reminderDate.AddMinutes(reminderMinute);
        var key = due.Ticks ^ DateTime.Today.Ticks ^ (long)TimeText.FormatVersion << 56 ^ Loc.Culture.GetHashCode();
        if (key != summaryKey)
        {
            summaryKey = key;
            summaryText = DueLabel(due);
        }

        return summaryText;
    }

    private void DrawDatePicker(ImDrawListPtr drawList, Vector2 origin, float width, float scale)
    {
        var pad = Metrics.Space.Lg * scale;
        var top = origin.Y + RevealPad * scale;
        var rail = new Rect(new Vector2(origin.X, top), new Vector2(origin.X + width, top + ChipRail.RowHeight * scale));
        DrawQuickDates(rail);
        top = rail.Max.Y + QuickRailGap * scale;

        var inner = new Rect(new Vector2(origin.X + pad, top), new Vector2(origin.X + width - pad, top));
        top = DrawMonthHeader(drawList, inner.Min.X, inner.Max.X, top, scale);
        top = DrawWeekdays(drawList, inner.Min.X, inner.Max.X, top, scale);
        top = DrawMonthGrid(drawList, inner.Min.X, inner.Max.X, top, scale);
        DrawTimeRow(drawList, inner.Min.X, inner.Max.X, top, scale);
    }

    private void DrawQuickDates(Rect rail)
    {
        quickLabels[0] = Loc.T(L.Notes.Today);
        quickLabels[1] = Loc.T(L.Notes.Tomorrow);
        quickLabels[2] = Loc.T(L.Notes.ThisWeekend);
        quickLabels[3] = Loc.T(L.Notes.NextWeek);
        var today = DateTime.Today;
        for (var index = 0; index < QuickDateCount; index++)
        {
            quickActive[index] = reminderDate == ReminderBoard.QuickDate((ReminderQuickDate)index, today);
        }

        var tapped = quickRail.Draw(rail, ui, quickLabels, quickActive);
        if (tapped < 0)
        {
            return;
        }

        reminderDate = ReminderBoard.QuickDate((ReminderQuickDate)tapped, today);
        calendarMonth = new DateTime(reminderDate.Year, reminderDate.Month, 1);
    }

    private float DrawMonthHeader(ImDrawListPtr drawList, float left, float right, float top, float scale)
    {
        var height = MonthHeaderHeight * scale;
        var centerY = top + height * 0.5f;
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        var arrowRadius = MonthArrowRadius * scale;
        var nextCenter = new Vector2(right - arrowRadius, centerY);
        var previousCenter = new Vector2(nextCenter.X - arrowRadius * 2f - Metrics.Space.Sm * scale, centerY);
        var title = Typography.FitText(MonthTitle(), previousCenter.X - arrowRadius - left, TextStyles.Headline);
        Typography.Draw(drawList, new Vector2(left, centerY - titleHeight * 0.5f), title, ui.TitleInk,
            TextStyles.Headline);
        if (MonthArrow(drawList, previousCenter, arrowRadius, FontAwesomeIcon.ChevronLeft, Loc.T(L.Common.Previous)))
        {
            calendarMonth = calendarMonth.AddMonths(-1);
        }

        if (MonthArrow(drawList, nextCenter, arrowRadius, FontAwesomeIcon.ChevronRight, Loc.T(L.Common.Next)))
        {
            calendarMonth = calendarMonth.AddMonths(1);
        }

        return top + height;
    }

    private bool MonthArrow(ImDrawListPtr drawList, Vector2 center, float radius, FontAwesomeIcon icon, string tip)
    {
        var clicked = RoundButton.Draw(drawList, ImGui.GetID(tip), center, radius, ui.Ink, ButtonStyle.Plain, true,
            false, out var face);
        AppSkin.Icon(drawList, center, IconGlyph.Of(icon), ui.Accent, 0.6f * face.Face.Width / (radius * 2f));
        HoverTooltip.Show(new Rect(center - new Vector2(radius, radius), center + new Vector2(radius, radius)), tip);
        return clicked;
    }

    private string MonthTitle()
    {
        var key = calendarMonth.Ticks ^ Loc.Culture.GetHashCode();
        if (key != monthTitleKey)
        {
            monthTitleKey = key;
            monthTitle = calendarMonth.ToString("Y", Loc.Culture);
        }

        return monthTitle;
    }

    private float DrawWeekdays(ImDrawListPtr drawList, float left, float right, float top, float scale)
    {
        var height = WeekdayRowHeight * scale;
        var cell = (right - left) / DaysPerWeek;
        var format = Loc.Culture.DateTimeFormat;
        var firstDay = (int)format.FirstDayOfWeek;
        for (var column = 0; column < DaysPerWeek; column++)
        {
            var day = (DayOfWeek)((firstDay + column) % DaysPerWeek);
            Typography.DrawCentered(drawList, new Vector2(left + cell * (column + 0.5f), top + height * 0.5f),
                format.GetShortestDayName(day), ui.MutedInk, TextStyles.FootnoteEmphasized);
        }

        return top + height;
    }

    private float DrawMonthGrid(ImDrawListPtr drawList, float left, float right, float top, float scale)
    {
        var cell = (right - left) / DaysPerWeek;
        var rowHeight = DayCellHeight * scale;
        var offset = MonthOffset(calendarMonth);
        var days = DateTime.DaysInMonth(calendarMonth.Year, calendarMonth.Month);
        var today = DateTime.Today;
        var circle = MathF.Min(DayCircleRadius * scale, cell * 0.5f);
        for (var day = 1; day <= days; day++)
        {
            var slot = offset + day - 1;
            var center = new Vector2(left + cell * (slot % DaysPerWeek + 0.5f),
                top + rowHeight * (slot / DaysPerWeek + 0.5f));
            var date = new DateTime(calendarMonth.Year, calendarMonth.Month, day);
            var selected = date == reminderDate;
            var isToday = date == today;
            var hit = new Vector2(circle, circle);
            var hovered = UiInteract.Hover(center - hit, center + hit);
            if (selected)
            {
                drawList.AddCircleFilled(center, circle, ImGui.GetColorU32(ui.Accent), 32);
            }
            else if (hovered)
            {
                drawList.AddCircleFilled(center, circle, ImGui.GetColorU32(ui.HoverTint), 32);
            }

            var ink = selected ? White : isToday ? ui.Accent : ui.TitleInk;
            if (!selected && date < today)
            {
                ink = Palette.WithAlpha(ink, ink.W * PastDayAlpha);
            }

            Typography.DrawCentered(drawList, center, DayNumbers[day], ink,
                isToday || selected ? TextStyles.Headline : TextStyles.Body);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(center - hit, center + hit, hovered))
            {
                reminderDate = date;
            }
        }

        return top + CalendarRows() * rowHeight;
    }

    private void DrawTimeRow(ImDrawListPtr drawList, float left, float right, float top, float scale)
    {
        var height = TimeRowHeight * scale;
        var centerY = top + height * 0.5f;
        drawList.AddLine(new Vector2(left, top), new Vector2(right + Metrics.Space.Lg * scale, top),
            ImGui.GetColorU32(ui.Hairline), Metrics.Stroke.Hairline);
        var tile = DateTileSize * scale;
        var tileMin = new Vector2(left, centerY - tile * 0.5f);
        IconTile.FillShaded(drawList, tileMin, tileMin + new Vector2(tile, tile), tile * Metrics.Radius.TileFactor,
            ui.Accent);
        PhoneIcon.Draw(drawList, tileMin + new Vector2(tile, tile) * 0.5f, PhoneIcons.Clock, White,
            tile * TileGlyphFraction);

        var fieldWidth = (right - left) * TimeFieldWidthFraction;
        var fieldHeight = TimeFieldHeight * scale;
        var field = new Rect(new Vector2(right - fieldWidth, centerY - fieldHeight * 0.5f),
            new Vector2(right, centerY + fieldHeight * 0.5f));
        var labelLeft = tileMin.X + tile + DateTileGap * scale;
        var label = Typography.FitText(Loc.T(L.Notes.ReminderTime), MathF.Max(1f, field.Min.X - labelLeft - 4f * scale),
            TextStyles.Body);
        var bodyHeight = Typography.LineHeight(TextStyles.Body);
        Typography.Draw(drawList, new Vector2(labelLeft, centerY - bodyHeight * 0.5f), label, ui.TitleInk,
            TextStyles.Body);
        reminderMinute = TimeOfDayField.Draw(ui, field, reminderMinute, scale);
    }

    private void DrawDeleteReminderRow(float scale)
    {
        var card = GroupCard.Begin(ui, 1, DeleteRowHeight);
        var bounds = card.Bounds;
        card.NextRow();
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(bounds.Min, bounds.Max);
        if (hovered)
        {
            var down = ImGui.IsMouseDown(ImGuiMouseButton.Left);
            DrawRowWash(drawList, bounds.Min, bounds.Max, true, true, down ? RowPressAlpha : RowWashAlpha, scale);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var label = Typography.FitText(Loc.T(L.Notes.DeleteReminder), bounds.Width - Metrics.Space.Lg * 2f * scale,
            TextStyles.Body);
        Typography.DrawCentered(drawList, bounds.Center, label, theme.Danger, TextStyles.Body);
        card.End();
        if (UiInteract.Click(bounds.Min, bounds.Max, hovered))
        {
            AskDeleteReminder(editingReminderId);
        }
    }

    private void CloseReminderEditor()
    {
        CommitReminder();
        router.Pop();
    }

    private void CommitReminder()
    {
        var title = reminderTitle.Trim();
        if (title.Length == 0)
        {
            return;
        }

        DateTime? due = reminderHasDue ? reminderDate.AddMinutes(reminderMinute) : null;
        if (editingReminderId == Guid.Empty)
        {
            configuration.Reminders.Insert(0, new ReminderItem
            {
                Title = title,
                DueAt = due,
                Notified = due is { } newDue && newDue <= DateTime.Now,
            });
            editingReminderId = configuration.Reminders[0].Id;
            configuration.Save();
            return;
        }

        var reminder = FindReminder(editingReminderId);
        if (reminder is null)
        {
            return;
        }

        var dueChanged = reminder.DueAt != due;
        var titleChanged = !string.Equals(reminder.Title, title, StringComparison.Ordinal);
        if (!dueChanged && !titleChanged)
        {
            return;
        }

        reminder.Title = title;
        reminder.DueAt = due;
        if (dueChanged)
        {
            reminder.Notified = reminder.Done || due is not { } dueAt || dueAt <= DateTime.Now;
        }

        configuration.Save();
    }

    private ReminderItem? FindReminder(Guid id)
    {
        var reminders = configuration.Reminders;
        for (var index = 0; index < reminders.Count; index++)
        {
            if (reminders[index].Id == id)
            {
                return reminders[index];
            }
        }

        return null;
    }

    private void AskDeleteReminder(Guid id)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Notes.DeleteReminderConfirm),
            ConfirmLabel = Loc.T(L.Notes.Delete),
            CancelLabel = Loc.T(L.Notes.KeepIt),
            Sheet = true,
            Confirm = () => DeleteReminder(id),
        });
    }

    private void DeleteReminder(Guid id)
    {
        var reminder = FindReminder(id);
        if (reminder is not null)
        {
            configuration.Reminders.Remove(reminder);
            reminderTexts.Remove(id);
            configuration.Save();
        }

        if (router.Current == NotesScreen.EditReminder)
        {
            router.Pop();
        }
    }
}
