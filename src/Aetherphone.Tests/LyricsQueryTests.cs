using Aetherphone.Core.Lyrics;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LyricsQueryTests
{
    private static readonly string EmDash = ((char)0x2014).ToString();

    [Theory]
    [InlineData("Rick Astley - Never Gonna Give You Up (Official Music Video)", "Rick Astley", "Rick Astley",
        "Never Gonna Give You Up")]
    [InlineData("Never Gonna Give You Up", "Rick Astley - Topic", "Rick Astley", "Never Gonna Give You Up")]
    [InlineData("Daft Punk - Get Lucky [Official Audio] [HD]", "Daft Punk", "Daft Punk", "Get Lucky")]
    [InlineData("Queen – Bohemian Rhapsody (Official Video Remastered)", "Queen Official", "Queen",
        "Bohemian Rhapsody")]
    [InlineData("Bohemian Rhapsody - Queen", "Queen Official", "Queen", "Bohemian Rhapsody")]
    [InlineData("Blinding Lights | The Weeknd", "Lyric Corner", "The Weeknd", "Blinding Lights")]
    [InlineData("Blinding Lights｜The Weeknd", "Lyric Corner", "The Weeknd", "Blinding Lights")]
    [InlineData("Calvin Harris - Summer (feat. Someone) (Lyrics)", "7clouds", "Calvin Harris", "Summer")]
    [InlineData("Song Name ft. Someone", "ArtistVEVO", "Artist", "Song Name")]
    [InlineData("Shape of You", "EdSheeranVEVO", "EdSheeran", "Shape of You")]
    [InlineData("Artist ft. Other - Song", "Uploader", "Artist", "Song")]
    [InlineData("Linkin Park - Numb - Lyrics", "Uploader", "Linkin Park", "Numb")]
    [InlineData("Taylor Swift - Love Story (Taylor's Version)", "Taylor Swift", "Taylor Swift",
        "Love Story (Taylor's Version)")]
    [InlineData("Imagine Dragons - Believer (Official Music Video) | 4K", "ImagineDragonsVEVO", "Imagine Dragons",
        "Believer")]
    [InlineData("YOASOBI「夜に駆ける」Official Music Video", "Ayase / YOASOBI", "YOASOBI", "夜に駆ける")]
    [InlineData("【MV】YOASOBI「アイドル」", "Ayase / YOASOBI", "YOASOBI", "アイドル")]
    [InlineData("米津玄師 MV「Lemon」", "米津玄師", "米津玄師", "Lemon")]
    [InlineData("LiSA『紅蓮華』MV", "LiSA Official YouTube", "LiSA", "紅蓮華")]
    [InlineData("ヨルシカ - ただ君に晴れ (MUSIC VIDEO)", "ヨルシカ / n-buna Official", "ヨルシカ", "ただ君に晴れ")]
    [InlineData("Adele \"Hello\" Official Video", "Uploader", "Adele", "Hello")]
    [InlineData("Avicii - Wake Me Up (Official Video) [HD] [Lyrics]", "AviciiOfficialVEVO", "Avicii", "Wake Me Up")]
    public void FirstCandidateIsTheBestGuess(string title, string channel, string artist, string track)
    {
        var query = LyricsQuery.FromYoutube(title, channel, 200);

        Assert.False(query.IsEmpty);
        Assert.Equal(artist, query.Candidates[0].Artist);
        Assert.Equal(track, query.Candidates[0].Track);
        Assert.Equal(200, query.DurationSeconds);
    }

    [Fact]
    public void SplitsOnEmDash()
    {
        var query = LyricsQuery.FromYoutube(string.Concat("Muse ", EmDash, " Uprising"), "Uploader", 0);

        Assert.Equal("Muse", query.Candidates[0].Artist);
        Assert.Equal("Uprising", query.Candidates[0].Track);
    }

    [Theory]
    [InlineData("FFXIV OST - Answers ( Main Theme )", "Hidden Ex", "Answers")]
    [InlineData("FINAL FANTASY XIV - Answers", "FINAL FANTASY XIV", "Answers")]
    [InlineData("FFXIV OST - Shadowbringers ( Main Theme )", "Uploader", "Shadowbringers")]
    [InlineData("FFXIV OST Endwalker Theme - Footfalls [Lyrics]", "Uploader", "Footfalls")]
    [InlineData("Final Fantasy XIV Soundtrack - Dragonsong", "Uploader", "Dragonsong")]
    [InlineData("Endwalker - FFXIV OST", "Uploader", "Endwalker")]
    public void SoundtrackLabelsAreNotUsedAsArtists(string title, string channel, string track)
    {
        var query = LyricsQuery.FromYoutube(title, channel, 300);

        Assert.Equal(string.Empty, query.Candidates[0].Artist);
        Assert.Equal(track, query.Candidates[0].Track);
        for (var index = 0; index < query.Candidates.Length; index++)
        {
            Assert.DoesNotContain("OST", query.Candidates[index].Artist, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Fantasy", query.Candidates[index].Artist, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SquareEnixChannelIsNotAnArtist()
    {
        var query = LyricsQuery.FromYoutube("Footfalls", "SQUARE ENIX MUSIC", 240);

        Assert.Single(query.Candidates);
        Assert.Equal(string.Empty, query.Candidates[0].Artist);
        Assert.Equal("Footfalls", query.Candidates[0].Track);
    }

    [Fact]
    public void UnknownOrderAlsoTriesTheReversedPair()
    {
        var query = LyricsQuery.FromYoutube("Daft Punk - Get Lucky", "Some Uploader", 248);

        Assert.True(Contains(query, "Daft Punk", "Get Lucky"));
        Assert.True(Contains(query, "Get Lucky", "Daft Punk"));
        Assert.True(Contains(query, "Some Uploader", "Get Lucky"));
        Assert.True(Contains(query, string.Empty, "Get Lucky"));
    }

    [Fact]
    public void ChannelMatchSkipsTheReversedPair()
    {
        var query = LyricsQuery.FromYoutube("Daft Punk - Get Lucky", "Daft Punk", 248);

        Assert.False(Contains(query, "Get Lucky", "Daft Punk"));
    }

    [Fact]
    public void AlwaysEndsWithATrackOnlyFallback()
    {
        var query = LyricsQuery.FromYoutube("Rick Astley - Never Gonna Give You Up", "Rick Astley", 213);

        var last = query.Candidates[^1];
        Assert.Equal(string.Empty, last.Artist);
        Assert.Equal("Never Gonna Give You Up", last.Track);
    }

    [Fact]
    public void CandidatesAreUnique()
    {
        var query = LyricsQuery.FromYoutube("Never Gonna Give You Up", "Rick Astley - Topic", 213);

        Assert.Equal(2, query.Candidates.Length);
        Assert.Equal("Rick Astley", query.Candidates[0].Artist);
        Assert.Equal(string.Empty, query.Candidates[1].Artist);
    }

    [Fact]
    public void TitleWithoutSeparatorOrChannelIsTrackOnly()
    {
        var query = LyricsQuery.FromYoutube("lofi beats to study to", string.Empty, 0);

        Assert.Single(query.Candidates);
        Assert.Equal(string.Empty, query.Candidates[0].Artist);
        Assert.Equal("lofi beats to study to", query.Candidates[0].Track);
    }

    [Fact]
    public void MusicAloneIsAValidTrackName()
    {
        var query = LyricsQuery.FromYoutube("Madonna - Music", "Madonna", 225);

        Assert.Equal("Madonna", query.Candidates[0].Artist);
        Assert.Equal("Music", query.Candidates[0].Track);
    }

    [Fact]
    public void EmptyTitleYieldsNoCandidates()
    {
        Assert.True(LyricsQuery.FromYoutube(string.Empty, "Someone", 100).IsEmpty);
        Assert.True(LyricsQuery.FromYoutube("[Official Video]", string.Empty, 100).IsEmpty);
    }

    private static bool Contains(LyricsQuery query, string artist, string track)
    {
        for (var index = 0; index < query.Candidates.Length; index++)
        {
            var candidate = query.Candidates[index];
            if (candidate.Artist == artist && candidate.Track == track)
            {
                return true;
            }
        }

        return false;
    }
}
