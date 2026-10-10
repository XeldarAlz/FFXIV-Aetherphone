using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class Spooks
{
    private const int FlockSize = 3;
    private const int GhostArcSegments = 12;
    private const int GhostHemSegments = 18;
    private const int GhostPointCount = GhostArcSegments + GhostHemSegments + 2;
    private const int EyeSegments = 10;

    public static readonly Vector4 Pumpkin = new(1f, 0.55f, 0.16f, 1f);
    public static readonly Vector4 BatShadow = new(0.03f, 0.02f, 0.06f, 1f);

    public static void DrawFlight(ImDrawListPtr drawList, Rect frame, double seconds, double periodSeconds,
        float window, float laneTop, float laneSpan, float size, Vector4 ink)
    {
        var cycle = (float)Fraction(seconds / periodSeconds);
        if (cycle > window)
        {
            return;
        }

        var travel = cycle / window;
        var flight = (int)(seconds / periodSeconds);
        var leftward = Hash(flight, 2.3f) < 0.5f;
        var laneY = laneTop + Hash(flight, 5.9f) * laneSpan;
        for (var batIndex = 0; batIndex < FlockSize; batIndex++)
        {
            var along = travel * 1.4f - 0.2f - batIndex * 0.08f;
            var fractionX = leftward ? 1f - along : along;
            var offsetY = (batIndex % 2 == 0 ? 1f : -1f) * batIndex * 0.035f;
            var fractionY = laneY + offsetY + MathF.Sin((along * 3f + batIndex) * MathF.PI) * 0.025f;
            var flap = MathF.Sin((float)(seconds * (11.0 + batIndex * 1.7)));
            var center = new Vector2(frame.Min.X + fractionX * frame.Width, frame.Min.Y + fractionY * frame.Height);
            NightScene.DrawBat(drawList, center, size * (1f - batIndex * 0.18f), flap, ImGui.GetColorU32(ink));
        }
    }

    public static void DrawGhost(ImDrawListPtr drawList, Vector2 center, float size, uint fill, uint rim, uint eyes,
        float sway, float gaze)
    {
        Span<Vector2> outline = stackalloc Vector2[GhostPointCount];
        TraceGhost(outline, center, size, sway);
        var hub = center + new Vector2(0f, size * 0.4f);
        var flags = drawList.Flags;
        drawList.Flags = flags & ~ImDrawListFlags.AntiAliasedFill;
        for (var pointIndex = 0; pointIndex < outline.Length; pointIndex++)
        {
            drawList.AddTriangleFilled(hub, outline[pointIndex], outline[(pointIndex + 1) % outline.Length], fill);
        }

        drawList.Flags = flags;
        drawList.PathClear();
        for (var pointIndex = 0; pointIndex < outline.Length; pointIndex++)
        {
            drawList.PathLineTo(outline[pointIndex]);
        }

        drawList.PathStroke(rim, ImDrawFlags.Closed, MathF.Max(1f, size * 0.07f));
        var eyeRadius = size * 0.14f;
        drawList.AddCircleFilled(center + new Vector2((gaze - 0.32f) * size, -0.05f * size), eyeRadius, eyes,
            EyeSegments);
        drawList.AddCircleFilled(center + new Vector2((gaze + 0.32f) * size, -0.05f * size), eyeRadius, eyes,
            EyeSegments);
    }

    private static void TraceGhost(Span<Vector2> outline, Vector2 center, float size, float sway)
    {
        for (var step = 0; step <= GhostArcSegments; step++)
        {
            var angle = MathF.PI + MathF.PI * step / GhostArcSegments;
            outline[step] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * size;
        }

        var hemY = center.Y + size * 1.15f;
        for (var step = 0; step <= GhostHemSegments; step++)
        {
            var across = step / (float)GhostHemSegments;
            var ripple = MathF.Abs(MathF.Sin(across * MathF.PI * 3f + sway)) * size * 0.28f;
            outline[GhostArcSegments + 1 + step] = new Vector2(center.X + size * (1f - 2f * across), hemY + ripple);
        }
    }

    private static double Fraction(double value) => value - Math.Floor(value);

    private static float Hash(int index, float salt)
    {
        var value = MathF.Sin(index * 127.1f + salt) * 43758.547f;
        return value - MathF.Floor(value);
    }
}
