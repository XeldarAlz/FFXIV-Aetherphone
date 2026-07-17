using System.Numerics;

namespace Aetherphone.Core.Animation;

internal readonly struct BubblePop
{
    public readonly float Pop;
    public readonly float Alpha;
    public readonly Vector2 Rise;
    public readonly Vector2 Anchor;

    private BubblePop(float pop, float alpha, Vector2 rise, Vector2 anchor)
    {
        Pop = pop;
        Alpha = alpha;
        Rise = rise;
        Anchor = anchor;
    }

    public static BubblePop For(float entrance, float scale, Vector2 anchor)
    {
        if (entrance >= 1f)
        {
            return new BubblePop(1f, 1f, Vector2.Zero, anchor);
        }

        var pop = 0.80f + 0.20f * Easing.EaseOutQuint(entrance);
        var alpha = MathF.Min(entrance * 1.8f, 1f);
        var rise = new Vector2(0f, (1f - Easing.EaseOutCubic(entrance)) * 10f * scale);
        return new BubblePop(pop, alpha, rise, anchor);
    }

    public Vector2 Apply(Vector2 point) => Anchor + (point - Anchor) * Pop + Rise;
}
