using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class OnboardingHero
{
    private static readonly string[] OrbitApps =
    {
        "messages", "market", "skywatcher", "games", "aethergram", "camera",
    };

    private static readonly Vector2[] TwinkleOffsets =
    {
        new(-0.86f, -0.58f), new(0.92f, -0.30f), new(0.58f, 0.82f),
    };

    private static readonly Vector4 GlyphInk = new(1f, 1f, 1f, 1f);
    private const float StaggerSpan = 0.34f;
    private const double DriftPeriodMs = 30000.0;
    private const double RipplePeriodMs = 2800.0;

    public static void Draw(ImDrawListPtr drawList, Vector2 center, HeroMotif motif, Vector4 accent, float scale,
        float reveal, float alpha)
    {
        if (alpha <= 0.001f)
        {
            return;
        }

        var clampedReveal = Easing.Clamp01(reveal);
        var settle = Easing.EaseOutQuint(clampedReveal);
        Halo(drawList, center, accent, scale, settle, alpha);
        if (motif == HeroMotif.Care)
        {
            DrawCare(drawList, center, accent, scale, clampedReveal, settle, alpha);
            return;
        }

        DrawConstellation(drawList, center, accent, scale, clampedReveal, settle, alpha);
    }

    private static void DrawConstellation(ImDrawListPtr drawList, Vector2 center, Vector4 accent, float scale,
        float reveal, float settle, float alpha)
    {
        var orbitRadius = 46f * scale;
        var tileSize = 30f * scale;
        OrbitRing(drawList, center, orbitRadius, accent, scale, settle * alpha);
        var drift = Pulse.Phase(DriftPeriodMs) * MathF.PI * 2f;
        var count = OrbitApps.Length;
        var denominator = MathF.Max(1f - StaggerSpan, 0.001f);
        for (var index = 0; index < count; index++)
        {
            var stagger = index / (float)(count - 1) * StaggerSpan;
            var appear = Easing.EaseOutQuint(Easing.Clamp01((reveal - stagger) / denominator));
            if (appear <= 0.002f)
            {
                continue;
            }

            var angle = -MathF.PI / 2f + index * (MathF.PI * 2f / count) + drift;
            var bob = MathF.Sin(Pulse.Phase(3200.0 + index * 260.0) * MathF.PI * 2f) * 2.2f * scale * settle;
            var radius = orbitRadius * appear;
            var position = new Vector2(center.X + MathF.Cos(angle) * radius, center.Y + MathF.Sin(angle) * radius + bob);
            var size = tileSize * (0.4f + 0.6f * appear);
            Tile(drawList, position, size, OrbitApps[index], scale, appear * alpha);
        }

        Core(drawList, center, accent, scale, settle, alpha);
    }

    private static void DrawCare(ImDrawListPtr drawList, Vector2 center, Vector4 accent, float scale, float reveal,
        float settle, float alpha)
    {
        Ripples(drawList, center, accent, scale, settle * alpha);
        for (var index = 0; index < TwinkleOffsets.Length; index++)
        {
            var appear = Easing.EaseOutQuint(Easing.Clamp01((reveal - 0.35f - index * 0.08f) / 0.5f));
            if (appear <= 0.002f)
            {
                continue;
            }

            var twinkle = 0.35f + 0.65f * Pulse.Wave(1400.0 + index * 420.0);
            var position = new Vector2(center.X + TwinkleOffsets[index].X * 42f * scale,
                center.Y + TwinkleOffsets[index].Y * 42f * scale);
            Twinkle(drawList, position, 1.9f * scale, accent, twinkle * appear * alpha);
        }

        var beat = settle * (1f + 0.05f * Pulse.Wave(Pulse.Calm));
        Heart(drawList, center, 30f * scale * beat, accent, settle * alpha);
    }

    private static void Tile(ImDrawListPtr drawList, Vector2 center, float size, string id, float scale, float alpha)
    {
        var half = size * 0.5f;
        var min = new Vector2(center.X - half, center.Y - half);
        var max = new Vector2(center.X + half, center.Y + half);
        var radius = size * 0.26f;
        var surface = IconTile.Surface(AppAccents.For(id));
        Elevation.IconRest(drawList, min, max, radius, scale, alpha);
        IconTile.FillShaded(drawList, min, max, radius, surface, alpha);
        Material.EdgeSquircle(drawList, min, max, radius, scale, alpha);
        AppIconArt.TryDraw(drawList, id, center, size, GlyphInk with { W = alpha },
            Palette.Darken(surface, 0.25f) with { W = alpha });
    }

    private static void Heart(ImDrawListPtr drawList, Vector2 center, float size, Vector4 accent, float alpha)
    {
        if (size <= 0.5f || alpha <= 0.001f)
        {
            return;
        }

        var fill = ImGui.GetColorU32(Palette.WithAlpha(accent, alpha));
        var lobeRadius = size * 0.30f;
        var lobeY = center.Y - size * 0.16f;
        var leftLobe = new Vector2(center.X - size * 0.30f, lobeY);
        var rightLobe = new Vector2(center.X + size * 0.30f, lobeY);
        drawList.AddCircleFilled(leftLobe, lobeRadius, fill, 28);
        drawList.AddCircleFilled(rightLobe, lobeRadius, fill, 28);
        drawList.AddTriangleFilled(new Vector2(center.X - size * 0.58f, center.Y - size * 0.06f),
            new Vector2(center.X + size * 0.58f, center.Y - size * 0.06f),
            new Vector2(center.X, center.Y + size * 0.64f), fill);
        var sheen = Palette.WithAlpha(Palette.Lighten(accent, 0.55f), 0.5f * alpha);
        drawList.AddCircleFilled(new Vector2(leftLobe.X, leftLobe.Y - lobeRadius * 0.32f), lobeRadius * 0.34f,
            ImGui.GetColorU32(sheen), 16);
    }

    private static void Ripples(ImDrawListPtr drawList, Vector2 center, Vector4 accent, float scale, float alpha)
    {
        if (alpha <= 0.001f)
        {
            return;
        }

        const int count = 3;
        var startRadius = 16f * scale;
        var endRadius = 50f * scale;
        for (var index = 0; index < count; index++)
        {
            var phase = Frac(Pulse.Phase(RipplePeriodMs) + index / (float)count);
            var radius = startRadius + (endRadius - startRadius) * phase;
            var fade = (1f - phase) * 0.45f * alpha;
            drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(accent, fade)), 64, 1.5f * scale);
        }
    }

    private static void Twinkle(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 accent, float alpha)
    {
        if (alpha <= 0.001f)
        {
            return;
        }

        drawList.AddCircleFilled(center, radius * 2.1f, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.14f * alpha)), 16);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(Palette.Lighten(accent, 0.6f),
            alpha)), 12);
    }

    private static void Halo(ImDrawListPtr drawList, Vector2 center, Vector4 accent, float scale, float settle,
        float alpha)
    {
        var breath = Pulse.Wave(Pulse.Breath);
        var radius = (50f + 6f * breath) * scale * (0.72f + 0.28f * settle);
        var glow = alpha * settle;
        drawList.AddCircleFilled(center, radius * 1.62f, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.045f * glow)), 64);
        drawList.AddCircleFilled(center, radius * 1.18f, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.065f * glow)), 64);
        drawList.AddCircleFilled(center, radius * 0.80f, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.085f * glow)), 64);
    }

    private static void OrbitRing(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 accent, float scale,
        float alpha)
    {
        drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.14f * alpha)), 96, 1.3f * scale);
    }

    private static void Core(ImDrawListPtr drawList, Vector2 center, Vector4 accent, float scale, float settle,
        float alpha)
    {
        var pulse = Pulse.Wave(Pulse.Calm);
        var radius = (10f + 1.4f * pulse) * scale * settle;
        if (radius <= 0.2f)
        {
            return;
        }

        drawList.AddCircleFilled(center, radius * 2.0f, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.18f * alpha)), 40);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(accent, 0.95f * alpha)), 40);
        var core = Palette.Mix(accent, Vector4.One, 0.72f);
        drawList.AddCircleFilled(center, radius * 0.42f, ImGui.GetColorU32(Palette.WithAlpha(core, alpha)), 32);
    }

    private static float Frac(float value) => value - MathF.Floor(value);
}
