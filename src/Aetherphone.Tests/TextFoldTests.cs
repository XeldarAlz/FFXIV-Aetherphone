using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TextFoldTests
{
    private const float LineHeight = 20f;

    [Fact]
    public void TextThatFitsWithTheLinkRowStaysOpen()
    {
        var fold = TextFold.Measure(3 * LineHeight, LineHeight, 2, false);

        Assert.False(fold.Folded);
        Assert.Equal(3 * LineHeight, fold.Height);
    }

    [Fact]
    public void LongerTextFoldsToTheLineCapPlusTheLinkRow()
    {
        var fold = TextFold.Measure(10 * LineHeight, LineHeight, 2, false);

        Assert.True(fold.Folded);
        Assert.Equal(2, fold.VisibleLines);
        Assert.Equal(2 * LineHeight, fold.VisibleHeight);
        Assert.Equal(3 * LineHeight, fold.Height);
    }

    [Fact]
    public void ExpandedTextShowsInFull()
    {
        var fold = TextFold.Measure(10 * LineHeight, LineHeight, 2, true);

        Assert.False(fold.Folded);
        Assert.Equal(10 * LineHeight, fold.Height);
    }
}
