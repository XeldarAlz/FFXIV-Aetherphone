using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class MoonPhase
{
    private const int Segments = 32;
    private const float EdgeStroke = 1f;

    public static void Draw(ImDrawListPtr drawList, Vector2 center, float radius, float phase, uint ink)
    {
        var waxing = phase < 0.5f;
        var lit = waxing ? phase * 2f : (1f - phase) * 2f;
        var side = waxing ? 1f : -1f;
        var terminator = 1f - 2f * lit;
        var flags = drawList.Flags;
        drawList.Flags = flags & ~ImDrawListFlags.AntiAliasedFill;
        var previousLimb = Point(center, radius, side, 0);
        var previousEdge = previousLimb;
        for (var step = 1; step <= Segments; step++)
        {
            var limb = Point(center, radius, side, step);
            var edge = Point(center, radius, side * terminator, step);
            drawList.AddQuadFilled(previousLimb, limb, edge, previousEdge, ink);
            previousLimb = limb;
            previousEdge = edge;
        }

        drawList.Flags = flags;
        Trace(drawList, center, radius, side, terminator);
        drawList.PathStroke(ink, ImDrawFlags.Closed, EdgeStroke);
    }

    private static void Trace(ImDrawListPtr drawList, Vector2 center, float radius, float side, float terminator)
    {
        drawList.PathClear();
        for (var step = 0; step <= Segments; step++)
        {
            drawList.PathLineTo(Point(center, radius, side, step));
        }

        for (var step = Segments - 1; step > 0; step--)
        {
            drawList.PathLineTo(Point(center, radius, side * terminator, step));
        }
    }

    private static Vector2 Point(Vector2 center, float radius, float reach, int step)
    {
        var angle = -MathF.PI * 0.5f + MathF.PI * step / Segments;
        return center + new Vector2(reach * radius * MathF.Cos(angle), radius * MathF.Sin(angle));
    }
}
