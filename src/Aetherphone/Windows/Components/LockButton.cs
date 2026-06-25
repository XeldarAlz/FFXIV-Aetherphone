using System.Numerics;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal static class LockButton
{
    public static bool Draw(Vector2 center, float radius, FontAwesomeIcon icon, bool active, PhoneTheme theme)
    {
        var min = new Vector2(center.X - radius, center.Y - radius);
        var max = new Vector2(center.X + radius, center.Y + radius);
        var hovered = ImGui.IsMouseHoveringRect(min, max);

        var background = active
            ? Palette.WithAlpha(theme.Accent, hovered ? 0.36f : 0.26f)
            : Palette.WithAlpha(theme.TextStrong, hovered ? 0.20f : 0.10f);
        ImGui.GetWindowDrawList().AddCircleFilled(center, radius, ImGui.GetColorU32(background), 32);

        var ink = active || hovered ? theme.TextStrong : theme.TextMuted;
        ProgressRing.CenterIcon(center, icon, ink, radius * 1.02f);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
