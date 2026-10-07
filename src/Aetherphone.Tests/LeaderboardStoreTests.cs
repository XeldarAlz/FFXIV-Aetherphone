using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Games;
using Aetherphone.Core.Net;
using Newtonsoft.Json;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LeaderboardStoreTests
{
    private const long Now = 100_000;

    private sealed class FakeUploadConfiguration : IScoreUploadConfiguration
    {
        public List<PendingScoreUpload> PendingScoreUploads { get; } = new();
        public int Saves { get; private set; }

        public void Save()
        {
            Saves++;
        }
    }

    private static ScoreSubmission Submission(string statId, int value, ScoreKind kind = ScoreKind.Score,
        string? gameId = null) =>
        new(statId, value, kind, 7UL, false, gameId ?? statId);

    private static GameScoreSubmitDto Reply(string reason, int rank = 0, int total = 0, int friendsRank = 0,
        int weekRank = 0) =>
        new(reason.Length == 0, reason, 0, rank, total, friendsRank, weekRank);

    private static ScoreUploadQueue Build(out FakeUploadConfiguration configuration)
    {
        configuration = new FakeUploadConfiguration();
        return new ScoreUploadQueue(configuration);
    }

    [Fact]
    public void UnknownStatIdsAndEmptyValuesAreIgnored()
    {
        var queue = Build(out var configuration);

        Assert.False(queue.Enqueue(Submission("doom", 10), Now));
        Assert.False(queue.Enqueue(Submission("tetris", 0), Now));
        Assert.False(queue.Enqueue(Submission("chess", 0, ScoreKind.Streak), Now));

        Assert.Equal(0, queue.Count);
        Assert.Equal(0, configuration.Saves);
    }

    [Fact]
    public void StreaksOnlyUploadForStreakStats()
    {
        var queue = Build(out _);

        Assert.False(queue.Enqueue(Submission("memory", 3, ScoreKind.Streak), Now));
        Assert.True(queue.Enqueue(Submission("chess", 3, ScoreKind.Streak), Now));
        Assert.False(queue.Enqueue(Submission("reversi", 3), Now));

        Assert.Equal(1, queue.Count);
    }

    [Fact]
    public void TierAndModeStatIdsFoldOntoTheCatalogRoot()
    {
        var queue = Build(out var configuration);

        Assert.True(queue.Enqueue(Submission("chess.hard", 3, ScoreKind.Streak, "chess"), Now));
        Assert.True(queue.Enqueue(Submission("snake.wrap", 40, gameId: "snake"), Now));
        Assert.False(queue.Enqueue(Submission("solitaire.vegas", 120, gameId: "solitaire"), Now));
        Assert.False(queue.Enqueue(Submission("chess.easy", 2, ScoreKind.Streak, "chess"), Now));

        Assert.Equal(2, queue.Count);
        Assert.Equal("chess", configuration.PendingScoreUploads[0].StatId);
        Assert.Equal("snake", configuration.PendingScoreUploads[1].StatId);
        Assert.True(queue.IsQueued("chess"));
        Assert.False(queue.IsQueued("chess.hard"));
        Assert.Equal("chess", ScoreStatIds.LeaderboardId("chess.medium", "chess", ScoreKind.Streak));
        Assert.Equal("sudoku.easy", ScoreStatIds.LeaderboardId("sudoku.easy", "sudoku", ScoreKind.Time));
        Assert.Equal(string.Empty, ScoreStatIds.LeaderboardId("solitaire.vegas", "solitaire", ScoreKind.Score));
        Assert.Equal(string.Empty, ScoreStatIds.LeaderboardId("doom", "doom", ScoreKind.Score));
    }

    [Fact]
    public void NextWaveStatsKeepTheMostStarsAndTheFewestStrokes()
    {
        var queue = Build(out var configuration);

        Assert.True(queue.Enqueue(Submission("fling", 12, ScoreKind.Level), Now));
        Assert.False(queue.Enqueue(Submission("fling", 9, ScoreKind.Level), Now));
        Assert.True(queue.Enqueue(Submission("fling", 15, ScoreKind.Level), Now));
        Assert.True(queue.Enqueue(Submission("minigolf", 58, ScoreKind.Time), Now));
        Assert.True(queue.Enqueue(Submission("minigolf", 52, ScoreKind.Time), Now));
        Assert.False(queue.Enqueue(Submission("minigolf", 60, ScoreKind.Time), Now));
        Assert.True(queue.Enqueue(Submission("crater.hard", 4, ScoreKind.Streak, "crater"), Now));
        Assert.False(queue.Enqueue(Submission("crater", 4), Now));
        Assert.True(queue.Enqueue(Submission("siege.endless", 40, ScoreKind.Level, "siege"), Now));

        Assert.Equal(4, queue.Count);
        Assert.Equal(15, configuration.PendingScoreUploads[0].Value);
        Assert.Equal(52, configuration.PendingScoreUploads[1].Value);
        Assert.Equal("crater", configuration.PendingScoreUploads[2].StatId);
        Assert.Equal("siege.endless", configuration.PendingScoreUploads[3].StatId);
        Assert.Equal("mahjong.easy", ScoreStatIds.LeaderboardId("mahjong.easy", "mahjong", ScoreKind.Time));
        Assert.Equal(string.Empty, ScoreStatIds.LeaderboardId("mahjong", "mahjong", ScoreKind.Time));
        Assert.Equal("gloop.versus", ScoreStatIds.LeaderboardId("gloop.versus", "gloop", ScoreKind.Streak));
    }

    [Fact]
    public void TheQueueKeepsOnlyTheBestPerStatId()
    {
        var queue = Build(out var configuration);

        Assert.True(queue.Enqueue(Submission("tetris", 100), Now));
        Assert.False(queue.Enqueue(Submission("tetris", 80), Now));
        Assert.True(queue.Enqueue(Submission("tetris", 120), Now + 1));
        Assert.True(queue.Enqueue(Submission("sudoku.easy", 90, ScoreKind.Time, "sudoku"), Now));
        Assert.False(queue.Enqueue(Submission("sudoku.easy", 120, ScoreKind.Time, "sudoku"), Now));
        Assert.True(queue.Enqueue(Submission("sudoku.easy", 60, ScoreKind.Time, "sudoku"), Now));
        Assert.True(queue.Enqueue(Submission("memory.attempts", 12, ScoreKind.Time, "memory"), Now));
        Assert.False(queue.Enqueue(Submission("memory.attempts", 15, ScoreKind.Time, "memory"), Now));

        Assert.Equal(3, queue.Count);
        Assert.Equal(120, configuration.PendingScoreUploads[0].Value);
        Assert.Equal(Now + 1, configuration.PendingScoreUploads[0].QueuedAtUnix);
        Assert.Equal(60, configuration.PendingScoreUploads[1].Value);
        Assert.Equal(12, configuration.PendingScoreUploads[2].Value);
        Assert.Equal(5, configuration.Saves);
    }

    [Fact]
    public void FlushFollowsQueueOrderAndSpacesTheSameGameFiveSecondsApart()
    {
        var queue = Build(out _);
        queue.Enqueue(Submission("tetris", 100), Now);
        queue.Enqueue(Submission("tetris.modern", 50, gameId: "tetris"), Now);
        queue.Enqueue(Submission("snake", 10), Now);

        Assert.True(queue.TryTakeDue(1000, out var first));
        Assert.Equal("tetris", first.StatId);
        Assert.Equal(ScoreUploadOutcome.Accepted, queue.Resolve(first, Reply(ScoreReasons.Accepted, 1, 1), AepFailure.None, 1000));

        Assert.True(queue.TryTakeDue(1000, out var second));
        Assert.Equal("snake", second.StatId);
        Assert.Equal(ScoreUploadOutcome.Accepted, queue.Resolve(second, Reply(ScoreReasons.Accepted, 1, 1), AepFailure.None, 1000));

        Assert.False(queue.TryTakeDue(1000 + ScoreUploadQueue.SpacingMilliseconds - 1, out _));
        Assert.Equal(1000 + ScoreUploadQueue.SpacingMilliseconds, queue.NextDueTick());
        Assert.True(queue.TryTakeDue(1000 + ScoreUploadQueue.SpacingMilliseconds, out var third));
        Assert.Equal("tetris.modern", third.StatId);
    }

    [Fact]
    public void TooSoonKeepsTheUploadAndWaitsOutTheCadence()
    {
        var queue = Build(out _);
        queue.Enqueue(Submission("snake", 10), Now);
        Assert.True(queue.TryTakeDue(1000, out var attempt));

        Assert.Equal(ScoreUploadOutcome.Retry, queue.Resolve(attempt, Reply(ScoreReasons.TooSoon), AepFailure.None, 1000));

        Assert.Equal(1, queue.Count);
        Assert.False(queue.TryTakeDue(1000 + ScoreUploadQueue.SpacingMilliseconds - 1, out _));
        Assert.True(queue.TryTakeDue(1000 + ScoreUploadQueue.SpacingMilliseconds, out _));
        Assert.Equal(RankState.Uploading, queue.RankFor("snake", true).State);
    }

    [Fact]
    public void AcceptedAndNotBetterRemoveTheUploadAndCacheTheServerRank()
    {
        var queue = Build(out _);
        queue.Enqueue(Submission("snake", 10), Now);
        queue.Enqueue(Submission("flap", 4), Now);
        Assert.True(queue.TryTakeDue(1000, out var snake));
        Assert.Equal(ScoreUploadOutcome.Accepted, queue.Resolve(snake, Reply(ScoreReasons.Accepted, 8, 1240, 2, 3), AepFailure.None, 1000));
        Assert.True(queue.TryTakeDue(1000, out var flap));
        Assert.Equal(ScoreUploadOutcome.Accepted, queue.Resolve(flap, Reply(ScoreReasons.NotBetter, 40, 500, 0, 0), AepFailure.None, 1000));

        Assert.Equal(0, queue.Count);
        var snakeRank = queue.RankFor("snake", true);
        Assert.Equal(RankState.Ranked, snakeRank.State);
        Assert.Equal(8, snakeRank.Rank);
        Assert.Equal(1240, snakeRank.Total);
        Assert.Equal(2, snakeRank.FriendsRank);
        Assert.Equal(3, snakeRank.WeekRank);
        var flapRank = queue.RankFor("flap", true);
        Assert.Equal(RankState.Ranked, flapRank.State);
        Assert.Equal(40, flapRank.Rank);
    }

    [Theory]
    [InlineData(ScoreReasons.UnknownGame)]
    [InlineData(ScoreReasons.Implausible)]
    [InlineData(ScoreReasons.Hidden)]
    public void RefusalsDropTheUploadAndReadAsFailed(string reason)
    {
        var queue = Build(out _);
        queue.Enqueue(Submission("snake", 10), Now);
        Assert.True(queue.TryTakeDue(1000, out var attempt));

        Assert.Equal(ScoreUploadOutcome.Dropped, queue.Resolve(attempt, Reply(reason), AepFailure.None, 1000));

        Assert.Equal(0, queue.Count);
        Assert.Equal(RankState.Failed, queue.RankFor("snake", true).State);
    }

    [Fact]
    public void OfflineKeepsTheUploadAndBacksOff() => AssertTransportFailureBacksOff(AepFailureKind.Offline);

    [Fact]
    public void TimeoutKeepsTheUploadAndBacksOff() => AssertTransportFailureBacksOff(AepFailureKind.Timeout);

    private static void AssertTransportFailureBacksOff(AepFailureKind kind)
    {
        var queue = Build(out _);
        queue.Enqueue(Submission("snake", 10), Now);
        Assert.True(queue.TryTakeDue(1000, out var attempt));

        Assert.Equal(ScoreUploadOutcome.Retry, queue.Resolve(attempt, null, AepFailure.Transport(kind), 1000));

        Assert.Equal(1, queue.Count);
        Assert.Equal(RankState.Failed, queue.RankFor("snake", true).State);
        Assert.False(queue.TryTakeDue(1000 + ScoreUploadQueue.RetryMilliseconds - 1, out _));
        Assert.True(queue.TryTakeDue(1000 + ScoreUploadQueue.RetryMilliseconds, out _));
        Assert.Equal(RankState.Uploading, queue.RankFor("snake", true).State);
    }

    [Fact]
    public void ServerErrorsRetryButClientErrorsDrop()
    {
        var queue = Build(out _);
        queue.Enqueue(Submission("snake", 10), Now);
        queue.Enqueue(Submission("flap", 4), Now);
        Assert.True(queue.TryTakeDue(1000, out var snake));
        Assert.Equal(ScoreUploadOutcome.Retry, queue.Resolve(snake, null, AepFailure.FromStatus(503, null), 1000));
        Assert.True(queue.TryTakeDue(1000, out var flap));
        Assert.Equal(ScoreUploadOutcome.Dropped, queue.Resolve(flap, null, AepFailure.FromStatus(400, null), 1000));

        Assert.Equal(1, queue.Count);
        Assert.True(queue.IsQueued("snake"));
        Assert.False(queue.IsQueued("flap"));
        Assert.Equal(RankState.Failed, queue.RankFor("flap", true).State);
    }

    [Fact]
    public void SignedOutKeepsTheUploadWithoutCallingItAFailure()
    {
        var queue = Build(out _);
        queue.Enqueue(Submission("snake", 10), Now);
        Assert.True(queue.TryTakeDue(1000, out var attempt));

        Assert.Equal(ScoreUploadOutcome.Retry,
            queue.Resolve(attempt, null, AepFailure.Transport(AepFailureKind.SignedOut), 1000));

        Assert.Equal(1, queue.Count);
        Assert.Equal(RankState.SignedOut, queue.RankFor("snake", false).State);
        Assert.Equal(RankState.Uploading, queue.RankFor("snake", true).State);
    }

    [Fact]
    public void ABetterScoreQueuedDuringTheUploadSurvivesTheReply()
    {
        var queue = Build(out var configuration);
        queue.Enqueue(Submission("tetris", 100), Now);
        Assert.True(queue.TryTakeDue(1000, out var attempt));
        Assert.True(queue.Enqueue(Submission("tetris", 150), Now + 1));

        Assert.Equal(ScoreUploadOutcome.Accepted, queue.Resolve(attempt, Reply(ScoreReasons.Accepted, 3, 10), AepFailure.None, 1000));

        Assert.Equal(1, queue.Count);
        Assert.Equal(150, configuration.PendingScoreUploads[0].Value);
        Assert.Equal(RankState.Uploading, queue.RankFor("tetris", true).State);
    }

    [Fact]
    public void RankStateWalksFromUnknownThroughUploadingToRanked()
    {
        var queue = Build(out _);
        Assert.Equal(RankState.Unknown, queue.RankFor("tetris", true).State);
        Assert.Equal(RankState.SignedOut, queue.RankFor("tetris", false).State);

        queue.Enqueue(Submission("tetris", 100), Now);
        Assert.Equal(RankState.Uploading, queue.RankFor("tetris", true).State);
        Assert.True(queue.TryTakeDue(1000, out var attempt));
        Assert.Equal(RankState.Uploading, queue.RankFor("tetris", true).State);
        queue.Resolve(attempt, Reply(ScoreReasons.Accepted, 12, 1240), AepFailure.None, 1000);

        var rank = queue.RankFor("tetris", true);
        Assert.Equal(RankState.Ranked, rank.State);
        Assert.True(rank.IsRanked);
        Assert.Equal(12, rank.Rank);
        Assert.Equal(RankState.SignedOut, queue.RankFor("tetris", false).State);

        queue.ForgetReplies();
        Assert.Equal(RankState.Unknown, queue.RankFor("tetris", true).State);
    }

    [Fact]
    public void PendingUploadsRoundTripThroughConfiguration()
    {
        var saved = new Configuration();
        saved.PendingScoreUploads.Add(new PendingScoreUpload
        {
            StatId = "sudoku.hard",
            GameId = "sudoku",
            Value = 240,
            Kind = ScoreKind.Time,
            Seed = 123456789UL,
            Daily = true,
            QueuedAtUnix = Now,
        });
        var json = string.Concat("{\"PendingScoreUploads\":", JsonConvert.SerializeObject(saved.PendingScoreUploads), "}");

        var loaded = JsonConvert.DeserializeObject<Configuration>(json);

        Assert.NotNull(loaded);
        var queue = new ScoreUploadQueue(loaded);
        Assert.Equal(1, queue.Count);
        Assert.True(queue.IsQueued("sudoku.hard"));
        Assert.True(queue.TryTakeDue(1000, out var attempt));
        Assert.Equal("sudoku.hard", attempt.StatId);
        Assert.Equal("sudoku", attempt.GameId);
        Assert.Equal(240, attempt.Value);
        var upload = loaded.PendingScoreUploads[0];
        Assert.Equal(ScoreKind.Time, upload.Kind);
        Assert.Equal(123456789UL, upload.Seed);
        Assert.True(upload.Daily);
        Assert.Equal(Now, upload.QueuedAtUnix);
    }
}
