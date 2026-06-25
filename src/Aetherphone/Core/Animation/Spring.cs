namespace Aetherphone.Core.Animation;

internal struct Spring
{
    public float Value;

    public float Velocity;

    public Spring(float value)
    {
        Value = value;
        Velocity = 0f;
    }

    public float Step(float target, float smoothTime, float deltaSeconds)
    {
        var clampedSmoothTime = MathF.Max(0.0001f, smoothTime);
        var omega = 2f / clampedSmoothTime;
        var x = omega * deltaSeconds;
        var decay = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
        var current = Value;
        var difference = current - target;
        var temp = (Velocity + omega * difference) * deltaSeconds;
        Velocity = (Velocity - omega * temp) * decay;
        var result = target + (difference + temp) * decay;

        if (target - current > 0f == result > target)
        {
            result = target;
            Velocity = 0f;
        }

        Value = result;
        return result;
    }

    public readonly bool IsResting(float target, float positionEpsilon, float velocityEpsilon)
    {
        return MathF.Abs(Value - target) <= positionEpsilon && MathF.Abs(Velocity) <= velocityEpsilon;
    }

    public void SnapTo(float value)
    {
        Value = value;
        Velocity = 0f;
    }
}
