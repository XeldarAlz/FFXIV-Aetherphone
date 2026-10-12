using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Casino.Venue;

internal static class VenuePosters
{
    public static readonly HostGame[] Games =
    {
        HostGame.Blackjack,
        HostGame.Holdem,
        HostGame.DiceTable,
        HostGame.Deathroll,
        HostGame.Raffle,
        HostGame.Book,
    };

    public static int Count(bool bookOpen) => bookOpen ? Games.Length : Games.Length - 1;

    public static bool PrefersGil(HostGame game, bool gilOpen) =>
        HostGames.Accepts(game, CasinoCurrencies.Gil, gilOpen);

    public static bool ShowsGilBadge(HostGame game, bool gilOpen) => PrefersGil(game, gilOpen);

    public static LocString MetaOf(HostGame game, bool gilOpen)
    {
        if (HostGames.IsBook(game))
        {
            return L.Book.Plays;
        }

        var gil = HostGames.Accepts(game, CasinoCurrencies.Gil, gilOpen);
        var chips = HostGames.Accepts(game, CasinoCurrencies.Chips, gilOpen);
        if (game == HostGame.Raffle)
        {
            return gil ? L.VenueHub.PlaysGilFree : L.VenueHub.PlaysFreeTickets;
        }

        if (chips)
        {
            return gil ? L.VenueHub.PlaysGilPracticeChips : L.VenueHub.PlaysChipsPractice;
        }

        return gil ? L.VenueHub.PlaysGilPractice : L.VenueHub.PlaysPractice;
    }
}
