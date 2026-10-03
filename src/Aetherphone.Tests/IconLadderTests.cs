using Aetherphone.Core.Media;
using Xunit;

namespace Aetherphone.Tests;

public sealed class IconLadderTests
{
    [Fact]
    public void TheWantedLevelWinsWhenItIsResident()
    {
        var resident = IconLadder.Bit(2) | IconLadder.Bit(3) | IconLadder.Bit(4);

        Assert.Equal(3, IconLadder.Nearest(resident, 3));
    }

    [Fact]
    public void TheSmallestLargerLevelIsPreferredOverASmallerOne()
    {
        var resident = IconLadder.Bit(1) | IconLadder.Bit(4) | IconLadder.Bit(5);

        Assert.Equal(4, IconLadder.Nearest(resident, 2));
    }

    [Fact]
    public void TheLargestSmallerLevelServesWhenNothingLargerIsResident()
    {
        var resident = IconLadder.Bit(1) | IconLadder.Bit(2);

        Assert.Equal(2, IconLadder.Nearest(resident, 4));
    }

    [Fact]
    public void NothingResidentMeansNative()
    {
        Assert.Equal(TextureSizes.Native, IconLadder.Nearest(0, 3));
    }

    [Fact]
    public void BitsCoverEveryLadderLevelWithoutOverlap()
    {
        var all = 0;
        for (var level = 1; level <= TextureSizes.LevelCount; level++)
        {
            var bit = IconLadder.Bit(level);
            Assert.Equal(0, all & bit);
            all |= bit;
            Assert.True(IconLadder.IsResident(all, level));
        }

        Assert.Equal((1 << TextureSizes.LevelCount) - 1, all);
    }

    [Fact]
    public void TheHomeGridNeverMinifiesPastTwoToOne()
    {
        var drawnPixels = 49f;
        var level = TextureSizes.LevelFor(drawnPixels);

        Assert.Equal(64, TextureSizes.SizeOf(level));
        Assert.True(TextureSizes.SizeOf(level) <= drawnPixels * 2f);
    }
}
