using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace Aetherphone.Core.Jobs;

internal static unsafe class JobsReader
{
    // ClassJobCategory sheet rows for the divisions the Character window groups by.
    private const uint WarCategoryId = 30; // "Disciple of War"
    private const uint LandCategoryId = 32; // "Disciple of the Land"
    private const uint HandCategoryId = 33; // "Disciple of the Hand"
    private const int RoleCount = 7;

    public static JobRoleSection[] Build(GameData gameData)
    {
        var playerState = PlayerState.Instance();
        if (playerState is null)
        {
            return Array.Empty<JobRoleSection>();
        }

        var buckets = new List<(JobEntry Entry, byte UiPriority)>[RoleCount];
        for (var index = 0; index < buckets.Length; index++)
        {
            buckets[index] = new List<(JobEntry, byte)>();
        }

        var levels = playerState->ClassJobLevels;
        var armoryTools = ArmoryToolsByJob(gameData);
        BuildGearsetEntries(gameData, levels, buckets);
        BuildToolEntries(gameData, levels, buckets, armoryTools, HandCategoryId, (int)JobRole.Hand);
        BuildToolEntries(gameData, levels, buckets, armoryTools, LandCategoryId, (int)JobRole.Land);

        var sections = new List<JobRoleSection>(RoleCount);
        for (var bucketIndex = 0; bucketIndex < buckets.Length; bucketIndex++)
        {
            var bucket = buckets[bucketIndex];
            if (bucket.Count == 0)
            {
                continue;
            }

            bucket.Sort((a, b) => a.UiPriority != b.UiPriority
                ? a.UiPriority.CompareTo(b.UiPriority)
                : string.CompareOrdinal(a.Entry.Abbreviation, b.Entry.Abbreviation));
            var role = (JobRole)bucketIndex;
            var jobEntries = new JobEntry[bucket.Count];
            for (var entryIndex = 0; entryIndex < bucket.Count; entryIndex++)
            {
                jobEntries[entryIndex] = bucket[entryIndex].Entry;
            }

            sections.Add(new JobRoleSection(role, TitleFor(role), jobEntries));
        }

        return sections.ToArray();
    }

    private static void BuildGearsetEntries(GameData gameData, Span<short> levels,
        List<(JobEntry Entry, byte UiPriority)>[] buckets)
    {
        var module = RaptureGearsetModule.Instance();
        if (module is null)
        {
            return;
        }

        var entries = module->Entries;
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if ((entry.Flags & RaptureGearsetModule.GearsetFlag.Exists) == 0)
            {
                continue;
            }

            var classJobId = (uint)entry.ClassJob;
            if (!gameData.TryGetClassJobDivision(classJobId, out var jobType, out var role, out var uiPriority,
                    out var categoryId))
            {
                continue;
            }

            var bucketIndex = CombatBucketFor(jobType, role, categoryId);
            if (bucketIndex < 0)
            {
                continue;
            }

            var level = LevelFor(gameData, levels, classJobId);
            var iconId = module->GetClassJobIconForGearset(entry.Id);
            var isActive = module->CurrentGearsetIndex == entry.Id;
            var jobEntry = new JobEntry(JobEntryKind.Gearset, entry.Id, classJobId, gameData.JobAbbreviation(classJobId),
                gameData.JobName(classJobId), level, entry.ItemLevel, iconId > 0 ? (uint)iconId : 0u, isActive);
            buckets[bucketIndex].Add((jobEntry, uiPriority));
        }
    }

    private static void BuildToolEntries(GameData gameData, Span<short> levels,
        List<(JobEntry Entry, byte UiPriority)>[] buckets, Dictionary<uint, uint> armoryTools, uint classJobCategoryId,
        int bucketIndex)
    {
        var currentJobId = gameData.LocalPlayer?.ClassJob.RowId ?? 0u;
        var classJobIds = gameData.ClassJobIdsInCategory(classJobCategoryId);
        for (var index = 0; index < classJobIds.Length; index++)
        {
            var classJobId = classJobIds[index];
            if (!gameData.TryGetClassJobDivision(classJobId, out _, out _, out var uiPriority, out _))
            {
                continue;
            }

            var isActive = classJobId == currentJobId;
            var itemLevel = -1;
            var iconId = 0u;
            if (isActive)
            {
                // The active job's tool is equipped, so it is never in the Armoury Chest the map was built from.
                TryGetEquippedMainHand(gameData, out iconId, out itemLevel);
            }
            else
            {
                armoryTools.TryGetValue(classJobId, out iconId);
            }

            var level = LevelFor(gameData, levels, classJobId);
            if (level == 0 && iconId == 0)
            {
                continue;
            }

            var jobEntry = new JobEntry(JobEntryKind.Tool, -1, classJobId, gameData.JobAbbreviation(classJobId),
                gameData.JobName(classJobId), level, itemLevel, iconId, isActive);
            buckets[bucketIndex].Add((jobEntry, uiPriority));
        }
    }

    private static int LevelFor(GameData gameData, Span<short> levels, uint classJobId)
    {
        var expArrayIndex = gameData.JobExpArrayIndex(classJobId);
        return expArrayIndex >= 0 && expArrayIndex < levels.Length ? levels[expArrayIndex] : 0;
    }

    private static bool TryGetEquippedMainHand(GameData gameData, out uint iconId, out int itemLevel)
    {
        iconId = 0;
        itemLevel = -1;
        var manager = InventoryManager.Instance();
        var equipped = manager is null ? null : manager->GetInventoryContainer(InventoryType.EquippedItems);
        if (equipped is null || !equipped->IsLoaded)
        {
            return false;
        }

        var mainHand = equipped->GetInventorySlot((int)RaptureGearsetModule.GearsetItemIndex.MainHand);
        if (mainHand is null || mainHand->ItemId == 0 ||
            !gameData.TryGetItem(mainHand->ItemId, out _, out var mainHandIconId, out var mainHandItemLevel))
        {
            return false;
        }

        iconId = mainHandIconId;
        itemLevel = mainHandItemLevel;
        return true;
    }

    private static Dictionary<uint, uint> ArmoryToolsByJob(GameData gameData)
    {
        var toolsByJob = new Dictionary<uint, uint>();
        var manager = InventoryManager.Instance();
        var armory = manager is null ? null : manager->GetInventoryContainer(InventoryType.ArmoryMainHand);
        if (armory is null || !armory->IsLoaded)
        {
            return toolsByJob;
        }

        for (var index = 0; index < armory->Size; index++)
        {
            var item = armory->GetInventorySlot(index);
            if (item is null || item->ItemId == 0 ||
                !gameData.TryGetItemClassJobUse(item->ItemId, out var classJobId, out var iconId))
            {
                continue;
            }

            toolsByJob.TryAdd(classJobId, iconId);
        }

        return toolsByJob;
    }

    // JobType splits the combat roles the way the in-game job guide does, but it reads 0 for the base classes
    // (Gladiator, Archer, Arcanist and friends), which would drop their gearsets entirely. Those fall back to
    // Role, which cannot tell physical from magical ranged: the Disciple of War/Magic category settles that.
    private static int CombatBucketFor(byte jobType, byte role, uint classJobCategoryId) =>
        jobType switch
        {
            1 => (int)JobRole.Tank,
            2 or 6 => (int)JobRole.Healer,
            3 => (int)JobRole.Melee,
            4 => (int)JobRole.PhysicalRanged,
            5 => (int)JobRole.MagicalRanged,
            _ => role switch
            {
                1 => (int)JobRole.Tank,
                2 => (int)JobRole.Melee,
                3 => classJobCategoryId == WarCategoryId ? (int)JobRole.PhysicalRanged : (int)JobRole.MagicalRanged,
                4 => (int)JobRole.Healer,
                _ => -1,
            },
        };

    private static LocString TitleFor(JobRole role) =>
        role switch
        {
            JobRole.Tank => L.Jobs.SectionTank,
            JobRole.Healer => L.Jobs.SectionHealer,
            JobRole.Melee => L.Jobs.SectionMelee,
            JobRole.PhysicalRanged => L.Jobs.SectionPhysicalRanged,
            JobRole.MagicalRanged => L.Jobs.SectionMagicalRanged,
            JobRole.Hand => L.Jobs.SectionHand,
            JobRole.Land => L.Jobs.SectionLand,
            _ => L.Jobs.SectionTank,
        };
}
