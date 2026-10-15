using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Apps.Casino.Strip;

internal enum StreakDayState : byte
{
    Future,
    Done,
    Today,
}

internal readonly record struct StreakView(int Done, int TodayIndex, bool Claimable, int DayNumber)
{
    public static StreakView Of(CasinoBonusDto streak, int days)
    {
        if (streak.Ready && streak.Eligible)
        {
            var paid = Math.Clamp(streak.StreakDay - 1, 0, days - 1);
            return new StreakView(paid, paid, true, paid + 1);
        }

        var claimed = Math.Clamp(streak.StreakDay, 0, days);
        return claimed == 0
            ? new StreakView(0, -1, false, 1)
            : new StreakView(claimed, claimed - 1, false, claimed);
    }

    public StreakDayState StateOf(int day)
    {
        if (day == TodayIndex)
        {
            return StreakDayState.Today;
        }

        return day < Done ? StreakDayState.Done : StreakDayState.Future;
    }
}
