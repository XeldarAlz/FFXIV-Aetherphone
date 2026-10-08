namespace Aetherphone.Apps.Casino.Cabinets;

internal struct WheelPointer
{
    public const float Stiffness = 320f;
    public const float Damping = 11f;
    public const float MaxDeflection = 0.6f;
    public const float KickGain = 0.6f;
    public const float MaxKick = 9f;
    public const float StepSeconds = 1f / 120f;

    private const float RestAngle = 0.0005f;
    private const float RestVelocity = 0.005f;

    public float Deflection { get; private set; }

    public float Velocity { get; private set; }

    public readonly bool Resting => Deflection == 0f && Velocity == 0f;

    public static float KickFor(float wheelRadiansPerSecond) =>
        MathF.Min(MaxKick, MathF.Abs(wheelRadiansPerSecond) * KickGain);

    public void Kick(float wheelRadiansPerSecond)
    {
        Velocity += KickFor(wheelRadiansPerSecond);
    }

    public void Step(float deltaSeconds)
    {
        var remaining = deltaSeconds;
        while (remaining > 0f)
        {
            var step = MathF.Min(remaining, StepSeconds);
            Velocity += (-Stiffness * Deflection - Damping * Velocity) * step;
            Deflection += Velocity * step;
            if (Deflection > MaxDeflection)
            {
                Deflection = MaxDeflection;
                Velocity = MathF.Min(Velocity, 0f);
            }
            else if (Deflection < -MaxDeflection)
            {
                Deflection = -MaxDeflection;
                Velocity = MathF.Max(Velocity, 0f);
            }

            remaining -= step;
        }

        if (MathF.Abs(Deflection) < RestAngle && MathF.Abs(Velocity) < RestVelocity)
        {
            Deflection = 0f;
            Velocity = 0f;
        }
    }

    public void Reset()
    {
        Deflection = 0f;
        Velocity = 0f;
    }
}
