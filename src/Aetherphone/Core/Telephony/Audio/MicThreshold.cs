namespace Aetherphone.Core.Telephony.Audio;

internal static class MicThreshold
{
    public const float DefaultPosition = 0.42f;
    public const float FloorDecibels = -60f;
    public const float CloseRatio = 0.556f;
    public const float ClipLevel = 0.98f;
    private const float LegacyOpenRms = 0.018f;
    private const float LegacyMinimumGain = 0.25f;
    private const float LegacyMaximumGain = 2f;
    private const int ReadoutSteps = 100;

    public static IReadOnlyList<string> Readouts { get; } = BuildReadouts();

    public static float ToRms(float position)
    {
        if (position <= 0f)
        {
            return 0f;
        }

        var decibels = FloorDecibels * (1f - Math.Clamp(position, 0f, 1f));
        return MathF.Pow(10f, decibels / 20f);
    }

    public static float MeterFill(float rms)
    {
        if (rms <= 0f)
        {
            return 0f;
        }

        return Math.Clamp(1f - 20f * MathF.Log10(rms) / FloorDecibels, 0f, 1f);
    }

    public static bool Opens(float rms, float position) => position <= 0f || rms >= ToRms(position);

    public static float FromLegacyGain(float gain)
    {
        var trim = Math.Clamp(gain, LegacyMinimumGain, LegacyMaximumGain);
        return MeterFill(LegacyOpenRms / trim);
    }

    private static string[] BuildReadouts()
    {
        var readouts = new string[ReadoutSteps + 1];
        for (var index = 0; index <= ReadoutSteps; index++)
        {
            readouts[index] = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return readouts;
    }
}
