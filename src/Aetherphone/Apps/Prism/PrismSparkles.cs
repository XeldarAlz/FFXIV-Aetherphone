using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Prism;

internal static class PrismSparkles
{
    private const float Tau = MathF.PI * 2f;
    private const float InnerReach = 0.82f;
    private const float ReachSpread = 0.42f;
    private const float Drift = 0.12f;
    private const float PeriodSeconds = 2.4f;
    private const float PeriodSpread = 1.8f;
    private const float ShowShare = 0.38f;
    private const float SizeUnits = 3.2f;
    private const float SizeSpread = 2.6f;
    private const float WaistShare = 0.22f;
    private const float WhiteMix = 0.45f;
    private const float Faint = 0.02f;
    private const uint HashSeed = 2166136261u;
    private const uint HashPrime = 16777619u;
    private const float ByteScale = 1f / 255f;

    private static readonly Vector4 PearlOnDark = new(1f, 0.97f, 0.92f, 1f);
    private static readonly Vector4 PearlOnLight = new(0.62f, 0.64f, 0.70f, 1f);

    public static void Around(ImDrawListPtr drawList, Vector2 center, float radius, int seed, int count,
        PrismGlow glow, float alpha, float scale)
    {
        if (alpha <= Faint || count <= 0)
        {
            return;
        }

        var time = (float)ImGui.GetTime();
        var white = new Vector4(1f, 1f, 1f, 1f);
        var pearl = PrismArt.Light ? PearlOnLight : PearlOnDark;
        for (var index = 0; index < count; index++)
        {
            var hash = Hash(seed, index);
            var period = PeriodSeconds + Fraction(hash, 24) * PeriodSpread;
            var cycle = time / period + Fraction(hash, 16);
            cycle -= MathF.Floor(cycle);
            if (cycle >= ShowShare)
            {
                continue;
            }

            var twinkle = MathF.Sin(cycle / ShowShare * MathF.PI);
            if (twinkle <= Faint)
            {
                continue;
            }

            var angle = Fraction(hash, 0) * Tau;
            var reach = radius * (InnerReach + Fraction(hash, 8) * ReachSpread + cycle * Drift);
            var position = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * reach;
            var size = (SizeUnits + Fraction(hash, 4) * SizeSpread) * scale * twinkle;
            var tint = glow == PrismGlow.Spectrum
                ? Vector4.Lerp(PrismLight.Hues[index % PrismLight.Hues.Length], white, WhiteMix)
                : pearl;
            Star(drawList, position, size, Palette.Pack(tint with { W = alpha * twinkle }));
        }
    }

    private static void Star(ImDrawListPtr drawList, Vector2 center, float size, uint color)
    {
        var waist = size * WaistShare;
        drawList.AddQuadFilled(center + new Vector2(0f, -size), center + new Vector2(waist, 0f),
            center + new Vector2(0f, size), center + new Vector2(-waist, 0f), color);
        drawList.AddQuadFilled(center + new Vector2(-size, 0f), center + new Vector2(0f, -waist),
            center + new Vector2(size, 0f), center + new Vector2(0f, waist), color);
    }

    private static uint Hash(int seed, int index)
    {
        var hash = HashSeed;
        hash = (hash ^ (uint)seed) * HashPrime;
        hash = (hash ^ (uint)index) * HashPrime;
        hash ^= hash >> 13;
        hash *= HashPrime;
        return hash ^ (hash >> 16);
    }

    private static float Fraction(uint hash, int shift) => ((hash >> shift) & 0xFFu) * ByteScale;
}
