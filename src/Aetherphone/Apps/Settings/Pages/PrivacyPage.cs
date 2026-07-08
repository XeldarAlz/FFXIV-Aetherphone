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

internal sealed class PrivacyPage : ISettingsPage
{
    public string Title => Loc.T(L.Settings.Privacy);
    public string Summary => configuration.AnalyticsEnabled ? Loc.T(L.Settings.PrivacyOn) : Loc.T(L.Settings.PrivacyOff);
    public FontAwesomeIcon Icon => FontAwesomeIcon.UserShield;
    public Vector4 Tint => new(0.42f, 0.56f, 0.86f, 1f);
    private readonly Configuration configuration;

    public PrivacyPage(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header(Loc.T(L.Settings.Privacy), theme);
            var card = GroupCard.Begin(theme, 1);
            var share = SettingsRow.Bool(card.NextRow(), Loc.T(L.Settings.PrivacyAnalytics),
                configuration.AnalyticsEnabled, theme);
            card.End();
            if (share != configuration.AnalyticsEnabled)
            {
                configuration.AnalyticsEnabled = share;
                configuration.AnalyticsConsentPrompted = true;
                configuration.Save();
                if (share)
                {
                    Plugin.Analytics.Track(AnalyticsEvents.SettingChanged("analytics_enabled", "1"));
                }
            }

            ImGui.Dummy(new Vector2(0f, 8f * scale));
            SettingsSection.Hint(Loc.T(L.Settings.PrivacyHint), theme);
        }
    }
}
