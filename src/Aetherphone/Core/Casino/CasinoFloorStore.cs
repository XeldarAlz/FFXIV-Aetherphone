using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Casino;

internal sealed class CasinoFloorStore : IDisposable
{
    private const long MissionsRefreshMilliseconds = 45_000;
    private const long ChallengesRefreshMilliseconds = 60_000;
    private const long FameRefreshMilliseconds = 60_000;
    private const long FeedRefreshMilliseconds = 5_000;
    private const long RetryMilliseconds = 15_000;
    private const int FeedTabCount = 2;
    private const int FameSpanCount = 2;

    private readonly AethernetSession session;
    private readonly CasinoClient casino;
    private readonly CasinoStore chips;
    private readonly RealtimeSignalBus signals;
    private readonly StoreWork work = new("CasinoFloor");
    private readonly object watchGate = new();
    private readonly CasinoFameBoardDto?[] fame = new CasinoFameBoardDto?[CasinoFameBoards.All.Length * FameSpanCount];
    private readonly long[] fameLoadedAt = new long[CasinoFameBoards.All.Length * FameSpanCount];
    private readonly int[] fameFetching = new int[CasinoFameBoards.All.Length * FameSpanCount];
    private readonly CasinoFeedDto?[] feeds = new CasinoFeedDto?[FeedTabCount];
    private readonly long[] feedLoadedAt = new long[FeedTabCount];
    private readonly int[] feedFetching = new int[FeedTabCount];

    private volatile CasinoMissionsDto? missions;
    private volatile CasinoChallengesDto? challenges;
    private volatile CasinoFloorTickDto[] ticker = Array.Empty<CasinoFloorTickDto>();
    private volatile string claimingMission = string.Empty;
    private CasinoMissionClaimDto? missionClaim;
    private string completedMission = string.Empty;
    private long rainAmount;
    private int tickerVersion;
    private int missionsFetching;
    private int challengesFetching;
    private int claimFailed;
    private long missionsLoadedAt;
    private long challengesLoadedAt;
    private long challengesServerSkewSeconds;
    private bool watching;
    private int epoch;
    private string? lastAccountId;

    public CasinoFloorStore(AethernetSession session, CasinoClient casino, CasinoStore chips, RealtimeSignalBus signals)
    {
        this.session = session;
        this.casino = casino;
        this.chips = chips;
        this.signals = signals;
        session.Changed += OnSessionChanged;
        signals.CasinoReceived += OnCasinoSignal;
        signals.ConnectedChanged += OnRealtimeConnected;
    }

    public CasinoMissionsDto? Missions => missions;

    public CasinoChallengesDto? Challenges => challenges;

    public CasinoFloorTickDto[] Ticker => ticker;

    public int TickerVersion => Volatile.Read(ref tickerVersion);

    public string ClaimingMission => claimingMission;

    public long ServerNowUnix => DateTimeOffset.UtcNow.ToUnixTimeSeconds() + Interlocked.Read(ref challengesServerSkewSeconds);

    public static int FameSlot(string board, string span)
    {
        var boardIndex = Array.IndexOf(CasinoFameBoards.All, board);
        if (boardIndex < 0)
        {
            return -1;
        }

        return boardIndex * FameSpanCount + (string.Equals(span, CasinoFameSpans.Last, StringComparison.Ordinal) ? 1 : 0);
    }

    public static int FeedSlot(string tab) =>
        string.Equals(tab, CasinoFeedTabs.High, StringComparison.Ordinal) ? 1 : 0;

    public CasinoFameBoardDto? Fame(string board, string span)
    {
        var slot = FameSlot(board, span);
        return slot < 0 ? null : Volatile.Read(ref fame[slot]);
    }

    public CasinoFeedDto? Feed(string tab) => Volatile.Read(ref feeds[FeedSlot(tab)]);

    public CasinoMissionClaimDto? TakeMissionClaim() => Interlocked.Exchange(ref missionClaim, null);

    public bool TakeClaimFailure() => Interlocked.Exchange(ref claimFailed, 0) != 0;

    public string TakeCompletedMission() => Interlocked.Exchange(ref completedMission, string.Empty);

    public long TakeRain() => Interlocked.Exchange(ref rainAmount, 0);

    public CasinoChallengeDto? LiveChallenge()
    {
        var held = challenges?.Challenges;
        if (held is null)
        {
            return null;
        }

        for (var index = 0; index < held.Length; index++)
        {
            if (held[index].State == CasinoChallengeStates.Live)
            {
                return held[index];
            }
        }

        return null;
    }

    public void Watch()
    {
        if (!session.IsSignedIn || !chips.HasFeature(CasinoFeatures.Feed))
        {
            return;
        }

        lock (watchGate)
        {
            if (watching)
            {
                return;
            }

            watching = true;
            Send(SignalType.CasinoAttach);
        }
    }

    public void Unwatch()
    {
        lock (watchGate)
        {
            if (!watching)
            {
                return;
            }

            watching = false;
            Send(SignalType.CasinoDetach);
        }
    }

    public void EnsureFresh()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        if (chips.HasFeature(CasinoFeatures.Missions)
            && Due(Interlocked.Read(ref missionsLoadedAt), MissionsRefreshMilliseconds))
        {
            RefreshMissions();
        }

        if (chips.HasFeature(CasinoFeatures.Challenges)
            && Due(Interlocked.Read(ref challengesLoadedAt), ChallengesRefreshMilliseconds))
        {
            RefreshChallenges();
        }

        if (!watching && chips.HasFeature(CasinoFeatures.Feed))
        {
            Watch();
        }
    }

    public void RefreshMissionsNow()
    {
        Interlocked.Exchange(ref missionsLoadedAt, 0);
        if (chips.HasFeature(CasinoFeatures.Missions))
        {
            RefreshMissions();
        }
    }

    public void EnsureFame(string board, string span)
    {
        var slot = FameSlot(board, span);
        if (slot < 0 || !session.IsSignedIn || !chips.HasFeature(CasinoFeatures.Fame)
            || !Due(Interlocked.Read(ref fameLoadedAt[slot]), FameRefreshMilliseconds)
            || Interlocked.Exchange(ref fameFetching[slot], 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref fameLoadedAt[slot], Environment.TickCount64 - FameRefreshMilliseconds + RetryMilliseconds);
        work.Run("fame", async token =>
        {
            var answer = await casino.FameAsync(board, span, CasinoFloorRules.FameDefaultLimit, token)
                .ConfigureAwait(false);
            if (answer is null)
            {
                return;
            }

            Volatile.Write(ref fame[slot], answer);
            Interlocked.Exchange(ref fameLoadedAt[slot], Environment.TickCount64);
        }, () => Interlocked.Exchange(ref fameFetching[slot], 0));
    }

    public void EnsureFeed(string tab)
    {
        var slot = FeedSlot(tab);
        if (!session.IsSignedIn || !chips.HasFeature(CasinoFeatures.Feed)
            || !Due(Interlocked.Read(ref feedLoadedAt[slot]), FeedRefreshMilliseconds)
            || Interlocked.Exchange(ref feedFetching[slot], 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref feedLoadedAt[slot], Environment.TickCount64);
        work.Run("feed", async token =>
        {
            var feed = await casino.FeedAsync(tab, token).ConfigureAwait(false);
            if (feed is not null)
            {
                Volatile.Write(ref feeds[slot], feed);
            }
        }, () => Interlocked.Exchange(ref feedFetching[slot], 0));
    }

    public void ClaimMission(string missionId)
    {
        if (missionId.Length == 0 || claimingMission.Length > 0 || !session.IsSignedIn)
        {
            return;
        }

        claimingMission = missionId;
        var clientActionId = Guid.NewGuid().ToString("N");
        work.Run("claim mission", async token =>
        {
            var result = await casino.ClaimMissionAsync(missionId, clientActionId, token).ConfigureAwait(false);
            if (result is null)
            {
                Interlocked.Exchange(ref claimFailed, 1);
                return;
            }

            if (result.Granted)
            {
                var held = missions;
                if (held is not null)
                {
                    missions = Claimed(held, missionId);
                }

                chips.AbsorbGrant(result.Sitting, result.Stack);
            }

            Interlocked.Exchange(ref missionClaim, result);
            chips.RefreshNow();
        }, () => claimingMission = string.Empty);
    }

    internal static CasinoMissionsDto Claimed(CasinoMissionsDto held, string missionId)
    {
        var list = held.Missions;
        if (list is null)
        {
            return held;
        }

        var next = new CasinoMissionDto[list.Length];
        for (var index = 0; index < list.Length; index++)
        {
            next[index] = string.Equals(list[index].Id, missionId, StringComparison.Ordinal)
                ? list[index] with { Claimed = true, Complete = true, Progress = list[index].Target }
                : list[index];
        }

        return held with { Missions = next };
    }

    internal static string NewlyComplete(CasinoMissionsDto? held, CasinoMissionsDto fresh)
    {
        var list = fresh.Missions;
        if (held?.Missions is null || list is null || held.DayIndex != fresh.DayIndex)
        {
            return string.Empty;
        }

        for (var index = 0; index < list.Length; index++)
        {
            var mission = list[index];
            if (!mission.Complete || mission.Claimed || WasComplete(held.Missions, mission.Id))
            {
                continue;
            }

            return mission.Id;
        }

        return string.Empty;
    }

    private static bool WasComplete(CasinoMissionDto[] held, string missionId)
    {
        for (var index = 0; index < held.Length; index++)
        {
            if (string.Equals(held[index].Id, missionId, StringComparison.Ordinal))
            {
                return held[index].Complete;
            }
        }

        return false;
    }

    private static bool Due(long loadedAt, long interval)
    {
        return loadedAt == 0 || Environment.TickCount64 - loadedAt >= interval;
    }

    private void RefreshMissions()
    {
        if (Interlocked.Exchange(ref missionsFetching, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref missionsLoadedAt,
            Environment.TickCount64 - MissionsRefreshMilliseconds + RetryMilliseconds);
        work.Run("missions", async token =>
        {
            var fresh = await casino.MissionsAsync(token).ConfigureAwait(false);
            if (fresh is null)
            {
                return;
            }

            var done = NewlyComplete(missions, fresh);
            missions = fresh;
            Interlocked.Exchange(ref missionsLoadedAt, Environment.TickCount64);
            if (done.Length > 0)
            {
                Interlocked.Exchange(ref completedMission, done);
            }
        }, () => Interlocked.Exchange(ref missionsFetching, 0));
    }

    private void RefreshChallenges()
    {
        if (Interlocked.Exchange(ref challengesFetching, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref challengesLoadedAt,
            Environment.TickCount64 - ChallengesRefreshMilliseconds + RetryMilliseconds);
        work.Run("challenges", async token =>
        {
            var fresh = await casino.ChallengesAsync(token).ConfigureAwait(false);
            if (fresh is null)
            {
                return;
            }

            challenges = fresh;
            if (fresh.ServerNowUnix > 0)
            {
                Interlocked.Exchange(ref challengesServerSkewSeconds,
                    fresh.ServerNowUnix - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }

            Interlocked.Exchange(ref challengesLoadedAt, Environment.TickCount64);
        }, () => Interlocked.Exchange(ref challengesFetching, 0));
    }

    private void OnCasinoSignal(CasinoSignal signal)
    {
        var payload = signal.Payload;
        if (payload is null || !string.Equals(payload.RoomId, CasinoFloorRules.FloorRoomId, StringComparison.Ordinal))
        {
            return;
        }

        switch (signal.Type)
        {
            case SignalType.CasinoAttached:
            case SignalType.CasinoSnapshot:
                AbsorbSnapshot(payload);
                return;
            case SignalType.CasinoEvent:
                AbsorbTick(payload);
                return;
            case SignalType.CasinoPrivate:
                AbsorbRain(payload);
                return;
        }
    }

    private void AbsorbSnapshot(CasinoPayload payload)
    {
        Volatile.Write(ref epoch, payload.Epoch);
        var state = Parse(payload.Snapshot?.GameState, AethernetJsonContext.Default.CasinoFloorStateDto);
        ticker = CasinoFloorTicker.Seed(state?.Ticker, CasinoFloorRules.TickerLength);
        Interlocked.Increment(ref tickerVersion);
    }

    private void AbsorbTick(CasinoPayload payload)
    {
        if (!string.Equals(payload.EventKind, CasinoFloorRules.TickEvent, StringComparison.Ordinal))
        {
            return;
        }

        if (payload.Epoch != Volatile.Read(ref epoch))
        {
            Volatile.Write(ref epoch, payload.Epoch);
            Send(SignalType.CasinoResync);
        }

        var tick = Parse(payload.Event?.GameState, AethernetJsonContext.Default.CasinoFloorTickDto);
        if (tick is null)
        {
            return;
        }

        var next = CasinoFloorTicker.Prepend(ticker, tick, CasinoFloorRules.TickerLength);
        if (ReferenceEquals(next, ticker))
        {
            return;
        }

        ticker = next;
        Interlocked.Increment(ref tickerVersion);
    }

    private void AbsorbRain(CasinoPayload payload)
    {
        var personal = payload.Private;
        if (personal is null
            || !string.Equals(personal.EventKind, CasinoFloorRules.RainEvent, StringComparison.Ordinal))
        {
            return;
        }

        var rain = Parse(personal.Payload, AethernetJsonContext.Default.CasinoRainPrivateDto);
        if (rain is null || rain.Amount <= 0)
        {
            return;
        }

        Interlocked.Exchange(ref rainAmount, rain.Amount);
        chips.RefreshNow();
    }

    private static TValue? Parse<TValue>(string? json,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TValue> typeInfo)
        where TValue : class
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(json, typeInfo);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void OnRealtimeConnected(bool connected)
    {
        if (!connected)
        {
            return;
        }

        lock (watchGate)
        {
            if (watching)
            {
                Send(SignalType.CasinoAttach);
            }
        }
    }

    private void Send(string type)
    {
        signals.TrySend(new CallControl
        {
            Type = type,
            Casino = new CasinoPayload { RoomId = CasinoFloorRules.FloorRoomId },
        });
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        missions = null;
        challenges = null;
        ticker = Array.Empty<CasinoFloorTickDto>();
        Interlocked.Increment(ref tickerVersion);
        Interlocked.Exchange(ref missionsLoadedAt, 0);
        Interlocked.Exchange(ref challengesLoadedAt, 0);
        Interlocked.Exchange(ref missionClaim, null);
        Interlocked.Exchange(ref completedMission, string.Empty);
        Interlocked.Exchange(ref rainAmount, 0);
        for (var index = 0; index < fame.Length; index++)
        {
            Volatile.Write(ref fame[index], null);
            Interlocked.Exchange(ref fameLoadedAt[index], 0);
        }

        for (var index = 0; index < feeds.Length; index++)
        {
            Volatile.Write(ref feeds[index], null);
            Interlocked.Exchange(ref feedLoadedAt[index], 0);
        }

        lock (watchGate)
        {
            watching = false;
        }
    }

    public void Dispose()
    {
        Unwatch();
        session.Changed -= OnSessionChanged;
        signals.CasinoReceived -= OnCasinoSignal;
        signals.ConnectedChanged -= OnRealtimeConnected;
        work.Dispose();
    }
}
