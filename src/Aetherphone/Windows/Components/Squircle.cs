using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class Squircle
{
    public static void Fill(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, uint color)
    {
        drawList.AddRectFilled(min, max, color, ClampRadius(min, max, radius), ImDrawFlags.RoundCornersAll);
    }

    public static void Stroke(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, uint color, float thickness)
    {
        drawList.AddRect(min, max, color, ClampRadius(min, max, radius), ImDrawFlags.RoundCornersAll, thickness);
    }

    private static float ClampRadius(Vector2 min, Vector2 max, float radius)
    {
        var limit = MathF.Min(max.X - min.X, max.Y - min.Y) * 0.5f;
        return MathF.Max(0f, MathF.Min(radius, limit));
    }
}
