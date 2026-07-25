using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Linkpearl;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.YellowPages;
using Aetherphone.Core.Onboarding;
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
    private readonly LinkpearlLauncher linkpearlLauncher;
    private readonly VelvetLauncher velvetLauncher;
    private readonly DmLauncher dmLauncher;
    private readonly GramDmLauncher gramDmLauncher;
    private readonly SocialLauncher socialLauncher;
    private readonly MusterLauncher musterLauncher;
    private readonly YellowPagesLauncher yellowPagesLauncher;
    private NotificationCenter? center;

    public NotificationsApp(NotificationService notifications, LinkpearlLauncher linkpearlLauncher,
        VelvetLauncher velvetLauncher, DmLauncher dmLauncher, GramDmLauncher gramDmLauncher,
        SocialLauncher socialLauncher, MusterLauncher musterLauncher, YellowPagesLauncher yellowPagesLauncher)
    {
        this.notifications = notifications;
        this.linkpearlLauncher = linkpearlLauncher;
        this.velvetLauncher = velvetLauncher;
        this.dmLauncher = dmLauncher;
        this.gramDmLauncher = gramDmLauncher;
        this.socialLauncher = socialLauncher;
        this.musterLauncher = musterLauncher;
        this.yellowPagesLauncher = yellowPagesLauncher;
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
            new NotificationRouter(context.Navigation, notifications, linkpearlLauncher, velvetLauncher, dmLauncher,
                gramDmLauncher, socialLauncher, musterLauncher, yellowPagesLauncher));
        var scale = ImGuiHelpers.GlobalScale;
        var content = context.Content;
        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);
        UiAnchors.Report("notifications.list", body);
        center.Draw(context, body);
    }

    public void Dispose()
    {
    }
}
