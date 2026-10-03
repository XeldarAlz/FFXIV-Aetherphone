using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Jobs;

internal enum JobRole : byte
{
    Tank,
    Healer,
    Melee,
    PhysicalRanged,
    MagicalRanged,
    Hand,
    Land,
}

internal static class JobRoles
{
    public const int Count = 7;

    private static readonly LocString[] Titles =
    {
        L.Jobs.SectionTank,
        L.Jobs.SectionHealer,
        L.Jobs.SectionMelee,
        L.Jobs.SectionPhysicalRanged,
        L.Jobs.SectionMagicalRanged,
        L.Jobs.SectionHand,
        L.Jobs.SectionLand,
    };

    public static LocString Title(JobRole role) => Titles[(int)role];

    public static bool IsCombat(JobRole role) => role < JobRole.Hand;
}

internal readonly record struct ClassJobFacts(
    uint Id,
    uint ParentId,
    sbyte ExpArrayIndex,
    byte JobType,
    byte Role,
    uint CategoryId,
    byte UiPriority,
    byte StartingLevel,
    bool IsLimited,
    uint UnlockQuestId);

internal readonly record struct JobSlot(int FactIndex, JobRole Role, bool Locked);

internal sealed class JobRow
{
    public uint ClassJobId { get; init; }
    public JobRole Role { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Abbreviation { get; init; } = string.Empty;
    public uint IconId { get; init; }
    public int Level { get; init; }
    public int Cap { get; init; }
    public long Experience { get; init; }
    public long ExperienceNeeded { get; init; }
    public float Fraction { get; init; }
    public bool IsCapped { get; init; }
    public bool IsLocked { get; init; }
    public bool IsActive { get; set; }
    public byte StartingLevel { get; init; }
    public int[] GearsetIndices { get; set; } = Array.Empty<int>();
    public string LevelText { get; init; } = string.Empty;
    public string LockedText { get; init; } = string.Empty;
    public string ProgressText { get; init; } = string.Empty;
    public string ToGoText { get; init; } = string.Empty;
    public string PercentText { get; init; } = string.Empty;
    public string TooltipId { get; init; } = string.Empty;
    public bool IsCombat => JobRoles.IsCombat(Role);
}

internal sealed class GearsetRow
{
    public int Id { get; init; }
    public uint ClassJobId { get; init; }
    public int JobIndex { get; init; } = -1;
    public string Name { get; init; } = string.Empty;
    public string Abbreviation { get; init; } = string.Empty;
    public int ItemLevel { get; init; }
    public bool IsActive { get; init; }
    public bool MainHandMissing { get; init; }
    public uint IconId { get; init; }
    public string ItemLevelText { get; init; } = string.Empty;
    public string SwitchText { get; init; } = string.Empty;
    public string PressId { get; init; } = string.Empty;
}

internal sealed class JobShelf
{
    public int CategoryIndex { get; init; }
    public string Title { get; init; } = string.Empty;
    public int[] GearsetIndices { get; init; } = Array.Empty<int>();
}

internal sealed class JobsSnapshot
{
    public static readonly JobsSnapshot Empty = new();

    public JobRow[] Jobs { get; init; } = Array.Empty<JobRow>();
    public int[] RoleStarts { get; init; } = new int[JobRoles.Count + 1];
    public GearsetRow[] Gearsets { get; init; } = Array.Empty<GearsetRow>();
    public JobShelf[] Shelves { get; init; } = Array.Empty<JobShelf>();
    public int ActiveJobIndex { get; init; } = -1;
    public int ActiveGearsetIndex { get; init; } = -1;
    public long RestedExperience { get; init; }
    public string RestedText { get; init; } = string.Empty;
    public JobsSummary Summary { get; init; }
    public string CappedText { get; init; } = string.Empty;
    public string LevelsText { get; init; } = string.Empty;
    public bool HasJobs => Jobs.Length > 0;

    public int IndexOfJob(uint classJobId)
    {
        for (var index = 0; index < Jobs.Length; index++)
        {
            if (Jobs[index].ClassJobId == classJobId)
            {
                return index;
            }
        }

        return -1;
    }

    public int IndexOfGearset(int gearsetId)
    {
        for (var index = 0; index < Gearsets.Length; index++)
        {
            if (Gearsets[index].Id == gearsetId)
            {
                return index;
            }
        }

        return -1;
    }
}

internal readonly record struct JobsSummary(int CappedCount, int TrackedCount, int LevelsEarned, int LevelsPossible)
{
    public float CappedFraction => TrackedCount > 0 ? CappedCount / (float)TrackedCount : 0f;
    public float LevelsFraction => LevelsPossible > 0 ? LevelsEarned / (float)LevelsPossible : 0f;
}
