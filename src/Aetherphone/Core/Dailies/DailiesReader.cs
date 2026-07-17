using Aetherphone.Core.Game;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Aetherphone.Core.Dailies;

internal static unsafe class DailiesReader
{
    private const int WondrousTailsCellCount = 16;

    public static DailyAutoStatus Read(DailyTracking tracking, int goal)
    {
        switch (tracking)
        {
            case DailyTracking.BeastTribeAllowances:
            {
                return ReadBeastTribe(goal);
            }
            case DailyTracking.CustomDeliveries:
            {
                return ReadCustomDeliveries(goal);
            }
            case DailyTracking.WondrousTails:
            {
                return ReadWondrousTails(goal);
            }
            case DailyTracking.DomanEnclave:
            {
                return ReadDomanEnclave();
            }
            case DailyTracking.Levequests:
            {
                return ReadLevequests(goal);
            }
            default:
            {
                return DailyAutoStatus.Unavailable;
            }
        }
    }

    private static DailyAutoStatus ReadBeastTribe(int goal)
    {
        var quests = QuestManager.Instance();
        if (quests is null)
        {
            return DailyAutoStatus.Unavailable;
        }

        var remaining = (int)quests->GetBeastTribeAllowance();
        var clamped = Math.Clamp(remaining, 0, goal);
        return new DailyAutoStatus(true, clamped == 0, clamped, goal);
    }

    private static DailyAutoStatus ReadCustomDeliveries(int goal)
    {
        var supply = SatisfactionSupplyManager.Instance();
        if (supply is null)
        {
            return DailyAutoStatus.Unavailable;
        }

        var remaining = supply->GetRemainingAllowances();
        if (remaining < 0)
        {
            return DailyAutoStatus.Unavailable;
        }

        var clamped = Math.Clamp(remaining, 0, goal);
        return new DailyAutoStatus(true, clamped == 0, clamped, goal);
    }

    private static DailyAutoStatus ReadWondrousTails(int goal)
    {
        var player = PlayerState.Instance();
        if (player is null)
        {
            return DailyAutoStatus.Unavailable;
        }

        if (player->IsWeeklyBingoExpired())
        {
            return new DailyAutoStatus(true, false, goal, goal);
        }

        var placed = 0;
        for (var index = 0; index < WondrousTailsCellCount; index++)
        {
            if (player->IsWeeklyBingoStickerPlaced(index))
            {
                placed++;
            }
        }

        var clampedPlaced = Math.Clamp(placed, 0, goal);
        var remaining = goal - clampedPlaced;
        return new DailyAutoStatus(true, remaining == 0, remaining, goal);
    }

    private static DailyAutoStatus ReadDomanEnclave()
    {
        var manager = DomanEnclaveManager.Instance();
        if (manager is null || !manager->IsLoaded)
        {
            return DailyAutoStatus.Unavailable;
        }

        var state = manager->State;
        if (state.CurrentMilestone == 0)
        {
            return DailyAutoStatus.Unavailable;
        }

        var remaining = state.Allowance;
        return new DailyAutoStatus(true, remaining == 0, remaining, remaining);
    }

    private static DailyAutoStatus ReadLevequests(int goal)
    {
        var quests = QuestManager.Instance();
        if (quests is null)
        {
            return DailyAutoStatus.Unavailable;
        }

        var current = Math.Clamp((int)quests->NumLeveAllowances, 0, goal);
        return new DailyAutoStatus(true, true, current, goal);
    }

    public static DailyAutoStatus ReadDutyRoulettes(IReadOnlyList<byte> bonusIndices)
    {
        var content = InstanceContent.Instance();
        if (content is null || bonusIndices.Count == 0)
        {
            return DailyAutoStatus.Unavailable;
        }

        var claimed = 0;
        for (var index = 0; index < bonusIndices.Count; index++)
        {
            if (content->IsRouletteComplete(bonusIndices[index]))
            {
                claimed++;
            }
        }

        var complete = claimed > 0;
        return new DailyAutoStatus(true, complete, complete ? 0 : 1, 1);
    }

    public static DailyAutoStatus ReadHuntBills(IReadOnlyList<byte> weeklyIndices)
    {
        var hunt = MobHunt.Instance();
        if (hunt is null || weeklyIndices.Count == 0)
        {
            return DailyAutoStatus.Unavailable;
        }

        var unlocked = 0;
        var done = 0;
        for (var index = 0; index < weeklyIndices.Count; index++)
        {
            var billIndex = weeklyIndices[index];
            if (!hunt->IsMarkBillUnlocked(billIndex))
            {
                continue;
            }

            unlocked++;
            if (!hunt->IsMarkBillObtained(billIndex) && hunt->GetAvailableHuntOrderRowId(billIndex) == 0)
            {
                done++;
            }
        }

        if (unlocked == 0)
        {
            return DailyAutoStatus.Unavailable;
        }

        return new DailyAutoStatus(true, done >= unlocked, unlocked - done, unlocked);
    }

    public static TimerWindow ReadFashionReportWindow(DateTime utcNow) => GameSchedule.FashionReport(utcNow);

    public static DateTime ReadNextJumboCactpot(DateTime utcNow) => GameSchedule.NextJumboCactpot(utcNow);
}
