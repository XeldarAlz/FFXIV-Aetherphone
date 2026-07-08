using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

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
        var scale = ImGuiHelpers.GlobalScale;
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header(Loc.T(L.Common.Alerts), theme);
            var card = GroupCard.Begin(theme, 1);
            var wasEnabled = configuration.IsAppNotificationEnabled(channel.AppId);
            var enabled = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.AllowNotifications), wasEnabled, theme);
            card.End();
            if (enabled != wasEnabled)
            {
                configuration.NotificationSettingFor(channel.AppId).Enabled = enabled;
                Plugin.Analytics.Track(AnalyticsEvents.SettingChanged("notify_" + channel.AppId, enabled ? "1" : "0"));
                configuration.Save();
            }

            if (!enabled)
            {
                return;
            }

            ImGui.Dummy(new Vector2(0f, Metrics.Space.Lg * scale));
            SettingsSection.Header(Loc.T(L.Settings.Sound), theme);
            SoundOptionList.Draw(theme, sound, configuration.AppSoundOverride(channel.AppId), true, Select);
        }
    }

    private void Select(string? token)
    {
        var setting = configuration.NotificationSettingFor(channel.AppId);
        if (!string.Equals(setting.Sound, token, StringComparison.Ordinal))
        {
            setting.Sound = token;
            Plugin.Analytics.Track(AnalyticsEvents.SettingChanged("notify_sound_" + channel.AppId, token ?? "default"));
            configuration.Save();
        }

        sound.Preview(token ?? configuration.NotificationSound, configuration.NotificationVolume);
    }
}
