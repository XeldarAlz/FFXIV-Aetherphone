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
        Assert.Equal(PostBreak.Allowed, PostText.CanBreak("a\nb\nc\nd", 7, 100));
        Assert.Equal(PostBreak.LineCap, PostText.CanBreak("a\nb\nc\nd\ne", 9, 100));
    }

    [Fact]
    public void BlankLinesBetweenParagraphsDoNotCountAsLines()
    {
        const string text = "a\n\nb\n\nc\n\nd";

        Assert.Equal(PostBreak.Allowed, PostText.CanBreak(text, text.Length, 100));
        Assert.Equal(PostBreak.LineCap, PostText.CanBreak(text + "\n\ne", text.Length + 3, 100));
    }

    [Fact]
    public void OnlyOneBlankLineFitsBetweenParagraphs()
    {
        Assert.Equal(PostBreak.BlankRun, PostText.CanBreak("a\n\nb", 3, 100));
        Assert.Equal(PostBreak.BlankRun, PostText.CanBreak("a\n\nb", 2, 100));
        Assert.Equal(PostBreak.Allowed, PostText.CanBreak("a\nb", 2, 100));
    }

    [Fact]
    public void ABlankLineCanStillGoBeforeTheFifthParagraph()
    {
        const string text = "a\nb\nc\nd\ne";

        Assert.Equal(PostBreak.Allowed, PostText.CanBreak(text, text.Length - 1, 100));
        Assert.Equal(PostBreak.LineCap, PostText.CanBreak(text, text.Length, 100));
    }

    [Fact]
    public void ALineBreakNeedsRoomForBothOfItsCharacters()
    {
        Assert.Equal(PostBreak.Allowed, PostText.CanBreak("abcdefgh", 8, 10));
        Assert.Equal(PostBreak.NoRoom, PostText.CanBreak("abcdefghi", 9, 10));
    }

    [Fact]
    public void PastedRunsOfBlankLinesCollapseToOne()
    {
        var cursor = 6;

        var fitted = PostText.Fit("a\n\n\n\nb", 100, ref cursor);

        Assert.Equal("a\n\nb", fitted);
        Assert.Equal(4, cursor);
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

        var fitted = PostText.Fit("1\n\n2\n3\n4\n5\n6\n7", 100, ref cursor);

        Assert.Equal("1\n\n2\n3\n4\n5 6 7", fitted);
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
