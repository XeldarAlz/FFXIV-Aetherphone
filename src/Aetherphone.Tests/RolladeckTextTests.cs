using Aetherphone.Core.Rolladeck;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RolladeckTextTests
{
    [Fact]
    public void Normalize_Null_ReturnsEmpty() =>
        Assert.Equal(string.Empty, RolladeckText.Normalize(null));

    [Fact]
    public void Normalize_Empty_ReturnsEmpty() =>
        Assert.Equal(string.Empty, RolladeckText.Normalize(string.Empty));

    [Fact]
    public void Normalize_PlainAscii_ReturnedUnchanged() =>
        Assert.Equal("DJ Name", RolladeckText.Normalize("DJ Name"));

    [Theory]
    [InlineData(0x1D400)] // Mathematical Bold
    [InlineData(0x1D434)] // Mathematical Italic
    [InlineData(0x1D468)] // Mathematical Bold Italic
    [InlineData(0x1D49C)] // Mathematical Script
    [InlineData(0x1D4D0)] // Mathematical Bold Script
    [InlineData(0x1D504)] // Mathematical Fraktur
    [InlineData(0x1D538)] // Mathematical Double-Struck
    [InlineData(0x1D56C)] // Mathematical Bold Fraktur
    [InlineData(0x1D5A0)] // Mathematical Sans-Serif
    [InlineData(0x1D5D4)] // Mathematical Sans-Serif Bold
    [InlineData(0x1D608)] // Mathematical Sans-Serif Italic
    [InlineData(0x1D63C)] // Mathematical Sans-Serif Bold Italic
    [InlineData(0x1D670)] // Mathematical Monospace
    public void Normalize_LetterStyleUppercaseA_MapsToA(int styleBase)
    {
        Assert.Equal("A", RolladeckText.Normalize(char.ConvertFromUtf32(styleBase)));
    }

    [Theory]
    [InlineData(0x1D400 + 25, "Z")] // last uppercase in Bold block
    [InlineData(0x1D400 + 26, "a")] // first lowercase in Bold block
    [InlineData(0x1D400 + 51, "z")] // last lowercase in Bold block
    public void Normalize_LetterBlockBoundaries(int codepoint, string expected)
    {
        Assert.Equal(expected, RolladeckText.Normalize(char.ConvertFromUtf32(codepoint)));
    }

    [Theory]
    [InlineData(0x1D7CE)] // Bold
    [InlineData(0x1D7D8)] // Double-Struck
    [InlineData(0x1D7E2)] // Sans-Serif
    [InlineData(0x1D7EC)] // Sans-Serif Bold
    [InlineData(0x1D7F6)] // Monospace
    public void Normalize_DigitStyleZero_MapsToZero(int digitBase)
    {
        Assert.Equal("0", RolladeckText.Normalize(char.ConvertFromUtf32(digitBase)));
    }

    [Fact]
    public void Normalize_LastDigitInBlock_MapsToNine() =>
        Assert.Equal("9", RolladeckText.Normalize(char.ConvertFromUtf32(0x1D7CE + 9)));

    [Fact]
    public void Normalize_Emoji_Stripped() =>
        Assert.Equal(string.Empty, RolladeckText.Normalize(char.ConvertFromUtf32(0x1F600)));

    [Fact]
    public void Normalize_MathBoldDjName_NormalizesCorrectly()
    {
        // U+1D5D7 D, U+1D5DD J, U+1D5E1 N, U+1D5EE a, U+1D5FA m, U+1D5F2 e
        // (Mathematical Sans-Serif Bold)
        var input = char.ConvertFromUtf32(0x1D5D7)
                  + char.ConvertFromUtf32(0x1D5DD)
                  + " "
                  + char.ConvertFromUtf32(0x1D5E1)
                  + char.ConvertFromUtf32(0x1D5EE)
                  + char.ConvertFromUtf32(0x1D5FA)
                  + char.ConvertFromUtf32(0x1D5F2);
        Assert.Equal("DJ Name", RolladeckText.Normalize(input));
    }

    [Fact]
    public void Normalize_EmojiAfterText_StrippedAndTrimmed()
    {
        var boldA = char.ConvertFromUtf32(0x1D400); // Mathematical Bold A
        var emoji  = char.ConvertFromUtf32(0x1F600);
        Assert.Equal("A", RolladeckText.Normalize(boldA + " " + emoji));
    }

    [Fact]
    public void Normalize_OnlyEmojiSurroundedBySpaces_Trimmed()
    {
        var emoji = char.ConvertFromUtf32(0x1F600);
        Assert.Equal(string.Empty, RolladeckText.Normalize("  " + emoji + "  "));
    }
}
