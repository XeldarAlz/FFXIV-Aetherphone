using Aetherphone.Apps.Music;
using Aetherphone.Apps.Music.NowPlaying;
using Aetherphone.Core.Lyrics;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LyricsScrollTests
{
    private const float Tolerance = 0.001f;

    [Fact]
    public void KeepsActiveLineCenterAtAnchorHeight()
    {
        var target = LyricsScroll.Target(600f, 40f, 400f, 2000f);

        Assert.Equal(600f + 20f - 400f * LyricsScroll.Anchor, target, Tolerance);
    }

    [Fact]
    public void NeverScrollsAboveTheTop()
    {
        Assert.Equal(0f, LyricsScroll.Target(20f, 40f, 400f, 2000f), Tolerance);
    }

    [Fact]
    public void StopsWhenTheEndReachesTheAnchor()
    {
        var target = LyricsScroll.Target(1980f, 40f, 400f, 2000f);

        Assert.Equal(LyricsScroll.MaxOffset(400f, 2000f), target, Tolerance);
        Assert.Equal(2000f - 400f * LyricsScroll.Anchor, target, Tolerance);
    }

    [Fact]
    public void FollowResumesAfterThePause()
    {
        Assert.False(LyricsScroll.Following(10f, 12f));
        Assert.True(LyricsScroll.Following(12f, 12f));
    }

    [Fact]
    public void FindsSungWordAndProgress()
    {
        LrcWord[] words = [new(10.0, 0, 3), new(11.0, 4, 5), new(13.0, 10, 2)];

        var index = LyricsScroll.WordAt(words, 12.0, 15.0, out var fraction);

        Assert.Equal(1, index);
        Assert.Equal(0.5f, fraction, Tolerance);
    }

    [Fact]
    public void LastWordRunsUntilLineEnd()
    {
        LrcWord[] words = [new(10.0, 0, 3), new(11.0, 4, 5)];

        var index = LyricsScroll.WordAt(words, 12.5, 13.0, out var fraction);

        Assert.Equal(1, index);
        Assert.Equal(0.75f, fraction, Tolerance);
    }

    [Fact]
    public void NoWordBeforeTheFirstStart()
    {
        LrcWord[] words = [new(10.0, 0, 3)];

        Assert.Equal(-1, LyricsScroll.WordAt(words, 9.0, 12.0, out _));
    }
}
