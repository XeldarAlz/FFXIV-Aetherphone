namespace Aetherphone.Core.Animation;

internal static class Motion
{
    private const float ReducedSmoothTime = 0.0001f;

    public static bool Reduced { get; private set; }

    public static void BeginFrame(bool reduced)
    {
        Reduced = reduced;
    }

    public static float SmoothTime(float smoothTime) => Reduced ? ReducedSmoothTime : smoothTime;
}
