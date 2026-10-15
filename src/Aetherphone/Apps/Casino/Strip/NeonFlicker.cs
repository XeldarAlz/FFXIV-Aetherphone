namespace Aetherphone.Apps.Casino.Strip;

internal static class NeonFlicker
{
    private static readonly float[] Stops = { 0f, 0.40f, 0.46f, 0.48f, 0.52f, 0.55f, 0.80f, 1f };
    private static readonly float[] Levels = { 1f, 0.88f, 0.55f, 0.95f, 0.70f, 1f, 0.90f, 1f };

    public static float Level(float seconds, float period)
    {
        if (period <= 0f)
        {
            return 1f;
        }

        var cycle = seconds / period;
        var position = cycle - MathF.Floor(cycle);
        for (var stopIndex = 1; stopIndex < Stops.Length; stopIndex++)
        {
            var end = Stops[stopIndex];
            if (position > end)
            {
                continue;
            }

            var start = Stops[stopIndex - 1];
            var blend = (position - start) / (end - start);
            return Levels[stopIndex - 1] + (Levels[stopIndex] - Levels[stopIndex - 1]) * blend;
        }

        return Levels[^1];
    }
}
