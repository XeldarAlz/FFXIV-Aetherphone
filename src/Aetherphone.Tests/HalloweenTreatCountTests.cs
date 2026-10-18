using System.Numerics;
using Aetherphone.Core.Coins;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HalloweenTreatCountTests
{
    private const int Total = 13;

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(3, 0, 3)]
    [InlineData(0, 5, 5)]
    [InlineData(4, 7, 7)]
    [InlineData(9, 2, 9)]
    [InlineData(20, 0, Total)]
    public void MergeKeepsTheHigherCountWithinTheTotal(int local, int server, int expected) =>
        Assert.Equal(expected, HalloweenTreatCount.Merge(local, server, Total));

    [Fact]
    public void NextTreatCountsOnFromTheServerWhenItIsAhead() =>
        Assert.Equal(6, HalloweenTreatCount.Next(1, 5, Total));

    [Fact]
    public void NextTreatCountsOnFromTheLocalCountWhenItIsAhead() =>
        Assert.Equal(4, HalloweenTreatCount.Next(3, 0, Total));

    [Fact]
    public void NextTreatNeverPassesTheTotal() =>
        Assert.Equal(Total, HalloweenTreatCount.Next(Total, Total, Total));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(Total)]
    public void MaskHoldsExactlyTheCount(int count) =>
        Assert.Equal(count, BitOperations.PopCount((uint)HalloweenTreatCount.Mask(count)));

    [Fact]
    public void MaskMatchesTheLegacyIncrement()
    {
        var treats = 0;
        for (var count = 1; count <= Total; count++)
        {
            treats |= ~treats & (treats + 1);
            Assert.Equal(treats, HalloweenTreatCount.Mask(count));
        }
    }
}
