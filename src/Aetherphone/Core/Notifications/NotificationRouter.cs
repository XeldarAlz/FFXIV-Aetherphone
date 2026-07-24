using Aetherphone.Core.Apps;
using Aetherphone.Core.Linkpearl;
using Aetherphone.Core.Muster;

namespace Aetherphone.Core.Notifications;

internal sealed class NotificationRouter
{
    private const string MessagesAppId = "messages";
    private const string DmAppId = "message";
    private const string VelvetAppId = "velvet";
    private const string ChirperAppId = "chirper";
    private const string AethergramAppId = "aethergram";
    private const string MusterAppId = "muster";
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
    private readonly INavigator navigation;
    private readonly NotificationService notifications;
    private readonly LinkpearlLauncher linkpearlLauncher;
    private readonly VelvetLauncher velvetLauncher;
    private readonly DmLauncher dmLauncher;
    private readonly GramDmLauncher gramDmLauncher;
    private readonly SocialLauncher socialLauncher;
    private readonly MusterLauncher musterLauncher;

    public NotificationRouter(INavigator navigation, NotificationService notifications, LinkpearlLauncher linkpearlLauncher,
        VelvetLauncher velvetLauncher, DmLauncher dmLauncher, GramDmLauncher gramDmLauncher, SocialLauncher socialLauncher,
        MusterLauncher musterLauncher)
    {
        this.navigation = navigation;
        this.notifications = notifications;
        this.linkpearlLauncher = linkpearlLauncher;
        this.velvetLauncher = velvetLauncher;
        this.dmLauncher = dmLauncher;
        this.gramDmLauncher = gramDmLauncher;
        this.socialLauncher = socialLauncher;
        this.musterLauncher = musterLauncher;
    }

    public void Open(PhoneNotification notification)
    {
        if (!navigation.IsAvailable(notification.AppId))
        {
            notifications.RemoveGroup(notification.StackKey);
            return;
        }

        notifications.RemoveGroup(notification.StackKey);

        if (notification.AppId == MessagesAppId && !string.IsNullOrEmpty(notification.GroupKey))
        {
            if (LinkshellChannel.TryParse(notification.GroupKey, out var channel))
            {
                linkpearlLauncher.RequestLinkshell(channel, notification.Title);
            }
            else
            {
                linkpearlLauncher.Request(notification.Title, notification.GroupKey);
            }
        }
        else if (notification.AppId == DmAppId && !string.IsNullOrEmpty(notification.GroupKey))
        {
            dmLauncher.RequestConversation(notification.GroupKey);
        }
        else if (notification.AppId == VelvetAppId && !string.IsNullOrEmpty(notification.GroupKey))
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
