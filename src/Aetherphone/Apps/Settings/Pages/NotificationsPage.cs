using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class NotificationsPage : ISettingsPage
{
    public string Title => Loc.T(L.Settings.Notifications);
    public string Summary => configuration.DoNotDisturb ? Loc.T(L.Settings.DoNotDisturb) : string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Bell;
    public Vector4 Tint => new(0.98f, 0.27f, 0.25f, 1f);
    private readonly Configuration configuration;
    private readonly ISettingsNavigator navigator;
    private readonly AppNotificationPage appPage;
    private readonly SoundService sound;
    private readonly ISettingsPage soundPage;
    private readonly AppInstaller installer;

    public NotificationsPage(Configuration configuration, ISettingsNavigator navigator, AppNotificationPage appPage,
        SoundService sound, ISettingsPage soundPage, AppInstaller installer)
    {
        this.configuration = configuration;
        this.navigator = navigator;
        this.appPage = appPage;
        this.sound = sound;
        this.soundPage = soundPage;
        this.installer = installer;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        var scale = UiScale.Current;
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header(Loc.T(L.Common.Alerts), theme);
            var alerts = GroupCard.Begin(theme, 5);
            var doNotDisturb = SettingsRow.Bool(alerts.NextRow(), Loc.T(L.Settings.DoNotDisturb),
                configuration.DoNotDisturb, theme);
            if (doNotDisturb != configuration.DoNotDisturb)
            {
                configuration.DoNotDisturb = doNotDisturb;
                configuration.Save();
            }

            var quietWhileBusy = SettingsRow.Bool(alerts.NextRow(), Loc.T(L.Settings.QuietWhileBusy),
                configuration.QuietWhileBusy, theme, null, Loc.T(L.Settings.QuietWhileBusyHint),
                dimmed: doNotDisturb);
            if (quietWhileBusy != configuration.QuietWhileBusy)
            {
                configuration.QuietWhileBusy = quietWhileBusy;
                configuration.Save();
            }

            var showNotificationBanner = SettingsRow.Bool(alerts.NextRow(), Loc.T(L.Settings.ShowNotificationBanner),
                configuration.ShowNotificationBanner, theme, null, Loc.T(L.Settings.ShowNotificationBannerHint),
                dimmed: doNotDisturb);
            if (showNotificationBanner != configuration.ShowNotificationBanner)
            {
                configuration.ShowNotificationBanner = showNotificationBanner;
                configuration.Save();
            }

            var vibration = SettingsRow.Bool(alerts.NextRow(), Loc.T(L.Settings.Vibration),
                configuration.Vibration, theme, null, Loc.T(L.Settings.VibrationHint), dimmed: doNotDisturb);
            if (vibration != configuration.Vibration)
            {
                configuration.Vibration = vibration;
                configuration.Save();
            }

            if (SettingsRow.Disclosure(alerts.NextRow(), Loc.T(L.Settings.NotificationSound),
                    sound.Label(SoundKind.Notification, configuration.NotificationSound), theme, dimmed: doNotDisturb))
            {
                navigator.Open(soundPage);
            }

            alerts.End();

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            SettingsSection.Header(Loc.T(L.Settings.NotificationApps), theme);
            var channels = NotificationChannels.All;
            var apps = GroupCard.Begin(theme, CountInstalled(channels));
            for (var index = 0; index < channels.Count; index++)
            {
                var channel = channels[index];
                if (!installer.IsInstalled(channel.AppId))
                {
                    continue;
                }

                if (SettingsRow.AppLink(apps.NextRow(), channel.AppId, channel.Accent, Loc.T(channel.Name),
                        Summarize(channel.AppId), theme))
                {
                    appPage.Show(channel);
                    navigator.Open(appPage);
                }
            }

            apps.End();
        }
    }

    private int CountInstalled(IReadOnlyList<NotificationChannel> channels)
    {
        var count = 0;
        for (var index = 0; index < channels.Count; index++)
        {
            if (installer.IsInstalled(channels[index].AppId))
            {
                count++;
            }
        }

        return count;
    }

    private string Summarize(string appId)
    {
        if (!configuration.IsAppNotificationEnabled(appId))
        {
            return Loc.T(L.Settings.NotificationsOff);
        }

        return sound.Label(SoundKind.Notification, configuration.ResolveNotificationToken(appId));
    }
}
