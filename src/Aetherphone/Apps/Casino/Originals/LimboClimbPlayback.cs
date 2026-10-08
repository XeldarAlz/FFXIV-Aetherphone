using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Originals;

internal sealed class LimboClimbPlayback
{
    public const float MinSeconds = 0.4f;
    public const float MaxSeconds = 1.6f;
    public const float SecondsPerDecade = 0.3f;
    public const int Steps = 60;

    private float elapsed;
    private float seconds = MinSeconds;
    private bool climbing;
    private bool settlePending;
    private bool hasResult;
    private int result = OriginalsRules.LimboMinResult;
    private int target;
    private bool won;
    private int lastStep;
    private OriginalsOutcome outcome;

    public bool Climbing => climbing;

    public bool HasResult => hasResult;

    public int Result => result;

    public int Target => target;

    public bool Won => won;

    public float Progress => climbing ? Math.Clamp(elapsed / seconds, 0f, 1f) : 1f;

    public int Display => DisplayAt(result, Progress);

    public static float DurationFor(int result)
    {
        var decades = MathF.Log10(MathF.Max(1f, result / 100f));
        return Math.Clamp(MinSeconds + decades * SecondsPerDecade, MinSeconds, MaxSeconds);
    }

    public static int DisplayAt(int result, float progress)
    {
        if (progress >= 1f)
        {
            return result;
        }

        var eased = Easing.EaseOutCubic(progress);
        var stepped = MathF.Floor(eased * Steps) / Steps;
        var ratio = MathF.Max(1f, result / (float)OriginalsRules.LimboMinResult);
        var value = OriginalsRules.LimboMinResult * MathF.Pow(ratio, stepped);
        return Math.Clamp((int)value, OriginalsRules.LimboMinResult, result);
    }

    public static bool IsValid(CasinoLimboDto dto) =>
        dto.Granted && dto.RoundId.Length > 0 && dto.Result >= OriginalsRules.LimboMinResult
        && dto.Result <= OriginalsRules.LimboMaxTarget;

    public bool Begin(CasinoLimboDto dto, bool instant)
    {
        if (!IsValid(dto))
        {
            return false;
        }

        result = dto.Result;
        target = dto.Target;
        won = dto.Won;
        hasResult = true;
        outcome = new OriginalsOutcome(dto.Stake, dto.Payout, dto.RoundId);
        settlePending = true;
        seconds = DurationFor(result);
        elapsed = 0f;
        lastStep = 0;
        climbing = true;
        if (instant)
        {
            Snap();
        }

        return true;
    }

    public void Advance(float deltaSeconds)
    {
        if (!climbing)
        {
            return;
        }

        elapsed += deltaSeconds;
        if (elapsed >= seconds)
        {
            Snap();
        }
    }

    public void Snap()
    {
        elapsed = seconds;
        climbing = false;
        lastStep = Steps;
    }

    public bool TakeTick()
    {
        if (!climbing)
        {
            return false;
        }

        var step = (int)(Easing.EaseOutCubic(Progress) * Steps / 4f);
        if (step == lastStep)
        {
            return false;
        }

        lastStep = step;
        return true;
    }

    public bool TakeSettled(out OriginalsOutcome settled)
    {
        settled = default;
        if (!settlePending || climbing)
        {
            return false;
        }

        settlePending = false;
        settled = outcome;
        return true;
    }

    public void Clear()
    {
        elapsed = 0f;
        climbing = false;
        settlePending = false;
        hasResult = false;
        result = OriginalsRules.LimboMinResult;
        won = false;
    }
}
