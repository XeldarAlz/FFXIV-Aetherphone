namespace Aetherphone.Core.Animation;

internal static class TransitionTiming
{
    public const float PresentSeconds = 0.34f;

    public const float DismissSeconds = 0.30f;

    public const float PushSeconds = 0.28f;

    public const float ShellDimMax = 0.45f;

    public const float UnderParallax = 0.26f;

    public const float UnderDimMax = 0.16f;

    public const float MaxFrameSeconds = 0.1f;

    public const float BubbleSeconds = 0.34f;

    public static readonly EasingFunction PresentCurve = Easing.EaseOutQuint;

    public static readonly EasingFunction DismissCurve = Easing.EaseInOutCubic;

    public static readonly EasingFunction PushCurve = Easing.EaseOutCubic;
}
