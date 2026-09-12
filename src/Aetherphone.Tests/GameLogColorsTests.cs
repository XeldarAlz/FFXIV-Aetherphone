using Aetherphone.Core.GameChat;
using Xunit;

namespace Aetherphone.Tests;

public sealed class GameLogColorsTests
{
    private const float Tolerance = 0.002f;

    [Fact]
    public void DecodesPackedRgbWithRedInTheHighByte()
    {
        Assert.True(GameLogColors.TryDecode(0x00FF8040u, out var color));
        Assert.Equal(1f, color.X, Tolerance);
        Assert.Equal(128f / 255f, color.Y, Tolerance);
        Assert.Equal(64f / 255f, color.Z, Tolerance);
        Assert.Equal(1f, color.W, Tolerance);
    }

    [Fact]
    public void IgnoresBitsAboveTheRgbBytes()
    {
        Assert.True(GameLogColors.TryDecode(0xFF0000FFu, out var color));
        Assert.Equal(0f, color.X, Tolerance);
        Assert.Equal(1f, color.Z, Tolerance);
    }

    [Fact]
    public void ZeroMeansTheGameHasNoColorForTheChannel()
    {
        Assert.False(GameLogColors.TryDecode(0xFF000000u, out _));
    }

    [Theory]
    [InlineData("say", "ColorSay")]
    [InlineData("fc", "ColorFCompany")]
    [InlineData("novice", "ColorBeginner")]
    [InlineData("ls3", "ColorLS3")]
    [InlineData("cwls1", "ColorCWLS")]
    [InlineData("cwls2", "ColorCWLS2")]
    public void MapsChannelsToTheGamesConfigOptions(string key, string expected)
    {
        Assert.True(GameChannels.TryByKey(key, out var channel));
        Assert.Equal(expected, GameLogColors.OptionFor(channel));
    }
}
