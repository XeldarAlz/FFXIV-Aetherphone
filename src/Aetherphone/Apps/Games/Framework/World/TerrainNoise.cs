namespace Aetherphone.Apps.Games.Framework.World;

internal static class TerrainNoise
{
    private const float UnitScale = 1f / 16777216f;
    private const uint OctaveSalt = 0x632BE5ABu;

    public static uint Hash(int x, int y, uint seed) => Mix((uint)x * 0x9E3779B1u ^ Mix((uint)y * 0x85EBCA77u ^ seed));

    public static float Lattice(int x, int y, uint seed) => (Hash(x, y, seed) >> 8) * UnitScale;

    public static float Value1D(float x, uint seed)
    {
        var floor = MathF.Floor(x);
        var cell = (int)floor;
        var blend = Smooth(x - floor);
        return Lerp(Lattice(cell, 0, seed), Lattice(cell + 1, 0, seed), blend);
    }

    public static float Value2D(float x, float y, uint seed)
    {
        var floorX = MathF.Floor(x);
        var floorY = MathF.Floor(y);
        var cellX = (int)floorX;
        var cellY = (int)floorY;
        var blendX = Smooth(x - floorX);
        var blendY = Smooth(y - floorY);
        var top = Lerp(Lattice(cellX, cellY, seed), Lattice(cellX + 1, cellY, seed), blendX);
        var bottom = Lerp(Lattice(cellX, cellY + 1, seed), Lattice(cellX + 1, cellY + 1, seed), blendX);
        return Lerp(top, bottom, blendY);
    }

    public static float Fractal1D(float x, uint seed, int octaves)
    {
        var total = 0f;
        var weight = 0f;
        var amplitude = 1f;
        var frequency = 1f;
        for (var octave = 0; octave < octaves; octave++)
        {
            total += Value1D(x * frequency, seed + (uint)octave * OctaveSalt) * amplitude;
            weight += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return weight <= 0f ? 0.5f : total / weight;
    }

    public static float Fractal2D(float x, float y, uint seed, int octaves)
    {
        var total = 0f;
        var weight = 0f;
        var amplitude = 1f;
        var frequency = 1f;
        for (var octave = 0; octave < octaves; octave++)
        {
            total += Value2D(x * frequency, y * frequency, seed + (uint)octave * OctaveSalt) * amplitude;
            weight += amplitude;
            amplitude *= 0.5f;
            frequency *= 2f;
        }

        return weight <= 0f ? 0.5f : total / weight;
    }

    public static float Smooth(float value) => value * value * (3f - 2f * value);

    public static float Lerp(float from, float to, float amount) => from + (to - from) * amount;

    // lowbias32 finaliser constants from Chris Wellons' hash-prospector.
    private static uint Mix(uint value)
    {
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        value ^= value >> 16;
        return value;
    }
}
