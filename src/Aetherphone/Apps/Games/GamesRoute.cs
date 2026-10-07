using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Apps;

namespace Aetherphone.Apps.Games;

internal enum GamesScreen : byte
{
    Root,
    Shelf,
    Playing,
    OnlineRoom,
    Leaderboard,
    Bests,
}

internal enum GamesTab : byte
{
    Home,
    Together,
    Library,
    Profile,
}

internal enum GamesShelf : byte
{
    Arcade = (byte)GameGenre.Arcade,
    Action = (byte)GameGenre.Action,
    Puzzle = (byte)GameGenre.Puzzle,
    Brain = (byte)GameGenre.Brain,
    Strategy = (byte)GameGenre.Strategy,
    Tabletop = (byte)GameGenre.Tabletop,
    All,
    New,
}

internal readonly record struct GamesRoute(GamesScreen Screen, GamesShelf Shelf, string GameId = "", string StatId = "")
{
    public static readonly GamesRoute Root = new(GamesScreen.Root, GamesShelf.All);
    public static readonly GamesRoute Playing = new(GamesScreen.Playing, GamesShelf.All);
    public static readonly GamesRoute OnlineRoom = new(GamesScreen.OnlineRoom, GamesShelf.All);
    public static readonly GamesRoute Bests = new(GamesScreen.Bests, GamesShelf.All);

    public static GamesRoute ShelfOf(GamesShelf shelf) => new(GamesScreen.Shelf, shelf);

    public static GamesRoute LeaderboardOf(string gameId, string statId) =>
        new(GamesScreen.Leaderboard, GamesShelf.All, gameId, statId);

    public static GamesShelf ShelfFor(GameGenre genre) => (GamesShelf)genre;
}

internal static class GamesStack
{
    public static bool HoldsGame(ViewRouter<GamesRoute> router)
    {
        for (var index = router.Depth - 1; index >= 0; index--)
        {
            if (router.TryGetView(index, out var route) && route.Screen == GamesScreen.Playing)
            {
                return true;
            }
        }

        return false;
    }
}
