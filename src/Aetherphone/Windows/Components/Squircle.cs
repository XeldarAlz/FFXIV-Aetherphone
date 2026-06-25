using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class Squircle
{
    private const int CornerSegments = 8;
    private const float Exponent = 0.4f;

    private static readonly float[] CornerA = new float[CornerSegments + 1];
    private static readonly float[] CornerB = new float[CornerSegments + 1];

    static Squircle()
    {
        for (var index = 0; index <= CornerSegments; index++)
        {
            var theta = index / (float)CornerSegments * (MathF.PI * 0.5f);
            CornerA[index] = MathF.Pow(MathF.Cos(theta), Exponent);
            CornerB[index] = MathF.Pow(MathF.Sin(theta), Exponent);
        }
    }

    public static void Fill(ImDrawListPtr drawList, Vector2 min, Vector2 max, float radius, uint color)
    {
        var limit = MathF.Min(max.X - min.X, max.Y - min.Y) * 0.5f;
        var cornerRadius = MathF.Min(radius, limit);
        if (cornerRadius <= 0f)
        {
            drawList.AddRectFilled(min, max, color);
            return;
        }

        drawList.PathClear();
        EmitCorner(drawList, new Vector2(min.X + cornerRadius, min.Y + cornerRadius), cornerRadius, -1f, -1f, false);
        EmitCorner(drawList, new Vector2(max.X - cornerRadius, min.Y + cornerRadius), cornerRadius, 1f, -1f, true);
        EmitCorner(drawList, new Vector2(max.X - cornerRadius, max.Y - cornerRadius), cornerRadius, 1f, 1f, false);
        EmitCorner(drawList, new Vector2(min.X + cornerRadius, max.Y - cornerRadius), cornerRadius, -1f, 1f, true);
        drawList.PathFillConvex(color);
    }

    private static void EmitCorner(ImDrawListPtr drawList, Vector2 cornerCenter, float radius, float signX, float signY, bool swap)
    {
        for (var index = 0; index <= CornerSegments; index++)
        {
            var offsetX = swap ? CornerB[index] : CornerA[index];
            var offsetY = swap ? CornerA[index] : CornerB[index];
            drawList.PathLineTo(new Vector2(cornerCenter.X + signX * radius * offsetX, cornerCenter.Y + signY * radius * offsetY));
        }
    }
}
