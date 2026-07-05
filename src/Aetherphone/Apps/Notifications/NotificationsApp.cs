using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Messaging;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Notifications;

internal sealed class NotificationsApp : IPhoneApp
{
    public string Id => "notifications";
    public string DisplayName => Loc.T(L.Apps.Notifications);
    public string Glyph => "N";
    public int BadgeCount => notifications.UnreadCount;
    private readonly NotificationService notifications;
    private readonly MessageLauncher messageLauncher;
    private readonly VelvetLauncher velvetLauncher;
    private NotificationCenter? center;

    public NotificationsApp(NotificationService notifications, MessageLauncher messageLauncher,
        VelvetLauncher velvetLauncher)
    {
        this.notifications = notifications;
        this.messageLauncher = messageLauncher;
        this.velvetLauncher = velvetLauncher;
    }

    public void OnOpened()
    {
        notifications.MarkAllRead();
        center?.Reset();
    }

    public void OnClosed() => center?.Reset();

    public void Draw(in PhoneContext context)
    {
        AppHeader.Draw(context, DisplayName);
        center ??= new NotificationCenter(notifications,
            new NotificationRouter(context.Navigation, messageLauncher, velvetLauncher));
        var scale = ImGuiHelpers.GlobalScale;
        var content = context.Content;
        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);
        center.Draw(context, body);
    }

    public void Dispose()
    {
    }
}
