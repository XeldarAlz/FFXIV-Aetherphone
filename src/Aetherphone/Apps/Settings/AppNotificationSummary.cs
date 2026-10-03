using Aetherphone.Apps.Settings.Pages;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Settings;

internal static class AppNotificationSummary
{
    public static string For(Configuration configuration, in AppSettingsEntry entry)
    {
        var notificationsOn = !entry.HasChannel || configuration.IsAppNotificationEnabled(entry.AppId);
        var badgeOn = !entry.HasBadge || configuration.IsAppBadgeEnabled(entry.AppId);
        if (notificationsOn && badgeOn)
        {
            return string.Empty;
        }

        if (!entry.HasChannel || !entry.HasBadge)
        {
            return Loc.T(L.Settings.NotificationsOff);
        }

        return notificationsOn ? Loc.T(L.Settings.NotificationOnly)
            : badgeOn ? Loc.T(L.Settings.BadgeOnly)
            : Loc.T(L.Settings.NotificationsOff);
    }
}
