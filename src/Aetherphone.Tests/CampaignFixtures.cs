using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Tests;

internal static class CampaignFixtures
{
    public const long Start = 1_793_419_200L;
    public const long End = Start + 29L * 3600L;
    public const long Retire = End + 47L * 3600L;
    public const long Hour = 3600L;

    public static CampaignExperienceDto Experience(string id, long startsAt, long endsAt, string? twitch = null) =>
        new(id, "Set " + id, "Main island", startsAt, endsAt, "Sephirot", "Mist", 24, 32, "Main hall",
            Array.Empty<string>(), null, twitch);

    public static CampaignDto Campaign(CampaignExperienceDto[]? experiences = null, long updatedAt = Start - 10L * Hour,
        CampaignTextDto[]? texts = null) =>
        new("love-island-eclipse", "loveisland", "Love Island Eclipse", "A Halloween festival", "About text",
            "#cf255b", null, null, null, null, null, null, Array.Empty<CampaignLinkDto>(), "LoveIslandEclipse", Start,
            End, Retire, "Materia (OCE)", null, null, false, false, 30, false,
            new CampaignRewardDto("badge", "Eclipse badge", "frame", "Eclipse frame", 500),
            texts ?? Array.Empty<CampaignTextDto>(),
            experiences ?? new[]
            {
                Experience("doors", Start, Start + Hour),
                Experience("opening", Start + 2L * Hour, Start + 4L * Hour, "https://twitch.tv/example"),
                Experience("midnight", Start + 5L * Hour, Start + 7L * Hour),
            },
            Array.Empty<CampaignMemberDto>(), Array.Empty<CampaignSyncshellDto>(), null, updatedAt);

    public static CampaignPostDto Post(string id, long publishedAt, bool pinned = false) =>
        new(id, "Title " + id, "Body " + id, Array.Empty<string>(), null, pinned, publishedAt);

    public static CampaignPostPage Page(params CampaignPostDto[] posts) => new(posts, null);
}
