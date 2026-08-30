using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Changelog;

internal readonly struct ChangelogEntry
{
    public readonly string Version;
    public readonly string Date;
    public readonly IReadOnlyList<LocString> Highlights;
    public readonly IReadOnlyList<ChangelogSection> Sections;

    public ChangelogEntry(string version, string date, IReadOnlyList<LocString> highlights)
    {
        Version = version;
        Date = date;
        Highlights = highlights;
        Sections = Array.Empty<ChangelogSection>();
    }

    public ChangelogEntry(string version, string date, IReadOnlyList<ChangelogSection> sections)
    {
        Version = version;
        Date = date;
        Highlights = Array.Empty<LocString>();
        Sections = sections;
    }
}
