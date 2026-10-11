using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Casino.Originals;

internal struct OriginalsPicker
{
    private const int MaxPopulation = 64;

    private GameRandom random;

    public static OriginalsPicker FromSeed(ulong seed) => new() { random = GameRandom.FromSeed(seed) };

    public int PickOne(ReadOnlySpan<bool> open)
    {
        var available = 0;
        for (var index = 0; index < open.Length; index++)
        {
            if (open[index])
            {
                available++;
            }
        }

        if (available == 0)
        {
            return -1;
        }

        var chosen = random.Next(available);
        for (var index = 0; index < open.Length; index++)
        {
            if (!open[index])
            {
                continue;
            }

            if (chosen == 0)
            {
                return index;
            }

            chosen--;
        }

        return -1;
    }

    public int PickDistinct(Span<int> output, int population)
    {
        var size = Math.Min(population, MaxPopulation);
        var count = Math.Min(output.Length, size);
        Span<int> pool = stackalloc int[MaxPopulation];
        for (var index = 0; index < size; index++)
        {
            pool[index] = index;
        }

        for (var index = 0; index < count; index++)
        {
            var swap = index + random.Next(size - index);
            (pool[index], pool[swap]) = (pool[swap], pool[index]);
            output[index] = pool[index];
        }

        return count;
    }
}
