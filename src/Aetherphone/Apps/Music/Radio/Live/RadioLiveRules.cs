using Aetherphone.Core.Radio;

namespace Aetherphone.Apps.Music.Radio.Live;

internal enum RadioComposerLock : byte
{
    None,
    Suspended,
    SignedOut,
    Connecting,
    Unavailable,
    Muted,
}

internal enum RadioDurationUnit : byte
{
    Seconds,
    Minutes,
    HoursMinutes,
}

internal readonly record struct RadioDuration(RadioDurationUnit Unit, int Major, int Minor);

internal readonly record struct RadioReactionPose(float Rise, float Sway, float Alpha, float Scale);

internal static class RadioLiveRules
{
    public const int CounterThreshold = 40;
    public const int ReactionLanes = 4;

    // SplitMix64 multipliers (Steele, Lea and Flood 2014), used only to scatter reactions across lanes.
    private const ulong GoldenRatioMultiplier = 0x9E3779B97F4A7C15UL;
    private const ulong SplitMixMultiplier = 0xBF58476D1CE4E5B9UL;
    private const int LaneShift = 33;
    private const float RiseStiffness = 5.5f;
    private const float FadeInPortion = 0.12f;
    private const float FadeOutStart = 0.6f;
    private const float GrowPortion = 0.15f;
    private const float StartScale = 0.7f;
    private const float SwayCycles = 1.1f;
    private const float SwayFloor = 0.4f;
    private const float LanePhase = 1.7f;
    private const long MillisecondsPerSecond = 1000;
    private const int SecondsPerMinute = 60;
    private const int MinutesPerHour = 60;

    public static RadioComposerLock Evaluate(bool suspended, bool signedIn, RadioRoomStatus status, bool muted,
        bool canModerate)
    {
        if (suspended)
        {
            return RadioComposerLock.Suspended;
        }

        if (!signedIn)
        {
            return RadioComposerLock.SignedOut;
        }

        if (status == RadioRoomStatus.Unavailable)
        {
            return RadioComposerLock.Unavailable;
        }

        if (status != RadioRoomStatus.Attached)
        {
            return RadioComposerLock.Connecting;
        }

        return muted && !canModerate ? RadioComposerLock.Muted : RadioComposerLock.None;
    }

    public static RadioDuration Remaining(long untilUnixMs, long nowUnixMs)
    {
        var remainingMs = Math.Max(0L, untilUnixMs - nowUnixMs);
        var seconds = (int)Math.Min(int.MaxValue, (remainingMs + MillisecondsPerSecond - 1) / MillisecondsPerSecond);
        if (seconds < SecondsPerMinute)
        {
            return new RadioDuration(RadioDurationUnit.Seconds, seconds, 0);
        }

        var minutes = (seconds + SecondsPerMinute - 1) / SecondsPerMinute;
        if (minutes < MinutesPerHour)
        {
            return new RadioDuration(RadioDurationUnit.Minutes, minutes, 0);
        }

        return new RadioDuration(RadioDurationUnit.HoursMinutes, minutes / MinutesPerHour, minutes % MinutesPerHour);
    }

    public static bool ShowsCounter(int length, int maxLength)
    {
        return length >= maxLength - CounterThreshold;
    }

    public static bool IsLink(string text)
    {
        var trimmed = text.AsSpan().Trim();
        return trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
               || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
               || trimmed.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase)
               || trimmed.Contains("youtube.com/", StringComparison.OrdinalIgnoreCase);
    }

    public static int OrderRequests(RadioRequestEntry[] entries, int count, int[] order)
    {
        var filled = Math.Min(count, order.Length);
        for (var index = 0; index < filled; index++)
        {
            order[index] = index;
        }

        for (var index = 1; index < filled; index++)
        {
            var current = order[index];
            var probe = index - 1;
            while (probe >= 0 && Precedes(entries[current], entries[order[probe]]))
            {
                order[probe + 1] = order[probe];
                probe--;
            }

            order[probe + 1] = current;
        }

        return filled;
    }

    public static int ReactionLane(long atTick, int reaction)
    {
        var mixed = unchecked((ulong)atTick * GoldenRatioMultiplier + (ulong)reaction * SplitMixMultiplier);
        return (int)((mixed >> LaneShift) % ReactionLanes);
    }

    public static RadioReactionPose Pose(long ageMilliseconds, long lifetimeMilliseconds, int lane)
    {
        if (lifetimeMilliseconds <= 0)
        {
            return new RadioReactionPose(1f, 0f, 0f, 1f);
        }

        var progress = Math.Clamp((float)ageMilliseconds / lifetimeMilliseconds, 0f, 1f);
        var damped = RiseStiffness * progress;
        var rise = 1f - (1f + damped) * MathF.Exp(-damped);
        var alpha = progress < FadeInPortion
            ? progress / FadeInPortion
            : progress > FadeOutStart
                ? 1f - (progress - FadeOutStart) / (1f - FadeOutStart)
                : 1f;
        var scale = StartScale + (1f - StartScale) * MathF.Min(1f, progress / GrowPortion);
        var sway = MathF.Sin(progress * MathF.Tau * SwayCycles + lane * LanePhase)
                   * (SwayFloor + (1f - SwayFloor) * progress);
        return new RadioReactionPose(rise, sway, Math.Clamp(alpha, 0f, 1f), scale);
    }

    private static bool Precedes(RadioRequestEntry candidate, RadioRequestEntry other)
    {
        if (candidate.State != other.State)
        {
            return candidate.State == RadioRequestState.Accepted;
        }

        return candidate.CreatedAtUnixMs < other.CreatedAtUnixMs;
    }
}
