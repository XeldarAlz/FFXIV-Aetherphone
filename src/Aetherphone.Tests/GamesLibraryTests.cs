using Aetherphone.Apps.Games;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Online;
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

        public FakeGame(GameSpec spec, string title)
        {
            Spec = spec;
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

    private sealed class FixedRanks : IRankSource
    {
        private readonly string rankedStatId;
        private readonly int rankValue;

        public FixedRanks(string rankedStatId, int rankValue)
        {
            this.rankedStatId = rankedStatId;
            this.rankValue = rankValue;
        }

        public bool TryGetRank(string statId, out GameRank rank)
        {
            rank = string.Equals(statId, rankedStatId, StringComparison.Ordinal)
                ? new GameRank(rankValue, 5000, 0, 0, RankState.Ranked)
                : GameRank.Unknown;
            return rank.IsRanked;
        }
    }

    private static readonly int ReleaseWave = new DateOnly(2026, 10, 8).DayNumber;

    private static GamesLibrary Build(Configuration configuration) =>
        new(Games, new GameStatsStore(configuration));

    private static string[] IdsOf(GamesLibrary library, ReadOnlySpan<int> entries)
    {
        var ids = new string[entries.Length];
        for (var index = 0; index < entries.Length; index++)
        {
            ids[index] = library.Entries[entries[index]].Id;
        }

        return ids;
    }

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
            "online.broadside", "online.luckydraw", "online.crater", "online.minigolf", "online.connectfour",
            "online.uno", "online.chess", "online.pool", "doom", "wordrun", "chess", "tetris", "minesweeper", "snake", "breakout",
        }, ids);
    }

    [Fact]
    public void LatestHoldsOnlyTheNewestWave()
    {
        var library = Build(new Configuration());

        var latest = library.Latest.ToArray();

        Assert.NotEmpty(latest);
        for (var index = 0; index < latest.Length; index++)
        {
            Assert.NotEqual("chess", library.Entries[latest[index]].Id);
            Assert.StartsWith("online.", library.Entries[latest[index]].Id, StringComparison.Ordinal);
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
        Assert.Equal(OnlineGameArt.Kinds.Length, friends.Length);
        Assert.True(library.Entries[friends[0]].Online);
    }

    [Fact]
    public void AnEmptyStrategyShelfLeavesTheNeighbouringShelvesIntact()
    {
        var library = Build(new Configuration());

        var tabletop = library.Genre(GameGenre.Tabletop).ToArray();

        Assert.Equal(0, library.Genre(GameGenre.Strategy).Length);
        Assert.Single(tabletop);
        Assert.Equal("chess", library.Entries[tabletop[0]].Id);
        Assert.Equal(OnlineGameArt.Kinds.Length, library.Genre(GameGenre.Friends).Length);
        for (var shelf = 0; shelf < GameGenres.Shelves.Length; shelf++)
        {
            Assert.Equal((GameGenre)shelf, GameGenres.Shelves[shelf]);
        }

        Assert.Equal(GameGenre.Tabletop, GameGenres.Shelves[(int)GameGenre.Strategy + 1]);
        Assert.Equal("games.genreStrategy", GameGenres.Label(GameGenre.Strategy).Key);
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
    public void SolitaireShowsItsVegasScoreUntilAClassicTimeExists()
    {
        var games = new IMiniGame[] { new FakeGame("solitaire", "Solitaire", GameGenre.Tabletop) };
        var configuration = new Configuration();
        configuration.GameStats.Add(new GameStatRecord { GameId = "solitaire.vegas", BestScore = 120 });
        var library = new GamesLibrary(games, new GameStatsStore(configuration));
        var solitaire = library.IndexOf("solitaire");

        Assert.Equal(RecordKind.Score, library.BestKind(solitaire));
        Assert.Equal("120", library.BestValue(solitaire));

        configuration.GameStats.Add(new GameStatRecord { GameId = "solitaire", BestTimeSeconds = 95 });
        library.Rebuild();

        Assert.Equal(RecordKind.Time, library.BestKind(solitaire));
        Assert.Equal("1:35", library.BestValue(solitaire));
        Assert.Equal(string.Empty, library.BestTier(solitaire));
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
    public void SnakeReadsTheHigherOfItsClassicAndWrapRecords()
    {
        var configuration = new Configuration();
        configuration.GameStats.Add(new GameStatRecord { GameId = "snake", BestScore = 12 });
        configuration.GameStats.Add(new GameStatRecord { GameId = "snake.wrap", BestScore = 31 });
        var library = Build(configuration);
        var snake = library.IndexOf("snake");

        Assert.Equal(RecordKind.Score, library.BestKind(snake));
        Assert.Equal("31", library.BestValue(snake));
        Assert.Equal(string.Empty, library.BestTier(snake));
    }

    [Fact]
    public void TetrisReadsTheHigherOfItsClassicAndModernRecords()
    {
        var configuration = new Configuration();
        configuration.GameStats.Add(new GameStatRecord { GameId = "tetris", BestScore = 900 });
        configuration.GameStats.Add(new GameStatRecord { GameId = "tetris.modern", BestScore = 2400 });
        var library = Build(configuration);
        var tetris = library.IndexOf("tetris");

        Assert.Equal(RecordKind.Score, library.BestKind(tetris));
        Assert.Equal("2400", library.BestValue(tetris));
        Assert.Equal(string.Empty, library.BestTier(tetris));
    }

    [Fact]
    public void GemSwapReadsTheHigherOfItsClassicAndBlitzRecords()
    {
        var games = new IMiniGame[] { new FakeGame("match3", "Gem Swap", GameGenre.Puzzle) };
        var configuration = new Configuration();
        configuration.GameStats.Add(new GameStatRecord { GameId = "match3", BestScore = 400 });
        configuration.GameStats.Add(new GameStatRecord { GameId = "match3.blitz", BestScore = 1250 });
        var library = new GamesLibrary(games, new GameStatsStore(configuration));
        var gemSwap = library.IndexOf("match3");

        Assert.Equal(RecordKind.Score, library.BestKind(gemSwap));
        Assert.Equal("1250", library.BestValue(gemSwap));
        Assert.Equal(string.Empty, library.BestTier(gemSwap));
    }

    [Fact]
    public void GamesWithoutTiersCarryNoTierLabel()
    {
        var configuration = new Configuration();
        configuration.GameStats.Add(new GameStatRecord { GameId = "snake", BestScore = 40 });
        var library = Build(configuration);

        Assert.Equal(string.Empty, library.BestTier(library.IndexOf("snake")));
    }

    [Fact]
    public void ReversiShowsTheHigherOfItsTwoDifficultyStreaks()
    {
        var games = new IMiniGame[] { new FakeGame("reversi", "Reversi", GameGenre.Tabletop) };
        var configuration = new Configuration();
        configuration.GameStats.Add(new GameStatRecord { GameId = "reversi.easy", Streak = 5 });
        configuration.GameStats.Add(new GameStatRecord { GameId = "reversi.hard", Streak = 2 });
        var library = new GamesLibrary(games, new GameStatsStore(configuration));
        var reversi = library.IndexOf("reversi");

        Assert.Equal(RecordKind.Streak, library.BestKind(reversi));
        Assert.Equal("5", library.BestValue(reversi));
        Assert.Equal("Easy", library.BestTier(reversi));
        Assert.EndsWith("5 · Easy", library.Best(reversi));
    }

    [Fact]
    public void ChessShowsTheHighestStreakAcrossThreeDifficulties()
    {
        var configuration = new Configuration();
        configuration.GameStats.Add(new GameStatRecord { GameId = "chess.easy", Streak = 1 });
        configuration.GameStats.Add(new GameStatRecord { GameId = "chess.medium", Streak = 4 });
        configuration.GameStats.Add(new GameStatRecord { GameId = "chess.hard", Streak = 2 });
        var library = Build(configuration);
        var chess = library.IndexOf("chess");

        Assert.Equal(RecordKind.Streak, library.BestKind(chess));
        Assert.Equal("4", library.BestValue(chess));
        Assert.Equal("Medium", library.BestTier(chess));
    }

    [Fact]
    public void AStreakRecordedBeforeTiersStillShowsWithoutATierLabel()
    {
        var games = new IMiniGame[] { new FakeGame("reversi", "Reversi", GameGenre.Tabletop) };
        var configuration = new Configuration();
        configuration.GameStats.Add(new GameStatRecord { GameId = "reversi", Streak = 3 });
        var library = new GamesLibrary(games, new GameStatsStore(configuration));
        var reversi = library.IndexOf("reversi");

        Assert.Equal(RecordKind.Streak, library.BestKind(reversi));
        Assert.Equal("3", library.BestValue(reversi));
        Assert.Equal(string.Empty, library.BestTier(reversi));
    }

    [Fact]
    public void LatestKeepsEveryReleaseWithinTwoWeeksOfTheNewestUncapped()
    {
        var library = Build(new Configuration());

        Assert.Equal(new[]
        {
            "online.broadside", "online.luckydraw", "online.crater", "online.minigolf", "online.connectfour",
        }, IdsOf(library, library.Latest));
    }

    [Fact]
    public void IdsArePrecomputedForEveryEntry()
    {
        var library = Build(new Configuration());
        var snake = library.IndexOf("snake");
        var pool = library.IndexOf("online.pool");
        var luckyDraw = library.IndexOf("online.luckydraw");

        Assert.Equal("games.tile.snake", library.TileIds[snake]);
        Assert.Equal("games.play.snake", library.PlayIds[snake]);
        Assert.Equal("snake", library.IconIds[snake]);
        Assert.Equal("pool", library.IconIds[pool]);
        Assert.Equal("luckydraw", library.IconIds[luckyDraw]);
        Assert.Equal("games.tile.online.pool", library.TileIds[pool]);
    }

    [Theory]
    [InlineData(100, 86, 0L, true)]
    [InlineData(100, 85, 0L, false)]
    [InlineData(100, 100, 0L, true)]
    [InlineData(100, 102, 0L, true)]
    [InlineData(100, 90, 1L, false)]
    [InlineData(100, 0, 0L, false)]
    public void NewMeansReleasedInTheLastTwoWeeksAndNeverPlayed(int today, int addedDay, long lastPlayed,
        bool expected)
    {
        Assert.Equal(expected, GamesLibrary.IsNewOn(today, addedDay, lastPlayed));
    }

    [Fact]
    public void TheMetaWordIsNewOnlyForFreshUnplayedEntries()
    {
        var configuration = new Configuration();
        configuration.GameStats.Add(new GameStatRecord
        {
            GameId = GamesLibrary.OnlineEntryId(GameRoomWire.CraterKind), LastPlayedUnixSeconds = 50,
        });
        var library = Build(configuration);

        library.Rebuild(ReleaseWave + 3);

        Assert.True(library.IsNew(library.IndexOf("online.broadside")));
        Assert.Equal("New", library.Meta(library.IndexOf("online.broadside")));
        Assert.False(library.IsNew(library.IndexOf("online.crater")));
        Assert.Equal("With friends", library.Meta(library.IndexOf("online.crater")));
        Assert.Equal("Arcade", library.Meta(library.IndexOf("snake")));

        library.Rebuild(ReleaseWave + GamesLibrary.NewDays + 1);

        Assert.False(library.IsNew(library.IndexOf("online.broadside")));
    }

    [Fact]
    public void EyebrowsAndHooksAreCachedPerEntry()
    {
        var hooked = new GameSpec("tetris", new LocString("test.tetris", "Tetris"), GameGenre.Puzzle,
            new LocString("test.tetris.hook", "Clear lines before the stack tops out."));
        var games = new IMiniGame[] { new FakeGame(hooked, "Tetris") };
        var library = new GamesLibrary(games, new GameStatsStore(new Configuration()));
        var tetris = library.IndexOf("tetris");

        Assert.Equal("Clear lines before the stack tops out.", library.Hook(tetris));
        Assert.Equal("New game · Puzzle", library.Eyebrow(tetris));
        Assert.Equal("Head-to-head, 10 minutes on each clock", library.Hook(library.IndexOf("online.chess")));
        Assert.Equal("Up to 6 players", library.Hook(library.IndexOf("online.uno")));
    }

    [Fact]
    public void AStarPackReportsItsStarsAndProgress()
    {
        var spec = new GameSpec("crates", new LocString("test.crates", "Crates"), GameGenre.Puzzle,
            kind: ScoreKind.Level, levelCount: 10);
        var configuration = new Configuration();
        configuration.GameLevelProgress.Add(new GameLevelProgress { GameId = "crates", Stars = "3321" });
        var library = new GamesLibrary(new IMiniGame[] { new FakeGame(spec, "Crates") },
            new GameStatsStore(configuration));
        var crates = library.IndexOf("crates");

        Assert.Equal(9, library.Stars(crates));
        Assert.Equal(30, library.StarMax(crates));
        Assert.Equal(1, library.StarTier(crates));
        Assert.Equal(0.3f, library.Progress(crates), 3);
        Assert.Equal("9 / 30 stars", library.ProgressLabel(crates));
        Assert.Equal(9, library.TotalStars);
    }

    [Fact]
    public void AScoredLevelPackReportsTheLevelItIsOn()
    {
        var spec = new GameSpec("pegfall", new LocString("test.pegfall", "Pegfall"), GameGenre.Arcade,
            levelCount: 20);
        var configuration = new Configuration();
        configuration.GameLevelProgress.Add(new GameLevelProgress { GameId = "pegfall", Stars = "32" });
        var library = new GamesLibrary(new IMiniGame[] { new FakeGame(spec, "Pegfall") },
            new GameStatsStore(configuration));
        var pegfall = library.IndexOf("pegfall");

        Assert.Equal(0.1f, library.Progress(pegfall), 3);
        Assert.Equal("Level 3 of 20", library.ProgressLabel(pegfall));
        Assert.Equal(5, library.Stars(pegfall));
        Assert.Equal(60, library.StarMax(pegfall));
    }

    [Fact]
    public void AClearedLevelPackIsFullyDone()
    {
        var spec = new GameSpec("pegfall", new LocString("test.pegfall", "Pegfall"), GameGenre.Arcade,
            levelCount: 2);
        var configuration = new Configuration();
        configuration.GameLevelProgress.Add(new GameLevelProgress { GameId = "pegfall", Stars = "33" });
        var library = new GamesLibrary(new IMiniGame[] { new FakeGame(spec, "Pegfall") },
            new GameStatsStore(configuration));
        var pegfall = library.IndexOf("pegfall");

        Assert.Equal(1f, library.Progress(pegfall), 3);
        Assert.Equal("Level 2 of 2", library.ProgressLabel(pegfall));
        Assert.Equal(3, library.StarTier(pegfall));
    }

    [Fact]
    public void WithoutALevelPackTheProgressLineIsTheBest()
    {
        var configuration = new Configuration();
        configuration.GameStats.Add(new GameStatRecord { GameId = "snake", BestScore = 40 });
        configuration.GameStats.Add(new GameStatRecord { GameId = "chess.hard", Streak = 3 });
        configuration.GameStats.Add(new GameStatRecord { GameId = "minesweeper.easy", BestTimeSeconds = 65 });
        var library = Build(configuration);

        Assert.Equal(-1f, library.Progress(library.IndexOf("snake")));
        Assert.Equal("Best 40", library.ProgressLabel(library.IndexOf("snake")));
        Assert.Equal("Streak 3 · Hard", library.ProgressLabel(library.IndexOf("chess")));
        Assert.Equal("Best 1:05 · Easy", library.ProgressLabel(library.IndexOf("minesweeper")));
        Assert.Equal("Action", library.ProgressLabel(library.IndexOf("doom")));
        Assert.Equal(0, library.StarMax(library.IndexOf("snake")));
    }

    [Fact]
    public void RankLabelsStopAtTheCapButTheRankIsKept()
    {
        var near = new GamesLibrary(Games, new GameStatsStore(new Configuration()), new FixedRanks("snake", 12));
        var far = new GamesLibrary(Games, new GameStatsStore(new Configuration()),
            new FixedRanks("snake", GamesLibrary.RankCap + 1));

        Assert.Equal("#12", near.RankLabel(near.IndexOf("snake")));
        Assert.Equal(12, near.Rank(near.IndexOf("snake")));
        Assert.Equal(string.Empty, far.RankLabel(far.IndexOf("snake")));
        Assert.Equal(GamesLibrary.RankCap + 1, far.Rank(far.IndexOf("snake")));
        Assert.Equal(0, near.Rank(near.IndexOf("tetris")));
    }

    [Fact]
    public void GenreOrderFollowsThePlayedGamesWithTiesInShelfOrder()
    {
        var configuration = new Configuration();
        configuration.GameStats.Add(new GameStatRecord { GameId = "snake", LastPlayedUnixSeconds = 10 });
        configuration.GameStats.Add(new GameStatRecord { GameId = "breakout", LastPlayedUnixSeconds = 20 });
        configuration.GameStats.Add(new GameStatRecord { GameId = "chess", LastPlayedUnixSeconds = 30 });
        configuration.GameStats.Add(new GameStatRecord { GameId = "tetris", LastPlayedUnixSeconds = 40 });
        configuration.GameStats.Add(new GameStatRecord
        {
            GameId = GamesLibrary.OnlineEntryId(GameRoomWire.PoolKind), LastPlayedUnixSeconds = 50,
        });
        var library = Build(configuration);

        Assert.Equal(new[]
        {
            GameGenre.Arcade, GameGenre.Puzzle, GameGenre.Tabletop, GameGenre.Action, GameGenre.Brain,
            GameGenre.Strategy,
        }, library.GenreOrder.ToArray());
    }

    [Fact]
    public void GenreOrderIsShelfOrderBeforeAnythingIsPlayed()
    {
        var library = Build(new Configuration());

        Assert.Equal(new[]
        {
            GameGenre.Arcade, GameGenre.Action, GameGenre.Puzzle, GameGenre.Brain, GameGenre.Strategy,
            GameGenre.Tabletop,
        }, library.GenreOrder.ToArray());
    }

    [Fact]
    public void ViewFiltersByGenreInEverySortOrder()
    {
        var library = Build(new Configuration());

        Assert.Equal(new[] { "snake", "breakout" },
            IdsOf(library, library.View(GamesFilter.Arcade, GamesSort.Newest, string.Empty)));
        Assert.Equal(new[] { "breakout", "snake" },
            IdsOf(library, library.View(GamesFilter.Arcade, GamesSort.Title, string.Empty)));
        Assert.Equal(new[] { "minesweeper", "wordrun" },
            IdsOf(library, library.View(GamesFilter.Brain, GamesSort.Title, string.Empty)));
        Assert.Equal(library.Entries.Length, library.View(GamesFilter.All, GamesSort.Newest, string.Empty).Length);
    }

    [Fact]
    public void TheTitleSortIsAlphabetical()
    {
        var library = Build(new Configuration());

        var view = library.View(GamesFilter.All, GamesSort.Title, string.Empty);

        for (var position = 1; position < view.Length; position++)
        {
            Assert.True(string.Compare(library.Title(view[position - 1]), library.Title(view[position]),
                StringComparison.OrdinalIgnoreCase) <= 0);
        }
    }

    [Fact]
    public void TheTogetherFilterKeepsOnlyOnlineEntries()
    {
        var library = Build(new Configuration());

        var view = library.View(GamesFilter.Together, GamesSort.Newest, string.Empty);

        Assert.Equal(OnlineGameArt.Kinds.Length, view.Length);
        for (var position = 0; position < view.Length; position++)
        {
            Assert.True(library.Entries[view[position]].Online);
        }
    }

    [Fact]
    public void TheRecentSortPutsPlayedGamesFirstThenTheNewest()
    {
        var configuration = new Configuration();
        configuration.GameStats.Add(new GameStatRecord { GameId = "snake", LastPlayedUnixSeconds = 100 });
        configuration.GameStats.Add(new GameStatRecord { GameId = "chess", LastPlayedUnixSeconds = 300 });
        var library = Build(configuration);

        var ids = IdsOf(library, library.View(GamesFilter.All, GamesSort.Recent, string.Empty));

        Assert.Equal(library.Entries.Length, ids.Length);
        Assert.Equal("chess", ids[0]);
        Assert.Equal("snake", ids[1]);
        Assert.Equal("online.broadside", ids[2]);
        Assert.Equal("breakout", ids[^1]);
    }

    [Fact]
    public void TheQueryNarrowsTheFilteredView()
    {
        var library = Build(new Configuration());

        Assert.Equal(new[] { "wordrun" },
            IdsOf(library, library.View(GamesFilter.Brain, GamesSort.Newest, "  word ")));
        Assert.Empty(IdsOf(library, library.View(GamesFilter.Arcade, GamesSort.Newest, "word")));
        Assert.Equal(new[] { "online.chess", "chess" },
            IdsOf(library, library.View(GamesFilter.All, GamesSort.Newest, "CHESS")));
    }

    [Fact]
    public void TheViewIsRebuiltWhenTheLibraryChanges()
    {
        var configuration = new Configuration();
        var library = Build(configuration);
        var query = string.Empty;

        Assert.Equal("online.broadside", IdsOf(library, library.View(GamesFilter.All, GamesSort.Recent, query))[0]);

        configuration.GameStats.Add(new GameStatRecord { GameId = "doom", LastPlayedUnixSeconds = 10 });
        Assert.Equal("online.broadside", IdsOf(library, library.View(GamesFilter.All, GamesSort.Recent, query))[0]);
        library.Rebuild();

        Assert.Equal("doom", IdsOf(library, library.View(GamesFilter.All, GamesSort.Recent, query))[0]);
    }

    [Fact]
    public void EveryFilterNamesItsGenre()
    {
        Assert.Equal(GamesFilters.Count, (int)GamesFilter.Together + 1);
        Assert.Equal(GameGenre.Arcade, GamesFilters.Genre(GamesFilter.Arcade));
        Assert.Equal(GameGenre.Tabletop, GamesFilters.Genre(GamesFilter.Tabletop));
        Assert.Equal(GameGenre.Friends, GamesFilters.Genre(GamesFilter.Together));
        Assert.True(GamesFilters.Matches(GamesFilter.All, GameGenre.Friends));
        Assert.False(GamesFilters.Matches(GamesFilter.Puzzle, GameGenre.Brain));
        Assert.Equal("gamesHub.filterAll", GamesFilters.Label(GamesFilter.All).Key);
        Assert.Equal("gamesHub.tabTogether", GamesFilters.Label(GamesFilter.Together).Key);
        Assert.Equal("games.genreTabletop", GamesFilters.Label(GamesFilter.Tabletop).Key);
        Assert.Equal("gamesHub.sortRecent", GamesSorts.Label(GamesSort.Recent).Key);
    }
}
