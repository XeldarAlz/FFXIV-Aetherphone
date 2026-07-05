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
        ImGui.Dummy(new Vector2(0f, 10f * scale));
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 16f * scale);
        using (Plugin.Fonts.Push(0.8f))
        using (ImRaii.PushColor(ImGuiCol.Text, theme.TextMuted))
        {
            ImGui.TextUnformatted(title.ToUpperInvariant());
        }

        ImGui.Dummy(new Vector2(0f, 6f * scale));
    }
}
