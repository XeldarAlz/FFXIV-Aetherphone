using Aetherphone.Core.Game;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel;
using MobHuntOrder = Lumina.Excel.Sheets.MobHuntOrder;
using MobHuntOrderType = Lumina.Excel.Sheets.MobHuntOrderType;

namespace Aetherphone.Core.Dailies;

internal static unsafe class DailiesReader
{
    private const int WondrousTailsCellCount = 16;

    public static DailyAutoStatus Read(DailyTracking tracking, int goal)
    {
        if (!GameMemory.Attached)
        {
            return GameMemory.Simulated is { } simulated ? ReadSimulated(simulated.Dailies, tracking, goal) : default;
        }

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

    private static DailyAutoStatus ReadSimulated(SimulatedDailies dailies, DailyTracking tracking, int goal) =>
        tracking switch
        {
            DailyTracking.BeastTribeAllowances => Allowance(dailies.BeastTribeAllowances, goal),
            DailyTracking.CustomDeliveries => Allowance(dailies.CustomDeliveryAllowances, goal),
            DailyTracking.WondrousTails => WondrousTails(dailies.WondrousTailsExpired, dailies.WondrousTailsStickers,
                goal),
            DailyTracking.DomanEnclave => DomanEnclave(dailies.DomanDonated, dailies.DomanAllowance),
            DailyTracking.Levequests => Levequests(dailies.LeveAllowances, goal),
            _ => DailyAutoStatus.Unavailable,
        };

    private static DailyAutoStatus ReadBeastTribe(int goal)
    {
        var quests = QuestManager.Instance();
        if (quests is null)
        {
            return DailyAutoStatus.Unavailable;
        }

        return Allowance((int)quests->GetBeastTribeAllowance(), goal);
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

        return Allowance(remaining, goal);
    }

    private static DailyAutoStatus Allowance(int remaining, int goal)
    {
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
            return WondrousTails(true, 0, goal);
        }

        var placed = 0;
        for (var index = 0; index < WondrousTailsCellCount; index++)
        {
            if (player->IsWeeklyBingoStickerPlaced(index))
            {
                placed++;
            }
        }

        return WondrousTails(false, placed, goal);
    }

    private static DailyAutoStatus WondrousTails(bool expired, int placed, int goal)
    {
        if (expired)
        {
            return new DailyAutoStatus(true, false, goal, goal);
        }

        var clampedPlaced = Math.Clamp(placed, 0, goal);
        var remaining = goal - clampedPlaced;
        return new DailyAutoStatus(true, remaining == 0, remaining, goal);
    }

    private static DailyAutoStatus ReadDomanEnclave()
    {
        var manager = DomanEnclaveManager.Instance();
        if (manager is null)
        {
            return DailyAutoStatus.Unavailable;
        }

        var state = manager->State;
        return DomanEnclave((int)state.Donated, (int)state.Allowance);
    }

    private static DailyAutoStatus DomanEnclave(int donated, int allowance)
    {
        if (allowance == 0)
        {
            return DailyAutoStatus.Unavailable;
        }

        var remaining = allowance - donated;
        return new DailyAutoStatus(true, remaining == 0, remaining, allowance);
    }

    private static DailyAutoStatus ReadLevequests(int goal)
    {
        var quests = QuestManager.Instance();
        if (quests is null)
        {
            return DailyAutoStatus.Unavailable;
        }

        return Levequests((int)quests->NumLeveAllowances, goal);
    }

    private static DailyAutoStatus Levequests(int allowances, int goal) =>
        new(true, true, Math.Clamp(allowances, 0, goal), goal);

    public static DailyAutoStatus ReadDutyRoulettes(IReadOnlyList<byte> rouletteRowIds)
    {
        if (!GameMemory.Attached)
        {
            return GameMemory.Simulated is { } simulated && rouletteRowIds.Count > 0
                ? Roulette(simulated.Dailies.DutyRouletteComplete)
                : default;
        }

        var content = InstanceContent.Instance();
        if (content is null || rouletteRowIds.Count == 0)
        {
            return DailyAutoStatus.Unavailable;
        }

        var claimed = 0;
        for (var index = 0; index < rouletteRowIds.Count; index++)
        {
            if (content->IsRouletteComplete(rouletteRowIds[index]))
            {
                claimed++;
            }
        }

        return Roulette(claimed > 0);
    }

    private static DailyAutoStatus Roulette(bool complete) => new(true, complete, complete ? 0 : 1, 1);

    public static DailyAutoStatus ReadHuntBills(IReadOnlyList<byte> weeklyIndices,
        ExcelSheet<MobHuntOrderType> orderTypes, SubrowExcelSheet<MobHuntOrder> orders)
    {
        if (!GameMemory.Attached)
        {
            return GameMemory.Simulated is { } simulated && weeklyIndices.Count > 0
                ? HuntBills(Math.Min(simulated.Dailies.HuntBillsUnlocked, weeklyIndices.Count),
                    simulated.Dailies.HuntBillsComplete)
                : default;
        }

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
            if (IsWeeklyBillComplete(hunt, billIndex, orderTypes, orders))
            {
                done++;
            }
        }

        return HuntBills(unlocked, done);
    }

    private static DailyAutoStatus HuntBills(int unlocked, int done)
    {
        if (unlocked == 0)
        {
            return DailyAutoStatus.Unavailable;
        }

        var clampedDone = Math.Min(done, unlocked);
        return new DailyAutoStatus(true, clampedDone >= unlocked, unlocked - clampedDone, unlocked);
    }

    private static bool IsWeeklyBillComplete(MobHunt* hunt, byte billIndex,
        ExcelSheet<MobHuntOrderType> orderTypes, SubrowExcelSheet<MobHuntOrder> orders)
    {
        if (hunt->GetAvailableHuntOrderRowId(billIndex) != hunt->GetObtainedHuntOrderRowId(billIndex))
        {
            return false;
        }

        var obtainedMark = hunt->ObtainedMarkId[billIndex];
        if (obtainedMark == 0)
        {
            return false;
        }

        if (!orderTypes.TryGetRow(billIndex, out var orderType))
        {
            return false;
        }

        var targetRow = orderType.OrderStart.RowId + obtainedMark - 1u;
        if (!orders.TryGetSubrow(targetRow, 0, out var eliteTarget))
        {
            return false;
        }

        return hunt->CurrentKills[billIndex][0] == eliteTarget.NeededKills;
    }
}
