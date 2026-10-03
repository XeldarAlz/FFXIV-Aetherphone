namespace Aetherphone.Apps.Skywatcher.Sky;

internal static class SkyNoise
{
    private const float TwoPi = MathF.PI * 2f;
    private const float DitherStrength = 1.5f;
    private const int DitherSeed = 977;

    public static float Perlin(float x, float y, int period, int seed)
    {
        var cellX = (int)MathF.Floor(x);
        var cellY = (int)MathF.Floor(y);
        var fractionX = x - cellX;
        var fractionY = y - cellY;
        var topLeft = Corner(cellX, cellY, period, seed, fractionX, fractionY);
        var topRight = Corner(cellX + 1, cellY, period, seed, fractionX - 1f, fractionY);
        var bottomLeft = Corner(cellX, cellY + 1, period, seed, fractionX, fractionY - 1f);
        var bottomRight = Corner(cellX + 1, cellY + 1, period, seed, fractionX - 1f, fractionY - 1f);
        var blendX = Fade(fractionX);
        var blendY = Fade(fractionY);
        var top = topLeft + (topRight - topLeft) * blendX;
        var bottom = bottomLeft + (bottomRight - bottomLeft) * blendX;
        return top + (bottom - top) * blendY;
    }

    public static float Fractal(float u, float v, int basePeriod, int octaves, int seed)
    {
        var sum = 0f;
        var amplitude = 1f;
        var period = basePeriod;
        for (var octave = 0; octave < octaves; octave++)
        {
            sum += amplitude * Perlin(u * period, v * period, period, seed + octave * 101);
            amplitude *= 0.5f;
            period *= 2;
        }

        return sum;
    }

    public static float Warped(float u, float v, int basePeriod, int octaves, float warp, int seed)
    {
        var offsetU = Fractal(u, v, 2, 3, seed + 7919);
        var offsetV = Fractal(u, v, 2, 3, seed + 104729);
        return Fractal(u + warp * offsetU, v + warp * offsetV, basePeriod, octaves, seed);
    }

    public static float[] Field(int width, int height, int basePeriod, int octaves, float warp, int seed)
    {
        var field = new float[width * height];
        for (var row = 0; row < height; row++)
        {
            var v = row / (float)height;
            for (var column = 0; column < width; column++)
            {
                var u = column / (float)width;
                field[row * width + column] = Warped(u, v, basePeriod, octaves, warp, seed);
            }
        }

        Normalize(field);
        return field;
    }

    public static void Normalize(float[] field)
    {
        var minimum = float.MaxValue;
        var maximum = float.MinValue;
        for (var index = 0; index < field.Length; index++)
        {
            minimum = MathF.Min(minimum, field[index]);
            maximum = MathF.Max(maximum, field[index]);
        }

        var span = MathF.Max(maximum - minimum, 1e-6f);
        for (var index = 0; index < field.Length; index++)
        {
            field[index] = (field[index] - minimum) / span;
        }
    }

    public static float SmoothStep(float edgeStart, float edgeEnd, float value)
    {
        var fraction = Math.Clamp((value - edgeStart) / (edgeEnd - edgeStart), 0f, 1f);
        return fraction * fraction * (3f - 2f * fraction);
    }

    public static byte Quantize(float value, int column, int row)
    {
        var dither = (Unit(column, row, DitherSeed) - 0.5f) * DitherStrength;
        return (byte)Math.Clamp((int)MathF.Round(value * 255f + dither), 0, 255);
    }

    public static uint Hash(int x, int y, int seed)
    {
        unchecked
        {
            var hash = (uint)x * 0x8DA6B343u ^ (uint)y * 0xD8163841u ^ (uint)seed * 0xCB1AB31Fu;
            hash ^= hash >> 13;
            hash *= 0x5BD1E995u;
            hash ^= hash >> 15;
            return hash;
        }
    }

    public static float Unit(int x, int y, int seed) => Hash(x, y, seed) / (float)uint.MaxValue;

    private static float Corner(int cellX, int cellY, int period, int seed, float offsetX, float offsetY)
    {
        var angle = Unit(Wrap(cellX, period), Wrap(cellY, period), seed) * TwoPi;
        return MathF.Cos(angle) * offsetX + MathF.Sin(angle) * offsetY;
    }

    private static int Wrap(int value, int period)
    {
        var wrapped = value % period;
        return wrapped < 0 ? wrapped + period : wrapped;
    }

    private static float Fade(float value) => value * value * value * (value * (value * 6f - 15f) + 10f);
}
