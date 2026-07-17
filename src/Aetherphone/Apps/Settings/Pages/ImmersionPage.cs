using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Analytics;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class ImmersionPage : ISettingsPage
{
    public string Title => Loc.T(L.Settings.Immersion);
    public string Summary => configuration.ScrollWhileIdle ? Loc.T(L.Settings.ScrollWhileIdle) : string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Magic;
    public Vector4 Tint => new(0.20f, 0.70f, 0.62f, 1f);
    private readonly Configuration configuration;
    private readonly IAnalyticsService analytics;

    public ImmersionPage(Configuration configuration, IAnalyticsService analytics)
    {
        this.configuration = configuration;
        this.analytics = analytics;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header(Loc.T(L.Settings.Immersion), theme);
            var behaviorCard = GroupCard.Begin(theme, 2);
            var scroll = SettingsRow.Bool(behaviorCard.NextRow(), Loc.T(L.Settings.ScrollWhileIdle),
                configuration.ScrollWhileIdle, theme);
            var lockPosition = SettingsRow.Bool(behaviorCard.NextRow(), Loc.T(L.ControlCenter.LockPosition),
                configuration.LockPosition, theme);
            behaviorCard.End();
            if (scroll != configuration.ScrollWhileIdle)
            {
                configuration.ScrollWhileIdle = scroll;
                analytics.Track(AnalyticsEvents.SettingChanged("scroll_while_idle", scroll ? "1" : "0"));
                configuration.Save();
            }

            if (lockPosition != configuration.LockPosition)
            {
                configuration.LockPosition = lockPosition;
                analytics.Track(AnalyticsEvents.SettingChanged("lock_position", lockPosition ? "1" : "0"));
                configuration.Save();
            }

            ImGui.Dummy(new Vector2(0f, 8f * scale));
            SettingsSection.Hint(Loc.T(L.Settings.ScrollWhileIdleHint), theme);

            ImGui.Dummy(new Vector2(0f, 12f * scale));
            var startupCard = GroupCard.Begin(theme, 2);
            var openStartup = SettingsRow.Bool(startupCard.NextRow(), Loc.T(L.Settings.OpenOnStartup),
                configuration.OpenOnStartup, theme);
            var openMinimized = SettingsRow.Bool(startupCard.NextRow(), Loc.T(L.Settings.OpenMinimized),
                configuration.OpenMinimizedOnStartup, theme);
            startupCard.End();
            if (openStartup != configuration.OpenOnStartup)
            {
                configuration.OpenOnStartup = openStartup;
                analytics.Track(AnalyticsEvents.SettingChanged("open_on_startup", openStartup ? "1" : "0"));
                configuration.Save();
            }

            if (openMinimized != configuration.OpenMinimizedOnStartup)
            {
                configuration.OpenMinimizedOnStartup = openMinimized;
                analytics.Track(AnalyticsEvents.SettingChanged("open_minimized", openMinimized ? "1" : "0"));
                configuration.Save();
            }

            ImGui.Dummy(new Vector2(0f, 8f * scale));
            SettingsSection.Hint(Loc.T(L.Settings.StartupHint), theme);
        }
    }
}
