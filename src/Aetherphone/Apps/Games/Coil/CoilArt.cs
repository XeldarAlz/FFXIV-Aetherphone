using Aetherphone.Apps.Games.Framework;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Coil;

internal static class CoilArt
{
    private const float HalfPi = MathF.PI * 0.5f;

    public static readonly Vector4[] MarbleColours =
    {
        new(0.94f, 0.30f, 0.36f, 1f), new(0.98f, 0.78f, 0.24f, 1f), new(0.28f, 0.80f, 0.50f, 1f),
        new(0.30f, 0.56f, 0.98f, 1f), new(0.68f, 0.42f, 0.95f, 1f), new(0.88f, 0.89f, 0.93f, 1f),
    };

    public static Vector4 ColourOf(byte colour) => MarbleColours[colour % MarbleColours.Length];

    public static Vector4 PowerColour(CoilPower power) => power switch
    {
        CoilPower.Freeze => new Vector4(0.55f, 0.86f, 1f, 1f),
        CoilPower.Slow => new Vector4(1f, 0.78f, 0.36f, 1f),
        CoilPower.Reverse => new Vector4(0.78f, 0.58f, 1f, 1f),
        CoilPower.Blast => new Vector4(1f, 0.56f, 0.24f, 1f),
        CoilPower.Prism => new Vector4(1f, 1f, 1f, 1f),
        CoilPower.Guide => new Vector4(0.46f, 0.98f, 0.62f, 1f),
        _ => new Vector4(1f, 1f, 1f, 1f),
    };

    public static void Shadow(ImDrawListPtr drawList, Vector2 center, float radius, float alpha)
    {
        var offset = new Vector2(radius * 0.16f, radius * 0.30f);
        drawList.AddCircleFilled(center + offset, radius * 1.12f, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.14f * alpha)));
        drawList.AddCircleFilled(center + offset, radius * 0.94f, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.26f * alpha)));
    }

    public static void Marble(ImDrawListPtr drawList, Vector2 center, float radius, byte colour, CoilPower power,
        float rollPhase, Vector2 tangent, float alpha, float time)
    {
        if (radius < 0.5f || alpha <= 0.01f)
        {
            return;
        }

        var fill = ColourOf(colour);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(GamePalette.Darken(fill, 0.42f) with { W = alpha }));
        drawList.AddCircleFilled(center + new Vector2(-0.07f, -0.09f) * radius, radius * 0.86f,
            ImGui.GetColorU32(GamePalette.Darken(fill, 0.14f) with { W = alpha }));
        drawList.AddCircleFilled(center + new Vector2(-0.15f, -0.19f) * radius, radius * 0.62f,
            ImGui.GetColorU32(fill with { W = alpha }));
        drawList.AddCircleFilled(center + new Vector2(-0.23f, -0.29f) * radius, radius * 0.36f,
            ImGui.GetColorU32(GamePalette.Lighten(fill, 0.22f) with { W = alpha }));
        Bands(drawList, center, radius, fill, rollPhase, tangent, alpha);
        if (power == CoilPower.None)
        {
            ColourGlyph(drawList, center, radius * 0.34f, colour,
                ImGui.GetColorU32(GamePalette.InkOn(fill) with { W = 0.5f * alpha }));
        }
        else
        {
            var pulse = 0.5f + 0.5f * MathF.Sin(time * 6f);
            drawList.AddCircleFilled(center, radius * 0.56f, ImGui.GetColorU32(new Vector4(0.05f, 0.05f, 0.08f, 0.62f * alpha)));
            PowerGlyph(drawList, center, radius * 0.42f, power,
                ImGui.GetColorU32(GamePalette.Lighten(PowerColour(power), 0.3f) with { W = alpha }), radius * 0.12f);
            drawList.AddCircle(center, radius * (1.16f + pulse * 0.14f),
                ImGui.GetColorU32(PowerColour(power) with { W = (0.35f + pulse * 0.45f) * alpha }), 0,
                MathF.Max(1f, radius * 0.12f));
        }

        drawList.AddCircleFilled(center + new Vector2(-0.30f, -0.36f) * radius, radius * 0.36f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.16f * alpha)));
        drawList.AddCircleFilled(center + new Vector2(-0.36f, -0.42f) * radius, radius * 0.17f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.88f * alpha)));
        drawList.PathArcTo(center, radius * 0.84f, HalfPi * 0.15f, HalfPi * 1.15f, 12);
        drawList.PathStroke(ImGui.GetColorU32(GamePalette.Lighten(fill, 0.4f) with { W = 0.42f * alpha }), ImDrawFlags.None,
            MathF.Max(1f, radius * 0.12f));
        drawList.AddCircle(center, radius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.28f * alpha)), 0, 1f);
    }

    private static void Bands(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 fill, float rollPhase,
        Vector2 tangent, float alpha)
    {
        var across = new Vector2(-tangent.Y, tangent.X);
        for (var stripe = 0; stripe < 2; stripe++)
        {
            var phase = rollPhase + stripe * MathF.PI;
            var depth = MathF.Cos(phase);
            if (depth <= 0.05f)
            {
                continue;
            }

            var offset = MathF.Sin(phase) * radius * 0.78f;
            var half = MathF.Sqrt(MathF.Max(0f, radius * radius - offset * offset)) * 0.84f;
            var thickness = radius * 0.13f * depth;
            var middle = center + tangent * offset;
            var color = ImGui.GetColorU32(GamePalette.Lighten(fill, 0.5f) with { W = 0.5f * depth * alpha });
            drawList.AddQuadFilled(middle - across * half - tangent * thickness, middle + across * half - tangent * thickness,
                middle + across * half + tangent * thickness, middle - across * half + tangent * thickness, color);
        }
    }

    public static void ColourGlyph(ImDrawListPtr drawList, Vector2 center, float size, byte colour, uint color)
    {
        switch (colour % MarbleColours.Length)
        {
            case 0:
                drawList.AddCircleFilled(center, size * 0.78f, color);
                return;
            case 1:
                drawList.AddTriangleFilled(new Vector2(center.X, center.Y - size),
                    new Vector2(center.X + size * 0.92f, center.Y + size * 0.7f),
                    new Vector2(center.X - size * 0.92f, center.Y + size * 0.7f), color);
                return;
            case 2:
                drawList.AddRectFilled(center - new Vector2(size * 0.74f, size * 0.74f),
                    center + new Vector2(size * 0.74f, size * 0.74f), color, size * 0.18f);
                return;
            case 3:
                drawList.AddQuadFilled(new Vector2(center.X, center.Y - size), new Vector2(center.X + size, center.Y),
                    new Vector2(center.X, center.Y + size), new Vector2(center.X - size, center.Y), color);
                return;
            case 4:
                drawList.AddRectFilled(new Vector2(center.X - size, center.Y - size * 0.3f),
                    new Vector2(center.X + size, center.Y + size * 0.3f), color, size * 0.14f);
                drawList.AddRectFilled(new Vector2(center.X - size * 0.3f, center.Y - size),
                    new Vector2(center.X + size * 0.3f, center.Y + size), color, size * 0.14f);
                return;
            default:
                drawList.AddCircle(center, size * 0.72f, color, 0, MathF.Max(1f, size * 0.36f));
                return;
        }
    }

    public static void PowerGlyph(ImDrawListPtr drawList, Vector2 center, float size, CoilPower power, uint color,
        float thickness)
    {
        thickness = MathF.Max(1f, thickness);
        switch (power)
        {
            case CoilPower.Freeze:
                for (var arm = 0; arm < 3; arm++)
                {
                    var direction = CoilShapes.Polar(arm * MathF.PI / 3f + HalfPi);
                    drawList.AddLine(center - direction * size, center + direction * size, color, thickness);
                }

                return;
            case CoilPower.Slow:
                drawList.AddTriangleFilled(center + new Vector2(-size * 0.7f, -size), center + new Vector2(size * 0.7f, -size),
                    center, color);
                drawList.AddTriangleFilled(center, center + new Vector2(size * 0.7f, size),
                    center + new Vector2(-size * 0.7f, size), color);
                return;
            case CoilPower.Reverse:
                drawList.PathArcTo(center, size * 0.8f, -MathF.PI * 0.2f, MathF.PI * 1.25f, 16);
                drawList.PathStroke(color, ImDrawFlags.None, thickness);
                var tip = center + CoilShapes.Polar(-MathF.PI * 0.2f) * size * 0.8f;
                drawList.AddTriangleFilled(tip + new Vector2(-size * 0.5f, -size * 0.05f), tip + new Vector2(size * 0.35f, -size * 0.45f),
                    tip + new Vector2(size * 0.2f, size * 0.4f), color);
                return;
            case CoilPower.Blast:
                for (var ray = 0; ray < 8; ray++)
                {
                    var direction = CoilShapes.Polar(ray * MathF.PI / 4f);
                    var reach = ray % 2 == 0 ? size : size * 0.62f;
                    drawList.AddLine(center + direction * size * 0.25f, center + direction * reach, color, thickness);
                }

                drawList.AddCircleFilled(center, size * 0.28f, color);
                return;
            case CoilPower.Prism:
                drawList.AddTriangle(center + new Vector2(0f, -size), center + new Vector2(size * 0.92f, size * 0.7f),
                    center + new Vector2(-size * 0.92f, size * 0.7f), color, thickness);
                drawList.AddLine(center + new Vector2(-size, -size * 0.1f), center + new Vector2(size * 0.1f, size * 0.15f), color,
                    thickness * 0.8f);
                return;
            case CoilPower.Guide:
                drawList.AddCircle(center, size * 0.62f, color, 0, thickness);
                drawList.AddLine(center + new Vector2(0f, -size), center + new Vector2(0f, -size * 0.3f), color, thickness);
                drawList.AddLine(center + new Vector2(0f, size), center + new Vector2(0f, size * 0.3f), color, thickness);
                drawList.AddLine(center + new Vector2(-size, 0f), center + new Vector2(-size * 0.3f, 0f), color, thickness);
                drawList.AddLine(center + new Vector2(size, 0f), center + new Vector2(size * 0.3f, 0f), color, thickness);
                return;
        }
    }
}
