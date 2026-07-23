using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class MediaGlyph
{
    public static void Stop(ImDrawListPtr drawList, Vector2 center, float size, uint ink)
    {
        drawList.AddRectFilled(center - new Vector2(size, size), center + new Vector2(size, size), ink, size * 0.3f);
    }

    public static void Play(ImDrawListPtr drawList, Vector2 center, float size, uint ink)
    {
        drawList.AddTriangleFilled(new Vector2(center.X - size * 0.7f, center.Y - size),
            new Vector2(center.X - size * 0.7f, center.Y + size), new Vector2(center.X + size, center.Y), ink);
    }

    public static void FastForward(ImDrawListPtr drawList, Vector2 center, float size, uint ink)
    {
        drawList.AddTriangleFilled(new Vector2(center.X - size * 1.35f, center.Y - size),
            new Vector2(center.X - size * 1.35f, center.Y + size), new Vector2(center.X, center.Y), ink);
        drawList.AddTriangleFilled(new Vector2(center.X - size * 0.05f, center.Y - size),
            new Vector2(center.X - size * 0.05f, center.Y + size), new Vector2(center.X + size * 1.30f, center.Y),
            ink);
    }

    public static void Pause(ImDrawListPtr drawList, Vector2 center, float size, uint ink)
    {
        var barWidth = size * 0.34f;
        var gap = size * 0.32f;
        drawList.AddRectFilled(new Vector2(center.X - gap - barWidth, center.Y - size),
            new Vector2(center.X - gap, center.Y + size), ink, barWidth * 0.3f);
        drawList.AddRectFilled(new Vector2(center.X + gap, center.Y - size),
            new Vector2(center.X + gap + barWidth, center.Y + size), ink, barWidth * 0.3f);
    }

    public static void Next(ImDrawListPtr drawList, Vector2 center, float size, uint ink)
    {
        drawList.AddTriangleFilled(new Vector2(center.X - size * 0.85f, center.Y - size),
            new Vector2(center.X - size * 0.85f, center.Y + size), new Vector2(center.X + size * 0.45f, center.Y), ink);
        drawList.AddRectFilled(new Vector2(center.X + size * 0.5f, center.Y - size),
            new Vector2(center.X + size * 0.8f, center.Y + size), ink, size * 0.2f);
    }

    public static void Previous(ImDrawListPtr drawList, Vector2 center, float size, uint ink)
    {
        drawList.AddTriangleFilled(new Vector2(center.X + size * 0.85f, center.Y - size),
            new Vector2(center.X + size * 0.85f, center.Y + size), new Vector2(center.X - size * 0.45f, center.Y), ink);
        drawList.AddRectFilled(new Vector2(center.X - size * 0.8f, center.Y - size),
            new Vector2(center.X - size * 0.5f, center.Y + size), ink, size * 0.2f);
    }

    public static void Repeat(ImDrawListPtr drawList, Vector2 center, float size, uint ink)
    {
        var radius = size;
        var thickness = MathF.Max(1.4f, size * 0.24f);
        RepeatArc(drawList, center, radius, MathF.PI * -0.15f, MathF.PI * 0.59f, ink, thickness);
        RepeatArc(drawList, center, radius, MathF.PI * 0.85f, MathF.PI * 1.59f, ink, thickness);
        RepeatArrow(drawList, center, radius, MathF.PI * 0.59f, thickness, ink);
        RepeatArrow(drawList, center, radius, MathF.PI * 1.59f, thickness, ink);
    }

    private static void RepeatArc(ImDrawListPtr drawList, Vector2 center, float radius, float from, float to, uint ink,
        float thickness)
    {
        drawList.PathArcTo(center, radius, from, to, 20);
        drawList.PathStroke(ink, ImDrawFlags.None, thickness);
    }

    private static void RepeatArrow(ImDrawListPtr drawList, Vector2 center, float radius, float angle, float thickness,
        uint ink)
    {
        var radial = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var tangent = new Vector2(-MathF.Sin(angle), MathF.Cos(angle));
        var anchor = center + radial * radius;
        var head = MathF.Max(2.8f, thickness * 2f);
        drawList.AddTriangleFilled(anchor + tangent * head, anchor + radial * head, anchor - radial * head, ink);
    }
}
