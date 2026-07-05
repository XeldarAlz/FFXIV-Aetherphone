namespace Aetherphone.Core.Lodestone;

internal enum LookupKind : byte
{
    Character,
    FreeCompany,
}

internal enum LookupState : byte
{
    Idle,
    Loading,
    Ready,
    Empty,
    Failed,
}

internal sealed class CharacterMatch
{
    public CharacterMatch(string id, string name, string world)
    {
        Id = id;
        Name = name;
        World = world;
    }

    public string Id { get; }
    public string Name { get; }
    public string World { get; }
}

internal sealed class FreeCompanyMatch
{
    public FreeCompanyMatch(string id, string name, string world, string subtitle, bool recruiting, Uri? crest,
        string crestKey)
    {
        Id = id;
        Name = name;
        World = world;
        Subtitle = subtitle;
        Recruiting = recruiting;
        Crest = crest;
        CrestKey = crestKey;
    }

    public string Id { get; }
    public string Name { get; }
    public string World { get; }
    public string Subtitle { get; }
    public bool Recruiting { get; }
    public Uri? Crest { get; }
    public string CrestKey { get; }
}

internal enum JobCategory : byte
{
    Combat,
    Crafter,
    Gatherer,
}

internal sealed class ClassJobLevel
{
    public ClassJobLevel(string name, int level, string levelLabel, JobCategory category)
    {
        Name = name;
        Level = level;
        LevelLabel = levelLabel;
        Category = category;
    }

    public string Name { get; }
    public int Level { get; }
    public string LevelLabel { get; }
    public JobCategory Category { get; }
}

internal sealed class GearPiece
{
    public GearPiece(string itemName, string itemLevelLabel)
    {
        ItemName = itemName;
        ItemLevelLabel = itemLevelLabel;
    }

    public string ItemName { get; }
    public string ItemLevelLabel { get; }
}

internal sealed class CharacterDetail
{
    public CharacterDetail(string id, string name, string title, string world, string raceClan, string grandCompany,
        string freeCompany, Uri? portrait, string portraitKey, IReadOnlyList<ClassJobLevel> jobs,
        IReadOnlyList<GearPiece> gear)
    {
        Id = id;
        Name = name;
        Title = title;
        World = world;
        RaceClan = raceClan;
        GrandCompany = grandCompany;
        FreeCompany = freeCompany;
        Portrait = portrait;
        PortraitKey = portraitKey;
        Jobs = jobs;
        Gear = gear;
    }

    public string Id { get; }
    public string Name { get; }
    public string Title { get; }
    public string World { get; }
    public string RaceClan { get; }
    public string GrandCompany { get; }
    public string FreeCompany { get; }
    public Uri? Portrait { get; }
    public string PortraitKey { get; }
    public IReadOnlyList<ClassJobLevel> Jobs { get; }
    public IReadOnlyList<GearPiece> Gear { get; }
}

internal sealed class RosterMember
{
    public RosterMember(string id, string name, string subtitle, string world, Uri? avatar, string avatarKey)
    {
        Id = id;
        Name = name;
        Subtitle = subtitle;
        World = world;
        Avatar = avatar;
        AvatarKey = avatarKey;
    }

    public string Id { get; }
    public string Name { get; }
    public string Subtitle { get; }
    public string World { get; }
    public Uri? Avatar { get; }
    public string AvatarKey { get; }
}

internal sealed class RosterSnapshot
{
    public static readonly RosterSnapshot Empty = new(Array.Empty<RosterMember>(), 0, 0);

    public RosterSnapshot(RosterMember[] members, int page, int pageCount)
    {
        Members = members;
        Page = page;
        PageCount = pageCount;
    }

    public RosterMember[] Members { get; }
    public int Page { get; }
    public int PageCount { get; }
}

internal sealed class FreeCompanyDetail
{
    public FreeCompanyDetail(string id, string name, string tag, string heading, string world, string slogan,
        string membersLabel, bool recruiting, Uri? crest, string crestKey)
    {
        Id = id;
        Name = name;
        Tag = tag;
        Heading = heading;
        World = world;
        Slogan = slogan;
        MembersLabel = membersLabel;
        Recruiting = recruiting;
        Crest = crest;
        CrestKey = crestKey;
    }

    public string Id { get; }
    public string Name { get; }
    public string Tag { get; }
    public string Heading { get; }
    public string World { get; }
    public string Slogan { get; }
    public string MembersLabel { get; }
    public bool Recruiting { get; }
    public Uri? Crest { get; }
    public string CrestKey { get; }
}
