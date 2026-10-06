using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class GameSessionTests
{
    private sealed class FakeStatsConfiguration : IGameStatsConfiguration
    {
        public List<GameStatRecord> GameStats { get; } = new();
        public List<GameModeChoice> GameModeChoices { get; } = new();
        public List<GameLevelProgress> GameLevelProgress { get; } = new();
        public int DailyChallengeStreak { get; set; }
        public int DailyChallengeLastDay { get; set; }
        public string WordRunBank { get; set; } = string.Empty;
        public bool TetrisModern { get; set; }
        public int Saves { get; private set; }

        public void Save()
        {
            Saves++;
        }
    }

    private sealed class CountingSink : IScoreSink
    {
        public int Count { get; private set; }
        public ScoreSubmission Last { get; private set; }

        public void Submit(in ScoreSubmission submission)
        {
            Count++;
            Last = submission;
        }
    }

    private sealed class FixedRank : IRankSource
    {
        public GameRank Rank { get; set; } = GameRank.Unknown;

        public bool TryGetRank(string statId, out GameRank rank)
        {
            rank = Rank;
            return Rank.State != RankState.Unknown;
        }
    }

    private static readonly LocString[] Tiers = { new("t.easy", "Easy"), new("t.hard", "Hard") };
    private static readonly string[] TierStatIds = { "tap.easy", "tap.hard" };

    private static readonly ScoreKind[] TierKinds = { ScoreKind.Time, ScoreKind.Score };
    private static readonly bool[] TierCountdowns = { false, true };

    private static GameSpec Spec(bool countdown = false, ScoreKind kind = ScoreKind.Score, bool modes = false,
        bool modeKinds = false, bool countdownModes = false) =>
        new("tap", new LocString("t.title", "Tap"), GameGenre.Arcade, kind: kind, clocked: true, countdown: countdown,
            modes: modes ? Tiers : null, modeStatIds: modes ? TierStatIds : null,
            modeKinds: modeKinds ? TierKinds : null, countdownModes: countdownModes ? TierCountdowns : null);

    private static GameSession Build(out FakeStatsConfiguration configuration, out CountingSink sink,
        IRankSource? ranks = null)
    {
        configuration = new FakeStatsConfiguration();
        sink = new CountingSink();
        return new GameSession(new GameStatsStore(configuration), sink, ranks ?? new FixedRank());
    }

    [Fact]
    public void BeginLandsOnTheIntroWithTheStartValues()
    {
        var session = Build(out _, out _);

        session.Begin(Spec(), new GameStart(0, 77, true));

        Assert.Equal(StageFlow.Intro, session.State);
        Assert.Equal(77UL, session.Seed);
        Assert.True(session.Daily);
        Assert.Equal("tap", session.StatId);
        Assert.Equal(0, session.Best);
    }

    [Fact]
    public void PlaySkipsTheCountdownUnlessTheSpecAsksForIt()
    {
        var plain = Build(out _, out _);
        plain.Begin(Spec(), new GameStart(0, 1, false));
        plain.Play();
        Assert.Equal(StageFlow.Playing, plain.State);

        var counted = Build(out _, out _);
        counted.Begin(Spec(countdown: true), new GameStart(0, 1, false));
        counted.Play();
        Assert.Equal(StageFlow.Countdown, counted.State);
        Assert.Equal(3, counted.CountdownStep);
    }

    [Fact]
    public void TheCountdownTicksThroughThreeStepsThenGo()
    {
        var session = Build(out _, out _);
        session.Begin(Spec(countdown: true), new GameStart(0, 1, false));
        session.Play();

        session.Tick(GameSession.CountdownStepSeconds + 0.01f);
        Assert.Equal(2, session.CountdownStep);
        Assert.True(session.CountdownStepChanged);
        session.Tick(GameSession.CountdownStepSeconds);
        Assert.Equal(1, session.CountdownStep);
        session.Tick(GameSession.CountdownStepSeconds);
        Assert.Equal(0, session.CountdownStep);
        Assert.Equal(StageFlow.Countdown, session.State);
        session.Tick(GameSession.CountdownStepSeconds);

        Assert.Equal(StageFlow.Playing, session.State);
    }

    [Fact]
    public void PauseAndResumeOnlyApplyWhilePlaying()
    {
        var session = Build(out _, out _);
        session.Begin(Spec(), new GameStart(0, 1, false));
        session.Pause();
        Assert.Equal(StageFlow.Intro, session.State);

        session.Play();
        session.Pause();
        Assert.Equal(StageFlow.Paused, session.State);
        session.Tick(1f);
        Assert.Equal(0f, session.PlaySeconds);
        session.Resume();
        Assert.Equal(StageFlow.Playing, session.State);
        session.Tick(0.5f);
        Assert.Equal(0.5f, session.PlaySeconds);
    }

    [Fact]
    public void FinishSubmitsOnceToStatsAndTheSink()
    {
        var session = Build(out var configuration, out var sink);
        session.Begin(Spec(), new GameStart(0, 5, false));
        session.Play();
        session.Report(40);

        session.Finish(new GameOutcome(40, ScoreKind.Score, "tap"));
        session.Finish(new GameOutcome(99, ScoreKind.Score, "tap"));

        Assert.Equal(StageFlow.Result, session.State);
        Assert.Equal(1, sink.Count);
        Assert.Equal(40, sink.Last.Value);
        Assert.Equal(5UL, sink.Last.Seed);
        Assert.Equal("tap", sink.Last.GameId);
        Assert.Single(configuration.GameStats);
        Assert.Equal(40, configuration.GameStats[0].BestScore);
        Assert.True(session.NewBest);
        Assert.Equal(40, session.Best);
        Assert.Equal(40, session.ResultValue);
    }

    [Fact]
    public void FinishBeforePlayingIsIgnored()
    {
        var session = Build(out _, out var sink);
        session.Begin(Spec(), new GameStart(0, 5, false));

        session.Finish(new GameOutcome(40, ScoreKind.Score, "tap"));

        Assert.Equal(StageFlow.Intro, session.State);
        Assert.Equal(0, sink.Count);
    }

    [Fact]
    public void FinishCompletesTheDailyChallenge()
    {
        var session = Build(out var configuration, out _);
        var stats = session.Stats;
        stats.DailyGameId = "tap";
        session.Begin(Spec(), new GameStart(0, 5, true));
        session.Play();

        session.Finish(new GameOutcome(0, ScoreKind.Score, "tap"));

        Assert.True(stats.DailyDone);
        Assert.Equal(1, stats.DailyStreak);
        Assert.Equal(GameStatsStore.TodayIndex, configuration.DailyChallengeLastDay);
    }

    [Fact]
    public void TimeOutcomesSubmitAsBestTimes()
    {
        var session = Build(out var configuration, out var sink);
        session.Begin(Spec(kind: ScoreKind.Time), new GameStart(0, 5, false));
        session.Play();

        session.Finish(new GameOutcome(65, ScoreKind.Time, "tap"));

        Assert.Equal(65, configuration.GameStats[0].BestTimeSeconds);
        Assert.Equal(ScoreKind.Time, sink.Last.Kind);
        Assert.Equal(65, session.Best);
    }

    [Fact]
    public void ALostTimeRunCompletesTheDailyButRecordsNoBestAndUploadsNothing()
    {
        var session = Build(out var configuration, out var sink);
        var stats = session.Stats;
        stats.DailyGameId = "tap";
        configuration.GameStats.Add(new GameStatRecord { GameId = "tap", BestTimeSeconds = 90 });
        session.Begin(Spec(kind: ScoreKind.Time), new GameStart(0, 5, true));
        session.Play();

        session.Finish(new GameOutcome(30, ScoreKind.Time, "tap", won: false));

        Assert.Equal(StageFlow.Result, session.State);
        Assert.Equal(90, configuration.GameStats[0].BestTimeSeconds);
        Assert.False(session.NewBest);
        Assert.Equal(0, sink.Count);
        Assert.True(stats.DailyDone);
        Assert.Equal(90, session.Best);
    }

    [Fact]
    public void ADrawKeepsTheStreakAndUploadsNothing()
    {
        var session = Build(out var configuration, out var sink);
        var stats = session.Stats;
        stats.DailyGameId = "tap";
        configuration.GameStats.Add(new GameStatRecord { GameId = "tap", Streak = 4 });
        session.Begin(Spec(kind: ScoreKind.Streak), new GameStart(0, 5, true));
        session.Play();

        session.Finish(GameOutcome.Drawn("tap"));

        Assert.True(session.Outcome.IsDraw);
        Assert.Equal(4, configuration.GameStats[0].Streak);
        Assert.Equal(4, session.ResultValue);
        Assert.False(session.NewBest);
        Assert.Equal(0, sink.Count);
        Assert.True(stats.DailyDone);
    }

    [Fact]
    public void ModeKindsDriveTheBestAndTheBeatingGlow()
    {
        var session = Build(out var configuration, out _);
        configuration.GameStats.Add(new GameStatRecord { GameId = "tap.easy", BestTimeSeconds = 40, BestScore = 7 });
        configuration.GameStats.Add(new GameStatRecord { GameId = "tap.hard", BestScore = 12, BestTimeSeconds = 3 });
        session.Begin(Spec(kind: ScoreKind.Time, modes: true, modeKinds: true), new GameStart(0, 5, false));
        Assert.Equal(ScoreKind.Time, session.Kind);
        Assert.Equal(40, session.Best);

        session.SelectMode(1);

        Assert.Equal(ScoreKind.Score, session.Kind);
        Assert.Equal(12, session.Best);
        session.Play();
        session.Report(13);
        Assert.True(session.BeatingBest);
    }

    [Fact]
    public void CountdownModesOverrideTheSpecFlagPerMode()
    {
        var session = Build(out _, out _);
        session.Begin(Spec(modes: true, countdownModes: true), new GameStart(0, 5, false));
        session.Play();
        Assert.Equal(StageFlow.Playing, session.State);

        var blitz = Build(out _, out _);
        blitz.Begin(Spec(modes: true, countdownModes: true), new GameStart(1, 5, false));
        blitz.Play();
        Assert.Equal(StageFlow.Countdown, blitz.State);
    }

    [Fact]
    public void OutcomeDecorationsSurviveEveryBuilder()
    {
        var label = new LocString("t.next", "Next level");
        var outcome = new GameOutcome(3, ScoreKind.Level, "tap")
            .WithContinueLabel(label)
            .WithQuietBest()
            .WithStat(label, "1")
            .WithSecondary("tap.height", 9);

        Assert.True(outcome.QuietBest);
        Assert.Equal("t.next", outcome.ContinueLabel!.Value.Key);
        Assert.Equal(1, outcome.StatCount);
        Assert.Equal(9, outcome.SecondaryValue);
        Assert.False(outcome.IsDraw);
    }

    [Fact]
    public void StreakOutcomesRecordWinsAndLosses()
    {
        var session = Build(out var configuration, out var sink);
        session.Begin(Spec(kind: ScoreKind.Streak), new GameStart(0, 5, false));
        session.Play();
        session.Finish(new GameOutcome(0, ScoreKind.Streak, "tap", won: true));
        Assert.Equal(1, configuration.GameStats[0].Streak);
        Assert.Equal(1, session.ResultValue);
        Assert.Equal(1, sink.Last.Value);

        session.Play();
        session.Finish(new GameOutcome(0, ScoreKind.Streak, "tap", won: false));

        Assert.Equal(0, configuration.GameStats[0].Streak);
        Assert.Equal(2, sink.Count);
    }

    [Fact]
    public void SecondaryStatsLandNextToThePrimaryAndReachTheSink()
    {
        var session = Build(out var configuration, out var sink);
        session.Begin(Spec(), new GameStart(0, 5, false));
        session.Play();

        session.Finish(new GameOutcome(40, ScoreKind.Score, "tap").WithSecondary("tap.height", 120));

        Assert.Equal(2, configuration.GameStats.Count);
        Assert.Equal(120, configuration.GameStats[1].BestScore);
        Assert.Equal(2, sink.Count);
        Assert.Equal("tap", sink.Last.StatId);
        Assert.Equal(40, sink.Last.Value);
    }

    [Fact]
    public void TheRankLookupFoldsATierOntoItsCatalogRoot()
    {
        var ranks = new RecordingRank();
        var session = Build(out _, out _, ranks);
        var tiers = new LocString[] { new("t.easy", "Easy"), new("t.hard", "Hard") };
        var tierStatIds = new[] { "chess.easy", "chess.hard" };
        var spec = new GameSpec("chess", new LocString("t.chess", "Chess"), GameGenre.Tabletop, kind: ScoreKind.Streak,
            modes: tiers, modeStatIds: tierStatIds);

        session.Begin(spec, new GameStart(1, 5, false));

        Assert.Equal("chess.hard", session.StatId);
        Assert.Equal("chess", session.LeaderboardStatId);
        Assert.Equal("chess", ranks.LastStatId);
    }

    private sealed class RecordingRank : IRankSource
    {
        public string LastStatId { get; private set; } = string.Empty;

        public bool TryGetRank(string statId, out GameRank rank)
        {
            LastStatId = statId;
            rank = GameRank.Unknown;
            return false;
        }
    }

    [Fact]
    public void PlayAgainKeepsTheDailySeedButRollsAFreshOne()
    {
        var daily = Build(out _, out _);
        daily.Begin(Spec(), new GameStart(0, 5, true));
        daily.Play();
        daily.Finish(new GameOutcome(1, ScoreKind.Score, "tap"));
        daily.Play();
        Assert.Equal(5UL, daily.Seed);
        Assert.Equal(StageFlow.Playing, daily.State);
        Assert.False(daily.Finished);

        var fresh = Build(out _, out _);
        fresh.Begin(Spec(), new GameStart(0, 5, false));
        fresh.Play();
        fresh.Finish(new GameOutcome(1, ScoreKind.Score, "tap"));
        fresh.Play();

        Assert.NotEqual(5UL, fresh.Seed);
        Assert.Equal(2, fresh.Runs);
    }

    [Fact]
    public void SelectingAModeSwitchesTheStatAndPersistsTheChoice()
    {
        var session = Build(out var configuration, out _);
        configuration.GameStats.Add(new GameStatRecord { GameId = "tap.hard", BestScore = 12 });
        session.Begin(Spec(modes: true), new GameStart(0, 5, false));
        Assert.Equal("tap.easy", session.StatId);

        session.SelectMode(1);

        Assert.Equal("tap.hard", session.StatId);
        Assert.Equal(12, session.Best);
        Assert.Equal(1, session.Stats.LastMode("tap"));
        Assert.Equal(1, configuration.Saves);
    }

    [Fact]
    public void BeatingBestTracksReportedScores()
    {
        var session = Build(out var configuration, out _);
        configuration.GameStats.Add(new GameStatRecord { GameId = "tap", BestScore = 10 });
        session.Begin(Spec(), new GameStart(0, 5, false));
        session.Play();

        session.Report(9);
        Assert.False(session.BeatingBest);
        session.Report(11);
        Assert.True(session.BeatingBest);
    }

    [Fact]
    public void TheRankSourceFeedsTheResult()
    {
        var ranks = new FixedRank { Rank = new GameRank(8, 1240, 2, 3, RankState.Ranked) };
        var session = Build(out _, out _, ranks);
        var spec = new GameSpec("snake", new LocString("t.snake", "Snake"), GameGenre.Arcade, clocked: true);
        session.Begin(spec, new GameStart(0, 5, false));
        session.Play();
        session.Finish(new GameOutcome(40, ScoreKind.Score, "snake"));

        Assert.Equal(8, session.Rank.Rank);
        Assert.True(session.Rank.IsRanked);
        Assert.True(GameOverlay.IsTopTen(session.Rank));
    }

    private static readonly LocString[] PackModes = { new("t.levels", "Levels"), new("t.endless", "Endless") };
    private static readonly string[] PackStatIds = { "fling", "fling.endless" };
    private static readonly bool[] PackLevelModes = { true, false };

    private static GameSpec LevelSpec(ScoreKind kind = ScoreKind.Level, int levels = 40, bool modes = false) =>
        new("fling", new LocString("t.fling", "Fling"), GameGenre.Puzzle, kind: kind, levelCount: levels,
            modes: modes ? PackModes : null, modeStatIds: modes ? PackStatIds : null,
            levelModes: modes ? PackLevelModes : null);

    [Fact]
    public void LevelProgressKeepsTheBestStarsPerLevelAndPersists()
    {
        var configuration = new FakeStatsConfiguration();
        var stats = new GameStatsStore(configuration);

        Assert.True(stats.SetStars("fling", 1, 2));
        Assert.False(stats.SetStars("fling", 1, 1));
        Assert.False(stats.SetStars("fling", 2, 0));
        Assert.True(stats.SetStars("fling", 1, 3));
        Assert.True(stats.SetStars("fling", 3, 1));
        Assert.True(stats.SetStars("fling", 4, 9));

        Assert.Single(configuration.GameLevelProgress);
        Assert.Equal("fling", configuration.GameLevelProgress[0].GameId);
        Assert.Equal("3013", configuration.GameLevelProgress[0].Stars);
        Assert.Equal(4, configuration.Saves);
        var reloaded = new GameStatsStore(configuration);
        Assert.Equal(3, reloaded.Stars("fling", 1));
        Assert.Equal(0, reloaded.Stars("fling", 2));
        Assert.Equal(3, reloaded.Stars("fling", 4));
        Assert.Equal(0, reloaded.Stars("fling", 41));
        Assert.Equal(7, reloaded.TotalStars("fling"));
        Assert.Equal(0, reloaded.TotalStars("snip"));
    }

    [Fact]
    public void ALevelUnlocksOnceTheLevelBeforeHoldsAStar()
    {
        var stats = new GameStatsStore(new FakeStatsConfiguration());

        Assert.True(stats.IsUnlocked("fling", 1));
        Assert.False(stats.IsUnlocked("fling", 2));
        Assert.False(stats.IsUnlocked("fling", 0));
        Assert.Equal(1, stats.HighestUnlocked("fling"));
        Assert.Equal(1, stats.NextLevel("fling", 40));
        Assert.Equal(0, stats.NextLevel("fling", 0));

        stats.SetStars("fling", 1, 3);
        stats.SetStars("fling", 2, 1);

        Assert.True(stats.IsUnlocked("fling", 3));
        Assert.False(stats.IsUnlocked("fling", 4));
        Assert.Equal(3, stats.HighestUnlocked("fling", 40));
        Assert.Equal(2, stats.HighestUnlocked("fling", 2));
        Assert.Equal(3, stats.NextLevel("fling", 40));
        Assert.Equal(2, stats.NextLevel("fling", 2));
    }

    [Fact]
    public void BeginStartsTheNextUnclearedLevelAndHandsItToTheGame()
    {
        var session = Build(out var configuration, out _);
        configuration.GameLevelProgress.Add(new GameLevelProgress { GameId = "fling", Stars = "3210" });

        session.Begin(LevelSpec(), new GameStart(0, 5, false));

        Assert.True(session.HasLevels);
        Assert.Equal(40, session.LevelCount);
        Assert.Equal(4, session.Level);
        Assert.Equal(6, session.TotalStars);
        Assert.False(session.SelectLevel(6));
        Assert.True(session.SelectLevel(2));
        session.Play();
        Assert.Equal(2, session.Start.Level);
        Assert.False(session.SelectLevel(1));
    }

    [Fact]
    public void AStarRunRecordsTheLevelAndSubmitsTheTotalStars()
    {
        var session = Build(out _, out var sink);
        session.Begin(LevelSpec(), new GameStart(0, 5, false));
        session.Play();
        session.Report(1200);
        Assert.False(session.BeatingBest);

        session.Finish(new GameOutcome(1200, ScoreKind.Level, "fling").WithStars(2));

        Assert.Equal(2, session.LevelStars);
        Assert.Equal(2, session.Stats.Stars("fling", 1));
        Assert.Equal(1, sink.Count);
        Assert.Equal(2, sink.Last.Value);
        Assert.Equal(ScoreKind.Level, sink.Last.Kind);
        Assert.Equal(2, session.ResultValue);
        Assert.True(session.NewBest);
        Assert.True(session.AdvanceLevel());
        Assert.Equal(2, session.Level);

        session.Play();
        Assert.Equal(GameOutcome.NoStars, session.LevelStars);
        session.Finish(new GameOutcome(900, ScoreKind.Level, "fling").WithStars(3));

        Assert.Equal(5, sink.Last.Value);
        Assert.Equal(5, session.Best);
        Assert.Equal(3, session.Stats.Stars("fling", 2));
    }

    [Fact]
    public void ReplayingALevelForFewerStarsKeepsTheBestAndTheTotal()
    {
        var session = Build(out _, out var sink);
        session.Begin(LevelSpec(), new GameStart(0, 5, false));
        session.Play();
        session.Finish(new GameOutcome(0, ScoreKind.Level, "fling").WithStars(3));
        session.Play();

        session.Finish(new GameOutcome(0, ScoreKind.Level, "fling").WithStars(1));

        Assert.Equal(3, session.Stats.Stars("fling", 1));
        Assert.Equal(3, sink.Last.Value);
        Assert.False(session.NewBest);
        Assert.Equal(1, session.LevelStars);
    }

    [Fact]
    public void NextLevelIsOfferedOnlyWhenTheRunEarnedStars()
    {
        var failed = Build(out _, out _);
        failed.Begin(LevelSpec(), new GameStart(0, 5, false));
        failed.Play();
        failed.Finish(new GameOutcome(0, ScoreKind.Level, "fling", won: false).WithStars(0));
        Assert.False(failed.CanAdvance);
        Assert.False(failed.AdvanceLevel());
        Assert.Equal(1, failed.Level);

        var silent = Build(out _, out _);
        silent.Begin(LevelSpec(), new GameStart(0, 5, false));
        silent.Play();
        silent.Finish(new GameOutcome(0, ScoreKind.Level, "fling"));
        Assert.Equal(GameOutcome.NoStars, silent.LevelStars);
        Assert.False(silent.CanAdvance);

        var last = Build(out _, out _);
        last.Begin(LevelSpec(levels: 1), new GameStart(0, 5, false));
        last.Play();
        last.Finish(new GameOutcome(0, ScoreKind.Level, "fling").WithStars(3));
        Assert.False(last.CanAdvance);

        var cleared = Build(out _, out _);
        cleared.Begin(LevelSpec(), new GameStart(0, 5, false));
        cleared.Play();
        Assert.False(cleared.CanAdvance);
        cleared.Finish(new GameOutcome(0, ScoreKind.Level, "fling").WithStars(1));
        Assert.True(cleared.CanAdvance);

        var plain = Build(out _, out _);
        plain.Begin(Spec(), new GameStart(0, 5, false));
        plain.Play();
        plain.Finish(new GameOutcome(40, ScoreKind.Score, "tap").WithStars(3));
        Assert.False(plain.HasLevels);
        Assert.Equal(0, plain.Level);
        Assert.False(plain.CanAdvance);
        Assert.Equal(40, plain.ResultValue);
    }

    [Fact]
    public void ScoreKindLevelRunsRecordStarsButKeepSubmittingTheirScore()
    {
        var session = Build(out _, out var sink);
        session.Begin(LevelSpec(ScoreKind.Score), new GameStart(0, 5, false));
        session.Play();

        session.Finish(new GameOutcome(48000, ScoreKind.Score, "fling").WithStars(2));

        Assert.Equal(48000, sink.Last.Value);
        Assert.Equal(2, session.Stats.Stars("fling", 1));
        Assert.True(session.CanAdvance);
    }

    [Fact]
    public void LevelModesKeepThePackOutOfTheOtherModes()
    {
        var session = Build(out _, out var sink);
        session.Begin(LevelSpec(modes: true), new GameStart(0, 5, false));
        Assert.Equal(1, session.Level);

        session.SelectMode(1);

        Assert.False(session.HasLevels);
        Assert.Equal(0, session.Level);
        session.Play();
        session.Finish(new GameOutcome(12, ScoreKind.Level, "fling.endless").WithStars(3));
        Assert.Equal(0, session.Stats.TotalStars("fling"));
        Assert.Equal(12, sink.Last.Value);
        Assert.Equal(GameOutcome.NoStars, session.LevelStars);
    }
}
