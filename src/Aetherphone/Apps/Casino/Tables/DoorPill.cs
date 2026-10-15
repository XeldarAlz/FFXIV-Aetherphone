using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Tables;

internal static class DoorPill
{
    public const float PadX = 12f;

    public static float WidthFor(string label, float height, float scale) =>
        Typography.Measure(label, Button.LabelStyle(height)).X + PadX * 2f * scale;

    public static bool Draw(ImDrawListPtr drawList, Rect rect, string label, in ControlInk ink, ButtonStyle style,
        Vector4? labelInk, bool enabled, string id, float scale)
    {
        var slop = MathF.Max(0f, (DoorLayout.Touch * scale - rect.Height) * 0.5f);
        var hitMin = new Vector2(rect.Min.X, rect.Min.Y - slop);
        var hitMax = new Vector2(rect.Max.X, rect.Max.Y + slop);
        var hovered = enabled && UiInteract.Hover(hitMin, hitMax);
        var face = Button.Surface(drawList, rect, ink, style, ButtonRole.Normal, enabled, hovered, ImGui.GetID(id));
        var labelStyle = Button.LabelStyle(rect.Height);
        var color = labelInk is { } custom ? custom with { W = custom.W * face.LabelInk.W } : face.LabelInk;
        var room = MathF.Max(1f, face.Face.Width - PadX * scale);
        var fitted = Typography.FitText(label, room, labelStyle);
        var size = Typography.Measure(fitted, labelStyle);
        Typography.Draw(drawList, face.Face.Center - size * 0.5f, fitted, color, labelStyle);
        return enabled && UiInteract.Click(hitMin, hitMax, hovered);
    }
}
