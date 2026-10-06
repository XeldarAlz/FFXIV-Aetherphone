using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Games;

internal enum ScoreUploadOutcome : byte
{
    Accepted,
    Retry,
    Dropped,
}

internal readonly struct ScoreUploadAttempt
{
    public readonly string StatId;
    public readonly string GameId;
    public readonly int Value;

    public ScoreUploadAttempt(string statId, string gameId, int value)
    {
        StatId = statId;
        GameId = gameId;
        Value = value;
    }
}

internal sealed class ScoreUploadQueue
{
    public const long SpacingMilliseconds = 5_000;
    public const long RetryMilliseconds = 30_000;
    private const int ServerErrorFloor = 500;

    private readonly IScoreUploadConfiguration configuration;
    private readonly Dictionary<string, GameRank> replies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> gameReadyAtTick = new(StringComparer.Ordinal);
    private readonly HashSet<string> failed = new(StringComparer.Ordinal);
    private string attemptingStatId = string.Empty;

    public ScoreUploadQueue(IScoreUploadConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public int Count => configuration.PendingScoreUploads.Count;

    public bool Enqueue(in ScoreSubmission submission, long nowUnix)
    {
        if (!ScoreStatIds.TryFind(submission.StatId, out var stat) || submission.Value <= 0)
        {
            return false;
        }

        if ((submission.Kind == ScoreKind.Streak) != (stat.Kind == ScoreKind.Streak))
        {
            return false;
        }

        var pending = configuration.PendingScoreUploads;
        for (var index = 0; index < pending.Count; index++)
        {
            var existing = pending[index];
            if (!string.Equals(existing.StatId, submission.StatId, StringComparison.Ordinal))
            {
                continue;
            }

            if (!stat.IsBetter(submission.Value, existing.Value))
            {
                return false;
            }

            existing.Value = submission.Value;
            existing.Kind = submission.Kind;
            existing.Seed = submission.Seed;
            existing.Daily = submission.Daily;
            existing.QueuedAtUnix = nowUnix;
            failed.Remove(submission.StatId);
            configuration.Save();
            return true;
        }

        pending.Add(new PendingScoreUpload
        {
            StatId = submission.StatId,
            GameId = submission.GameId,
            Value = submission.Value,
            Kind = submission.Kind,
            Seed = submission.Seed,
            Daily = submission.Daily,
            QueuedAtUnix = nowUnix,
        });
        failed.Remove(submission.StatId);
        configuration.Save();
        return true;
    }

    public bool IsQueued(string statId)
    {
        var pending = configuration.PendingScoreUploads;
        for (var index = 0; index < pending.Count; index++)
        {
            if (string.Equals(pending[index].StatId, statId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public bool TryTakeDue(long nowTick, out ScoreUploadAttempt attempt)
    {
        var pending = configuration.PendingScoreUploads;
        for (var index = 0; index < pending.Count; index++)
        {
            var upload = pending[index];
            if (nowTick < ReadyTick(upload.GameId))
            {
                continue;
            }

            attemptingStatId = upload.StatId;
            failed.Remove(upload.StatId);
            attempt = new ScoreUploadAttempt(upload.StatId, upload.GameId, upload.Value);
            return true;
        }

        attempt = default;
        return false;
    }

    public long NextDueTick()
    {
        var pending = configuration.PendingScoreUploads;
        var next = long.MaxValue;
        for (var index = 0; index < pending.Count; index++)
        {
            next = Math.Min(next, ReadyTick(pending[index].GameId));
        }

        return next;
    }

    public ScoreUploadOutcome Resolve(in ScoreUploadAttempt attempt, GameScoreSubmitDto? reply,
        in AepFailure failure, long nowTick)
    {
        attemptingStatId = string.Empty;
        gameReadyAtTick[attempt.GameId] = nowTick + SpacingMilliseconds;
        if (reply is null)
        {
            return ResolveFailure(attempt, failure, nowTick);
        }

        switch (reply.Reason)
        {
            case ScoreReasons.Accepted:
            case ScoreReasons.NotBetter:
                Remove(attempt);
                if (reply.Rank > 0)
                {
                    replies[attempt.StatId] = new GameRank(reply.Rank, reply.Total, reply.FriendsRank,
                        reply.WeekRank, RankState.Ranked);
                }
                else
                {
                    replies.Remove(attempt.StatId);
                }

                return ScoreUploadOutcome.Accepted;
            case ScoreReasons.TooSoon:
                return ScoreUploadOutcome.Retry;
            default:
                Remove(attempt);
                failed.Add(attempt.StatId);
                return ScoreUploadOutcome.Dropped;
        }
    }

    public GameRank RankFor(string statId, bool signedIn)
    {
        if (!signedIn)
        {
            return new GameRank(0, 0, 0, 0, RankState.SignedOut);
        }

        if (string.Equals(attemptingStatId, statId, StringComparison.Ordinal))
        {
            return new GameRank(0, 0, 0, 0, RankState.Uploading);
        }

        if (failed.Contains(statId))
        {
            return new GameRank(0, 0, 0, 0, RankState.Failed);
        }

        if (IsQueued(statId))
        {
            return new GameRank(0, 0, 0, 0, RankState.Uploading);
        }

        return replies.TryGetValue(statId, out var rank) ? rank : GameRank.Unknown;
    }

    public void ForgetReplies()
    {
        replies.Clear();
        failed.Clear();
        gameReadyAtTick.Clear();
        attemptingStatId = string.Empty;
    }

    private ScoreUploadOutcome ResolveFailure(in ScoreUploadAttempt attempt, in AepFailure failure, long nowTick)
    {
        if (failure.Kind == AepFailureKind.Server && failure.StatusCode < ServerErrorFloor)
        {
            Remove(attempt);
            failed.Add(attempt.StatId);
            return ScoreUploadOutcome.Dropped;
        }

        gameReadyAtTick[attempt.GameId] = nowTick + RetryMilliseconds;
        if (failure.Kind != AepFailureKind.SignedOut)
        {
            failed.Add(attempt.StatId);
        }

        return ScoreUploadOutcome.Retry;
    }

    private long ReadyTick(string gameId) => gameReadyAtTick.TryGetValue(gameId, out var tick) ? tick : 0L;

    private void Remove(in ScoreUploadAttempt attempt)
    {
        var pending = configuration.PendingScoreUploads;
        for (var index = 0; index < pending.Count; index++)
        {
            var upload = pending[index];
            if (!string.Equals(upload.StatId, attempt.StatId, StringComparison.Ordinal))
            {
                continue;
            }

            if (upload.Value != attempt.Value)
            {
                return;
            }

            pending.RemoveAt(index);
            configuration.Save();
            return;
        }
    }
}
