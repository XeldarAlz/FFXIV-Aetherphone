using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

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
    private readonly IAnalyticsService analytics;

    public NotificationsPage(Configuration configuration, ISettingsNavigator navigator, AppNotificationPage appPage,
        SoundService sound, ISettingsPage soundPage, IAnalyticsService analytics)
    {
        this.configuration = configuration;
        this.navigator = navigator;
        this.appPage = appPage;
        this.sound = sound;
        this.soundPage = soundPage;
        this.analytics = analytics;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        var scale = ImGuiHelpers.GlobalScale;
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header(Loc.T(L.Common.Alerts), theme);
            var alerts = GroupCard.Begin(theme, 3);
            var doNotDisturb = SettingsRow.Bool(alerts.NextRow(), Loc.T(L.Settings.DoNotDisturb),
                configuration.DoNotDisturb, theme);
            if (doNotDisturb != configuration.DoNotDisturb)
            {
                configuration.DoNotDisturb = doNotDisturb;
                analytics.Track(AnalyticsEvents.SettingChanged("do_not_disturb", doNotDisturb ? "1" : "0"));
                configuration.Save();
            }

            var vibration = SettingsRow.Bool(alerts.NextRow(), Loc.T(L.Settings.Vibration),
                configuration.Vibration, theme);
            if (vibration != configuration.Vibration)
            {
                configuration.Vibration = vibration;
                analytics.Track(AnalyticsEvents.SettingChanged("vibration", vibration ? "1" : "0"));
                configuration.Save();
            }

            if (SettingsRow.Disclosure(alerts.NextRow(), Loc.T(L.Settings.NotificationSound),
                    sound.Label(configuration.NotificationSound), theme))
            {
                navigator.Open(soundPage);
            }

            alerts.End();

            ImGui.Dummy(new Vector2(0f, 8f * scale));
            SettingsSection.Hint(Loc.T(L.Settings.VibrationHint), theme);

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            SettingsSection.Header(Loc.T(L.Settings.NotificationApps), theme);
            var channels = NotificationChannels.All;
            var apps = GroupCard.Begin(theme, channels.Count);
            for (var index = 0; index < channels.Count; index++)
            {
                var channel = channels[index];
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

    private string Summarize(string appId)
    {
        if (!configuration.IsAppNotificationEnabled(appId))
        {
            return Loc.T(L.Settings.NotificationsOff);
        }

        return sound.Label(configuration.ResolveNotificationToken(appId));
    }
}
