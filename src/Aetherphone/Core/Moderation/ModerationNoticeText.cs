using System.Text;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Social;

namespace Aetherphone.Core.Moderation;

internal static class ModerationNoticeKinds
{
    public const int ContentRemoved = 0;
    public const int Warning = 1;
    public const int ProfileMediaRemoved = 2;
    public const int ProfileTextCleared = 3;
    public const int Suspended = 4;
    public const int SignedOut = 5;
    public const int ReportOutcome = 6;
    public const int BadgeGranted = 7;
    public const int BadgeRevoked = 8;
}

internal static class ModerationNoticeText
{
    private static BadgeCatalogStore? badgeCatalog;

    public static void Configure(BadgeCatalogStore catalog)
    {
        badgeCatalog = catalog;
    }

    public static void Reset()
    {
        badgeCatalog = null;
    }

    public static bool IsBlocking(ModerationNoticeDto notice)
    {
        return notice.Kind != ModerationNoticeKinds.ReportOutcome
            && notice.Kind != ModerationNoticeKinds.BadgeGranted
            && notice.Kind != ModerationNoticeKinds.BadgeRevoked;
    }

    public static string Title(ModerationNoticeDto notice)
    {
        return notice.Kind switch
        {
            ModerationNoticeKinds.ContentRemoved => Loc.T(RemovedTitleFor(notice)),
            ModerationNoticeKinds.Warning => Loc.T(L.Moderation.WarningTitle),
            ModerationNoticeKinds.ProfileMediaRemoved => Loc.T(L.Moderation.NoticeAvatarRemoved),
            ModerationNoticeKinds.ProfileTextCleared => Loc.T(L.Moderation.NoticeProfileCleared),
            ModerationNoticeKinds.Suspended => Loc.T(L.Moderation.NoticeSuspendedTitle),
            ModerationNoticeKinds.SignedOut => Loc.T(L.Moderation.NoticeSignedOutTitle),
            ModerationNoticeKinds.BadgeGranted => Loc.T(L.Moderation.NoticeBadgeTitle),
            ModerationNoticeKinds.BadgeRevoked => Loc.T(L.Moderation.NoticeBadgeRevokedTitle),
            _ => Loc.T(L.Moderation.NoticeThanksTitle),
        };
    }

    public static string Body(ModerationNoticeDto notice)
    {
        if (notice.Kind == ModerationNoticeKinds.ReportOutcome)
        {
            return Loc.T(L.Moderation.NoticeThanksBody);
        }

        if (notice.Kind == ModerationNoticeKinds.BadgeGranted || notice.Kind == ModerationNoticeKinds.BadgeRevoked)
        {
            return BadgeBody(notice);
        }

        if (notice.Kind == ModerationNoticeKinds.SignedOut)
        {
            return Loc.T(L.Moderation.NoticeSignedOutBody);
        }

        var body = new StringBuilder();
        AppendRule(body, notice);

        if (notice.Kind == ModerationNoticeKinds.Suspended)
        {
            Append(body, notice.BanUntilUnix is { } until
                ? Loc.T(L.Moderation.NoticeSuspendedFor, LiftMoment(until))
                : Loc.T(L.Moderation.NoticeSuspendedPermanent));
        }

        if (notice.Kind == ModerationNoticeKinds.ProfileTextCleared && notice.Detail.Length > 0)
        {
            Append(body, Loc.T(L.Moderation.NoticeProfileClearedFields, notice.Detail));
        }

        AppendQuote(body, notice);

        if (notice.ModeratorNote.Length > 0)
        {
            Append(body, Loc.T(L.Moderation.NoticeModeratorNote, notice.ModeratorNote));
        }

        if (notice.Kind == ModerationNoticeKinds.Warning)
        {
            Append(body, Loc.T(L.Moderation.NoticeWarningConsequence));
        }

        Append(body, Loc.T(L.Moderation.RemovedFooter));
        return body.ToString();
    }

    private static string BadgeBody(ModerationNoticeDto notice)
    {
        var revoked = notice.Kind == ModerationNoticeKinds.BadgeRevoked;
        var names = BadgeNames(notice.Detail);
        if (names.Count == 0)
        {
            return Loc.T(revoked ? L.Moderation.NoticeBadgeRevokedBodyFallback : L.Moderation.NoticeBadgeBodyFallback);
        }

        if (names.Count == 1)
        {
            return Loc.T(revoked ? L.Moderation.NoticeBadgeRevokedBodyOne : L.Moderation.NoticeBadgeBodyOne, names[0]);
        }

        return Loc.T(revoked ? L.Moderation.NoticeBadgeRevokedBodyMany : L.Moderation.NoticeBadgeBodyMany,
            string.Join(", ", names));
    }

    private static List<string> BadgeNames(string detail)
    {
        var keys = detail.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var names = new List<string>(keys.Length);
        for (var index = 0; index < keys.Length; index++)
        {
            if (BadgeNameFor(keys[index]) is { } name)
            {
                names.Add(Loc.T(name));
            }
            else if (badgeCatalog?.Find(keys[index]) is { } style)
            {
                names.Add(style.Name);
            }
        }

        return names;
    }

    private static LocString? BadgeNameFor(string key)
    {
        return key switch
        {
            "management" => L.Social.RoleManagement,
            "moderator" => L.Social.RoleModerator,
            "patreon" => L.Social.RolePatreon,
            "developer" => L.Social.RoleDeveloper,
            "supporter" => L.Social.RoleSupport,
            "contributor" => L.Social.RoleContributor,
            "aide" => L.Social.RoleAide,
            "aurelia" => L.Social.RoleAurelia,
            "verified" => L.Social.RoleVerified,
            _ => null,
        };
    }

    private static void AppendRule(StringBuilder body, ModerationNoticeDto notice)
    {
        if (notice.RuleTitle.Length == 0)
        {
            Append(body, ContentModeration.RemovalMessage(notice.ReasonCode));
            return;
        }

        Append(body, notice.RuleSummary.Length > 0
            ? notice.RuleTitle + ": " + notice.RuleSummary
            : notice.RuleTitle);
    }

    private static void AppendQuote(StringBuilder body, ModerationNoticeDto notice)
    {
        if (notice.ContentExcerpt.Length > 0)
        {
            Append(body, Loc.T(L.Moderation.NoticeQuoted, notice.ContentExcerpt));
            return;
        }

        if (notice.MediaCount > 0)
        {
            Append(body, Loc.T(L.Moderation.NoticeQuotedPhotos,
                Loc.T(L.Moderation.NoticePhotoCount, notice.MediaCount.ToString())));
        }
    }

    public static string LiftMoment(long unixSeconds)
    {
        var local = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime();
        return local.ToString("d MMM", Loc.Culture) + " " + TimeText.Clock(local.DateTime);
    }

    private static void Append(StringBuilder body, string line)
    {
        if (line.Length == 0)
        {
            return;
        }

        if (body.Length > 0)
        {
            body.Append("\n\n");
        }

        body.Append(line);
    }

    private static LocString RemovedTitleFor(ModerationNoticeDto notice)
    {
        switch (notice.ContentType)
        {
            case "comment":
            case "velvet_comment":
                return L.Moderation.NoticeRemovedComment;
            case "story":
                return L.Moderation.NoticeRemovedStory;
            case "velvet_post":
                return L.Moderation.NoticeRemovedVelvetPost;
            case "ad":
                return L.Moderation.NoticeRemovedAd;
            case "muster":
                return L.Moderation.NoticeRemovedMuster;
            case "chat_message":
            case "velvet_message":
            case "gram_message":
            case "ad_message":
                return L.Moderation.NoticeRemovedMessage;
            case "post":
                return notice.App == "aethergram"
                    ? L.Moderation.NoticeRemovedGram
                    : L.Moderation.NoticeRemovedChirp;
            default:
                return L.Moderation.NoticeRemovedContent;
        }
    }
}
