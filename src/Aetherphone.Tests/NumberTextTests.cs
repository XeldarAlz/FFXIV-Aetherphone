using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class NumberTextTests
{
    private static string Expected(long value) => value.ToString("N0", Loc.Culture);

    [Fact]
    public void GroupMatchesTheUncachedFormat()
    {
        foreach (var value in new long[]
                 {
                     0, 1, 9, 10, 99, 100, 999, 1000, 1001, 9999, 10_000, 123_456,
                     1_000_000, 999_999_999, long.MaxValue,
                     -1, -999, -1000, -123_456, long.MinValue,
                 })
        {
            Assert.Equal(Expected(value), NumberText.Group(value));
        }
    }

    [Fact]
    public void GroupStaysCorrectPastTheCacheLimit()
    {
        for (long value = 0; value < 5000; value++)
        {
            Assert.Equal(Expected(value), NumberText.Group(value));
        }
    }

    [Fact]
    public void GroupIsStableAcrossRepeatedCalls()
    {
        var first = NumberText.Group(1_234_567);
        for (var attempt = 0; attempt < 50; attempt++)
        {
            Assert.Equal(first, NumberText.Group(1_234_567));
        }

        Assert.Equal(Expected(1_234_567), first);
    }

    [Fact]
    public void CompactReadsInThousandsMillionsBillionsAndTrillions()
    {
        Assert.Equal("0", NumberText.Compact(0));
        Assert.Equal("999", NumberText.Compact(999));
        Assert.Equal("1K", NumberText.Compact(1000));
        Assert.Equal(Decimal(12.5m) + "K", NumberText.Compact(12_560));
        Assert.Equal("125K", NumberText.Compact(125_999));
        Assert.Equal(Decimal(3.2m) + "M", NumberText.Compact(3_200_000));
        Assert.Equal(Decimal(1.1m) + "B", NumberText.Compact(1_100_000_000));
        Assert.Equal("4T", NumberText.Compact(4_000_000_000_000));
        Assert.Equal("-" + Decimal(2.5m) + "K", NumberText.Compact(-2_500));
    }

    [Fact]
    public void CompactNeverRoundsABalanceUp()
    {
        Assert.Equal("999K", NumberText.Compact(999_999));
        Assert.Equal(Decimal(9.9m) + "M", NumberText.Compact(9_999_999));
    }

    [Fact]
    public void SignedPutsAPlusOnGainsOnly()
    {
        Assert.Equal("+" + Expected(1234), NumberText.Signed(1234));
        Assert.Equal(Expected(-1234), NumberText.Signed(-1234));
        Assert.Equal(Expected(0), NumberText.Signed(0));
        Assert.Same(NumberText.Signed(5000), NumberText.Signed(5000));
    }

    private static string Decimal(decimal value) => value.ToString("0.0", Loc.Culture);

    [Fact]
    public void GroupSeparatesNeighbouringValues()
    {
        Assert.NotEqual(NumberText.Group(1000), NumberText.Group(1001));
        Assert.NotEqual(NumberText.Group(-1000), NumberText.Group(1000));
    }
}
