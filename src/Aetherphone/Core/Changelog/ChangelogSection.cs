using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Changelog;

internal readonly struct ChangelogSection
{
    public readonly LocString Title;
    public readonly IReadOnlyList<LocString> Highlights;

    public ChangelogSection(LocString title, IReadOnlyList<LocString> highlights)
    {
        Title = title;
        Highlights = highlights;
    }
}
