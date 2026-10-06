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
}
