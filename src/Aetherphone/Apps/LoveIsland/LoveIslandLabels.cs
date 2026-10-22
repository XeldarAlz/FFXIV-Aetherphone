using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Campaigns;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Venues;

namespace Aetherphone.Apps.LoveIsland;

internal sealed class ExperienceLabels
{
    public string DayLabel = string.Empty;
    public string StartClock = string.Empty;
    public string EndClock = string.Empty;
    public string Lineup = string.Empty;
    public string RowDetail = string.Empty;
    public string Meta = string.Empty;
    public string LiveLine = string.Empty;
    public string NextLine = string.Empty;
    public string NextUpLine = string.Empty;
    public string FirstUpLine = string.Empty;
    public string DoorsLine = string.Empty;
    public string StreamLine = string.Empty;
    public string Address = string.Empty;
    public string WardPlot = string.Empty;
    public string Command = string.Empty;
    public string Destination = string.Empty;
    public DateTime LocalDay;
    public readonly ClampedLines RowTitle = new();
}

internal sealed class LoveIslandLabels
{
    private const string CommandPrefix = "/li ";
    private const string CopyIdPrefix = "loveisland.syncshell.copyId.";
    private const string CopyPasswordPrefix = "loveisland.syncshell.copyPassword.";

    public string WindowLine = string.Empty;
    public string StartsLine = string.Empty;
    public string HashtagTitle = string.Empty;
    public string SyncshellNames = string.Empty;
    public string LiveChannelLine = string.Empty;
    public string AboutTitle = string.Empty;
    public string ExperiencesSub = string.Empty;
    public string EarnBody = string.Empty;
    public string EarnTitle = string.Empty;
    public string GalleryCta = string.Empty;
    public string RemindMe = string.Empty;
    public string CoinsBody = string.Empty;
    public string CoinsAmount = string.Empty;
    public string WhereLine = string.Empty;
    public ExperienceLabels[] Experiences = Array.Empty<ExperienceLabels>();
    public string[] LinkLabels = Array.Empty<string>();
    public string[] SyncshellServices = Array.Empty<string>();
    public string[] SyncshellCopyIdButtons = Array.Empty<string>();
    public string[] SyncshellCopyPasswordButtons = Array.Empty<string>();

    private readonly Dictionary<string, ExperienceLabels> experienceCache = new(StringComparer.Ordinal);
    private CampaignDto? source;
    private int textsVersion = -1;
    private LanguageInfo? language;
    private int formatVersion = -1;
    private DateTime day;

    public bool Sync(CampaignDto? campaign, CampaignTexts texts, string hashtag)
    {
        var today = DateTime.Now.Date;
        if (ReferenceEquals(campaign, source) && textsVersion == texts.Version && ReferenceEquals(language, Loc.Current)
            && formatVersion == TimeText.FormatVersion && day == today)
        {
            return false;
        }

        source = campaign;
        textsVersion = texts.Version;
        language = Loc.Current;
        formatVersion = TimeText.FormatVersion;
        day = today;
        Rebuild(campaign, texts, hashtag);
        return true;
    }

    private void Rebuild(CampaignDto? campaign, CampaignTexts texts, string hashtag)
    {
        HashtagTitle = "#" + hashtag;
        EarnTitle = texts.T(CampaignTextKeys.EarnTitle, new CampaignTextArgs(Hashtag: hashtag));
        GalleryCta = texts.T(CampaignTextKeys.GalleryCta2, new CampaignTextArgs(Hashtag: hashtag));
        if (campaign is null)
        {
            WindowLine = string.Empty;
            StartsLine = string.Empty;
            SyncshellNames = string.Empty;
            LiveChannelLine = string.Empty;
            AboutTitle = Loc.T(L.LoveIsland.AboutTitle, Loc.T(CampaignDefinition.LoveIsland.FallbackName));
            ExperiencesSub = texts.T(CampaignTextKeys.ExperiencesSub);
            EarnBody = texts.T(CampaignTextKeys.EarnBody, new CampaignTextArgs(Start: string.Empty, End: string.Empty));
            RemindMe = texts.T(CampaignTextKeys.RemindMe, new CampaignTextArgs(N: "30"));
            CoinsBody = string.Empty;
            CoinsAmount = string.Empty;
            WhereLine = string.Empty;
            Experiences = Array.Empty<ExperienceLabels>();
            LinkLabels = Array.Empty<string>();
            SyncshellServices = Array.Empty<string>();
            SyncshellCopyIdButtons = Array.Empty<string>();
            SyncshellCopyPasswordButtons = Array.Empty<string>();
            return;
        }

        var startDay = TimeText.MonthDay(campaign.StartsAtUnix);
        var endDay = TimeText.MonthDay(campaign.EndsAtUnix);
        WindowLine = Loc.T(L.LoveIsland.WindowLine, startDay, endDay, campaign.DataCenter);
        StartsLine = Loc.T(L.LoveIsland.StartsLine, TimeText.FutureDayLabel(campaign.StartsAtUnix),
            TimeText.Clock(campaign.StartsAtUnix));
        LiveChannelLine = string.IsNullOrEmpty(campaign.LiveChannel)
            ? string.Empty
            : Loc.T(L.LoveIsland.LiveChannel, campaign.LiveChannel);
        AboutTitle = Loc.T(L.LoveIsland.AboutTitle, campaign.Name);
        ExperiencesSub = string.Concat(campaign.DataCenter, LoveIslandKit.Separator,
            texts.T(CampaignTextKeys.ExperiencesSub));
        EarnBody = texts.T(CampaignTextKeys.EarnBody, new CampaignTextArgs(Start: TimeText.FutureMoment(campaign.StartsAtUnix),
            End: TimeText.FutureMoment(campaign.EndsAtUnix)));
        var lead = campaign.ReminderLeadMinutes > 0 ? campaign.ReminderLeadMinutes : 30;
        RemindMe = texts.T(CampaignTextKeys.RemindMe, new CampaignTextArgs(N: lead.ToString(Loc.Culture)));
        CoinsAmount = NumberText.Group(campaign.Reward.Coins);
        CoinsBody = texts.T(CampaignTextKeys.CoinsBody, new CampaignTextArgs(N: CoinsAmount));
        WhereLine = Loc.T(L.LoveIsland.Where, campaign.DataCenter);
        RebuildExperiences(campaign);
        RebuildLinks(campaign);
        RebuildSyncshells(campaign);
    }

    private void RebuildExperiences(CampaignDto campaign)
    {
        var experiences = campaign.Experiences;
        var rebuilt = Experiences.Length == experiences.Length ? Experiences : new ExperienceLabels[experiences.Length];
        var streamed = Loc.T(L.LoveIsland.Streamed);
        for (var index = 0; index < experiences.Length; index++)
        {
            var experience = experiences[index];
            if (!experienceCache.TryGetValue(experience.Id, out var labels))
            {
                labels = new ExperienceLabels();
                experienceCache[experience.Id] = labels;
            }

            Fill(labels, campaign, experience, streamed);
            labels.DoorsLine = string.Concat(StartsLine, LoveIslandKit.Separator, labels.FirstUpLine);
            rebuilt[index] = labels;
        }

        Experiences = rebuilt;
    }

    private static void Fill(ExperienceLabels labels, CampaignDto campaign, CampaignExperienceDto experience,
        string streamed)
    {
        labels.DayLabel = TimeText.FutureDayLabel(experience.StartsAtUnix);
        labels.StartClock = TimeText.Clock(experience.StartsAtUnix);
        labels.EndClock = TimeText.Clock(experience.EndsAtUnix);
        labels.LocalDay = experience.StartsAtUnix > 0
            ? DateTimeOffset.FromUnixTimeSeconds(experience.StartsAtUnix).ToLocalTime().Date
            : DateTime.MinValue;
        labels.Lineup = LineupNames(campaign, experience);
        var detail = labels.Lineup.Length > 0
            ? labels.Lineup
            : experience.Spot.Length > 0 ? experience.Spot : experience.District;
        labels.RowDetail = string.IsNullOrEmpty(experience.TwitchUrl)
            ? detail
            : string.Concat(detail, LoveIslandKit.Separator, streamed);
        labels.Meta = Loc.T(L.LoveIsland.TimeRange, labels.DayLabel, labels.StartClock, labels.EndClock);
        var until = Loc.T(L.LoveIsland.UntilClock, labels.EndClock);
        labels.LiveLine = labels.Lineup.Length > 0 ? string.Concat(until, LoveIslandKit.Separator, labels.Lineup) : until;
        labels.NextLine = Loc.T(L.LoveIsland.NextUpAt, experience.Title, labels.StartClock);
        labels.NextUpLine = Loc.T(L.LoveIsland.NextUp, experience.Title);
        labels.FirstUpLine = Loc.T(L.LoveIsland.FirstUp, experience.Title);
        labels.StreamLine = string.Concat(TimeText.FutureMoment(experience.StartsAtUnix), LoveIslandKit.Separator,
            labels.Lineup.Length > 0 ? labels.Lineup : experience.Stage);
        labels.Address = string.Concat(experience.World, LoveIslandKit.Separator, experience.District);
        var wardPlot = Loc.T(L.LoveIsland.WardPlot, experience.Ward, experience.Plot);
        labels.WardPlot = experience.Spot.Length > 0 ? string.Concat(wardPlot, LoveIslandKit.Separator, experience.Spot) : wardPlot;
        labels.Command = LifestreamBridge.HousingCommand(experience.World, LoveIslandKit.StripThe(experience.District),
            experience.Ward, experience.Plot);
        labels.Destination = labels.Command.StartsWith(CommandPrefix, StringComparison.Ordinal)
            ? labels.Command.Substring(CommandPrefix.Length)
            : labels.Command;
    }

    private static string LineupNames(CampaignDto campaign, CampaignExperienceDto experience)
    {
        var ids = experience.LineupMemberIds;
        if (ids is null || ids.Length == 0)
        {
            return string.Empty;
        }

        var names = new string[ids.Length];
        for (var index = 0; index < ids.Length; index++)
        {
            names[index] = MemberName(campaign, ids[index]);
        }

        return string.Join(", ", names);
    }

    private static string MemberName(CampaignDto campaign, string memberId)
    {
        var members = campaign.Members;
        for (var index = 0; index < members.Length; index++)
        {
            if (string.Equals(members[index].Id, memberId, StringComparison.Ordinal))
            {
                return members[index].Name;
            }
        }

        return memberId;
    }

    private void RebuildLinks(CampaignDto campaign)
    {
        var links = campaign.Links;
        var labels = LinkLabels.Length == links.Length ? LinkLabels : new string[links.Length];
        for (var index = 0; index < links.Length; index++)
        {
            labels[index] = LoveIslandKit.LinkLabel(links[index].Kind);
        }

        LinkLabels = labels;
    }

    private void RebuildSyncshells(CampaignDto campaign)
    {
        var syncshells = campaign.Syncshells;
        var count = syncshells.Length;
        var services = SyncshellServices.Length == count ? SyncshellServices : new string[count];
        var copyIds = SyncshellCopyIdButtons.Length == count ? SyncshellCopyIdButtons : new string[count];
        var copyPasswords = SyncshellCopyPasswordButtons.Length == count ? SyncshellCopyPasswordButtons : new string[count];
        var names = new string[count];
        var fallback = Loc.T(L.LoveIsland.Syncshell);
        for (var index = 0; index < count; index++)
        {
            var syncshell = syncshells[index];
            names[index] = syncshell.Name;
            services[index] = string.IsNullOrWhiteSpace(syncshell.Service) ? fallback : syncshell.Service;
            copyIds[index] = CopyIdPrefix + syncshell.Id;
            copyPasswords[index] = CopyPasswordPrefix + syncshell.Id;
        }

        SyncshellServices = services;
        SyncshellCopyIdButtons = copyIds;
        SyncshellCopyPasswordButtons = copyPasswords;
        SyncshellNames = string.Join(", ", names);
    }
}

internal sealed class PostLabels
{
    private readonly Dictionary<string, string> stamps = new(StringComparer.Ordinal);
    private CampaignPostDto[]? source;
    private LanguageInfo? language;
    private int formatVersion = -1;
    private DateTime day;

    public string Stamp(CampaignPostDto post)
    {
        if (stamps.TryGetValue(post.Id, out var stamp))
        {
            return stamp;
        }

        stamp = TimeText.Stamp(post.PublishedAtUnix);
        stamps[post.Id] = stamp;
        return stamp;
    }

    public void Sync(CampaignPostDto[] posts)
    {
        var today = DateTime.Now.Date;
        if (ReferenceEquals(posts, source) && ReferenceEquals(language, Loc.Current)
            && formatVersion == TimeText.FormatVersion && day == today)
        {
            return;
        }

        source = posts;
        language = Loc.Current;
        formatVersion = TimeText.FormatVersion;
        day = today;
        stamps.Clear();
    }
}
