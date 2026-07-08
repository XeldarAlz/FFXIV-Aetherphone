using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal static class ComposeFab
{
    public static bool Draw(Rect area, string childId, Vector4 accent, string glyph, string tooltip)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var radius = 26f * scale;
        var margin = 18f * scale;
        var boxSize = radius * 2f + margin;
        var boxMin = new Vector2(area.Max.X - boxSize, area.Max.Y - boxSize);
        ImGui.SetCursorScreenPos(boxMin);
        using var overlay = ImRaii.Child(childId, new Vector2(boxSize, boxSize), false,
            ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        var center = new Vector2(area.Max.X - radius - margin, area.Max.Y - radius - margin);
        var drawList = ImGui.GetWindowDrawList();
        var hovered =
            UiInteract.Hover(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        drawList.AddCircleFilled(center + new Vector2(0f, 2f * scale), radius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.30f)), 32);
        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(hovered ? Palette.Mix(accent, new Vector4(1f, 1f, 1f, 1f), 0.12f) : accent), 32);
        AppSkin.Icon(center, glyph, new Vector4(1f, 1f, 1f, 1f), 1.1f);
        HoverTooltip.Show(new Rect(center - new Vector2(radius, radius), center + new Vector2(radius, radius)),
            tooltip, HoverLabelSide.Above);
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
