using Aetherphone.Core.Games;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Games.Framework;

internal static class StarRow
{
    private const float GapFraction = 0.2f;
    private const float GlowAlpha = 0.35f;

    public static float Width(float starSize) =>
        GameStatsStore.MaxStars * starSize + (GameStatsStore.MaxStars - 1) * starSize * GapFraction;

    public static void Draw(ImDrawListPtr drawList, Vector2 center, float starSize, int earned, Vector4 emptyInk,
        float alpha, float reveal = 1f)
    {
        var step = starSize * (1f + GapFraction);
        var left = center.X - (GameStatsStore.MaxStars - 1) * step * 0.5f;
        for (var star = 0; star < GameStatsStore.MaxStars; star++)
        {
            var position = new Vector2(left + star * step, center.Y);
            ProgressRing.CenterIcon(drawList, position, FontAwesomeIcon.Star, emptyInk with { W = emptyInk.W * alpha },
                starSize);
            if (star >= earned)
            {
                continue;
            }

            var phase = GameJuice.Stagger(reveal, star, GameStatsStore.MaxStars);
            if (phase <= 0f)
            {
                continue;
            }

            var size = starSize * MathF.Max(0.01f, GameJuice.PopIn(phase));
            if (reveal < 1f)
            {
                ProgressRing.Glow(position, size * 0.9f, GamePalette.Star, GlowAlpha * alpha * phase);
            }

            ProgressRing.CenterIcon(drawList, position, FontAwesomeIcon.Star,
                GamePalette.Star with { W = alpha * MathF.Min(1f, phase * 2f) }, size);
        }
    }
}
