using Aetherphone.Core.Apps;
using Aetherphone.Core.Messaging;

namespace Aetherphone.Core.Notifications;

internal sealed class NotificationRouter
{
    private const string MessagesAppId = "messages";

    private const string VelvetAppId = "velvet";

    private readonly INavigator navigation;
    private readonly MessageLauncher messageLauncher;
    private readonly VelvetLauncher velvetLauncher;

    public NotificationRouter(INavigator navigation, MessageLauncher messageLauncher, VelvetLauncher velvetLauncher)
    {
        this.navigation = navigation;
        this.messageLauncher = messageLauncher;
        this.velvetLauncher = velvetLauncher;
    }

    public void Open(PhoneNotification notification)
    {
        if (notification.AppId == MessagesAppId && !string.IsNullOrEmpty(notification.GroupKey))
        {
            messageLauncher.Request(notification.Title, notification.GroupKey);
        }
        else if (notification.AppId == VelvetAppId && !string.IsNullOrEmpty(notification.GroupKey))
        {
            velvetLauncher.Request(notification.GroupKey);
        }

        navigation.Open(notification.AppId);
    }
}
