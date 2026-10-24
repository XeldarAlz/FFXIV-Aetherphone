using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class StridePagerLoopTests
{
    [Theory]
    [InlineData(0, 8, 0)]
    [InlineData(7, 8, 7)]
    [InlineData(8, 8, 0)]
    [InlineData(17, 8, 1)]
    [InlineData(-1, 8, 7)]
    [InlineData(-9, 8, 7)]
    public void CardIndexWrapsBothWays(int virtualIndex, int count, int card) =>
        Assert.Equal(card, StridePager.CardIndex(virtualIndex, count));

    [Theory]
    [InlineData(0, 8, 8)]
    [InlineData(15, 8, 15)]
    [InlineData(16, 8, 8)]
    [InlineData(23, 8, 15)]
    [InlineData(7, 8, 15)]
    public void WrapPageKeepsTheMiddleCopy(int page, int count, int wrapped) =>
        Assert.Equal(wrapped, StridePager.WrapPage(page, count));

    [Fact]
    public void WrappingNeverChangesTheCardShown()
    {
        for (var page = 0; page < 24; page++)
        {
            Assert.Equal(StridePager.CardIndex(page, 8), StridePager.CardIndex(StridePager.WrapPage(page, 8), 8));
        }
    }

    [Fact]
    public void EmptyRailsStayAtZero()
    {
        Assert.Equal(0, StridePager.CardIndex(5, 0));
        Assert.Equal(0, StridePager.WrapPage(5, 0));
    }
}
