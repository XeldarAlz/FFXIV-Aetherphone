using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Runtime;
using Aetherphone.Core.Telephony.Contracts;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Casino;

internal sealed record CasinoStakeOutcome(bool Granted, string Reason, long Ceiling = 0);

internal sealed class CasinoRoomsStore : IDisposable
{
    private static readonly TimeSpan ForegroundPollInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan BackgroundPollInterval = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan ForegroundRoomPollInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan BackgroundRoomPollInterval = TimeSpan.FromSeconds(10);
    private const long RetryAfterAttemptMilliseconds = 30_000;
    private const int NotFoundStatus = 404;

    private const int HandBetSpot = -2;

    private readonly AethernetSession session;
    private readonly CasinoClient casino;
    private readonly CasinoStore chips;
    private readonly RealtimeSignalBus signals;
    private readonly PollCadence directoryCadence;
    private readonly PollCadence roomCadence;
    private readonly CasinoRoomSession room;
    private readonly StoreWork work = new("CasinoRooms");
    private readonly object stakeGate = new();
    private readonly Action<int> roomStatusSink;

    private volatile CasinoRoomListItemDto[] rooms = Array.Empty<CasinoRoomListItemDto>();
    private volatile CasinoWheelBetsDto? wheelBets;
    private volatile CasinoBingoCardsDto? bingoCards;
    private volatile CasinoRaceBetsDto? raceBets;
    private volatile bool loadingRooms;
    private volatile bool loadedRooms;
    private volatile bool stakeInFlight;
    private CasinoStakeOutcome? stakeResult;
    private string chipRoundId = string.Empty;
    private long chipRoundIndex = -1;
    private string unansweredBetId = string.Empty;
    private int unansweredBetSpot = -1;
    private long unansweredBetAmount;
    private long unansweredBetRoundIndex = -1;
    private long unansweredBetPairs;
    private long unansweredBetThree;
    private string unansweredPurchaseId = string.Empty;
    private long unansweredPurchaseRoundIndex = -1;
    private int unansweredPurchaseCardCount = -1;
    private string unansweredActionId = string.Empty;
    private string unansweredActionHandId = string.Empty;
    private int unansweredActionValue;
    private long unansweredActionSeq = -1;
    private long personalRoundIndex = -1;
    private int personalPhase = -1;
    private long personalAskedRoundIndex = -1;
    private int personalAskedPhase = -1;
    private int personalGeneration;
    private int roomsFailed;
    private int stakeFailed;
    private int fetchingRooms;
    private int fetchingRoomState;
    private int fetchingPersonal;
    private int roomGoneStatus;
    private long roomsAttemptedAtTick;
    private long roomAttemptedAtTick;
    private long personalAttemptedAtTick;
    private string? lastAccountId;

    public CasinoRoomsStore(AethernetSession session, CasinoClient casino, CasinoStore chips,
        PhoneVisibility visibility, RealtimeSignalBus signals)
    {
        this.session = session;
        this.casino = casino;
        this.chips = chips;
        this.signals = signals;
        directoryCadence = new PollCadence(visibility, ForegroundPollInterval, BackgroundPollInterval);
        roomCadence = new PollCadence(visibility, ForegroundRoomPollInterval, BackgroundRoomPollInterval);
        room = new CasinoRoomSession(signals);
        roomStatusSink = OnRoomStatus;
        session.Changed += OnSessionChanged;
        signals.CasinoReceived += OnCasinoSignal;
        signals.ConnectedChanged += OnRealtimeConnected;
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    public CasinoRoomSession Room => room;

    public string AccountId => session.CurrentUser?.Id ?? string.Empty;

    public CasinoRoomListItemDto[] Rooms => rooms;

    public bool LoadingRooms => loadingRooms;

    public bool LoadedRooms => loadedRooms;

    public bool StakeInFlight => stakeInFlight;

    public CasinoWheelBetsDto? WheelBets => wheelBets;

    public CasinoBingoCardsDto? BingoCards => bingoCards;

    public bool TakeRoomsFailure()
    {
        return Interlocked.Exchange(ref roomsFailed, 0) != 0;
    }

    public CasinoStakeOutcome? TakeStakeResult()
    {
        return Interlocked.Exchange(ref stakeResult, null);
    }

    public bool TakeStakeFailure()
    {
        return Interlocked.Exchange(ref stakeFailed, 0) != 0;
    }

    public CasinoWheelBetsDto? WheelBetsFor(string roomId, long roundIndex)
    {
        var held = wheelBets;
        if (held is null || held.RoundIndex != roundIndex
            || !string.Equals(held.RoomId, roomId, StringComparison.Ordinal))
        {
            return null;
        }

        return held;
    }

    public CasinoBingoCardsDto? BingoCardsFor(string roomId, long roundIndex)
    {
        var held = bingoCards;
        if (held is null || !held.Granted || held.RoundIndex != roundIndex
            || !string.Equals(held.RoomId, roomId, StringComparison.Ordinal))
        {
            return null;
        }

        return held;
    }

    public CasinoRaceBetsDto? RaceBetsFor(string roomId, long roundIndex)
    {
        var held = raceBets;
        if (held is null || held.RoundIndex != roundIndex
            || !string.Equals(held.RoomId, roomId, StringComparison.Ordinal))
        {
            return null;
        }

        return held;
    }

    public void PlaceRaceBet(int kind, int runner, int runnerB, long amount)
    {
        var roomId = room.RoomId;
        var snapshot = room.State?.Snapshot;
        if (stakeInFlight || !session.IsSignedIn || roomId.Length == 0 || snapshot is null)
        {
            return;
        }

        if (!string.Equals(snapshot.GameKind, CasinoWire.RaceKind, StringComparison.Ordinal)
            || !RaceRules.IsTicket(kind, runner, runnerB) || amount < RaceRules.MinBet)
        {
            return;
        }

        var roundIndex = snapshot.RoundIndex;
        var sittingId = chips.State?.Sitting?.Id ?? string.Empty;
        var ticketKey = RaceRules.TicketKey(kind, runner, runnerB);
        string betId;
        string clientRoundId;
        lock (stakeGate)
        {
            clientRoundId = ChipRoundFor(roundIndex);
            betId = ReusableBetId(roundIndex, ticketKey, amount);
            if (betId.Length == 0)
            {
                betId = Guid.NewGuid().ToString("N");
            }

            unansweredBetId = betId;
            unansweredBetSpot = ticketKey;
            unansweredBetAmount = amount;
            unansweredBetRoundIndex = roundIndex;
        }

        var request = new CasinoRaceBetRequest(roomId, roundIndex, clientRoundId, betId, kind, runner, runnerB,
            amount);
        stakeInFlight = true;
        work.Run("race bet", async token =>
        {
            var result = await casino.PlaceRaceBetAsync(request, token).ConfigureAwait(false);
            if (result is null)
            {
                Interlocked.Exchange(ref stakeFailed, 1);
                return;
            }

            ForgetUnansweredBet(betId);
            Interlocked.Exchange(ref stakeResult, new CasinoStakeOutcome(result.Granted, result.Reason));
            if (!result.Granted)
            {
                chips.RefreshNow();
                InvalidatePersonal();
                return;
            }

            if (sittingId.Length > 0)
            {
                chips.AbsorbStack(sittingId, result.Stack);
            }

            InvalidatePersonal();
        }, () => stakeInFlight = false);
    }

    public int OccupancyOf(string roomId)
    {
        var current = room.State?.Snapshot;
        if (current is not null && string.Equals(current.RoomId, roomId, StringComparison.Ordinal))
        {
            return current.Occupancy;
        }

        var directory = rooms;
        for (var index = 0; index < directory.Length; index++)
        {
            if (string.Equals(directory[index].RoomId, roomId, StringComparison.Ordinal))
            {
                return directory[index].Occupancy;
            }
        }

        return 0;
    }

    public bool TryRoomClock(string roomId, out int phase, out long phaseEndsAtUnixMs)
    {
        var current = room.State?.Snapshot;
        if (current is not null && string.Equals(current.RoomId, roomId, StringComparison.Ordinal))
        {
            phase = current.Phase;
            phaseEndsAtUnixMs = current.PhaseEndsAtUnixMs;
            return true;
        }

        var directory = rooms;
        for (var index = 0; index < directory.Length; index++)
        {
            if (!string.Equals(directory[index].RoomId, roomId, StringComparison.Ordinal))
            {
                continue;
            }

            phase = directory[index].Phase;
            phaseEndsAtUnixMs = directory[index].PhaseEndsAtUnixMs;
            return true;
        }

        phase = CasinoRoomPhases.Open;
        phaseEndsAtUnixMs = 0;
        return false;
    }

    public void PlaceWheelBet(int spot, long amount)
    {
        var roomId = room.RoomId;
        var snapshot = room.State?.Snapshot;
        if (stakeInFlight || !session.IsSignedIn || roomId.Length == 0 || snapshot is null)
        {
            return;
        }

        if (!string.Equals(snapshot.GameKind, CasinoWire.WheelKind, StringComparison.Ordinal)
            || !WheelRules.IsSpot(spot) || !WheelRules.IsStakeInRange(amount))
        {
            return;
        }

        var roundIndex = snapshot.RoundIndex;
        var sittingId = chips.State?.Sitting?.Id ?? string.Empty;
        string betId;
        string clientRoundId;
        lock (stakeGate)
        {
            clientRoundId = ChipRoundFor(roundIndex);
            betId = ReusableBetId(roundIndex, spot, amount);
            if (betId.Length == 0)
            {
                betId = Guid.NewGuid().ToString("N");
            }

            unansweredBetId = betId;
            unansweredBetSpot = spot;
            unansweredBetAmount = amount;
            unansweredBetRoundIndex = roundIndex;
        }

        stakeInFlight = true;
        work.Run("wheel bet", async token =>
        {
            var result = await casino
                .PlaceWheelBetAsync(roomId, roundIndex, clientRoundId, betId, spot, amount, token)
                .ConfigureAwait(false);
            if (result is null)
            {
                Interlocked.Exchange(ref stakeFailed, 1);
                return;
            }

            ForgetUnansweredBet(betId);
            Interlocked.Exchange(ref stakeResult,
                new CasinoStakeOutcome(result.Granted, result.Reason, result.Ceiling));
            if (!result.Granted)
            {
                chips.AbsorbCeiling(result.Ceiling);
                chips.RefreshNow();
                InvalidatePersonal();
                return;
            }

            if (sittingId.Length > 0)
            {
                chips.AbsorbStack(sittingId, result.Stack);
            }

            InvalidatePersonal();
        }, () => stakeInFlight = false);
    }

    public void BuyBingoCards(int cardCount)
    {
        var roomId = room.RoomId;
        var snapshot = room.State?.Snapshot;
        if (stakeInFlight || !session.IsSignedIn || roomId.Length == 0 || snapshot is null)
        {
            return;
        }

        if (!string.Equals(snapshot.GameKind, CasinoWire.BingoKind, StringComparison.Ordinal)
            || !BingoRules.IsValidCardCount(cardCount))
        {
            return;
        }

        var roundIndex = snapshot.RoundIndex;
        var sittingId = chips.State?.Sitting?.Id ?? string.Empty;
        string purchaseId;
        lock (stakeGate)
        {
            purchaseId = ReusablePurchaseId(roundIndex, cardCount);
            if (purchaseId.Length == 0)
            {
                purchaseId = Guid.NewGuid().ToString("N");
            }

            unansweredPurchaseId = purchaseId;
            unansweredPurchaseRoundIndex = roundIndex;
            unansweredPurchaseCardCount = cardCount;
        }

        stakeInFlight = true;
        work.Run("bingo cards", async token =>
        {
            var result = await casino
                .BuyBingoCardsAsync(roomId, roundIndex, purchaseId, cardCount, token)
                .ConfigureAwait(false);
            if (result is null)
            {
                Interlocked.Exchange(ref stakeFailed, 1);
                return;
            }

            ForgetUnansweredPurchase(purchaseId);
            Interlocked.Exchange(ref stakeResult,
                new CasinoStakeOutcome(result.Granted, result.Reason, result.Ceiling));
            if (!result.Granted)
            {
                chips.AbsorbCeiling(result.Ceiling);
                chips.RefreshNow();
                InvalidatePersonal();
                return;
            }

            if (sittingId.Length > 0)
            {
                chips.AbsorbStack(sittingId, result.Stack);
            }

            AbsorbBingoCards(roomId, result);
            InvalidatePersonal();
        }, () => stakeInFlight = false);
    }

    public void PlaceBlackjackBet(long amount, long perfectPairs = 0, long twentyOnePlusThree = 0)
    {
        var roomId = room.RoomId;
        var snapshot = room.State?.Snapshot;
        var board = room.State?.Blackjack;
        if (stakeInFlight || !session.IsSignedIn || roomId.Length == 0 || snapshot is null || board is null
            || amount <= 0)
        {
            return;
        }

        if (!string.Equals(snapshot.GameKind, CasinoWire.BlackjackKind, StringComparison.Ordinal))
        {
            return;
        }

        var roundIndex = board.HandIndex;
        var sittingId = CasinoCurrencies.SeatBanked(CasinoCurrencies.Of(board))
            ? string.Empty
            : chips.State?.TableSitting?.Id ?? string.Empty;
        string betId;
        string clientRoundId;
        lock (stakeGate)
        {
            clientRoundId = ChipRoundFor(roundIndex);
            betId = unansweredBetPairs == perfectPairs && unansweredBetThree == twentyOnePlusThree
                ? ReusableBetId(roundIndex, HandBetSpot, amount)
                : string.Empty;
            if (betId.Length == 0)
            {
                betId = Guid.NewGuid().ToString("N");
            }

            unansweredBetId = betId;
            unansweredBetSpot = HandBetSpot;
            unansweredBetAmount = amount;
            unansweredBetRoundIndex = roundIndex;
            unansweredBetPairs = perfectPairs;
            unansweredBetThree = twentyOnePlusThree;
        }

        stakeInFlight = true;
        work.Run("blackjack bet", async token =>
        {
            var result = await casino
                .PlaceBlackjackBetAsync(roomId, clientRoundId, betId, amount, perfectPairs, twentyOnePlusThree,
                    token)
                .ConfigureAwait(false);
            if (result is null)
            {
                Interlocked.Exchange(ref stakeFailed, 1);
                return;
            }

            ForgetUnansweredBet(betId);
            Interlocked.Exchange(ref stakeResult, new CasinoStakeOutcome(result.Granted, result.Reason));
            if (!result.Granted)
            {
                chips.RefreshNow();
                InvalidatePersonal();
                return;
            }

            if (sittingId.Length > 0)
            {
                chips.AbsorbStack(sittingId, result.Stack);
            }

            InvalidatePersonal();
        }, () => stakeInFlight = false);
    }

    public void SendBlackjackAction(int action)
    {
        var roomId = room.RoomId;
        var held = room.State;
        var board = held?.Blackjack;
        var mine = room.Private?.Blackjack;
        if (stakeInFlight || !session.IsSignedIn || roomId.Length == 0 || board is null || mine is null
            || action == 0)
        {
            return;
        }

        var verb = BlackjackActions.VerbFor(action);
        if (verb.Length == 0
            || board.HandId.Length == 0
            || !string.Equals(mine.HandId, board.HandId, StringComparison.Ordinal)
            || !BlackjackRules.Allows(mine.ActionsMask, action))
        {
            return;
        }

        var handId = board.HandId;
        var actionSeq = mine.ActionCount;
        var sittingId = CasinoCurrencies.SeatBanked(CasinoCurrencies.Of(board))
            ? string.Empty
            : chips.State?.TableSitting?.Id ?? string.Empty;
        string actionId;
        lock (stakeGate)
        {
            actionId = ReusableActionId(handId, action, actionSeq);
            if (actionId.Length == 0)
            {
                actionId = Guid.NewGuid().ToString("N");
            }

            unansweredActionId = actionId;
            unansweredActionHandId = handId;
            unansweredActionValue = action;
            unansweredActionSeq = actionSeq;
        }

        stakeInFlight = true;
        work.Run("blackjack action", async token =>
        {
            var result = await casino
                .SendBlackjackActionAsync(roomId, handId, actionSeq, verb, actionId, BlackjackActions.IsWager(verb), token)
                .ConfigureAwait(false);
            if (result is null)
            {
                Interlocked.Exchange(ref stakeFailed, 1);
                return;
            }

            ForgetUnansweredAction(actionId);
            Interlocked.Exchange(ref stakeResult, new CasinoStakeOutcome(result.Granted, result.Reason));
            if (!result.Granted)
            {
                chips.RefreshNow();
                InvalidatePersonal();
                return;
            }

            if (sittingId.Length > 0)
            {
                chips.AbsorbStack(sittingId, result.Stack);
            }

            InvalidatePersonal();
        }, () => stakeInFlight = false);
    }

    private string ReusableActionId(string handId, int action, long actionSeq)
    {
        if (unansweredActionId.Length == 0
            || unansweredActionValue != action
            || unansweredActionSeq != actionSeq
            || !string.Equals(unansweredActionHandId, handId, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        return unansweredActionId;
    }

    private void ForgetUnansweredAction(string actionId)
    {
        lock (stakeGate)
        {
            if (!string.Equals(unansweredActionId, actionId, StringComparison.Ordinal))
            {
                return;
            }

            unansweredActionId = string.Empty;
            unansweredActionHandId = string.Empty;
            unansweredActionValue = 0;
            unansweredActionSeq = -1;
        }
    }

    private string ChipRoundFor(long roundIndex)
    {
        if (chipRoundIndex == roundIndex && chipRoundId.Length > 0)
        {
            return chipRoundId;
        }

        chipRoundIndex = roundIndex;
        chipRoundId = Guid.NewGuid().ToString("N");
        return chipRoundId;
    }

    private string ReusableBetId(long roundIndex, int spot, long amount)
    {
        if (unansweredBetId.Length == 0
            || unansweredBetSpot != spot
            || unansweredBetAmount != amount
            || unansweredBetRoundIndex != roundIndex)
        {
            return string.Empty;
        }

        return unansweredBetId;
    }

    internal static bool ReusesPurchase(long heldRoundIndex, int heldCardCount, long roundIndex, int cardCount)
    {
        return heldRoundIndex == roundIndex && heldCardCount == cardCount;
    }

    private string ReusablePurchaseId(long roundIndex, int cardCount)
    {
        if (unansweredPurchaseId.Length == 0
            || !ReusesPurchase(unansweredPurchaseRoundIndex, unansweredPurchaseCardCount, roundIndex, cardCount))
        {
            return string.Empty;
        }

        return unansweredPurchaseId;
    }

    private void ForgetUnansweredBet(string betId)
    {
        lock (stakeGate)
        {
            if (!string.Equals(unansweredBetId, betId, StringComparison.Ordinal))
            {
                return;
            }

            unansweredBetId = string.Empty;
            unansweredBetSpot = -1;
            unansweredBetAmount = 0;
            unansweredBetRoundIndex = -1;
        }
    }

    private void ForgetUnansweredPurchase(string purchaseId)
    {
        lock (stakeGate)
        {
            if (!string.Equals(unansweredPurchaseId, purchaseId, StringComparison.Ordinal))
            {
                return;
            }

            unansweredPurchaseId = string.Empty;
            unansweredPurchaseRoundIndex = -1;
            unansweredPurchaseCardCount = -1;
        }
    }

    public void EnsureFresh()
    {
        if (!session.IsSignedIn || !directoryCadence.Due(DateTime.UtcNow))
        {
            return;
        }

        RefreshRooms();
    }

    public void RefreshNow()
    {
        Interlocked.Exchange(ref roomsAttemptedAtTick, 0);
        directoryCadence.Reset();
        RefreshRooms();
    }

    public void Enter(string roomId)
    {
        if (roomId.Length == 0 || !session.IsSignedIn)
        {
            return;
        }

        ClearPersonal();
        room.Enter(roomId);
        roomCadence.Reset();
        Interlocked.Exchange(ref roomAttemptedAtTick, 0);
        RefreshRoomState();
    }

    public void Leave()
    {
        room.Leave();
        ClearPersonal();
        roomCadence.Reset();
        Interlocked.Exchange(ref roomAttemptedAtTick, 0);
    }

    internal static bool CoolingDown(long attemptedAtTick, long nowTick)
    {
        return attemptedAtTick != 0 && nowTick - attemptedAtTick < RetryAfterAttemptMilliseconds;
    }

    internal static bool PersonalIsStale(long heldRoundIndex, int heldPhase, long roundIndex, int phase)
    {
        return heldRoundIndex != roundIndex || heldPhase != phase;
    }

    private static long NowUnixMilliseconds()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!session.IsSignedIn || room.RoomId.Length == 0)
        {
            return;
        }

        SyncPersonal();
        var nowUtc = DateTime.UtcNow;
        if (signals.RealtimeActive && room.Attached && !room.AwaitingSnapshot)
        {
            roomCadence.Mark(nowUtc);
            return;
        }

        if (!roomCadence.Due(nowUtc))
        {
            return;
        }

        RefreshRoomState();
    }

    private void SyncPersonal()
    {
        var snapshot = room.State?.Snapshot;
        if (snapshot is null)
        {
            return;
        }

        var roundIndex = snapshot.RoundIndex;
        var phase = snapshot.Phase;
        if (!PersonalIsStale(Interlocked.Read(ref personalRoundIndex), Volatile.Read(ref personalPhase),
                roundIndex, phase))
        {
            return;
        }

        if (PersonalIsStale(Interlocked.Read(ref personalAskedRoundIndex), Volatile.Read(ref personalAskedPhase),
                roundIndex, phase))
        {
            Interlocked.Exchange(ref personalAskedRoundIndex, roundIndex);
            Volatile.Write(ref personalAskedPhase, phase);
            Interlocked.Exchange(ref personalAttemptedAtTick, 0);
        }

        RefreshPersonal();
    }

    private void InvalidatePersonal()
    {
        Interlocked.Increment(ref personalGeneration);
        Interlocked.Exchange(ref personalRoundIndex, -1);
        Volatile.Write(ref personalPhase, -1);
        Interlocked.Exchange(ref personalAskedRoundIndex, -1);
        Volatile.Write(ref personalAskedPhase, -1);
        Interlocked.Exchange(ref personalAttemptedAtTick, 0);
    }

    private void OnCasinoSignal(CasinoSignal signal)
    {
        room.Receive(signal, NowUnixMilliseconds());
    }

    private void OnRealtimeConnected(bool connected)
    {
        room.OnRealtimeConnected(connected);
        if (connected)
        {
            directoryCadence.RequestAfterReconnect();
            return;
        }

        roomCadence.Reset();
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        room.Reset();
        rooms = Array.Empty<CasinoRoomListItemDto>();
        loadedRooms = false;
        ClearPersonal();
        Interlocked.Exchange(ref roomsFailed, 0);
        Interlocked.Exchange(ref roomsAttemptedAtTick, 0);
        Interlocked.Exchange(ref roomAttemptedAtTick, 0);
        directoryCadence.Reset();
        roomCadence.Reset();
    }

    private void ClearPersonal()
    {
        Interlocked.Increment(ref personalGeneration);
        wheelBets = null;
        bingoCards = null;
        raceBets = null;
        Interlocked.Exchange(ref personalRoundIndex, -1);
        Volatile.Write(ref personalPhase, -1);
        personalAskedRoundIndex = -1;
        personalAskedPhase = -1;
        chipRoundId = string.Empty;
        chipRoundIndex = -1;
        Interlocked.Exchange(ref personalAttemptedAtTick, 0);
        Interlocked.Exchange(ref stakeResult, null);
        Interlocked.Exchange(ref stakeFailed, 0);
        lock (stakeGate)
        {
            unansweredBetId = string.Empty;
            unansweredBetSpot = -1;
            unansweredBetAmount = 0;
            unansweredBetRoundIndex = -1;
            unansweredPurchaseId = string.Empty;
            unansweredPurchaseRoundIndex = -1;
            unansweredPurchaseCardCount = -1;
            unansweredActionId = string.Empty;
            unansweredActionHandId = string.Empty;
            unansweredActionValue = 0;
            unansweredActionSeq = -1;
        }
    }

    private void RefreshRooms()
    {
        if (!session.IsSignedIn
            || CoolingDown(Interlocked.Read(ref roomsAttemptedAtTick), Environment.TickCount64)
            || Interlocked.Exchange(ref fetchingRooms, 1) != 0)
        {
            return;
        }

        loadingRooms = true;
        Interlocked.Exchange(ref roomsAttemptedAtTick, Environment.TickCount64);
        work.Run("rooms directory", async token =>
        {
            var directory = await casino.RoomsAsync(token).ConfigureAwait(false);
            if (directory is null)
            {
                Interlocked.Exchange(ref roomsFailed, 1);
                return;
            }

            Interlocked.Exchange(ref roomsAttemptedAtTick, 0);
            room.AbsorbClock(directory.ServerNowUnixMs, NowUnixMilliseconds());
            rooms = directory.Rooms ?? Array.Empty<CasinoRoomListItemDto>();
            loadedRooms = true;
        }, () =>
        {
            loadingRooms = false;
            Interlocked.Exchange(ref fetchingRooms, 0);
        });
    }

    private void OnRoomStatus(int statusCode)
    {
        if (statusCode == NotFoundStatus)
        {
            Interlocked.Exchange(ref roomGoneStatus, 1);
        }
    }

    private void RefreshRoomState()
    {
        var target = room.RoomId;
        if (target.Length == 0 || !session.IsSignedIn
            || CoolingDown(Interlocked.Read(ref roomAttemptedAtTick), Environment.TickCount64)
            || Interlocked.Exchange(ref fetchingRoomState, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref roomAttemptedAtTick, Environment.TickCount64);
        Interlocked.Exchange(ref roomGoneStatus, 0);
        work.Run("room state", async token =>
        {
            var fresh = await casino.RoomStateAsync(target, roomStatusSink, token).ConfigureAwait(false);
            if (fresh is null)
            {
                if (Interlocked.Exchange(ref roomGoneStatus, 0) != 0)
                {
                    Interlocked.Exchange(ref roomAttemptedAtTick, 0);
                    room.CloseFromHttp(target, CasinoReasons.Ended);
                }

                return;
            }

            Interlocked.Exchange(ref roomAttemptedAtTick, 0);
            room.AbsorbHttpState(target, fresh, NowUnixMilliseconds());
        }, () => Interlocked.Exchange(ref fetchingRoomState, 0));
    }

    private void RefreshPersonal()
    {
        var target = room.RoomId;
        var snapshot = room.State?.Snapshot;
        if (target.Length == 0 || snapshot is null || !session.IsSignedIn
            || CoolingDown(Interlocked.Read(ref personalAttemptedAtTick), Environment.TickCount64)
            || Interlocked.Exchange(ref fetchingPersonal, 1) != 0)
        {
            return;
        }

        var gameKind = snapshot.GameKind;
        var roundIndex = snapshot.RoundIndex;
        var phase = snapshot.Phase;
        var generation = Volatile.Read(ref personalGeneration);
        var handNeedsReading = !room.Attached || HandUnread();
        Interlocked.Exchange(ref personalAttemptedAtTick, Environment.TickCount64);
        work.Run("room personal", async token =>
        {
            if (string.Equals(gameKind, CasinoWire.WheelKind, StringComparison.Ordinal))
            {
                var bets = await casino.MyWheelBetsAsync(target, token).ConfigureAwait(false);
                if (bets is null)
                {
                    return;
                }

                Interlocked.Exchange(ref personalAttemptedAtTick, 0);
                AbsorbWheelBets(target, bets);
                MarkPersonalLoaded(target, generation, roundIndex, phase);
                return;
            }

            if (string.Equals(gameKind, CasinoWire.BingoKind, StringComparison.Ordinal))
            {
                var cards = await casino.MyBingoCardsAsync(target, token).ConfigureAwait(false);
                if (cards is null)
                {
                    return;
                }

                Interlocked.Exchange(ref personalAttemptedAtTick, 0);
                AbsorbBingoCards(target, cards);
                MarkPersonalLoaded(target, generation, roundIndex, phase);
                return;
            }

            if (string.Equals(gameKind, CasinoWire.RaceKind, StringComparison.Ordinal))
            {
                var tickets = await casino.MyRaceBetsAsync(target, token).ConfigureAwait(false);
                if (tickets is null)
                {
                    return;
                }

                Interlocked.Exchange(ref personalAttemptedAtTick, 0);
                AbsorbRaceBets(target, tickets);
                MarkPersonalLoaded(target, generation, roundIndex, phase);
                return;
            }

            if (handNeedsReading && string.Equals(gameKind, CasinoWire.BlackjackKind, StringComparison.Ordinal))
            {
                var hand = await casino.MyBlackjackHandAsync(target, token).ConfigureAwait(false);
                if (hand is null)
                {
                    return;
                }

                Interlocked.Exchange(ref personalAttemptedAtTick, 0);
                AbsorbBlackjackHand(target, hand);
                MarkPersonalLoaded(target, generation, roundIndex, phase);
                return;
            }

            Interlocked.Exchange(ref personalAttemptedAtTick, 0);
            MarkPersonalLoaded(target, generation, roundIndex, phase);
        }, () => Interlocked.Exchange(ref fetchingPersonal, 0));
    }

    private void MarkPersonalLoaded(string requestedRoomId, int askedGeneration, long roundIndex, int phase)
    {
        if (askedGeneration != Volatile.Read(ref personalGeneration)
            || !string.Equals(room.RoomId, requestedRoomId, StringComparison.Ordinal))
        {
            return;
        }

        Interlocked.Exchange(ref personalRoundIndex, roundIndex);
        Volatile.Write(ref personalPhase, phase);
    }

    private void AbsorbWheelBets(string requestedRoomId, CasinoWheelBetsDto fresh)
    {
        if (!string.Equals(room.RoomId, requestedRoomId, StringComparison.Ordinal))
        {
            return;
        }

        wheelBets = fresh;
    }

    private void AbsorbRaceBets(string requestedRoomId, CasinoRaceBetsDto fresh)
    {
        if (!string.Equals(room.RoomId, requestedRoomId, StringComparison.Ordinal))
        {
            return;
        }

        raceBets = fresh;
    }

    private void AbsorbBingoCards(string requestedRoomId, CasinoBingoCardsDto fresh)
    {
        if (!string.Equals(room.RoomId, requestedRoomId, StringComparison.Ordinal))
        {
            return;
        }

        bingoCards = fresh;
    }

    private void AbsorbBlackjackHand(string requestedRoomId, CasinoBlackjackHandStateDto fresh)
    {
        var mine = CasinoRoomSession.BuildPrivate(new CasinoPrivateDto(fresh.EventKind, fresh.Payload));
        if (mine is null)
        {
            return;
        }

        room.AbsorbHttpPrivate(requestedRoomId, fresh.Epoch, fresh.Seq, mine);
    }

    private bool HandUnread()
    {
        var board = room.State?.Blackjack;
        var seats = board?.Seats;
        var me = session.CurrentUser?.Id ?? string.Empty;
        if (board is null || seats is null || board.HandId.Length == 0 || me.Length == 0)
        {
            return false;
        }

        var seated = false;
        for (var index = 0; index < seats.Length; index++)
        {
            if (string.Equals(seats[index].UserId, me, StringComparison.Ordinal))
            {
                seated = true;
                break;
            }
        }

        if (!seated)
        {
            return false;
        }

        var mine = room.Private?.Blackjack;
        return mine is null || !string.Equals(mine.HandId, board.HandId, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
        session.Changed -= OnSessionChanged;
        signals.CasinoReceived -= OnCasinoSignal;
        signals.ConnectedChanged -= OnRealtimeConnected;
        work.Dispose();
    }
}
