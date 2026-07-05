using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class ImmersionPage : ISettingsPage
{
    public string Title => Loc.T(L.Settings.Immersion);

    public string Summary => configuration.ScrollWhileIdle ? Loc.T(L.Settings.ScrollWhileIdle) : string.Empty;

    public string Glyph => "I";

    public Vector4 Tint => new(0.20f, 0.70f, 0.62f, 1f);

    private readonly Configuration configuration;

    public ImmersionPage(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header(Loc.T(L.Settings.Immersion), theme);
            var behaviorCard = GroupCard.Begin(theme, 2);
            var scroll = SettingsRow.Bool(behaviorCard.NextRow(), Loc.T(L.Settings.ScrollWhileIdle), configuration.ScrollWhileIdle, theme);
            var lockPosition = SettingsRow.Bool(behaviorCard.NextRow(), Loc.T(L.ControlCenter.LockPosition), configuration.LockPosition, theme);
            behaviorCard.End();

            if (scroll != configuration.ScrollWhileIdle)
            {
                configuration.ScrollWhileIdle = scroll;
                configuration.Save();
            }

            if (lockPosition != configuration.LockPosition)
            {
                configuration.LockPosition = lockPosition;
                configuration.Save();
            }

            ImGui.Dummy(new Vector2(0f, 8f * scale));
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 16f * scale);
            using (Plugin.Fonts.Push(0.8f))
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
            {
                ImGui.TextWrapped(Loc.T(L.Settings.ScrollWhileIdleHint));
            }

            ImGui.Dummy(new Vector2(0f, 12f * scale));

            var startupCard = GroupCard.Begin(theme, 2);
            var openStartup = SettingsRow.Bool(startupCard.NextRow(), Loc.T(L.Settings.OpenOnStartup), configuration.OpenOnStartup, theme);
            var openMinimized = SettingsRow.Bool(startupCard.NextRow(), Loc.T(L.Settings.OpenMinimized), configuration.OpenMinimizedOnStartup, theme);
            startupCard.End();

            if (openStartup != configuration.OpenOnStartup)
            {
                configuration.OpenOnStartup = openStartup;
                configuration.Save();
            }

            if (openMinimized != configuration.OpenMinimizedOnStartup)
            {
                configuration.OpenMinimizedOnStartup = openMinimized;
                configuration.Save();
            }

            ImGui.Dummy(new Vector2(0f, 8f * scale));
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 16f * scale);
            using (Plugin.Fonts.Push(0.8f))
            using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
            {
                ImGui.TextWrapped(Loc.T(L.Settings.StartupHint));
            }
        }
    }
}
