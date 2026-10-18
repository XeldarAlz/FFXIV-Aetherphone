using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Runtime;
using Aetherphone.Core.Telephony.Contracts;

namespace Aetherphone.Core.Casino;

internal sealed record CasinoSeatOutcome(bool Granted, string Reason, bool JoinsNextHand, bool AtHandEnd);

internal readonly record struct LedgerIntent(string Kind, string Counterparty, long Amount, bool Guest = false);

internal sealed class CasinoTablesStore : IDisposable
{
    private static readonly TimeSpan ForegroundPollInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan BackgroundPollInterval = TimeSpan.FromSeconds(120);
    private const long RetryAfterAttemptMilliseconds = 30_000;
    private const long LedgerRetryMilliseconds = 3_000;
    private const long LedgerRefreshMilliseconds = 5_000;
    private const long DoorRefreshMilliseconds = 5_000;
    private const long KnockRefreshMilliseconds = 3_000;
    private const long AwayKnockRefreshMilliseconds = 15_000;
    private const long AttentionMilliseconds = 1_500;
    private const int NotFoundStatus = 404;

    private readonly AethernetSession session;
    private readonly CasinoClient casino;
    private readonly CasinoStore chips;
    private readonly PollCadence cadence;
    private readonly StoreWork work = new("CasinoTables");
    private readonly BookLedgerAdmission bookLedgerAdmission = new();
    private readonly object intentGate = new();
    private readonly Action<int> tableStatusSink;

    private volatile CasinoTableRowDto[] tables = Array.Empty<CasinoTableRowDto>();
    private volatile CasinoTableRowDto[] listed = Array.Empty<CasinoTableRowDto>();
    private volatile CasinoTableDoorDto? door;
    private readonly object ledgerGate = new();
    private Dictionary<string, LedgerSlot> ledgerSlots = new(StringComparer.Ordinal);
    private int ledgerVersion;
    private volatile string attendedTableId = string.Empty;
    private long attendedTableAtTick;
    private volatile CasinoTableRowDto? card;
    private readonly Action<int> cardStatusSink;
    private int fetchingCard;
    private string cardRefetchId = string.Empty;
    private long cardAtTick;
    private long listedAtTick;
    private int cardMissing;
    private string vanishedTableId = string.Empty;
    private readonly Action<int> knockStatusSink;
    private volatile CasinoKnockStatus knock = CasinoKnockStatus.Idle;
    private int fetchingKnock;
    private int knockMissing;
    private long knockReadAtTick;
    private long knockWatchedAtTick;
    private long doorWatchedAtTick;
    private volatile string doorWatchedRoomId = string.Empty;
    private CasinoTableConfigDto? createIntentConfig;
    private string ledgerIntentId = string.Empty;
    private LedgerIntent ledgerIntentKey;
    private volatile CasinoLedgerEntryDto[] myEntries = Array.Empty<CasinoLedgerEntryDto>();
    private volatile bool myLedgerLoaded;
    private int fetchingMyLedger;
    private long myLedgerAttemptedAtTick;
    private long myLedgerLoadedAtTick;
    private volatile CasinoQuickSeatDto? quickSeat;
    private volatile CasinoTableRowDto? hostedTable;
    private volatile CasinoTableRowDto? resolvedTable;
    private readonly CasinoLaunchResolve launchResolve = new();
    private volatile bool loading;
    private volatile bool loaded;
    private volatile bool intentInFlight;
    private CasinoSeatOutcome? seatOutcome;
    private CasinoStakeOutcome? noticeOutcome;
    private string closedTableId = string.Empty;
    private string retiredTableId = string.Empty;
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
    private long doorLoadedAtTick;
    private string? lastAccountId;

    public CasinoTablesStore(AethernetSession session, CasinoClient casino, CasinoStore chips,
        PhoneVisibility visibility)
    {
        this.session = session;
        this.casino = casino;
        this.chips = chips;
        cadence = new PollCadence(visibility, ForegroundPollInterval, BackgroundPollInterval);
        tableStatusSink = OnTableStatus;
        cardStatusSink = OnCardStatus;
        knockStatusSink = OnKnockStatus;
        session.Changed += OnSessionChanged;
    }

    public CasinoTableRowDto[] Tables => tables;

    public CasinoTableRowDto[] Listed => listed;

    public CasinoTableRowDto? CardFor(string roomId) =>
        CardIn(listed, Interlocked.Read(ref listedAtTick), card, Interlocked.Read(ref cardAtTick), roomId);

    internal static CasinoTableRowDto? CardIn(CasinoTableRowDto[] rows, long rowsAtTick, CasinoTableRowDto? held,
        long heldAtTick, string roomId)
    {
        if (roomId.Length == 0)
        {
            return null;
        }

        var holds = held is not null && string.Equals(held.TableId, roomId, StringComparison.Ordinal);
        if (holds && heldAtTick >= rowsAtTick)
        {
            return held;
        }

        for (var index = 0; index < rows.Length; index++)
        {
            if (string.Equals(rows[index].TableId, roomId, StringComparison.Ordinal))
            {
                return rows[index];
            }
        }

        return holds ? held : null;
    }

    private void HoldCard(CasinoTableRowDto fresh)
    {
        card = fresh;
        Interlocked.Exchange(ref cardAtTick, Environment.TickCount64);
    }

    public void RefreshCard(string roomId)
    {
        if (roomId.Length == 0 || !session.IsSignedIn)
        {
            return;
        }

        if (Interlocked.Exchange(ref fetchingCard, 1) != 0)
        {
            Interlocked.Exchange(ref cardRefetchId, roomId);
            return;
        }

        Interlocked.Exchange(ref cardMissing, 0);
        work.Run("table card", async token =>
        {
            var fresh = await casino.TableAsync(roomId, cardStatusSink, token).ConfigureAwait(false);
            if (fresh is not null)
            {
                HoldCard(fresh);
                return;
            }

            if (Interlocked.Exchange(ref cardMissing, 0) == 0)
            {
                return;
            }

            Vanished(roomId);
        }, CardFetched);
    }

    private void CardFetched()
    {
        Interlocked.Exchange(ref fetchingCard, 0);
        var queued = Interlocked.Exchange(ref cardRefetchId, string.Empty);
        if (queued.Length > 0)
        {
            RefreshCard(queued);
        }
    }

    internal static bool LostOwnership(CasinoTableDoorDto fresh, CasinoTableRowDto? held, string accountId)
    {
        return !fresh.Owner && held is not null && accountId.Length > 0
            && string.Equals(held.OwnerUserId, accountId, StringComparison.Ordinal);
    }

    private void Vanished(string roomId)
    {
        ForgetCard(roomId);
        Interlocked.Exchange(ref vanishedTableId, roomId);
        RefreshNow();
    }

    private void Retire(string roomId)
    {
        Interlocked.Exchange(ref retiredTableId, roomId);
        listed = CasinoTableFilters.Without(listed, roomId);
        tables = CasinoTableFilters.Without(tables, roomId);
    }

    private void ForgetCard(string roomId)
    {
        var held = card;
        if (held is not null && string.Equals(held.TableId, roomId, StringComparison.Ordinal))
        {
            card = null;
        }
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

    public string TakeVanishedTable()
    {
        return Interlocked.Exchange(ref vanishedTableId, string.Empty);
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

            Hosted(answer.Table);
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

            Hosted(answer.Table);
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
                ForgetCard(roomId);
                Retire(roomId);
                Interlocked.Exchange(ref closedTableId, roomId);
                RefreshNow();
                return;
            }

            RefreshCard(roomId);
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

    private sealed class LedgerSlot
    {
        public CasinoTableLedgerDto? Ledger;
        public long AttemptedAtTick;
        public long LoadedAtTick;
        public bool Fetching;
        public bool Refetch;
    }

    public CasinoTableLedgerDto? LedgerFor(string tableId)
    {
        if (tableId.Length == 0)
        {
            return null;
        }

        lock (ledgerGate)
        {
            return ledgerSlots.TryGetValue(tableId, out var slot) ? slot.Ledger : null;
        }
    }

    public int LedgerVersion => Volatile.Read(ref ledgerVersion);

    public void AttendTable(string tableId)
    {
        if (tableId.Length == 0)
        {
            return;
        }

        attendedTableId = tableId;
        Interlocked.Exchange(ref attendedTableAtTick, Environment.TickCount64);
    }

    public string AttendedTableId(long nowTick)
    {
        var tableId = attendedTableId;
        return tableId.Length > 0 && Attended(Interlocked.Read(ref attendedTableAtTick), nowTick)
            ? tableId
            : string.Empty;
    }

    public CasinoTableActionDto? JoinBookLedger(string roomId, bool retry = false)
    {
        var accountId = AccountId;
        var attempt = bookLedgerAdmission.Begin(roomId, accountId, retry);
        if (attempt is not null)
        {
            work.Run("join book ledger", async token =>
            {
                var answer = await casino.JoinBookLedgerAsync(roomId, token).ConfigureAwait(false);
                if (!string.Equals(accountId, AccountId, StringComparison.Ordinal)
                    || !bookLedgerAdmission.Complete(attempt, answer))
                {
                    return;
                }

                if (answer is { Granted: true })
                {
                    RefreshCard(roomId);
                    RefreshLedgerNow(roomId);
                }
            }, () => bookLedgerAdmission.Complete(attempt, null));
        }

        return bookLedgerAdmission.For(roomId, accountId);
    }

    public void ForgetBookLedgerAdmission()
    {
        bookLedgerAdmission.Reset();
    }

    public void RefreshLedgerNow(string tableId)
    {
        if (tableId.Length == 0 || !session.IsSignedIn)
        {
            return;
        }

        FetchLedger(tableId);
    }

    public void EnsureLedgerFresh(string tableId)
    {
        if (tableId.Length == 0 || !session.IsSignedIn)
        {
            return;
        }

        var now = Environment.TickCount64;
        lock (ledgerGate)
        {
            var slot = SlotFor(tableId);
            if (slot.Fetching || Within(slot.AttemptedAtTick, now, LedgerRetryMilliseconds)
                || Within(slot.LoadedAtTick, now, LedgerRefreshMilliseconds))
            {
                return;
            }
        }

        FetchLedger(tableId);
    }

    public void UpsertLedgerEntry(string tableId, CasinoLedgerEntryDto entry)
    {
        if (entry.EntryId.Length == 0)
        {
            return;
        }

        var accountId = AccountId;
        lock (ledgerGate)
        {
            if (tableId.Length > 0 && ledgerSlots.TryGetValue(tableId, out var slot) && slot.Ledger is { } held)
            {
                slot.Ledger = held with
                {
                    Entries = LedgerEntryPatch.Upsert(held.Entries ?? Array.Empty<CasinoLedgerEntryDto>(), entry,
                        false),
                };
                ledgerVersion++;
            }

            var mine = myEntries;
            if (myLedgerLoaded && (LedgerEntryPatch.IndexOf(mine, entry.EntryId) >= 0
                    || CasinoGilLedger.SideOf(entry, accountId) != LedgerSide.None))
            {
                myEntries = LedgerEntryPatch.Upsert(mine, entry, true);
            }
        }
    }

    public void RemoveLedgerEntry(string tableId, string entryId)
    {
        if (entryId.Length == 0)
        {
            return;
        }

        lock (ledgerGate)
        {
            if (tableId.Length > 0 && ledgerSlots.TryGetValue(tableId, out var slot) && slot.Ledger is { } held
                && held.Entries is { } entries)
            {
                slot.Ledger = held with { Entries = LedgerEntryPatch.Remove(entries, entryId) };
                ledgerVersion++;
            }

            myEntries = LedgerEntryPatch.Remove(myEntries, entryId);
        }
    }

    private LedgerSlot SlotFor(string tableId)
    {
        if (!ledgerSlots.TryGetValue(tableId, out var slot))
        {
            slot = new LedgerSlot();
            ledgerSlots[tableId] = slot;
        }

        return slot;
    }

    private static bool Within(long stampTick, long nowTick, long windowMilliseconds)
    {
        return stampTick != 0 && nowTick - stampTick < windowMilliseconds;
    }

    private void FetchLedger(string tableId)
    {
        LedgerSlot slot;
        Dictionary<string, LedgerSlot> slots;
        lock (ledgerGate)
        {
            slots = ledgerSlots;
            slot = SlotFor(tableId);
            if (slot.Fetching)
            {
                slot.Refetch = true;
                return;
            }

            slot.Fetching = true;
            slot.Refetch = false;
            slot.AttemptedAtTick = Environment.TickCount64;
        }

        work.Run("table ledger", async token =>
        {
            var fresh = await casino.TableLedgerAsync(tableId, token).ConfigureAwait(false);
            if (fresh is null)
            {
                return;
            }

            lock (ledgerGate)
            {
                if (!ReferenceEquals(slots, ledgerSlots))
                {
                    return;
                }

                slot.AttemptedAtTick = 0;
                slot.LoadedAtTick = Environment.TickCount64;
                slot.Ledger = fresh.TableId.Length > 0 ? fresh : fresh with { TableId = tableId };
                ledgerVersion++;
            }
        }, () => LedgerFetched(tableId, slot, slots));
    }

    private void LedgerFetched(string tableId, LedgerSlot slot, Dictionary<string, LedgerSlot> slots)
    {
        bool again;
        lock (ledgerGate)
        {
            slot.Fetching = false;
            again = slot.Refetch && ReferenceEquals(slots, ledgerSlots);
            slot.Refetch = false;
        }

        if (again)
        {
            FetchLedger(tableId);
        }
    }

    public CasinoLedgerEntryDto[] MyEntries => myEntries;

    public bool MyLedgerLoaded => myLedgerLoaded;

    public void EnsureMyLedgerFresh()
    {
        var last = Interlocked.Read(ref myLedgerLoadedAtTick);
        if (last != 0 && Environment.TickCount64 - last < LedgerRefreshMilliseconds)
        {
            return;
        }

        RefreshMyLedger();
    }

    public void RefreshMyLedgerNow()
    {
        Interlocked.Exchange(ref myLedgerAttemptedAtTick, 0);
        Interlocked.Exchange(ref myLedgerLoadedAtTick, 0);
        RefreshMyLedger();
    }

    private void RefreshMyLedger()
    {
        if (!session.IsSignedIn
            || LedgerCoolingDown(Interlocked.Read(ref myLedgerAttemptedAtTick), Environment.TickCount64)
            || Interlocked.Exchange(ref fetchingMyLedger, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref myLedgerAttemptedAtTick, Environment.TickCount64);
        var account = session.CurrentUser?.Id;
        work.Run("my gil ledger", async token =>
        {
            var fresh = await casino.MyLedgerAsync(token).ConfigureAwait(false);
            if (fresh is null || !string.Equals(account, session.CurrentUser?.Id, StringComparison.Ordinal))
            {
                return;
            }

            Interlocked.Exchange(ref myLedgerAttemptedAtTick, 0);
            Interlocked.Exchange(ref myLedgerLoadedAtTick, Environment.TickCount64);
            lock (ledgerGate)
            {
                myEntries = fresh.Entries ?? Array.Empty<CasinoLedgerEntryDto>();
                myLedgerLoaded = true;
            }
        }, () => Interlocked.Exchange(ref fetchingMyLedger, 0));
    }

    public void RecordEntry(string tableId, string kind, string counterpartyUserId, string guestName, long amount)
    {
        var guest = counterpartyUserId.Length == 0;
        var name = guest ? CasinoBookRules.NormalizeGuestName(guestName) : string.Empty;
        if (tableId.Length == 0 || amount <= 0 || amount > CasinoHostingRules.MaxGil
            || !CasinoLedgerKinds.Proposable(kind) || (guest && !CasinoBookRules.IsGuestName(name)) || !Begin())
        {
            return;
        }

        var clientEntryId = LedgerIntentIdFor(new LedgerIntent(kind, guest ? name : counterpartyUserId, amount, guest));
        var request = new CasinoLedgerProposeRequest(clientEntryId, kind, counterpartyUserId, amount,
            CasinoLedgerKinds.Manual, name);
        work.Run("ledger record", async token =>
        {
            var answer = await casino.ProposeLedgerEntryAsync(tableId, request, token).ConfigureAwait(false);
            if (answer is null)
            {
                Interlocked.Exchange(ref intentFailed, 1);
                return;
            }

            ForgetLedgerIntent(clientEntryId);
            Interlocked.Exchange(ref noticeOutcome, new CasinoStakeOutcome(answer.Granted, Named(answer.Reason)));
            AbsorbLedgerAnswer(tableId, string.Empty, answer);
        }, EndIntent);
    }

    private string LedgerIntentIdFor(in LedgerIntent key)
    {
        lock (intentGate)
        {
            if (ledgerIntentKey != key || ledgerIntentId.Length == 0)
            {
                ledgerIntentId = Guid.NewGuid().ToString("N");
            }

            ledgerIntentKey = key;
            return ledgerIntentId;
        }
    }

    private void ForgetLedgerIntent(string intentId)
    {
        lock (intentGate)
        {
            if (string.Equals(ledgerIntentId, intentId, StringComparison.Ordinal))
            {
                ledgerIntentId = string.Empty;
            }
        }
    }

    public void RequestSettle(string tableId, string entryId)
    {
        RunLedgerAction("ledger settle", entryId, async token =>
        {
            var answer = await casino.SettleLedgerEntryAsync(entryId, token).ConfigureAwait(false);
            AbsorbLedgerAnswer(tableId, entryId, answer);
            return answer;
        });
    }

    public void RequestRemove(string tableId, string entryId)
    {
        RunLedgerAction("ledger remove", entryId, token => RemoveEntryAsync(tableId, entryId, token));
    }

    public async Task<CasinoLedgerResultDto?> RemoveEntryAsync(string tableId, string entryId,
        CancellationToken token)
    {
        var answer = await casino.RemoveLedgerEntryAsync(entryId, token).ConfigureAwait(false);
        AbsorbLedgerAnswer(tableId, entryId, answer);
        return answer;
    }

    private void AbsorbLedgerAnswer(string tableId, string entryId, CasinoLedgerResultDto? answer)
    {
        if (answer is null)
        {
            return;
        }

        var entry = answer.Entry;
        var table = tableId.Length > 0 ? tableId : entry?.TableId ?? string.Empty;
        if (answer.Granted && entry is not null)
        {
            UpsertLedgerEntry(table, entry);
        }
        else if (answer.Granted)
        {
            RemoveLedgerEntry(table, entryId);
        }

        RefreshLedgerNow(table);
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

    private void RunLedgerAction(string label, string entryId,
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
        }, EndIntent);
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

            if (!answer.Admitted && !Owns(answer) && !CasinoKnocks.AtTheDoor(answer.Reason))
            {
                Interlocked.Exchange(ref noticeOutcome, new CasinoStakeOutcome(false, Named(answer.Reason)));
                return;
            }

            Resolved(answer, answer.Admitted ? string.Empty : answer.Reason);
        }, EndIntent);
    }

    private void Resolved(CasinoTableRowDto table, string doorReason)
    {
        Admit(table, doorReason);
        Interlocked.Exchange(ref resolvedTable, table);
    }

    private void Admit(CasinoTableRowDto table, string doorReason)
    {
        HoldCard(table);
        if (doorReason.Length > 0)
        {
            knock = new CasinoKnockStatus(table.TableId, CasinoKnocks.FromDecline(doorReason), doorReason);
            Interlocked.Exchange(ref knockReadAtTick, Environment.TickCount64);
        }
    }

    public bool ResolveLaunch(string tableId)
    {
        if (tableId.Length == 0 || !Begin())
        {
            return false;
        }

        var asked = launchResolve.Begin();
        Interlocked.Exchange(ref tableMissing, 0);
        work.Run("resolve launched table", async token =>
        {
            var answer = await casino.TableAsync(tableId, tableStatusSink, token).ConfigureAwait(false);
            if (answer is null)
            {
                var reason = Interlocked.Exchange(ref tableMissing, 0) != 0
                    ? CasinoReasons.TableClosed
                    : CasinoReasons.Unreachable;
                launchResolve.Complete(asked, new CasinoLaunchAnswer(tableId, null, reason));
                return;
            }

            if (!answer.Admitted && !Owns(answer) && !CasinoKnocks.AtTheDoor(answer.Reason))
            {
                launchResolve.Complete(asked, new CasinoLaunchAnswer(tableId, null, Named(answer.Reason)));
                return;
            }

            launchResolve.Complete(asked, new CasinoLaunchAnswer(tableId, answer, string.Empty));
        }, EndIntent);
        return true;
    }

    public bool TryTakeLaunchAnswer(out CasinoLaunchAnswer answer)
    {
        if (!launchResolve.TryTake(out answer))
        {
            return false;
        }

        if (answer.Table is { } table)
        {
            Admit(table, table.Admitted ? string.Empty : table.Reason);
        }

        return true;
    }

    public void ForgetLaunch() => launchResolve.Forget();

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
            if (admitted || (answer.Table is not null && CasinoKnocks.AtTheDoor(answer.Reason)))
            {
                Resolved(answer.Table!, admitted ? string.Empty : answer.Reason);
                RefreshNow();
                return;
            }

            Interlocked.Exchange(ref noticeOutcome, new CasinoStakeOutcome(false, Named(answer.Reason)));
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

            var held = knock;
            var current = string.Equals(held.RoomId, roomId, StringComparison.Ordinal) ? held.Stage : KnockStage.None;
            knock = new CasinoKnockStatus(roomId, CasinoKnocks.FromKnock(current, answer.Granted, answer.Reason),
                Named(answer.Reason));
            Interlocked.Exchange(ref knockReadAtTick, Environment.TickCount64);
        }, EndIntent);
    }

    public CasinoKnockStatus KnockFor(string roomId, string declineReason)
    {
        var held = knock;
        if (string.Equals(held.RoomId, roomId, StringComparison.Ordinal))
        {
            return held;
        }

        var seeded = new CasinoKnockStatus(roomId, CasinoKnocks.FromDecline(declineReason), declineReason);
        knock = seeded;
        Interlocked.Exchange(ref knockReadAtTick, Environment.TickCount64);
        return seeded;
    }

    public CasinoKnockStatus HeldKnock => knock;

    public bool KnockAttended(long nowTick) => Attended(Interlocked.Read(ref knockWatchedAtTick), nowTick);

    public bool DoorAttended(string roomId, long nowTick) =>
        string.Equals(doorWatchedRoomId, roomId, StringComparison.Ordinal)
        && Attended(Interlocked.Read(ref doorWatchedAtTick), nowTick);

    internal static bool Attended(long watchedAtTick, long nowTick)
    {
        return watchedAtTick != 0 && nowTick - watchedAtTick <= AttentionMilliseconds;
    }

    public string HostedTableId()
    {
        var rows = listed;
        for (var index = 0; index < rows.Length; index++)
        {
            if (rows[index].Kind == CasinoTableKinds.Private && Owns(rows[index]))
            {
                return rows[index].TableId;
            }
        }

        var held = card;
        return held is not null && held.Kind == CasinoTableKinds.Private && Owns(held) ? held.TableId : string.Empty;
    }

    public void EnsureKnockFresh(string roomId)
    {
        Interlocked.Exchange(ref knockWatchedAtTick, Environment.TickCount64);
        RefreshKnock(roomId, KnockRefreshMilliseconds);
    }

    public void PollWaitingKnock()
    {
        var held = knock;
        if (held.Stage == KnockStage.Waiting)
        {
            RefreshKnock(held.RoomId, AwayKnockRefreshMilliseconds);
        }
    }

    private void RefreshKnock(string roomId, long intervalMilliseconds)
    {
        var held = knock;
        if (held.Stage != KnockStage.Waiting || !string.Equals(held.RoomId, roomId, StringComparison.Ordinal)
            || !session.IsSignedIn
            || Environment.TickCount64 - Interlocked.Read(ref knockReadAtTick) < intervalMilliseconds
            || Interlocked.Exchange(ref fetchingKnock, 1) != 0)
        {
            return;
        }

        Interlocked.Exchange(ref knockReadAtTick, Environment.TickCount64);
        Interlocked.Exchange(ref knockMissing, 0);
        work.Run("knock answer", async token =>
        {
            var fresh = await casino.TableAsync(roomId, knockStatusSink, token).ConfigureAwait(false);
            var watched = knock;
            if (watched.Stage != KnockStage.Waiting
                || !string.Equals(watched.RoomId, roomId, StringComparison.Ordinal))
            {
                return;
            }

            if (fresh is null)
            {
                if (Interlocked.Exchange(ref knockMissing, 0) != 0)
                {
                    knock = new CasinoKnockStatus(roomId, KnockStage.Refused, CasinoReasons.Ended);
                    Vanished(roomId);
                }

                return;
            }

            var stage = CasinoKnocks.FromCard(watched.Stage, fresh.Admitted, fresh.Reason);
            if (stage == KnockStage.Admitted)
            {
                HoldCard(fresh);
            }

            if (stage != watched.Stage)
            {
                knock = new CasinoKnockStatus(roomId, stage, fresh.Reason);
            }
        }, () => Interlocked.Exchange(ref fetchingKnock, 0));
    }

    public bool TakeKnockAdmitted(string roomId)
    {
        var held = knock;
        if (held.Stage != KnockStage.Admitted || !string.Equals(held.RoomId, roomId, StringComparison.Ordinal))
        {
            return false;
        }

        knock = CasinoKnockStatus.Idle;
        return true;
    }

    public void ForgetKnock()
    {
        if (knock.Stage == KnockStage.Waiting)
        {
            return;
        }

        knock = CasinoKnockStatus.Idle;
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

    public void EnsureDoorFresh(string roomId)
    {
        if (roomId.Length == 0 || !session.IsSignedIn)
        {
            return;
        }

        doorWatchedRoomId = roomId;
        Interlocked.Exchange(ref doorWatchedAtTick, Environment.TickCount64);
        if (!string.Equals(doorRoomId, roomId, StringComparison.Ordinal))
        {
            RefreshDoorNow(roomId);
            return;
        }

        var last = Interlocked.Read(ref doorLoadedAtTick);
        if (last != 0 && Environment.TickCount64 - last < DoorRefreshMilliseconds)
        {
            return;
        }

        RefreshDoor(roomId);
    }

    public void ForgetDoor()
    {
        doorRoomId = string.Empty;
        door = null;
        Interlocked.Exchange(ref doorAttemptedAtTick, 0);
        Interlocked.Exchange(ref doorLoadedAtTick, 0);
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

    internal static bool LedgerCoolingDown(long attemptedAtTick, long nowTick)
    {
        return attemptedAtTick != 0 && nowTick - attemptedAtTick < LedgerRetryMilliseconds;
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

    private void Hosted(CasinoTableRowDto table)
    {
        HoldCard(table);
        Interlocked.Exchange(ref hostedTable, table);
        RefreshNow();
    }

    private void OnTableStatus(int statusCode)
    {
        if (statusCode == NotFoundStatus)
        {
            Interlocked.Exchange(ref tableMissing, 1);
        }
    }

    private void OnCardStatus(int statusCode)
    {
        if (statusCode == NotFoundStatus)
        {
            Interlocked.Exchange(ref cardMissing, 1);
        }
    }

    private void OnKnockStatus(int statusCode)
    {
        if (statusCode == NotFoundStatus)
        {
            Interlocked.Exchange(ref knockMissing, 1);
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
        bookLedgerAdmission.Reset();
        tables = Array.Empty<CasinoTableRowDto>();
        listed = Array.Empty<CasinoTableRowDto>();
        loaded = false;
        door = null;
        doorRoomId = string.Empty;
        Interlocked.Exchange(ref quickSeat, null);
        Interlocked.Exchange(ref hostedTable, null);
        Interlocked.Exchange(ref resolvedTable, null);
        launchResolve.Forget();
        Interlocked.Exchange(ref seatOutcome, null);
        Interlocked.Exchange(ref noticeOutcome, null);
        Interlocked.Exchange(ref closedTableId, string.Empty);
        Interlocked.Exchange(ref retiredTableId, string.Empty);
        Interlocked.Exchange(ref vanishedTableId, string.Empty);
        Interlocked.Exchange(ref cardMissing, 0);
        Interlocked.Exchange(ref cardRefetchId, string.Empty);
        Interlocked.Exchange(ref knockMissing, 0);
        knock = CasinoKnockStatus.Idle;
        Interlocked.Exchange(ref quickSeatRefusal, string.Empty);
        Interlocked.Exchange(ref tablesFailed, 0);
        Interlocked.Exchange(ref intentFailed, 0);
        Interlocked.Exchange(ref tablesAttemptedAtTick, 0);
        Interlocked.Exchange(ref doorAttemptedAtTick, 0);
        lock (ledgerGate)
        {
            ledgerSlots = new Dictionary<string, LedgerSlot>(StringComparer.Ordinal);
            ledgerVersion++;
            myEntries = Array.Empty<CasinoLedgerEntryDto>();
            myLedgerLoaded = false;
        }

        attendedTableId = string.Empty;
        Interlocked.Exchange(ref attendedTableAtTick, 0);
        Interlocked.Exchange(ref myLedgerAttemptedAtTick, 0);
        Interlocked.Exchange(ref myLedgerLoadedAtTick, 0);
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

        card = null;
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
            var rows = CasinoTableFilters.Without(directory.Tables ?? Array.Empty<CasinoTableRowDto>(),
                Volatile.Read(ref retiredTableId));
            listed = rows;
            Interlocked.Exchange(ref listedAtTick, Environment.TickCount64);
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
            Interlocked.Exchange(ref doorLoadedAtTick, Environment.TickCount64);
            if (LostOwnership(fresh, CardFor(roomId), AccountId))
            {
                RefreshCard(roomId);
            }
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
