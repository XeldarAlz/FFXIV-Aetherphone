using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Changelog;

internal static class ChangelogData
{
    public static readonly IReadOnlyList<ChangelogEntry> Entries = new[]
    {
        new ChangelogEntry("1.0.1.9", "2026-08-31", new ChangelogSection[]
        {
            new(L.Changelog.SectionPhone, L.Changelog.Release1019Phone),
            new(L.Apps.Camera, L.Changelog.Release1019Camera),
            new(L.Apps.Hunts, L.Changelog.Release1019Hunts),
            new(L.Apps.Aethergram, L.Changelog.Release1019Aethergram),
            new(L.Apps.Games, L.Changelog.Release1019Games),
        }),
        new ChangelogEntry("1.0.1.8", "2026-08-30", new ChangelogSection[]
        {
            new(L.Apps.Aethergram, L.Changelog.Release1018Aethergram),
            new(L.Apps.Settings, L.Changelog.Release1018Settings),
        }),
        new ChangelogEntry("1.0.1.7", "2026-08-30", new ChangelogSection[]
        {
            new(L.Apps.Aethergram, L.Changelog.Release1017Aethergram),
        }),
        new ChangelogEntry("1.0.1.6", "2026-08-30", new ChangelogSection[]
        {
            new(L.Apps.Aethergram, L.Changelog.Release1016Aethergram),
            new(L.Changelog.SectionMessaging, L.Changelog.Release1016Messaging),
            new(L.Apps.Coin, L.Changelog.Release1016Coin),
            new(L.Apps.Linkpearl, L.Changelog.Release1016Linkpearl),
        }),
        new ChangelogEntry("1.0.1.5", "2026-08-30", L.Changelog.Release1015),
        new ChangelogEntry("1.0.1.4", "2026-08-30", L.Changelog.Release1014),
        new ChangelogEntry("1.0.1.3", "2026-08-29", L.Changelog.Release1013),
        new ChangelogEntry("1.0.1.2", "2026-08-26", L.Changelog.Release1012),
        new ChangelogEntry("1.0.1.1", "2026-08-26", L.Changelog.Release1011),
        new ChangelogEntry("1.0.1.0", "2026-08-24", L.Changelog.Release1010),
        new ChangelogEntry("1.0.0.9", "2026-08-23", L.Changelog.Release1009),
        new ChangelogEntry("1.0.0.8", "2026-08-23", L.Changelog.Release1008),
        new ChangelogEntry("1.0.0.7", "2026-08-19", L.Changelog.Release1007),
        new ChangelogEntry("1.0.0.6", "2026-08-19", L.Changelog.Release1006),
        new ChangelogEntry("1.0.0.5", "2026-08-18", L.Changelog.Release1005),
        new ChangelogEntry("1.0.0.4", "2026-08-18", L.Changelog.Release1004),
        new ChangelogEntry("1.0.0.3", "2026-08-17", L.Changelog.Release1003),
        new ChangelogEntry("1.0.0.2", "2026-08-17", L.Changelog.Release1002),
        new ChangelogEntry("1.0.0.1", "2026-08-17", L.Changelog.Release1001),
        new ChangelogEntry("1.0.0.0", "2026-08-16", L.Changelog.Release1000),
        new ChangelogEntry("0.9.9.9", "2026-08-07", L.Changelog.Release0999),
        new ChangelogEntry("0.9.9.8", "2026-08-06", L.Changelog.Release0998),
        new ChangelogEntry("0.9.9.7", "2026-08-02", L.Changelog.Release0997),
        new ChangelogEntry("0.9.9.6", "2026-08-01", L.Changelog.Release0996),
        new ChangelogEntry("0.9.9.5", "2026-08-01", L.Changelog.Release0995),
        new ChangelogEntry("0.9.9.4", "2026-07-29", L.Changelog.Release0994),
        new ChangelogEntry("0.9.9.3", "2026-07-28", L.Changelog.Release0993),
        new ChangelogEntry("0.9.9.2", "2026-07-26", L.Changelog.Release0992),
        new ChangelogEntry("0.9.9.1", "2026-07-26", L.Changelog.Release0991),
        new ChangelogEntry("0.9.9.0", "2026-07-26", L.Changelog.Release0990),
        new ChangelogEntry("0.9.8.9", "2026-07-22", L.Changelog.Release0989),
        new ChangelogEntry("0.9.8.8", "2026-07-22", L.Changelog.Release0988),
        new ChangelogEntry("0.9.8.7", "2026-07-21", L.Changelog.Release0987),
        new ChangelogEntry("0.9.8.6", "2026-07-21", L.Changelog.Release0986),
        new ChangelogEntry("0.9.8.5", "2026-07-21", L.Changelog.Release0985),
        new ChangelogEntry("0.9.8.4", "2026-07-19", L.Changelog.Release0984),
        new ChangelogEntry("0.9.8.3", "2026-07-19", L.Changelog.Release0983),
        new ChangelogEntry("0.9.8.2", "2026-07-18", L.Changelog.Release0982),
        new ChangelogEntry("0.9.8.1", "2026-07-18", L.Changelog.Release0981),
        new ChangelogEntry("0.9.8.0", "2026-07-18", L.Changelog.Release0980),
        new ChangelogEntry("0.9.7.0", "2026-07-16", L.Changelog.Release0970),
        new ChangelogEntry("0.9.6.0", "2026-07-15", L.Changelog.Release0960),
        new ChangelogEntry("0.9.5.0", "2026-07-12", L.Changelog.Release0950),
        new ChangelogEntry("0.9.4.0", "2026-07-12", L.Changelog.Release0940),
        new ChangelogEntry("0.9.3.1", "2026-07-12", L.Changelog.Release0931),
        new ChangelogEntry("0.9.3.0", "2026-07-11", L.Changelog.Release0930),
        new ChangelogEntry("0.9.2.0", "2026-07-11", L.Changelog.Release0920),
        new ChangelogEntry("0.9.1.0", "2026-07-10", L.Changelog.Release0910),
        new ChangelogEntry("0.9.0.0", "2026-07-08", L.Changelog.Release0900),
        new ChangelogEntry("0.8.7.0", "2026-07-06", L.Changelog.Release0870),
        new ChangelogEntry("0.8.6.0", "2026-07-05", L.Changelog.Release0860),
        new ChangelogEntry("0.8.5.1", "2026-07-05", L.Changelog.Release0851),
        new ChangelogEntry("0.8.5.0", "2026-07-05", L.Changelog.Release0850),
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

    public static string LatestVersion => Entries[0].Version;
}
