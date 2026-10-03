using Aetherphone.Core.Game;

namespace Aetherphone.Core.Dailies;

internal static class DailyProgress
{
    public static DailyAutoStatus ReadStatus(GameData gameData, in DailyItem item)
    {
        return item.Tracking switch
        {
            DailyTracking.Manual => DailyAutoStatus.Unavailable,
            DailyTracking.DutyRoulettes => DailiesReader.ReadDutyRoulettes(gameData.DailyBonusRouletteRowIds()),
            DailyTracking.HuntBills => DailiesReader.ReadHuntBills(gameData.WeeklyHuntBillIndices(),
                gameData.HuntOrderTypeSheet(), gameData.HuntOrderSheet()),
            _ => DailiesReader.Read(item.Tracking, item.Goal),
        };
    }

    public static DailyRowState State(in DailyItem item, in DailyAutoStatus status, bool hidden, bool manualChecked)
    {
        if (hidden)
        {
            return DailyRowState.Hidden;
        }

        if (item.Tracking == DailyTracking.Manual)
        {
            return manualChecked ? DailyRowState.Done : DailyRowState.Open;
        }

        if (!status.Available)
        {
            return DailyRowState.Unavailable;
        }

        if (item.Tracking == DailyTracking.Levequests)
        {
            return DailyRowState.Info;
        }

        return status.Complete ? DailyRowState.Done : DailyRowState.Open;
    }

    public static bool Counts(DailyRowState state) => state is DailyRowState.Open or DailyRowState.Done;

    public static int DoneCount(in DailyAutoStatus status, DailyTracking tracking) =>
        tracking == DailyTracking.Levequests
            ? Math.Clamp(status.Remaining, 0, status.Goal)
            : Math.Clamp(status.Goal - status.Remaining, 0, status.Goal);

    public static float Fraction(in DailyAutoStatus status, DailyTracking tracking)
    {
        if (!status.Available || status.Goal <= 0)
        {
            return 0f;
        }

        if (status.Complete && tracking != DailyTracking.Levequests)
        {
            return 1f;
        }

        return Math.Clamp(DoneCount(status, tracking) / (float)status.Goal, 0f, 1f);
    }

    public static bool ShowsCount(DailyTracking tracking) =>
        tracking is DailyTracking.BeastTribeAllowances or DailyTracking.CustomDeliveries
            or DailyTracking.WondrousTails or DailyTracking.Levequests or DailyTracking.HuntBills
            or DailyTracking.DomanEnclave;
}
