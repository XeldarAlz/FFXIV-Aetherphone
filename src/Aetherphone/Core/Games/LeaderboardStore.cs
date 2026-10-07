using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Games;

internal sealed class LeaderboardStore : IScoreSink, IRankSource, IDisposable
{
    private const long BoardTtlMilliseconds = 60_000;
    private const long MyRanksTtlMilliseconds = 60_000;
    private const long RetryAfterAttemptMilliseconds = 30_000;
    private const long MinimumWaitMilliseconds = 250;
    private const long MaximumWaitMilliseconds = 30_000;
    private const int BoardLimit = ScoresClient.DefaultLimit;

    private readonly IScoreUploadConfiguration configuration;
    private readonly AethernetSession session;
    private readonly ScoresClient scores;
    private readonly RealtimeSignalBus signals;
    private readonly ScoreUploadQueue queue;
    private readonly LeaderboardConsent consent;
    private readonly LeaderboardParticipation participation = new();
    private readonly StoreWork work = new("Leaderboard");
    private readonly object gate = new();
    private readonly Dictionary<LeaderboardKey, LeaderboardBoard> boards = new();
    private readonly List<LeaderboardKey> staleKeys = new();

    private volatile GameScoreRankDto[] myRanks = Array.Empty<GameScoreRankDto>();
    private volatile bool myRanksLoaded;
    private volatile bool loadingMyRanks;
    private volatile bool savingParticipation;
    private volatile bool requestedParticipation;
    private AepFailure participationFailure;
    private int flushing;
    private int fetchingMyRanks;
    private int participationInFlight;
    private int version;
    private long myRanksLoadedAtTick;
    private long myRanksAttemptedAtTick;
    private string? lastAccountId;

    public LeaderboardStore(IScoreUploadConfiguration configuration, AethernetSession session, ScoresClient scores,
        RealtimeSignalBus signals)
    {
        this.configuration = configuration;
        this.session = session;
        this.scores = scores;
        this.signals = signals;
        queue = new ScoreUploadQueue(configuration);
        consent = new LeaderboardConsent(configuration);
        lastAccountId = session.CurrentUser?.Id;
        participation.Observe(ObservedAccountId(), OptedIn);
        session.Changed += OnSessionChanged;
        signals.ConnectedChanged += OnRealtimeConnected;
        Flush();
    }

    public int Version => Volatile.Read(ref version);

    public bool IsSignedIn => session.IsSignedIn;

    public string AccountId => session.CurrentUser?.Id ?? string.Empty;

    public UserDto? CurrentUser => session.CurrentUser;

    public GameScoreRankDto[] MyRanks => myRanks;

    public bool MyRanksLoaded => myRanksLoaded;

    public bool LoadingMyRanks => loadingMyRanks;

    public bool OptedIn => UploadsAllowed(session.IsSignedIn, session.CurrentUser);

    public bool OptedOut => session.IsSignedIn && session.CurrentUser is { ShowOnLeaderboards: false };

    public bool NeedsConsent => consent.Needed(session.IsSignedIn, session.CurrentUser);

    public bool SavingParticipation => savingParticipation;

    public bool ShownParticipation => savingParticipation ? requestedParticipation : OptedIn;

    public AepFailure ParticipationFailure
    {
        get
        {
            lock (gate)
            {
                return participationFailure;
            }
        }
    }

    internal static bool UploadsAllowed(bool signedIn, UserDto? user) =>
        signedIn && user is { ShowOnLeaderboards: true };

    public void SetParticipation(bool show)
    {
        if (session.CurrentUser is null || !session.IsSignedIn)
        {
            return;
        }

        if (Interlocked.Exchange(ref participationInFlight, 1) != 0)
        {
            return;
        }

        requestedParticipation = show;
        savingParticipation = true;
        SetParticipationFailure(AepFailure.None);
        var failure = AepFailure.None;
        work.Run("participation", async token =>
        {
            var reply = await scores.SetShowOnLeaderboardsAsync(show, token, reported => failure = reported)
                .ConfigureAwait(false);
            if (reply is null)
            {
                return false;
            }

            consent.Answer(reply.Id);
            session.SetUser(reply);
            return true;
        }, succeeded =>
        {
            Interlocked.Exchange(ref participationInFlight, 0);
            if (!succeeded)
            {
                SetParticipationFailure(failure.Failed ? failure : AepFailure.Transport(AepFailureKind.BadResponse));
            }

            if (!succeeded || session.CurrentUser?.ShowOnLeaderboards == requestedParticipation)
            {
                savingParticipation = false;
            }

            Bump();
        });
    }

    public void DeclineConsent()
    {
        var user = session.CurrentUser;
        if (user is null)
        {
            return;
        }

        consent.Answer(user.Id);
        SetParticipationFailure(AepFailure.None);
    }

    public void ClearParticipationFailure() => SetParticipationFailure(AepFailure.None);

    public void Submit(in ScoreSubmission submission)
    {
        bool queued;
        lock (gate)
        {
            queued = queue.Enqueue(submission, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        if (!queued)
        {
            return;
        }

        Bump();
        Flush();
    }

    public bool TryGetRank(string statId, out GameRank rank)
    {
        lock (gate)
        {
            rank = queue.RankFor(statId, session.IsSignedIn, OptedOut);
        }

        if (rank.State != RankState.Unknown)
        {
            return true;
        }

        var ranks = myRanks;
        for (var index = 0; index < ranks.Length; index++)
        {
            var entry = ranks[index];
            if (entry.Rank <= 0 || !string.Equals(entry.GameId, statId, StringComparison.Ordinal))
            {
                continue;
            }

            rank = new GameRank(entry.Rank, entry.Total, 0, entry.WeekRank, RankState.Ranked);
            return true;
        }

        return false;
    }

    public void EnsureMyRanksFresh() => FetchMyRanks(MyRanksTtlMilliseconds);

    public void RefreshMyRanksNow() => FetchMyRanks(0);

    public LeaderboardBoard Board(in LeaderboardKey key)
    {
        lock (gate)
        {
            return boards.TryGetValue(key, out var board) ? board : LeaderboardBoard.Empty;
        }
    }

    public void EnsureFresh(in LeaderboardKey key) => FetchBoard(key, BoardTtlMilliseconds);

    public void RefreshNow(in LeaderboardKey key) => FetchBoard(key, 0);

    internal static bool CoolingDown(long attemptedAtTick, long nowTick)
    {
        return attemptedAtTick != 0 && nowTick - attemptedAtTick < RetryAfterAttemptMilliseconds;
    }

    private void FetchMyRanks(long ttlMilliseconds)
    {
        if (!session.IsSignedIn || OptedOut)
        {
            return;
        }

        var now = Environment.TickCount64;
        var loadedAt = Interlocked.Read(ref myRanksLoadedAtTick);
        if (ttlMilliseconds > 0 && loadedAt != 0 && now - loadedAt < ttlMilliseconds)
        {
            return;
        }

        if (ttlMilliseconds > 0 && CoolingDown(Interlocked.Read(ref myRanksAttemptedAtTick), now))
        {
            return;
        }

        if (Interlocked.Exchange(ref fetchingMyRanks, 1) != 0)
        {
            return;
        }

        loadingMyRanks = true;
        Interlocked.Exchange(ref myRanksAttemptedAtTick, now);
        work.Run("my ranks", async token =>
        {
            var reply = await scores.MyRanksAsync(token).ConfigureAwait(false);
            if (reply is null)
            {
                return;
            }

            myRanks = reply.Ranks ?? Array.Empty<GameScoreRankDto>();
            myRanksLoaded = true;
            Interlocked.Exchange(ref myRanksAttemptedAtTick, 0);
            Interlocked.Exchange(ref myRanksLoadedAtTick, Environment.TickCount64);
            Bump();
        }, () =>
        {
            loadingMyRanks = false;
            Interlocked.Exchange(ref fetchingMyRanks, 0);
        });
    }

    private void FetchBoard(LeaderboardKey key, long ttlMilliseconds)
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        var now = Environment.TickCount64;
        lock (gate)
        {
            if (!boards.TryGetValue(key, out var current))
            {
                current = LeaderboardBoard.Empty;
            }

            if (current.Loading)
            {
                return;
            }

            if (ttlMilliseconds > 0 && current.LoadedAtTick != 0 && now - current.LoadedAtTick < ttlMilliseconds)
            {
                return;
            }

            if (ttlMilliseconds > 0 && CoolingDown(current.AttemptedAtTick, now))
            {
                return;
            }

            boards[key] = new LeaderboardBoard(current.Data, true, AepFailure.None, current.LoadedAtTick, now);
        }

        Bump();
        work.Run("board", async token =>
        {
            var failure = AepFailure.None;
            var dto = await scores.BoardAsync(key.StatId, ScopeName(key.Scope), SpanName(key.Span), BoardLimit,
                token, reported => failure = reported).ConfigureAwait(false);
            lock (gate)
            {
                var previous = boards[key];
                boards[key] = dto is null
                    ? new LeaderboardBoard(previous.Data, false,
                        failure.Failed ? failure : AepFailure.Transport(AepFailureKind.BadResponse),
                        previous.LoadedAtTick, previous.AttemptedAtTick)
                    : new LeaderboardBoard(dto, false, AepFailure.None, Environment.TickCount64, 0);
            }
        }, () =>
        {
            SettleBoard(key);
            Bump();
        });
    }

    private void SettleBoard(LeaderboardKey key)
    {
        lock (gate)
        {
            if (!boards.TryGetValue(key, out var board) || !board.Loading)
            {
                return;
            }

            boards[key] = new LeaderboardBoard(board.Data, false, AepFailure.Transport(AepFailureKind.BadResponse),
                board.LoadedAtTick, board.AttemptedAtTick);
        }
    }

    private void InvalidateBoards(string statId)
    {
        lock (gate)
        {
            staleKeys.Clear();
            foreach (var pair in boards)
            {
                if (string.Equals(pair.Key.StatId, statId, StringComparison.Ordinal) && !pair.Value.Loading)
                {
                    staleKeys.Add(pair.Key);
                }
            }

            for (var index = 0; index < staleKeys.Count; index++)
            {
                var board = boards[staleKeys[index]];
                boards[staleKeys[index]] = new LeaderboardBoard(board.Data, false, board.Failure, 0, 0);
            }
        }
    }

    private void Flush()
    {
        if (!OptedIn)
        {
            return;
        }

        lock (gate)
        {
            if (queue.Count == 0)
            {
                return;
            }
        }

        if (Interlocked.Exchange(ref flushing, 1) != 0)
        {
            return;
        }

        work.Run("flush", FlushAsync, () => Interlocked.Exchange(ref flushing, 0));
    }

    private async Task FlushAsync(CancellationToken token)
    {
        while (OptedIn)
        {
            ScoreUploadAttempt attempt;
            long waitMilliseconds;
            lock (gate)
            {
                if (queue.Count == 0)
                {
                    return;
                }

                var now = Environment.TickCount64;
                if (queue.TryTakeDue(now, out attempt))
                {
                    waitMilliseconds = 0;
                }
                else
                {
                    waitMilliseconds = Math.Clamp(queue.NextDueTick() - now, MinimumWaitMilliseconds,
                        MaximumWaitMilliseconds);
                }
            }

            if (waitMilliseconds > 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(waitMilliseconds), token).ConfigureAwait(false);
                continue;
            }

            Bump();
            var failure = AepFailure.None;
            var reply = await scores.SubmitAsync(attempt.StatId, attempt.Value, null, token,
                reported => failure = reported).ConfigureAwait(false);
            ScoreUploadOutcome outcome;
            lock (gate)
            {
                outcome = queue.Resolve(attempt, reply, failure, Environment.TickCount64);
            }

            Bump();
            if (outcome == ScoreUploadOutcome.Accepted)
            {
                InvalidateBoards(attempt.StatId);
                RefreshMyRanksNow();
            }

            if (failure.Kind == AepFailureKind.SignedOut)
            {
                return;
            }
        }
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (!string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            lastAccountId = accountId;
            ForgetAccountState();
            SetParticipationFailure(AepFailure.None);
        }

        switch (participation.Observe(ObservedAccountId(), OptedIn))
        {
            case ParticipationChange.Joined:
                lock (gate)
                {
                    queue.EnqueueLocalBests(configuration.GameStats, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                }

                break;
            case ParticipationChange.Left:
                lock (gate)
                {
                    queue.Clear();
                }

                ForgetAccountState();
                break;
            default:
                break;
        }

        if (Volatile.Read(ref participationInFlight) == 0)
        {
            savingParticipation = false;
        }

        Bump();
        Flush();
    }

    private void ForgetAccountState()
    {
        lock (gate)
        {
            queue.ForgetReplies();
            boards.Clear();
        }

        myRanks = Array.Empty<GameScoreRankDto>();
        myRanksLoaded = false;
        Interlocked.Exchange(ref myRanksLoadedAtTick, 0);
        Interlocked.Exchange(ref myRanksAttemptedAtTick, 0);
    }

    private string? ObservedAccountId() => session.IsSignedIn ? session.CurrentUser?.Id : null;

    private void SetParticipationFailure(in AepFailure failure)
    {
        lock (gate)
        {
            participationFailure = failure;
        }

        Bump();
    }

    private void OnRealtimeConnected(bool connected)
    {
        if (connected)
        {
            Flush();
        }
    }

    private void Bump()
    {
        Interlocked.Increment(ref version);
    }

    private static string ScopeName(LeaderboardScope scope) =>
        scope == LeaderboardScope.Friends ? ScoresClient.ScopeFriends : ScoresClient.ScopeGlobal;

    private static string SpanName(LeaderboardSpan span) =>
        span == LeaderboardSpan.Week ? ScoresClient.SpanWeek : ScoresClient.SpanAll;

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        signals.ConnectedChanged -= OnRealtimeConnected;
        work.Dispose();
    }
}
