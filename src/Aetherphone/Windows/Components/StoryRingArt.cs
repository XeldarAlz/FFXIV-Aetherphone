using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class StoryRingArt
{
    private const int Segments = 40;

    public static void Sweep(ImDrawListPtr drawList, Vector2 center, float radius, float scale, bool unseen,
        Vector4[] stops, Vector4 seenTint)
    {
        var thickness = (unseen ? 2.4f : 1.6f) * scale;
        if (!unseen)
        {
            drawList.AddCircle(center, radius, ImGui.GetColorU32(seenTint), Segments, thickness);
            return;
        }

        var direction = Vector2.Normalize(new Vector2(1f, -1f));
        var step = MathF.Tau / Segments;
        var previous = center + new Vector2(radius, 0f);
        for (var index = 1; index <= Segments; index++)
        {
            var angle = step * index;
            var point = center + radius * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var normal = Vector2.Normalize((previous + point) * 0.5f - center);
            var position = Vector2.Dot(normal, direction) * 0.5f + 0.5f;
            drawList.AddLine(previous, point, ImGui.GetColorU32(ColorAt(stops, position)), thickness);
            previous = point;
        }
    }

    private static Vector4 ColorAt(Vector4[] stops, float amount)
    {
        var position = Math.Clamp(amount, 0f, 1f) * (stops.Length - 1);
        var index = Math.Min((int)position, stops.Length - 2);
        return Vector4.Lerp(stops[index], stops[index + 1], position - index);
    }
}
