using Aetherphone.Core.Analytics;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Messaging;

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
    private readonly INavigator navigation;
    private readonly NotificationService notifications;
    private readonly MessageLauncher messageLauncher;
    private readonly VelvetLauncher velvetLauncher;
    private readonly DmLauncher dmLauncher;
    private readonly SocialLauncher socialLauncher;

    public NotificationRouter(INavigator navigation, NotificationService notifications, MessageLauncher messageLauncher,
        VelvetLauncher velvetLauncher, DmLauncher dmLauncher, SocialLauncher socialLauncher)
    {
        this.navigation = navigation;
        this.notifications = notifications;
        this.messageLauncher = messageLauncher;
        this.velvetLauncher = velvetLauncher;
        this.dmLauncher = dmLauncher;
        this.socialLauncher = socialLauncher;
    }

    public void Open(PhoneNotification notification)
    {
        notifications.RemoveGroup(notification.StackKey);

        if (notification.AppId == MessagesAppId && !string.IsNullOrEmpty(notification.GroupKey))
        {
            if (LinkshellChannel.TryParse(notification.GroupKey, out var channel))
            {
                messageLauncher.RequestLinkshell(channel, notification.Title);
            }
            else
            {
                messageLauncher.Request(notification.Title, notification.GroupKey);
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

        Plugin.Analytics.Track(AnalyticsEvents.NotificationOpened(notification.AppId, notification.GroupKey ?? string.Empty));
        navigation.Open(notification.AppId, AppOpenSource.Notification);
    }

    private static SocialDeepLink? SocialLinkFor(PhoneNotification notification)
    {
        if (notification.AppId is not (ChirperAppId or AethergramAppId or VelvetAppId))
        {
            return null;
        }

        return notification.SocialType switch
        {
            TypeLike or TypeComment or TypeCommentLike when !string.IsNullOrEmpty(notification.PostId)
                => new SocialDeepLink(SocialLinkKind.Post, notification.PostId!),
            TypeFollow or TypeConnectRequest or TypeConnectAccept when !string.IsNullOrEmpty(notification.ActorId)
                => new SocialDeepLink(SocialLinkKind.Profile, notification.ActorId!),
            _ => null,
        };
    }
}
