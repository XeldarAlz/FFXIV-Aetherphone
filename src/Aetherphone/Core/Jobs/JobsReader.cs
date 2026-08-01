using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace Aetherphone.Core.Jobs;

internal static unsafe class JobsReader
{
    private const uint WarCategoryId = 30;
    private const uint LandCategoryId = 32;
    private const uint HandCategoryId = 33;
    private const int RoleCount = 7;

    public static JobSection[] Build(GameData gameData, IReadOnlyList<JobsCategory> categories)
    {
        var playerState = PlayerState.Instance();
        if (playerState is null)
        {
            return Array.Empty<JobSection>();
        }

        var buckets = new List<(JobEntry Entry, byte UiPriority)>[RoleCount];
        for (var index = 0; index < buckets.Length; index++)
        {
            buckets[index] = new List<(JobEntry, byte)>();
        }

        var customBuckets = new List<(JobEntry Entry, byte UiPriority)>[categories.Count];
        for (var index = 0; index < customBuckets.Length; index++)
        {
            customBuckets[index] = new List<(JobEntry, byte)>();
        }

        var categoryByGearsetId = new Dictionary<int, int>();
        for (var categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++)
        {
            var gearsetIds = categories[categoryIndex].GearsetIds;
            for (var idIndex = 0; idIndex < gearsetIds.Count; idIndex++)
            {
                categoryByGearsetId.TryAdd(gearsetIds[idIndex], categoryIndex);
            }
        }

        var levels = playerState->ClassJobLevels;
        var gearsetJobIds = new HashSet<uint>();
        BuildGearsetEntries(gameData, levels, buckets, customBuckets, categoryByGearsetId, gearsetJobIds);
        BuildClassEntries(gameData, levels, buckets, gearsetJobIds, HandCategoryId, (int)JobRole.Hand);
        BuildClassEntries(gameData, levels, buckets, gearsetJobIds, LandCategoryId, (int)JobRole.Land);

        var sections = new List<JobSection>(customBuckets.Length + RoleCount);
        for (var categoryIndex = 0; categoryIndex < customBuckets.Length; categoryIndex++)
        {
            sections.Add(new JobSection(categoryIndex, categories[categoryIndex].Name,
                CustomOrderedEntries(customBuckets[categoryIndex], categories[categoryIndex].GearsetIds)));
        }

        for (var bucketIndex = 0; bucketIndex < buckets.Length; bucketIndex++)
        {
            var bucket = buckets[bucketIndex];
            if (bucket.Count == 0)
            {
                continue;
            }

            sections.Add(new JobSection(TitleFor((JobRole)bucketIndex), SortedEntries(bucket)));
        }

        return sections.ToArray();
    }

    // Custom categories are user-curated, so their display order follows GearsetIds' own list
    // order (whatever JobsApp's drag-to-reorder last left it in) instead of UiPriority - unlike
    // the built-in role sections below, which still fall back to SortedEntries. A gearset id with
    // no live entry this cycle (deleted, or the bucket simply hasn't caught up yet) is skipped
    // rather than left as a gap.
    private static JobEntry[] CustomOrderedEntries(List<(JobEntry Entry, byte UiPriority)> bucket,
        List<int> gearsetIds)
    {
        var byGearsetId = new Dictionary<int, JobEntry>(bucket.Count);
        for (var index = 0; index < bucket.Count; index++)
        {
            byGearsetId[bucket[index].Entry.GearsetId] = bucket[index].Entry;
        }

        var ordered = new List<JobEntry>(bucket.Count);
        for (var index = 0; index < gearsetIds.Count; index++)
        {
            if (byGearsetId.TryGetValue(gearsetIds[index], out var entry))
            {
                ordered.Add(entry);
            }
        }

        return ordered.ToArray();
    }

    private static JobEntry[] SortedEntries(List<(JobEntry Entry, byte UiPriority)> bucket)
    {
        bucket.Sort((a, b) => a.UiPriority != b.UiPriority
            ? a.UiPriority.CompareTo(b.UiPriority)
            : string.CompareOrdinal(a.Entry.Abbreviation, b.Entry.Abbreviation));
        var jobEntries = new JobEntry[bucket.Count];
        for (var entryIndex = 0; entryIndex < bucket.Count; entryIndex++)
        {
            jobEntries[entryIndex] = bucket[entryIndex].Entry;
        }

        return jobEntries;
    }

    private static void BuildGearsetEntries(GameData gameData, Span<short> levels,
        List<(JobEntry Entry, byte UiPriority)>[] buckets, List<(JobEntry Entry, byte UiPriority)>[] customBuckets,
        Dictionary<int, int> categoryByGearsetId, HashSet<uint> gearsetJobIds)
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

            var bucketIndex = BucketFor(jobType, role, categoryId);
            if (bucketIndex < 0)
            {
                continue;
            }

            var level = LevelFor(gameData, levels, classJobId);
            var isActive = module->CurrentGearsetIndex == entry.Id;
            var gearsetName = entry.NameString;
            if (gearsetName.Length == 0)
            {
                gearsetName = gameData.JobName(classJobId);
            }

            var jobEntry = new JobEntry(JobEntryKind.Gearset, entry.Id, classJobId, gameData.JobAbbreviation(classJobId),
                gearsetName, level, entry.ItemLevel, GameData.JobIconId(classJobId), isActive);
            if (categoryByGearsetId.TryGetValue(entry.Id, out var categoryIndex))
            {
                customBuckets[categoryIndex].Add((jobEntry, uiPriority));
            }
            else
            {
                buckets[bucketIndex].Add((jobEntry, uiPriority));
            }

            gearsetJobIds.Add(classJobId);
        }
    }

    private static void BuildClassEntries(GameData gameData, Span<short> levels,
        List<(JobEntry Entry, byte UiPriority)>[] buckets, HashSet<uint> gearsetJobIds, uint classJobCategoryId,
        int bucketIndex)
    {
        var currentJobId = gameData.LocalPlayer?.ClassJob.RowId ?? 0u;
        var classJobIds = gameData.ClassJobIdsInCategory(classJobCategoryId);
        for (var index = 0; index < classJobIds.Length; index++)
        {
            var classJobId = classJobIds[index];
            var level = LevelFor(gameData, levels, classJobId);
            if (level == 0 || gearsetJobIds.Contains(classJobId) ||
                !gameData.TryGetClassJobDivision(classJobId, out _, out _, out var uiPriority, out _))
            {
                continue;
            }

            var isActive = classJobId == currentJobId;
            var itemLevel = -1;
            if (isActive)
            {
                TryGetEquippedMainHandItemLevel(gameData, out itemLevel);
            }

            var jobEntry = new JobEntry(JobEntryKind.NoGearset, -1, classJobId, gameData.JobAbbreviation(classJobId),
                gameData.JobName(classJobId), level, itemLevel, GameData.JobIconId(classJobId), isActive);
            buckets[bucketIndex].Add((jobEntry, uiPriority));
        }
    }

    private static int LevelFor(GameData gameData, Span<short> levels, uint classJobId)
    {
        var expArrayIndex = gameData.JobExpArrayIndex(classJobId);
        return expArrayIndex >= 0 && expArrayIndex < levels.Length ? levels[expArrayIndex] : 0;
    }

    private static bool TryGetEquippedMainHandItemLevel(GameData gameData, out int itemLevel)
    {
        itemLevel = -1;
        var manager = InventoryManager.Instance();
        var equipped = manager is null ? null : manager->GetInventoryContainer(InventoryType.EquippedItems);
        if (equipped is null || !equipped->IsLoaded)
        {
            return false;
        }

        var mainHand = equipped->GetInventorySlot((int)RaptureGearsetModule.GearsetItemIndex.MainHand);
        if (mainHand is null || mainHand->ItemId == 0 ||
            !gameData.TryGetItem(mainHand->ItemId, out _, out _, out var mainHandItemLevel))
        {
            return false;
        }

        itemLevel = mainHandItemLevel;
        return true;
    }

    private static int BucketFor(byte jobType, byte role, uint classJobCategoryId)
    {
        if (classJobCategoryId == HandCategoryId)
        {
            return (int)JobRole.Hand;
        }

        if (classJobCategoryId == LandCategoryId)
        {
            return (int)JobRole.Land;
        }

        return jobType switch
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
    }

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
