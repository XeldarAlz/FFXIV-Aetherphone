using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class AppNotificationPage : ISettingsPage
{
    public string Title => Loc.T(channel.Name);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Bell;
    public Vector4 Tint => channel.Accent;
    private readonly Configuration configuration;
    private readonly SoundService sound;
    private NotificationChannel channel = NotificationChannels.All[0];

    public AppNotificationPage(Configuration configuration, SoundService sound)
    {
        this.configuration = configuration;
        this.sound = sound;
    }

    public void Show(NotificationChannel target) => channel = target;

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        var scale = UiScale.Current;
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header(Loc.T(L.Common.Alerts), theme);
            var appSetting = configuration.NotificationSettingFor(channel.AppId);
            var wasEnabled = configuration.IsAppNotificationEnabled(channel.AppId);
            var card = GroupCard.Begin(theme, wasEnabled ? 2 : 1);
            var enabled = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.AllowNotifications), wasEnabled, theme);

            if (wasEnabled)
            {
                var showNotificationBanner = SettingsRow.Bool(card.NextRow(),
                    Loc.T(L.Settings.ShowNotificationBanner), appSetting.ShowNotificationBanner, theme);
                if (showNotificationBanner != appSetting.ShowNotificationBanner)
                {
                    appSetting.ShowNotificationBanner = showNotificationBanner;
                    configuration.Save();
                }
            }

            card.End();
            if (enabled != wasEnabled)
            {
                appSetting.Enabled = enabled;
                configuration.Save();
            }

            if (!wasEnabled)
            {
                return;
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            SettingsSection.Header(Loc.T(L.Settings.Sound), theme);
            SoundOptionList.Draw(theme, sound, SoundKind.Notification, configuration.AppSoundOverride(channel.AppId),
                true, Select);
        }
    }

    private void Select(string? token)
    {
        var setting = configuration.NotificationSettingFor(channel.AppId);
        if (!string.Equals(setting.Sound, token, StringComparison.Ordinal))
        {
            setting.Sound = token;
            configuration.Save();
        }

        sound.Preview(SoundKind.Notification, token ?? configuration.NotificationSound,
            configuration.NotificationVolume);
    }
}
