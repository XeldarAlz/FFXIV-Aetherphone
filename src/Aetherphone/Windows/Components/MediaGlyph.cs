using System.Numerics;
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
        drawList.AddTriangleFilled(
            new Vector2(center.X - size * 0.7f, center.Y - size),
            new Vector2(center.X - size * 0.7f, center.Y + size),
            new Vector2(center.X + size, center.Y),
            ink);
    }

    public static void Pause(ImDrawListPtr drawList, Vector2 center, float size, uint ink)
    {
        var barWidth = size * 0.34f;
        var gap = size * 0.32f;
        drawList.AddRectFilled(new Vector2(center.X - gap - barWidth, center.Y - size), new Vector2(center.X - gap, center.Y + size), ink, barWidth * 0.3f);
        drawList.AddRectFilled(new Vector2(center.X + gap, center.Y - size), new Vector2(center.X + gap + barWidth, center.Y + size), ink, barWidth * 0.3f);
    }

    public static void Next(ImDrawListPtr drawList, Vector2 center, float size, uint ink)
    {
        drawList.AddTriangleFilled(
            new Vector2(center.X - size * 0.85f, center.Y - size),
            new Vector2(center.X - size * 0.85f, center.Y + size),
            new Vector2(center.X + size * 0.45f, center.Y),
            ink);
        drawList.AddRectFilled(new Vector2(center.X + size * 0.5f, center.Y - size), new Vector2(center.X + size * 0.8f, center.Y + size), ink, size * 0.2f);
    }

    public static void Previous(ImDrawListPtr drawList, Vector2 center, float size, uint ink)
    {
        drawList.AddTriangleFilled(
            new Vector2(center.X + size * 0.85f, center.Y - size),
            new Vector2(center.X + size * 0.85f, center.Y + size),
            new Vector2(center.X - size * 0.45f, center.Y),
            ink);
        drawList.AddRectFilled(new Vector2(center.X - size * 0.8f, center.Y - size), new Vector2(center.X - size * 0.5f, center.Y + size), ink, size * 0.2f);
    }
}
