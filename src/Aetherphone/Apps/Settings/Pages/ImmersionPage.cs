using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class ImmersionPage : ISettingsPage
{
    public string Title => Loc.T(L.Settings.Immersion);
    public string Summary => configuration.ScrollWhileIdle ? Loc.T(L.Settings.ScrollWhileIdle) : string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.Magic;
    public Vector4 Tint => new(0.20f, 0.70f, 0.62f, 1f);
    private readonly Configuration configuration;

    public ImmersionPage(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = UiScale.Current;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header(Loc.T(L.Settings.Immersion), theme);
            var scrollCard = GroupCard.Begin(theme, 1);
            var scroll = SettingsRow.Bool(scrollCard.NextRow(), Loc.T(L.Settings.ScrollWhileIdle),
                configuration.ScrollWhileIdle, theme);
            scrollCard.End();
            if (scroll != configuration.ScrollWhileIdle)
            {
                configuration.ScrollWhileIdle = scroll;
                configuration.Save();
            }

            ImGui.Dummy(new Vector2(0f, 8f * scale));
            SettingsSection.Hint(Loc.T(L.Settings.ScrollWhileIdleHint), theme);

            ImGui.Dummy(new Vector2(0f, 12f * scale));
            var gposeCard = GroupCard.Begin(theme, 1);
            var showInGpose = SettingsRow.Bool(gposeCard.NextRow(), Loc.T(L.Settings.ShowInGpose),
                configuration.ShowInGpose, theme);
            gposeCard.End();
            if (showInGpose != configuration.ShowInGpose)
            {
                configuration.ShowInGpose = showInGpose;
                Plugin.PluginInterface.UiBuilder.DisableGposeUiHide = showInGpose;
                configuration.Save();
            }

            ImGui.Dummy(new Vector2(0f, 8f * scale));
            SettingsSection.Hint(Loc.T(L.Settings.ShowInGposeHint), theme);
        }
    }
}
