using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Interface;

namespace Aetherphone.Apps.Casino.Tables;

internal enum HostGame : byte
{
    Blackjack,
    Holdem,
    DiceTable,
    Deathroll,
    Raffle,
    Book,
}

internal readonly record struct HostGameInfo(
    HostGame Game,
    string GameId,
    LocString Title,
    LocString Line,
    CasinoSign Sign,
    FontAwesomeIcon Icon,
    bool HasGlyph,
    VenueRoomKind Venue,
    Vector4 Tint);

internal static class HostGames
{
    public static readonly HostGameInfo[] All =
    {
        new(HostGame.Blackjack, CasinoGames.Blackjack, L.Casino.GameBlackjack, L.Tables.GameBlackjackLine,
            CasinoSign.TwentyOne, FontAwesomeIcon.Clone, true, VenueRoomKind.None, AccentRing.Green),
        new(HostGame.Holdem, CasinoGames.Holdem, L.Casino.GameHoldem, L.Tables.GameHoldemLine, CasinoSign.Holdem,
            FontAwesomeIcon.Clone, true, VenueRoomKind.None, AccentRing.Red),
        new(HostGame.DiceTable, CasinoGames.DiceTable, L.Venue.GameDiceTable, L.Tables.GameDiceLine, CasinoSign.Dice,
            FontAwesomeIcon.Dice, true, VenueRoomKind.Dice, AccentRing.Indigo),
        new(HostGame.Deathroll, CasinoGames.Deathroll, L.Venue.GameDeathroll, L.Tables.GameDeathrollLine,
            CasinoSign.Deathroll, FontAwesomeIcon.Skull, true, VenueRoomKind.Deathroll, AccentRing.Orange),
        new(HostGame.Raffle, CasinoGames.Raffle, L.Venue.GameRaffle, L.Tables.GameRaffleLine, CasinoSign.Raffle,
            FontAwesomeIcon.Ticket, true, VenueRoomKind.Raffle, AccentRing.Gold),
        new(HostGame.Book, CasinoGames.Book, L.Book.Game, L.Book.Line, CasinoSign.Book, FontAwesomeIcon.Book,
            true, VenueRoomKind.None, AccentRing.Violet),
    };

    public static ref readonly HostGameInfo Of(HostGame game) => ref All[(int)game];

    public static int Count(bool bookOpen) => bookOpen ? All.Length : All.Length - 1;

    public static bool IsVenue(HostGame game) => game is HostGame.DiceTable or HostGame.Deathroll or HostGame.Raffle;

    public static bool IsBook(HostGame game) => game == HostGame.Book;

    public static bool HasSeats(HostGame game) => game is HostGame.Blackjack or HostGame.Holdem;

    public static int MaxSeats(HostGame game) =>
        game == HostGame.Holdem ? HoldemRules.MaxSeats : CasinoHostingRules.MaxSeats;

    public static int DefaultSeats(HostGame game) =>
        game == HostGame.Holdem ? HoldemRules.HouseSeats : CasinoHostingRules.DefaultSeats;

    public static HostGame FromVenue(VenueRoomKind kind) => kind switch
    {
        VenueRoomKind.Dice => HostGame.DiceTable,
        VenueRoomKind.Deathroll => HostGame.Deathroll,
        VenueRoomKind.Raffle => HostGame.Raffle,
        _ => HostGame.Blackjack,
    };

    public static HostGame FromKind(string gameKind)
    {
        if (string.Equals(gameKind, HoldemRules.Kind, StringComparison.Ordinal))
        {
            return HostGame.Holdem;
        }

        if (CasinoBookRules.IsBook(gameKind))
        {
            return HostGame.Book;
        }

        return FromVenue(VenueKinds.Of(gameKind));
    }

    public static bool Accepts(HostGame game, int currency, bool gilOpen) => currency switch
    {
        CasinoCurrencies.Chips => !IsVenue(game) && !IsBook(game),
        CasinoCurrencies.Practice => !IsBook(game),
        CasinoCurrencies.Gil => gilOpen && game != HostGame.Holdem,
        _ => false,
    };

    public static int DefaultCurrency(HostGame game)
    {
        if (IsBook(game))
        {
            return CasinoCurrencies.Gil;
        }

        return IsVenue(game) ? CasinoCurrencies.Practice : CasinoCurrencies.Chips;
    }

    public static int FallbackCurrency(HostGame game, int currency, bool gilOpen)
    {
        return Accepts(game, currency, gilOpen) ? currency : DefaultCurrency(game);
    }
}
