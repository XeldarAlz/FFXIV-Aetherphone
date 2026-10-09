using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Runtime;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Casino;

internal sealed record CasinoSeatOutcome(bool Granted, string Reason, bool JoinsNextHand, bool AtHandEnd);

internal readonly record struct LedgerIntent(string Kind, string Counterparty, long Amount);

internal sealed class CasinoTablesStore : IDisposable
{
    private static readonly TimeSpan ForegroundPollInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan BackgroundPollInterval = TimeSpan.FromSeconds(120);
    private const long RetryAfterAttemptMilliseconds = 30_000;
    private const long LedgerRefreshMilliseconds = 10_000;
    private const int NotFoundStatus = 404;

    private readonly AethernetSession session;
    private readonly CasinoClient casino;
    private readonly CasinoStore chips;
    private readonly PollCadence cadence;
    private readonly StoreWork work = new("CasinoTables");
    private readonly object intentGate = new();
    private readonly Action<int> tableStatusSink;

    private volatile CasinoTableRowDto[] tables = Array.Empty<CasinoTableRowDto>();
    private volatile CasinoTableRowDto[] listed = Array.Empty<CasinoTableRowDto>();
    private volatile CasinoTableDoorDto? door;
    private volatile CasinoTableLedgerDto? ledger;
    private volatile CasinoTableRowDto? card;
    private readonly Action<int> cardStatusSink = static _ => { };
    private int fetchingCard;
    private volatile string ledgerRoomId = string.Empty;
    private CasinoTableConfigDto? createIntentConfig;
    private string ledgerIntentId = string.Empty;
    private LedgerIntent ledgerIntentKey;
    private int fetchingLedger;
    private long ledgerAttemptedAtTick;
    private long ledgerLoadedAtTick;
    private volatile CasinoQuickSeatDto? quickSeat;
    private volatile CasinoTableRowDto? hostedTable;
    private volatile CasinoTableRowDto? resolvedTable;
    private volatile bool loading;
    private volatile bool loaded;
    private volatile bool intentInFlight;
    private CasinoSeatOutcome? seatOutcome;
    private CasinoStakeOutcome? noticeOutcome;
    private string closedTableId = string.Empty;
    private string quickSeatRefusal = string.Empty;
    private string seatIntentId = string.Empty;
    private string createIntentId = string.Empty;
    private string doorRoomId = string.Empty;
    private int seatIntentSeatIndex = -1;
    private long seatIntentBuyIn = -1;
    private int createIntentTier = int.MinValue;
    private int tablesFailed;
    private int intentFailed;
    private int tableMissing;
    private int fetchingTables;
    private int fetchingDoor;
    private long tablesAttemptedAtTick;
    private long doorAttemptedAtTick;
    private string? lastAccountId;

    public CasinoTablesStore(AethernetSession session, CasinoClient casino, CasinoStore chips,
        PhoneVisibility visibility)
    {
        this.session = session;
        this.casino = casino;
        this.chips = chips;
        cadence = new PollCadence(visibility, ForegroundPollInterval, BackgroundPollInterval);
        tableStatusSink = OnTableStatus;
        session.Changed += OnSessionChanged;
    }

    public CasinoTableRowDto[] Tables => tables;

    public CasinoTableRowDto[] Listed => listed;

    public CasinoTableRowDto? CardFor(string roomId)
    {
        if (roomId.Length == 0)
        {
            return null;
        }

        var rows = listed;
        for (var index = 0; index < rows.Length; index++)
        {
            if (string.Equals(rows[index].TableId, roomId, StringComparison.Ordinal))
            {
                return rows[index];
            }
        }

        var held = card;
        return held is not null && string.Equals(held.TableId, roomId, StringComparison.Ordinal) ? held : null;
    }

    public void RefreshCard(string roomId)
    {
        if (roomId.Length == 0 || !session.IsSignedIn || Interlocked.Exchange(ref fetchingCard, 1) != 0)
        {
            return;
        }

        work.Run("table card", async token =>
        {
            var fresh = await casino.TableAsync(roomId, cardStatusSink, token).ConfigureAwait(false);
            if (fresh is not null)
            {
                card = fresh;
            }
        }, () => Interlocked.Exchange(ref fetchingCard, 0));
    }

    public string AccountId => session.CurrentUser?.Id ?? string.Empty;

    public int SeatedAt(string gameKind)
    {
        var directory = tables;
        var seated = 0;
        for (var index = 0; index < directory.Length; index++)
        {
            var row = directory[index];
            if (string.Equals(row.GameKind, gameKind, StringComparison.Ordinal))
            {
                seated += row.SeatedCount;
            }
        }

        return seated;
    }

    public CasinoTableDoorDto? Door => door;

    public bool Loading => loading;

    public bool Loaded => loaded;

    public bool IntentInFlight => intentInFlight;

    public bool TakeTablesFailure()
    {
        return Interlocked.Exchange(ref tablesFailed, 0) != 0;
    }

    public bool TakeIntentFailure()
    {
        return Interlocked.Exchange(ref intentFailed, 0) != 0;
    }

    public CasinoSeatOutcome? TakeSeatOutcome()
    {
        return Interlocked.Exchange(ref seatOutcome, null);
    }

    public CasinoStakeOutcome? TakeNoticeOutcome()
    {
        return Interlocked.Exchange(ref noticeOutcome, null);
    }

    public CasinoQuickSeatDto? TakeQuickSeat()
    {
        return Interlocked.Exchange(ref quickSeat, null);
    }

    public string TakeClosedTable()
    {
        return Interlocked.Exchange(ref closedTableId, string.Empty);
    }

    public string TakeQuickSeatRefusal()
    {
        return Interlocked.Exchange(ref quickSeatRefusal, string.Empty);
    }

    public CasinoTableRowDto? TakeHostedTable()
    {
        return Interlocked.Exchange(ref hostedTable, null);
    }

    public CasinoTableRowDto? TakeResolvedTable()
    {
        return Interlocked.Exchange(ref resolvedTable, null);
    }

    public bool Owns(CasinoTableRowDto table)
    {
        var me = session.CurrentUser?.Id ?? string.Empty;
        return me.Length > 0 && string.Equals(table.OwnerUserId, me, StringComparison.Ordinal);
    }

    public void EnsureFresh()
    {
        if (!session.IsSignedIn || !cadence.Due(DateTime.UtcNow))
        {
            return;
        }

        RefreshTables();
    }

    public void RefreshNow()
    {
        Interlocked.Exchange(ref tablesAttemptedAtTick, 0);
        cadence.Reset();
        RefreshTables();
    }

    public void QuickSeat(int stakeTier)
    {
        if (!Begin())
        {
            return;
        }

        work.Run("quick seat", async token =>
        {
            var answer = await casino.QuickSeatAsync(CasinoWire.BlackjackKind, stakeTier, token)
                .ConfigureAwait(false);
            if (answer is null)
            {
                Interlocked.Exchange(ref intentFailed, 1);
                return;
            }

            if (!answer.Granted)
            {
                Interlocked.Exchange(ref quickSeatRefusal, Named(answer.Reason));
                return;
            }

            Interlocked.Exchange(ref quickSeat, answer);
        }, EndIntent);
    }

    public void CreatePrivateTable(int stakeTier)
    {
        if (!Begin())
        {
            return;
        }

        string clientTableId;
        lock (intentGate)
        {
            if (!ReusesCreate(createIntentTier, stakeTier))
            {
                createIntentId = string.Empty;
            }

            if (createIntentId.Length == 0)
            {
                createIntentId = Guid.NewGuid().ToString("N");
            }

            createIntentTier = stakeTier;
            createIntentConfig = null;
            clientTableId = createIntentId;
        }

        work.Run("create table", async token =>
        {
            var answer = await casino.CreateTableAsync(clientTableId, stakeTier, token)
                .ConfigureAwait(false);
            if (answer is null)
            {
                Interlocked.Exchange(ref intentFailed, 1);
                return;
            }

            ForgetCreateIntent(clientTableId);
            if (!answer.Granted || answer.Table is null)
            {
                Interlocked.Exchange(ref noticeOutcome, new CasinoStakeOutcome(false, Named(answer.Reason)));
                return;
            }

            Interlocked.Exchange(ref hostedTable, answer.Table);
            RefreshNow();
        }, EndIntent);
    }

    public void HostTable(CasinoTableConfigDto config)
    {
        if (!Begin())
        {
            return;
        }

        string clientTableId;
        lock (intentGate)
        {
            if (createIntentConfig is null || createIntentConfig != config || createIntentId.Length == 0)
            {
                createIntentId = Guid.NewGuid().ToString("N");
            }

            createIntentConfig = config;
            createIntentTier = int.MinValue;
            clientTableId = createIntentId;
        }

        work.Run("host table", async token =>
        {
            var answer = await casino.HostTableAsync(clientTableId, config, token).ConfigureAwait(false);
            if (answer is null)
            {
                Interlocked.Exchange(ref intentFailed, 1);
                return;
            }

            ForgetCreateIntent(clientTableId);
            if (!answer.Granted || answer.Table is null)
            {
                Interlocked.Exchange(ref noticeOutcome, new CasinoStakeOutcome(false, Named(answer.Reason)));
                return;
            }

            Interlocked.Exchange(ref hostedTable, answer.Table);
            RefreshNow();
        }, EndIntent);
    }

    public void Rename(string roomId, string name)
    {
        RunHostAction("rename table", roomId, token => casino.RenameTableAsync(roomId, name, token));
    }

    public void SetCoDealers(string roomId, string[] userIds)
    {
        RunHostAction("co-dealers", roomId, token => casino.SetCoDealersAsync(roomId, userIds, token));
    }

    public void Pause(string roomId, bool paused)
    {
        RunHostAction("pause table", roomId, token => casino.PauseTableAsync(roomId, paused, token));
    }

    public void Deal(string roomId)
    {
        RunHostAction("deal", roomId, token => casino.DealAsync(roomId, token));
    }

    public void CloseTable(string roomId)
    {
        if (roomId.Length == 0 || !Begin())
        {
            return;
        }

        work.Run("close table", async token =>
        {
            var answer = await casino.CloseTableAsync(roomId, token).ConfigureAwait(false);
            if (answer is null)
            {
                Interlocked.Exchange(ref intentFailed, 1);
                return;
            }

            Interlocked.Exchange(ref noticeOutcome, new CasinoStakeOutcome(answer.Granted, Named(answer.Reason)));
            if (answer.Granted)
            {
                Interlocked.Exchange(ref closedTableId, roomId);
            }

            RefreshNow();
        }, EndIntent);
    }

    public void Rebuy(string roomId)
    {
        if (roomId.Length == 0 || !Begin())
        {
            return;
        }

        work.Run("rebuy", async token =>
        {
            var answer = await casino.RebuyAsync(roomId, token).ConfigureAwait(false);
            if (answer is null)
            {
                Interlocked.Exchange(ref intentFailed, 1);
                return;
            }

            Interlocked.Exchange(ref noticeOutcome, new CasinoStakeOutcome(answer.Granted, Named(answer.Reason)));
        }, EndIntent);
    }

    public CasinoTableLedgerDto? LedgerFor(string roomId)
    {
        var held = ledger;
        return held is not null && string.Equals(held.TableId, roomId, StringComparison.Ordinal) ? held : null;
    }

    public void RefreshLedgerNow(string roomId)
    {
        if (roomId.Length == 0)
        {
            return;
        }

        ledgerRoomId = roomId;
        Interlocked.Exchange(ref ledgerAttemptedAtTick, 0);
        RefreshLedger(roomId);
    }

    public void EnsureLedgerFresh(string roomId)
    {
        if (roomId.Length == 0 || !session.IsSignedIn)
        {
            return;
        }

        if (!string.Equals(ledgerRoomId, roomId, StringComparison.Ordinal))
        {
            RefreshLedgerNow(roomId);
            return;
        }

        var last = Interlocked.Read(ref ledgerLoadedAtTick);
        if (last != 0 && Environment.TickCount64 - last < LedgerRefreshMilliseconds)
        {
            return;
        }

        RefreshLedger(roomId);
    }

    public void ForgetLedger()
    {
        ledgerRoomId = string.Empty;
        ledger = null;
        Interlocked.Exchange(ref ledgerAttemptedAtTick, 0);
        Interlocked.Exchange(ref ledgerLoadedAtTick, 0);
    }

    public void ProposeLedgerEntry(string roomId, string kind, string counterpartyUserId, long amount)
    {
        if (roomId.Length == 0 || counterpartyUserId.Length == 0 || amount <= 0 || !CasinoLedgerKinds.Proposable(kind)
            || !Begin())
        {
            return;
        }

        string clientEntryId;
        lock (intentGate)
        {
            if (!string.Equals(ledgerIntentKey.Kind, kind, StringComparison.Ordinal)
                || !string.Equals(ledgerIntentKey.Counterparty, counterpartyUserId, StringComparison.Ordinal)
                || ledgerIntentKey.Amount != amount || ledgerIntentId.Length == 0)
            {
                ledgerIntentId = Guid.NewGuid().ToString("N");
            }

            ledgerIntentKey = new LedgerIntent(kind, counterpartyUserId, amount);
            clientEntryId = ledgerIntentId;
        }

        var request = new CasinoLedgerProposeRequest(clientEntryId, kind, counterpartyUserId, amount,
            CasinoLedgerKinds.Manual);
        work.Run("ledger propose", async token =>
        {
            var answer = await casino.ProposeLedgerEntryAsync(roomId, request, token).ConfigureAwait(false);
            if (answer is null)
            {
                Interlocked.Exchange(ref intentFailed, 1);
                return;
            }

            lock (intentGate)
            {
                if (string.Equals(ledgerIntentId, clientEntryId, StringComparison.Ordinal))
                {
                    ledgerIntentId = string.Empty;
                }
            }

            Interlocked.Exchange(ref noticeOutcome, new CasinoStakeOutcome(answer.Granted, Named(answer.Reason)));
            RefreshLedgerNow(roomId);
        }, EndIntent);
    }

    public void ConfirmLedgerEntry(string roomId, string entryId)
    {
        RunLedgerAction("ledger confirm", roomId, entryId,
            token => casino.ConfirmLedgerEntryAsync(entryId, token));
    }

    public void DisputeLedgerEntry(string roomId, string entryId)
    {
        RunLedgerAction("ledger dispute", roomId, entryId,
            token => casino.DisputeLedgerEntryAsync(entryId, token));
    }

    private void RunHostAction(string label, string roomId,
        Func<CancellationToken, Task<CasinoTableActionDto?>> call)
    {
        if (roomId.Length == 0 || !Begin())
        {
            return;
        }

        work.Run(label, async token =>
        {
            var answer = await call(token).ConfigureAwait(false);
            if (answer is null)
            {
                Interlocked.Exchange(ref intentFailed, 1);
                return;
            }

            Interlocked.Exchange(ref noticeOutcome, new CasinoStakeOutcome(answer.Granted, Named(answer.Reason)));
            RefreshDoorNow(roomId);
            RefreshNow();
        }, EndIntent);
    }

    private void RunLedgerAction(string label, string roomId, string entryId,
        Func<CancellationToken, Task<CasinoLedgerResultDto?>> call)
    {
        if (entryId.Length == 0 || !Begin())
        {
            return;
        }

        work.Run(label, async token =>
        {
            var answer = await call(token).ConfigureAwait(false);
            if (answer is null)
            {
                Interlocked.Exchange(ref intentFailed, 1);
                return;
            }

            Interlocked.Exchange(ref noticeOutcome, new CasinoStakeOutcome(answer.Granted, Named(answer.Reason)));
            RefreshLedgerNow(roomId);
        }, EndIntent);
    }

    private void RefreshLedger(string roomId)
    {
        if (!session.IsSignedIn
            || CoolingDown(Interlocked.Read(ref ledgerAttemptedAtTick), Environment.TickCount64)
            || Interlocked.Exchange(ref fetchingLedger, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref ledgerAttemptedAtTick, Environment.TickCount64);
        work.Run("table ledger", async token =>
        {
            var fresh = await casino.TableLedgerAsync(roomId, token).ConfigureAwait(false);
            if (fresh is null)
            {
                return;
            }

            Interlocked.Exchange(ref ledgerAttemptedAtTick, 0);
            Interlocked.Exchange(ref ledgerLoadedAtTick, Environment.TickCount64);
            if (!string.Equals(ledgerRoomId, roomId, StringComparison.Ordinal))
            {
                return;
            }

            ledger = fresh.TableId.Length > 0 ? fresh : fresh with { TableId = roomId };
        }, () => Interlocked.Exchange(ref fetchingLedger, 0));
    }

    public void ResolveToken(string tableId)
    {
        if (tableId.Length == 0 || !Begin())
        {
            return;
        }

        Interlocked.Exchange(ref tableMissing, 0);
        work.Run("resolve table", async token =>
        {
            var answer = await casino.TableAsync(tableId, tableStatusSink, token).ConfigureAwait(false);
            if (answer is null)
            {
                if (Interlocked.Exchange(ref tableMissing, 0) != 0)
                {
                    Interlocked.Exchange(ref noticeOutcome,
                        new CasinoStakeOutcome(false, CasinoReasons.TableClosed));
                    return;
                }

                Interlocked.Exchange(ref intentFailed, 1);
                return;
            }

            if (!answer.Admitted && !Owns(answer))
            {
                Interlocked.Exchange(ref noticeOutcome, new CasinoStakeOutcome(false, Named(answer.Reason)));
                return;
            }

            Interlocked.Exchange(ref resolvedTable, answer);
        }, EndIntent);
    }

    public void JoinByCode(string raw)
    {
        var code = CasinoRoomCodes.Normalized(raw);
        if (code.Length == 0 || !Begin())
        {
            return;
        }

        work.Run("join by code", async token =>
        {
            var answer = await casino.JoinTableAsync(code, token).ConfigureAwait(false);
            if (answer is null)
            {
                Interlocked.Exchange(ref intentFailed, 1);
                return;
            }

            var admitted = answer.Granted && answer.Reason.Length == 0 && answer.Table is not null;
            if (!admitted)
            {
                Interlocked.Exchange(ref noticeOutcome, new CasinoStakeOutcome(false, Named(answer.Reason)));
                RefreshNow();
                return;
            }

            Interlocked.Exchange(ref resolvedTable, answer.Table);
            RefreshNow();
        }, EndIntent);
    }

    public void Knock(string roomId)
    {
        if (roomId.Length == 0 || !Begin())
        {
            return;
        }

        work.Run("knock", async token =>
        {
            var answer = await casino.KnockAsync(roomId, token).ConfigureAwait(false);
            if (answer is null)
            {
                Interlocked.Exchange(ref intentFailed, 1);
                return;
            }

            Interlocked.Exchange(ref noticeOutcome,
                new CasinoStakeOutcome(answer.Granted, Named(answer.Reason)));
        }, EndIntent);
    }

    public void AnswerKnock(string roomId, string userId, bool approve)
    {
        if (roomId.Length == 0 || userId.Length == 0 || !Begin())
        {
            return;
        }

        work.Run("answer knock", async token =>
        {
            var answer = await casino.AnswerKnockAsync(roomId, userId, approve, token).ConfigureAwait(false);
            if (answer is null)
            {
                Interlocked.Exchange(ref intentFailed, 1);
                return;
            }

            Interlocked.Exchange(ref noticeOutcome, new CasinoStakeOutcome(answer.Granted, Named(answer.Reason)));
            RefreshDoorNow(roomId);
        }, EndIntent);
    }

    public void Kick(string roomId, string userId)
    {
        if (roomId.Length == 0 || userId.Length == 0 || !Begin())
        {
            return;
        }

        work.Run("kick", async token =>
        {
            var answer = await casino.KickAsync(roomId, userId, token).ConfigureAwait(false);
            if (answer is null)
            {
                Interlocked.Exchange(ref intentFailed, 1);
                return;
            }

            Interlocked.Exchange(ref noticeOutcome, new CasinoStakeOutcome(answer.Granted, Named(answer.Reason)));
            RefreshDoorNow(roomId);
        }, EndIntent);
    }

    public void Sit(string roomId, int seatIndex, long buyIn)
    {
        if (roomId.Length == 0 || !Begin())
        {
            return;
        }

        string clientSeatId;
        lock (intentGate)
        {
            if (!ReusesSeat(seatIntentSeatIndex, seatIntentBuyIn, seatIndex, buyIn))
            {
                seatIntentId = string.Empty;
            }

            if (seatIntentId.Length == 0)
            {
                seatIntentId = Guid.NewGuid().ToString("N");
            }

            seatIntentSeatIndex = seatIndex;
            seatIntentBuyIn = buyIn;
            clientSeatId = seatIntentId;
        }

        work.Run("sit", async token =>
        {
            var held = chips.State?.TableSitting?.Id ?? string.Empty;
            var sittingId = held.Length > 0 ? held : clientSeatId;
            var answer = await casino.SitAsync(roomId, seatIndex, sittingId, clientSeatId, buyIn, token)
                .ConfigureAwait(false);
            if (answer is null)
            {
                Interlocked.Exchange(ref intentFailed, 1);
                return;
            }

            ForgetSeatIntent(clientSeatId);
            Interlocked.Exchange(ref seatOutcome,
                new CasinoSeatOutcome(answer.Granted, Named(answer.Reason), false, false));
            chips.RefreshNow();
        }, EndIntent);
    }

    public void Stand(string roomId)
    {
        if (roomId.Length == 0 || !Begin())
        {
            return;
        }

        work.Run("stand", async token =>
        {
            var answer = await casino.StandAsync(roomId, token).ConfigureAwait(false);
            if (answer is null)
            {
                Interlocked.Exchange(ref intentFailed, 1);
                return;
            }

            var atHandEnd = string.Equals(answer.Reason, CasinoReasons.AtHandEnd, StringComparison.Ordinal);
            Interlocked.Exchange(ref seatOutcome, new CasinoSeatOutcome(answer.Granted,
                atHandEnd ? string.Empty : Named(answer.Reason), false, atHandEnd));
            chips.RefreshNow();
        }, EndIntent);
    }

    public void Abandon(string roomId)
    {
        if (roomId.Length == 0)
        {
            return;
        }

        work.Run("abandon", async token =>
        {
            var answer = await casino.StandAsync(roomId, token).ConfigureAwait(false);
            if (answer is not null)
            {
                chips.RefreshNow();
            }
        });
    }

    public void RefreshDoorNow(string roomId)
    {
        if (roomId.Length == 0)
        {
            return;
        }

        doorRoomId = roomId;
        Interlocked.Exchange(ref doorAttemptedAtTick, 0);
        RefreshDoor(roomId);
    }

    public void ForgetDoor()
    {
        doorRoomId = string.Empty;
        door = null;
        Interlocked.Exchange(ref doorAttemptedAtTick, 0);
    }

    public CasinoTableDoorDto? DoorFor(string roomId)
    {
        var held = door;
        return held is not null && string.Equals(held.RoomId, roomId, StringComparison.Ordinal) ? held : null;
    }

    internal static bool CoolingDown(long attemptedAtTick, long nowTick)
    {
        return attemptedAtTick != 0 && nowTick - attemptedAtTick < RetryAfterAttemptMilliseconds;
    }

    internal static string Named(string reason)
    {
        return reason.Length > 0 ? reason : CasinoReasons.Unreachable;
    }

    internal static bool ReusesSeat(int heldSeatIndex, long heldBuyIn, int seatIndex, long buyIn)
    {
        return heldSeatIndex == seatIndex && heldBuyIn == buyIn;
    }

    internal static bool ReusesCreate(int heldStakeTier, int stakeTier)
    {
        return heldStakeTier == stakeTier;
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

    private void OnTableStatus(int statusCode)
    {
        if (statusCode == NotFoundStatus)
        {
            Interlocked.Exchange(ref tableMissing, 1);
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
        listed = Array.Empty<CasinoTableRowDto>();
        loaded = false;
        door = null;
        doorRoomId = string.Empty;
        Interlocked.Exchange(ref quickSeat, null);
        Interlocked.Exchange(ref hostedTable, null);
        Interlocked.Exchange(ref resolvedTable, null);
        Interlocked.Exchange(ref seatOutcome, null);
        Interlocked.Exchange(ref noticeOutcome, null);
        Interlocked.Exchange(ref closedTableId, string.Empty);
        Interlocked.Exchange(ref quickSeatRefusal, string.Empty);
        Interlocked.Exchange(ref tablesFailed, 0);
        Interlocked.Exchange(ref intentFailed, 0);
        Interlocked.Exchange(ref tablesAttemptedAtTick, 0);
        Interlocked.Exchange(ref doorAttemptedAtTick, 0);
        lock (intentGate)
        {
            seatIntentId = string.Empty;
            seatIntentSeatIndex = -1;
            seatIntentBuyIn = -1;
            createIntentId = string.Empty;
            createIntentTier = int.MinValue;
            createIntentConfig = null;
            ledgerIntentId = string.Empty;
        }

        ledger = null;
        card = null;
        ledgerRoomId = string.Empty;
        Interlocked.Exchange(ref ledgerAttemptedAtTick, 0);
        Interlocked.Exchange(ref ledgerLoadedAtTick, 0);
        cadence.Reset();
    }

    private void RefreshTables()
    {
        if (!session.IsSignedIn
            || CoolingDown(Interlocked.Read(ref tablesAttemptedAtTick), Environment.TickCount64)
            || Interlocked.Exchange(ref fetchingTables, 1) != 0)
        {
            return;
        }

        loading = true;
        Interlocked.Exchange(ref tablesAttemptedAtTick, Environment.TickCount64);
        work.Run("tables directory", async token =>
        {
            var directory = await casino.TablesAsync(string.Empty, token).ConfigureAwait(false);
            if (directory is null)
            {
                Interlocked.Exchange(ref tablesFailed, 1);
                return;
            }

            Interlocked.Exchange(ref tablesAttemptedAtTick, 0);
            var rows = directory.Tables ?? Array.Empty<CasinoTableRowDto>();
            listed = rows;
            tables = CasinoTableFilters.OfKind(rows, CasinoWire.BlackjackKind);
            loaded = true;
        }, () =>
        {
            loading = false;
            Interlocked.Exchange(ref fetchingTables, 0);
        });
    }

    private void RefreshDoor(string roomId)
    {
        if (!session.IsSignedIn
            || CoolingDown(Interlocked.Read(ref doorAttemptedAtTick), Environment.TickCount64)
            || Interlocked.Exchange(ref fetchingDoor, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref doorAttemptedAtTick, Environment.TickCount64);
        work.Run("table door", async token =>
        {
            var fresh = await casino.TableDoorAsync(roomId, token).ConfigureAwait(false);
            if (fresh is null)
            {
                return;
            }

            Interlocked.Exchange(ref doorAttemptedAtTick, 0);
            if (!string.Equals(doorRoomId, roomId, StringComparison.Ordinal))
            {
                return;
            }

            door = fresh;
        }, () => Interlocked.Exchange(ref fetchingDoor, 0));
    }

    private void ForgetSeatIntent(string intentId)
    {
        lock (intentGate)
        {
            if (string.Equals(seatIntentId, intentId, StringComparison.Ordinal))
            {
                seatIntentId = string.Empty;
                seatIntentSeatIndex = -1;
                seatIntentBuyIn = -1;
            }
        }
    }

    private void ForgetCreateIntent(string intentId)
    {
        lock (intentGate)
        {
            if (string.Equals(createIntentId, intentId, StringComparison.Ordinal))
            {
                createIntentId = string.Empty;
                createIntentTier = int.MinValue;
                createIntentConfig = null;
            }
        }
    }

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        work.Dispose();
    }
}
