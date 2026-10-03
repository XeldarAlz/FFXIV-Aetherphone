namespace Aetherphone.Apps.Games.Swoop;

internal sealed class SwoopTerrain
{
    public const int MaxIslands = 96;
    public const int HarmonicCount = 3;
    public const double ShoreLength = 45.0;
    private const double BaseIslandLength = 420.0;
    private const double IslandLengthStep = 120.0;
    private const double MaxIslandLength = 1600.0;
    private const double BaseAmplitude = 6.0;
    private const double AmplitudeStep = 1.0;
    private const double MaxAmplitude = 18.0;
    private const double BaseWavelength = 70.0;
    private const double WavelengthStep = 6.0;
    private const double MaxWavelength = 130.0;
    private static readonly double[] WavelengthRatios = { 1.0, 0.47, 0.23 };
    private static readonly double[] AmplitudeRatios = { 1.0, 0.32, 0.1 };

    private readonly double[] islandStarts = new double[MaxIslands + 1];
    private readonly double[] amplitudes = new double[MaxIslands * HarmonicCount];
    private readonly double[] waveNumbers = new double[MaxIslands * HarmonicCount];
    private readonly double[] phases = new double[MaxIslands * HarmonicCount];

    public double IslandStart(int island) => islandStarts[Math.Clamp(island, 0, MaxIslands)];

    public double IslandEnd(int island) => island >= MaxIslands ? double.PositiveInfinity : IslandStart(island + 1);

    public static double IslandLength(int island) => Math.Min(BaseIslandLength + island * IslandLengthStep, MaxIslandLength);

    public void Generate(uint seed)
    {
        var random = new SwoopRandom(seed);
        islandStarts[0] = 0.0;
        for (var island = 0; island < MaxIslands; island++)
        {
            islandStarts[island + 1] = islandStarts[island] + IslandLength(island);
            var amplitude = Math.Min(BaseAmplitude + island * AmplitudeStep, MaxAmplitude);
            var wavelength = Math.Min(BaseWavelength + island * WavelengthStep, MaxWavelength);
            for (var harmonic = 0; harmonic < HarmonicCount; harmonic++)
            {
                var slot = island * HarmonicCount + harmonic;
                var harmonicWavelength = wavelength * WavelengthRatios[harmonic] * random.Range(0.9f, 1.1f);
                amplitudes[slot] = amplitude * AmplitudeRatios[harmonic] * random.Range(0.85f, 1.15f);
                waveNumbers[slot] = Math.Tau / harmonicWavelength;
                phases[slot] = random.Range(0f, MathF.Tau);
            }
        }
    }

    public int IslandIndexAt(double x)
    {
        if (x < islandStarts[0])
        {
            return -1;
        }

        if (x >= islandStarts[MaxIslands])
        {
            return MaxIslands;
        }

        var low = 0;
        var high = MaxIslands - 1;
        while (low < high)
        {
            var middle = (low + high + 1) >> 1;
            if (islandStarts[middle] <= x)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return low;
    }

    public double Height(double x)
    {
        Evaluate(x, out var height, out _, out _);
        return height;
    }

    public double Slope(double x)
    {
        Evaluate(x, out _, out var slope, out _);
        return slope;
    }

    public void Evaluate(double x, out double height, out double slope, out double curvature)
    {
        var island = IslandIndexAt(x);
        if (island < 0 || island >= MaxIslands)
        {
            height = 0.0;
            slope = 0.0;
            curvature = 0.0;
            return;
        }

        var start = islandStarts[island];
        var end = islandStarts[island + 1];
        var rise = Shore((x - start) / ShoreLength, out var riseSlope, out var riseCurve);
        var fall = Shore((end - x) / ShoreLength, out var fallSlope, out var fallCurve);
        riseSlope /= ShoreLength;
        riseCurve /= ShoreLength * ShoreLength;
        fallSlope = -fallSlope / ShoreLength;
        fallCurve /= ShoreLength * ShoreLength;
        var envelope = rise * fall;
        var envelopeSlope = riseSlope * fall + rise * fallSlope;
        var envelopeCurve = riseCurve * fall + 2.0 * riseSlope * fallSlope + rise * fallCurve;
        var hills = 0.0;
        var hillsSlope = 0.0;
        var hillsCurve = 0.0;
        for (var harmonic = 0; harmonic < HarmonicCount; harmonic++)
        {
            var slot = island * HarmonicCount + harmonic;
            var waveNumber = waveNumbers[slot];
            var angle = waveNumber * (x - start) + phases[slot];
            var sine = Math.Sin(angle);
            var cosine = Math.Cos(angle);
            var amplitude = amplitudes[slot];
            hills += amplitude * sine;
            hillsSlope += amplitude * waveNumber * cosine;
            hillsCurve -= amplitude * waveNumber * waveNumber * sine;
        }

        height = envelope * hills;
        slope = envelopeSlope * hills + envelope * hillsSlope;
        curvature = envelopeCurve * hills + 2.0 * envelopeSlope * hillsSlope + envelope * hillsCurve;
    }

    private static double Shore(double progress, out double slope, out double curve)
    {
        if (progress >= 1.0)
        {
            slope = 0.0;
            curve = 0.0;
            return 1.0;
        }

        if (progress <= 0.0)
        {
            slope = 0.0;
            curve = 0.0;
            return 0.0;
        }

        slope = 6.0 * progress - 6.0 * progress * progress;
        curve = 6.0 - 12.0 * progress;
        return progress * progress * (3.0 - 2.0 * progress);
    }
}
