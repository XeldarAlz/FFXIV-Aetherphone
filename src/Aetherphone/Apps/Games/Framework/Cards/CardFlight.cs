using Aetherphone.Core.Animation;

namespace Aetherphone.Apps.Games.Framework.Cards;

internal readonly struct CardLanding
{
    public readonly int Card;
    public readonly int Tag;
    public readonly CardPose Pose;

    public CardLanding(int card, int tag, in CardPose pose)
    {
        Card = card;
        Tag = tag;
        Pose = pose;
    }
}

internal sealed class CardFlight
{
    public const int DefaultCapacity = 32;
    public const float DefaultSeconds = 0.36f;
    public const float DefaultArc = 0.12f;
    private const float FlipStart = 0.25f;
    private const float FlipSpan = 0.5f;

    private struct Tween
    {
        public int Card;
        public int Tag;
        public CardPose From;
        public CardPose To;
        public float Delay;
        public float Seconds;
        public float Elapsed;
        public float Arc;
        public bool Flip;
        public bool HideWhileWaiting;
    }

    private readonly Tween[] tweens;
    private readonly CardLanding[] landed;
    private int count;
    private int landedHead;
    private int landedCount;

    public CardFlight(int capacity = DefaultCapacity)
    {
        tweens = new Tween[Math.Max(1, capacity)];
        landed = new CardLanding[tweens.Length * 2];
    }

    public int Count => count;

    public bool Busy => count > 0;

    public void Launch(int card, in CardPose from, in CardPose to, float delay = 0f, bool flip = false, int tag = 0,
        float seconds = DefaultSeconds, float arc = DefaultArc, bool hideWhileWaiting = false)
    {
        if (count == tweens.Length)
        {
            Land(0);
        }

        tweens[count] = new Tween
        {
            Card = card,
            Tag = tag,
            From = from,
            To = to,
            Delay = MathF.Max(0f, delay),
            Seconds = MathF.Max(0.01f, seconds),
            Elapsed = 0f,
            Arc = arc,
            Flip = flip,
            HideWhileWaiting = hideWhileWaiting,
        };
        count++;
    }

    public void Advance(float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        var index = 0;
        while (index < count)
        {
            ref var tween = ref tweens[index];
            var step = deltaSeconds;
            if (tween.Delay > 0f)
            {
                var waited = MathF.Min(tween.Delay, step);
                tween.Delay -= waited;
                step -= waited;
            }

            tween.Elapsed += step;
            if (tween.Delay <= 0f && tween.Elapsed >= tween.Seconds)
            {
                Land(index);
                continue;
            }

            index++;
        }
    }

    public int Card(int index) => tweens[index].Card;

    public int Tag(int index) => tweens[index].Tag;

    public bool Waiting(int index) => tweens[index].Delay > 0f;

    public bool Visible(int index) => !tweens[index].HideWhileWaiting || tweens[index].Delay <= 0f;

    public float Progress(int index)
    {
        ref readonly var tween = ref tweens[index];
        return tween.Delay > 0f ? 0f : Math.Clamp(tween.Elapsed / tween.Seconds, 0f, 1f);
    }

    public CardPose Pose(int index)
    {
        ref readonly var tween = ref tweens[index];
        var progress = Progress(index);
        var eased = Easing.EaseOutCubic(progress);
        var distance = Vector2.Distance(tween.From.Center, tween.To.Center);
        var center = Vector2.Lerp(tween.From.Center, tween.To.Center, eased);
        center.Y -= MathF.Sin(eased * MathF.PI) * distance * tween.Arc;
        var width = tween.From.Width + (tween.To.Width - tween.From.Width) * eased;
        var angle = tween.From.Angle + (tween.To.Angle - tween.From.Angle) * eased;
        var turn = Math.Clamp((progress - FlipStart) / FlipSpan, 0f, 1f);
        var faceUp = turn < 0.5f ? tween.From.FaceUp : tween.To.FaceUp;
        var squash = tween.Flip && tween.From.FaceUp != tween.To.FaceUp ? MathF.Abs(MathF.Cos(turn * MathF.PI)) : 1f;
        return new CardPose(center, width, angle, faceUp, squash);
    }

    public bool TryTakeLanded(out CardLanding landing)
    {
        if (landedCount == 0)
        {
            landing = default;
            return false;
        }

        landing = landed[landedHead];
        landedHead = (landedHead + 1) % landed.Length;
        landedCount--;
        return true;
    }

    public void Clear()
    {
        count = 0;
        landedHead = 0;
        landedCount = 0;
    }

    private void Land(int index)
    {
        ref readonly var tween = ref tweens[index];
        var slot = (landedHead + landedCount) % landed.Length;
        landed[slot] = new CardLanding(tween.Card, tween.Tag, tween.To);
        if (landedCount == landed.Length)
        {
            landedHead = (landedHead + 1) % landed.Length;
        }
        else
        {
            landedCount++;
        }

        for (var next = index + 1; next < count; next++)
        {
            tweens[next - 1] = tweens[next];
        }

        count--;
    }
}
