using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Casino;

internal sealed class CasinoTradeSync : IDisposable
{
    private const long LocateEveryMilliseconds = 1_000;
    private const long ProbeMilliseconds = 30_000;
    private const long ProbeEveryMilliseconds = 5_000;

    private readonly Configuration configuration;
    private readonly AethernetSession session;
    private readonly ITradeWindowReader reader;
    private readonly CasinoRoomsStore rooms;
    private readonly CasinoTablesStore tables;
    private readonly CasinoVenueStore venue;
    private readonly CasinoStore chips;
    private readonly NotificationService notifications;
    private readonly Vector4 accent;
    private readonly TradeSyncTracker tracker = new();
    private readonly TradeTableMemory memory = new();
    private readonly TradeProposalQueue queue = new();
    private readonly ITradeMatchSource source;
    private readonly StoreWork work = new("CasinoTradeSync");
    private readonly Func<string, CasinoTableRowDto?> cardFor;
    private readonly Func<string, CasinoTableLedgerDto?> ledgerFor;

    private volatile TradeMatchHolder? serverAnswer;
    private volatile TradeMiss? lastMiss;
    private volatile GuestOffer? pendingGuest;
    private int matching;
    private bool wasOpen;
    private long locatedAtTick;
    private TradeCompletion? heldCompletion;
    private TradeMiss? probedMiss;
    private CasinoTableLedgerDto? probedLedger;
    private string probeHost = string.Empty;
    private long probedAtTick;

    public CasinoTradeSync(Configuration configuration, AethernetSession session, ITradeWindowReader reader,
        ITradeMatchSource source, CasinoRoomsStore rooms, CasinoTablesStore tables, CasinoVenueStore venue,
        CasinoStore chips, NotificationService notifications, Vector4 accent)
    {
        this.configuration = configuration;
        this.session = session;
        this.reader = reader;
        this.source = source;
        this.rooms = rooms;
        this.tables = tables;
        this.venue = venue;
        this.chips = chips;
        this.notifications = notifications;
        this.accent = accent;
        cardFor = tables.CardFor;
        ledgerFor = tables.LedgerFor;
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    public bool Enabled
    {
        get => configuration.CasinoTradeSync;
        set
        {
            if (configuration.CasinoTradeSync == value)
            {
                return;
            }

            configuration.CasinoTradeSync = value;
            configuration.Save();
            tracker.Reset();
        }
    }

    public bool AutoConfirm
    {
        get => configuration.CasinoTradeAutoConfirm;
        set
        {
            if (configuration.CasinoTradeAutoConfirm == value)
            {
                return;
            }

            configuration.CasinoTradeAutoConfirm = value;
            configuration.Save();
        }
    }

    public TradeProposal Pending => queue.Head;

    public TradeMiss? LastMiss => lastMiss;

    public GuestOffer? PendingGuest => GuestLedger.Shown(pendingGuest, chips.HasFeature(CasinoFeatures.VenueBook));

    public bool Busy => venue.HostInFlight;

    public bool GuestBusy => tables.IntentInFlight;

    public CasinoStakeOutcome? TakeOutcome()
    {
        return venue.TakeTradeOutcome();
    }

    public CasinoStakeOutcome? TakeGuestOutcome()
    {
        return tables.TakeGuestOutcome();
    }

    public void Accept()
    {
        if (queue.TryTake(out var proposal))
        {
            venue.ProposeTrade(proposal);
        }
    }

    public void Dismiss()
    {
        queue.TryTake(out _);
    }

    public void RecordGuest()
    {
        var offer = Interlocked.Exchange(ref pendingGuest, null);
        if (offer is null)
        {
            return;
        }

        lastMiss = null;
        AepLog.Info($"[TradeSync] Recording a guest {offer.LedgerKind} of {offer.Amount} gil at table {offer.TableId}");
        tables.ProposeGuestEntry(offer.TableId, offer.LedgerKind, offer.GuestName, offer.Amount,
            CasinoLedgerKinds.Trade);
    }

    public void DismissGuest()
    {
        pendingGuest = null;
    }

    public TradeTableContext Context()
    {
        var view = View();
        return TradeContexts.Build(memory.Resolve(view, Environment.TickCount64), view);
    }

    private TradeTableView View()
    {
        return new TradeTableView(session.CurrentUser?.Id ?? string.Empty, rooms.Room.RoomId,
            rooms.Room.State?.Blackjack, venue.View, tables.Door, tables.Listed, cardFor, ledgerFor,
            tables.LedgerRoomId);
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!configuration.CasinoTradeSync || !session.IsSignedIn)
        {
            heldCompletion = null;
            probedMiss = null;
            return;
        }

        var now = Environment.TickCount64;
        var sample = reader.Sample();
        if (sample.Open && !wasOpen)
        {
            Opened(memory.Resolve(View(), now));
        }
        else if (now - locatedAtTick >= LocateEveryMilliseconds)
        {
            locatedAtTick = now;
            memory.Resolve(View(), now);
        }

        wasOpen = sample.Open;
        var answered = Interlocked.Exchange(ref serverAnswer, null);
        if (answered is not null)
        {
            Answered(answered);
        }

        if (heldCompletion is { } held && Volatile.Read(ref matching) == 0)
        {
            heldCompletion = null;
            AepLog.Info("[TradeSync] The server match finished, matching the trade that waited for it");
            Match(held);
        }

        Probe(now);
        var completion = tracker.Observe(sample, now);
        if (completion is { } done)
        {
            Completed(done);
        }
    }

    private void Completed(TradeCompletion completion)
    {
        AepLog.Info($"[TradeSync] Trade completed: net {completion.Net} gil, "
            + $"{completion.NameCandidates.Length} name candidates, partner read {completion.PartnerRead}");
        Match(completion);
    }

    private void Match(TradeCompletion completion)
    {
        var context = Context();
        var local = TradeMatchFlow.Local(completion, context);
        if (local.Route == TradeMatchRoute.NoTable)
        {
            AepLog.Info("[TradeSync] No gil table is known for this trade, nothing to match");
            return;
        }

        if (local.Route == TradeMatchRoute.Local)
        {
            AepLog.Info($"[TradeSync] Matched by display name at table {context.TableId}");
            Offer(local.Proposal);
            return;
        }

        var partner = GuestLedger.PartnerNameOf(completion);
        if (Interlocked.Exchange(ref matching, 1) != 0)
        {
            if (heldCompletion is null)
            {
                AepLog.Info("[TradeSync] A server match is still in flight, this trade waits for it");
                heldCompletion = completion;
                return;
            }

            AepLog.Info("[TradeSync] A server match is in flight and a trade already waits, this one is not matched");
            Missed(new TradeMiss(context.TableId, Math.Abs(completion.Net), Environment.TickCount64, partner,
                completion.Net > 0, TradeMissKind.Unread), context);
            return;
        }

        AepLog.Info($"[TradeSync] No display name matched at table {context.TableId}, asking the server");
        work.Run("trade match", async token =>
        {
            var result = await TradeMatchFlow.ServerAsync(source, completion, context, token).ConfigureAwait(false);
            Interlocked.Exchange(ref serverAnswer,
                new TradeMatchHolder(result, context.MyUserId, context.TableId, completion.Net, partner));
        }, () => Interlocked.Exchange(ref matching, 0));
    }

    private void Answered(TradeMatchHolder answered)
    {
        if (!string.Equals(answered.AccountId, session.CurrentUser?.Id, StringComparison.Ordinal))
        {
            AepLog.Info("[TradeSync] Server match arrived after the account changed, dropped");
            return;
        }

        var result = answered.Result;
        if (result.Route != TradeMatchRoute.Server)
        {
            AepLog.Info(MissNote(result));
            Missed(new TradeMiss(answered.TableId, Math.Abs(answered.Net), Environment.TickCount64,
                answered.PartnerName, answered.Net > 0, TradeMatchFlow.MissKindOf(result.Route)), Context());
            return;
        }

        AepLog.Info("[TradeSync] Matched by character name on the server");
        Offer(result.Proposal);
    }

    private static string MissNote(in TradeMatchResult result) => result.Route switch
    {
        TradeMatchRoute.Refused => $"[TradeSync] Server refused the match: {result.Reason}",
        TradeMatchRoute.Unreachable => $"[TradeSync] Server match unavailable ({result.Reason}), treated as no match",
        TradeMatchRoute.Mismatch => "[TradeSync] Server match does not fit this table's side, dropped",
        TradeMatchRoute.Unrecordable =>
            "[TradeSync] Server matched a counterparty, but no ledger entry can take this trade",
        TradeMatchRoute.NoNames => "[TradeSync] No names were read from the trade window",
        _ => "[TradeSync] Server found no counterparty for this trade",
    };

    private void Missed(TradeMiss miss, in TradeTableContext context)
    {
        lastMiss = miss;
        var offer = GuestLedger.Offer(miss, context.Hosting, context.Entries);
        pendingGuest = offer;
        if (context.Hosting)
        {
            Arm(miss, context.MyUserId);
        }

        if (offer is null)
        {
            return;
        }

        AepLog.Info($"[TradeSync] Offering a guest {offer.LedgerKind} of {offer.Amount} gil to the host");
        Notify(offer.TableId, offer.Incoming, offer.GuestName, offer.Amount);
    }

    private void Arm(TradeMiss miss, string hostUserId)
    {
        probedMiss = miss;
        probeHost = hostUserId;
        probedLedger = tables.LedgerFor(miss.TableId);
        probedAtTick = 0;
    }

    private void Probe(long now)
    {
        var miss = probedMiss;
        if (miss is null)
        {
            return;
        }

        if (!ReferenceEquals(lastMiss, miss) || now - miss.AtTick > ProbeMilliseconds)
        {
            probedMiss = null;
            probedLedger = null;
            return;
        }

        var ledger = tables.LedgerFor(miss.TableId);
        if (ledger is not null && !ReferenceEquals(ledger, probedLedger))
        {
            probedLedger = ledger;
            var waiting = TradeMatchFlow.WaitingOnHost(miss, probeHost,
                ledger.Entries ?? Array.Empty<CasinoLedgerEntryDto>());
            if (waiting.Kind != TradeProposalKind.None)
            {
                AepLog.Info("[TradeSync] The ledger now holds an entry for that trade waiting on the host");
                probedMiss = null;
                Offer(waiting);
                return;
            }
        }

        if (now - probedAtTick < ProbeEveryMilliseconds
            || !string.Equals(tables.LedgerRoomId, miss.TableId, StringComparison.Ordinal))
        {
            return;
        }

        probedAtTick = now;
        tables.RefreshLedgerNow(miss.TableId);
    }

    private void Offer(in TradeProposal proposal)
    {
        lastMiss = null;
        pendingGuest = null;
        probedMiss = null;
        var automatic = !proposal.HostSide && configuration.CasinoTradeAutoConfirm;
        AepLog.Info($"[TradeSync] {proposal.Kind} {proposal.LedgerKind} for {proposal.Amount} gil, "
            + $"host side {proposal.HostSide}, incoming {proposal.Incoming}, automatic {automatic}");
        if (automatic && !venue.HostInFlight)
        {
            venue.ProposeTrade(proposal);
            return;
        }

        if (!queue.Add(proposal))
        {
            AepLog.Info("[TradeSync] Prompt queue full, the oldest unanswered prompt was dropped");
        }

        Notify(proposal.TableId, proposal.Incoming, proposal.CounterpartyName, proposal.Amount);
    }

    private void Notify(string tableId, bool incoming, string name, long amount)
    {
        notifications.Notify(new PhoneNotification(CasinoTurnNotifier.AppId, Loc.T(L.Venue.TradeNotifyTitle),
            Loc.T(incoming ? L.Venue.TradeReceivedFrom : L.Venue.TradeSentTo, name, NumberText.Group(amount)),
            DateTime.Now, accent, string.Concat(CasinoTurnNotifier.GroupPrefix, tableId))
        {
            CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        });
    }

    private void Opened(in TradeTableRef table)
    {
        if (!table.Located)
        {
            return;
        }

        tables.RefreshLedgerNow(table.TableId);
        if (!table.Known)
        {
            tables.RefreshCard(table.TableId);
        }
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
        work.Dispose();
        reader.Dispose();
    }

    private sealed record TradeMatchHolder(TradeMatchResult Result, string AccountId, string TableId, long Net,
        string PartnerName);
}
