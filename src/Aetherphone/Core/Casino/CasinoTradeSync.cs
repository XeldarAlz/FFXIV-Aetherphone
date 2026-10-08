using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Notifications;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Casino;

internal sealed class CasinoTradeSync : IDisposable
{
    private readonly Configuration configuration;
    private readonly AethernetSession session;
    private readonly ITradeWindowReader reader;
    private readonly CasinoRoomsStore rooms;
    private readonly CasinoTablesStore tables;
    private readonly CasinoVenueStore venue;
    private readonly NotificationService notifications;
    private readonly Vector4 accent;
    private readonly TradeSyncTracker tracker = new();

    private volatile TradeProposalHolder? pending;
    private bool wasOpen;

    public CasinoTradeSync(Configuration configuration, AethernetSession session, ITradeWindowReader reader,
        CasinoRoomsStore rooms, CasinoTablesStore tables, CasinoVenueStore venue, NotificationService notifications,
        Vector4 accent)
    {
        this.configuration = configuration;
        this.session = session;
        this.reader = reader;
        this.rooms = rooms;
        this.tables = tables;
        this.venue = venue;
        this.notifications = notifications;
        this.accent = accent;
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

    public TradeProposal Pending => pending?.Proposal ?? TradeProposal.None;

    public bool Busy => venue.HostInFlight;

    public void Accept()
    {
        var held = Interlocked.Exchange(ref pending, null);
        if (held is not null)
        {
            venue.ProposeTrade(held.Proposal);
        }
    }

    public void Dismiss()
    {
        Interlocked.Exchange(ref pending, null);
    }

    public TradeTableContext Context()
    {
        var me = session.CurrentUser?.Id ?? string.Empty;
        var board = rooms.Room.State?.Blackjack;
        var roomId = rooms.Room.RoomId;
        if (board is not null && roomId.Length > 0 && CasinoCurrencies.Of(board) == CasinoCurrencies.Gil)
        {
            var card = tables.CardFor(roomId);
            return new TradeTableContext(roomId, me, card?.OwnerUserId ?? string.Empty, card?.OwnerName ?? string.Empty,
                SeatsOf(board.Seats), EntriesOf(roomId));
        }

        var door = tables.Door;
        if (door is null || !door.Owner)
        {
            return default;
        }

        var doorCard = tables.CardFor(door.RoomId);
        if (doorCard is null || CasinoCurrencies.Of(doorCard) != CasinoCurrencies.Gil)
        {
            return default;
        }

        return new TradeTableContext(door.RoomId, me, me, doorCard.OwnerName, SeatsOf(door.Seated),
            EntriesOf(door.RoomId));
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!configuration.CasinoTradeSync || !session.IsSignedIn)
        {
            return;
        }

        var sample = reader.Sample();
        if (sample.Open && !wasOpen)
        {
            var opened = Context();
            if (opened.Known)
            {
                tables.RefreshLedgerNow(opened.TableId);
            }
        }

        wasOpen = sample.Open;
        var completion = tracker.Observe(sample, Environment.TickCount64);
        if (completion is not { } done)
        {
            return;
        }

        var proposal = TradeLedgerMatcher.Match(done, Context());
        if (proposal.Kind == TradeProposalKind.None)
        {
            return;
        }

        if (!proposal.HostSide && configuration.CasinoTradeAutoConfirm)
        {
            venue.ProposeTrade(proposal);
            return;
        }

        pending = new TradeProposalHolder(proposal);
        notifications.Notify(new PhoneNotification(CasinoTurnNotifier.AppId, Loc.T(L.Venue.TradeNotifyTitle),
            Loc.T(proposal.Incoming ? L.Venue.TradeReceivedFrom : L.Venue.TradeSentTo, proposal.CounterpartyName,
                NumberText.Group(proposal.Amount)), DateTime.Now, accent,
            string.Concat(CasinoTurnNotifier.GroupPrefix, proposal.TableId))
        {
            CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        });
    }

    private CasinoLedgerEntryDto[] EntriesOf(string roomId)
    {
        return tables.LedgerFor(roomId)?.Entries ?? Array.Empty<CasinoLedgerEntryDto>();
    }

    private static TradeSeat[] SeatsOf(CasinoBlackjackSeatDto[]? seats)
    {
        if (seats is null)
        {
            return Array.Empty<TradeSeat>();
        }

        var result = new TradeSeat[seats.Length];
        for (var index = 0; index < seats.Length; index++)
        {
            result[index] = new TradeSeat(seats[index].UserId, seats[index].DisplayName);
        }

        return result;
    }

    private static TradeSeat[] SeatsOf(CasinoTableSeatedDto[]? seated)
    {
        if (seated is null)
        {
            return Array.Empty<TradeSeat>();
        }

        var result = new TradeSeat[seated.Length];
        for (var index = 0; index < seated.Length; index++)
        {
            result[index] = new TradeSeat(seated[index].UserId, seated[index].DisplayName);
        }

        return result;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnFrameworkUpdate;
        reader.Dispose();
    }

    private sealed record TradeProposalHolder(TradeProposal Proposal);
}
