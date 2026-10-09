using System.Collections.Generic;
using Aetherphone.Core.Emoji;
using Xunit;

namespace Aetherphone.Tests;

[Collection("EmojiCatalog")]
public sealed class EmojiShortcodeTests
{
    private const string Catalog = """
        {
          "groups": ["Smileys"],
          "emoji": [
            {"file":"1f604","short":["smile"],"group":0,"label":"smiling face","tags":"happy","tones":[]},
            {"file":"1f603","short":["smiley"],"group":0,"label":"grinning face","tags":"","tones":[]},
            {"file":"1f60a","short":["smiling_face"],"group":0,"label":"blushing face","tags":"","tones":[]},
            {"file":"1f6e9","short":["small_airplane"],"group":0,"label":"small airplane","tags":"","tones":[]},
            {"file":"1f44b","short":["wave","waving_hand"],"group":0,"label":"waving hand","tags":"",
             "tones":[{"tone":1,"file":"1f44b-1f3fb"}]}
          ]
        }
        """;

    private readonly List<EmojiSpan> spans = new();

    public EmojiShortcodeTests()
    {
        EmojiCatalog.LoadJson(Catalog);
    }

    [Fact]
    public void EveryAliasAndToneResolvesToItsImage()
    {
        Assert.True(EmojiCatalog.Ready);
        Assert.True(EmojiCatalog.TryResolve("smile", out var smile));
        Assert.Equal("1f604", smile);
        Assert.True(EmojiCatalog.TryResolve("waving_hand", out var alias));
        Assert.Equal("1f44b", alias);
        Assert.True(EmojiCatalog.TryResolve("wave_tone1", out var toned));
        Assert.Equal("1f44b-1f3fb", toned);
        Assert.False(EmojiCatalog.TryResolve("not_a_shortcode", out _));
    }

    [Fact]
    public void TheSpanLookupMatchesTheStringLookup()
    {
        const string line = "say :smile: now";
        Assert.True(EmojiShortcodes.TryResolve(line.AsSpan(5, 5), out var file));
        Assert.Equal("1f604", file);
    }

    [Fact]
    public void ALineWithoutAColonIsRejectedOutright()
    {
        Assert.False(EmojiShortcodes.MightContain("nothing to see here"));
        Assert.True(EmojiShortcodes.MightContain("a :smile: here"));
        Assert.True(EmojiShortcodes.MightContain("12:30 raid"));
    }

    [Fact]
    public void EverySpanInALineIsCollectedInOrder()
    {
        EmojiShortcodes.Collect("hi :smile: and :wave: ok", spans);
        Assert.Equal(2, spans.Count);
        Assert.Equal(3, spans[0].Start);
        Assert.Equal(7, spans[0].Length);
        Assert.Equal("1f604", spans[0].File);
        Assert.Equal(15, spans[1].Start);
        Assert.Equal(6, spans[1].Length);
        Assert.Equal("1f44b", spans[1].File);
    }

    [Theory]
    [InlineData("what :smile is missing")]
    [InlineData(":smile")]
    [InlineData("raid at 12:30")]
    [InlineData("no :such_code: here")]
    [InlineData("::")]
    public void NothingIsCollectedWithoutAClosedKnownShortcode(string line)
    {
        EmojiShortcodes.Collect(line, spans);
        Assert.Empty(spans);
    }

    [Fact]
    public void BackToBackShortcodesBothResolve()
    {
        EmojiShortcodes.Collect(":smile::wave:", spans);
        Assert.Equal(2, spans.Count);
        Assert.Equal(0, spans[0].Start);
        Assert.Equal(7, spans[0].Length);
        Assert.Equal(7, spans[1].Start);
        Assert.Equal(6, spans[1].Length);
    }

    [Theory]
    [InlineData("hey :sma", 4, 3)]
    [InlineData(":sm", 0, 2)]
    [InlineData("first :smile: then :wav", 19, 3)]
    public void TheTokenBeingTypedIsFound(string draft, int start, int length)
    {
        Assert.True(EmojiAutocomplete.TryToken(draft, draft.Length, out var foundStart, out var foundLength));
        Assert.Equal(start, foundStart);
        Assert.Equal(length, foundLength);
    }

    [Theory]
    [InlineData("hey :smile:")]
    [InlineData("https://example.com")]
    [InlineData("hey:sm")]
    [InlineData("plain text")]
    [InlineData("")]
    public void NothingIsOfferedWhenNoTokenIsBeingTyped(string draft)
    {
        Assert.False(EmojiAutocomplete.TryToken(draft, draft.Length, out _, out _));
    }

    [Fact]
    public void RankingPutsTheExactMatchFirstThenTheShortestPrefix()
    {
        var results = new EmojiShortcode[8];
        var count = EmojiAutocomplete.Rank("smile", results);
        Assert.Equal(2, count);
        Assert.Equal("smile", results[0].Code);
        Assert.Equal("smiley", results[1].Code);
    }

    [Fact]
    public void PrefixMatchesRankShortestFirstAndBeatContainsMatches()
    {
        var results = new EmojiShortcode[8];
        var count = EmojiAutocomplete.Rank("sm", results);
        Assert.Equal(4, count);
        Assert.Equal("smile", results[0].Code);
        Assert.Equal("smiley", results[1].Code);
        Assert.Equal("smiling_face", results[2].Code);
        Assert.Equal("small_airplane", results[3].Code);
    }

    [Fact]
    public void AContainsMatchStillSurfaces()
    {
        var results = new EmojiShortcode[8];
        var count = EmojiAutocomplete.Rank("airplane", results);
        Assert.Equal(1, count);
        Assert.Equal("small_airplane", results[0].Code);
    }

    [Fact]
    public void RankingNeverWritesPastTheCallersBuffer()
    {
        var results = new EmojiShortcode[2];
        var count = EmojiAutocomplete.Rank("sm", results);
        Assert.Equal(2, count);
        Assert.Equal("smile", results[0].Code);
        Assert.Equal("smiley", results[1].Code);
    }

    [Fact]
    public void ToneVariantsStayOutOfTheSuggestions()
    {
        var results = new EmojiShortcode[8];
        var count = EmojiAutocomplete.Rank("wave", results);
        Assert.Equal(1, count);
        Assert.Equal("wave", results[0].Code);
    }

    [Fact]
    public void FavoritesKeepTheMostRecentPickFirst()
    {
        var codes = new List<string> { "wave", "smile" };
        Assert.True(EmojiFavorites.Promote(codes, "smile"));
        Assert.Equal(new[] { "smile", "wave" }, codes);
        Assert.False(EmojiFavorites.Promote(codes, "smile"));
        Assert.True(EmojiFavorites.Promote(codes, "smiley"));
        Assert.Equal(new[] { "smiley", "smile", "wave" }, codes);
    }

    [Fact]
    public void FavoritesStopAtTheCap()
    {
        var codes = new List<string>();
        for (var index = 0; index < EmojiFavorites.Capacity + 4; index++)
        {
            EmojiFavorites.Promote(codes, "code" + index);
        }

        Assert.Equal(EmojiFavorites.Capacity, codes.Count);
        Assert.Equal("code" + (EmojiFavorites.Capacity + 3), codes[0]);
        Assert.Equal("code4", codes[EmojiFavorites.Capacity - 1]);
    }
}
