using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Aethergram;

/// <summary>
/// Aethergram's signature bespoke artwork: the multi-stop "story" gradient ring drawn around
/// avatars. Pure drawing; state and layout live in <see cref="AethergramApp"/>.
/// </summary>
internal static class AethergramArt
{
    private static readonly Vector4[] RingStops =
    [
        new(1f, 0.863f, 0.502f, 1f), new(0.969f, 0.435f, 0.216f, 1f), new(0.882f, 0.188f, 0.424f, 1f),
        new(0.514f, 0.227f, 0.706f, 1f),
    ];

    public static void StoryRing(ImDrawListPtr drawList, Vector2 center, float radius, float scale)
    {
        const int Segments = 40;
        var direction = Vector2.Normalize(new Vector2(1f, -1f));
        var step = MathF.Tau / Segments;
        var previous = center + new Vector2(radius, 0f);
        for (var index = 1; index <= Segments; index++)
        {
            var angle = step * index;
            var point = center + radius * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var normal = Vector2.Normalize((previous + point) * 0.5f - center);
            var position = Vector2.Dot(normal, direction) * 0.5f + 0.5f;
            drawList.AddLine(previous, point, ImGui.GetColorU32(RingColor(position)), 2.4f * scale);
            previous = point;
        }
    }

    private static Vector4 RingColor(float t)
    {
        var position = Math.Clamp(t, 0f, 1f) * (RingStops.Length - 1);
        var index = Math.Min((int)position, RingStops.Length - 2);
        return Vector4.Lerp(RingStops[index], RingStops[index + 1], position - index);
    }
}
