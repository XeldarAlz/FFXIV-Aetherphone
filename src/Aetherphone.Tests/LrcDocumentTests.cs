using Aetherphone.Core.Lyrics;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LrcDocumentTests
{
    private const double Tolerance = 0.0001;

    [Fact]
    public void ParsesTwoDigitAndThreeDigitFractions()
    {
        var document = LrcDocument.Parse("[00:12.34]First\n[01:02.345]Second\n");

        Assert.True(document.IsSynced);
        Assert.Equal(2, document.Count);
        Assert.Equal(12.34, document.Starts[0], Tolerance);
        Assert.Equal(62.345, document.Starts[1], Tolerance);
        Assert.Equal("First", document.Texts[0]);
        Assert.Equal("Second", document.Texts[1]);
    }

    [Fact]
    public void AcceptsTimestampsWithoutFractionOrWithColonFraction()
    {
        var document = LrcDocument.Parse("[00:05]Plain seconds\n[00:07:50]Colon fraction");

        Assert.Equal(5, document.Starts[0], Tolerance);
        Assert.Equal(7.5, document.Starts[1], Tolerance);
    }

    [Fact]
    public void ExpandsMultipleTimestampsAndSortsByTime()
    {
        const string lrc = "[00:01.00][00:20.00]Chorus\n[00:10.00]Verse\n";

        var document = LrcDocument.Parse(lrc);

        Assert.Equal(3, document.Count);
        Assert.Equal(new[] { "Chorus", "Verse", "Chorus" }, document.Texts.ToArray());
        Assert.Equal(1, document.Starts[0], Tolerance);
        Assert.Equal(10, document.Starts[1], Tolerance);
        Assert.Equal(20, document.Starts[2], Tolerance);
    }

    [Fact]
    public void KeepsSourceOrderForEqualTimestamps()
    {
        var document = LrcDocument.Parse("[00:03.00]Original\n[00:03.00]Translation\n");

        Assert.Equal("Original", document.Texts[0]);
        Assert.Equal("Translation", document.Texts[1]);
    }

    [Fact]
    public void IgnoresMetadataTags()
    {
        const string lrc = "[ar:Rick Astley]\n[ti:Never Gonna Give You Up]\n[al:Whenever You Need Somebody]\n"
            + "[by:someone]\n[length:03:33]\n[00:18.50]We're no strangers to love\n";

        var document = LrcDocument.Parse(lrc);

        Assert.Equal(1, document.Count);
        Assert.Equal("We're no strangers to love", document.Texts[0]);
    }

    [Fact]
    public void PositiveOffsetShowsLyricsSooner()
    {
        var document = LrcDocument.Parse("[offset:+500]\n[00:10.00]Line\n[00:00.20]Intro");

        Assert.Equal(0, document.Starts[0], Tolerance);
        Assert.Equal(9.5, document.Starts[1], Tolerance);
    }

    [Fact]
    public void NegativeOffsetShowsLyricsLater()
    {
        var document = LrcDocument.Parse("[offset:-1500]\n[00:10.00]Line");

        Assert.Equal(11.5, document.Starts[0], Tolerance);
    }

    [Fact]
    public void KeepsBlankTimedLinesAsInstrumentalGaps()
    {
        var document = LrcDocument.Parse("[00:01.00]Sing\n[00:05.00]\n[00:30.00]Sing again\n\n");

        Assert.Equal(3, document.Count);
        Assert.Equal(string.Empty, document.Texts[1]);
        Assert.Equal(5, document.Starts[1], Tolerance);
    }

    [Fact]
    public void HandlesWindowsLineEndings()
    {
        var document = LrcDocument.Parse("[00:01.00]One\r\n[00:02.00]Two\r\n");

        Assert.Equal(new[] { "One", "Two" }, document.Texts.ToArray());
    }

    [Fact]
    public void ParsesEnhancedWordTiming()
    {
        var document = LrcDocument.Parse("[00:10.00]<00:10.00>Hello <00:10.50>world\n[00:12.00]No words here");

        Assert.True(document.HasWordTiming);
        Assert.Equal("Hello world", document.Texts[0]);
        var words = document.WordsOf(0);
        Assert.Equal(2, words.Length);
        Assert.Equal(10, words[0].Start, Tolerance);
        Assert.Equal("Hello ", document.Texts[0].Substring(words[0].CharStart, words[0].CharLength));
        Assert.Equal(10.5, words[1].Start, Tolerance);
        Assert.Equal("world", document.Texts[0].Substring(words[1].CharStart, words[1].CharLength));
        Assert.Equal(0, document.WordsOf(1).Length);
    }

    [Fact]
    public void WordTimingIsOptional()
    {
        var document = LrcDocument.Parse("[00:10.00]Hello world");

        Assert.False(document.HasWordTiming);
        Assert.Equal(0, document.WordsOf(0).Length);
    }

    [Fact]
    public void FallsBackToPlainWhenNoTimestamps()
    {
        var document = LrcDocument.Parse("First line\nSecond line");

        Assert.False(document.IsSynced);
        Assert.Equal(2, document.Count);
        Assert.Equal(-1, document.LineAt(30));
    }

    [Fact]
    public void PlainTrimsOuterBlankLinesAndKeepsStanzaBreaks()
    {
        var document = LrcDocument.Plain("\n\nVerse one\n\nVerse two\n\n");

        Assert.False(document.IsSynced);
        Assert.Equal(new[] { "Verse one", string.Empty, "Verse two" }, document.Texts.ToArray());
    }

    [Fact]
    public void EmptyInputYieldsEmptyDocument()
    {
        Assert.True(LrcDocument.Parse("   ").IsEmpty);
        Assert.True(LrcDocument.Plain(string.Empty).IsEmpty);
        Assert.Equal(-1, LrcDocument.Empty.LineAt(0));
    }

    [Theory]
    [InlineData(0.5, -1)]
    [InlineData(1.0, 0)]
    [InlineData(4.99, 0)]
    [InlineData(5.0, 1)]
    [InlineData(7.0, 1)]
    [InlineData(10.0, 2)]
    [InlineData(500.0, 3)]
    public void LineAtFindsTheLastStartedLine(double seconds, int expected)
    {
        var document = LrcDocument.Parse("[00:01.00]A\n[00:05.00]B\n[00:10.00]C\n[00:20.00]D");

        Assert.Equal(expected, document.LineAt(seconds));
    }

    [Fact]
    public void LineAtHandlesASingleLine()
    {
        var document = LrcDocument.Parse("[00:02.00]Only");

        Assert.Equal(-1, document.LineAt(1));
        Assert.Equal(0, document.LineAt(2));
        Assert.Equal(0, document.LineAt(100));
    }

    [Fact]
    public void RejectsMalformedTimeTags()
    {
        Assert.False(LrcDocument.TryParseTime("ar:Someone", out _));
        Assert.False(LrcDocument.TryParseTime("00:", out _));
        Assert.False(LrcDocument.TryParseTime(":12.00", out _));
        Assert.False(LrcDocument.TryParseTime("00:1a.00", out _));
        Assert.True(LrcDocument.TryParseTime("123:00.00", out var longSeconds));
        Assert.Equal(7380, longSeconds, Tolerance);
    }

    [Fact]
    public void IgnoresTimeTagsTooLargeToRepresent()
    {
        Assert.False(LrcDocument.TryParseTime("99999999999:00.00", out _));
        Assert.False(LrcDocument.TryParseTime("00:99999999999", out _));

        var document = LrcDocument.Parse("[99999999999:00.00]Broken\n[00:01.00]Kept");

        Assert.Equal(1, document.Count);
        Assert.Equal("Kept", document.Texts[0]);
    }
}
