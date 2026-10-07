using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Swoop;

internal static class SwoopShapes
{
    private const int GlowLayers = 4;

    public static void Glow(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color, float intensity)
    {
        if (intensity <= 0f || radius <= 0f)
        {
            return;
        }

        for (var layer = GlowLayers; layer >= 1; layer--)
        {
            var layerRadius = radius * layer / GlowLayers;
            var alpha = intensity * 0.11f * (GlowLayers + 1 - layer) / GlowLayers;
            drawList.AddCircleFilled(center, layerRadius, ImGui.GetColorU32(color with { W = color.W * alpha }), 24);
        }
    }

    public static Vector2 Rotate(Vector2 local, float cosine, float sine) =>
        new(local.X * cosine - local.Y * sine, local.X * sine + local.Y * cosine);

    public static Vector4 Multiply(Vector4 color, Vector4 tint) =>
        new(color.X * tint.X, color.Y * tint.Y, color.Z * tint.Z, color.W);

    public static Vector4 Mix(Vector4 from, Vector4 to, float amount) => Vector4.Lerp(from, to, Math.Clamp(amount, 0f, 1f));

    public static float Smooth(float edge0, float edge1, float value)
    {
        var progress = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return progress * progress * (3f - 2f * progress);
    }

    public static float Fraction(float value) => value - MathF.Floor(value);
}
