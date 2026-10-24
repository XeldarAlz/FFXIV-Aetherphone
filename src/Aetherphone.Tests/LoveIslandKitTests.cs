using Aetherphone.Apps.LoveIsland;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LoveIslandKitTests
{
    [Theory]
    [InlineData("ashtural", "ashtural")]
    [InlineData("https://www.twitch.tv/Ashtural/", "Ashtural")]
    [InlineData("twitch.tv/pnxlive?ref=phone", "pnxlive")]
    [InlineData("@pnxlive", "pnxlive")]
    [InlineData("", "")]
    public void TwitchChannelNamesDropTheUrlAroundThem(string input, string expected)
    {
        Assert.Equal(expected, LoveIslandKit.TwitchChannelName(input));
    }
}
