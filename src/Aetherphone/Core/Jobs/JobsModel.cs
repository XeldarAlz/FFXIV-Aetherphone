using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Jobs;

internal enum JobRole
{
    Tank,
    Healer,
    Melee,
    PhysicalRanged,
    MagicalRanged,
    Hand,
    Land,
}

internal enum JobEntryKind
{
    Gearset,
    NoGearset,
}

internal sealed class JobEntry
{
    public JobEntry(JobEntryKind kind, int gearsetId, uint classJobId, string abbreviation, string name, int level,
        int itemLevel, uint iconId, bool isActive)
    {
        Kind = kind;
        GearsetId = gearsetId;
        ClassJobId = classJobId;
        Abbreviation = abbreviation;
        Name = name;
        Level = level;
        ItemLevel = itemLevel;
        IconId = iconId;
        IsActive = isActive;
    }

    public JobEntryKind Kind { get; }
    public int GearsetId { get; }
    public uint ClassJobId { get; }
    public string Abbreviation { get; }
    public string Name { get; }
    public int Level { get; }

    /// <summary>-1 when the item level is unknown (a class with no gearset that isn't currently active).</summary>
    public int ItemLevel { get; }
    public uint IconId { get; }
    public bool IsActive { get; }
}

internal sealed class JobRoleSection
{
    public JobRoleSection(JobRole role, LocString title, JobEntry[] entries)
    {
        Role = role;
        Title = title;
        Entries = entries;
    }

    public JobRole Role { get; }
    public LocString Title { get; }
    public JobEntry[] Entries { get; }
}
