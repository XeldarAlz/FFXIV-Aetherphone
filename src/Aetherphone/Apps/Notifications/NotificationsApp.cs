using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Notifications;

internal sealed class NotificationsApp : IPhoneApp
{
    public string Id => "notifications";

    public string DisplayName => "Notifications";

    public string Glyph => "N";

    public Vector4 Accent => new(0.92f, 0.30f, 0.34f, 1f);

    public int BadgeCount => notifications.UnreadCount;

    private readonly NotificationService notifications;

    public NotificationsApp(NotificationService notifications)
    {
        this.notifications = notifications;
    }

    public void OnOpened() => notifications.MarkAllRead();

    public void OnClosed()
    {
    }

    public void Draw(in PhoneContext context)
    {
        AppHeader.Draw(context, DisplayName);

        var scale = ImGuiHelpers.GlobalScale;
        var content = context.Content;
        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);

        var recent = notifications.Recent;
        if (recent.Count == 0)
        {
            Typography.DrawCentered(body.Center, "No notifications", context.Theme.TextMuted);
            return;
        }

        using (AppSurface.Begin(body))
        {
            for (var index = recent.Count - 1; index >= 0; index--)
            {
                NotificationCard.Draw(recent[index], context.Theme);
            }
        }
    }

    public void Dispose()
    {
    }
}
