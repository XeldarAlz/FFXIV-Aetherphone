namespace Aetherphone.Core.Animation;

internal static class BootTiming
{
    public const float PowerOnSeconds = 0.9f;
    public const float EmblemInSeconds = 0.85f;
    public const float EmblemHoldSeconds = 0.7f;
    public const float EmblemExitSeconds = 0.55f;
    public const float GreetingInSeconds = 0.45f;
    public const float GreetingHoldSeconds = 0.35f;
    public const float GreetingOutSeconds = 0.45f;
    public const float RevealSeconds = 1.0f;
    public const float ShortPowerOnSeconds = 0.2f;
    public const float ShortEmblemInSeconds = 0.5f;
    public const float ShortEmblemHoldSeconds = 0.3f;
    public const float ShortEmblemExitSeconds = 0.4f;
    public const float ShortRevealSeconds = 0.45f;
    public const float FontWaitCapSeconds = 60f;
    public const float EmblemBreatheFrequency = 3.2f;
    public const float EmblemBreatheAmplitude = 0.025f;
    public const float EmblemExitGrowth = 1.18f;
    public const float EmblemRingPeriod = 1.4f;
    public const float EmblemRingExpansion = 1.8f;
    public static readonly EasingFunction RevealCurve = Easing.EaseInOutCubic;
}
