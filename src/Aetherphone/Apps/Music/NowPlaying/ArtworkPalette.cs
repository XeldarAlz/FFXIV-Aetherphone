namespace Aetherphone.Apps.Music.NowPlaying;

internal readonly record struct ArtworkSwatch(Vector4 Primary, Vector4 Secondary, Vector4 Tertiary,
    Vector4 Quaternary)
{
    public Vector4 At(int index)
    {
        return index switch
        {
            0 => Primary,
            1 => Secondary,
            2 => Tertiary,
            _ => Quaternary,
        };
    }

    public static ArtworkSwatch Lerp(in ArtworkSwatch from, in ArtworkSwatch to, float amount)
    {
        return new ArtworkSwatch(Vector4.Lerp(from.Primary, to.Primary, amount),
            Vector4.Lerp(from.Secondary, to.Secondary, amount), Vector4.Lerp(from.Tertiary, to.Tertiary, amount),
            Vector4.Lerp(from.Quaternary, to.Quaternary, amount));
    }
}

internal static class ArtworkPalette
{
    public const int ColorCount = 4;
    public const int SampleDimension = 48;
    private const int BytesPerPixel = 4;
    private const int Iterations = 10;
    private const byte MinimumAlpha = 128;
    private const float DarkLuma = 0.06f;
    private const int DarkMajorityDivisor = 10;
    private const float RedWeight = 0.299f;
    private const float GreenWeight = 0.587f;
    private const float BlueWeight = 0.114f;
    private const float ByteToUnit = 1f / 255f;

    public static readonly ArtworkSwatch Neutral = new(new Vector4(0.32f, 0.30f, 0.36f, 1f),
        new Vector4(0.22f, 0.24f, 0.30f, 1f), new Vector4(0.40f, 0.34f, 0.38f, 1f),
        new Vector4(0.18f, 0.18f, 0.22f, 1f));

    public static ArtworkSwatch Extract(ReadOnlySpan<byte> rgba, int width, int height)
    {
        var pixelCount = Math.Min(Math.Max(0, width) * Math.Max(0, height), rgba.Length / BytesPerPixel);
        if (pixelCount <= 0)
        {
            return Neutral;
        }

        var includeDark = CountBright(rgba, pixelCount) < pixelCount / DarkMajorityDivisor;
        Span<Vector3> centers = stackalloc Vector3[ColorCount];
        Span<Vector3> sums = stackalloc Vector3[ColorCount];
        Span<int> counts = stackalloc int[ColorCount];
        if (!Seed(rgba, pixelCount, includeDark, centers))
        {
            return Neutral;
        }

        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            sums.Clear();
            counts.Clear();
            Assign(rgba, pixelCount, includeDark, centers, sums, counts);
            var moved = false;
            for (var centerIndex = 0; centerIndex < ColorCount; centerIndex++)
            {
                if (counts[centerIndex] == 0)
                {
                    continue;
                }

                var updated = sums[centerIndex] / counts[centerIndex];
                moved |= Vector3.DistanceSquared(updated, centers[centerIndex]) > 1e-7f;
                centers[centerIndex] = updated;
            }

            if (!moved)
            {
                break;
            }
        }

        sums.Clear();
        counts.Clear();
        Assign(rgba, pixelCount, includeDark, centers, sums, counts);
        Span<int> order = stackalloc int[ColorCount];
        SortByPopulation(counts, order);
        var largest = centers[order[0]];
        return new ArtworkSwatch(Pick(centers, counts, order, 0, largest), Pick(centers, counts, order, 1, largest),
            Pick(centers, counts, order, 2, largest), Pick(centers, counts, order, 3, largest));
    }

    private static Vector4 Pick(ReadOnlySpan<Vector3> centers, ReadOnlySpan<int> counts, ReadOnlySpan<int> order,
        int rank, Vector3 fallback)
    {
        var center = counts[order[rank]] > 0 ? centers[order[rank]] : fallback;
        return new Vector4(center, 1f);
    }

    private static int CountBright(ReadOnlySpan<byte> rgba, int pixelCount)
    {
        var count = 0;
        for (var pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
        {
            if (Usable(rgba, pixelIndex, false, out _))
            {
                count++;
            }
        }

        return count;
    }

    private static bool Seed(ReadOnlySpan<byte> rgba, int pixelCount, bool includeDark, Span<Vector3> centers)
    {
        var mean = Vector3.Zero;
        var usable = 0;
        for (var pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
        {
            if (!Usable(rgba, pixelIndex, includeDark, out var color))
            {
                continue;
            }

            mean += color;
            usable++;
        }

        if (usable == 0)
        {
            return false;
        }

        centers[0] = mean / usable;
        for (var centerIndex = 1; centerIndex < ColorCount; centerIndex++)
        {
            var farthest = centers[0];
            var farthestDistance = -1f;
            for (var pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
            {
                if (!Usable(rgba, pixelIndex, includeDark, out var color))
                {
                    continue;
                }

                var nearest = NearestDistance(centers[..centerIndex], color);
                if (nearest > farthestDistance)
                {
                    farthestDistance = nearest;
                    farthest = color;
                }
            }

            centers[centerIndex] = farthest;
        }

        return true;
    }

    private static void Assign(ReadOnlySpan<byte> rgba, int pixelCount, bool includeDark,
        ReadOnlySpan<Vector3> centers, Span<Vector3> sums, Span<int> counts)
    {
        for (var pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
        {
            if (!Usable(rgba, pixelIndex, includeDark, out var color))
            {
                continue;
            }

            var nearest = NearestIndex(centers, color);
            sums[nearest] += color;
            counts[nearest]++;
        }
    }

    private static bool Usable(ReadOnlySpan<byte> rgba, int pixelIndex, bool includeDark, out Vector3 color)
    {
        var offset = pixelIndex * BytesPerPixel;
        color = new Vector3(rgba[offset], rgba[offset + 1], rgba[offset + 2]) * ByteToUnit;
        if (rgba[offset + 3] < MinimumAlpha)
        {
            return false;
        }

        return includeDark || color.X * RedWeight + color.Y * GreenWeight + color.Z * BlueWeight >= DarkLuma;
    }

    private static float NearestDistance(ReadOnlySpan<Vector3> centers, Vector3 color)
    {
        var best = float.MaxValue;
        for (var centerIndex = 0; centerIndex < centers.Length; centerIndex++)
        {
            best = MathF.Min(best, Vector3.DistanceSquared(centers[centerIndex], color));
        }

        return best;
    }

    private static int NearestIndex(ReadOnlySpan<Vector3> centers, Vector3 color)
    {
        var best = 0;
        var bestDistance = float.MaxValue;
        for (var centerIndex = 0; centerIndex < centers.Length; centerIndex++)
        {
            var distance = Vector3.DistanceSquared(centers[centerIndex], color);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = centerIndex;
            }
        }

        return best;
    }

    private static void SortByPopulation(ReadOnlySpan<int> counts, Span<int> order)
    {
        for (var index = 0; index < order.Length; index++)
        {
            order[index] = index;
        }

        for (var index = 1; index < order.Length; index++)
        {
            var current = order[index];
            var scan = index - 1;
            while (scan >= 0 && counts[order[scan]] < counts[current])
            {
                order[scan + 1] = order[scan];
                scan--;
            }

            order[scan + 1] = current;
        }
    }
}
