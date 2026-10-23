using System.Buffers;

namespace Aetherphone.Core.Telephony.Audio;

internal static class SpeechLeveler
{
    public const float BlockSeconds = 0.01f;
    private const int WindowBlocks = 15;
    private const float NoisePercentile = 0.1f;
    private const float LoudestNoiseRms = 0.0056f;
    private const float SimilarEnergyNaturalLog = 2.763f;
    private const float ReleaseDecibelsPerSecond = 100f;
    private const float AttackDecibelsPerSecond = 400f;
    private const float NaturalLogPerDecibel = 0.11512925f;
    private const float SampleScale = 1f / 32768f;
    private const float SilentEnergy = 1e-12f;

    public static float Level(Span<short> pcm, int sampleRate, float startGain)
    {
        var blockSamples = Math.Max(1, (int)(sampleRate * BlockSeconds));
        var blocks = (pcm.Length + blockSamples - 1) / blockSamples;
        if (blocks == 0)
        {
            return startGain;
        }

        var energies = ArrayPool<float>.Shared.Rent(blocks);
        var peaks = ArrayPool<float>.Shared.Rent(blocks);
        var scratch = ArrayPool<float>.Shared.Rent(blocks);
        var knots = ArrayPool<float>.Shared.Rent(blocks + 1);
        try
        {
            Measure(pcm, blockSamples, blocks, energies, peaks);
            var speechEnergy = SpeechEnergy(energies, scratch, blocks);
            ShortLevels(energies, blocks, scratch);
            if (!Desired(energies, scratch, blocks, speechEnergy, knots))
            {
                return startGain;
            }

            Constrain(peaks, blocks, knots);
            Smooth(knots, blocks + 1);
            Apply(pcm, blockSamples, blocks, knots);
            return MathF.Exp(knots[blocks]);
        }
        finally
        {
            ArrayPool<float>.Shared.Return(energies);
            ArrayPool<float>.Shared.Return(peaks);
            ArrayPool<float>.Shared.Return(scratch);
            ArrayPool<float>.Shared.Return(knots);
        }
    }

    private static void Measure(ReadOnlySpan<short> pcm, int blockSamples, int blocks, float[] energies,
        float[] peaks)
    {
        for (var block = 0; block < blocks; block++)
        {
            var start = block * blockSamples;
            var samples = pcm.Slice(start, Math.Min(blockSamples, pcm.Length - start));
            double sum = 0;
            var peak = 0;
            for (var index = 0; index < samples.Length; index++)
            {
                int sample = samples[index];
                sum += sample * (double)sample;
                peak = Math.Max(peak, Math.Abs(sample));
            }

            energies[block] = (float)(sum / samples.Length) * SampleScale * SampleScale;
            peaks[block] = peak * SampleScale;
        }
    }

    private static float SpeechEnergy(float[] energies, float[] sorted, int blocks)
    {
        Array.Copy(energies, sorted, blocks);
        Array.Sort(sorted, 0, blocks);
        var noiseEnergy = sorted[Math.Min(blocks - 1, (int)(blocks * NoisePercentile))];
        var noise = Math.Clamp(MathF.Sqrt(noiseEnergy), AutoGain.QuietestNoise, LoudestNoiseRms);
        var speech = MathF.Max(AutoGain.NoiseFloorRms, noise * AutoGain.SpeechOverNoise);
        return speech * speech;
    }

    private static void ShortLevels(float[] energies, int blocks, float[] levels)
    {
        for (var block = 0; block < blocks; block++)
        {
            var sum = energies[block];
            var count = 1;
            if (block > 0)
            {
                sum += energies[block - 1];
                count++;
            }

            if (block + 1 < blocks)
            {
                sum += energies[block + 1];
                count++;
            }

            levels[block] = MathF.Log(MathF.Max(sum / count, SilentEnergy));
        }
    }

    private static bool Desired(float[] energies, float[] levels, int blocks, float speechEnergy, float[] knots)
    {
        var any = false;
        for (var block = 0; block < blocks; block++)
        {
            if (energies[block] < speechEnergy)
            {
                knots[block] = float.NaN;
                continue;
            }

            var first = Math.Max(0, block - WindowBlocks);
            var last = Math.Min(blocks - 1, block + WindowBlocks);
            var sum = 0d;
            var count = 0;
            for (var neighbour = first; neighbour <= last; neighbour++)
            {
                if (energies[neighbour] < speechEnergy
                    || MathF.Abs(levels[neighbour] - levels[block]) > SimilarEnergyNaturalLog)
                {
                    continue;
                }

                sum += energies[neighbour];
                count++;
            }

            var rms = MathF.Sqrt((float)(sum / count));
            knots[block] = MathF.Log(Math.Clamp(AutoGain.TargetRms / rms, AutoGain.MinimumGain,
                AutoGain.MaximumGain));
            any = true;
        }

        if (!any)
        {
            return false;
        }

        var next = float.NaN;
        for (var block = blocks - 1; block >= 0; block--)
        {
            if (float.IsNaN(knots[block]))
            {
                knots[block] = next;
                continue;
            }

            next = knots[block];
        }

        for (var block = 1; block < blocks; block++)
        {
            if (float.IsNaN(knots[block]))
            {
                knots[block] = knots[block - 1];
            }
        }

        knots[blocks] = knots[blocks - 1];
        return true;
    }

    private static void Constrain(float[] peaks, int blocks, float[] knots)
    {
        var ceiling = MathF.Log(AutoGain.AutomaticCeiling);
        for (var block = 0; block < blocks; block++)
        {
            if (peaks[block] <= 0f)
            {
                continue;
            }

            var limit = ceiling - MathF.Log(peaks[block]);
            knots[block] = MathF.Min(knots[block], limit);
            knots[block + 1] = MathF.Min(knots[block + 1], limit);
        }
    }

    private static void Smooth(float[] knots, int count)
    {
        var release = ReleaseDecibelsPerSecond * BlockSeconds * NaturalLogPerDecibel;
        var attack = AttackDecibelsPerSecond * BlockSeconds * NaturalLogPerDecibel;
        for (var index = 1; index < count; index++)
        {
            knots[index] = MathF.Min(knots[index], knots[index - 1] + release);
        }

        for (var index = count - 2; index >= 0; index--)
        {
            knots[index] = MathF.Min(knots[index], knots[index + 1] + attack);
        }
    }

    private static void Apply(Span<short> pcm, int blockSamples, int blocks, float[] knots)
    {
        for (var block = 0; block < blocks; block++)
        {
            var start = block * blockSamples;
            var count = Math.Min(blockSamples, pcm.Length - start);
            var from = MathF.Exp(knots[block]);
            var to = MathF.Exp(knots[block + 1]);
            var step = (to - from) / blockSamples;
            for (var index = 0; index < count; index++)
            {
                var gain = from + step * index;
                var value = MathF.Round(pcm[start + index] * gain);
                pcm[start + index] = (short)Math.Clamp(value, short.MinValue, short.MaxValue);
            }
        }
    }
}
