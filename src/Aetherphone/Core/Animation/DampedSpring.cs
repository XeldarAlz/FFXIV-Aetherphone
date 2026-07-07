namespace Aetherphone.Core.Animation;

internal struct DampedSpring
{
    private const float SubStepSeconds = 1f / 240f;

    public float Value;
    public float Velocity;

    public DampedSpring(float value)
    {
        Value = value;
        Velocity = 0f;
    }

    public float Step(float target, float frequency, float dampingRatio, float deltaSeconds)
    {
        var omega = 2f * MathF.PI * MathF.Max(0.01f, frequency);
        var remaining = MathF.Min(deltaSeconds, TransitionTiming.MaxFrameSeconds);
        while (remaining > 0f)
        {
            var step = MathF.Min(SubStepSeconds, remaining);
            var acceleration = omega * omega * (target - Value) - 2f * dampingRatio * omega * Velocity;
            Velocity += acceleration * step;
            Value += Velocity * step;
            remaining -= step;
        }

        return Value;
    }

    public readonly bool IsResting(float target, float positionEpsilon, float velocityEpsilon) =>
        MathF.Abs(Value - target) <= positionEpsilon && MathF.Abs(Velocity) <= velocityEpsilon;

    public void SnapTo(float value)
    {
        Value = value;
        Velocity = 0f;
    }
}
