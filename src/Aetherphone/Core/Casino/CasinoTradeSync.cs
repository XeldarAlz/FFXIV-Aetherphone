using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Casino;

internal sealed record TradeRecorded(CasinoLedgerEntryDto Entry, string PartnerName, long Received, long Sent,
    long ShownAtTick)
{
    public bool Incoming => Received > Sent;

    public long Amount => Math.Abs(Received - Sent);
}

internal sealed record TradeRecordAnswer(CasinoLedgerTradeRequest Request, string AccountId,
    CasinoLedgerResultDto? Answer);

internal sealed class CasinoTradeSync : IDisposable
{
    public const long RecordedShownMilliseconds = 8_000;

    public const long HostedWindowMilliseconds = CasinoHostingRules.TradeClosedTableHours * 3_600_000L;

    private const long HostingCheckMilliseconds = 1_000;

    private const long TableMemorySaveMilliseconds = 5 * 60_000L;

    private static readonly int[] RetryDelaysMilliseconds = { 2_000, 5_000, 10_000 };

    private readonly Configuration configuration;
    private readonly AethernetSession session;
    private readonly CasinoClient casino;
    private readonly CasinoTablesStore tables;
    private readonly CasinoVenueStore venue;
    private readonly NotificationService notifications;
    private readonly Vector4 accent;
    private readonly TradeWindowReader reader = new();
    private readonly TradeSyncTracker tracker = new();
    private readonly Queue<CasinoLedgerTradeRequest> pending = new();
    private readonly StoreWork work = new("CasinoTradeSync");

    private TradeRecordAnswer? answer;
    private CasinoStakeOutcome? undoOutcome;
    private volatile TradeRecorded? recorded;
    private volatile bool undoing;
    private bool sending;
    private bool tracking;
    private long hostingCheckedAtTick;
    private long atTableAtUnixMs;
    private long savedAtUnixMs;
    private string atTableId = string.Empty;
    private string atTableAccountId = string.Empty;
    private string accountId = string.Empty;

    public CasinoTradeSync(Configuration configuration, AethernetSession session, CasinoClient casino,
        CasinoTablesStore tables, CasinoVenueStore venue, NotificationService notifications, Vector4 accent)
    {
        this.configuration = configuration;
        this.session = session;
        this.casino = casino;
        this.tables = tables;
        this.venue = venue;
        this.notifications = notifications;
        this.accent = accent;
        atTableId = configuration.CasinoTradeTableId;
        atTableAccountId = configuration.CasinoTradeTableAccountId;
        atTableAtUnixMs = configuration.CasinoTradeTableAtUnixMs;
        savedAtUnixMs = atTableAtUnixMs;
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    public bool Enabled
    {
        get => configuration.CasinoHostTradeSync;
        set
        {
            if (configuration.CasinoHostTradeSync == value)
            {
                return;
            }

            configuration.CasinoHostTradeSync = value;
            configuration.Save();
            tracker.Reset();
            AepLog.Info($"[TradeSync] Trade sync turned {(value ? "on" : "off")}");
        }
    }

    public bool Undoing => undoing;

    public TradeRecorded? RecordedAt(long nowTick)
    {
        var shown = recorded;
        return shown is not null && nowTick - shown.ShownAtTick < RecordedShownMilliseconds ? shown : null;
    }

    public void DismissRecorded()
    {
        recorded = null;
    }

    public CasinoStakeOutcome? TakeUndoOutcome()
    {
        return Interlocked.Exchange(ref undoOutcome, null);
    }

    public void ForgetTable(string tableId)
    {
        if (tableId.Length == 0 || !string.Equals(atTableId, tableId, StringComparison.Ordinal))
        {
            return;
        }

        AepLog.Info($"[TradeSync] Table {tableId} is closed, trade sync no longer follows it");
        ClearTable();
    }

    public void Undo()
    {
        var shown = recorded;
        if (shown is null || undoing)
        {
            return;
        }

        recorded = null;
        undoing = true;
        var tableId = shown.Entry.TableId;
        var entryId = shown.Entry.EntryId;
        AepLog.Info($"[TradeSync] Undoing the {shown.Entry.Kind} of {shown.Amount} gil at table {tableId}");
        work.Run("trade undo", async token =>
        {
            var result = await tables.RemoveEntryAsync(tableId, entryId, token).ConfigureAwait(false);
            AepLog.Info(result is null
                ? "[TradeSync] Undo got no answer"
                : $"[TradeSync] Undo answered granted {result.Granted}, reason {result.Reason}");
            Interlocked.Exchange(ref undoOutcome, result is null
                ? new CasinoStakeOutcome(false, CasinoReasons.Unreachable)
                : new CasinoStakeOutcome(result.Granted, CasinoTablesStore.Named(result.Reason)));
        }, () => undoing = false);
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        var account = session.CurrentUser?.Id ?? string.Empty;
        if (!string.Equals(account, accountId, StringComparison.Ordinal))
        {
            SwitchAccount(account);
        }

        TakeAnswer();
        if (!session.IsSignedIn)
        {
            StopTracking();
            return;
        }

        Send();
        var nowTick = Environment.TickCount64;
        var nowUnixMs = NowUnixMs();
        if (!configuration.CasinoHostTradeSync || !Hosting(nowTick, nowUnixMs))
        {
            StopTracking();
            return;
        }

        tracking = true;
        var completion = tracker.Observe(reader.Sample(), nowTick);
        if (completion is { } done)
        {
            Completed(done, nowTick, nowUnixMs);
        }
    }

    private bool Hosting(long nowTick, long nowUnixMs)
    {
        if (nowTick - hostingCheckedAtTick >= HostingCheckMilliseconds)
        {
            hostingCheckedAtTick = nowTick;
            NoteTable(nowTick, nowUnixMs);
        }

        return HostedRecently(atTableAtUnixMs, nowUnixMs);
    }

    internal static bool HostedRecently(long atTableAtUnixMs, long nowUnixMs)
    {
        return atTableAtUnixMs != 0 && nowUnixMs - atTableAtUnixMs <= HostedWindowMilliseconds;
    }

    internal static bool ForgetsTable(string atTableAccountId, string signedInAccountId)
    {
        return signedInAccountId.Length > 0
            && !string.Equals(atTableAccountId, signedInAccountId, StringComparison.Ordinal);
    }

    private static long NowUnixMs()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private void StopTracking()
    {
        if (!tracking)
        {
            return;
        }

        tracking = false;
        tracker.Reset();
    }

    private void SwitchAccount(string account)
    {
        accountId = account;
        if (ForgetsTable(atTableAccountId, account))
        {
            hostingCheckedAtTick = 0;
            ClearTable();
        }

        tracker.Reset();
        if (pending.Count > 0)
        {
            AepLog.Info($"[TradeSync] The account changed, {pending.Count} unsent trades dropped");
        }

        pending.Clear();
        recorded = null;
    }

    private void Completed(in TradeCompletion completion, long nowTick, long nowUnixMs)
    {
        var amount = Math.Abs(completion.Net);
        var hint = HintTableId(nowTick, nowUnixMs);
        if (completion.PartnerName.Length == 0)
        {
            AepLog.Info($"[TradeSync] A trade of {amount} gil completed without a partner name, not recorded");
            if (hint.Length > 0)
            {
                NotifyFailure(amount);
            }

            return;
        }

        pending.Enqueue(new CasinoLedgerTradeRequest(Guid.NewGuid().ToString("N"), completion.PartnerName,
            completion.PartnerWorld, completion.Received, completion.Sent, hint));
        AepLog.Info($"[TradeSync] Trade completed: received {completion.Received} gil, sent {completion.Sent} gil, "
            + (hint.Length > 0 ? $"hint table {hint}" : "no table hint") + $", {pending.Count} waiting to send");
    }

    private void Send()
    {
        if (sending || pending.Count == 0)
        {
            return;
        }

        sending = true;
        StartRecord(pending.Dequeue(), accountId);
    }

    private void StartRecord(CasinoLedgerTradeRequest request, string account)
    {
        work.Run("trade record", async token =>
        {
            CasinoLedgerResultDto? result = null;
            try
            {
                result = await RecordAsync(request, token).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Exchange(ref answer, new TradeRecordAnswer(request, account, result));
            }
        });
    }

    private async Task<CasinoLedgerResultDto?> RecordAsync(CasinoLedgerTradeRequest request, CancellationToken token)
    {
        var result = await casino.RecordTradeAsync(request, token).ConfigureAwait(false);
        for (var attempt = 0; result is null && attempt < RetryDelaysMilliseconds.Length; attempt++)
        {
            var delay = RetryDelaysMilliseconds[attempt];
            AepLog.Info($"[TradeSync] Recording the trade got no answer, retrying in {delay} ms");
            await Task.Delay(delay, token).ConfigureAwait(false);
            result = await casino.RecordTradeAsync(request, token).ConfigureAwait(false);
        }

        return result;
    }

    private void TakeAnswer()
    {
        var taken = Interlocked.Exchange(ref answer, null);
        if (taken is null)
        {
            return;
        }

        sending = false;
        if (!string.Equals(taken.AccountId, accountId, StringComparison.Ordinal))
        {
            AepLog.Info("[TradeSync] A trade answer arrived after the account changed, dropped");
            return;
        }

        var request = taken.Request;
        var amount = Math.Abs(request.Received - request.Sent);
        var result = taken.Answer;
        if (result is null)
        {
            AepLog.Info($"[TradeSync] Gave up recording a trade of {amount} gil after every retry");
            NotifyFailure(amount);
            return;
        }

        if (!result.Granted)
        {
            AepLog.Info($"[TradeSync] A trade of {amount} gil was refused: {result.Reason}");
            if (!string.Equals(result.Reason, CasinoReasons.NoGilTable, StringComparison.Ordinal)
                && !string.Equals(result.Reason, CasinoReasons.ConfigInvalid, StringComparison.Ordinal))
            {
                NotifyFailure(amount);
            }

            return;
        }

        var entry = result.Entry;
        if (entry is null)
        {
            AepLog.Info($"[TradeSync] A trade of {amount} gil was granted without an entry");
            return;
        }

        tables.UpsertLedgerEntry(entry.TableId, entry);
        tables.RefreshLedgerNow(entry.TableId);
        AepLog.Info($"[TradeSync] Recorded as a {entry.Kind} of {entry.Amount} gil at table {entry.TableId}, "
            + $"guest {entry.Guest}");
        var shown = new TradeRecorded(entry, PartnerOf(entry, request), request.Received, request.Sent,
            Environment.TickCount64);
        recorded = shown;
        NotifyRecorded(shown);
    }

    private static string PartnerOf(CasinoLedgerEntryDto entry, CasinoLedgerTradeRequest request)
    {
        var incoming = request.Received > request.Sent;
        var userId = incoming ? entry.PayerUserId : entry.PayeeUserId;
        var name = entry.Guest && userId.Length == 0 && entry.GuestName.Length > 0
            ? entry.GuestName
            : incoming ? entry.PayerName : entry.PayeeName;
        return name.Length > 0 ? name : request.PartnerName;
    }

    private string HintTableId(long nowTick, long nowUnixMs)
    {
        var live = NoteTable(nowTick, nowUnixMs);
        if (live.Length > 0)
        {
            return live;
        }

        return HostedRecently(atTableAtUnixMs, nowUnixMs) ? atTableId : string.Empty;
    }

    private string NoteTable(long nowTick, long nowUnixMs)
    {
        var live = LiveTableId(nowTick);
        if (live.Length > 0)
        {
            RememberTable(live, nowUnixMs);
        }

        return live;
    }

    private void RememberTable(string tableId, long nowUnixMs)
    {
        var changed = !string.Equals(atTableId, tableId, StringComparison.Ordinal)
            || !string.Equals(atTableAccountId, accountId, StringComparison.Ordinal);
        atTableId = tableId;
        atTableAtUnixMs = nowUnixMs;
        atTableAccountId = accountId;
        if (changed)
        {
            AepLog.Info($"[TradeSync] Following gil table {tableId}");
        }

        if (changed || nowUnixMs - savedAtUnixMs >= TableMemorySaveMilliseconds)
        {
            SaveTable();
        }
    }

    private void ClearTable()
    {
        var stored = atTableId.Length > 0 || atTableAccountId.Length > 0 || atTableAtUnixMs != 0;
        atTableId = string.Empty;
        atTableAccountId = string.Empty;
        atTableAtUnixMs = 0;
        if (stored)
        {
            SaveTable();
        }
    }

    private void SaveTable()
    {
        savedAtUnixMs = atTableAtUnixMs;
        configuration.CasinoTradeTableId = atTableId;
        configuration.CasinoTradeTableAccountId = atTableAccountId;
        configuration.CasinoTradeTableAtUnixMs = atTableAtUnixMs;
        configuration.Save();
    }

    private string LiveTableId(long nowTick)
    {
        var roomId = venue.Rooms.Room.RoomId;
        if (OwnsGil(roomId))
        {
            return roomId;
        }

        var attended = tables.AttendedTableId(nowTick);
        return OwnsGil(attended) ? attended : string.Empty;
    }

    private bool OwnsGil(string tableId)
    {
        if (tableId.Length == 0)
        {
            return false;
        }

        var ledger = tables.LedgerFor(tableId);
        if (ledger is not null)
        {
            return ledger.Owner && ledger.Currency == CasinoCurrencies.Gil;
        }

        var card = tables.CardFor(tableId);
        return card is not null && tables.Owns(card) && CasinoCurrencies.Of(card) == CasinoCurrencies.Gil;
    }

    private void NotifyRecorded(TradeRecorded shown)
    {
        Notify(shown.Entry.TableId, Loc.T(L.Venue.TradeRecorded, Loc.T(CasinoGilLedger.KindLabel(shown.Entry.Kind))),
            Loc.T(shown.Incoming ? L.Venue.TradeReceivedFrom : L.Venue.TradeSentTo, shown.PartnerName,
                NumberText.Group(shown.Amount)));
    }

    private void NotifyFailure(long amount)
    {
        Notify(string.Empty, Loc.T(L.Venue.TradeFailedTitle), Loc.T(L.Venue.TradeFailedBody, NumberText.Group(amount)));
    }

    private void Notify(string tableId, string title, string body)
    {
        notifications.Notify(new PhoneNotification(CasinoTurnNotifier.AppId, title, body, DateTime.Now, accent,
            tableId.Length > 0 ? CasinoLauncher.LedgerGroupKey(tableId) : CasinoTurnNotifier.GroupPrefix)
        {
            CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        });
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
        work.Dispose();
    }
}
