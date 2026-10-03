using Aetherphone.Core.Songs;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LibraryStoreTests : IDisposable
{
    private readonly DirectoryInfo root =
        new(Path.Combine(Path.GetTempPath(), "aep-library-" + Guid.NewGuid().ToString("N")));

    public void Dispose()
    {
        if (root.Exists)
        {
            root.Delete(true);
        }
    }

    private static Song Track(int number) =>
        new("video" + number.ToString("D6"), "Track " + number, "Artist", string.Empty, 200, "channel");

    [Fact]
    public void PlaylistsRoundTripThroughDisk()
    {
        using (var store = new LibraryStore(root))
        {
            var id = store.CreatePlaylist("  Road trip  ", "Long drives");
            store.AddToPlaylist(id, Track(1));
            store.AddToPlaylist(id, Track(2));
            store.AddToPlaylist(id, Track(1));
        }

        using var reloaded = new LibraryStore(root);
        Assert.Single(reloaded.Playlists);
        var playlist = reloaded.Playlists[0];
        Assert.Equal("Road trip", playlist.Name);
        Assert.Equal("Long drives", playlist.Description);
        Assert.Equal(2, playlist.Songs.Count);
    }

    [Fact]
    public void MoveInPlaylistReorders()
    {
        using var store = new LibraryStore(root);
        var id = store.CreatePlaylist("Order");
        store.AddRangeToPlaylist(id, new[] { Track(1), Track(2), Track(3) });

        store.MoveInPlaylist(id, 2, 0);

        var songs = store.PlaylistSongs(id);
        Assert.Equal(Track(3).VideoId, songs[0].VideoId);
        Assert.Equal(Track(1).VideoId, songs[1].VideoId);
        Assert.Equal(Track(2).VideoId, songs[2].VideoId);
    }

    [Fact]
    public void LovingAddsToLibraryAndRemovingFromLibraryUnloves()
    {
        using var store = new LibraryStore(root);

        store.SetLoved(Track(5), true);

        Assert.True(store.InLibrary(Track(5).VideoId));
        Assert.True(store.IsLoved(Track(5).VideoId));
        Assert.Single(store.LovedSongs());

        store.RemoveFromLibrary(Track(5).VideoId);

        Assert.False(store.IsLoved(Track(5).VideoId));
        Assert.Empty(store.LovedSongs());
    }

    [Fact]
    public void RecordPlayCountsAndMovesToFront()
    {
        using var store = new LibraryStore(root);
        store.RecordPlay(Track(1));
        store.RecordPlay(Track(2));
        store.RecordPlay(Track(1));

        var recent = store.RecentlyPlayed(5);

        Assert.Equal(2, recent.Length);
        Assert.Equal(Track(1).VideoId, recent[0].VideoId);
        Assert.Equal(2, store.PlayCount(Track(1).VideoId));
        Assert.Equal(Track(1).VideoId, store.MostPlayed(1)[0].VideoId);
    }

    [Fact]
    public void RecentSearchesDeduplicateCaseInsensitively()
    {
        using var store = new LibraryStore(root);
        store.RecordSearch("Nobuo Uematsu");
        store.RecordSearch("lofi");
        store.RecordSearch("nobuo uematsu");

        Assert.Equal(2, store.RecentSearches.Count);
        Assert.Equal("nobuo uematsu", store.RecentSearches[0]);
    }

    [Fact]
    public void ArtistsFollowAndUnfollow()
    {
        using var store = new LibraryStore(root);
        store.SetFollowArtist("UC123", "Soken", string.Empty, true);
        store.SetFollowArtist("UC123", "Soken", string.Empty, true);

        Assert.Single(store.Artists);
        store.SetFollowArtist("UC123", "Soken", string.Empty, false);
        Assert.Empty(store.Artists);
    }

    [Fact]
    public void ListeningTimeAndDailyHistorySurviveAReload()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        using (var store = new LibraryStore(root))
        {
            store.RecordPlay(Track(1));
            store.RecordListening(Track(1), 150);
            store.RecordPlay(Track(2));
            store.RecordListening(Track(2), 30);
        }

        using var reloaded = new LibraryStore(root);
        var summary = reloaded.BuildListening(ReplayPeriod.Week, today, 10);

        Assert.Equal(180, summary.TotalSeconds);
        Assert.Equal(2, summary.TotalPlays);
        Assert.Equal(Track(1).VideoId, summary.TopSongs[0].Song.VideoId);
        Assert.Equal(150, summary.TopSongs[0].Seconds);
        Assert.Single(summary.TopArtists);
    }

    [Fact]
    public void AVersionOneLibraryEstimatesItsLifetimeListening()
    {
        root.Create();
        File.WriteAllText(Path.Combine(root.FullName, "library.json"),
            "{\"Version\":1,\"Plays\":[{\"Song\":{\"VideoId\":\"abc\",\"Title\":\"Song\",\"Author\":\"Band\"," +
            "\"DurationSeconds\":200},\"Count\":3,\"FirstPlayedUnix\":1,\"LastPlayedUnix\":2}]}");

        using var store = new LibraryStore(root);
        var allTime = store.BuildListening(ReplayPeriod.AllTime, DateOnly.FromDateTime(DateTime.Now), 10);
        var week = store.BuildListening(ReplayPeriod.Week, DateOnly.FromDateTime(DateTime.Now), 10);

        Assert.Equal(600, allTime.TotalSeconds);
        Assert.Equal(3, allTime.TopSongs[0].Plays);
        Assert.Equal(0, week.TotalSeconds);
        Assert.Empty(week.TopSongs);
    }
}
