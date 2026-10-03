using Aetherphone.Core.Lyrics;
using Aetherphone.Core.Net;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LyricsMatcherTests
{
    private const string Synced = "[00:01.00]Line";
    private const string Plain = "Line";

    private static LrcLibTrack Track(string artist, string track, double duration, string synced = Synced,
        string plain = Plain, bool instrumental = false, long id = 1)
    {
        return new LrcLibTrack(id, track, artist, "Album", duration, instrumental, plain, synced);
    }

    private static LyricsQuery Query(string artist, string track, int duration)
    {
        return new LyricsQuery(new[] { new LyricsCandidate(artist, track), new LyricsCandidate(string.Empty, track) },
            duration);
    }

    [Fact]
    public void PrefersSyncedOverPlainForTheSameSong()
    {
        var tracks = new[]
        {
            Track("Rick Astley", "Never Gonna Give You Up", 213, synced: string.Empty, id: 1),
            Track("Rick Astley", "Never Gonna Give You Up", 213, id: 2),
        };

        var best = LyricsMatcher.PickBest(tracks, Query("Rick Astley", "Never Gonna Give You Up", 213));

        Assert.Equal(1, best);
    }

    [Fact]
    public void RejectsDurationsBeyondTheMaximumTolerance()
    {
        var tracks = new[] { Track("Rick Astley", "Never Gonna Give You Up", 222) };

        Assert.Equal(-1, LyricsMatcher.PickBest(tracks, Query("Rick Astley", "Never Gonna Give You Up", 213)));
    }

    [Fact]
    public void AcceptsTheExtendedToleranceWhenNothingCloserExists()
    {
        var tracks = new[] { Track("Rick Astley", "Never Gonna Give You Up", 220) };

        Assert.Equal(0, LyricsMatcher.PickBest(tracks, Query("Rick Astley", "Never Gonna Give You Up", 213)));
    }

    [Fact]
    public void PrefersTheCloserDuration()
    {
        var tracks = new[]
        {
            Track("Rick Astley", "Never Gonna Give You Up", 219, id: 1),
            Track("Rick Astley", "Never Gonna Give You Up", 214, id: 2),
        };

        Assert.Equal(1, LyricsMatcher.PickBest(tracks, Query("Rick Astley", "Never Gonna Give You Up", 213)));
    }

    [Fact]
    public void RejectsADifferentArtist()
    {
        var tracks = new[] { Track("Taylor Swift", "Love Story", 300) };
        var query = new LyricsQuery(new[] { new LyricsCandidate("Indila", "Love Story") }, 300);

        Assert.Equal(-1, LyricsMatcher.PickBest(tracks, query));
    }

    [Fact]
    public void RejectsADifferentTrack()
    {
        var tracks = new[] { Track("Rick Astley", "Together Forever", 213) };

        Assert.Equal(-1, LyricsMatcher.PickBest(tracks, Query("Rick Astley", "Never Gonna Give You Up", 213)));
    }

    [Fact]
    public void IgnoresCaseDiacriticsAndPunctuation()
    {
        var tracks = new[] { Track("Beyoncé", "Halo!", 261) };

        Assert.Equal(0, LyricsMatcher.PickBest(tracks, Query("BEYONCE", "halo", 261)));
    }

    [Fact]
    public void MatchesOneArtistOfACollaboration()
    {
        var tracks = new[] { Track("Masayoshi Soken, Susan Calloway", "Answers", 286) };

        Assert.Equal(0, LyricsMatcher.PickBest(tracks, Query("Susan Calloway", "Answers", 287)));
    }

    [Fact]
    public void TrackOnlyCandidateNeedsAKnownDuration()
    {
        var tracks = new[] { Track("Susan Calloway", "Answers", 286) };
        var trackOnly = new LyricsQuery(new[] { new LyricsCandidate(string.Empty, "Answers") }, 0);
        var timed = new LyricsQuery(new[] { new LyricsCandidate(string.Empty, "Answers") }, 288);

        Assert.Equal(-1, LyricsMatcher.PickBest(tracks, trackOnly));
        Assert.Equal(0, LyricsMatcher.PickBest(tracks, timed));
    }

    [Fact]
    public void ArtistCandidateWorksWithoutAKnownDuration()
    {
        var tracks = new[] { Track("Daft Punk", "Get Lucky", 248) };

        Assert.Equal(0, LyricsMatcher.PickBest(tracks, Query("Daft Punk", "Get Lucky", 0)));
    }

    [Fact]
    public void InstrumentalTracksAreValidMatches()
    {
        var tracks = new[] { Track("Daft Punk", "Aerodynamic", 212, string.Empty, string.Empty, true) };

        Assert.Equal(0, LyricsMatcher.PickBest(tracks, Query("Daft Punk", "Aerodynamic", 212)));
    }

    [Fact]
    public void SkipsTracksWithoutAnyLyrics()
    {
        var tracks = new[] { Track("Daft Punk", "Get Lucky", 248, string.Empty, string.Empty) };

        Assert.Equal(-1, LyricsMatcher.PickBest(tracks, Query("Daft Punk", "Get Lucky", 248)));
    }

    [Fact]
    public void ToleratesAnExtraSuffixOnTheResultTitle()
    {
        var tracks = new[] { Track("Susan Calloway", "Answers (Main Theme of Final Fantasy XIV)", 286) };

        Assert.Equal(0, LyricsMatcher.PickBest(tracks, Query("Susan Calloway", "Answers", 286)));
    }

    [Fact]
    public void EmptyInputsPickNothing()
    {
        Assert.Equal(-1, LyricsMatcher.PickBest(ReadOnlySpan<LrcLibTrack>.Empty, Query("A", "B", 100)));
        Assert.Equal(-1, LyricsMatcher.PickBest(new[] { Track("A", "B", 100) },
            new LyricsQuery(Array.Empty<LyricsCandidate>(), 100)));
    }

    [Theory]
    [InlineData(200, 200, 1)]
    [InlineData(200, 203, 1)]
    [InlineData(200, 208, 0.4)]
    [InlineData(200, 209, -1)]
    [InlineData(0, 200, 0.5)]
    public void DurationScoreFollowsTheTolerances(double expected, double actual, double score)
    {
        Assert.Equal(score, LyricsMatcher.DurationScore(expected, actual), 0.0001);
    }

    [Theory]
    [InlineData("Beyoncé", "beyonce")]
    [InlineData("AC/DC", "ac dc")]
    [InlineData("Don't Stop Me Now", "dont stop me now")]
    [InlineData("Simon & Garfunkel", "simon and garfunkel")]
    [InlineData("ＡＢＣ　ｄｅｆ", "abc def")]
    [InlineData("  Hello,   World!!  ", "hello world")]
    public void NormalizeFoldsCaseDiacriticsAndPunctuation(string input, string expected)
    {
        Assert.Equal(expected, LyricsText.Normalize(input));
    }

    [Fact]
    public void SimilarityRanksCloseStringsHigher()
    {
        Assert.Equal(1, LyricsText.Similarity("halo", "halo"));
        Assert.True(LyricsText.Similarity("answers", "answers main theme") >= 0.85);
        Assert.True(LyricsText.Similarity("never gonna give you up", "never gonna give u up") > 0.7);
        Assert.True(LyricsText.Similarity("halo", "hello") < 0.6);
        Assert.Equal(0, LyricsText.Similarity(string.Empty, "x"));
        Assert.True(LyricsText.Similarity("夜に駆ける", "夜に駆ける tv size") >= 0.85);
    }

    [Theory]
    [InlineData(404, (int)AepFailureKind.Server, (int)LrcLibOutcome.NotFound)]
    [InlineData(400, (int)AepFailureKind.Server, (int)LrcLibOutcome.NotFound)]
    [InlineData(429, (int)AepFailureKind.Server, (int)LrcLibOutcome.Throttled)]
    [InlineData(0, (int)AepFailureKind.RateLimitPaused, (int)LrcLibOutcome.Throttled)]
    [InlineData(503, (int)AepFailureKind.Server, (int)LrcLibOutcome.Failed)]
    [InlineData(0, (int)AepFailureKind.Offline, (int)LrcLibOutcome.Failed)]
    [InlineData(0, (int)AepFailureKind.Timeout, (int)LrcLibOutcome.Failed)]
    [InlineData(0, (int)AepFailureKind.BadResponse, (int)LrcLibOutcome.NotFound)]
    public void ClientClassifiesFailures(int status, int kind, int expected)
    {
        var failure = new AepFailure((AepFailureKind)kind, status, null, null, null, null);

        Assert.Equal((LrcLibOutcome)expected, LrcLibClient.Classify(failure).Outcome);
    }

    [Fact]
    public void ClientBuildsEscapedUrls()
    {
        Assert.Equal("https://lrclib.net/api/get?track_name=Get%20Lucky&artist_name=Daft%20Punk&duration=248",
            LrcLibClient.GetUrl("Daft Punk", "Get Lucky", 248));
        Assert.Equal("https://lrclib.net/api/get?track_name=Halo&artist_name=Beyonc%C3%A9",
            LrcLibClient.GetUrl("Beyoncé", "Halo", 0));
        Assert.Equal("https://lrclib.net/api/search?q=AC%2FDC%20%26%20friends",
            LrcLibClient.SearchUrl("AC/DC & friends"));
    }
}
