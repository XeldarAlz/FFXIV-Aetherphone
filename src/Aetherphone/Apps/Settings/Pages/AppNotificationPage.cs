using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class AppNotificationPage : ISettingsPage
{
    private static readonly NotificationChannel PlaceholderChannel = NotificationChannels.All[0];

    public string Title => entry.App?.DisplayName ?? Loc.T(PlaceholderChannel.Name);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Bell;
    public Vector4 Tint => entry.App?.Accent ?? PlaceholderChannel.Accent;
    private readonly Configuration configuration;
    private readonly SoundService sound;
    private AppSettingsEntry entry = new(PlaceholderChannel.AppId, string.Empty, PlaceholderChannel.Accent, true,
        false);

    public AppNotificationPage(Configuration configuration, SoundService sound)
    {
        this.configuration = configuration;
        this.sound = sound;
    }

    public void Show(AppSettingsEntry target) => entry = target;

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        var scale = UiScale.Current;
        using (AppSurface.Begin(body))
        {
            var wasEnabled = entry.HasChannel && configuration.IsAppNotificationEnabled(entry.AppId);
            var drewPrevious = false;
            if (entry.HasChannel)
            {
                DrawAlertsSection(theme, wasEnabled);
                drewPrevious = true;
            }

            if (entry.HasBadge)
            {
                if (drewPrevious)
                {
                    ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
                }

                DrawBadgeSection(theme);
                drewPrevious = true;
            }

            if (wasEnabled)
            {
                if (drewPrevious)
                {
                    ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
                }

                SettingsSection.Header(Loc.T(L.Settings.Sound), theme);
                SoundOptionList.Draw(theme, sound, SoundKind.Notification, configuration.AppSoundOverride(entry.AppId),
                    true, Select);
            }
        }
    }

    private void DrawAlertsSection(PhoneTheme theme, bool wasEnabled)
    {
        SettingsSection.Header(Loc.T(L.Common.Alerts), theme);
        var appSetting = configuration.NotificationSettingFor(entry.AppId);
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
    }

    private void DrawBadgeSection(PhoneTheme theme)
    {
        SettingsSection.Header(Loc.T(L.Home.HomeScreen), theme);
        var badgeEnabled = configuration.IsAppBadgeEnabled(entry.AppId);
        var card = GroupCard.Begin(theme, 1);
        var updated = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.ShowBadge), badgeEnabled, theme);
        card.End();
        if (updated == badgeEnabled)
        {
            return;
        }

        configuration.SetAppBadgeEnabled(entry.AppId, updated);
    }

    private void Select(string? token)
    {
        var setting = configuration.NotificationSettingFor(entry.AppId);
        if (!string.Equals(setting.Sound, token, StringComparison.Ordinal))
        {
            setting.Sound = token;
            configuration.Save();
        }

        sound.Preview(SoundKind.Notification, token ?? configuration.NotificationSound,
            configuration.NotificationVolume);
    }
}
