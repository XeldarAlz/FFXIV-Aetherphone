using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class MultiPhotoBadge
{
    private static readonly Vector4 Ink = new(1f, 1f, 1f, 0.95f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.35f);

    public static void Draw(ImDrawListPtr drawList, Vector2 topRight, float scale)
    {
        var side = 9f * scale;
        var offset = 3f * scale;
        var rounding = 2f * scale;
        var backMin = new Vector2(topRight.X - side, topRight.Y);
        var backMax = new Vector2(topRight.X, topRight.Y + side);
        var frontMin = new Vector2(backMin.X - offset, backMin.Y + offset);
        var frontMax = new Vector2(backMax.X - offset, backMax.Y + offset);
        drawList.AddRectFilled(backMin + new Vector2(0f, 1f * scale), backMax + new Vector2(0f, 1f * scale),
            ImGui.GetColorU32(Shadow), rounding);
        drawList.AddRectFilled(backMin, backMax, ImGui.GetColorU32(Ink), rounding);
        drawList.AddRectFilled(frontMin - new Vector2(1f * scale, 1f * scale),
            frontMax + new Vector2(1f * scale, 1f * scale), ImGui.GetColorU32(Shadow), rounding);
        drawList.AddRectFilled(frontMin, frontMax, ImGui.GetColorU32(Ink), rounding);
    }
}
