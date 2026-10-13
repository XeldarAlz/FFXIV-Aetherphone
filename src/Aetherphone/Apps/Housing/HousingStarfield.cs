using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Housing;

internal static class HousingStarfield
{
    private const int StarCount = 72;
    private const int Seed = 20261010;
    private const float MinRadius = 0.45f;
    private const float RadiusRange = 0.9f;
    private const float MinAlpha = 0.10f;
    private const float AlphaRange = 0.32f;
    private const float TopBias = 1.7f;
    private const float TwinkleDepth = 0.35f;
    private const float MinPeriod = 3.5f;
    private const float PeriodRange = 4f;
    private const float BrightShare = 0.12f;
    private const float BrightBoost = 1.6f;
    private const float GlowRadius = 2.6f;
    private const float GlowAlpha = 0.18f;

    private static readonly Vector4 StarInk = new(0.86f, 0.92f, 1f, 1f);
    private static readonly Star[] Stars = Build();

    private readonly record struct Star(Vector2 Position, float Radius, float Alpha, float Period, float Phase,
        bool Bright);

    public static void Draw(ImDrawListPtr drawList, Rect screen, float rounding, float scale)
    {
        var time = (float)ImGui.GetTime();
        for (var index = 0; index < Stars.Length; index++)
        {
            var star = Stars[index];
            var center = new Vector2(screen.Min.X + star.Position.X * screen.Width,
                screen.Min.Y + star.Position.Y * screen.Height);
            var radius = star.Radius * scale;
            if (!InsideCorners(center, screen, rounding, radius * GlowRadius))
            {
                continue;
            }

            var twinkle = 1f - TwinkleDepth * 0.5f * (1f + MathF.Sin(time * MathF.Tau / star.Period + star.Phase));
            var alpha = star.Alpha * twinkle;
            if (star.Bright)
            {
                drawList.AddCircleFilled(center, radius * GlowRadius,
                    ImGui.GetColorU32(Palette.WithAlpha(StarInk, alpha * GlowAlpha)), 12);
            }

            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(StarInk, alpha)), 8);
        }
    }

    private static bool InsideCorners(Vector2 point, Rect screen, float rounding, float margin)
    {
        var cornerOffsetX =
            MathF.Max(MathF.Max(screen.Min.X + rounding - point.X, point.X - (screen.Max.X - rounding)), 0f);
        var cornerOffsetY =
            MathF.Max(MathF.Max(screen.Min.Y + rounding - point.Y, point.Y - (screen.Max.Y - rounding)), 0f);
        var limit = MathF.Max(0f, rounding - margin);
        return cornerOffsetX * cornerOffsetX + cornerOffsetY * cornerOffsetY <= limit * limit;
    }

    private static Star[] Build()
    {
        var random = new Random(Seed);
        var stars = new Star[StarCount];
        for (var index = 0; index < stars.Length; index++)
        {
            var bright = random.NextSingle() < BrightShare;
            var x = random.NextSingle();
            var y = MathF.Pow(random.NextSingle(), TopBias);
            var radius = MinRadius + random.NextSingle() * RadiusRange;
            var alpha = MinAlpha + random.NextSingle() * AlphaRange;
            stars[index] = new Star(new Vector2(x, y), bright ? radius * BrightBoost : radius,
                bright ? MathF.Min(1f, alpha * BrightBoost) : alpha, MinPeriod + random.NextSingle() * PeriodRange,
                random.NextSingle() * MathF.Tau, bright);
        }

        return stars;
    }
}
