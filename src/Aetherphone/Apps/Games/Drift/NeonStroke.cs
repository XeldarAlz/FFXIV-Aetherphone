using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Drift;

internal static class NeonStroke
{
    public const float HaloScale = 3.4f;
    public const float HaloAlpha = 0.14f;
    public const float GlowScale = 1.9f;
    public const float GlowAlpha = 0.36f;
    private const float CoreLift = 0.6f;
    private const float FillAlpha = 0.12f;
    private const int CircleSegments = 28;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    public static Vector4 Core(Vector4 color) => Vector4.Lerp(color, White, CoreLift) with { W = color.W };

    public static void Line(ImDrawListPtr drawList, Vector2 from, Vector2 to, Vector4 color, float width)
    {
        drawList.AddLine(from, to, Faded(color, HaloAlpha), width * HaloScale);
        drawList.AddLine(from, to, Faded(color, GlowAlpha), width * GlowScale);
        drawList.AddLine(from, to, ImGui.GetColorU32(Core(color)), width);
    }

    public static void Path(ImDrawListPtr drawList, ReadOnlySpan<Vector2> points, bool closed, Vector4 color,
        float width)
    {
        if (points.Length < 2)
        {
            return;
        }

        var flags = closed ? ImDrawFlags.Closed : ImDrawFlags.None;
        Pass(drawList, points, flags, Faded(color, HaloAlpha), width * HaloScale);
        Pass(drawList, points, flags, Faded(color, GlowAlpha), width * GlowScale);
        Pass(drawList, points, flags, ImGui.GetColorU32(Core(color)), width);
    }

    public static void Fill(ImDrawListPtr drawList, ReadOnlySpan<Vector2> points, Vector4 color, float alpha = FillAlpha)
    {
        if (points.Length < 3)
        {
            return;
        }

        drawList.PathClear();
        for (var index = 0; index < points.Length; index++)
        {
            drawList.PathLineTo(points[index]);
        }

        drawList.PathFillConvex(Faded(color, alpha));
    }

    public static void Circle(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color, float width)
    {
        drawList.AddCircle(center, radius, Faded(color, HaloAlpha), CircleSegments, width * HaloScale);
        drawList.AddCircle(center, radius, Faded(color, GlowAlpha), CircleSegments, width * GlowScale);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(Core(color)), CircleSegments, width);
    }

    public static void Dot(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color)
    {
        drawList.AddCircleFilled(center, radius * HaloScale, Faded(color, HaloAlpha), 16);
        drawList.AddCircleFilled(center, radius * GlowScale, Faded(color, GlowAlpha), 16);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Core(color)), 12);
    }

    private static void Pass(ImDrawListPtr drawList, ReadOnlySpan<Vector2> points, ImDrawFlags flags, uint color,
        float width)
    {
        drawList.PathClear();
        for (var index = 0; index < points.Length; index++)
        {
            drawList.PathLineTo(points[index]);
        }

        drawList.PathStroke(color, flags, width);
    }

    private static uint Faded(Vector4 color, float alpha) => ImGui.GetColorU32(color with { W = color.W * alpha });
}
