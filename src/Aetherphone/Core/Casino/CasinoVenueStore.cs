using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Report;
using Aetherphone.Core.Venues;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Casino;

internal sealed record VenueVerifyResult(string RoomId, long Seq, CasinoRoundVerdict Verdict, bool Done);

internal readonly record struct VenueActDraft(
    string Action,
    int Count = 0,
    string Title = "",
    int Winners = 0,
    int DurationSeconds = 0,
    string OpponentUserId = "",
    long TicketPrice = 0,
    long Prize = 0);

internal sealed class CasinoVenueStore : IDisposable
{
    public const string ReportTarget = "casino_table";

    private const long NearbyRefreshMilliseconds = 30_000;
    private const long NearbyRetryMilliseconds = 15_000;

    private readonly AethernetSession session;
    private readonly CasinoClient casino;
    private readonly SafetyClient safety;
    private readonly CasinoRoomsStore rooms;
    private readonly CasinoTablesStore tables;
    private readonly IHousingPositionSource housing;
    private readonly Func<uint, string> worldName;
    private readonly StoreWork work = new("CasinoVenue");
    private readonly object intentGate = new();

    private volatile VenueRoomView? view;
    private volatile CasinoVenueActDto? actAnswer;
    private volatile CasinoTableRowDto[] nearby = Array.Empty<CasinoTableRowDto>();
    private volatile CasinoStakeOutcome? hostOutcome;
    private volatile bool actInFlight;
    private volatile bool hostInFlight;
    private CasinoRoomState? absorbedState;
    private volatile VenueVerifyResult? verify;
    private VenueActDraft heldDraft;
    private string heldRoomId = string.Empty;
    private string heldActionId = string.Empty;
    private volatile CasinoTableLocationDto? nearbyFor;
    private CasinoHousingPosition position;
    private CasinoTableRowDto[] indexedSource = Array.Empty<CasinoTableRowDto>();
    private Dictionary<VenueAddress, CasinoTableRowDto> venueIndex = new();
    private int actFailed;
    private int fetchingNearby;
    private int verifying;
    private long nearbyAtTick;
    private string? lastAccountId;

    public CasinoVenueStore(AethernetSession session, CasinoClient casino, SafetyClient safety, CasinoRoomsStore rooms,
        CasinoTablesStore tables, IHousingPositionSource housing, Func<uint, string> worldName)
    {
        this.session = session;
        this.casino = casino;
        this.safety = safety;
        this.rooms = rooms;
        this.tables = tables;
        this.housing = housing;
        this.worldName = worldName;
        session.Changed += OnSessionChanged;
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    public VenueRoomView? View => view;

    public CasinoRoomsStore Rooms => rooms;

    public CasinoTablesStore Tables => tables;

    public string AccountId => session.CurrentUser?.Id ?? string.Empty;

    public bool ActInFlight => actInFlight;

    public bool HostInFlight => hostInFlight;

    public CasinoTableRowDto[] Nearby => nearby;

    public CasinoHousingPosition Position => position;

    public VenueVerifyResult? Verification => verify;

    public CasinoVenueActDto? TakeActAnswer()
    {
        return Interlocked.Exchange(ref actAnswer, null);
    }

    public bool TakeActFailure()
    {
        return Interlocked.Exchange(ref actFailed, 0) != 0;
    }

    public CasinoStakeOutcome? TakeHostOutcome()
    {
        return Interlocked.Exchange(ref hostOutcome, null);
    }

    public VenueRoomView? ViewFor(string roomId)
    {
        var held = view;
        return held is not null && string.Equals(held.RoomId, roomId, StringComparison.Ordinal) ? held : null;
    }

    public void Act(string roomId, in VenueActDraft draft)
    {
        if (roomId.Length == 0 || actInFlight || !session.IsSignedIn)
        {
            return;
        }

        actInFlight = true;
        string actionId;
        lock (intentGate)
        {
            if (heldActionId.Length == 0 || heldDraft != draft
                || !string.Equals(heldRoomId, roomId, StringComparison.Ordinal))
            {
                heldActionId = Guid.NewGuid().ToString("N");
            }

            heldDraft = draft;
            heldRoomId = roomId;
            actionId = heldActionId;
        }

        var request = new CasinoVenueActRequest(actionId, draft.Action, draft.Count, draft.Title, draft.Winners,
            draft.DurationSeconds, draft.OpponentUserId, draft.TicketPrice, draft.Prize);
        work.Run("venue act", async token =>
        {
            var answer = await casino.VenueActAsync(roomId, request, token).ConfigureAwait(false);
            if (answer is null)
            {
                Interlocked.Exchange(ref actFailed, 1);
                return;
            }

            lock (intentGate)
            {
                if (string.Equals(heldActionId, actionId, StringComparison.Ordinal))
                {
                    heldActionId = string.Empty;
                }
            }

            Interlocked.Exchange(ref actAnswer, answer.Reason.Length == 0 && !answer.Granted
                ? answer with { Reason = CasinoReasons.Unreachable }
                : answer);
        }, () => actInFlight = false);
    }

    public void VerifyRoll(string roomId, long seq, long bound, long shownValue, bool deathroll)
    {
        if (!BeginVerify(roomId, seq))
        {
            return;
        }

        work.Run("venue verify", async token =>
        {
            var proof = await casino.VerifyRoomIndexAsync(roomId, seq, token).ConfigureAwait(false);
            var verdict = proof is null
                ? CasinoRoundVerdict.Unrevealed
                : VenueVerifier.VerifyRoll(proof, roomId, seq, bound, shownValue, deathroll);
            verify = new VenueVerifyResult(roomId, seq, verdict, true);
        }, () => Interlocked.Exchange(ref verifying, 0));
    }

    public void VerifyRaffle(string roomId, CasinoRaffleDto raffle)
    {
        if (!BeginVerify(roomId, raffle.DrawSeq))
        {
            return;
        }

        work.Run("venue verify raffle", async token =>
        {
            var proof = await casino.VerifyRoomIndexAsync(roomId, raffle.DrawSeq, token).ConfigureAwait(false);
            var verdict = proof is null
                ? CasinoRoundVerdict.Unrevealed
                : VenueVerifier.VerifyRaffle(proof, roomId, raffle);
            verify = new VenueVerifyResult(roomId, raffle.DrawSeq, verdict, true);
        }, () => Interlocked.Exchange(ref verifying, 0));
    }

    public void StartTournament(string roomId, int hands, long stack)
    {
        RunHost("tournament start", roomId, token => casino.StartTournamentAsync(roomId, hands, stack, token));
    }

    public void StopTournament(string roomId)
    {
        RunHost("tournament stop", roomId, token => casino.StopTournamentAsync(roomId, token));
    }

    public void ProposeTrade(in TradeProposal proposal)
    {
        if (proposal.Kind == TradeProposalKind.None || proposal.TableId.Length == 0 || hostInFlight
            || !session.IsSignedIn)
        {
            return;
        }

        hostInFlight = true;
        var tableId = proposal.TableId;
        if (proposal.Kind == TradeProposalKind.Confirm)
        {
            var entryId = proposal.EntryId;
            work.Run("trade confirm", async token =>
            {
                var answer = await casino.ConfirmLedgerEntryAsync(entryId, token).ConfigureAwait(false);
                FinishLedger(tableId, answer);
            }, () => hostInFlight = false);
            return;
        }

        var request = new CasinoLedgerProposeRequest(Guid.NewGuid().ToString("N"), proposal.LedgerKind,
            proposal.CounterpartyUserId, proposal.Amount, CasinoLedgerKinds.Trade);
        work.Run("trade propose", async token =>
        {
            var answer = await casino.ProposeLedgerEntryAsync(tableId, request, token).ConfigureAwait(false);
            FinishLedger(tableId, answer);
        }, () => hostInFlight = false);
    }

    public void Report(string tableId, ReportReason reason, Action<bool> done)
    {
        if (tableId.Length == 0)
        {
            done(false);
            return;
        }

        work.Run("table report", async token =>
        {
            var sent = await safety.ReportAsync(ReportTarget, tableId, reason, token).ConfigureAwait(false);
            done(sent);
        });
    }

    public void EnsureNearby()
    {
        if (!session.IsSignedIn)
        {
            return;
        }

        position = housing.Read();
        var location = position.ToLocation();
        if (location is null)
        {
            if (nearbyFor is not null)
            {
                nearbyFor = null;
                nearby = Array.Empty<CasinoTableRowDto>();
            }

            return;
        }

        var sameWard = nearbyFor is not null && position.SameWard(nearbyFor);
        var now = Environment.TickCount64;
        var last = Interlocked.Read(ref nearbyAtTick);
        var interval = nearby.Length > 0 || sameWard ? NearbyRefreshMilliseconds : NearbyRetryMilliseconds;
        if (sameWard && last != 0 && now - last < interval)
        {
            return;
        }

        if (!sameWard)
        {
            nearby = Array.Empty<CasinoTableRowDto>();
        }

        if (Interlocked.Exchange(ref fetchingNearby, 1) != 0)
        {
            return;
        }

        nearbyFor = location;
        Interlocked.Exchange(ref nearbyAtTick, now);
        work.Run("nearby tables", async token =>
        {
            var answer = await casino.NearbyTablesAsync(location.World, location.Territory, location.Ward, token)
                .ConfigureAwait(false);
            if (answer is not null && ReferenceEquals(nearbyFor, location))
            {
                nearby = answer.Tables ?? Array.Empty<CasinoTableRowDto>();
            }
        }, () => Interlocked.Exchange(ref fetchingNearby, 0));
    }

    public CasinoTableRowDto? LiveTableAt(VenueAddress address)
    {
        if (!address.IsKnown)
        {
            return null;
        }

        var source = tables.Listed;
        if (!ReferenceEquals(source, indexedSource))
        {
            venueIndex = IndexByAddress(source, worldName);
            indexedSource = source;
        }

        return venueIndex.TryGetValue(address, out var row) ? row : null;
    }

    public void EnsureListed()
    {
        tables.EnsureFresh();
    }

    internal static Dictionary<VenueAddress, CasinoTableRowDto> IndexByAddress(CasinoTableRowDto[] rows,
        Func<uint, string> worldName)
    {
        var index = new Dictionary<VenueAddress, CasinoTableRowDto>();
        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var row = rows[rowIndex];
            if (row.Listing == CasinoListings.Private || row.Paused)
            {
                continue;
            }

            var address = CasinoHousingPosition.AddressOf(row.Config?.Location, worldName);
            if (address.IsKnown && !index.ContainsKey(address))
            {
                index[address] = row;
            }
        }

        return index;
    }

    private bool BeginVerify(string roomId, long seq)
    {
        if (roomId.Length == 0 || seq <= 0 || !session.IsSignedIn
            || Interlocked.Exchange(ref verifying, 1) != 0)
        {
            return false;
        }

        verify = new VenueVerifyResult(roomId, seq, CasinoRoundVerdict.Unrevealed, false);
        return true;
    }

    private void RunHost(string label, string roomId, Func<CancellationToken, Task<CasinoTableActionDto?>> call)
    {
        if (roomId.Length == 0 || hostInFlight || !session.IsSignedIn)
        {
            return;
        }

        hostInFlight = true;
        work.Run(label, async token =>
        {
            var answer = await call(token).ConfigureAwait(false);
            Interlocked.Exchange(ref hostOutcome, answer is null
                ? new CasinoStakeOutcome(false, CasinoReasons.Unreachable)
                : new CasinoStakeOutcome(answer.Granted, CasinoTablesStore.Named(answer.Reason)));
        }, () => hostInFlight = false);
    }

    private void FinishLedger(string tableId, CasinoLedgerResultDto? answer)
    {
        Interlocked.Exchange(ref hostOutcome, answer is null
            ? new CasinoStakeOutcome(false, CasinoReasons.Unreachable)
            : new CasinoStakeOutcome(answer.Granted, CasinoTablesStore.Named(answer.Reason)));
        tables.RefreshLedgerNow(tableId);
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        var state = rooms.Room.State;
        if (ReferenceEquals(state, absorbedState))
        {
            return;
        }

        absorbedState = state;
        if (state is null || !VenueKinds.IsVenue(state.Snapshot.GameKind))
        {
            if (state is not null || rooms.Room.RoomId.Length == 0)
            {
                view = null;
            }

            return;
        }

        view = VenueRoomView.From(state);
    }

    private void OnSessionChanged()
    {
        var accountId = session.CurrentUser?.Id;
        if (string.Equals(accountId, lastAccountId, StringComparison.Ordinal))
        {
            return;
        }

        lastAccountId = accountId;
        view = null;
        nearby = Array.Empty<CasinoTableRowDto>();
        nearbyFor = null;
        Interlocked.Exchange(ref actAnswer, null);
        Interlocked.Exchange(ref hostOutcome, null);
        Interlocked.Exchange(ref nearbyAtTick, 0);
        lock (intentGate)
        {
            heldActionId = string.Empty;
            heldRoomId = string.Empty;
        }
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
        session.Changed -= OnSessionChanged;
        work.Dispose();
    }
}
