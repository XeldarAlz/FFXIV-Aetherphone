namespace Aetherphone.Core.Changelog;

internal readonly struct ChangelogEntry
{
    public readonly string Version;

    public readonly string Date;

    public readonly IReadOnlyList<string> Highlights;

    public ChangelogEntry(string version, string date, IReadOnlyList<string> highlights)
    {
        Version = version;
        Date = date;
        Highlights = highlights;
    }
}
