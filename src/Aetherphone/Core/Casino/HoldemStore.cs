using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Core.Casino;

internal sealed class HoldemStore : IDisposable
{
    private const long RetryAfterAttemptMilliseconds = 30_000;
    private const long HandPollMilliseconds = 1_500;
    private const long RejoinPenaltyMilliseconds = HoldemRules.RejoinPenaltyMinutes * 60_000L;

    private readonly AethernetSession session;
    private readonly CasinoClient casino;
    private readonly CasinoStore chips;
    private readonly StoreWork work = new("Holdem");
    private readonly object intentGate = new();
    private readonly Dictionary<string, long> leftAtTick = new(StringComparer.Ordinal);

    private volatile CasinoTableRowDto[] tables = Array.Empty<CasinoTableRowDto>();
    private volatile CasinoHoldemHistoryDto? history;
    private volatile bool loading;
    private volatile bool loaded;
    private volatile bool intentInFlight;
    private volatile bool actInFlight;
    private volatile bool historyLoading;
    private CasinoSeatOutcome? seatOutcome;
    private CasinoStakeOutcome? actionOutcome;
    private string sitIntentId = string.Empty;
    private string sitIntentRoom = string.Empty;
    private long sitIntentBuyIn = -1;
    private int sitIntentSeat = -1;
    private string actIntentId = string.Empty;
    private string actIntentHand = string.Empty;
    private int actIntentCount = -1;
    private int actIntentAction;
    private long actIntentAmount = -1;
    private int tablesFailed;
    private int intentFailed;
    private int historyFailed;
    private int fetchingTables;
    private int fetchingHand;
    private long tablesAttemptedAtTick;
    private long handAttemptedAtTick;
    private string? lastAccountId;

    public HoldemStore(AethernetSession session, CasinoClient casino, CasinoStore chips)
    {
        this.session = session;
        this.casino = casino;
        this.chips = chips;
        session.Changed += OnSessionChanged;
    }

    public CasinoTableRowDto[] Tables => tables;

    public string AccountId => session.CurrentUser?.Id ?? string.Empty;

    public bool Loading => loading;

    public bool Loaded => loaded;

    public bool IntentInFlight => intentInFlight;

    public bool ActInFlight => actInFlight;

    public bool HistoryLoading => historyLoading;

    public CasinoHoldemHistoryDto? HistoryFor(string roomId)
    {
        var held = history;
        return held is not null && string.Equals(held.RoomId, roomId, StringComparison.Ordinal) ? held : null;
    }

    public bool TakeTablesFailure() => Interlocked.Exchange(ref tablesFailed, 0) != 0;

    public bool TakeIntentFailure() => Interlocked.Exchange(ref intentFailed, 0) != 0;

    public bool TakeHistoryFailure() => Interlocked.Exchange(ref historyFailed, 0) != 0;

    public CasinoSeatOutcome? TakeSeatOutcome() => Interlocked.Exchange(ref seatOutcome, null);

    public CasinoStakeOutcome? TakeActionOutcome() => Interlocked.Exchange(ref actionOutcome, null);

    public bool RejoinPenalty(string roomId, long nowTick)
    {
        lock (intentGate)
        {
            return leftAtTick.TryGetValue(roomId, out var left) && RejoinPenaltyApplies(left, nowTick);
        }
    }

    internal static bool RejoinPenaltyApplies(long leftAtTick, long nowTick)
    {
        return leftAtTick != 0 && nowTick - leftAtTick < RejoinPenaltyMilliseconds;
    }

    internal static bool CoolingDown(long attemptedAtTick, long nowTick, long window)
    {
        return attemptedAtTick != 0 && nowTick - attemptedAtTick < window;
    }

    internal static bool ReusesSit(string heldRoom, int heldSeat, long heldBuyIn, string roomId, int seatIndex,
        long buyIn)
    {
        return heldSeat == seatIndex && heldBuyIn == buyIn && string.Equals(heldRoom, roomId, StringComparison.Ordinal);
    }

    internal static bool ReusesAct(string heldHand, int heldCount, int heldAction, long heldAmount, string handId,
        int actionCount, int action, long amount)
    {
        return heldCount == actionCount && heldAction == action && heldAmount == amount
            && string.Equals(heldHand, handId, StringComparison.Ordinal);
    }

    public void RefreshTables()
    {
        Interlocked.Exchange(ref tablesAttemptedAtTick, 0);
        LoadTables();
    }

    public void EnsureTables()
    {
        if (CoolingDown(Interlocked.Read(ref tablesAttemptedAtTick), Environment.TickCount64,
                RetryAfterAttemptMilliseconds))
        {
            return;
        }

        LoadTables();
    }

    public void Sit(string roomId, int seatIndex, long buyIn, bool postBigBlind)
    {
        if (roomId.Length == 0 || !Begin())
        {
            return;
        }

        string clientActionId;
        lock (intentGate)
        {
            if (!ReusesSit(sitIntentRoom, sitIntentSeat, sitIntentBuyIn, roomId, seatIndex, buyIn))
            {
                sitIntentId = string.Empty;
            }

            if (sitIntentId.Length == 0)
            {
                sitIntentId = Guid.NewGuid().ToString("N");
            }

            sitIntentRoom = roomId;
            sitIntentSeat = seatIndex;
            sitIntentBuyIn = buyIn;
            clientActionId = sitIntentId;
        }

        work.Run("holdem sit", async token =>
        {
            var held = chips.State?.TableSitting?.Id ?? string.Empty;
            var sittingId = held.Length > 0 ? held : clientActionId;
            var answer = await casino.SitHoldemAsync(new CasinoHoldemSitRequest(roomId, seatIndex, sittingId,
                clientActionId, buyIn, postBigBlind), token).ConfigureAwait(false);
            if (answer is null)
            {
                Interlocked.Exchange(ref intentFailed, 1);
                return;
            }

            ForgetSit(clientActionId);
            Interlocked.Exchange(ref seatOutcome,
                new CasinoSeatOutcome(answer.Granted, answer.Granted ? string.Empty : Named(answer.Reason), false,
                    false));
            chips.RefreshNow();
        }, EndIntent);
    }

    public void Leave(string roomId)
    {
        if (roomId.Length == 0 || !Begin())
        {
            return;
        }

        StampLeft(roomId);
        work.Run("holdem leave", async token =>
        {
            var answer = await casino.LeaveHoldemAsync(roomId, token).ConfigureAwait(false);
            if (answer is null)
            {
                Interlocked.Exchange(ref intentFailed, 1);
                return;
            }

            var atHandEnd = string.Equals(answer.Reason, CasinoReasons.AtHandEnd, StringComparison.Ordinal);
            Interlocked.Exchange(ref seatOutcome, new CasinoSeatOutcome(answer.Granted || atHandEnd,
                atHandEnd || answer.Granted ? string.Empty : Named(answer.Reason), false, atHandEnd));
            chips.RefreshNow();
        }, EndIntent);
    }

    public void Abandon(string roomId)
    {
        if (roomId.Length == 0)
        {
            return;
        }

        StampLeft(roomId);
        work.Run("holdem abandon", async token =>
        {
            var answer = await casino.LeaveHoldemAsync(roomId, token).ConfigureAwait(false);
            if (answer is not null)
            {
                chips.RefreshNow();
            }
        });
    }

    public void TopUp(string roomId, long amount)
    {
        if (roomId.Length == 0 || amount <= 0 || !Begin())
        {
            return;
        }

        var clientActionId = Guid.NewGuid().ToString("N");
        work.Run("holdem topup", async token =>
        {
            var answer = await casino.TopUpHoldemAsync(new CasinoHoldemTopUpRequest(roomId, clientActionId, amount),
                token).ConfigureAwait(false);
            if (answer is null)
            {
                Interlocked.Exchange(ref intentFailed, 1);
                return;
            }

            Interlocked.Exchange(ref actionOutcome,
                new CasinoStakeOutcome(answer.Granted, answer.Granted ? string.Empty : Named(answer.Reason)));
            chips.RefreshNow();
        }, EndIntent);
    }

    public void SitOut(string roomId, bool sitOut, bool postBigBlind)
    {
        if (roomId.Length == 0 || !Begin())
        {
            return;
        }

        work.Run("holdem sitout", async token =>
        {
            var answer = await casino.HoldemSitOutAsync(new CasinoHoldemSitOutRequest(roomId, sitOut, postBigBlind),
                token).ConfigureAwait(false);
            if (answer is null)
            {
                Interlocked.Exchange(ref intentFailed, 1);
                return;
            }

            Interlocked.Exchange(ref actionOutcome,
                new CasinoStakeOutcome(answer.Granted, answer.Granted ? string.Empty : Named(answer.Reason)));
        }, EndIntent);
    }

    public void Act(string roomId, string handId, int actionCount, int action, long amount)
    {
        var verb = HoldemActions.VerbFor(action);
        if (roomId.Length == 0 || handId.Length == 0 || verb.Length == 0 || actInFlight || !session.IsSignedIn)
        {
            return;
        }

        var sentAmount = HoldemActions.CarriesAmount(action) ? amount : 0;
        string clientActionId;
        lock (intentGate)
        {
            if (!ReusesAct(actIntentHand, actIntentCount, actIntentAction, actIntentAmount, handId, actionCount,
                    action, sentAmount))
            {
                actIntentId = string.Empty;
            }

            if (actIntentId.Length == 0)
            {
                actIntentId = Guid.NewGuid().ToString("N");
            }

            actIntentHand = handId;
            actIntentCount = actionCount;
            actIntentAction = action;
            actIntentAmount = sentAmount;
            clientActionId = actIntentId;
        }

        actInFlight = true;
        work.Run("holdem act", async token =>
        {
            var answer = await casino.ActHoldemAsync(new CasinoHoldemActRequest(roomId, handId, actionCount,
                clientActionId, verb, sentAmount), token).ConfigureAwait(false);
            if (answer is null)
            {
                Interlocked.Exchange(ref intentFailed, 1);
                return;
            }

            ForgetAct(clientActionId);
            Interlocked.Exchange(ref actionOutcome,
                new CasinoStakeOutcome(answer.Granted, answer.Granted ? string.Empty : Named(answer.Reason)));
        }, () => actInFlight = false);
    }

    public void TimeBank(string roomId, string handId, int actionCount)
    {
        if (roomId.Length == 0 || handId.Length == 0 || actInFlight || !session.IsSignedIn)
        {
            return;
        }

        actInFlight = true;
        work.Run("holdem timebank", async token =>
        {
            var answer = await casino.HoldemTimeBankAsync(new CasinoHoldemTimeBankRequest(roomId, handId,
                actionCount), token).ConfigureAwait(false);
            if (answer is null)
            {
                Interlocked.Exchange(ref intentFailed, 1);
                return;
            }

            Interlocked.Exchange(ref actionOutcome,
                new CasinoStakeOutcome(answer.Granted, answer.Granted ? string.Empty : Named(answer.Reason)));
        }, () => actInFlight = false);
    }

    public void LoadHistory(string roomId)
    {
        if (roomId.Length == 0 || historyLoading || !session.IsSignedIn)
        {
            return;
        }

        historyLoading = true;
        work.Run("holdem history", async token =>
        {
            var answer = await casino.HoldemHistoryAsync(roomId, token).ConfigureAwait(false);
            if (answer is null)
            {
                Interlocked.Exchange(ref historyFailed, 1);
                return;
            }

            history = answer with { RoomId = roomId };
        }, () => historyLoading = false);
    }

    public void SyncHand(CasinoRoomSession room, string myUserId)
    {
        var roomId = room.RoomId;
        var board = room.State?.Holdem;
        if (roomId.Length == 0 || board is null || board.HandId.Length == 0 || myUserId.Length == 0
            || !session.IsSignedIn || !NeedsHand(board, room.Private?.Holdem, myUserId, room.Attached))
        {
            return;
        }

        var nowTick = Environment.TickCount64;
        if (CoolingDown(Interlocked.Read(ref handAttemptedAtTick), nowTick, HandPollMilliseconds)
            || Interlocked.Exchange(ref fetchingHand, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref handAttemptedAtTick, nowTick);
        work.Run("holdem hand", async token =>
        {
            var hand = await casino.MyHoldemHandAsync(roomId, token).ConfigureAwait(false);
            if (hand is null || hand.Payload.Length == 0)
            {
                return;
            }

            var mine = CasinoRoomSession.BuildHoldemPrivate(new CasinoPrivateDto(hand.EventKind, hand.Payload));
            if (mine is not null)
            {
                room.AbsorbHttpHoldemPrivate(roomId, hand.Epoch, hand.Seq, mine);
            }
        }, () => Interlocked.Exchange(ref fetchingHand, 0));
    }

    internal static bool NeedsHand(CasinoHoldemRoomStateDto board, CasinoHoldemYouDto? mine, string myUserId,
        bool attached)
    {
        var seats = board.Seats;
        if (seats is null)
        {
            return false;
        }

        var dealt = false;
        for (var index = 0; index < seats.Length; index++)
        {
            if (string.Equals(seats[index].UserId, myUserId, StringComparison.Ordinal))
            {
                dealt = HoldemSeatStates.DealtIn(seats[index].State);
                break;
            }
        }

        if (!dealt)
        {
            return false;
        }

        if (mine is null || !string.Equals(mine.HandId, board.HandId, StringComparison.Ordinal))
        {
            return true;
        }

        return !attached && mine.ActionCount != board.ActionCount;
    }

    internal static string Named(string reason)
    {
        return reason.Length > 0 ? reason : CasinoReasons.Unreachable;
    }

    private void StampLeft(string roomId)
    {
        lock (intentGate)
        {
            leftAtTick[roomId] = Environment.TickCount64;
        }
    }

    private void LoadTables()
    {
        if (!session.IsSignedIn || Interlocked.Exchange(ref fetchingTables, 1) != 0)
        {
            return;
        }

        loading = true;
        Interlocked.Exchange(ref tablesAttemptedAtTick, Environment.TickCount64);
        work.Run("holdem tables", async token =>
        {
            var directory = await casino.TablesAsync(HoldemRules.Kind, token).ConfigureAwait(false);
            if (directory is null)
            {
                Interlocked.Exchange(ref tablesFailed, 1);
                return;
            }

            tables = directory.Tables ?? Array.Empty<CasinoTableRowDto>();
            loaded = true;
        }, () =>
        {
            loading = false;
            Interlocked.Exchange(ref fetchingTables, 0);
        });
    }

    private bool Begin()
    {
        if (intentInFlight || !session.IsSignedIn)
        {
            return false;
        }

        intentInFlight = true;
        return true;
    }

    private void EndIntent()
    {
        intentInFlight = false;
    }

    private void ForgetSit(string intentId)
    {
        lock (intentGate)
        {
            if (!string.Equals(sitIntentId, intentId, StringComparison.Ordinal))
            {
                return;
            }

            sitIntentId = string.Empty;
            sitIntentRoom = string.Empty;
            sitIntentSeat = -1;
            sitIntentBuyIn = -1;
        }
    }

    private void ForgetAct(string intentId)
    {
        lock (intentGate)
        {
            if (!string.Equals(actIntentId, intentId, StringComparison.Ordinal))
            {
                return;
            }

            actIntentId = string.Empty;
            actIntentHand = string.Empty;
            actIntentCount = -1;
            actIntentAction = 0;
            actIntentAmount = -1;
        }
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        tables = Array.Empty<CasinoTableRowDto>();
        history = null;
        loaded = false;
        Interlocked.Exchange(ref seatOutcome, null);
        Interlocked.Exchange(ref actionOutcome, null);
        Interlocked.Exchange(ref tablesFailed, 0);
        Interlocked.Exchange(ref intentFailed, 0);
        Interlocked.Exchange(ref tablesAttemptedAtTick, 0);
        lock (intentGate)
        {
            leftAtTick.Clear();
            sitIntentId = string.Empty;
            sitIntentRoom = string.Empty;
            sitIntentSeat = -1;
            sitIntentBuyIn = -1;
            actIntentId = string.Empty;
            actIntentHand = string.Empty;
            actIntentCount = -1;
            actIntentAction = 0;
            actIntentAmount = -1;
        }
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        work.Dispose();
    }
}
