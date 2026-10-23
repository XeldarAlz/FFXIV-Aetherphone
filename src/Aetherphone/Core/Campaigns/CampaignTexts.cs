using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;

namespace Aetherphone.Core.Campaigns;

internal readonly record struct CampaignTextArgs(string? N = null, string? Name = null, string? Hashtag = null,
    string? Start = null, string? End = null);

internal static class CampaignTextKeys
{
    public const string TabTonight = "tabTonight";
    public const string TabExperiences = "tabExperiences";
    public const string TabGallery = "tabGallery";
    public const string TabPerks = "tabPerks";
    public const string DoorsOpenIn = "doorsOpenIn";
    public const string LiveNow = "liveNow";
    public const string BetweenSets = "betweenSets";
    public const string EndedTitle = "endedTitle";
    public const string EndedBody = "endedBody";
    public const string KeepNight = "keepNight";
    public const string KeepNightHintLive = "keepNightHintLive";
    public const string KeepNightHintBefore = "keepNightHintBefore";
    public const string LiveDj = "liveDj";
    public const string NoStream = "noStream";
    public const string StreamHint = "streamHint";
    public const string WatchTwitch = "watchTwitch";
    public const string ShareTitle = "shareTitle";
    public const string ShareBody = "shareBody";
    public const string WebsiteNote = "websiteNote";
    public const string PostCta = "postCta";
    public const string GalleryCta = "galleryCta";
    public const string Pinned = "pinned";
    public const string Announcements = "announcements";
    public const string AnnouncementsFrom = "announcementsFrom";
    public const string AnnouncementsBody = "announcementsBody";
    public const string SyncshellsTitle = "syncshellsTitle";
    public const string SyncshellsBody = "syncshellsBody";
    public const string About = "about";
    public const string More = "more";
    public const string AboutFooter = "aboutFooter";
    public const string ExperiencesSub = "experiencesSub";
    public const string NothingScheduled = "nothingScheduled";
    public const string WhoIsOn = "whoIsOn";
    public const string RemindMe = "remindMe";
    public const string DcTravelNote = "dcTravelNote";
    public const string GallerySub = "gallerySub";
    public const string GalleryCta2 = "galleryCta2";
    public const string PerksSub = "perksSub";
    public const string FrameName = "frameName";
    public const string FrameBody = "frameBody";
    public const string BadgeName = "badgeName";
    public const string BadgeBody = "badgeBody";
    public const string CoinsName = "coinsName";
    public const string CoinsBody = "coinsBody";
    public const string HowToEarn = "howToEarn";
    public const string EarnTitle = "earnTitle";
    public const string EarnBody = "earnBody";
    public const string EquipCta = "equipCta";
    public const string Earned = "earned";
    public const string NotYet = "notYet";
}

internal sealed class CampaignTexts
{
    private static readonly string[] PositionalTokens = { "{0}", "{1}", "{2}", "{3}", "{4}" };

    private readonly Dictionary<string, string> overrides = new(StringComparer.Ordinal);
    private CampaignTextDto[]? source;

    public int Version { get; private set; }

    public void Sync(CampaignTextDto[]? texts)
    {
        if (ReferenceEquals(texts, source))
        {
            return;
        }

        source = texts;
        overrides.Clear();
        if (texts is not null)
        {
            for (var index = 0; index < texts.Length; index++)
            {
                var text = texts[index];
                if (string.IsNullOrEmpty(text.Key) || string.IsNullOrWhiteSpace(text.Value))
                {
                    continue;
                }

                overrides[text.Key] = text.Value;
            }
        }

        Version++;
    }

    public string T(string key) => overrides.TryGetValue(key, out var value) ? value : Loc.T(Default(key));

    public string T(string key, in CampaignTextArgs args) => Fill(T(key), in args);

    public static string Fill(string template, in CampaignTextArgs args)
    {
        if (template.IndexOf('{') < 0)
        {
            return template;
        }

        var positional = 0;
        var result = Apply(template, "{n}", args.N, ref positional);
        result = Apply(result, "{name}", args.Name, ref positional);
        result = Apply(result, "{hashtag}", args.Hashtag, ref positional);
        result = Apply(result, "{start}", args.Start, ref positional);
        return Apply(result, "{end}", args.End, ref positional);
    }

    private static string Apply(string text, string token, string? value, ref int positional)
    {
        if (value is null)
        {
            return text;
        }

        var replaced = text.Replace(token, value, StringComparison.Ordinal);
        var slot = positional++;
        return slot < PositionalTokens.Length
            ? replaced.Replace(PositionalTokens[slot], value, StringComparison.Ordinal)
            : replaced;
    }

    public static LocString Default(string key) => key switch
    {
        CampaignTextKeys.TabTonight => L.LoveIsland.TabTonight,
        CampaignTextKeys.TabExperiences => L.LoveIsland.TabExperiences,
        CampaignTextKeys.TabGallery => L.LoveIsland.TabGallery,
        CampaignTextKeys.TabPerks => L.LoveIsland.TabPerks,
        CampaignTextKeys.DoorsOpenIn => L.LoveIsland.DoorsOpenIn,
        CampaignTextKeys.LiveNow => L.LoveIsland.LiveNow,
        CampaignTextKeys.BetweenSets => L.LoveIsland.BetweenSets,
        CampaignTextKeys.EndedTitle => L.LoveIsland.EndedTitle,
        CampaignTextKeys.EndedBody => L.LoveIsland.EndedBody,
        CampaignTextKeys.KeepNight => L.LoveIsland.KeepNight,
        CampaignTextKeys.KeepNightHintLive => L.LoveIsland.KeepNightHintLive,
        CampaignTextKeys.KeepNightHintBefore => L.LoveIsland.KeepNightHintBefore,
        CampaignTextKeys.LiveDj => L.LoveIsland.LiveDj,
        CampaignTextKeys.NoStream => L.LoveIsland.NoStream,
        CampaignTextKeys.StreamHint => L.LoveIsland.StreamHint,
        CampaignTextKeys.WatchTwitch => L.LoveIsland.WatchTwitch,
        CampaignTextKeys.ShareTitle => L.LoveIsland.ShareTitle,
        CampaignTextKeys.ShareBody => L.LoveIsland.ShareBody,
        CampaignTextKeys.WebsiteNote => L.LoveIsland.WebsiteNote,
        CampaignTextKeys.PostCta => L.LoveIsland.PostCta,
        CampaignTextKeys.GalleryCta => L.LoveIsland.GalleryCta,
        CampaignTextKeys.Pinned => L.LoveIsland.Pinned,
        CampaignTextKeys.Announcements => L.LoveIsland.Announcements,
        CampaignTextKeys.AnnouncementsFrom => L.LoveIsland.AnnouncementsFrom,
        CampaignTextKeys.AnnouncementsBody => L.LoveIsland.AnnouncementsBody,
        CampaignTextKeys.SyncshellsTitle => L.LoveIsland.SyncshellsTitle,
        CampaignTextKeys.SyncshellsBody => L.LoveIsland.SyncshellsBody,
        CampaignTextKeys.About => L.LoveIsland.About,
        CampaignTextKeys.More => L.LoveIsland.More,
        CampaignTextKeys.AboutFooter => L.LoveIsland.AboutFooter,
        CampaignTextKeys.ExperiencesSub => L.LoveIsland.ExperiencesSub,
        CampaignTextKeys.NothingScheduled => L.LoveIsland.NothingScheduled,
        CampaignTextKeys.WhoIsOn => L.LoveIsland.WhoIsOn,
        CampaignTextKeys.RemindMe => L.LoveIsland.RemindMe,
        CampaignTextKeys.DcTravelNote => L.LoveIsland.DcTravelNote,
        CampaignTextKeys.GallerySub => L.LoveIsland.GallerySub,
        CampaignTextKeys.GalleryCta2 => L.LoveIsland.GalleryCta2,
        CampaignTextKeys.PerksSub => L.LoveIsland.PerksSub,
        CampaignTextKeys.FrameName => L.LoveIsland.FrameName,
        CampaignTextKeys.FrameBody => L.LoveIsland.FrameBody,
        CampaignTextKeys.BadgeName => L.LoveIsland.BadgeName,
        CampaignTextKeys.BadgeBody => L.LoveIsland.BadgeBody,
        CampaignTextKeys.CoinsName => L.LoveIsland.CoinsName,
        CampaignTextKeys.CoinsBody => L.LoveIsland.CoinsBody,
        CampaignTextKeys.HowToEarn => L.LoveIsland.HowToEarn,
        CampaignTextKeys.EarnTitle => L.LoveIsland.EarnTitle,
        CampaignTextKeys.EarnBody => L.LoveIsland.EarnBody,
        CampaignTextKeys.EquipCta => L.LoveIsland.EquipCta,
        CampaignTextKeys.Earned => L.LoveIsland.Earned,
        CampaignTextKeys.NotYet => L.LoveIsland.NotYet,
        _ => new LocString(key, string.Empty),
    };
}

internal sealed class CampaignTextSlot
{
    private string value = string.Empty;
    private string key = string.Empty;
    private int version = -1;
    private LanguageInfo? language;
    private CampaignTextArgs args;

    public string Get(CampaignTexts texts, string textKey, in CampaignTextArgs textArgs)
    {
        if (version == texts.Version && ReferenceEquals(language, Loc.Current) && args == textArgs
            && string.Equals(key, textKey, StringComparison.Ordinal))
        {
            return value;
        }

        version = texts.Version;
        language = Loc.Current;
        args = textArgs;
        key = textKey;
        value = texts.T(textKey, in textArgs);
        return value;
    }
}
