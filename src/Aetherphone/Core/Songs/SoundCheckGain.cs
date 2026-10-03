namespace Aetherphone.Core.Songs;

internal static class SoundCheckGain
{
    public const double TargetLufs = -14.0;
    public const float MinimumDecibels = -12f;
    public const float MaximumDecibels = 6f;
    public const float PeakCeilingDecibels = -1f;

    public static float Decibels(double integratedLufs, float samplePeak)
    {
        if (!double.IsFinite(integratedLufs))
        {
            return 0f;
        }

        var gain = Math.Clamp((float)(TargetLufs - integratedLufs), MinimumDecibels, MaximumDecibels);
        if (gain <= 0f || samplePeak <= 0f)
        {
            return gain;
        }

        var headroom = PeakCeilingDecibels - 20f * MathF.Log10(samplePeak);
        return Math.Clamp(gain, 0f, MathF.Max(0f, headroom));
    }

    public static float ToLinear(float decibels) => MathF.Pow(10f, decibels / 20f);
}
