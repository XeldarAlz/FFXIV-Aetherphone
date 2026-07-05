using System.Numerics;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal static class SettingsSection
{
    public static void Header(string title, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.Dummy(new Vector2(0f, Metrics.Space.Sm * scale));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Metrics.Space.Lg * scale);
        using (Plugin.Fonts.Push(0.8f))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            ImGui.TextUnformatted(title.ToUpperInvariant());
        }

        ImGui.Dummy(new Vector2(0f, Metrics.Space.Xs * scale));
    }
}
