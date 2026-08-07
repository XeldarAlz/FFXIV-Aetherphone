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
        var rowId = kind == JobEntryKind.Gearset ? "jobs.row.gearset." + gearsetId : "jobs.row.class." + classJobId;
        NameRowId = rowId + ".name";
        SubRowId = rowId + ".sub";
    }

    public JobEntryKind Kind { get; }
    public int GearsetId { get; }
    public uint ClassJobId { get; }
    public string Abbreviation { get; }
    public string Name { get; }
    public int Level { get; }
    public string NameRowId { get; }
    public string SubRowId { get; }

    public int ItemLevel { get; }
    public uint IconId { get; }
    public bool IsActive { get; }
}

internal sealed class JobSection
{
    public JobSection(LocString roleTitle, JobEntry[] entries)
    {
        CategoryIndex = -1;
        RoleTitle = roleTitle;
        CustomTitle = string.Empty;
        Entries = entries;
    }

    public JobSection(int categoryIndex, string customTitle, JobEntry[] entries)
    {
        CategoryIndex = categoryIndex;
        RoleTitle = default;
        CustomTitle = customTitle;
        Entries = entries;
    }

    public int CategoryIndex { get; }

    public LocString RoleTitle { get; }
    public string CustomTitle { get; }
    public JobEntry[] Entries { get; }
    public bool IsCustom => CategoryIndex >= 0;
}
