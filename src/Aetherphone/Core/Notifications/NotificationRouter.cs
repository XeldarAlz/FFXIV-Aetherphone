using Aetherphone.Core.Apps;
using Aetherphone.Core.Linkpearl;

namespace Aetherphone.Core.Notifications;

internal sealed class NotificationRouter
{
    private const string MessagesAppId = "messages";
    private const string DmAppId = "message";
    private const string VelvetAppId = "velvet";
    private const string ChirperAppId = "chirper";
    private const string AethergramAppId = "aethergram";
    private const int TypeLike = 0;
    private const int TypeComment = 1;
    private const int TypeFollow = 2;
    private const int TypeConnectRequest = 3;
    private const int TypeConnectAccept = 4;
    private const int TypeCommentLike = 6;
    private const int TypeMention = 7;
    private const int TypeCommentMention = 8;
    private const int TypePhotoTag = 9;
    private readonly INavigator navigation;
    private readonly NotificationService notifications;
    private readonly LinkpearlLauncher linkpearlLauncher;
    private readonly VelvetLauncher velvetLauncher;
    private readonly DmLauncher dmLauncher;
    private readonly SocialLauncher socialLauncher;

    public NotificationRouter(INavigator navigation, NotificationService notifications, LinkpearlLauncher linkpearlLauncher,
        VelvetLauncher velvetLauncher, DmLauncher dmLauncher, SocialLauncher socialLauncher)
    {
        this.navigation = navigation;
        this.notifications = notifications;
        this.linkpearlLauncher = linkpearlLauncher;
        this.velvetLauncher = velvetLauncher;
        this.dmLauncher = dmLauncher;
        this.socialLauncher = socialLauncher;
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
                when !string.IsNullOrEmpty(notification.PostId)
                => new SocialDeepLink(SocialLinkKind.Post, notification.PostId!),
            TypeFollow or TypeConnectRequest or TypeConnectAccept when !string.IsNullOrEmpty(notification.ActorId)
                => new SocialDeepLink(SocialLinkKind.Profile, notification.ActorId!),
            _ => null,
        };
    }
}
