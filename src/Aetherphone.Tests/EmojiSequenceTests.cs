using System.Collections.Generic;
using Aetherphone.Core.Emoji;
using Xunit;

namespace Aetherphone.Tests;

[Collection("EmojiCatalog")]
public sealed class EmojiSequenceTests
{
    private const string Catalog = """
        {
          "groups": ["Smileys"],
          "emoji": [
            {"file":"1f602","short":["joy"],"group":0,"label":"face with tears of joy","tags":"","tones":[]},
            {"file":"2764","short":["heart"],"group":0,"label":"red heart","tags":"","tones":[]},
            {"file":"1f44d","short":["+1","thumbsup"],"group":0,"label":"thumbs up","tags":"",
             "tones":[{"tone":1,"file":"1f44d-1f3fb"},{"tone":3,"file":"1f44d-1f3fd"}]},
            {"file":"1f3f3-fe0f-200d-1f308","short":["rainbow_flag"],"group":0,"label":"rainbow flag","tags":"",
             "tones":[]},
            {"file":"23-20e3","short":["hash"],"group":0,"label":"keycap #","tags":"","tones":[]},
            {"file":"1f525","short":["fire"],"group":0,"label":"fire","tags":"","tones":[]},
            {"file":"a9","short":["copyright"],"group":0,"label":"copyright","tags":"","tones":[]},
            {"file":"2122","short":["tm"],"group":0,"label":"trade mark","tags":"","tones":[]}
          ]
        }
        """;

    private const string Joy = "\U0001F602";
    private const string Fire = "\U0001F525";
    private const string FaceWithBagsUnderEyes = "\U0001FAE9";

    private readonly List<EmojiSpan> spans = new();

    public EmojiSequenceTests()
    {
        EmojiCatalog.LoadJson(Catalog);
    }

    [Fact]
    public void ASurrogatePairEmojiResolvesToItsImage()
    {
        AssertSingle("hi " + Joy, 3, 2, "1f602");
    }

    [Theory]
    [InlineData("❤️", 2)]
    [InlineData("❤", 1)]
    public void TheHeartResolvesWithOrWithoutThePresentationSelector(string text, int length)
    {
        AssertSingle(text, 0, length, "2764");
    }

    [Fact]
    public void ASkinToneModifierResolvesToTheToneImage()
    {
        AssertSingle("\U0001F44D\U0001F3FD", 0, 4, "1f44d-1f3fd");
    }

    [Theory]
    [InlineData("\U0001F3F3️‍\U0001F308", 6)]
    [InlineData("\U0001F3F3‍\U0001F308", 5)]
    public void AZeroWidthJoinerSequenceKeepsItsPresentationSelectorInTheFileName(string text, int length)
    {
        AssertSingle(text, 0, length, "1f3f3-fe0f-200d-1f308");
    }

    [Theory]
    [InlineData("#️⃣", 3)]
    [InlineData("#⃣", 2)]
    public void AKeycapResolvesWithOrWithoutThePresentationSelector(string text, int length)
    {
        AssertSingle(text, 0, length, "23-20e3");
    }

    [Theory]
    [InlineData("#1 in line")]
    [InlineData("© 2026")]
    [InlineData("Aetherphone™")]
    public void TextDefaultSymbolsStayTextWithoutThePresentationSelector(string text)
    {
        EmojiScanner.Collect(text, spans);
        Assert.Empty(spans);
    }

    [Fact]
    public void ATextDefaultSymbolWithThePresentationSelectorBecomesAnEmoji()
    {
        AssertSingle("©️", 0, 2, "a9");
    }

    [Fact]
    public void ShortcodesAndUnicodeEmojiAreCollectedTogetherInOrder()
    {
        const string text = ":fire: and " + Fire + " :joy:";
        Assert.True(EmojiScanner.MightContain(text));
        EmojiScanner.Collect(text, spans);
        Assert.Equal(3, spans.Count);
        AssertSpan(spans[0], 0, 6, "1f525");
        AssertSpan(spans[1], 11, 2, "1f525");
        AssertSpan(spans[2], 14, 5, "1f602");
    }

    [Fact]
    public void BackToBackShortcodeAndUnicodeEmojiDoNotOverlap()
    {
        EmojiScanner.Collect(Fire + ":fire:" + Fire, spans);
        Assert.Equal(3, spans.Count);
        AssertSpan(spans[0], 0, 2, "1f525");
        AssertSpan(spans[1], 2, 6, "1f525");
        AssertSpan(spans[2], 8, 2, "1f525");
    }

    [Fact]
    public void UnicodeEmojiStillResolveWhenShortcodesAreOff()
    {
        const string text = ":fire: " + Fire;
        Assert.True(EmojiScanner.MightContain(text, false));
        EmojiScanner.Collect(text, spans, false);
        var only = Assert.Single(spans);
        AssertSpan(only, 7, 2, "1f525");
    }

    [Theory]
    [InlineData("just plain words")]
    [InlineData("it’s fine… really")]
    [InlineData("こんにちは")]
    public void TextWithoutEmojiIsRejectedBeforeAnyScan(string text)
    {
        Assert.False(EmojiScanner.MightContain(text));
        EmojiScanner.Collect(text, spans);
        Assert.Empty(spans);
    }

    [Fact]
    public void AnEmojiNewerThanTheCatalogBecomesAPlaceholder()
    {
        const string text = "tired " + FaceWithBagsUnderEyes;
        Assert.True(EmojiScanner.MightContain(text));
        AssertSingle(text, 6, 2, EmojiScanner.MissingFile);
        Assert.True(EmojiScanner.IsMissing(spans[0].File));
    }

    [Fact]
    public void AStraySurrogateBecomesAPlaceholderInsteadOfQuestionMarks()
    {
        AssertSingle("a\uD83D b", 1, 1, EmojiScanner.MissingFile);
    }

    private void AssertSingle(string text, int start, int length, string file)
    {
        Assert.True(EmojiScanner.MightContain(text));
        EmojiScanner.Collect(text, spans);
        var only = Assert.Single(spans);
        AssertSpan(only, start, length, file);
    }

    private static void AssertSpan(EmojiSpan span, int start, int length, string file)
    {
        Assert.Equal(start, span.Start);
        Assert.Equal(length, span.Length);
        Assert.Equal(file, span.File);
    }
}
