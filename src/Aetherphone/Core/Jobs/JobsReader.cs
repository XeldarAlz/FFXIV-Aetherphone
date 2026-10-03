using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace Aetherphone.Core.Jobs;

internal static unsafe class JobsReader
{
    private const int MaxSlots = 64;
    private const int MaxGearsets = 100;

    public static JobsSnapshot Build(GameData gameData, IReadOnlyList<JobsCategory> categories)
    {
        var playerState = PlayerState.Instance();
        if (playerState is null || !playerState->IsLoaded)
        {
            return JobsSnapshot.Empty;
        }

        var facts = JobsCatalog.Facts();
        if (facts.Length == 0)
        {
            return JobsSnapshot.Empty;
        }

        var levels = playerState->ClassJobLevels;
        var experience = playerState->ClassJobExperience;
        var maxLevel = (int)playerState->MaxLevel;
        var currentJobId = (uint)playerState->CurrentClassJobId;
        var rested = Math.Max(0L, (long)playerState->BaseRestedExperience);

        var gearsets = ReadGearsets(gameData, out var activeGearsetId);
        Span<bool> unlocked = stackalloc bool[facts.Length];
        for (var index = 0; index < facts.Length; index++)
        {
            var level = LevelAt(levels, facts[index].ExpArrayIndex);
            unlocked[index] = JobsRoster.HasParent(facts[index])
                ? level > 0 && (facts[index].Id == currentJobId || HasGearsetFor(gearsets, facts[index].Id) ||
                                JobsCatalog.IsQuestCompleted(facts[index].UnlockQuestId))
                : level > 0;
        }

        Span<JobSlot> slots = stackalloc JobSlot[MaxSlots];
        var slotCount = JobsRoster.Compose(facts, unlocked, slots);
        var jobs = new JobRow[slotCount];
        var roleStarts = new int[JobRoles.Count + 1];
        Span<int> summaryLevels = stackalloc int[slotCount];
        Span<int> summaryCaps = stackalloc int[slotCount];
        var activeJobIndex = -1;
        for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            var slot = slots[slotIndex];
            var row = facts[slot.FactIndex];
            var level = slot.Locked ? 0 : LevelAt(levels, row.ExpArrayIndex);
            var cap = JobsRoster.CapFor(row.IsLimited, maxLevel);
            var capped = level >= cap;
            var current = slot.Locked ? 0L : Math.Max(0L, (long)LevelAt(experience, row.ExpArrayIndex));
            var needed = capped || level <= 0 ? 0L : gameData.ExpToNextLevel(level);
            var isActive = !slot.Locked && IsSameDiscipline(facts, row, currentJobId);
            if (isActive && (activeJobIndex < 0 || row.Id == currentJobId))
            {
                activeJobIndex = slotIndex;
            }

            jobs[slotIndex] = new JobRow
            {
                ClassJobId = row.Id,
                Role = slot.Role,
                Name = gameData.JobName(row.Id),
                Abbreviation = gameData.JobAbbreviation(row.Id),
                IconId = GameData.JobIconId(row.Id),
                Level = level,
                Cap = cap,
                Experience = current,
                ExperienceNeeded = needed,
                Fraction = slot.Locked ? 0f : JobsRoster.Fraction(current, needed, capped),
                IsCapped = !slot.Locked && capped,
                IsLocked = slot.Locked,
                StartingLevel = row.StartingLevel,
                LevelText = slot.Locked ? Loc.T(L.Jobs.Locked) : Loc.T(L.Jobs.TileLevel, level),
                LockedText = !slot.Locked
                    ? string.Empty
                    : row.StartingLevel > 1
                        ? Loc.T(L.Jobs.LockedStartsAt, row.StartingLevel)
                        : Loc.T(L.Jobs.LockedBody),
                ProgressText = needed > 0
                    ? Loc.T(L.Jobs.ExpProgress, Grouped(current), Grouped(needed))
                    : string.Empty,
                ToGoText = needed > 0
                    ? Loc.T(L.Jobs.ExpToGo, Grouped(Math.Max(0L, needed - current)), level + 1)
                    : string.Empty,
                PercentText = needed > 0 ? Percent(current, needed) : string.Empty,
                TooltipId = "jobs.tile." + row.Id,
            };
            roleStarts[(int)slot.Role + 1]++;
            summaryLevels[slotIndex] = level;
            summaryCaps[slotIndex] = cap;
        }

        for (var roleIndex = 1; roleIndex < roleStarts.Length; roleIndex++)
        {
            roleStarts[roleIndex] += roleStarts[roleIndex - 1];
        }

        if (activeJobIndex >= 0)
        {
            jobs[activeJobIndex].IsActive = true;
        }

        var gearsetRows = BuildGearsetRows(facts, jobs, gearsets, activeGearsetId, out var activeGearsetIndex);
        AttachGearsets(jobs, gearsetRows);
        var summary = JobsRoster.Summarize(summaryLevels, summaryCaps);
        return new JobsSnapshot
        {
            Jobs = jobs,
            RoleStarts = roleStarts,
            Gearsets = gearsetRows,
            Shelves = BuildShelves(categories, gearsetRows),
            ActiveJobIndex = activeJobIndex,
            ActiveGearsetIndex = activeGearsetIndex,
            RestedExperience = rested,
            RestedText = rested > 0 ? Loc.T(L.Jobs.RestedBonus, Grouped(rested)) : string.Empty,
            Summary = summary,
            CappedText = Loc.T(L.Jobs.OfTotal, Grouped(summary.CappedCount), Grouped(summary.TrackedCount)),
            LevelsText = Loc.T(L.Jobs.OfTotal, Grouped(summary.LevelsEarned), Grouped(summary.LevelsPossible)),
        };
    }

    public static string Grouped(long value) => value.ToString("N0", Loc.Culture);

    private static string Percent(long current, long needed) =>
        (Math.Floor(Math.Clamp(current / (double)needed, 0d, 1d) * 100d) / 100d).ToString("P0", Loc.Culture);


    private static int LevelAt(Span<short> values, sbyte index) =>
        index >= 0 && index < values.Length ? values[index] : 0;

    private static int LevelAt(Span<int> values, sbyte index) =>
        index >= 0 && index < values.Length ? values[index] : 0;

    private static bool IsSameDiscipline(ClassJobFacts[] facts, in ClassJobFacts row, uint classJobId)
    {
        if (classJobId == row.Id)
        {
            return true;
        }

        for (var index = 0; index < facts.Length; index++)
        {
            if (facts[index].Id == classJobId)
            {
                return facts[index].ExpArrayIndex == row.ExpArrayIndex;
            }
        }

        return false;
    }

    private static List<RawGearset> ReadGearsets(GameData gameData, out int activeGearsetId)
    {
        activeGearsetId = -1;
        var result = new List<RawGearset>(32);
        var module = RaptureGearsetModule.Instance();
        if (module is null)
        {
            return result;
        }

        activeGearsetId = module->CurrentGearsetIndex;
        var entries = module->Entries;
        for (var index = 0; index < entries.Length && result.Count < MaxGearsets; index++)
        {
            var entry = entries[index];
            if ((entry.Flags & RaptureGearsetModule.GearsetFlag.Exists) == 0 || entry.ClassJob == 0)
            {
                continue;
            }

            var classJobId = (uint)entry.ClassJob;
            var name = entry.NameString;
            if (name.Length == 0)
            {
                name = gameData.JobName(classJobId);
            }

            result.Add(new RawGearset(entry.Id, classJobId, name, entry.ItemLevel,
                (entry.Flags & RaptureGearsetModule.GearsetFlag.MainHandMissing) != 0));
        }

        return result;
    }

    private static bool HasGearsetFor(List<RawGearset> gearsets, uint classJobId)
    {
        for (var index = 0; index < gearsets.Count; index++)
        {
            if (gearsets[index].ClassJobId == classJobId)
            {
                return true;
            }
        }

        return false;
    }

    private static GearsetRow[] BuildGearsetRows(ClassJobFacts[] facts, JobRow[] jobs, List<RawGearset> gearsets,
        int activeGearsetId, out int activeGearsetIndex)
    {
        activeGearsetIndex = -1;
        var rows = new GearsetRow[gearsets.Count];
        for (var index = 0; index < gearsets.Count; index++)
        {
            var raw = gearsets[index];
            var abbreviation = string.Empty;
            var jobIndex = JobIndexFor(facts, jobs, raw.ClassJobId);
            if (jobIndex >= 0)
            {
                abbreviation = jobs[jobIndex].Abbreviation;
            }

            var isActive = raw.Id == activeGearsetId;
            if (isActive)
            {
                activeGearsetIndex = index;
            }

            rows[index] = new GearsetRow
            {
                Id = raw.Id,
                ClassJobId = raw.ClassJobId,
                JobIndex = jobIndex,
                Name = raw.Name,
                Abbreviation = abbreviation,
                ItemLevel = raw.ItemLevel,
                IsActive = isActive,
                MainHandMissing = raw.MainHandMissing,
                IconId = GameData.JobIconId(raw.ClassJobId),
                ItemLevelText = Loc.T(L.Jobs.ItemLevel, raw.ItemLevel),
                SwitchText = Loc.T(L.Jobs.SwitchTo, raw.Name),
                PressId = "jobs.gearset." + raw.Id,
            };
        }

        return rows;
    }

    private static int JobIndexFor(ClassJobFacts[] facts, JobRow[] jobs, uint classJobId)
    {
        for (var index = 0; index < jobs.Length; index++)
        {
            if (jobs[index].ClassJobId == classJobId)
            {
                return index;
            }
        }

        var expArrayIndex = (sbyte)-1;
        for (var index = 0; index < facts.Length; index++)
        {
            if (facts[index].Id == classJobId)
            {
                expArrayIndex = facts[index].ExpArrayIndex;
                break;
            }
        }

        if (expArrayIndex < 0)
        {
            return -1;
        }

        for (var index = 0; index < jobs.Length; index++)
        {
            if (jobs[index].IsLocked)
            {
                continue;
            }

            for (var factIndex = 0; factIndex < facts.Length; factIndex++)
            {
                if (facts[factIndex].Id == jobs[index].ClassJobId && facts[factIndex].ExpArrayIndex == expArrayIndex)
                {
                    return index;
                }
            }
        }

        return -1;
    }

    private static void AttachGearsets(JobRow[] jobs, GearsetRow[] gearsets)
    {
        Span<int> counts = stackalloc int[jobs.Length];
        for (var index = 0; index < gearsets.Length; index++)
        {
            if (gearsets[index].JobIndex >= 0)
            {
                counts[gearsets[index].JobIndex]++;
            }
        }

        for (var jobIndex = 0; jobIndex < jobs.Length; jobIndex++)
        {
            if (counts[jobIndex] == 0)
            {
                continue;
            }

            var indices = new int[counts[jobIndex]];
            var filled = 0;
            for (var index = 0; index < gearsets.Length; index++)
            {
                if (gearsets[index].JobIndex == jobIndex)
                {
                    indices[filled] = index;
                    filled++;
                }
            }

            SortByItemLevel(gearsets, indices);
            jobs[jobIndex].GearsetIndices = indices;
        }
    }

    private static void SortByItemLevel(GearsetRow[] gearsets, int[] indices)
    {
        for (var outer = 1; outer < indices.Length; outer++)
        {
            var moving = indices[outer];
            var position = outer - 1;
            while (position >= 0 && gearsets[indices[position]].ItemLevel < gearsets[moving].ItemLevel)
            {
                indices[position + 1] = indices[position];
                position--;
            }

            indices[position + 1] = moving;
        }
    }


    private static JobShelf[] BuildShelves(IReadOnlyList<JobsCategory> categories, GearsetRow[] gearsets)
    {
        if (categories.Count == 0)
        {
            return Array.Empty<JobShelf>();
        }

        Span<int> available = stackalloc int[gearsets.Length];
        for (var index = 0; index < gearsets.Length; index++)
        {
            available[index] = gearsets[index].Id;
        }

        Span<int> ordered = stackalloc int[gearsets.Length];
        var shelves = new JobShelf[categories.Count];
        for (var categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++)
        {
            var count = JobsRoster.OrderedByIds(available, categories[categoryIndex].GearsetIds, ordered);
            var indices = new int[count];
            for (var index = 0; index < count; index++)
            {
                for (var gearsetIndex = 0; gearsetIndex < gearsets.Length; gearsetIndex++)
                {
                    if (gearsets[gearsetIndex].Id == ordered[index])
                    {
                        indices[index] = gearsetIndex;
                        break;
                    }
                }
            }

            shelves[categoryIndex] = new JobShelf
            {
                CategoryIndex = categoryIndex,
                Title = categories[categoryIndex].Name,
                GearsetIndices = indices,
            };
        }

        return shelves;
    }

    private readonly record struct RawGearset(int Id, uint ClassJobId, string Name, int ItemLevel,
        bool MainHandMissing);
}
