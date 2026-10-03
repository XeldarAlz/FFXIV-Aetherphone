namespace Aetherphone.Core.Jam;

internal readonly record struct JamDriftDecision(bool RateChanged, float Rate, bool Seek, double SeekTarget)
{
    internal static readonly JamDriftDecision Hold = new(false, 1f, false, 0d);
}

internal sealed class JamDriftController
{
    internal const double DeadZoneSeconds = 0.2;
    internal const double ReleaseSeconds = 0.06;
    internal const double FullNudgeSeconds = 1.5;
    internal const double HardSeekSeconds = 1.5;
    internal const float MinimumRateDeviation = 0.01f;
    internal const float MaximumRateDeviation = 0.03f;
    internal const float RateStep = 0.0025f;
    internal const double SeekSettleSeconds = 3.0;
    internal const double EndGuardSeconds = 1.0;

    private float appliedRate = 1f;
    private double settleRemaining;
    private bool correcting;

    internal float AppliedRate => appliedRate;
    internal bool Correcting => correcting;

    internal void Reset()
    {
        appliedRate = 1f;
        settleRemaining = 0d;
        correcting = false;
    }

    internal bool Release()
    {
        settleRemaining = 0d;
        correcting = false;
        if (appliedRate == 1f)
        {
            return false;
        }

        appliedRate = 1f;
        return true;
    }

    internal JamDriftDecision Step(double localPosition, double targetPosition, double durationSeconds, bool holding,
        float deltaSeconds)
    {
        settleRemaining = Math.Max(0d, settleRemaining - deltaSeconds);
        if (holding || settleRemaining > 0d)
        {
            return JamDriftDecision.Hold;
        }

        if (durationSeconds > 0d && targetPosition >= durationSeconds - EndGuardSeconds)
        {
            correcting = false;
            return ApplyRate(1f);
        }

        var drift = localPosition - targetPosition;
        var magnitude = Math.Abs(drift);
        if (magnitude > HardSeekSeconds)
        {
            correcting = false;
            settleRemaining = SeekSettleSeconds;
            var rateChanged = appliedRate != 1f;
            appliedRate = 1f;
            return new JamDriftDecision(rateChanged, 1f, true, Math.Max(0d, targetPosition));
        }

        var threshold = correcting ? ReleaseSeconds : DeadZoneSeconds;
        if (magnitude <= threshold)
        {
            correcting = false;
            return ApplyRate(1f);
        }

        correcting = true;
        var share = (float)Math.Min(1d, (magnitude - ReleaseSeconds) / (FullNudgeSeconds - ReleaseSeconds));
        var deviation = Math.Max(MinimumRateDeviation, share * MaximumRateDeviation);
        var desired = drift > 0d ? 1f - deviation : 1f + deviation;
        return ApplyRate(Quantize(desired));
    }

    private JamDriftDecision ApplyRate(float rate)
    {
        if (Math.Abs(rate - appliedRate) < RateStep / 2f)
        {
            return JamDriftDecision.Hold;
        }

        appliedRate = rate;
        return new JamDriftDecision(true, rate, false, 0d);
    }

    private static float Quantize(float rate) => MathF.Round(rate / RateStep) * RateStep;
}
