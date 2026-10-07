using Aetherphone.Core.Video;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PlaybackFailureClassifierTests
{
    [Theory]
    [InlineData("ERROR: [youtube] dQw4w9WgXcQ: Sign in to confirm you're not a bot. Use --cookies-from-browser or --cookies for the authentication. See https://github.com/yt-dlp/yt-dlp/wiki/FAQ")]
    [InlineData("ERROR: [youtube] dQw4w9WgXcQ: Sign in to confirm you’re not a bot. This helps protect our community.")]
    [InlineData("youtube-dl failed: Sign in to confirm you're NOT A BOT")]
    public void YouTubeBotWallReadsAsBotCheck(string logged)
    {
        Assert.Equal(PlaybackFailureKind.BotCheck, PlaybackFailureClassifier.Classify(logged));
    }

    [Theory]
    [InlineData("ERROR: [vimeo] 76979871: The web client only works when logged-in. Use --cookies, --cookies-from-browser, --username and --password, --netrc-cmd, or --netrc (vimeo) to provide account credentials.")]
    [InlineData("ERROR: [youtube] abc: Sign in to confirm your age. This video may be inappropriate for some users.")]
    [InlineData("ERROR: [youtube] abc: Private video. Sign in if you've been granted access to this video")]
    public void SignInWallsReadAsLoginRequired(string logged)
    {
        Assert.Equal(PlaybackFailureKind.LoginRequired, PlaybackFailureClassifier.Classify(logged));
    }

    [Theory]
    [InlineData("ERROR: [Crunchyroll] abc: This video is DRM protected")]
    [InlineData("ERROR: [generic] movie: This video is DRM protected; Try selecting another subtitle or format")]
    public void DrmReadsAsDrmProtected(string logged)
    {
        Assert.Equal(PlaybackFailureKind.DrmProtected, PlaybackFailureClassifier.Classify(logged));
    }

    [Theory]
    [InlineData("ERROR: [youtube] abc: The uploader has not made this video available in your country. You might want to use a VPN or a proxy server (with --proxy) to workaround.")]
    [InlineData("ERROR: [tubitv] 1: This video is not available from your location due to geo restriction")]
    public void GeoBlocksReadAsRegionLocked(string logged)
    {
        Assert.Equal(PlaybackFailureKind.RegionLocked, PlaybackFailureClassifier.Classify(logged));
    }

    [Fact]
    public void UnknownSitesReadAsUnsupported()
    {
        Assert.Equal(PlaybackFailureKind.UnsupportedSite,
            PlaybackFailureClassifier.Classify("ERROR: Unsupported URL: https://www.plex.tv/movie/some-film"));
    }

    [Theory]
    [InlineData("ERROR: [tubitv] 383676: Unable to extract data; please report this issue on  https://github.com/yt-dlp/yt-dlp/issues?q= , filling out the appropriate issue template.")]
    [InlineData("ERROR: [tubitv] 100062117: An extractor error has occurred. (caused by KeyError('100062117'))")]
    [InlineData("WARNING: The program functionality for this site has been marked as broken, and will probably not work.\nERROR: [PlutoTV] x: HTTP Error 404")]
    public void ExtractorBreakageReadsAsSiteChanged(string logged)
    {
        Assert.Equal(PlaybackFailureKind.SiteChanged, PlaybackFailureClassifier.Classify(logged));
    }

    [Theory]
    [InlineData("ERROR: [youtube] xyz: Playback on other websites has been disabled by the video owner")]
    [InlineData("ERROR: [generic] x: Unable to download webpage: HTTP Error 403: Forbidden")]
    [InlineData("loading failed")]
    public void OtherFailuresStayUnclassified(string logged)
    {
        Assert.Equal(PlaybackFailureKind.Unknown, PlaybackFailureClassifier.Classify(logged));
    }

    [Theory]
    [InlineData((byte)PlaybackFailureKind.DrmProtected, true)]
    [InlineData((byte)PlaybackFailureKind.RegionLocked, true)]
    [InlineData((byte)PlaybackFailureKind.LoginRequired, true)]
    [InlineData((byte)PlaybackFailureKind.UnsupportedSite, true)]
    [InlineData((byte)PlaybackFailureKind.SiteChanged, false)]
    [InlineData((byte)PlaybackFailureKind.BotCheck, false)]
    [InlineData((byte)PlaybackFailureKind.Unknown, false)]
    public void OnlyPermanentFailuresSkipAutoReplay(byte kind, bool permanent)
    {
        Assert.Equal(permanent, PlaybackFailureClassifier.RetryCannotHelp((PlaybackFailureKind)kind));
    }

    [Theory]
    [InlineData("https://tubitv.com/movies/383676/tracker", true)]
    [InlineData("https://www.tubitv.com/tv-shows/200340781/s01-e05", true)]
    [InlineData("https://tubi.tv/movies/1", true)]
    [InlineData("https://nottubitv.com/movies/1", false)]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", false)]
    [InlineData("not a url", false)]
    public void TubiLinksGetThePlainUserAgent(string url, bool expectsUserAgent)
    {
        var options = ResolverSiteOptions.RawOptionsFor(url);
        Assert.StartsWith(ResolverSiteOptions.BaseRawOptions, options);
        Assert.Equal(expectsUserAgent, options.Contains("user-agent=Mozilla/5.0", StringComparison.Ordinal));
    }

    [Fact]
    public void FormatSelectorKeepsFormatsWithoutAHeight()
    {
        var selector = MpvRenderer.FormatSelector(720);
        Assert.DoesNotContain("height<=720", selector);
        Assert.Contains("height<=?720", selector);
        Assert.EndsWith("/best", selector);
    }
}
