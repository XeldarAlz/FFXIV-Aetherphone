using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Games.Framework;

internal enum GameGenre : byte
{
    Arcade,
    Action,
    Puzzle,
    Brain,
    Strategy,
    Tabletop,
    Friends,
}

internal static class GameGenres
{
    public static readonly GameGenre[] Shelves =
    {
        GameGenre.Arcade, GameGenre.Action, GameGenre.Puzzle, GameGenre.Brain, GameGenre.Strategy, GameGenre.Tabletop,
        GameGenre.Friends,
    };

    public static LocString Label(GameGenre genre)
    {
        return genre switch
        {
            GameGenre.Action => L.Games.GenreAction,
            GameGenre.Puzzle => L.Games.GenrePuzzle,
            GameGenre.Brain => L.Games.GenreBrain,
            GameGenre.Strategy => L.Games.GenreStrategy,
            GameGenre.Tabletop => L.Games.GenreTabletop,
            GameGenre.Friends => L.Games.GenreFriends,
            _ => L.Games.GenreArcade,
        };
    }
}
