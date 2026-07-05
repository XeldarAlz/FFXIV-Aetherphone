using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class Toggle
{
    public static bool Draw(Rect bounds, bool value, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var result = value;
        if (ImGui.IsMouseHoveringRect(bounds.Min, bounds.Max))
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                result = !value;
            }
        }

        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(bounds.Min, bounds.Max, ImGui.GetColorU32(result ? theme.ToggleOn : theme.ToggleOff),
            bounds.Height * 0.5f);
        var knobRadius = bounds.Height * 0.5f - 2f * scale;
        var knobX = result ? bounds.Max.X - knobRadius - 2f * scale : bounds.Min.X + knobRadius + 2f * scale;
        dl.AddCircleFilled(new Vector2(knobX, bounds.Center.Y), knobRadius,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)), 32);
        return result;
    }
}
