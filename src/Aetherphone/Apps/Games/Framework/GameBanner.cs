using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Framework;

internal static class GameBanner
{
    private const float PopFraction = 0.18f;
    private const float FadeFraction = 0.25f;
    private const float PaddingX = 22f;
    private const float PaddingY = 10f;

    public static float HalfHeight(in TextStyle style) =>
        Typography.LineHeight(style) * 0.5f + PaddingY * UiScale.Current;

    public static float Advance(float progress, float deltaSeconds, float lifetimeSeconds)
    {
        if (progress >= 1f)
        {
            return 1f;
        }

        return MathF.Min(1f, progress + deltaSeconds / lifetimeSeconds);
    }

    public static void Draw(ImDrawListPtr drawList, Vector2 center, string text, Vector4 accent, PhoneTheme theme,
        float progress)
    {
        Draw(drawList, center, text, accent, theme, progress, TextStyles.Title2);
    }

    public static void Draw(ImDrawListPtr drawList, Vector2 center, string text, Vector4 accent, PhoneTheme theme,
        float progress, in TextStyle style)
    {
        if (progress <= 0f || progress >= 1f)
        {
            return;
        }

        var scale = UiScale.Current;
        var pop = progress < PopFraction ? GameJuice.PopIn(progress / PopFraction) : 1f;
        var alpha = progress > 1f - FadeFraction ? (1f - progress) / FadeFraction : 1f;
        var textScale = style.Scale * MathF.Max(0.01f, pop);
        var textSize = Typography.Measure(text, textScale, style.Weight);
        var padding = new Vector2(PaddingX * scale, PaddingY * scale);
        var half = textSize * 0.5f + padding;
        var min = center - half;
        var max = center + half;
        var radius = half.Y;
        ProgressRing.Glow(center, radius * 1.6f, accent, 0.45f * alpha);
        Material.Frosted(drawList, min, max, radius, scale, 0.92f * alpha);
        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(accent with { W = 0.7f * alpha }), 1.5f * scale);
        Typography.DrawCentered(drawList, center, text, StageInks.Strong with { W = alpha }, textScale, style.Weight);
    }
}
