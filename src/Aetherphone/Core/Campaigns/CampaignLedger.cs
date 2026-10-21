using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;

namespace Aetherphone.Core.Campaigns;

internal sealed class CampaignLedger
{
    public const int TileEffectNone = 0;
    public const int TileEffectGlow = 1;
    public const int TileEffectAura = 2;
    private const int DefaultReminderLeadMinutes = 30;

    private readonly CampaignDefinition definition;
    private readonly Configuration configuration;
    private readonly Action save;
    private readonly CampaignTexts texts = new();
    private bool pagedDeeper;

    public CampaignLedger(CampaignDefinition definition, Configuration configuration, Action save)
    {
        this.definition = definition;
        this.configuration = configuration;
        this.save = save;
    }

    public CampaignDefinition Definition => definition;

    public CampaignTexts Texts => texts;

    public CampaignDto? Campaign { get; private set; }

    public CampaignPostDto[] Posts { get; private set; } = Array.Empty<CampaignPostDto>();

    public string? PostsCursor { get; private set; }

    public CampaignMeDto? Me { get; private set; }

    public bool LoadedOnce => Campaign is not null;

    public string DisplayName
    {
        get
        {
            var name = Campaign?.Name;
            return string.IsNullOrWhiteSpace(name) ? Loc.T(definition.FallbackName) : name;
        }
    }

    public string Hashtag
    {
        get
        {
            var hashtag = Campaign?.Hashtag;
            return string.IsNullOrWhiteSpace(hashtag) ? definition.FallbackHashtag : hashtag;
        }
    }

    public int ReminderLeadMinutes
    {
        get
        {
            var lead = Campaign?.ReminderLeadMinutes ?? 0;
            return lead > 0 ? lead : DefaultReminderLeadMinutes;
        }
    }

    public bool KeepNight => configuration.LoveIslandKeepNight ?? Campaign?.KeepNightDefault ?? false;

    public int UnreadCount
    {
        get
        {
            var posts = Posts;
            var seenUnix = configuration.LoveIslandPostsSeenUnix;
            var count = 0;
            for (var index = 0; index < posts.Length; index++)
            {
                if (posts[index].PublishedAtUnix > seenUnix)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public bool IsUnread(CampaignPostDto post) => post.PublishedAtUnix > configuration.LoveIslandPostsSeenUnix;

    public bool IsAvailable(bool serverFlag, long nowUnix)
    {
        if (!serverFlag)
        {
            return false;
        }

        return Campaign is null || nowUnix < Campaign.RetiresAtUnix;
    }

    public bool IsRetired(long nowUnix) => Campaign is not null && nowUnix >= Campaign.RetiresAtUnix;

    public int TileEffectLevel(long nowUnix)
    {
        var campaign = Campaign;
        if (campaign is null || nowUnix >= campaign.RetiresAtUnix)
        {
            return TileEffectNone;
        }

        if (configuration.LoveIslandSeenAtUnix < campaign.UpdatedAtUnix)
        {
            return TileEffectAura;
        }

        return nowUnix < campaign.EndsAtUnix ? TileEffectGlow : TileEffectNone;
    }

    public void ApplyCampaign(CampaignDto incoming, List<PhoneNotification> raised)
    {
        var campaign = Normalize(incoming);
        Campaign = campaign;
        texts.Sync(campaign.Texts);
        var known = configuration.LoveIslandKnownExperiences;
        var experiences = campaign.Experiences;
        if (!configuration.LoveIslandMarksInitialized)
        {
            configuration.LoveIslandMarksInitialized = true;
            ReplaceKnownExperiences(known, experiences);
            save();
            return;
        }

        var changed = false;
        for (var index = 0; index < experiences.Length; index++)
        {
            var experience = experiences[index];
            if (Contains(known, experience.Id))
            {
                continue;
            }

            changed = true;
            raised.Add(Notification(Loc.T(L.LoveIsland.NewExperienceNotification, experience.Title),
                CampaignLauncher.ExperiencePrefix + experience.Id));
        }

        if (known.Count != experiences.Length)
        {
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        ReplaceKnownExperiences(known, experiences);
        save();
    }

    public void ApplyPosts(CampaignPostPage page, bool firstPage, List<PhoneNotification> raised)
    {
        var items = page.Items ?? Array.Empty<CampaignPostDto>();
        if (!firstPage)
        {
            pagedDeeper = true;
            Posts = IdentifiedMerge.MergeById(Posts, items, ByNewestFirst);
            PostsCursor = page.NextCursor;
        }
        else if (pagedDeeper)
        {
            Posts = IdentifiedMerge.MergeById(Posts, items, ByNewestFirst);
        }
        else
        {
            Posts = Sorted(items);
            PostsCursor = page.NextCursor;
        }

        if (!firstPage)
        {
            return;
        }

        var newest = NewestUnix(items);
        if (!configuration.LoveIslandPostsInitialized)
        {
            configuration.LoveIslandPostsInitialized = true;
            configuration.LoveIslandPostsNotifiedUnix = newest;
            configuration.LoveIslandPostsSeenUnix = newest;
            save();
            return;
        }

        var notifiedUnix = configuration.LoveIslandPostsNotifiedUnix;
        if (newest <= notifiedUnix)
        {
            return;
        }

        for (var index = items.Length - 1; index >= 0; index--)
        {
            var post = items[index];
            if (post.PublishedAtUnix <= notifiedUnix)
            {
                continue;
            }

            raised.Add(Notification(post.Title, CampaignLauncher.PostPrefix + post.Id));
        }

        configuration.LoveIslandPostsNotifiedUnix = newest;
        save();
    }

    public void ApplyMe(CampaignMeDto me, List<PhoneNotification> raised)
    {
        Me = me;
        if (me.Earned == configuration.LoveIslandEarned)
        {
            return;
        }

        configuration.LoveIslandEarned = me.Earned;
        save();
        if (me.Earned)
        {
            raised.Add(Notification(Loc.T(L.LoveIsland.AwardNotification), CampaignLauncher.PerksKey));
        }
    }

    public void FireDueReminders(long nowUnix, List<PhoneNotification> raised)
    {
        var campaign = Campaign;
        var reminders = configuration.LoveIslandReminders;
        if (campaign is null || reminders.Count == 0)
        {
            return;
        }

        var leadSeconds = ReminderLeadMinutes * 60L;
        var changed = false;
        for (var index = reminders.Count - 1; index >= 0; index--)
        {
            var experience = FindExperience(campaign, reminders[index]);
            if (experience is null)
            {
                reminders.RemoveAt(index);
                changed = true;
                continue;
            }

            if (nowUnix < experience.StartsAtUnix - leadSeconds)
            {
                continue;
            }

            if (nowUnix < experience.StartsAtUnix)
            {
                var minutes = Math.Max(1L, (experience.StartsAtUnix - nowUnix + 59L) / 60L);
                raised.Add(Notification(Loc.T(L.LoveIsland.ReminderNotification, experience.Title, minutes),
                    CampaignLauncher.ExperiencePrefix + experience.Id));
            }

            reminders.RemoveAt(index);
            changed = true;
        }

        if (changed)
        {
            save();
        }
    }

    public bool HasReminder(string experienceId) => Contains(configuration.LoveIslandReminders, experienceId);

    public void SetReminder(string experienceId, bool enabled)
    {
        var reminders = configuration.LoveIslandReminders;
        var present = Contains(reminders, experienceId);
        if (present == enabled)
        {
            return;
        }

        if (enabled)
        {
            reminders.Add(experienceId);
        }
        else
        {
            reminders.Remove(experienceId);
        }

        save();
    }

    public void ClearReminders()
    {
        if (configuration.LoveIslandReminders.Count == 0)
        {
            return;
        }

        configuration.LoveIslandReminders.Clear();
        save();
    }

    public void SetKeepNight(bool enabled)
    {
        if (configuration.LoveIslandKeepNight == enabled)
        {
            return;
        }

        configuration.LoveIslandKeepNight = enabled;
        save();
    }

    public void MarkPostsSeen()
    {
        var newest = NewestUnix(Posts);
        if (newest <= configuration.LoveIslandPostsSeenUnix)
        {
            return;
        }

        configuration.LoveIslandPostsSeenUnix = newest;
        save();
    }

    public void MarkOpened()
    {
        var campaign = Campaign;
        if (campaign is null || configuration.LoveIslandSeenAtUnix >= campaign.UpdatedAtUnix)
        {
            return;
        }

        configuration.LoveIslandSeenAtUnix = campaign.UpdatedAtUnix;
        save();
    }

    public CampaignMemberDto? FindMember(string? memberId)
    {
        var campaign = Campaign;
        if (campaign is null || string.IsNullOrEmpty(memberId))
        {
            return null;
        }

        var members = campaign.Members;
        for (var index = 0; index < members.Length; index++)
        {
            if (string.Equals(members[index].Id, memberId, StringComparison.Ordinal))
            {
                return members[index];
            }
        }

        return null;
    }

    public CampaignPostDto? FindPost(string postId)
    {
        var posts = Posts;
        for (var index = 0; index < posts.Length; index++)
        {
            if (string.Equals(posts[index].Id, postId, StringComparison.Ordinal))
            {
                return posts[index];
            }
        }

        var pinned = Campaign?.PinnedPost;
        return pinned is not null && string.Equals(pinned.Id, postId, StringComparison.Ordinal) ? pinned : null;
    }

    public CampaignPostDto? PinnedPost
    {
        get
        {
            var pinned = Campaign?.PinnedPost;
            if (pinned is not null)
            {
                return pinned;
            }

            var posts = Posts;
            for (var index = 0; index < posts.Length; index++)
            {
                if (posts[index].Pinned)
                {
                    return posts[index];
                }
            }

            return null;
        }
    }

    private static CampaignExperienceDto? FindExperience(CampaignDto campaign, string experienceId)
    {
        var experiences = campaign.Experiences;
        for (var index = 0; index < experiences.Length; index++)
        {
            if (string.Equals(experiences[index].Id, experienceId, StringComparison.Ordinal))
            {
                return experiences[index];
            }
        }

        return null;
    }

    private PhoneNotification Notification(string body, string groupKey) =>
        new(definition.AppId, DisplayName, body, DateTime.Now, AppAccents.For(definition.AppId), groupKey);

    private static CampaignDto Normalize(CampaignDto campaign)
    {
        var experiences = campaign.Experiences ?? Array.Empty<CampaignExperienceDto>();
        var sorted = experiences;
        if (!IsSortedByStart(experiences))
        {
            sorted = (CampaignExperienceDto[])experiences.Clone();
            Array.Sort(sorted, ByStart);
        }

        return campaign with
        {
            Links = campaign.Links ?? Array.Empty<CampaignLinkDto>(),
            Texts = campaign.Texts ?? Array.Empty<CampaignTextDto>(),
            Experiences = sorted,
            Members = campaign.Members ?? Array.Empty<CampaignMemberDto>(),
            Linkshells = campaign.Linkshells ?? Array.Empty<CampaignLinkshellDto>(),
            Reward = campaign.Reward ?? new CampaignRewardDto(null, null, null, null, 0),
        };
    }

    private static bool IsSortedByStart(CampaignExperienceDto[] experiences)
    {
        for (var index = 1; index < experiences.Length; index++)
        {
            if (experiences[index - 1].StartsAtUnix > experiences[index].StartsAtUnix)
            {
                return false;
            }
        }

        return true;
    }

    private static int ByStart(CampaignExperienceDto left, CampaignExperienceDto right)
    {
        var byTime = left.StartsAtUnix.CompareTo(right.StartsAtUnix);
        return byTime != 0 ? byTime : string.CompareOrdinal(left.Id, right.Id);
    }

    private static CampaignPostDto[] Sorted(CampaignPostDto[] items)
    {
        if (items.Length < 2)
        {
            return items;
        }

        var sorted = (CampaignPostDto[])items.Clone();
        Array.Sort(sorted, ByNewestFirst);
        return sorted;
    }

    private static int ByNewestFirst(CampaignPostDto left, CampaignPostDto right)
    {
        var byTime = right.PublishedAtUnix.CompareTo(left.PublishedAtUnix);
        return byTime != 0 ? byTime : string.CompareOrdinal(right.Id, left.Id);
    }

    private static long NewestUnix(CampaignPostDto[] items)
    {
        var newest = 0L;
        for (var index = 0; index < items.Length; index++)
        {
            if (items[index].PublishedAtUnix > newest)
            {
                newest = items[index].PublishedAtUnix;
            }
        }

        return newest;
    }

    private static void ReplaceKnownExperiences(List<string> known, CampaignExperienceDto[] experiences)
    {
        known.Clear();
        for (var index = 0; index < experiences.Length; index++)
        {
            known.Add(experiences[index].Id);
        }
    }

    private static bool Contains(List<string> values, string value)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
