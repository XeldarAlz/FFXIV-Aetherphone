namespace Aetherphone.Core.Telephony.Audio;

internal sealed class AutoGain
{
    public const float TargetRms = 0.125f;
    public const float MinimumGain = 0.5f;
    public const float MaximumGain = 7.943f;
    public const float NoiseFloorRms = 0.0032f;
    public const float SpeechOverNoise = 3.16f;
    public const float QuietestNoise = NoiseFloorRms / SpeechOverNoise;
    public const float AutomaticCeiling = 0.708f;
    public const float LimiterThreshold = 0.8f;
    public const float LimiterCeiling = 0.97f;
    private const float AttackSeconds = 0.075f;
    private const float ReleaseSeconds = 0.8f;
    private const float NoiseFallSeconds = 0.1f;
    private const float NoiseRiseSeconds = 6f;
    private const float SpeechNoiseRiseSeconds = 60f;
    private const float OnsetSeconds = 0.06f;
    private const float SampleScale = 1f / 32768f;
    private float gain = 1f;
    private float noise = NoiseFloorRms;
    private float voicedSeconds;
    private float appliedGain = float.NaN;

    public float Gain => gain;

    public void Reset(float startGain = 1f)
    {
        gain = Math.Clamp(startGain, MinimumGain, MaximumGain);
        noise = NoiseFloorRms;
        voicedSeconds = 0f;
        appliedGain = float.NaN;
    }

    public void Process(Span<short> pcm, float rawRms, int sampleRate, float openRms, float volume, bool adaptive)
    {
        if (pcm.IsEmpty || sampleRate <= 0)
        {
            return;
        }

        var startGain = gain;
        if (!adaptive)
        {
            gain = 1f;
            voicedSeconds = 0f;
            var manualFrom = float.IsNaN(appliedGain) ? volume : appliedGain;
            appliedGain = volume;
            ApplyLimited(pcm, manualFrom, volume);
            return;
        }

        Adapt(rawRms, rawRms >= openRms, pcm.Length / (float)sampleRate);
        var peakLimit = PeakLimit(pcm);
        var from = MathF.Min(float.IsNaN(appliedGain) ? startGain : appliedGain, peakLimit);
        var to = MathF.Min(gain, peakLimit);
        appliedGain = to;
        ApplyRamp(pcm, from, to);
    }

    public void Observe(float rawRms, int samples, int sampleRate)
    {
        if (samples <= 0 || sampleRate <= 0)
        {
            return;
        }

        Adapt(rawRms, true, samples / (float)sampleRate);
    }

    public static float Rms(ReadOnlySpan<short> pcm)
    {
        if (pcm.IsEmpty)
        {
            return 0f;
        }

        double sum = 0;
        for (var index = 0; index < pcm.Length; index++)
        {
            double sample = pcm[index];
            sum += sample * sample;
        }

        return (float)(Math.Sqrt(sum / pcm.Length) / 32768.0);
    }

    public static float Peak(ReadOnlySpan<short> pcm)
    {
        var peak = 0;
        for (var index = 0; index < pcm.Length; index++)
        {
            peak = Math.Max(peak, Math.Abs((int)pcm[index]));
        }

        return peak * SampleScale;
    }

    private void Adapt(float rms, bool voiced, float seconds)
    {
        TrackNoise(rms, seconds);
        if (!voiced)
        {
            voicedSeconds = 0f;
            return;
        }

        voicedSeconds += seconds;
        if (voicedSeconds < OnsetSeconds || rms < NoiseFloorRms || rms < noise * SpeechOverNoise)
        {
            return;
        }

        var desired = Math.Clamp(TargetRms / rms, MinimumGain, MaximumGain);
        var timeConstant = desired < gain ? AttackSeconds : ReleaseSeconds;
        gain = TowardInLog(gain, desired, seconds, timeConstant);
    }

    private void TrackNoise(float rms, float seconds)
    {
        var level = MathF.Max(rms, QuietestNoise);
        float timeConstant;
        if (level < noise)
        {
            timeConstant = NoiseFallSeconds;
        }
        else
        {
            timeConstant = level >= noise * SpeechOverNoise ? SpeechNoiseRiseSeconds : NoiseRiseSeconds;
        }

        noise = TowardInLog(noise, level, seconds, timeConstant);
    }

    private static float TowardInLog(float current, float target, float seconds, float timeConstant)
    {
        var blend = 1f - MathF.Exp(-seconds / timeConstant);
        var logCurrent = MathF.Log(current);
        return MathF.Exp(logCurrent + (MathF.Log(target) - logCurrent) * blend);
    }

    private static float PeakLimit(ReadOnlySpan<short> pcm)
    {
        var peak = Peak(pcm);
        return peak <= 0f ? MaximumGain : AutomaticCeiling / peak;
    }

    private static void ApplyRamp(Span<short> pcm, float fromGain, float toGain)
    {
        if (fromGain == 1f && toGain == 1f)
        {
            return;
        }

        var step = (toGain - fromGain) / pcm.Length;
        var current = fromGain;
        for (var index = 0; index < pcm.Length; index++)
        {
            current += step;
            var value = MathF.Round(pcm[index] * current);
            pcm[index] = (short)Math.Clamp(value, short.MinValue, short.MaxValue);
        }
    }

    private static void ApplyLimited(Span<short> pcm, float fromGain, float toGain)
    {
        if (fromGain == 1f && toGain == 1f)
        {
            return;
        }

        var step = (toGain - fromGain) / pcm.Length;
        var current = fromGain;
        for (var index = 0; index < pcm.Length; index++)
        {
            current += step;
            pcm[index] = Limit(pcm[index] * SampleScale * current);
        }
    }

    public static short Limit(float sample)
    {
        var magnitude = MathF.Abs(sample);
        if (magnitude > LimiterThreshold)
        {
            const float headroom = LimiterCeiling - LimiterThreshold;
            magnitude = LimiterThreshold + headroom * MathF.Tanh((magnitude - LimiterThreshold) / headroom);
            sample = MathF.CopySign(magnitude, sample);
        }

        return (short)MathF.Round(sample * short.MaxValue);
    }
}
