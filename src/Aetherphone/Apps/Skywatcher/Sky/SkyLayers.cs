using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Skywatcher.Sky;

internal static class SkyLayers
{
    private const int FallbackGlowRings = 6;
    private const float MinimumAlpha = 0.002f;

    public static double Seconds => Environment.TickCount64 / 1000.0;

    public static float Wave(double periodSeconds, float offset = 0f) =>
        0.5f + 0.5f * MathF.Sin((float)(Frac(Seconds / periodSeconds + offset) * Math.PI * 2.0));

    public static float Phase(double periodSeconds, float offset = 0f) =>
        (float)Frac(Seconds / periodSeconds + offset);

    public static void Textured(in SkyCanvas canvas, SkyTexture texture, float top, float bottom, Vector2 tile,
        Vector2 drift, Vector2 seed, Vector4 tint, float alphaTop, float alphaBottom, float shear = 0f)
    {
        var topAlpha = alphaTop * canvas.Opacity;
        var bottomAlpha = alphaBottom * canvas.Opacity;
        if (topAlpha <= MinimumAlpha && bottomAlpha <= MinimumAlpha)
        {
            return;
        }

        if (!SkyTextures.TryGet(texture, out var handle))
        {
            return;
        }

        var min = new Vector2(canvas.Bounds.Min.X, canvas.Y(top));
        var max = new Vector2(canvas.Bounds.Max.X, canvas.Y(bottom));
        if (max.Y - min.Y < 1f)
        {
            return;
        }

        var scroll = new Vector2((float)Frac(Seconds * drift.X + seed.X), (float)Frac(Seconds * drift.Y + seed.Y));
        var map = TextureMap.Tiled(canvas.Bounds.Min, tile * canvas.Width, scroll, shear);
        Squircle.FillImageMapped(canvas.DrawList, min, max, canvas.Rounding, handle, map, Color(tint, topAlpha),
            Color(tint, bottomAlpha));
    }

    public static void Wash(in SkyCanvas canvas, float top, float bottom, Vector4 tint, float alphaTop,
        float alphaBottom)
    {
        var topAlpha = alphaTop * canvas.Opacity;
        var bottomAlpha = alphaBottom * canvas.Opacity;
        if (topAlpha <= MinimumAlpha && bottomAlpha <= MinimumAlpha)
        {
            return;
        }

        var min = new Vector2(canvas.Bounds.Min.X, canvas.Y(top));
        var max = new Vector2(canvas.Bounds.Max.X, canvas.Y(bottom));
        Squircle.FillVerticalGradient(canvas.DrawList, min, max, canvas.Rounding, Color(tint, topAlpha),
            Color(tint, bottomAlpha));
    }

    private static readonly Vector2 GlowUvMin = new(0.5f - SkyTextures.GlowExtent);
    private static readonly Vector2 GlowUvMax = new(0.5f + SkyTextures.GlowExtent);
    private static readonly Vector2 GlowUvCenter = new(0.5f);
    private static readonly Vector2 EdgeInset = new(0.5f);

    public static void Glow(in SkyCanvas canvas, Vector2 center, Vector2 radii, Vector4 color, float alpha)
    {
        if (alpha <= MinimumAlpha || radii.X < 0.5f || radii.Y < 0.5f)
        {
            return;
        }

        var bounds = canvas.Bounds;
        var min = center - radii;
        var max = center + radii;
        if (!ReachesCorner(bounds, min, max, canvas.Rounding) ||
            !SkyTextures.TryGet(SkyTexture.Glow, out var handle))
        {
            Glow(canvas.DrawList, center, radii, color, alpha);
            return;
        }

        var clippedMin = Vector2.Max(min, bounds.Min + EdgeInset);
        var clippedMax = Vector2.Min(max, bounds.Max - EdgeInset);
        if (clippedMax.X - clippedMin.X < 1f || clippedMax.Y - clippedMin.Y < 1f)
        {
            return;
        }

        var map = new TextureMap(center, GlowUvCenter, new Vector2(SkyTextures.GlowExtent / radii.X, 0f),
            new Vector2(0f, SkyTextures.GlowExtent / radii.Y));
        var tint = Color(color, alpha);
        Squircle.FillImageMapped(canvas.DrawList, clippedMin, clippedMax, canvas.Rounding, handle, map, tint, tint);
    }

    public static void Glow(ImDrawListPtr drawList, Vector2 center, Vector2 radii, Vector4 color, float alpha)
    {
        if (alpha <= MinimumAlpha || radii.X < 0.5f || radii.Y < 0.5f)
        {
            return;
        }

        if (SkyTextures.TryGet(SkyTexture.Glow, out var handle))
        {
            drawList.AddImage(handle, center - radii, center + radii, GlowUvMin, GlowUvMax, Color(color, alpha));
            return;
        }

        var radius = MathF.Min(radii.X, radii.Y);
        for (var ringIndex = 0; ringIndex < FallbackGlowRings; ringIndex++)
        {
            var fraction = (ringIndex + 1f) / FallbackGlowRings;
            drawList.AddCircleFilled(center, radius * fraction, Color(color, alpha * 0.22f * (1f - fraction * 0.8f)),
                32);
        }
    }

    public static void Beam(ImDrawListPtr drawList, Vector2 origin, float angle, float length, float width,
        Vector4 color, float alpha)
    {
        if (alpha <= MinimumAlpha || !SkyTextures.TryGet(SkyTexture.Glow, out var handle))
        {
            return;
        }

        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var normal = new Vector2(-direction.Y, direction.X) * (width * 0.5f);
        var tip = origin + direction * length;
        drawList.AddImageQuad(handle, origin - normal, origin + normal, tip + normal, tip - normal,
            new Vector2(0.5f, GlowUvMin.Y), new Vector2(0.5f, GlowUvMax.Y), GlowUvMax,
            new Vector2(GlowUvMax.X, GlowUvMin.Y),
            Color(color, alpha));
    }

    private static bool ReachesCorner(Rect bounds, Vector2 min, Vector2 max, float rounding)
    {
        var nearSide = min.X < bounds.Min.X + rounding || max.X > bounds.Max.X - rounding;
        var nearEnd = min.Y < bounds.Min.Y + rounding || max.Y > bounds.Max.Y - rounding;
        return nearSide && nearEnd;
    }

    public static uint Color(Vector4 color, float alpha) =>
        ImGui.GetColorU32(color with { W = Math.Clamp(color.W * alpha, 0f, 1f) });

    public static double Frac(double value) => value - Math.Floor(value);

    public static float Frac(float value) => value - MathF.Floor(value);

    public static float Hash(int index, float salt) => Frac(MathF.Sin(index * 127.1f + salt) * 43758.547f);
}
