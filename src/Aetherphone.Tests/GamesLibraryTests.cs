using Aetherphone.Apps.Games;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class GamesLibraryTests
{
    private sealed class FakeGame : IMiniGame
    {
        public FakeGame(string id, string title, GameGenre genre)
        {
            Spec = new GameSpec(id, new LocString(string.Concat("test.", id), title), genre);
            Title = title;
        }

        public GameSpec Spec { get; }
        public string Title { get; }

        public void Start(in GameStart start)
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
    public void GenreShelfKeepsOnlyThatGenreNewestFirst()
    {
        var library = Build(new Configuration());

        var arcade = library.Genre(GameGenre.Arcade).ToArray();
        var friends = library.Genre(GameGenre.Friends).ToArray();

        Assert.Equal(2, arcade.Length);
        Assert.Equal("snake", library.Entries[arcade[0]].Id);
        Assert.Equal("breakout", library.Entries[arcade[1]].Id);
        Assert.Equal(4, friends.Length);
        Assert.True(library.Entries[friends[0]].Online);
    }

    [Fact]
    public void SearchMatchesTitlesCaseInsensitively()
    {
        var library = Build(new Configuration());

        var hits = library.Search("  WORD ").ToArray();
        var none = library.Search("zzz").ToArray();
        var blank = library.Search("   ").ToArray();

        Assert.Single(hits);
        Assert.Equal("wordrun", library.Entries[hits[0]].Id);
        Assert.Empty(none);
        Assert.Empty(blank);
    }

    [Fact]
    public void SearchMatchesTheGenreName()
    {
        var library = Build(new Configuration());

        var hits = library.Search("board").ToArray();

        Assert.Single(hits);
        Assert.Equal("chess", library.Entries[hits[0]].Id);
    }

    [Fact]
    public void RecordsListOnlyGamesWithABestMostRecentFirst()
    {
        var configuration = new Configuration();
        configuration.GameStats.Add(new GameStatRecord { GameId = "snake", BestScore = 40, LastPlayedUnixSeconds = 100 });
        configuration.GameStats.Add(new GameStatRecord { GameId = "chess", Streak = 2, LastPlayedUnixSeconds = 300 });
        configuration.GameStats.Add(new GameStatRecord { GameId = "doom", LastPlayedUnixSeconds = 500 });
        var library = Build(configuration);

        var records = library.Records.ToArray();

        Assert.Equal(2, records.Length);
        Assert.Equal("chess", library.Entries[records[0]].Id);
        Assert.Equal(RecordKind.Streak, library.BestKind(records[0]));
        Assert.Equal("2", library.BestValue(records[0]));
        Assert.Equal("snake", library.Entries[records[1]].Id);
        Assert.Equal(RecordKind.Score, library.BestKind(records[1]));
        Assert.Equal(3, library.PlayedCount);
    }

    [Fact]
    public void RebuildPicksUpANewBest()
    {
        var configuration = new Configuration();
        var library = Build(configuration);
        var snake = library.IndexOf("snake");

        Assert.Equal(RecordKind.None, library.BestKind(snake));
        configuration.GameStats.Add(new GameStatRecord { GameId = "snake", BestScore = 12 });
        library.Rebuild();

        Assert.Equal(RecordKind.Score, library.BestKind(snake));
        Assert.Equal("12", library.BestValue(snake));
        Assert.Single(library.Records.ToArray());
    }

    [Fact]
    public void IndexOfFindsOnlineEntriesAndRejectsUnknownIds()
    {
        var library = Build(new Configuration());

        Assert.True(library.Entries[library.IndexOf("online.pool")].Online);
        Assert.Equal(-1, library.IndexOf("nope"));
    }

    [Fact]
    public void BestLabelShowsTheFastestTierForTimedPuzzles()
    {
        var configuration = new Configuration();
        configuration.GameStats.Add(new GameStatRecord { GameId = "minesweeper.easy", BestTimeSeconds = 65 });
        configuration.GameStats.Add(new GameStatRecord { GameId = "minesweeper.hard", BestTimeSeconds = 40 });
        var library = Build(configuration);

        Assert.Equal("0:40", library.BestValue(0));
        Assert.Equal("Hard", library.BestTier(0));
        Assert.EndsWith("0:40 · Hard", library.Best(0));
        Assert.Equal(string.Empty, library.Best(1));
    }

    [Fact]
    public void BestLabelReadsASingleTierWhenOnlyOneHasATime()
    {
        var configuration = new Configuration();
        configuration.GameStats.Add(new GameStatRecord { GameId = "minesweeper.medium", BestTimeSeconds = 65 });
        var library = Build(configuration);

        Assert.Equal(RecordKind.Time, library.BestKind(0));
        Assert.Equal("1:05", library.BestValue(0));
        Assert.Equal("Medium", library.BestTier(0));
    }

    [Fact]
    public void FlowReadsItsTieredLevelRecords()
    {
        var games = new IMiniGame[] { new FakeGame("flow", "Flow", GameGenre.Puzzle) };
        var configuration = new Configuration();
        configuration.GameStats.Add(new GameStatRecord { GameId = "flow.easy", BestScore = 4 });
        configuration.GameStats.Add(new GameStatRecord { GameId = "flow.medium", BestScore = 7 });
        var library = new GamesLibrary(games, new GameStatsStore(configuration));
        var flow = library.IndexOf("flow");

        Assert.Equal(RecordKind.Level, library.BestKind(flow));
        Assert.Equal("7", library.BestValue(flow));
        Assert.Equal("Medium", library.BestTier(flow));
    }

    [Fact]
    public void GamesWithoutTiersCarryNoTierLabel()
    {
        var configuration = new Configuration();
        configuration.GameStats.Add(new GameStatRecord { GameId = "snake", BestScore = 40 });
        var library = Build(configuration);

        Assert.Equal(string.Empty, library.BestTier(library.IndexOf("snake")));
    }
}
