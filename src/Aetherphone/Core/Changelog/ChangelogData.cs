namespace Aetherphone.Core.Changelog;

internal static class ChangelogData
{
    public static readonly IReadOnlyList<ChangelogEntry> Entries = new[]
    {
        new ChangelogEntry("0.7.1.0", "2026-07-03", new[]
        {
            "Added an onboarding tour with coachmarks to guide you through the phone and its apps",
            "Added content reporting to Chirper and Aethergram",
            "Rendered incoming call and notification banners in front of other windows",
            "Localized Phone Calls and filled in missing translations",
        }),
        new ChangelogEntry("0.7.0.0", "2026-07-02", new[]
        {
            "Added Phone app with group voice calls",
            "Added Chirper, an X-style microblog client",
            "Added Aethergram, an Instagram-style photo app",
            "Added Find People, Maps, Collections, and Inventory apps",
            "Added Tetris and Dailies to Games",
            "Redesigned MyCharacter into an Activity-style dashboard and reworked Lodestone sign-in",
        }),
        new ChangelogEntry("0.6.0.0", "2026-06-26", new[]
        {
            "Added Ocean Fishing voyage predictor app",
            "Pointed the Aethernet client at the production backend",
        }),
    };
}
