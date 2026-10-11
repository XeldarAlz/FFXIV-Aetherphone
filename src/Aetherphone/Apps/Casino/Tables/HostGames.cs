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
            FontAwesomeIcon.Dice, false, VenueRoomKind.Dice, AccentRing.Indigo),
        new(HostGame.Deathroll, CasinoGames.Deathroll, L.Venue.GameDeathroll, L.Tables.GameDeathrollLine,
            CasinoSign.Deathroll, FontAwesomeIcon.Skull, false, VenueRoomKind.Deathroll, AccentRing.Orange),
        new(HostGame.Raffle, CasinoGames.Raffle, L.Venue.GameRaffle, L.Tables.GameRaffleLine, CasinoSign.Raffle,
            FontAwesomeIcon.Ticket, false, VenueRoomKind.Raffle, AccentRing.Gold),
    };

    public static ref readonly HostGameInfo Of(HostGame game) => ref All[(int)game];

    public static bool IsVenue(HostGame game) => game >= HostGame.DiceTable;

    public static bool HasSeats(HostGame game) => game is HostGame.Blackjack or HostGame.Holdem;

    public static int MaxSeats(HostGame game) =>
        game == HostGame.Holdem ? HoldemRules.MaxSeats : CasinoHostingRules.MaxSeats;

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

        return FromVenue(VenueKinds.Of(gameKind));
    }

    public static bool Accepts(HostGame game, int currency, bool gilOpen) => currency switch
    {
        CasinoCurrencies.Chips => !IsVenue(game),
        CasinoCurrencies.Practice => true,
        CasinoCurrencies.Gil => gilOpen && game != HostGame.Holdem,
        _ => false,
    };

    public static int FallbackCurrency(HostGame game, int currency, bool gilOpen)
    {
        if (Accepts(game, currency, gilOpen))
        {
            return currency;
        }

        return IsVenue(game) ? CasinoCurrencies.Practice : CasinoCurrencies.Chips;
    }
}
