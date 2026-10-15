using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Casino.Tables;

internal static class TableRowViews
{
    private static readonly LocString[] CurrencyNames =
        { L.Tables.CurrencyChips, L.Tables.CurrencyPractice, L.Tables.CurrencyGil };

    public static LocString CurrencyName(int currency) =>
        CurrencyNames[Math.Clamp(currency, 0, CurrencyNames.Length - 1)];

    public static LocString ListingWord(int listing) => listing switch
    {
        CasinoListings.Open => L.HostSetup.SummaryAnyone,
        CasinoListings.Knock => L.HostSetup.SummaryKnock,
        _ => L.Tables.SummaryInvite,
    };

    public static string HostLine(CasinoTableRowDto row)
    {
        var game = Loc.T(HostGames.Of(HostGames.FromKind(row.GameKind)).Title);
        return Loc.T(L.HostDesk.TableLine, game, Loc.T(CurrencyName(CasinoCurrencies.Of(row))),
            Loc.T(ListingWord(row.Listing)));
    }

    public static TableRowView ViewOf(CasinoTableRowDto row, string myUserId, int bookEntries = -1)
    {
        var currency = CasinoCurrencies.Of(row);
        var hosted = CasinoTableFilters.IsPrivate(row);
        var mine = myUserId.Length > 0 && string.Equals(row.OwnerUserId, myUserId, StringComparison.Ordinal);
        var holdem = string.Equals(row.GameKind, HoldemRules.Kind, StringComparison.Ordinal);
        var book = CasinoBookRules.IsBook(row.GameKind);
        var hostNamed = hosted && row.OwnerName.Length > 0;
        var blindsNamed = holdem && row.Name.Length == 0 && !hostNamed;
        var name = NameOf(row);
        var room = VenueKinds.Of(row.GameKind);
        var hostLine = hosted && row.OwnerName.Length > 0 ? Loc.T(L.Tables.HostedBy, row.OwnerName) : string.Empty;
        var stakes = book ? BookLine(bookEntries, hostLine) : StakesOf(row, room, currency, holdem, blindsNamed);
        var seats = book
            ? bookEntries >= 0 ? hostLine : string.Empty
            : room != VenueRoomKind.None
                ? Loc.T(L.Venue.InRoom, row.Occupancy.ToString(Loc.Culture))
                : Loc.T(L.Casino.TableSeats, row.SeatedCount.ToString(Loc.Culture),
                    row.MaxSeats.ToString(Loc.Culture));
        var watching = room != VenueRoomKind.None || book ? 0 : CasinoTableFilters.SpectatorsOf(row);
        var spectators = watching > 0
            ? Loc.T(L.Casino.TableSpectators, watching.ToString(Loc.Culture))
            : string.Empty;
        var draining = string.Equals(row.Reason, CasinoReasons.Draining, StringComparison.Ordinal)
            || string.Equals(row.Reason, CasinoReasons.TableClosed, StringComparison.Ordinal);
        var inviteOnly = hosted && !mine && row.Listing == CasinoListings.Private;
        var monogram = hosted && row.OwnerName.Length > 0 ? Initials.Of(row.OwnerName) : string.Empty;
        return new TableRowView(name, stakes, seats, spectators,
            room == VenueRoomKind.None && !book && !CasinoTableFilters.HasOpenSeat(row), inviteOnly,
            mine, draining, currency, Loc.T(CurrencyName(currency)), row.Paused,
            GameIdOf(row.GameKind), hostLine, monogram, room == VenueRoomKind.None ? row.SeatedCount : row.Occupancy,
            row.MaxSeats, room, Book: book);
    }

    public static string NameOf(CasinoTableRowDto row) => NameOf(row, GameIdOf(row.GameKind));

    public static string NameOf(CasinoTableRowDto row, string gameId)
    {
        if (row.Name.Length > 0)
        {
            return row.Name;
        }

        if (CasinoTableFilters.IsPrivate(row) && row.OwnerName.Length > 0)
        {
            return Loc.T(CasinoBookRules.IsBook(row.GameKind) ? L.Book.HostedBy : L.Casino.TableHostedBy,
                row.OwnerName);
        }

        if (string.Equals(gameId, CasinoGames.Holdem, StringComparison.Ordinal))
        {
            return Loc.T(L.Holdem.TableName, NumberText.Compact(row.MinBet), NumberText.Compact(row.MaxBet));
        }

        return string.Equals(gameId, CasinoGames.Blackjack, StringComparison.Ordinal)
            ? Loc.T(L.Casino.TableUnnamed)
            : Loc.T(CasinoGameNames.Of(gameId));
    }

    public static string BookLine(int entries, string hostLine)
    {
        if (entries >= 0)
        {
            return Loc.T(L.Book.RowLine, entries.ToString(Loc.Culture));
        }

        return hostLine.Length > 0 ? hostLine : Loc.T(L.Book.Plays);
    }

    public static int BookEntriesOf(CasinoTableRowDto row, CasinoTableLedgerDto? ledger)
    {
        if (ledger is null || !CasinoBookRules.IsBook(row.GameKind)
            || !string.Equals(ledger.TableId, row.TableId, StringComparison.Ordinal))
        {
            return -1;
        }

        return ledger.Entries?.Length ?? 0;
    }

    public static TableRowView HouseView(in HouseBlackjackFloor floor, long playerMaxBet)
    {
        return new TableRowView(Loc.T(L.Casino.GameBlackjack),
            Loc.T(L.Casino.TableStakes, NumberText.Compact(floor.MinBet),
                NumberText.Compact(floor.TopBetFor(playerMaxBet))),
            Loc.T(L.Casino.LivePlayers, floor.Seated.ToString(Loc.Culture)), string.Empty, !floor.Open, false,
            false, false, CasinoCurrencies.Chips, Loc.T(CurrencyName(CasinoCurrencies.Chips)), false,
            CasinoGames.Blackjack, Seated: floor.Seated);
    }

    public static string GameIdOf(string gameKind)
    {
        if (string.Equals(gameKind, HoldemRules.Kind, StringComparison.Ordinal))
        {
            return CasinoGames.Holdem;
        }

        if (CasinoBookRules.IsBook(gameKind))
        {
            return CasinoGames.Book;
        }

        return VenueKinds.Of(gameKind) switch
        {
            VenueRoomKind.Dice => CasinoGames.DiceTable,
            VenueRoomKind.Deathroll => CasinoGames.Deathroll,
            VenueRoomKind.Raffle => CasinoGames.Raffle,
            _ => CasinoGames.Blackjack,
        };
    }

    private static string StakesOf(CasinoTableRowDto row, VenueRoomKind room, int currency, bool holdem,
        bool blindsNamed)
    {
        if (room != VenueRoomKind.None)
        {
            return Loc.T(Venue.VenueCabinet.NameOf(room));
        }

        if (currency == CasinoCurrencies.Gil)
        {
            return Loc.T(L.Tables.GilStakes, NumberText.Group(row.MaxBet), NumberText.Group(row.Config?.Bank ?? 0));
        }

        if (blindsNamed)
        {
            return Loc.T(L.Holdem.BuyInValue, NumberText.Compact(row.MinBuyIn), NumberText.Compact(row.MaxBuyIn));
        }

        return holdem
            ? Loc.T(L.Holdem.BlindsValue, NumberText.Compact(row.MinBet), NumberText.Compact(row.MaxBet))
            : Loc.T(L.Casino.TableStakes, NumberText.Compact(row.MinBet), NumberText.Compact(row.MaxBet));
    }
}
