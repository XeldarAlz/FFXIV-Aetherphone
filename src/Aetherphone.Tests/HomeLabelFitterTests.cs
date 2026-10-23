using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HomeLabelFitterTests
{
    private static float Width(string text) => text.Length;

    [Fact]
    public void AShortNameStaysOnOneLineAtFullSize()
    {
        var layout = HomeLabelFitter.Compute("Clock", 10f, Width);

        Assert.Equal("Clock", layout.First);
        Assert.Null(layout.Second);
        Assert.Equal(1f, layout.Scale);
    }

    [Fact]
    public void ANameSlightlyTooWideShrinksInsteadOfWrapping()
    {
        var layout = HomeLabelFitter.Compute("Yellow Pages", 11f, Width);

        Assert.Equal("Yellow Pages", layout.First);
        Assert.Null(layout.Second);
        Assert.Equal(11f / 12f, layout.Scale, 3);
    }

    [Fact]
    public void ALongNameWrapsAtTheSpaceThatBalancesTheLines()
    {
        var layout = HomeLabelFitter.Compute("Love Island Eclipse", 12f, Width);

        Assert.Equal("Love Island", layout.First);
        Assert.Equal("Eclipse", layout.Second);
        Assert.Equal(1f, layout.Scale);
    }

    [Fact]
    public void WrappedLinesShrinkWhenTheWiderOneStillDoesNotFit()
    {
        var layout = HomeLabelFitter.Compute("Love Island Eclipse", 8f, Width);

        Assert.Equal("Love Island", layout.First);
        Assert.Equal("Eclipse", layout.Second);
        Assert.Equal(8f / 11f, layout.Scale, 3);
    }

    [Fact]
    public void ALongSingleWordShrinksBeforeItBreaks()
    {
        var layout = HomeLabelFitter.Compute("Announcements", 11f, Width);

        Assert.Equal("Announcements", layout.First);
        Assert.Null(layout.Second);
        Assert.Equal(11f / 13f, layout.Scale, 3);
    }

    [Fact]
    public void AVeryLongSingleWordBreaksInsteadOfBeingCut()
    {
        var layout = HomeLabelFitter.Compute("Benachrichtigungen", 10f, Width);

        Assert.NotNull(layout.Second);
        Assert.EndsWith("-", layout.First);
        Assert.Equal("Benachrichtigungen", layout.First.TrimEnd('-') + layout.Second);
        Assert.Equal(1f, layout.Scale);
    }

    [Fact]
    public void AnIdeographicNameBreaksWithoutAHyphen()
    {
        var layout = HomeLabelFitter.Compute("通知センターの設定画面", 5f, Width);

        Assert.NotNull(layout.Second);
        Assert.Equal("通知センターの設定画面", layout.First + layout.Second);
    }

    [Theory]
    [InlineData("Notifications", 6f)]
    [InlineData("Love Island Eclipse", 3f)]
    [InlineData("Benachrichtigungen", 4f)]
    public void NoLayoutEverDropsCharacters(string label, float width)
    {
        var layout = HomeLabelFitter.Compute(label, width, Width);
        var shown = layout.Second is null ? layout.First : layout.First + " " + layout.Second;

        Assert.DoesNotContain("...", shown);
        Assert.DoesNotContain("…", shown);
        Assert.Equal(label.Replace(" ", string.Empty), shown.Replace(" ", string.Empty).Replace("-", string.Empty));
    }
}
