using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Changelog;

internal static class ChangelogData
{
    public static readonly IReadOnlyList<ChangelogEntry> Entries = new[]
    {
        new ChangelogEntry("0.8.4.0", "2026-07-05", L.Changelog.Release0840),
        new ChangelogEntry("0.8.3.0", "2026-07-04", L.Changelog.Release0830),
        new ChangelogEntry("0.8.2.0", "2026-07-04", L.Changelog.Release0820),
        new ChangelogEntry("0.8.1.0", "2026-07-04", L.Changelog.Release0810),
        new ChangelogEntry("0.8.0.0", "2026-07-04", L.Changelog.Release0800),
        new ChangelogEntry("0.7.1.0", "2026-07-03", L.Changelog.Release0710),
        new ChangelogEntry("0.7.0.0", "2026-07-02", L.Changelog.Release0700),
        new ChangelogEntry("0.6.0.0", "2026-06-26", L.Changelog.Release0600),
        new ChangelogEntry("0.5.0.0", "2026-06-25", L.Changelog.Release0500),
        new ChangelogEntry("0.4.0.0", "2026-06-24", L.Changelog.Release0400),
        new ChangelogEntry("0.3.0.0", "2026-06-24", L.Changelog.Release0300),
        new ChangelogEntry("0.2.0.0", "2026-06-23", L.Changelog.Release0200),
        new ChangelogEntry("0.1.3.0", "2026-06-23", L.Changelog.Release0130),
        new ChangelogEntry("0.1.2.0", "2026-06-22", L.Changelog.Release0120),
        new ChangelogEntry("0.1.1.0", "2026-06-21", L.Changelog.Release0110),
        new ChangelogEntry("0.1.0.0", "2026-06-21", L.Changelog.Release0100),
    };
}
