using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class TurnTimerRing
{
    public const float WarnFraction = 0.33f;

    public const int WarnSeconds = 8;

    private const float Top = -MathF.PI / 2f;
    private const float Thickness = 3.2f;
    private const float TrackAlpha = 0.16f;

    public static readonly Vector4 Warn = new(1f, 0.63f, 0.28f, 1f);

    public static float Fraction(long remainingMilliseconds, int windowSeconds)
    {
        if (windowSeconds <= 0 || remainingMilliseconds <= 0)
        {
            return 0f;
        }

        var fraction = remainingMilliseconds / (windowSeconds * 1000f);
        return fraction > 1f ? 1f : fraction;
    }

    public static bool IsUrgent(long remainingMilliseconds, int windowSeconds)
    {
        if (windowSeconds <= 0)
        {
            return false;
        }

        return remainingMilliseconds <= WarnSeconds * 1000L
            || Fraction(remainingMilliseconds, windowSeconds) <= WarnFraction;
    }

    public static void Draw(ImDrawListPtr drawList, Vector2 center, float radius, long remainingMilliseconds,
        int windowSeconds, Vector4 accent, float scale)
    {
        var thickness = Thickness * scale;
        var track = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, TrackAlpha));
        Arc(drawList, center, radius, thickness, Top, Top + MathF.PI * 2f, track);

        var fraction = Fraction(remainingMilliseconds, windowSeconds);
        if (fraction <= 0f)
        {
            return;
        }

        var urgent = IsUrgent(remainingMilliseconds, windowSeconds);
        var color = urgent ? Warn : accent;
        var alpha = urgent ? 0.55f + 0.45f * Pulse.Wave(Pulse.Fast) : 1f;
        Arc(drawList, center, radius, thickness, Top, Top + MathF.PI * 2f * fraction,
            ImGui.GetColorU32(Palette.WithAlpha(color, alpha)));
    }

    public static void Bar(ImDrawListPtr drawList, Vector2 center, float width, float height,
        long remainingMilliseconds, int windowSeconds, Vector4 accent)
    {
        var half = new Vector2(width * 0.5f, height * 0.5f);
        var min = center - half;
        var max = center + half;
        var radius = height * 0.5f;
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, TrackAlpha)));
        var fraction = Fraction(remainingMilliseconds, windowSeconds);
        var filled = width * fraction;
        if (filled < height)
        {
            return;
        }

        var urgent = IsUrgent(remainingMilliseconds, windowSeconds);
        var color = urgent ? Warn : accent;
        var alpha = urgent ? 0.55f + 0.45f * Pulse.Wave(Pulse.Fast) : 1f;
        Squircle.Fill(drawList, min, new Vector2(min.X + filled, max.Y), radius,
            ImGui.GetColorU32(Palette.WithAlpha(color, alpha)));
        drawList.AddCircleFilled(new Vector2(min.X + filled - radius, center.Y), radius * 1.6f,
            ImGui.GetColorU32(Palette.WithAlpha(color, 0.25f * alpha)), 16);
    }

    private static void Arc(ImDrawListPtr drawList, Vector2 center, float radius, float thickness, float from,
        float to, uint color)
    {
        var span = MathF.Abs(to - from);
        var segments = Math.Max(2, (int)MathF.Ceiling(span / (MathF.PI / 48f)));
        var previous = center + Direction(from) * radius;
        for (var index = 1; index <= segments; index++)
        {
            var angle = from + (to - from) * (index / (float)segments);
            var current = center + Direction(angle) * radius;
            drawList.AddLine(previous, current, color, thickness);
            previous = current;
        }

        var cap = thickness * 0.5f;
        drawList.AddCircleFilled(center + Direction(from) * radius, cap, color, 12);
        drawList.AddCircleFilled(center + Direction(to) * radius, cap, color, 12);
    }

    private static Vector2 Direction(float radians)
    {
        return new Vector2(MathF.Cos(radians), MathF.Sin(radians));
    }
}
