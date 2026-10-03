using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Cabinets;

internal static class WheelChoreography
{
    public const float SpinSeconds = 4.2f;

    public const int SpinTurns = 4;

    public const float Tau = MathF.PI * 2f;

    public const float SegmentSpan = Tau / WheelRules.SegmentCount;

    private const float HandoffSlope = 5f;

    public static float SpanFor(int segmentCount)
    {
        return Tau / segmentCount;
    }

    public static int PegOf(float angle, int segmentCount)
    {
        return (int)MathF.Floor(angle / SpanFor(segmentCount) + 0.5f);
    }

    public static float RestAngleOf(int segment, int segmentCount)
    {
        return Normalize(-segment * SpanFor(segmentCount));
    }

    public static float SweepFor(float fromAngle, int segment, int segmentCount, int turns)
    {
        var advance = Normalize(RestAngleOf(segment, segmentCount) - fromAngle);
        return turns * Tau + advance;
    }

    public static float RestAngleOf(int segment)
    {
        return Normalize(-segment * SegmentSpan);
    }

    public static int SegmentUnderPointer(float angle)
    {
        var steps = (int)MathF.Round(-Normalize(angle) / SegmentSpan);
        var wrapped = steps % WheelRules.SegmentCount;
        return wrapped < 0 ? wrapped + WheelRules.SegmentCount : wrapped;
    }

    public static float Normalize(float angle)
    {
        var wrapped = angle - Tau * MathF.Floor(angle / Tau);
        return wrapped >= Tau ? 0f : wrapped;
    }

    public static float SweepFor(float fromAngle, int segment, int turns)
    {
        var advance = Normalize(RestAngleOf(segment) - fromAngle);
        return turns * Tau + advance;
    }

    public static float SweepFor(float fromAngle, int segment)
    {
        return SweepFor(fromAngle, segment, SpinTurns);
    }

    public static float Progress(float elapsedSeconds)
    {
        if (elapsedSeconds <= 0f)
        {
            return 0f;
        }

        return elapsedSeconds >= SpinSeconds ? 1f : elapsedSeconds / SpinSeconds;
    }

    public static float AngleAt(float fromAngle, float sweep, float elapsedSeconds)
    {
        if (elapsedSeconds >= SpinSeconds)
        {
            return fromAngle + sweep;
        }

        if (elapsedSeconds <= 0f)
        {
            return fromAngle + elapsedSeconds * (HandoffSlope * sweep / SpinSeconds);
        }

        return fromAngle + sweep * Easing.EaseOutQuint(elapsedSeconds / SpinSeconds);
    }
}
