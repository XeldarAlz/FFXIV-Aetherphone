using Aetherphone.Core;
using Aetherphone.Core.Announcements;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Linkpearl;
using Aetherphone.Core.Moderation;
using Aetherphone.Core.Muster;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Radio;
using Aetherphone.Core.YellowPages;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Notifications;

internal sealed class NotificationsApp : IPhoneApp
{
    public string Id => "notifications";
    public string DisplayName => Loc.T(L.Apps.Notifications);
    public string Glyph => "N";
    public int BadgeCount => notifications.UnreadCount;
    public bool WantsSystemTheme => true;
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
    private NotificationCenter? center;

    public NotificationsApp(NotificationService notifications, SocialNotificationService socialNotifications,
        LinkpearlLauncher linkpearlLauncher,
        VelvetLauncher velvetLauncher, DmLauncher dmLauncher, GramDmLauncher gramDmLauncher,
        SocialLauncher socialLauncher, MusterLauncher musterLauncher, YellowPagesLauncher yellowPagesLauncher,
        AnnouncementsLauncher announcementsLauncher, SafetyLauncher safetyLauncher, RadioLauncher radioLauncher)
    {
        this.radioLauncher = radioLauncher;
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

    public void OnOpened()
    {
        socialNotifications.AcknowledgeAll();
        notifications.MarkAllRead();
        center?.Reset();
    }

    public void OnClosed() => center?.Reset();

    public void Draw(in PhoneContext context)
    {
        AppHeader.Draw(context, DisplayName);
        center ??= new NotificationCenter(notifications,
            new NotificationRouter(context.Navigation, notifications, socialNotifications, linkpearlLauncher,
                velvetLauncher, dmLauncher, gramDmLauncher, socialLauncher, musterLauncher, yellowPagesLauncher,
                announcementsLauncher, safetyLauncher, radioLauncher));
        var scale = UiScale.Current;
        var content = context.Content;
        var body = new Rect(new Vector2(content.Min.X, content.Min.Y + AppHeader.Height * scale), content.Max);
        UiAnchors.Report("notifications.list", body);
        center.Draw(context, body);
    }

    public void Dispose()
    {
    }
}
