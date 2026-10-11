namespace Aetherphone.Apps.Casino.Machines;

internal static class MachineTiming
{
    public const float Ramp = 0.1f;
    public const float FirstStop = 2.0f;
    public const float Stagger = 0.12f;
    public const float MicroPause = 0.12f;
    public const float Landing = 0.28f;
    public const float Anticipation = 0.8f;
    public const float WinPresent = 1.3f;
    public const float LossPresent = 0.35f;
    public const float RetriggerPresent = 0.9f;
    public const float OrbFlight = 0.7f;
    public const float Shatter = 0.35f;
    public const float Fall = 0.45f;
    public const float Expand = 0.9f;
    public const float Intro = 2.4f;
    public const float PickerIntro = 3.0f;
    public const float Outro = 1.8f;
    public const float Hold = 1.3f;
    public const float Respin = 0.9f;
    public const float RespinLanded = 0.35f;
    public const float CollectBase = 0.6f;
    public const float CollectPerCoin = 0.22f;
    public const float Grand = 1.4f;
    public const float Meter = 1.6f;
    public const float Buy = 1.6f;
    public const float TurboFactor = 0.5f;
    public const float RollupBetsPerSecond = 0.5f;
    public const float RollupFullSpeedBets = 20f;
    public const float RollupCompressSeconds = 2.5f;

    public static float Scaled(float seconds, bool turbo) => turbo ? seconds * TurboFactor : seconds;
}
