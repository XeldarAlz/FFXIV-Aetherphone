using Aetherphone.Core.Social;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PostTextTests
{
    [Fact]
    public void ALineBreakWeighsTwoCharacters()
    {
        Assert.Equal(6, PostText.Weight("ab\ncd"));
        Assert.Equal(4, PostText.Weight("abcd"));
    }

    [Fact]
    public void TheEmojiBudgetShrinksByTheExtraLineBreakWeight()
    {
        Assert.Equal(98, PostText.CharacterBudget("a\nb\nc", 100));
    }

    [Fact]
    public void AFifthLineIsTheLastOneAllowed()
    {
        Assert.True(PostText.CanBreak("a\nb\nc\nd", 100));
        Assert.False(PostText.CanBreak("a\nb\nc\nd\ne", 100));
    }

    [Fact]
    public void ALineBreakNeedsRoomForBothOfItsCharacters()
    {
        Assert.True(PostText.CanBreak("abcdefgh", 10));
        Assert.False(PostText.CanBreak("abcdefghi", 10));
    }

    [Fact]
    public void TextWithinTheRulesIsReturnedUntouched()
    {
        var cursor = 3;
        const string text = "one\ntwo";

        var fitted = PostText.Fit(text, 100, ref cursor);

        Assert.Same(text, fitted);
        Assert.Equal(3, cursor);
    }

    [Fact]
    public void LineBreaksPastTheFifthLineBecomeSpaces()
    {
        var cursor = 0;

        var fitted = PostText.Fit("1\n2\n3\n4\n5\n6\n7", 100, ref cursor);

        Assert.Equal("1\n2\n3\n4\n5 6 7", fitted);
    }

    [Fact]
    public void FittingCutsAtTheWeightedLimitAndClampsTheCursor()
    {
        var cursor = 9;

        var fitted = PostText.Fit("abc\ndefgh", 6, ref cursor);

        Assert.Equal("abc\nd", fitted);
        Assert.Equal(5, cursor);
    }

    [Fact]
    public void FittingNeverSplitsASurrogatePair()
    {
        var cursor = 0;

        var fitted = PostText.Fit("ab\U0001F600", 3, ref cursor);

        Assert.Equal("ab", fitted);
    }
}
