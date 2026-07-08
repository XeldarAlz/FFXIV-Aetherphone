namespace Aetherphone.Core.Animation;

internal delegate float EasingFunction(float progress);

internal static class Easing
{
    public static float Lerp(float from, float to, float amount) => from + (to - from) * amount;
    public static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
    public static float Linear(float progress) => progress;
    public static float SmoothStep(float progress) => progress * progress * (3f - 2f * progress);
    public static float EaseInCubic(float progress) => progress * progress * progress;

    public static float EaseOutCubic(float progress)
    {
        var inverse = 1f - progress;
        return 1f - inverse * inverse * inverse;
    }

    public static float EaseInOutCubic(float progress)
    {
        if (progress < 0.5f)
        {
            return 4f * progress * progress * progress;
        }

        var inverse = -2f * progress + 2f;
        return 1f - inverse * inverse * inverse * 0.5f;
    }

    public static float EaseOutQuint(float progress)
    {
        var inverse = 1f - progress;
        return 1f - inverse * inverse * inverse * inverse * inverse;
    }

    public static float EaseInQuint(float progress) =>
        progress * progress * progress * progress * progress;

    public static float SmootherStep(float progress)
    {
        var value = Clamp01(progress);
        return value * value * value * (value * (value * 6f - 15f) + 10f);
    }

    public static float Segment(float value, float start, float end) =>
        end <= start ? (value >= end ? 1f : 0f) : Clamp01((value - start) / (end - start));

    public static float EaseOutBack(float progress)
    {
        const float overshoot = 1.70158f;
        const float scaled = overshoot + 1f;
        var inverse = progress - 1f;
        return 1f + scaled * inverse * inverse * inverse + overshoot * inverse * inverse;
    }
}
