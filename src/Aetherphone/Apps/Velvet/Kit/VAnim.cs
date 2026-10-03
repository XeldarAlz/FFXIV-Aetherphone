using Aetherphone.Core.Animation;

namespace Aetherphone.Apps.Velvet.Kit;

internal static class VAnim
{
    private static readonly Dictionary<string, float> Values = new();

    public static float To(string id, float target, float smoothTime, float deltaSeconds)
    {
        if (!Values.TryGetValue(id, out var current))
        {
            current = target;
        }

        var factor = smoothTime <= 0f ? 1f : 1f - MathF.Exp(-deltaSeconds / smoothTime);
        current += (target - current) * factor;
        if (MathF.Abs(target - current) < 0.0005f)
        {
            current = target;
        }

        Values[id] = current;
        return current;
    }

    public static float Toggle(string id, bool on, float deltaSeconds, float smoothTime = Motion.Release) =>
        To(id, on ? 1f : 0f, smoothTime, deltaSeconds);
}
