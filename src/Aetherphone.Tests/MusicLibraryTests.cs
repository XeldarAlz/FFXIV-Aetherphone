using Aetherphone.Apps.Music;
using Aetherphone.Apps.Music.Library;
using Aetherphone.Core.Songs;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LibraryArtTests
{
    private static SongRecord Record(string id, string thumbnail) =>
        new() { VideoId = id, Title = id, ThumbnailUrl = thumbnail };

    [Fact]
    public void FourDistinctThumbnailsMakeAMosaic()
    {
        var songs = new List<SongRecord>
        {
            Record("a", "https://x/a.jpg"), Record("b", "https://x/a.jpg"), Record("c", "https://x/c.jpg"),
            Record("d", string.Empty), Record("e", "https://x/e.jpg"), Record("f", "https://x/f.jpg"),
            Record("g", "https://x/g.jpg"),
        };
        Span<string> picked = [string.Empty, string.Empty, string.Empty, string.Empty];

        var count = LibraryArt.SelectMosaic(songs, picked);

        Assert.Equal(LibraryArt.MosaicTiles, count);
        Assert.Equal("https://x/a.jpg", picked[0]);
        Assert.Equal("https://x/c.jpg", picked[1]);
        Assert.Equal("https://x/e.jpg", picked[2]);
        Assert.Equal("https://x/f.jpg", picked[3]);
    }

    [Fact]
    public void FewerThanFourDistinctFallsBackToTheFirstCover()
    {
        var songs = new List<SongRecord>
        {
            Record("a", "https://x/a.jpg"), Record("b", "https://x/b.jpg"), Record("c", "https://x/a.jpg"),
        };
        Span<string> picked = [string.Empty, string.Empty, string.Empty, string.Empty];

        Assert.Equal(1, LibraryArt.SelectMosaic(songs, picked));
        Assert.Equal("https://x/a.jpg", picked[0]);
    }

    [Fact]
    public void NoThumbnailsMeansNoArt()
    {
        var songs = new List<SongRecord> { Record("a", string.Empty) };
        Span<string> picked = [string.Empty, string.Empty, string.Empty, string.Empty];

        Assert.Equal(0, LibraryArt.SelectMosaic(songs, picked));
        Assert.Equal(0, LibraryArt.SelectMosaic(new List<SongRecord>(), picked));
    }

    [Fact]
    public void CoverVariantsSitNextToTheMaster()
    {
        var set = PlaylistCoverSet.From("C:/covers/abc-1.jpg");

        Assert.True(set.HasCover);
        Assert.Equal("C:/covers/abc-1.64.jpg", set.For(40f));
        Assert.Equal("C:/covers/abc-1.128.jpg", set.For(100f));
        Assert.Equal("C:/covers/abc-1.jpg", set.For(240f));
        Assert.False(PlaylistCoverSet.From(string.Empty).HasCover);
    }
}

public sealed class LibrarySortingTests
{
    private static PlaylistRecord Playlist(string name, long created, long updated) =>
        new() { Id = name, Name = name, CreatedUnix = created, UpdatedUnix = updated };

    private static Song Track(string title, string author) => new(title + author, title, author, string.Empty, 100);

    [Fact]
    public void PlaylistsSortByEachOrder()
    {
        var source = new List<PlaylistRecord>
        {
            Playlist("beta", 10, 300), Playlist("Alpha", 30, 100), Playlist("gamma", 20, 200),
        };

        var updated = LibrarySorting.Playlists(source, PlaylistSort.RecentlyUpdated);
        var title = LibrarySorting.Playlists(source, PlaylistSort.Title);
        var added = LibrarySorting.Playlists(source, PlaylistSort.RecentlyAdded);

        Assert.Equal(new[] { "beta", "gamma", "Alpha" }, Names(updated));
        Assert.Equal(new[] { "Alpha", "beta", "gamma" }, Names(title));
        Assert.Equal(new[] { "Alpha", "gamma", "beta" }, Names(added));
        Assert.Equal("beta", source[0].Name);
    }

    [Fact]
    public void SongsSortStablyByTitleAndArtist()
    {
        var source = new[] { Track("b", "Zed"), Track("a", "Moe"), Track("c", "Moe"), Track("a", "Abe") };

        var byTitle = LibrarySorting.Songs(source, SongSort.Title);
        var byArtist = LibrarySorting.Songs(source, SongSort.Artist);
        var recent = LibrarySorting.Songs(source, SongSort.RecentlyAdded);

        Assert.Equal(new[] { "aMoe", "aAbe", "bZed", "cMoe" }, Ids(byTitle));
        Assert.Equal(new[] { "aAbe", "aMoe", "cMoe", "bZed" }, Ids(byArtist));
        Assert.Equal(Ids(source), Ids(recent));
        Assert.NotSame(source, recent);
    }

    private static string[] Names(PlaylistRecord[] playlists)
    {
        var names = new string[playlists.Length];
        for (var index = 0; index < playlists.Length; index++)
        {
            names[index] = playlists[index].Name;
        }

        return names;
    }

    private static string[] Ids(Song[] songs)
    {
        var ids = new string[songs.Length];
        for (var index = 0; index < songs.Length; index++)
        {
            ids[index] = songs[index].VideoId;
        }

        return ids;
    }
}

public sealed class LibraryArtistsTests
{
    [Fact]
    public void ArtistsGroupByChannelThenAuthor()
    {
        var followed = new List<ArtistRecord> { new() { ChannelId = "UC2", Name = "Zoe", ThumbnailUrl = "z" } };
        var songs = new List<SongRecord>
        {
            new() { VideoId = "1", Author = "Ann", ChannelId = "UC1", ThumbnailUrl = "a1" },
            new() { VideoId = "2", Author = "Ann (Topic)", ChannelId = "UC1", ThumbnailUrl = "a2" },
            new() { VideoId = "3", Author = "Bob", ChannelId = string.Empty },
            new() { VideoId = "4", Author = "bob", ChannelId = string.Empty },
            new() { VideoId = "5", Author = "Zoe", ChannelId = "UC2" },
        };

        var artists = LibraryArtists.Build(followed, songs);

        Assert.Equal(3, artists.Length);
        Assert.Equal("Ann", artists[0].Name);
        Assert.Equal(2, artists[0].SongCount);
        Assert.Equal("a1", artists[0].ThumbnailUrl);
        Assert.Equal("Bob", artists[1].Name);
        Assert.Equal(2, artists[1].SongCount);
        Assert.True(artists[2].Followed);
        Assert.Equal(1, artists[2].SongCount);
        Assert.Equal("z", artists[2].ThumbnailUrl);
    }
}
