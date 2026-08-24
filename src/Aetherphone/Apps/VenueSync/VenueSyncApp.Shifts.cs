using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.VenueSync;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.VenueSync;

internal readonly record struct ShiftListEntry(VenueSyncShift Shift, string? RoleName);

internal readonly record struct ShiftDayGroup(DateTime Date, List<ShiftListEntry> Entries);

internal sealed partial class VenueSyncApp
{
    private string? shiftsActionError;
    private string? shiftsActionNotice;
    private volatile bool shiftsActionBusy;

    private DateTime shiftsGroupedForRefreshUtc = DateTime.MinValue;
    private VenueSyncShift? cachedActiveShift;
    private List<ShiftDayGroup> cachedOpenDayGroups = new();
    private List<ShiftDayGroup> cachedUpcomingDayGroups = new();

    private void DrawShifts(Rect area)
    {
        var scale = UiScale.Current;
        AppHeader.Draw(new PhoneContext(area, theme, navigation), Loc.T(L.VenueSync.Shifts), back);

        var body = new Rect(new Vector2(area.Min.X, area.Min.Y + AppHeader.Height * scale), area.Max);
        using (AppSurface.Begin(body))
        {
            if (shiftsActionError is not null)
            {
                var errorOrigin = ImGui.GetCursorScreenPos();
                var errorWidth = ImGui.GetContentRegionAvail().X;
                Marquee.DrawLeftAuto("venuesync.shifts.error", shiftsActionError, errorOrigin.X, errorOrigin.Y,
                    errorWidth, TextStyles.Caption1, theme.Danger);
                ImGui.Dummy(new Vector2(0f, 20f * scale + Metrics.Space.Sm * scale));
            }
            else if (shiftsActionNotice is not null)
            {
                var noticeOrigin = ImGui.GetCursorScreenPos();
                var noticeWidth = ImGui.GetContentRegionAvail().X;
                Marquee.DrawLeftAuto("venuesync.shifts.notice", shiftsActionNotice, noticeOrigin.X, noticeOrigin.Y,
                    noticeWidth, TextStyles.Caption1, theme.ToggleOn);
                ImGui.Dummy(new Vector2(0f, 20f * scale + Metrics.Space.Sm * scale));
            }

            var shiftsSnapshot = state.Shifts;
            if (shiftsSnapshot is null)
            {
                var emptyOrigin = ImGui.GetCursorScreenPos();
                var emptyWidth = ImGui.GetContentRegionAvail().X;
                Marquee.DrawCenteredAuto("venuesync.shifts.empty", Loc.T(L.VenueSync.NoShiftData),
                    emptyOrigin.X + emptyWidth * 0.5f, emptyOrigin.Y, emptyWidth, TextStyles.Body, ui.MutedInk);
                return;
            }

            RegroupShiftsIfStale(shiftsSnapshot);

            var activeShift = cachedActiveShift;
            var openDayGroups = cachedOpenDayGroups;
            var upcomingDayGroups = cachedUpcomingDayGroups;
            if (activeShift is null && openDayGroups.Count == 0 && upcomingDayGroups.Count == 0)
            {
                EmptyState.Draw(body, ui, FontAwesomeIcon.Clock, Loc.T(L.VenueSync.NoShiftsTitle),
                    Loc.T(L.VenueSync.NoShiftsSubtitle));
                return;
            }

            if (activeShift is not null)
            {
                var activeCard = GroupCard.Begin(theme, 1, ShiftRow.Height, showSeparators: false,
                    fillOverride: ui.Palette.CardFill);
                var activeRow = activeCard.NextRow();
                var action = ShiftRow.Draw(activeCard, activeRow, activeShift, isOpen: false, roleName: null, theme,
                    ui.Accent, "active", !shiftsActionBusy);
                HandleShiftAction(action, activeShift.Id);
                activeCard.End();
                ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            }

            if (openDayGroups.Count > 0)
            {
                ui.SectionHeading(Loc.T(L.VenueSync.OpenToClaim), 14f);
                for (var groupIndex = 0; groupIndex < openDayGroups.Count; groupIndex++)
                {
                    var group = openDayGroups[groupIndex];
                    DrawDayDivider(TimeText.FutureDayLabel(new DateTimeOffset(DateTime.SpecifyKind(group.Date,
                        DateTimeKind.Local)).ToUnixTimeSeconds()), scale);
                    var openCard = GroupCard.Begin(theme, group.Entries.Count, ShiftRow.Height, showSeparators: false,
                        fillOverride: ui.Palette.CardFill);
                    for (var entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
                    {
                        var entry = group.Entries[entryIndex];
                        var openRow = openCard.NextRow();
                        var action = ShiftRow.Draw(openCard, openRow, entry.Shift, isOpen: true, entry.RoleName,
                            theme, ui.Accent, $"open-{entry.Shift.Id}", !shiftsActionBusy);
                        HandleShiftAction(action, entry.Shift.Id);
                    }

                    openCard.End();
                    ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
                }

                ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            }

            if (upcomingDayGroups.Count > 0)
            {
                ui.SectionHeading(Loc.T(L.VenueSync.Upcoming), 14f);
                for (var groupIndex = 0; groupIndex < upcomingDayGroups.Count; groupIndex++)
                {
                    var group = upcomingDayGroups[groupIndex];
                    DrawDayDivider(TimeText.FutureDayLabel(new DateTimeOffset(DateTime.SpecifyKind(group.Date,
                        DateTimeKind.Local)).ToUnixTimeSeconds()), scale);
                    var upcomingCard = GroupCard.Begin(theme, group.Entries.Count, ShiftRow.Height,
                        showSeparators: false, fillOverride: ui.Palette.CardFill);
                    for (var entryIndex = 0; entryIndex < group.Entries.Count; entryIndex++)
                    {
                        var entry = group.Entries[entryIndex];
                        var upcomingRow = upcomingCard.NextRow();
                        var action = ShiftRow.Draw(upcomingCard, upcomingRow, entry.Shift, isOpen: false, null, theme,
                            ui.Accent, $"upcoming-{entry.Shift.Id}", !shiftsActionBusy);
                        HandleShiftAction(action, entry.Shift.Id);
                    }

                    upcomingCard.End();
                    ImGui.Dummy(new Vector2(0f, Metrics.Space.Md * scale));
                }
            }
        }
    }

    private void RegroupShiftsIfStale(VenueSyncShiftsResponse shiftsSnapshot)
    {
        if (shiftsGroupedForRefreshUtc == state.LastRefreshUtc)
        {
            return;
        }

        shiftsGroupedForRefreshUtc = state.LastRefreshUtc;

        VenueSyncShift? activeShift = null;
        var upcomingShifts = new List<VenueSyncShift>();
        for (var shiftIndex = 0; shiftIndex < shiftsSnapshot.Shifts.Count; shiftIndex++)
        {
            var shift = shiftsSnapshot.Shifts[shiftIndex];
            if (shift.Status == "ACTIVE")
            {
                activeShift = shift;
            }
            else if (shift.Status == "SCHEDULED")
            {
                upcomingShifts.Add(shift);
            }
        }

        cachedActiveShift = activeShift;

        var openEntries = new List<ShiftListEntry>();
        for (var index = 0; index < shiftsSnapshot.OpenShifts.Count; index++)
        {
            var open = shiftsSnapshot.OpenShifts[index];
            var shim = new VenueSyncShift
            {
                Id = open.Id,
                ScheduledStart = open.ScheduledStart,
                ScheduledEnd = open.ScheduledEnd,
                Status = "OPEN",
            };
            openEntries.Add(new ShiftListEntry(shim, open.RoleName));
        }

        cachedOpenDayGroups = GroupByDay(openEntries);

        var upcomingEntries = new List<ShiftListEntry>();
        for (var index = 0; index < upcomingShifts.Count; index++)
        {
            upcomingEntries.Add(new ShiftListEntry(upcomingShifts[index], null));
        }

        cachedUpcomingDayGroups = GroupByDay(upcomingEntries);
    }

    private void HandleShiftAction(ShiftRowAction action, string shiftId)
    {
        if (shiftsActionBusy)
        {
            return;
        }

        switch (action)
        {
            case ShiftRowAction.ClockIn:
                shiftsActionBusy = true;
                _ = RunShiftActionAsync(() => client.ClockInAsync(shiftId, CancellationToken.None),
                    Loc.T(L.VenueSync.FailedToClockIn));
                break;
            case ShiftRowAction.ClockOut:
                shiftsActionBusy = true;
                _ = RunShiftActionAsync(() => client.ClockOutAsync(shiftId, CancellationToken.None),
                    Loc.T(L.VenueSync.FailedToClockOut), NoticeClockedOutHoursWorked);
                break;
            case ShiftRowAction.Claim:
                shiftsActionBusy = true;
                _ = RunShiftActionAsync(() => client.ClaimShiftAsync(shiftId, CancellationToken.None),
                    Loc.T(L.VenueSync.FailedToClaimShift));
                break;
            case ShiftRowAction.None:
            default:
                break;
        }
    }

    private async Task RunShiftActionAsync(Func<Task<VenueSyncClockResult?>> action, string failureMessage,
        Action<VenueSyncClockResult>? onSuccess = null)
    {
        try
        {
            var result = await action().ConfigureAwait(false);
            if (result is { Success: true })
            {
                shiftsActionError = null;
                shiftsActionNotice = null;
                onSuccess?.Invoke(result);
                state.EnsureShiftsFresh(true);
            }
            else
            {
                shiftsActionNotice = null;
                shiftsActionError = result?.Error ?? failureMessage;
            }
        }
        catch (Exception exception)
        {
            shiftsActionNotice = null;
            shiftsActionError = exception.Message;
        }
        finally
        {
            shiftsActionBusy = false;
        }
    }

    private void NoticeClockedOutHoursWorked(VenueSyncClockResult result)
    {
        if (result.HoursWorked.HasValue)
        {
            shiftsActionNotice = Loc.T(L.VenueSync.ClockedOutHoursWorked,
                result.HoursWorked.Value.ToString("F1", Loc.Culture));
        }
    }

    private static List<ShiftDayGroup> GroupByDay(List<ShiftListEntry> entries)
    {
        var groups = new List<ShiftDayGroup>();
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (!DateTime.TryParse(entry.Shift.ScheduledStart, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var start))
            {
                continue;
            }

            var date = start.Date;
            var lastIndex = groups.Count - 1;
            if (lastIndex >= 0 && groups[lastIndex].Date == date)
            {
                groups[lastIndex].Entries.Add(entry);
            }
            else
            {
                groups.Add(new ShiftDayGroup(date, new List<ShiftListEntry> { entry }));
            }
        }

        return groups;
    }

    private void DrawDayDivider(string label, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var labelSize = Typography.Measure(label, TextStyles.Caption2);
        var lineY = origin.Y + labelSize.Y * 0.5f;
        var lineLeft = origin.X + labelSize.X + Metrics.Space.Sm * scale;
        var lineRight = origin.X + ImGui.GetContentRegionAvail().X;
        Typography.Draw(drawList, origin, label, theme.TextMuted, TextStyles.Caption2);
        drawList.AddLine(new Vector2(lineLeft, lineY), new Vector2(lineRight, lineY),
            ImGui.GetColorU32(theme.Separator), 1f);
        ImGui.Dummy(new Vector2(0f, labelSize.Y + Metrics.Space.Sm * scale));
    }
}
