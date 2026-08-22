using Aetherphone.Core.Announcements;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Linkpearl;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Hunts;
using Aetherphone.Core.Moderation;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Radio;
using Aetherphone.Core.Video;
using Aetherphone.Core.YellowPages;

namespace Aetherphone.Core.Notifications;

internal sealed class NotificationRouter
{
    private const string MessagesAppId = "messages";
    private const string DmAppId = "message";
    private const string VelvetAppId = "velvet";
    private const string ChirperAppId = "chirper";
    private const string AethergramAppId = "aethergram";
    private const string MusterAppId = "muster";
    private const string YellowPagesAppId = "yellowpages";
    private const string AnnouncementsAppId = "announcements";
    private const string SettingsAppId = "settings";
    private const string MusicAppId = "music";
    private const string CasinoAppId = "casino";
    private const string CasinoGroupPrefix = "casino:";
    private const string AetherStreamAppId = "aetherstream";
    private const string HuntsAppId = "hunts";
    private const int TypeLike = 0;
    private const int TypeComment = 1;
    private const int TypeFollow = 2;
    private const int TypeConnectRequest = 3;
    private const int TypeConnectAccept = 4;
    private const int TypeCommentLike = 6;
    private const int TypeMention = 7;
    private const int TypeCommentMention = 8;
    private const int TypePhotoTag = 9;
    private const int TypeRepost = 12;
    private const int TypeQuote = 13;
    private const int TypeFollowRequest = 14;
    private const int TypeFollowAccept = 15;
    private const int TypeAdInquiry = 19;
    private const int TypeMissedCall = 20;
    private const int TypeRadioLive = 21;
    private const string CallGroupPrefix = "call:";
    private readonly INavigator navigation;
    private readonly NotificationService notifications;
    private readonly SocialNotificationService socialNotifications;
    private readonly LinkpearlLauncher linkpearlLauncher;
    private readonly VelvetLauncher velvetLauncher;
    private readonly DmLauncher dmLauncher;
    private readonly GramDmLauncher gramDmLauncher;
    private readonly SocialLauncher socialLauncher;
    private readonly MusterLauncher musterLauncher;
    private readonly YellowPagesLauncher yellowPagesLauncher;
    private readonly AnnouncementsLauncher announcementsLauncher;
    private readonly SafetyLauncher safetyLauncher;
    private readonly RadioLauncher radioLauncher;
    private readonly CasinoLauncher casinoLauncher;
    private readonly AetherStreamLauncher aetherStreamLauncher;
    private readonly HuntsLauncher huntsLauncher;

    public NotificationRouter(INavigator navigation, NotificationService notifications,
        SocialNotificationService socialNotifications, LinkpearlLauncher linkpearlLauncher,
        VelvetLauncher velvetLauncher, DmLauncher dmLauncher, GramDmLauncher gramDmLauncher, SocialLauncher socialLauncher,
        MusterLauncher musterLauncher, YellowPagesLauncher yellowPagesLauncher,
        AnnouncementsLauncher announcementsLauncher, SafetyLauncher safetyLauncher, RadioLauncher radioLauncher,
        CasinoLauncher casinoLauncher, AetherStreamLauncher aetherStreamLauncher, HuntsLauncher huntsLauncher)
    {
        this.radioLauncher = radioLauncher;
        this.casinoLauncher = casinoLauncher;
        this.aetherStreamLauncher = aetherStreamLauncher;
        this.huntsLauncher = huntsLauncher;
        this.navigation = navigation;
        this.notifications = notifications;
        this.socialNotifications = socialNotifications;
        this.linkpearlLauncher = linkpearlLauncher;
        this.velvetLauncher = velvetLauncher;
        this.dmLauncher = dmLauncher;
        this.gramDmLauncher = gramDmLauncher;
        this.socialLauncher = socialLauncher;
        this.musterLauncher = musterLauncher;
        this.yellowPagesLauncher = yellowPagesLauncher;
        this.announcementsLauncher = announcementsLauncher;
        this.safetyLauncher = safetyLauncher;
    }

    public void AcknowledgeAll() => socialNotifications.AcknowledgeAll();

    public void Acknowledge(PhoneNotification notification)
    {
        if (notification.SocialType < 0)
        {
            return;
        }

        socialNotifications.AcknowledgeUpTo(notification.AppId, notification.CreatedAtUnix);
    }

    public void Open(PhoneNotification notification)
    {
        if (notification.SocialType >= 0)
        {
            socialNotifications.AcknowledgeUpTo(notification.AppId, notification.CreatedAtUnix);
        }

        if (!navigation.IsAvailable(notification.AppId))
        {
            notifications.RemoveGroup(notification.StackKey);
            return;
        }

        notifications.RemoveGroup(notification.StackKey);

        if (notification.AppId == MessagesAppId && !string.IsNullOrEmpty(notification.GroupKey))
        {
            linkpearlLauncher.Request(notification.GroupKey);
        }
        else if (notification.AppId == DmAppId && notification.GroupKey is { } dmKey
                 && dmKey.StartsWith(CallGroupPrefix, StringComparison.Ordinal))
        {
            dmLauncher.RequestCalls();
        }
        else if (notification.AppId == DmAppId && notification.SocialType < 0
                 && !string.IsNullOrEmpty(notification.GroupKey))
        {
            dmLauncher.RequestConversation(notification.GroupKey);
        }
        else if (notification.AppId == VelvetAppId && notification.SocialType < 0
                 && !string.IsNullOrEmpty(notification.GroupKey))
        {
            velvetLauncher.Request(notification.GroupKey);
        }
        else if (notification.AppId == AethergramAppId && notification.SocialType < 0
                 && !string.IsNullOrEmpty(notification.GroupKey))
        {
            gramDmLauncher.Request(notification.GroupKey);
        }
        else if (notification.AppId == MusterAppId && !string.IsNullOrEmpty(notification.GroupKey))
        {
            musterLauncher.RequestDetail(notification.GroupKey);
        }
        else if (notification.AppId == YellowPagesAppId && notification.SocialType == TypeAdInquiry
                 && !string.IsNullOrEmpty(notification.GroupKey))
        {
            yellowPagesLauncher.RequestInquiry(notification.GroupKey);
        }
        else if (notification.AppId == YellowPagesAppId && !string.IsNullOrEmpty(notification.GroupKey))
        {
            yellowPagesLauncher.RequestDetail(notification.GroupKey);
        }
        else if (notification.AppId == AnnouncementsAppId && !string.IsNullOrEmpty(notification.GroupKey))
        {
            announcementsLauncher.RequestDetail(notification.GroupKey);
        }
        else if (notification.AppId == MusicAppId && notification.SocialType == TypeRadioLive
                 && !string.IsNullOrEmpty(notification.PostId))
        {
            radioLauncher.RequestStation(notification.PostId!);
        }
        else if (notification.AppId == CasinoAppId && notification.GroupKey is { } tableKey
                 && tableKey.StartsWith(CasinoGroupPrefix, StringComparison.Ordinal))
        {
            casinoLauncher.RequestTable(tableKey[CasinoGroupPrefix.Length..]);
        }
        else if (notification.AppId == AetherStreamAppId && notification.GroupKey == StreamSuggestionNotifier.GroupKey)
        {
            aetherStreamLauncher.RequestUpNext();
        }
        else if (notification.AppId == HuntsAppId && notification.GroupKey is { } huntsKey
                 && HuntsService.TryParseGroupKey(huntsKey, out var mobId, out var worldId, out var zoneInstance))
        {
            huntsLauncher.RequestDetail(mobId, worldId, zoneInstance);
        }
        else if (notification.AppId == SettingsAppId)
        {
            safetyLauncher.Request();
        }
        else if (SocialLinkFor(notification) is { } link)
        {
            socialLauncher.Request(notification.AppId, link);
        }

        navigation.Open(notification.AppId);
    }

    private static SocialDeepLink? SocialLinkFor(PhoneNotification notification)
    {
        if (notification.AppId is not (ChirperAppId or AethergramAppId or VelvetAppId))
        {
            return null;
        }

        return notification.SocialType switch
        {
            TypeLike or TypeComment or TypeCommentLike or TypeMention or TypeCommentMention or TypePhotoTag
                or TypeRepost or TypeQuote
                when !string.IsNullOrEmpty(notification.PostId)
                => new SocialDeepLink(SocialLinkKind.Post, notification.PostId!),
            TypeFollow or TypeConnectRequest or TypeConnectAccept or TypeFollowAccept
                when !string.IsNullOrEmpty(notification.ActorId)
                => new SocialDeepLink(SocialLinkKind.Profile, notification.ActorId!),
            TypeFollowRequest => new SocialDeepLink(SocialLinkKind.Requests, notification.ActorId ?? string.Empty),
            _ => null,
        };
    }
}
