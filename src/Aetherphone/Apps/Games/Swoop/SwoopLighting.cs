namespace Aetherphone.Apps.Games.Swoop;

internal readonly struct SwoopLighting
{
    private static readonly float[] Stops = { 0f, 0.5f, 0.7f, 0.84f, 0.94f, 1f };

    private static readonly Vector4[] TopKeys =
    {
        new(0.30f, 0.56f, 0.92f, 1f), new(0.32f, 0.55f, 0.90f, 1f), new(0.40f, 0.45f, 0.78f, 1f),
        new(0.33f, 0.27f, 0.56f, 1f), new(0.14f, 0.13f, 0.32f, 1f), new(0.04f, 0.05f, 0.14f, 1f),
    };

    private static readonly Vector4[] BottomKeys =
    {
        new(0.70f, 0.86f, 0.98f, 1f), new(0.80f, 0.89f, 0.96f, 1f), new(0.99f, 0.80f, 0.52f, 1f),
        new(0.98f, 0.55f, 0.42f, 1f), new(0.55f, 0.33f, 0.50f, 1f), new(0.10f, 0.11f, 0.24f, 1f),
    };

    private static readonly Vector4[] TintKeys =
    {
        new(1f, 1f, 1f, 1f), new(1f, 0.98f, 0.95f, 1f), new(1f, 0.88f, 0.74f, 1f),
        new(0.92f, 0.72f, 0.68f, 1f), new(0.60f, 0.52f, 0.72f, 1f), new(0.32f, 0.34f, 0.52f, 1f),
    };

    private static readonly Vector4[] SunKeys =
    {
        new(1f, 0.97f, 0.84f, 1f), new(1f, 0.93f, 0.70f, 1f), new(1f, 0.78f, 0.42f, 1f),
        new(1f, 0.56f, 0.32f, 1f), new(0.96f, 0.40f, 0.32f, 1f), new(0.90f, 0.32f, 0.30f, 1f),
    };

    public readonly Vector4 SkyTop;
    public readonly Vector4 SkyBottom;
    public readonly Vector4 Tint;
    public readonly Vector4 Sun;
    public readonly float Progress;
    public readonly float Night;
    public readonly float Golden;

    private SwoopLighting(float progress)
    {
        Progress = progress;
        SkyTop = Sample(TopKeys, progress);
        SkyBottom = Sample(BottomKeys, progress);
        Tint = Sample(TintKeys, progress);
        Sun = Sample(SunKeys, progress);
        Night = SwoopShapes.Smooth(0.86f, 1f, progress);
        Golden = SwoopShapes.Smooth(0.55f, 0.75f, progress) * (1f - SwoopShapes.Smooth(0.88f, 0.98f, progress));
    }

    public static SwoopLighting At(float progress) => new(Math.Clamp(progress, 0f, 1f));

    private static Vector4 Sample(Vector4[] keys, float progress)
    {
        for (var stop = 1; stop < Stops.Length; stop++)
        {
            if (progress > Stops[stop])
            {
                continue;
            }

            var span = Stops[stop] - Stops[stop - 1];
            var amount = span <= 0f ? 1f : (progress - Stops[stop - 1]) / span;
            return Vector4.Lerp(keys[stop - 1], keys[stop], amount);
        }

        return keys[^1];
    }
}
