namespace Aetherphone.Core.Animation;

internal static class Pulse
{
    public const double Fast = 600.0;
    public const double Medium = 800.0;
    public const double Breath = 2600.0;
    public const double Calm = 1900.0;
    public const double Orbit = 3400.0;

    private const long ClockWrapMilliseconds = 3_600_000L;

    public static double Seconds => (Environment.TickCount64 % ClockWrapMilliseconds) / 1000.0;

    public static float Wave(double periodMs = Medium)
    {
        var t = (Environment.TickCount % periodMs) / periodMs;
        return (float)((Math.Sin(t * Math.PI * 2.0) + 1.0) * 0.5);
    }

    public static float Phase(double periodMs) => (float)((Environment.TickCount % periodMs) / periodMs);

    public static float Heartbeat(double periodMs)
    {
        var phase = Phase(periodMs);
        return MathF.Max(Bump(phase, 0.06f, 0.06f), Bump(phase, 0.20f, 0.06f) * 0.6f);
    }

    private static float Bump(float phase, float center, float width)
    {
        var distance = (phase - center) / width;
        if (distance < -1f || distance > 1f)
        {
            return 0f;
        }

        return 0.5f * (1f + MathF.Cos(distance * MathF.PI));
    }

    public static Vector4 Blend(Vector4 a, Vector4 b, double periodMs = Medium) =>
        Vector4.Lerp(a, b, Wave(periodMs));
}
