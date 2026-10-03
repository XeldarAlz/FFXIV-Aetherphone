namespace Aetherphone.Core.Market;

internal enum MarketRange : byte
{
    Day,
    Week,
    Month,
}

internal static class MarketTrend
{
    public const long DaySeconds = 86_400L;
    public const int WatchPoints = 14;
    public const long WatchSeconds = 7L * DaySeconds;

    private static readonly TradeTimeComparer TimeComparer = new();

    public static int Buckets(MarketRange range) => range switch
    {
        MarketRange.Day => 24,
        MarketRange.Week => 28,
        _ => 30,
    };

    public static long Seconds(MarketRange range) => range switch
    {
        MarketRange.Day => DaySeconds,
        MarketRange.Week => 7L * DaySeconds,
        _ => 30L * DaySeconds,
    };

    public static void SortByTime(MarketTrade[] trades) => Array.Sort(trades, TimeComparer);

    public static long Median(Span<long> values)
    {
        if (values.Length == 0)
        {
            return 0;
        }

        values.Sort();
        var middle = values.Length / 2;
        if ((values.Length & 1) == 1)
        {
            return values[middle];
        }

        return (values[middle - 1] + values[middle] + 1) / 2;
    }

    public static int FirstAtOrAfter(ReadOnlySpan<MarketTrade> trades, long unix)
    {
        var low = 0;
        var high = trades.Length;
        while (low < high)
        {
            var middle = (low + high) >>> 1;
            if (trades[middle].Unix < unix)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    public static int Bucket(ReadOnlySpan<MarketTrade> trades, bool hq, long fromUnix, long toUnix,
        Span<float> medians, Span<int> volumes, Span<long> scratch)
    {
        var count = Math.Min(medians.Length, volumes.Length);
        medians.Clear();
        volumes.Clear();
        if (count == 0 || toUnix <= fromUnix)
        {
            return 0;
        }

        var span = toUnix - fromUnix;
        var cursor = FirstAtOrAfter(trades, fromUnix);
        var filled = 0;
        var firstFilled = -1;
        var carried = 0f;
        for (var bucketIndex = 0; bucketIndex < count; bucketIndex++)
        {
            var bucketEnd = bucketIndex == count - 1 ? toUnix + 1 : fromUnix + span * (bucketIndex + 1) / count;
            var gathered = 0;
            var units = 0;
            while (cursor < trades.Length && trades[cursor].Unix < bucketEnd)
            {
                var trade = trades[cursor];
                cursor++;
                if (trade.Hq != hq || gathered >= scratch.Length)
                {
                    continue;
                }

                scratch[gathered] = trade.Price;
                gathered++;
                units += trade.Quantity;
            }

            volumes[bucketIndex] = units;
            if (gathered == 0)
            {
                medians[bucketIndex] = carried;
                continue;
            }

            carried = Median(scratch[..gathered]);
            medians[bucketIndex] = carried;
            filled++;
            if (firstFilled < 0)
            {
                firstFilled = bucketIndex;
            }
        }

        for (var bucketIndex = 0; bucketIndex < firstFilled; bucketIndex++)
        {
            medians[bucketIndex] = medians[firstFilled];
        }

        return filled;
    }

    public static long MedianPrice(ReadOnlySpan<MarketTrade> trades, bool hq, long fromUnix, Span<long> scratch)
    {
        var gathered = 0;
        for (var index = FirstAtOrAfter(trades, fromUnix); index < trades.Length && gathered < scratch.Length; index++)
        {
            if (trades[index].Hq != hq)
            {
                continue;
            }

            scratch[gathered] = trades[index].Price;
            gathered++;
        }

        return Median(scratch[..gathered]);
    }

    public static bool DominantHq(ReadOnlySpan<MarketTrade> trades, long fromUnix)
    {
        var hqCount = 0;
        var nqCount = 0;
        for (var index = FirstAtOrAfter(trades, fromUnix); index < trades.Length; index++)
        {
            if (trades[index].Hq)
            {
                hqCount++;
            }
            else
            {
                nqCount++;
            }
        }

        return hqCount > nqCount;
    }

    public static double Change(long current, long reference)
    {
        if (current <= 0 || reference <= 0)
        {
            return 0d;
        }

        return (current - (double)reference) / reference;
    }

    public static long Step(long value)
    {
        var target = Math.Max(1L, value / 100L);
        var magnitude = 1L;
        while (magnitude * 10L <= target)
        {
            magnitude *= 10L;
        }

        if (target <= magnitude)
        {
            return magnitude;
        }

        if (target <= magnitude * 2L)
        {
            return magnitude * 2L;
        }

        return target <= magnitude * 5L ? magnitude * 5L : magnitude * 10L;
    }

    public static long ParseGil(string text)
    {
        var value = 0L;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character < '0' || character > '9')
            {
                continue;
            }

            if (value > (long.MaxValue - 9L) / 10L)
            {
                return long.MaxValue;
            }

            value = value * 10L + (character - '0');
        }

        return value;
    }

    public static void GroupWorlds(ReadOnlySpan<MarketListing> listings, bool hq, List<MarketWorldOffer> output)
    {
        output.Clear();
        for (var index = 0; index < listings.Length; index++)
        {
            var listing = listings[index];
            if (listing.Hq != hq || listing.World.Length == 0 || listing.PricePerUnit <= 0)
            {
                continue;
            }

            var slot = -1;
            for (var offerIndex = 0; offerIndex < output.Count; offerIndex++)
            {
                if (string.Equals(output[offerIndex].World, listing.World, StringComparison.Ordinal))
                {
                    slot = offerIndex;
                    break;
                }
            }

            if (slot < 0)
            {
                output.Add(new MarketWorldOffer(listing.WorldId, listing.World, listing.PricePerUnit, 1,
                    listing.Quantity));
                continue;
            }

            var existing = output[slot];
            output[slot] = new MarketWorldOffer(existing.WorldId, existing.World,
                Math.Min(existing.Cheapest, listing.PricePerUnit), existing.Listings + 1,
                existing.Units + listing.Quantity);
        }

        for (var index = 1; index < output.Count; index++)
        {
            var offer = output[index];
            var cursor = index - 1;
            while (cursor >= 0 && output[cursor].Cheapest > offer.Cheapest)
            {
                output[cursor + 1] = output[cursor];
                cursor--;
            }

            output[cursor + 1] = offer;
        }
    }

    private sealed class TradeTimeComparer : IComparer<MarketTrade>
    {
        public int Compare(MarketTrade left, MarketTrade right) => left.Unix.CompareTo(right.Unix);
    }
}
