namespace Aetherphone.Core.Games;

internal readonly struct ScoreStat
{
    public readonly string Id;
    public readonly ScoreKind Kind;
    public readonly bool LowerIsBetter;

    public ScoreStat(string id, ScoreKind kind, bool lowerIsBetter)
    {
        Id = id;
        Kind = kind;
        LowerIsBetter = lowerIsBetter;
    }

    public bool IsBetter(int candidate, int current) => LowerIsBetter ? candidate < current : candidate > current;
}

internal static class ScoreStatIds
{
    private const char Separator = '.';

    public static readonly ScoreStat[] Catalog =
    {
        Score("2048"), Score("beat"), Score("blade"), Score("breakout"), Score("bubbles"), Score("capman"),
        Score("coil"), Score("crystaldrop"), Score("flap"), Score("hop"), Score("invaders"), Score("match3"),
        Score("match3.blitz"), Score("simon"), Score("skyfall"), Score("snake"), Score("squadron"), Score("stack"),
        Score("swoop"), Score("tetris"), Score("tetris.modern"), Score("trivia"), Score("updraft"),
        Score("updraft.height"), Score("whack"), Score("wordrun"), Level("watersort"), Level("flow.easy"),
        Level("flow.medium"), Level("flow.hard"), Time("solitaire"), Time("memory"), Count("memory.attempts"),
        Time("minesweeper.easy"), Time("minesweeper.medium"), Time("minesweeper.hard"), Time("sudoku.easy"),
        Time("sudoku.medium"), Time("sudoku.hard"), Time("nonogram.easy"), Time("nonogram.medium"),
        Time("nonogram.hard"), Streak("chess"), Streak("reversi"), Score("casino.barkeep"), Score("trailblaze"),
        Score("thrust"),
    };

    public static readonly string[] All = Ids(Catalog);

    public static bool TryFind(string statId, out ScoreStat stat)
    {
        for (var index = 0; index < Catalog.Length; index++)
        {
            if (string.Equals(Catalog[index].Id, statId, StringComparison.Ordinal))
            {
                stat = Catalog[index];
                return true;
            }
        }

        stat = default;
        return false;
    }

    public static bool Contains(string statId) => TryFind(statId, out _);

    public static ScoreKind KindOf(string statId) => TryFind(statId, out var stat) ? stat.Kind : ScoreKind.Score;

    public static string LeaderboardId(string statId, string gameId, ScoreKind kind)
    {
        if (TryFind(statId, out var exact))
        {
            return StreakAgrees(exact, kind) ? statId : string.Empty;
        }

        if (!TryFind(gameId, out var root) || !StreakAgrees(root, kind) ||
            (root.Kind == ScoreKind.Time) != (kind == ScoreKind.Time))
        {
            return string.Empty;
        }

        return gameId;
    }

    private static bool StreakAgrees(in ScoreStat stat, ScoreKind kind) =>
        (kind == ScoreKind.Streak) == (stat.Kind == ScoreKind.Streak);

    public static bool BelongsTo(string statId, string gameId)
    {
        if (string.Equals(statId, gameId, StringComparison.Ordinal))
        {
            return true;
        }

        return statId.Length > gameId.Length && statId[gameId.Length] == Separator &&
               statId.AsSpan(0, gameId.Length).SequenceEqual(gameId.AsSpan());
    }

    public static ReadOnlySpan<char> SuffixOf(string statId)
    {
        var separator = statId.IndexOf(Separator);
        return separator < 0 ? ReadOnlySpan<char>.Empty : statId.AsSpan(separator + 1);
    }

    public static int CountFor(string gameId)
    {
        var count = 0;
        for (var index = 0; index < Catalog.Length; index++)
        {
            if (BelongsTo(Catalog[index].Id, gameId))
            {
                count++;
            }
        }

        return count;
    }

    private static ScoreStat Score(string id) => new(id, ScoreKind.Score, false);

    private static ScoreStat Level(string id) => new(id, ScoreKind.Level, false);

    private static ScoreStat Time(string id) => new(id, ScoreKind.Time, true);

    private static ScoreStat Count(string id) => new(id, ScoreKind.Score, true);

    private static ScoreStat Streak(string id) => new(id, ScoreKind.Streak, false);

    private static string[] Ids(ScoreStat[] catalog)
    {
        var ids = new string[catalog.Length];
        for (var index = 0; index < catalog.Length; index++)
        {
            ids[index] = catalog[index].Id;
        }

        return ids;
    }
}
