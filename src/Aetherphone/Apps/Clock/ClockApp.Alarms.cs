using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Clock;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Clock;

internal sealed partial class ClockApp
{
    private const float AlarmRowHeight = 92f;
    private const float AlarmBadgeRadius = 11f;
    private const float AlarmBadgeSpan = 36f;
    private const float AlarmSubtitleGap = 2f;
    private const float AlarmChevronSize = 14f;
    private const float AlarmToggleGap = 12f;
    private const float AlarmsEmptyUnits = 420f;

    private static readonly TextStyle AlarmTimeStyle = TextStyles.WidgetDisplay;

    private int[] alarmOrder = new int[8];
    private string[] alarmSubtitles = new string[8];
    private string[] alarmToggleIds = new string[8];
    private int alarmLocalCount;
    private int alarmOrderCount = -1;
    private CultureInfo? alarmCulture;
    private int alarmFormat = -1;
    private bool alarmsDirty = true;
    private bool editingAlarms;
    private Guid pendingDeleteAlarm;

    private void DrawAlarms(in PhoneContext context)
    {
        var scale = UiScale.Current;
        var alarms = configuration.Alarms;
        if (alarms.Count == 0)
        {
            editingAlarms = false;
        }

        EnsureAlarmOrder();
        var navBar = AppHeader.BeginLargeTitle(context, false);
        using (UiScope.PushId("clock.alarms"))
        using (AppSurface.Begin(navBar.Body))
        {
            var width = ImGui.GetContentRegionAvail().X;
            if (alarms.Count == 0)
            {
                DrawAlarmsEmpty(width, scale);
            }
            else
            {
                DrawAlarmSections(width, scale);
            }

            ImGui.Dummy(new Vector2(0f, ClockArt.BottomPad * scale));
        }

        ApplyPendingAlarmDelete();
        var count = 0;
        if (alarms.Count > 0)
        {
            count = NavButton(count, editingAlarms ? PhoneIcons.Check : PhoneIcons.Edit,
                Loc.T(editingAlarms ? L.Clock.Done : L.Clock.Edit));
        }

        var addIndex = count;
        count = NavButton(count, PhoneIcons.Plus, Loc.T(L.Clock.NewAlarm));
        UiAnchors.Report("clock.add", AppHeader.LargeTitleButtonRect(in navBar, addIndex, count));
        var pressed = AppHeader.EndLargeTitle(in navBar, context, "clock.alarms.nav", Loc.T(L.Clock.TabAlarms),
            NavBarStyle.From(ui), navButtons.AsSpan(0, count));
        if (pressed == addIndex)
        {
            editingAlarms = false;
            StartNewAlarm();
        }
        else if (pressed == 0)
        {
            editingAlarms = !editingAlarms;
        }
    }

    private void DrawAlarmsEmpty(float width, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = MathF.Max(ImGui.GetContentRegionAvail().Y, AlarmsEmptyUnits * scale);
        var area = new Rect(origin, origin + new Vector2(width, height));
        if (EmptyState.Draw(area, ui, PhoneIcons.Bell, Loc.T(L.Clock.AlarmsEmptyTitle),
                Loc.T(L.Clock.AlarmsEmptyHint), Loc.T(L.Clock.NewAlarm)))
        {
            StartNewAlarm();
        }

        ClockArt.Advance(origin, width, height, 0f, scale);
    }

    private void DrawAlarmSections(float width, float scale)
    {
        var eorzeaCount = alarmOrderCount - alarmLocalCount;
        var both = alarmLocalCount > 0 && eorzeaCount > 0;
        if (alarmLocalCount > 0)
        {
            if (both)
            {
                CardSectionHeader.Flow(Loc.T(L.Clock.LocalTime), ui.TitleInk);
            }

            DrawAlarmCard(0, alarmLocalCount, width, scale);
        }

        if (eorzeaCount <= 0)
        {
            return;
        }

        if (alarmLocalCount > 0)
        {
            ImGui.Dummy(new Vector2(0f, ClockArt.SectionGap * scale - ImGui.GetStyle().ItemSpacing.Y));
        }

        CardSectionHeader.Flow(Loc.T(L.Clock.EorzeaTime), ui.TitleInk);
        DrawAlarmCard(alarmLocalCount, eorzeaCount, width, scale);
    }

    private void DrawAlarmCard(int first, int count, float width, float scale)
    {
        var rowHeight = AlarmRowHeight * scale;
        var origin = ImGui.GetCursorScreenPos();
        var max = new Vector2(origin.X + width, origin.Y + count * rowHeight);
        var drawList = ImGui.GetWindowDrawList();
        ui.Card(drawList, origin, max, Metrics.Radius.Grouped * scale);
        var alarms = configuration.Alarms;
        for (var slot = 0; slot < count; slot++)
        {
            var orderIndex = first + slot;
            var alarmIndex = alarmOrder[orderIndex];
            if (alarmIndex >= alarms.Count)
            {
                continue;
            }

            var row = RowRect(origin, width, slot * rowHeight, rowHeight);
            if (slot > 0)
            {
                FeedCell.Hairline(drawList, origin.X + Metrics.Space.Lg * scale, max.X, row.Min.Y, ui.Hairline);
            }

            DrawAlarmRow(drawList, row, alarms[alarmIndex], orderIndex, scale);
        }

        ClockArt.Advance(origin, width, max.Y - origin.Y, 0f, scale);
    }

    private void DrawAlarmRow(ImDrawListPtr drawList, Rect row, AlarmEntry alarm, int orderIndex, float scale)
    {
        var subtitle = alarmSubtitles[orderIndex];
        if (!ImGui.IsRectVisible(row.Min, row.Max))
        {
            return;
        }

        var inset = Metrics.Space.Lg * scale;
        var left = row.Min.X + inset;
        var right = row.Max.X - inset;
        var overChild = false;
        if (editingAlarms)
        {
            var badgeCenter = new Vector2(left + AlarmBadgeRadius * scale, row.Center.Y);
            overChild |= DrawAlarmRemoveBadge(drawList, badgeCenter, alarm, scale);
            left += AlarmBadgeSpan * scale;
        }

        var ink = alarm.Enabled ? ui.TitleInk : ui.MutedInk;
        var timeHeight = Typography.LineHeight(AlarmTimeStyle);
        var subtitleHeight = Typography.LineHeight(TextStyles.Subheadline);
        var top = row.Center.Y - (timeHeight + AlarmSubtitleGap * scale + subtitleHeight) * 0.5f;
        var suffix = alarm.Eorzea ? Loc.T(L.Clock.EorzeaShort) : null;
        ClockArt.DrawTime(drawList, new Vector2(left, top), alarm.Hour, alarm.Minute, alarm.Eorzea, AlarmTimeStyle,
            TextStyles.Title3, ink, alarm.Eorzea && alarm.Enabled ? ui.Accent : ink, suffix);

        float controlLeft;
        if (editingAlarms)
        {
            var chevron = AlarmChevronSize * scale;
            PhoneIcon.Draw(drawList, new Vector2(right - chevron * 0.5f, row.Center.Y), PhoneIcons.ChevronRight,
                ui.MutedInk, chevron);
            controlLeft = right - chevron;
        }
        else
        {
            var toggleSize = new Vector2(Metrics.Size.ToggleWidth, Metrics.Size.ToggleHeight) * scale;
            var toggleMin = new Vector2(right - toggleSize.X, row.Center.Y - toggleSize.Y * 0.5f);
            var toggleRect = new Rect(toggleMin, toggleMin + toggleSize);
            overChild |= UiInteract.Hover(toggleRect.Min, toggleRect.Max);
            var next = Toggle.Draw(alarmToggleIds[orderIndex], toggleRect, alarm.Enabled, theme);
            if (next != alarm.Enabled)
            {
                SetAlarmEnabled(alarm, next);
            }

            controlLeft = toggleMin.X;
        }

        var subtitleWidth = MathF.Max(1f, controlLeft - AlarmToggleGap * scale - left);
        Typography.Draw(drawList, new Vector2(left, top + timeHeight + AlarmSubtitleGap * scale),
            Typography.FitText(subtitle, subtitleWidth, TextStyles.Subheadline),
            alarm.Enabled ? ui.BodyInk : ui.MutedInk, TextStyles.Subheadline);

        var hovered = !overChild && UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            drawList.AddRectFilled(row.Min, row.Max, ImGui.GetColorU32(ui.HoverWash));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (!overChild && UiInteract.Click(row.Min, row.Max, hovered))
        {
            StartEditAlarm(alarm);
        }
    }

    private bool DrawAlarmRemoveBadge(ImDrawListPtr drawList, Vector2 center, AlarmEntry alarm, float scale)
    {
        var hit = new Vector2(Metrics.Size.TapTarget * 0.5f * scale);
        var hovered = UiInteract.Hover(center - hit, center + hit);
        var grow = PressFx.Scale(ImGui.GetID($"clock.alarm.remove.{alarm.Id}"),
            hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left), Motion.PressScaleControl);
        ClockArt.MinusBadge(drawList, center, AlarmBadgeRadius * scale * grow, theme.Danger);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        if (UiInteract.Click(center - hit, center + hit, hovered))
        {
            pendingDeleteAlarm = alarm.Id;
        }

        return hovered;
    }

    private void SetAlarmEnabled(AlarmEntry alarm, bool enabled)
    {
        alarm.Enabled = enabled;
        if (enabled)
        {
            var now = DateTime.Now;
            AlarmSchedule.Arm(alarm, now, now.ToUniversalTime());
            ShowRingsToast(alarm, now);
        }

        configuration.Save();
    }

    private static void ShowRingsToast(AlarmEntry alarm, DateTime now)
    {
        var next = AlarmSchedule.NextOccurrence(alarm, now);
        ShellToast.Show(Loc.T(L.Clock.AlarmSetToast, TimeText.Until(next - now)));
    }

    private void ApplyPendingAlarmDelete()
    {
        if (pendingDeleteAlarm == Guid.Empty)
        {
            return;
        }

        var id = pendingDeleteAlarm;
        pendingDeleteAlarm = Guid.Empty;
        AskDeleteAlarm(id, false);
    }

    private void AskDeleteAlarm(Guid id, bool popAfter)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Clock.DeleteAlarmConfirm),
            ConfirmLabel = Loc.T(L.Clock.Delete),
            CancelLabel = Loc.T(L.Clock.KeepIt),
            Sheet = true,
            Confirm = () => DeleteAlarm(id, popAfter),
        });
    }

    private void DeleteAlarm(Guid id, bool popAfter)
    {
        configuration.Alarms.RemoveAll(entry => entry.Id == id);
        configuration.Save();
        alarmsDirty = true;
        if (popAfter)
        {
            router.Pop();
        }
    }

    private void EnsureAlarmOrder()
    {
        var alarms = configuration.Alarms;
        if (!alarmsDirty && alarms.Count == alarmOrderCount && ReferenceEquals(alarmCulture, Loc.Culture) &&
            alarmFormat == TimeText.FormatVersion)
        {
            return;
        }

        alarmsDirty = false;
        alarmCulture = Loc.Culture;
        alarmFormat = TimeText.FormatVersion;
        alarmOrderCount = alarms.Count;
        if (alarmOrder.Length < alarms.Count)
        {
            var size = Math.Max(alarms.Count, alarmOrder.Length * 2);
            alarmOrder = new int[size];
            alarmSubtitles = new string[size];
            alarmToggleIds = new string[size];
        }

        alarmLocalCount = 0;
        for (var index = 0; index < alarms.Count; index++)
        {
            alarmOrder[index] = index;
            if (!alarms[index].Eorzea)
            {
                alarmLocalCount++;
            }
        }

        for (var index = 1; index < alarms.Count; index++)
        {
            var current = alarmOrder[index];
            var target = index - 1;
            while (target >= 0 && AlarmBefore(alarms[current], alarms[alarmOrder[target]]))
            {
                alarmOrder[target + 1] = alarmOrder[target];
                target--;
            }

            alarmOrder[target + 1] = current;
        }

        for (var index = 0; index < alarms.Count; index++)
        {
            var alarm = alarms[alarmOrder[index]];
            alarmSubtitles[index] = AlarmSubtitle(alarm);
            alarmToggleIds[index] = string.Concat("clock.alarm.", alarm.Id.ToString("N"));
        }
    }

    private static bool AlarmBefore(AlarmEntry left, AlarmEntry right)
    {
        if (left.Eorzea != right.Eorzea)
        {
            return !left.Eorzea;
        }

        return left.MinuteOfDay < right.MinuteOfDay;
    }

    private static string AlarmSubtitle(AlarmEntry alarm)
    {
        var name = alarm.Label.Length > 0 ? alarm.Label : Loc.T(L.Clock.Alarm);
        return alarm.Repeats ? string.Concat(name, ", ", AlarmSchedule.RepeatLabel(alarm)) : name;
    }
}
