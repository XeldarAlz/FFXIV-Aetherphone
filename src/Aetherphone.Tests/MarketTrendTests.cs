using Aetherphone.Core.Market;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MarketTrendTests
{
    private static MarketTrade Trade(long unix, int price, bool hq = false, int quantity = 1) =>
        new(unix, price, quantity, hq);

    [Fact]
    public void MedianOfOddCountIsTheMiddleValue()
    {
        Span<long> values = stackalloc long[] { 900, 100, 500 };
        Assert.Equal(500, MarketTrend.Median(values));
    }

    [Fact]
    public void MedianOfEvenCountAveragesTheMiddlePair()
    {
        Span<long> values = stackalloc long[] { 100, 400, 200, 300 };
        Assert.Equal(250, MarketTrend.Median(values));
    }

    [Fact]
    public void MedianIgnoresATrollSale()
    {
        Span<long> values = stackalloc long[] { 1000, 1100, 999_999_999, 1050, 1 };
        Assert.Equal(1050, MarketTrend.Median(values));
    }

    [Fact]
    public void MedianOfNothingIsZero()
    {
        Assert.Equal(0, MarketTrend.Median(Span<long>.Empty));
    }

    [Fact]
    public void FirstAtOrAfterFindsTheBoundary()
    {
        var trades = new[] { Trade(10, 1), Trade(20, 1), Trade(20, 1), Trade(30, 1) };
        Assert.Equal(0, MarketTrend.FirstAtOrAfter(trades, 5));
        Assert.Equal(1, MarketTrend.FirstAtOrAfter(trades, 20));
        Assert.Equal(3, MarketTrend.FirstAtOrAfter(trades, 25));
        Assert.Equal(4, MarketTrend.FirstAtOrAfter(trades, 31));
    }

    [Fact]
    public void BucketTakesTheMedianPerBucketAndCarriesGapsForward()
    {
        var trades = new[]
        {
            Trade(5, 100), Trade(6, 300), Trade(7, 200),
            Trade(35, 500),
        };
        var medians = new float[4];
        var volumes = new int[4];
        var scratch = new long[trades.Length];
        var filled = MarketTrend.Bucket(trades, false, 0, 40, medians, volumes, scratch);
        Assert.Equal(2, filled);
        Assert.Equal(new[] { 200f, 200f, 200f, 500f }, medians);
        Assert.Equal(new[] { 3, 0, 0, 1 }, volumes);
    }

    [Fact]
    public void BucketBackfillsLeadingEmptyBuckets()
    {
        var trades = new[] { Trade(25, 700), Trade(36, 900) };
        var medians = new float[4];
        var volumes = new int[4];
        var filled = MarketTrend.Bucket(trades, false, 0, 40, medians, volumes, new long[2]);
        Assert.Equal(2, filled);
        Assert.Equal(new[] { 700f, 700f, 700f, 900f }, medians);
    }

    [Fact]
    public void BucketOnlyCountsTheRequestedQuality()
    {
        var trades = new[] { Trade(1, 100, hq: false, quantity: 4), Trade(2, 900, hq: true, quantity: 2) };
        var medians = new float[1];
        var volumes = new int[1];
        MarketTrend.Bucket(trades, true, 0, 10, medians, volumes, new long[2]);
        Assert.Equal(900f, medians[0]);
        Assert.Equal(2, volumes[0]);
    }

    [Fact]
    public void BucketIncludesTheLastSecondOfTheRange()
    {
        var trades = new[] { Trade(40, 123) };
        var medians = new float[4];
        var volumes = new int[4];
        var filled = MarketTrend.Bucket(trades, false, 0, 40, medians, volumes, new long[1]);
        Assert.Equal(1, filled);
        Assert.Equal(1, volumes[3]);
    }

    [Fact]
    public void BucketSkipsTradesBeforeTheRange()
    {
        var trades = new[] { Trade(1, 5), Trade(50, 10) };
        var medians = new float[2];
        var volumes = new int[2];
        var filled = MarketTrend.Bucket(trades, false, 40, 60, medians, volumes, new long[2]);
        Assert.Equal(1, filled);
        Assert.Equal(new[] { 10f, 10f }, medians);
        Assert.Equal(new[] { 0, 1 }, volumes);
    }

    [Fact]
    public void MedianPriceUsesOnlyTheWindowAndQuality()
    {
        var trades = new[] { Trade(1, 1), Trade(10, 100), Trade(11, 300), Trade(12, 9000, hq: true) };
        Assert.Equal(200, MarketTrend.MedianPrice(trades, false, 10, new long[4]));
        Assert.Equal(9000, MarketTrend.MedianPrice(trades, true, 0, new long[4]));
    }

    [Fact]
    public void DominantQualityFollowsTheMajorityOfSales()
    {
        var trades = new[] { Trade(1, 1, hq: true), Trade(2, 1, hq: true), Trade(3, 1) };
        Assert.True(MarketTrend.DominantHq(trades, 0));
        Assert.False(MarketTrend.DominantHq(trades, 3));
    }

    [Theory]
    [InlineData(110L, 100L, 0.1d)]
    [InlineData(90L, 100L, -0.1d)]
    [InlineData(0L, 100L, 0d)]
    [InlineData(100L, 0L, 0d)]
    public void ChangeIsRelativeToTheReference(long current, long reference, double expected)
    {
        Assert.Equal(expected, MarketTrend.Change(current, reference), 6);
    }

    [Fact]
    public void GroupWorldsKeepsTheCheapestPerWorldSortedAscending()
    {
        var listings = new[]
        {
            new MarketListing(500, 2, 1000, false, 1, "Odin", "A"),
            new MarketListing(300, 1, 300, false, 2, "Lich", "B"),
            new MarketListing(450, 5, 2250, false, 1, "Odin", "C"),
            new MarketListing(100, 1, 100, true, 3, "Shiva", "D"),
        };
        var output = new List<MarketWorldOffer>();
        MarketTrend.GroupWorlds(listings, false, output);
        Assert.Equal(2, output.Count);
        Assert.Equal("Lich", output[0].World);
        Assert.Equal("Odin", output[1].World);
        Assert.Equal(450, output[1].Cheapest);
        Assert.Equal(2, output[1].Listings);
        Assert.Equal(7, output[1].Units);
        MarketTrend.GroupWorlds(listings, true, output);
        Assert.Single(output);
        Assert.Equal("Shiva", output[0].World);
    }

    [Theory]
    [InlineData(0L, 1L)]
    [InlineData(150L, 1L)]
    [InlineData(1_200L, 20L)]
    [InlineData(13_500L, 200L)]
    [InlineData(42_000L, 500L)]
    [InlineData(80_000L, 1_000L)]
    public void StepRoundsOnePercentToANiceNumber(long value, long expected)
    {
        Assert.Equal(expected, MarketTrend.Step(value));
    }

    [Theory]
    [InlineData("12,500", 12_500L)]
    [InlineData(" 7.000 ", 7_000L)]
    [InlineData("", 0L)]
    [InlineData("abc", 0L)]
    [InlineData("99999999999999999999999", long.MaxValue)]
    public void ParseGilKeepsOnlyDigits(string text, long expected)
    {
        Assert.Equal(expected, MarketTrend.ParseGil(text));
    }

    [Fact]
    public void HistoryFromSortsByTimeNormalisesMillisecondsAndDropsBadRows()
    {
        var source = new UniversalisHistory
        {
            ItemId = 5057,
            NqSaleVelocity = 2.5,
            Entries = new[]
            {
                new UniversalisHistoryEntry { PricePerUnit = 30, Quantity = 1, Timestamp = 1_791_000_300 },
                new UniversalisHistoryEntry { PricePerUnit = 10, Quantity = 2, Timestamp = 1_791_000_100_000 },
                new UniversalisHistoryEntry { PricePerUnit = 0, Quantity = 1, Timestamp = 1_791_000_200 },
                new UniversalisHistoryEntry { PricePerUnit = 20, Quantity = 0, Timestamp = 1_791_000_200, Hq = true },
            },
        };
        var history = MarketHistory.From(5057, source);
        Assert.Equal(3, history.Trades.Length);
        Assert.Equal(1_791_000_100L, history.Trades[0].Unix);
        Assert.Equal(20, history.Trades[1].Price);
        Assert.True(history.Trades[1].Hq);
        Assert.Equal(1, history.Trades[1].Quantity);
        Assert.Equal(30, history.Trades[2].Price);
        Assert.Equal(2.5, history.Velocity(false));
    }

    [Fact]
    public void HistoryFromNullIsEmpty()
    {
        var history = MarketHistory.From(1, null);
        Assert.Empty(history.Trades);
    }
}
