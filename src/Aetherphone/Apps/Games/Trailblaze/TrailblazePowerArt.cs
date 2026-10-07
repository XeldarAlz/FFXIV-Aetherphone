using Aetherphone.Apps.Games.Framework;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Trailblaze;

internal static class TrailblazePowerArt
{
    private static readonly Vector4 MagnetRed = new(0.95f, 0.36f, 0.36f, 1f);
    private static readonly Vector4 MagnetSteel = new(0.86f, 0.89f, 0.94f, 1f);
    private static readonly Vector4 DoubleGold = new(1f, 0.78f, 0.22f, 1f);
    private static readonly Vector4 DoubleRim = new(0.82f, 0.54f, 0.08f, 1f);
    private static readonly Vector4 WingsBlue = new(0.62f, 0.86f, 1f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public static Vector4 Color(TrailblazePower power) => power switch
    {
        TrailblazePower.Magnet => MagnetRed,
        TrailblazePower.Double => DoubleGold,
        TrailblazePower.Wings => WingsBlue,
        _ => White,
    };

    public static void Draw(ImDrawListPtr drawList, TrailblazePower power, Vector2 center, float size, float alpha)
    {
        switch (power)
        {
            case TrailblazePower.Magnet:
                DrawMagnet(drawList, center, size, alpha);
                return;
            case TrailblazePower.Double:
                DrawDouble(drawList, center, size, alpha);
                return;
            case TrailblazePower.Wings:
                DrawWings(drawList, center, size, alpha);
                return;
            default:
                return;
        }
    }

    private static void DrawMagnet(ImDrawListPtr drawList, Vector2 center, float size, float alpha)
    {
        var arcCenter = center + new Vector2(0f, size * 0.05f);
        var radius = size * 0.58f;
        var thickness = size * 0.4f;
        drawList.PathClear();
        drawList.PathArcTo(arcCenter, radius, MathF.PI, MathF.Tau, 16);
        drawList.PathStroke(ImGui.GetColorU32(MagnetRed with { W = alpha }), ImDrawFlags.None, thickness);
        var red = ImGui.GetColorU32(MagnetRed with { W = alpha });
        var steel = ImGui.GetColorU32(MagnetSteel with { W = alpha });
        for (var side = -1; side <= 1; side += 2)
        {
            var x = arcCenter.X + side * radius;
            var half = thickness * 0.5f;
            drawList.AddRectFilled(new Vector2(x - half, arcCenter.Y - 0.5f), new Vector2(x + half, arcCenter.Y + size * 0.2f), red);
            drawList.AddRectFilled(new Vector2(x - half, arcCenter.Y + size * 0.2f), new Vector2(x + half, arcCenter.Y + size * 0.55f),
                steel);
        }
    }

    private static void DrawDouble(ImDrawListPtr drawList, Vector2 center, float size, float alpha)
    {
        var back = center + new Vector2(size * 0.24f, -size * 0.2f);
        var front = center + new Vector2(-size * 0.18f, size * 0.16f);
        drawList.AddCircleFilled(back, size * 0.5f, ImGui.GetColorU32(DoubleRim with { W = alpha }), 18);
        drawList.AddCircleFilled(back, size * 0.38f, ImGui.GetColorU32(DoubleGold with { W = alpha * 0.85f }), 18);
        drawList.AddCircleFilled(front, size * 0.56f, ImGui.GetColorU32(DoubleRim with { W = alpha }), 20);
        drawList.AddCircleFilled(front, size * 0.43f, ImGui.GetColorU32(DoubleGold with { W = alpha }), 20);
        drawList.AddCircleFilled(front - new Vector2(size * 0.14f, size * 0.15f), size * 0.12f,
            ImGui.GetColorU32(White with { W = alpha * 0.85f }), 10);
    }

    private static void DrawWings(ImDrawListPtr drawList, Vector2 center, float size, float alpha)
    {
        var feather = ImGui.GetColorU32(WingsBlue with { W = alpha });
        var bright = ImGui.GetColorU32(White with { W = alpha });
        for (var side = -1; side <= 1; side += 2)
        {
            for (var index = 0; index < 3; index++)
            {
                var angle = -MathF.PI * 0.5f + side * (0.55f + index * 0.38f);
                var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                var featherCenter = center + new Vector2(side * size * 0.08f, size * 0.15f) + direction * size * 0.48f;
                Shapes.FillEllipse(drawList, featherCenter, size * (0.5f - index * 0.08f), size * 0.15f, angle,
                    index == 0 ? bright : feather, 12);
            }
        }

        drawList.AddCircleFilled(center + new Vector2(0f, size * 0.15f), size * 0.16f, bright, 10);
    }
}
