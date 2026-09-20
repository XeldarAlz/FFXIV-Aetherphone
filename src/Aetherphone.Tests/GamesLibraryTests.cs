using Aetherphone.Apps.Games;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Games;
using Xunit;

namespace Aetherphone.Tests;

public sealed class GamesLibraryTests
{
    private sealed class FakeGame : IMiniGame
    {
        public FakeGame(string id, string title, GameGenre genre)
        {
            Id = id;
            Title = title;
            Genre = genre;
        }

        public string Id { get; }
        public string Title { get; }
        public GameGenre Genre { get; }

        public void Open()
        {
        }

        public void Close()
        {
        }

        public void Draw(in GameContext context)
        {
        }

        public void Dispose()
        {
        }
    }

    private static readonly IMiniGame[] Games =
    {
        new FakeGame("minesweeper", "Sweeper", GameGenre.Brain),
        new FakeGame("snake", "Snake", GameGenre.Arcade),
        new FakeGame("tetris", "Tetris", GameGenre.Puzzle),
        new FakeGame("chess", "Chess", GameGenre.Tabletop),
        new FakeGame("doom", "Doom", GameGenre.Action),
        new FakeGame("wordrun", "Word Run", GameGenre.Brain),
        new FakeGame("breakout", "Breakout", GameGenre.Arcade),
    };

    private static GamesLibrary Build(Configuration configuration) =>
        new(Games, new GameStatsStore(configuration));

    [Fact]
    public void OrderedListsNewestReleasesFirst()
    {
        var library = Build(new Configuration());

        var ordered = library.Ordered.ToArray();
        var ids = new string[ordered.Length];
        for (var index = 0; index < ordered.Length; index++)
        {
            ids[index] = library.Entries[ordered[index]].Id;
        }

        Assert.Equal(new[]
        {
            "online.connectfour", "online.uno", "online.chess", "online.pool", "doom", "wordrun", "chess", "tetris",
            "minesweeper", "snake", "breakout",
        }, ids);
    }

    [Fact]
    public void LatestHoldsOnlyTheNewestWave()
    {
        var library = Build(new Configuration());

        var latest = library.Latest.ToArray();

        Assert.Single(latest);
        for (var index = 0; index < latest.Length; index++)
        {
            Assert.NotEqual("chess", library.Entries[latest[index]].Id);
        }
    }

    [Fact]
    public void RecentOrdersByLastPlayedAndSkipsUnplayedGames()
    {
        var configuration = new Configuration();
        configuration.GameStats.Add(new GameStatRecord { GameId = "snake", LastPlayedUnixSeconds = 100 });
        configuration.GameStats.Add(new GameStatRecord { GameId = "chess", LastPlayedUnixSeconds = 300 });
        configuration.GameStats.Add(new GameStatRecord
        {
            GameId = GamesLibrary.OnlineEntryId(GameRoomWire.PoolKind), LastPlayedUnixSeconds = 200,
        });
        var library = Build(configuration);

        var recent = library.Recent.ToArray();

        Assert.Equal(3, recent.Length);
        Assert.Equal("chess", library.Entries[recent[0]].Id);
        Assert.Equal("online.pool", library.Entries[recent[1]].Id);
        Assert.Equal("snake", library.Entries[recent[2]].Id);
    }

    [Fact]
    public void GenreFilterKeepsOnlyThatShelf()
    {
        var library = Build(new Configuration());

        var arcade = library.Filter(LibraryFilter.Arcade, string.Empty).ToArray();
        var friends = library.Filter(LibraryFilter.Friends, string.Empty).ToArray();

        Assert.Equal(2, arcade.Length);
        Assert.Equal(GameGenre.Arcade, library.Entries[arcade[0]].Genre);
        Assert.Equal(GameGenre.Arcade, library.Entries[arcade[1]].Genre);
        Assert.Equal(4, friends.Length);
        Assert.True(library.Entries[friends[0]].Online);
    }

    [Fact]
    public void SearchMatchesTitlesCaseInsensitivelyAcrossEveryShelf()
    {
        var library = Build(new Configuration());

        var hits = library.Filter(LibraryFilter.Arcade, "  WORD ").ToArray();
        var none = library.Filter(LibraryFilter.All, "zzz").ToArray();

        Assert.Single(hits);
        Assert.Equal("wordrun", library.Entries[hits[0]].Id);
        Assert.Empty(none);
    }

    [Fact]
    public void BestLabelReadsTheEasyBoardForTimedPuzzles()
    {
        var configuration = new Configuration();
        configuration.GameStats.Add(new GameStatRecord { GameId = "minesweeper.easy", BestTimeSeconds = 65 });
        var library = Build(configuration);

        Assert.EndsWith("1:05", library.Best(0));
        Assert.Equal(string.Empty, library.Best(1));
    }
}
