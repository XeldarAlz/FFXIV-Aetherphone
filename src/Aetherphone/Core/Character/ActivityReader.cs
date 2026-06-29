using Aetherphone.Core.Game;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Aetherphone.Core.Character;

internal static unsafe class ActivityReader
{
    private const long WeeklyTomestoneCap = 2000;

    public static ActivitySnapshot? Read(GameData gameData)
    {
        var player = gameData.LocalPlayer;
        if (player is null)
        {
            return null;
        }

        var playerState = PlayerState.Instance();
        if (playerState is null)
        {
            return null;
        }

        var jobRowId = player.ClassJob.RowId;
        var level = player.Level;
        var maxLevel = playerState->MaxLevel > 0 && level >= playerState->MaxLevel;

        long currentExp = 0;
        long neededExp = 0;
        if (!maxLevel)
        {
            currentExp = playerState->GetCurrentClassJobExp();
            neededExp = playerState->GetCurrentClassJobNeededExp();
        }

        CountJobsAtMax(playerState, out var jobsAtMax, out var jobsTotal);
        ReadTomestone(gameData, out var tomeAmount, out var tomeName);
        ReadCollection(gameData, playerState, out var mountsOwned, out var mountsTotal, out var minionsOwned, out var minionsTotal);
        ReadRetainers(out var retainerCount, out var venturesReady, out var venturesActive);

        var gil = ReadGil();

        return new ActivitySnapshot(
            gameData.JobName(jobRowId),
            level,
            maxLevel,
            currentExp,
            neededExp,
            jobsAtMax,
            jobsTotal,
            tomeAmount,
            WeeklyTomestoneCap,
            tomeName,
            mountsOwned,
            mountsTotal,
            minionsOwned,
            minionsTotal,
            gil,
            retainerCount,
            venturesReady,
            venturesActive);
    }

    private static void CountJobsAtMax(PlayerState* playerState, out int jobsAtMax, out int jobsTotal)
    {
        jobsAtMax = 0;
        jobsTotal = 0;

        var maxLevel = playerState->MaxLevel;
        if (maxLevel == 0)
        {
            return;
        }

        var levels = playerState->ClassJobLevels;
        for (var index = 0; index < levels.Length; index++)
        {
            var jobLevel = levels[index];
            if (jobLevel <= 0)
            {
                continue;
            }

            jobsTotal++;
            if (jobLevel >= maxLevel)
            {
                jobsAtMax++;
            }
        }
    }

    private static void ReadTomestone(GameData gameData, out long amount, out string name)
    {
        amount = 0;
        name = string.Empty;

        if (!gameData.TryGetWeeklyTomestone(out var itemId, out var resolvedName))
        {
            return;
        }

        name = resolvedName;

        var manager = InventoryManager.Instance();
        if (manager is null)
        {
            return;
        }

        amount = manager->GetTomestoneCount(itemId);
    }

    private static void ReadCollection(GameData gameData, PlayerState* playerState, out int mountsOwned, out int mountsTotal, out int minionsOwned, out int minionsTotal)
    {
        mountsOwned = 0;
        minionsOwned = 0;

        var mountIds = gameData.CollectableMountIds();
        mountsTotal = mountIds.Length;
        for (var index = 0; index < mountIds.Length; index++)
        {
            if (playerState->IsMountUnlocked(mountIds[index]))
            {
                mountsOwned++;
            }
        }

        var minionIds = gameData.CollectableMinionIds();
        minionsTotal = minionIds.Length;

        var uiState = UIState.Instance();
        if (uiState is null)
        {
            return;
        }

        for (var index = 0; index < minionIds.Length; index++)
        {
            if (uiState->IsCompanionUnlocked(minionIds[index]))
            {
                minionsOwned++;
            }
        }
    }

    private static void ReadRetainers(out int count, out int venturesReady, out int venturesActive)
    {
        count = 0;
        venturesReady = 0;
        venturesActive = 0;

        var manager = RetainerManager.Instance();
        if (manager is null)
        {
            return;
        }

        var retainerCount = manager->GetRetainerCount();
        if (retainerCount == 0)
        {
            return;
        }

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        for (var index = 0u; index < retainerCount; index++)
        {
            var retainer = manager->GetRetainerBySortedIndex(index);
            if (retainer is null)
            {
                continue;
            }

            count++;
            if (retainer->VentureId == 0)
            {
                continue;
            }

            if (retainer->VentureComplete <= nowUnix)
            {
                venturesReady++;
            }
            else
            {
                venturesActive++;
            }
        }
    }

    private static long ReadGil()
    {
        var manager = InventoryManager.Instance();
        if (manager is null)
        {
            return 0;
        }

        return (long)manager->GetGil();
    }
}
