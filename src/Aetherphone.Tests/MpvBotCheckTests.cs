using Aetherphone.Core.Video;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MpvBotCheckTests
{
    [Theory]
    [InlineData("ERROR: [youtube] dQw4w9WgXcQ: Sign in to confirm you're not a bot. Use --cookies-from-browser or --cookies for the authentication. See https://github.com/yt-dlp/yt-dlp/wiki/FAQ")]
    [InlineData("ERROR: [youtube] dQw4w9WgXcQ: Sign in to confirm you’re not a bot. This helps protect our community.")]
    [InlineData("youtube-dl failed: Sign in to confirm you're NOT A BOT")]
    public void YouTubeBotWallReadsAsBotCheck(string logged)
    {
        Assert.True(MpvRenderer.IsBotCheckText(logged));
    }

    [Theory]
    [InlineData("ERROR: [youtube] abc: Sign in to confirm your age")]
    [InlineData("ERROR: [youtube] xyz: Playback on other websites has been disabled by the video owner")]
    [InlineData("ERROR: [generic] x: Unable to download webpage: HTTP Error 403: Forbidden")]
    [InlineData("loading failed")]
    public void OtherFailuresStayUnclassified(string logged)
    {
        Assert.False(MpvRenderer.IsBotCheckText(logged));
    }
}
