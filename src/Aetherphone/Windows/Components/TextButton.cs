using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class TextButton
{
    private const float PadX = 12f;
    private const float PadY = 6f;

    public static bool Draw(Vector2 center, string label, Vector4 color, float scale)
    {
        var size = Typography.Measure(label, 0.9f, FontWeight.SemiBold);
        var min = new Vector2(center.X - size.X * 0.5f - PadX * scale, center.Y - size.Y * 0.5f - PadY * scale);
        var max = new Vector2(center.X + size.X * 0.5f + PadX * scale, center.Y + size.Y * 0.5f + PadY * scale);
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        Squircle.Fill(ImGui.GetWindowDrawList(), min, max, (max.Y - min.Y) * 0.5f,
            ImGui.GetColorU32(Palette.WithAlpha(color, hovered ? 0.22f : 0.14f)));
        Typography.DrawCentered(center, label, color, 0.9f, FontWeight.SemiBold);
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}
