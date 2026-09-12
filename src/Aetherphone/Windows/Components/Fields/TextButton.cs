using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class TextButton
{
    private const float PadX = 12f;
    private const float PadY = 6f;
    private const float TextScale = 0.9f;

    public static float Width(string label, float scale)
    {
        return Typography.Measure(label, TextScale, FontWeight.SemiBold).X + (PadX * 2f * scale);
    }

    public static bool Draw(Vector2 center, string label, Vector4 color, float scale)
    {
        var size = Typography.Measure(label, TextScale, FontWeight.SemiBold);
        var min = new Vector2(center.X - size.X * 0.5f - PadX * scale, center.Y - size.Y * 0.5f - PadY * scale);
        var max = new Vector2(center.X + size.X * 0.5f + PadX * scale, center.Y + size.Y * 0.5f + PadY * scale);
        var hovered = UiInteract.Hover(min, max);
        var pressed = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(label, pressed);
        var half = (max - min) * 0.5f * press;
        Squircle.Fill(ImGui.GetWindowDrawList(), center - half, center + half, half.Y,
            ImGui.GetColorU32(Palette.WithAlpha(color, hovered ? 0.22f : 0.14f)));
        Typography.DrawCentered(center, label, color, TextScale * press, FontWeight.SemiBold);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }
}
