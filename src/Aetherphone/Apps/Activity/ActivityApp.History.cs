using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Activity;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Activity;

internal sealed partial class ActivityApp
{
    private const float HistoryRefreshSeconds = 1f;
    private const int WeekLength = 7;

    private readonly ActivityDay?[] weekDays = new ActivityDay?[WeekLength];
    private readonly string[] weekLabels = new string[WeekLength];
    private RefreshCadence historyCadence;
    private bool historyBuilt;
    private int currentStreak;
    private int bestStreak;
    private ActivityDay? bestExpDay;
    private ActivityDay? bestDutiesDay;
    private ActivityDay? bestGilDay;
    private ActivityDay? bestPlayDay;

    private void DrawHistory(float scale)
    {
        if (!historyBuilt || historyCadence.Advance(ImGui.GetIO().DeltaTime, HistoryRefreshSeconds))
        {
            BuildHistory();
            historyCadence.Reset();
            historyBuilt = true;
        }

        DrawWeek(scale);
        DrawStreaks(scale);
        DrawBests(scale);
    }

    private void BuildHistory()
    {
        var days = tracker.Days;
        var todayDate = DateTime.Now.Date;
        for (var slot = 0; slot < WeekLength; slot++)
        {
            var date = todayDate.AddDays(slot - (WeekLength - 1));
            weekDays[slot] = FindDay(days, DateKey(date));
            weekLabels[slot] = DayLabel(date, todayDate);
        }

        currentStreak = ComputeCurrentStreak(days, todayDate);
        bestStreak = ComputeBestStreak(days);
        bestExpDay = null;
        bestDutiesDay = null;
        bestGilDay = null;
        bestPlayDay = null;
        for (var index = 0; index < days.Count; index++)
        {
            var day = days[index];
            if (day.ExpGained > 0 && day.ExpGained > (bestExpDay?.ExpGained ?? 0))
            {
                bestExpDay = day;
            }

            if (day.DutiesCompleted > 0 && day.DutiesCompleted > (bestDutiesDay?.DutiesCompleted ?? 0))
            {
                bestDutiesDay = day;
            }

            if (day.GilEarned > 0 && day.GilEarned > (bestGilDay?.GilEarned ?? 0))
            {
                bestGilDay = day;
            }

            if (day.PlaySeconds > 0 && day.PlaySeconds > (bestPlayDay?.PlaySeconds ?? 0))
            {
                bestPlayDay = day;
            }
        }
    }

    private int ComputeCurrentStreak(IReadOnlyList<ActivityDay> days, DateTime todayDate)
    {
        var date = todayDate;
        if (!IsClosedDay(FindDay(days, DateKey(date))))
        {
            date = date.AddDays(-1);
        }

        var streak = 0;
        while (IsClosedDay(FindDay(days, DateKey(date))))
        {
            streak++;
            date = date.AddDays(-1);
        }

        return streak;
    }

    private int ComputeBestStreak(IReadOnlyList<ActivityDay> days)
    {
        var best = 0;
        var run = 0;
        var previous = DateTime.MinValue;
        for (var index = 0; index < days.Count; index++)
        {
            var day = days[index];
            if (!TryParseDate(day.Date, out var date))
            {
                continue;
            }

            if (!IsClosedDay(day))
            {
                run = 0;
                previous = date;
                continue;
            }

            run = run > 0 && previous != DateTime.MinValue && (date - previous).Days == 1 ? run + 1 : 1;
            best = Math.Max(best, run);
            previous = date;
        }

        return best;
    }

    private bool IsClosedDay(ActivityDay? day) => day is not null && ActivityGoals.AllClosed(configuration, day);

    private static ActivityDay? FindDay(IReadOnlyList<ActivityDay> days, string dateKey)
    {
        for (var index = days.Count - 1; index >= 0; index--)
        {
            if (days[index].Date == dateKey)
            {
                return days[index];
            }
        }

        return null;
    }

    private static string DateKey(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static bool TryParseDate(string dateKey, out DateTime date) =>
        DateTime.TryParseExact(dateKey, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static string DayLabel(DateTime date, DateTime todayDate)
    {
        if (date == todayDate)
        {
            return Loc.T(L.Time.Today);
        }

        if (date == todayDate.AddDays(-1))
        {
            return Loc.T(L.Time.Yesterday);
        }

        return Loc.Culture.TextInfo.ToTitleCase(date.ToString("dddd", Loc.Culture));
    }

    private void DrawWeek(float scale)
    {
        ui.SectionLabel(Loc.T(L.Character.ThisWeek), TextStyles.FootnoteEmphasized, 6f);
        var card = BeginCard(WeekLength, CompactRowHeight, scale);
        for (var slot = 0; slot < WeekLength; slot++)
        {
            DrawWeekRow(CardRow(card, slot, CompactRowHeight, scale), weekDays[slot], weekLabels[slot], scale);
        }

        EndCard(card, scale);
    }

    private void DrawWeekRow(Rect row, ActivityDay? day, string label, float scale)
    {
        var labelInk = day is null ? AppPalettes.Activity.MutedInk : AppPalettes.Activity.TitleInk;
        var labelSize = Typography.Measure(label, TextStyles.Headline);
        Typography.Draw(new Vector2(row.Min.X, row.Center.Y - labelSize.Y * 0.5f), label, labelInk,
            TextStyles.Headline);
        var ringsCenter = new Vector2(row.Max.X - 15f * scale, row.Center.Y);
        DrawMiniRings(ringsCenter, day, scale);
        if (day is null || day.PlaySeconds <= 0)
        {
            return;
        }

        var playtime = Duration(day.PlaySeconds);
        var playtimeSize = Typography.Measure(playtime, TextStyles.Footnote);
        Typography.Draw(
            new Vector2(ringsCenter.X - 15f * scale - 14f * scale - playtimeSize.X,
                row.Center.Y - playtimeSize.Y * 0.5f), playtime, AppPalettes.Activity.MutedInk, TextStyles.Footnote);
    }

    private void DrawMiniRings(Vector2 center, ActivityDay? day, float scale)
    {
        var track = Palette.WithAlpha(AppPalettes.Activity.TitleInk, 0.10f);
        var thickness = 3.2f * scale;
        DrawMiniRing(center, 14f * scale, thickness, day is null ? 0f : ProgressFractionFor(day),
            ActivityRings.RingOneTint, track);
        DrawMiniRing(center, 9.6f * scale, thickness, day is null ? 0f : AdventureFractionFor(day),
            ActivityRings.RingTwoTint, track);
        DrawMiniRing(center, 5.2f * scale, thickness, day is null ? 0f : FortuneFractionFor(day),
            ActivityRings.RingThreeTint, track);
    }

    private static void DrawMiniRing(Vector2 center, float radius, float thickness, float fraction, Vector4 tint,
        Vector4 track)
    {
        ProgressRing.Track(center, radius, thickness, track);
        var clamped = Math.Clamp(fraction, 0f, 1f);
        if (clamped > 0.001f)
        {
            ProgressRing.Fill(center, radius, thickness, clamped, tint);
        }
    }

    private void DrawStreaks(float scale)
    {
        ui.SectionLabel(Loc.T(L.Character.Streaks), TextStyles.FootnoteEmphasized, 6f);
        var card = BeginCard(2, CompactRowHeight, scale);
        var currentInk = currentStreak > 0 ? AppPalettes.Activity.Accent : AppPalettes.Activity.MutedInk;
        StatRow(CardRow(card, 0, CompactRowHeight, scale), Accent.Amber, FontAwesomeIcon.Fire,
            Loc.T(L.Character.CurrentStreak), Loc.Plural(L.Character.StreakDays, currentStreak), currentInk, null,
            scale);
        StatRow(CardRow(card, 1, CompactRowHeight, scale), Accent.Violet, FontAwesomeIcon.Trophy,
            Loc.T(L.Character.BestStreak), Loc.Plural(L.Character.StreakDays, bestStreak),
            AppPalettes.Activity.TitleInk, null, scale);
        EndCard(card, scale);
        ui.HelpText(Loc.T(L.Character.StreaksHint));
        ImGui.Dummy(new Vector2(0f, 8f * scale));
    }

    private void DrawBests(float scale)
    {
        var rowCount = 0;
        rowCount += bestExpDay is null ? 0 : 1;
        rowCount += bestDutiesDay is null ? 0 : 1;
        rowCount += bestGilDay is null ? 0 : 1;
        rowCount += bestPlayDay is null ? 0 : 1;
        if (rowCount == 0)
        {
            return;
        }

        ui.SectionLabel(Loc.T(L.Character.PersonalBests), TextStyles.FootnoteEmphasized, 6f);
        var card = BeginCard(rowCount, RowHeight, scale);
        var rowIndex = 0;
        if (bestExpDay is { } expDay)
        {
            StatRow(CardRow(card, rowIndex++, RowHeight, scale), ActivityRings.RingOneTint, FontAwesomeIcon.Bolt,
                Loc.T(L.Character.Experience), "+" + Compact(expDay.ExpGained), ActivityRings.RingOneTint,
                BestDateLabel(expDay), scale);
        }

        if (bestDutiesDay is { } dutiesDay)
        {
            StatRow(CardRow(card, rowIndex++, RowHeight, scale), ActivityRings.RingTwoTint, FontAwesomeIcon.Dungeon,
                Loc.T(L.Character.Duties), Number(dutiesDay.DutiesCompleted), ActivityRings.RingTwoTint,
                BestDateLabel(dutiesDay), scale);
        }

        if (bestGilDay is { } gilDay)
        {
            StatRow(CardRow(card, rowIndex++, RowHeight, scale), ActivityRings.RingThreeTint, FontAwesomeIcon.Coins,
                Loc.T(L.Character.GilEarned), "+" + Compact(gilDay.GilEarned), ActivityRings.RingThreeTint,
                BestDateLabel(gilDay), scale);
        }

        if (bestPlayDay is { } playDay)
        {
            StatRow(CardRow(card, rowIndex, RowHeight, scale), Accent.Blue, FontAwesomeIcon.Clock,
                Loc.T(L.Character.TimePlayed), Duration(playDay.PlaySeconds), AppPalettes.Activity.TitleInk,
                BestDateLabel(playDay), scale);
        }

        EndCard(card, scale);
    }

    private static string BestDateLabel(ActivityDay day) =>
        TryParseDate(day.Date, out var date) ? date.ToString("MMM d", Loc.Culture) : string.Empty;
}
