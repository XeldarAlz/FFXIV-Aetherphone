namespace Aetherphone.Core.Aethernet.Contracts;

internal sealed record CampaignDto(
    string Slug,
    string AppId,
    string Name,
    string Tagline,
    string About,
    string AccentHex,
    string? HeroUrl,
    string? LogoUrl,
    string? IconUrl,
    string? IconForegroundUrl,
    string? BadgeArtUrl,
    string? FrameArtUrl,
    CampaignLinkDto[] Links,
    string Hashtag,
    long StartsAtUnix,
    long EndsAtUnix,
    long RetiresAtUnix,
    string DataCenter,
    string? LiveChannel,
    string? LiveTitle,
    bool LiveOnAir,
    bool KeepNightDefault,
    int ReminderLeadMinutes,
    bool WebsitePhotos,
    CampaignRewardDto Reward,
    CampaignTextDto[] Texts,
    CampaignExperienceDto[] Experiences,
    CampaignMemberDto[] Members,
    CampaignSyncshellDto[] Syncshells,
    CampaignPostDto? PinnedPost,
    long UpdatedAtUnix);

internal sealed record CampaignLinkDto(string Kind, string Url);

internal sealed record CampaignRewardDto(string? BadgeId, string? BadgeName, string? FrameId, string? FrameName,
    int Coins);

internal sealed record CampaignTextDto(string Key, string Value);

internal sealed record CampaignExperienceDto(
    string Id,
    string Title,
    string Stage,
    long StartsAtUnix,
    long EndsAtUnix,
    string World,
    string District,
    int Ward,
    int Plot,
    string Spot,
    string[] LineupMemberIds,
    string? BannerUrl,
    string? TwitchUrl) : IIdentified;

internal sealed record CampaignMemberDto(string Id, string Name, string Role, string World, string? PortraitUrl,
    CampaignLinkDto[] Links) : IIdentified;

internal sealed record CampaignSyncshellDto(string Id, string Name, string Service, string SyncshellId,
    string Password, string Blurb) : IIdentified;

internal sealed record CampaignPostDto(string Id, string Title, string Body, string[] ImageUrls, string? LinkUrl,
    bool Pinned, long PublishedAtUnix) : IIdentified;

internal sealed record CampaignPostPage(CampaignPostDto[] Items, string? NextCursor);

internal sealed record CampaignMeDto(bool Earned, long EarnedAtUnix, int PostCount, bool WindowOpen);
