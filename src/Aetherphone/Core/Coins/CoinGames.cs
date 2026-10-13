namespace Aetherphone.Core.Coins;

internal static class CoinGames
{
    public static readonly string[] All =
    [
        "minesweeper",
        "memory",
        "match3",
        "tetris",
        "2048",
        "watersort",
        "breakout",
        "bubbles",
        "nonogram",
        "flow",
        "solitaire",
        "simon",
        "flap",
        "reversi",
        "whack",
        "snake",
        "sudoku",
        "chess",
        "stack",
        "crystaldrop",
        "beat",
        "blade",
        "trivia",
        "skyfall",
        "invaders",
        "capman",
        "hop",
        "squadron",
        "doom",
        "wordrun",
        "coil",
        "swoop",
        "updraft",
        "mahjong",
        "gloop",
        "slice",
        "spiral",
        "drift",
        "crawler",
        "trails",
        "moogleclicker",
        "pinball",
        "pegfall",
        "fling",
        "snip",
        "crater",
        "siege",
        "trailblaze",
        "thrust",
        "crates",
        "delve",
        "luckydraw",
        "broadside",
        "herd",
        "claim",
        "tempo",
        "fuse",
        "lander",
        "minigolf",
    ];

    public static string FeaturedFor(int dayIndex)
    {
        var slot = dayIndex % All.Length;
        if (slot < 0)
        {
            slot += All.Length;
        }

        return All[slot];
    }
}
