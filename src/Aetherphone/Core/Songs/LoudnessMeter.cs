using NAudio.Wave;

namespace Aetherphone.Core.Songs;

internal readonly record struct LoudnessResult(double IntegratedLufs, float SamplePeak);

internal sealed class LoudnessMeter
{
    private const int SubBlocksPerBlock = 4;
    private const int SubBlocksPerSecond = 10;
    private const double LoudnessOffset = -0.691;
    private const double AbsoluteGateLufs = -70.0;
    private const double RelativeGateLu = -10.0;

    // ITU-R BS.1770-4 K-weighting (pre-filter shelf and RLB high-pass) in the sample-rate-generic form
    // used by libebur128, so 44.1 kHz decodes are weighted exactly like 48 kHz ones.
    private const double ShelfFrequency = 1681.974450955533;
    private const double ShelfGainDecibels = 3.999843853973347;
    private const double ShelfQuality = 0.7071752369554196;
    private const double ShelfSlope = 0.4996667741545416;
    private const double HighPassFrequency = 38.13547087602444;
    private const double HighPassQuality = 0.5003270373238773;

    private readonly int channels;
    private readonly int subBlockFrames;
    private readonly Biquad[] shelves;
    private readonly Biquad[] highPasses;
    private readonly double[] recentEnergy = new double[SubBlocksPerBlock];
    private readonly List<double> blockPowers = new();
    private double subBlockEnergy;
    private int subBlockFill;
    private int subBlocksSeen;
    private float peak;

    public LoudnessMeter(int sampleRate, int channels)
    {
        this.channels = Math.Max(1, channels);
        subBlockFrames = Math.Max(1, sampleRate / SubBlocksPerSecond);
        shelves = new Biquad[this.channels];
        highPasses = new Biquad[this.channels];
        var shelf = Biquad.Shelf(sampleRate);
        var highPass = Biquad.HighPass(sampleRate);
        for (var channel = 0; channel < this.channels; channel++)
        {
            shelves[channel] = shelf;
            highPasses[channel] = highPass;
        }
    }

    public float SamplePeak => peak;

    public int BlockCount => blockPowers.Count;

    public double IntegratedLufs
    {
        get
        {
            var absoluteThreshold = PowerOf(AbsoluteGateLufs);
            var (absoluteSum, absoluteCount) = SumAbove(absoluteThreshold);
            if (absoluteCount == 0)
            {
                return double.NegativeInfinity;
            }

            var relativeThreshold = Math.Max(absoluteThreshold,
                PowerOf(LoudnessOf(absoluteSum / absoluteCount) + RelativeGateLu));
            var (gatedSum, gatedCount) = SumAbove(relativeThreshold);
            return gatedCount == 0 ? double.NegativeInfinity : LoudnessOf(gatedSum / gatedCount);
        }
    }

    public static LoudnessResult Measure(ISampleProvider source, CancellationToken token)
    {
        var format = source.WaveFormat;
        var meter = new LoudnessMeter(format.SampleRate, format.Channels);
        var buffer = new float[Math.Max(1, format.SampleRate / SubBlocksPerSecond) * Math.Max(1, format.Channels)];
        while (!token.IsCancellationRequested)
        {
            var read = source.Read(buffer, 0, buffer.Length);
            if (read <= 0)
            {
                break;
            }

            meter.Process(buffer.AsSpan(0, read));
        }

        return new LoudnessResult(meter.IntegratedLufs, meter.SamplePeak);
    }

    public void Process(ReadOnlySpan<float> interleaved)
    {
        var frames = interleaved.Length / channels;
        for (var frame = 0; frame < frames; frame++)
        {
            var frameEnergy = 0.0;
            var baseIndex = frame * channels;
            for (var channel = 0; channel < channels; channel++)
            {
                var sample = interleaved[baseIndex + channel];
                var magnitude = MathF.Abs(sample);
                if (magnitude > peak)
                {
                    peak = magnitude;
                }

                var weighted = highPasses[channel].Process(shelves[channel].Process(sample));
                frameEnergy += weighted * weighted;
            }

            subBlockEnergy += frameEnergy;
            subBlockFill++;
            if (subBlockFill == subBlockFrames)
            {
                CloseSubBlock();
            }
        }
    }

    private void CloseSubBlock()
    {
        recentEnergy[subBlocksSeen % SubBlocksPerBlock] = subBlockEnergy;
        subBlocksSeen++;
        subBlockEnergy = 0.0;
        subBlockFill = 0;
        if (subBlocksSeen < SubBlocksPerBlock)
        {
            return;
        }

        var energy = 0.0;
        for (var index = 0; index < SubBlocksPerBlock; index++)
        {
            energy += recentEnergy[index];
        }

        blockPowers.Add(energy / (SubBlocksPerBlock * (double)subBlockFrames));
    }

    private (double Sum, int Count) SumAbove(double threshold)
    {
        var sum = 0.0;
        var count = 0;
        for (var index = 0; index < blockPowers.Count; index++)
        {
            var power = blockPowers[index];
            if (power <= threshold)
            {
                continue;
            }

            sum += power;
            count++;
        }

        return (sum, count);
    }

    private static double LoudnessOf(double power) => LoudnessOffset + 10.0 * Math.Log10(power);

    private static double PowerOf(double loudness) => Math.Pow(10.0, (loudness - LoudnessOffset) / 10.0);

    private struct Biquad
    {
        private double b0;
        private double b1;
        private double b2;
        private double a1;
        private double a2;
        private double firstState;
        private double secondState;

        public double Process(double input)
        {
            var output = b0 * input + firstState;
            firstState = b1 * input - a1 * output + secondState;
            secondState = b2 * input - a2 * output;
            return output;
        }

        public static Biquad Shelf(int sampleRate)
        {
            var warp = Math.Tan(Math.PI * ShelfFrequency / sampleRate);
            var high = Math.Pow(10.0, ShelfGainDecibels / 20.0);
            var band = Math.Pow(high, ShelfSlope);
            var normalizer = 1.0 + warp / ShelfQuality + warp * warp;
            return new Biquad
            {
                b0 = (high + band * warp / ShelfQuality + warp * warp) / normalizer,
                b1 = 2.0 * (warp * warp - high) / normalizer,
                b2 = (high - band * warp / ShelfQuality + warp * warp) / normalizer,
                a1 = 2.0 * (warp * warp - 1.0) / normalizer,
                a2 = (1.0 - warp / ShelfQuality + warp * warp) / normalizer,
            };
        }

        public static Biquad HighPass(int sampleRate)
        {
            var warp = Math.Tan(Math.PI * HighPassFrequency / sampleRate);
            var normalizer = 1.0 + warp / HighPassQuality + warp * warp;
            return new Biquad
            {
                b0 = 1.0,
                b1 = -2.0,
                b2 = 1.0,
                a1 = 2.0 * (warp * warp - 1.0) / normalizer,
                a2 = (1.0 - warp / HighPassQuality + warp * warp) / normalizer,
            };
        }
    }
}
