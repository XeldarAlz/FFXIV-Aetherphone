using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class SoftGlow
{
    private const int Segments = 40;
    private const float MiddleRadius = 0.42f;
    private const float MiddleAlpha = 0.36f;

    private static readonly Vector2[] UnitCircle = BuildCircle();

    public static void Draw(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color)
    {
        if (radius <= 0.5f || color.W <= 0.002f)
        {
            return;
        }

        var inner = ImGui.GetColorU32(color);
        var middle = ImGui.GetColorU32(color with { W = color.W * MiddleAlpha });
        var outer = ImGui.GetColorU32(color with { W = 0f });
        var whitePixel = ImGui.GetFontTexUvWhitePixel();
        var middleRadius = radius * MiddleRadius;
        drawList.PushTextureID(ImGui.GetFontTexIdWhitePixel());
        drawList.PrimReserve(Segments * 9, Segments * 9);
        for (var segment = 0; segment < Segments; segment++)
        {
            var from = UnitCircle[segment];
            var to = UnitCircle[segment + 1];
            var middleFrom = center + from * middleRadius;
            var middleTo = center + to * middleRadius;
            var outerFrom = center + from * radius;
            var outerTo = center + to * radius;
            drawList.PrimVtx(center, whitePixel, inner);
            drawList.PrimVtx(middleFrom, whitePixel, middle);
            drawList.PrimVtx(middleTo, whitePixel, middle);
            drawList.PrimVtx(middleFrom, whitePixel, middle);
            drawList.PrimVtx(outerFrom, whitePixel, outer);
            drawList.PrimVtx(outerTo, whitePixel, outer);
            drawList.PrimVtx(middleFrom, whitePixel, middle);
            drawList.PrimVtx(outerTo, whitePixel, outer);
            drawList.PrimVtx(middleTo, whitePixel, middle);
        }

        drawList.PopTextureID();
    }

    private static Vector2[] BuildCircle()
    {
        var points = new Vector2[Segments + 1];
        for (var index = 0; index <= Segments; index++)
        {
            var angle = index * MathF.Tau / Segments;
            points[index] = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }

        return points;
    }
}
