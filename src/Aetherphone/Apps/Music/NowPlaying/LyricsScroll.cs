using Aetherphone.Core.Lyrics;

namespace Aetherphone.Apps.Music.NowPlaying;

internal static class LyricsScroll
{
    public const float Anchor = 0.35f;
    public const float FollowPauseSeconds = 3f;

    public static float Target(float lineTop, float lineHeight, float viewportHeight, float contentHeight)
    {
        var target = lineTop + lineHeight * 0.5f - viewportHeight * Anchor;
        var maximum = MathF.Max(0f, contentHeight - viewportHeight * Anchor);
        return Math.Clamp(target, 0f, maximum);
    }

    public static float MaxOffset(float viewportHeight, float contentHeight) =>
        MathF.Max(0f, contentHeight - viewportHeight * Anchor);

    public static bool Following(float clock, float pausedUntil) => clock >= pausedUntil;

    public static int WordAt(ReadOnlySpan<LrcWord> words, double position, double lineEnd, out float fraction)
    {
        fraction = 0f;
        var current = -1;
        for (var wordIndex = 0; wordIndex < words.Length; wordIndex++)
        {
            if (words[wordIndex].Start > position)
            {
                break;
            }

            current = wordIndex;
        }

        if (current < 0)
        {
            return -1;
        }

        var start = words[current].Start;
        var end = current + 1 < words.Length ? words[current + 1].Start : lineEnd;
        fraction = end > start ? (float)Math.Clamp((position - start) / (end - start), 0d, 1d) : 1f;
        return current;
    }
}
