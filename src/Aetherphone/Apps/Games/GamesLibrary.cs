using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Online;
using Aetherphone.Apps.Games.Tetris;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Games;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Games;

internal readonly struct GameEntry
{
    public readonly string Id;
    public readonly int GameIndex;
    public readonly string OnlineKind;
    public readonly GameGenre Genre;
    public readonly int AddedDay;

    public GameEntry(string id, int gameIndex, string onlineKind, GameGenre genre, int addedDay)
    {
        Id = id;
        GameIndex = gameIndex;
        OnlineKind = onlineKind;
        Genre = genre;
        AddedDay = addedDay;
    }

    public bool Online => GameIndex < 0;
}

internal enum LibraryFilter : byte
{
    All,
    New,
    Arcade,
    Action,
    Puzzle,
    Brain,
    Tabletop,
    Friends,
}

internal sealed class GamesLibrary
{
    public const int FilterCount = 8;
    private const int NewBadgeDays = 30;
    private const int LatestWaveDays = 7;
    private const int LatestCap = 10;
    private const int RecentCap = 8;
    private const string OnlineIdPrefix = "online.";

    private readonly struct Release
    {
        public readonly string Id;
        public readonly int Day;

        public Release(string id, int year, int month, int day)
        {
            Id = id;
            Day = new DateOnly(year, month, day).DayNumber;
        }
    }

    private static readonly Release[] Releases =
    {
        new("minesweeper", 2026, 6, 26), new("memory", 2026, 6, 26), new("match3", 2026, 6, 26),
        new("2048", 2026, 6, 26), new("watersort", 2026, 6, 26), new("breakout", 2026, 6, 26),
        new("bubbles", 2026, 6, 26), new("nonogram", 2026, 6, 26), new("flow", 2026, 6, 26),
        new("solitaire", 2026, 6, 26), new("simon", 2026, 6, 26), new("flap", 2026, 6, 26),
        new("reversi", 2026, 6, 26), new("whack", 2026, 6, 26), new("snake", 2026, 6, 26),
        new("tetris", 2026, 6, 30),
        new("beat", 2026, 7, 26), new("blade", 2026, 7, 26), new("chess", 2026, 7, 26),
        new("crystaldrop", 2026, 7, 26), new("stack", 2026, 7, 26), new("sudoku", 2026, 7, 26),
        new("trivia", 2026, 7, 26),
        new("capman", 2026, 8, 24), new("doom", 2026, 8, 24), new("hop", 2026, 8, 24),
        new("invaders", 2026, 8, 24), new("skyfall", 2026, 8, 24), new("squadron", 2026, 8, 24),
        new("wordrun", 2026, 8, 24),
        new("online.uno", 2026, 8, 25), new("online.chess", 2026, 8, 25), new("online.pool", 2026, 8, 25),
        new("online.connectfour", 2026, 9, 19),
    };

    private readonly IMiniGame[] games;
    private readonly GameStatsStore stats;
    private readonly int[] ordered;
    private readonly int[] latest;
    private readonly int[] recent;
    private readonly int[] filtered;
    private readonly long[] lastPlayed;
    private readonly string[] bestLabels;
    private int latestCount;
    private int recentCount;
    private int filteredCount;
    private LibraryFilter filteredKind;
    private string filteredQuery = string.Empty;
    private bool filterDirty = true;

    public readonly GameEntry[] Entries;
    public readonly Spring[] Lift;
    public readonly string[] MarqueeIds;

    public int Today { get; private set; }

    public GamesLibrary(IMiniGame[] games, GameStatsStore stats)
    {
        this.games = games;
        this.stats = stats;
        var kinds = OnlineGameArt.Kinds;
        Entries = new GameEntry[games.Length + kinds.Length];
        for (var index = 0; index < games.Length; index++)
        {
            var game = games[index];
            Entries[index] = new GameEntry(game.Id, index, string.Empty, game.Genre, AddedDay(game.Id));
        }

        for (var index = 0; index < kinds.Length; index++)
        {
            var id = OnlineEntryId(kinds[index]);
            Entries[games.Length + index] = new GameEntry(id, -1, kinds[index], GameGenre.Friends, AddedDay(id));
        }

        var count = Entries.Length;
        ordered = new int[count];
        latest = new int[count];
        recent = new int[count];
        filtered = new int[count];
        lastPlayed = new long[count];
        bestLabels = new string[count];
        Lift = new Spring[count];
        MarqueeIds = new string[count];
        for (var index = 0; index < count; index++)
        {
            Lift[index] = new Spring(1f);
            MarqueeIds[index] = "games.tile." + Entries[index].Id;
            bestLabels[index] = string.Empty;
        }

        BuildOrder();
        Rebuild();
    }

    public ReadOnlySpan<int> Ordered => ordered;

    public ReadOnlySpan<int> Latest => latest.AsSpan(0, latestCount);

    public ReadOnlySpan<int> Recent => recent.AsSpan(0, recentCount);

    public static LocString FilterLabel(LibraryFilter filter)
    {
        return filter switch
        {
            LibraryFilter.New => L.Games.FilterNew,
            LibraryFilter.Arcade => L.Games.GenreArcade,
            LibraryFilter.Action => L.Games.GenreAction,
            LibraryFilter.Puzzle => L.Games.GenrePuzzle,
            LibraryFilter.Brain => L.Games.GenreBrain,
            LibraryFilter.Tabletop => L.Games.GenreTabletop,
            LibraryFilter.Friends => L.Games.GenreFriends,
            _ => L.Games.FilterAll,
        };
    }

    public void Rebuild()
    {
        Today = DateOnly.FromDateTime(DateTime.UtcNow).DayNumber;
        BuildLatest();
        BuildRecent();
        BuildBestLabels();
        filterDirty = true;
    }

    public ReadOnlySpan<int> Filter(LibraryFilter kind, string query)
    {
        if (filterDirty || kind != filteredKind || !string.Equals(query, filteredQuery, StringComparison.Ordinal))
        {
            filteredKind = kind;
            filteredQuery = query;
            filterDirty = false;
            filteredCount = 0;
            var needle = query.AsSpan().Trim();
            for (var position = 0; position < ordered.Length; position++)
            {
                var entryIndex = ordered[position];
                if (Matches(entryIndex, kind, needle))
                {
                    filtered[filteredCount++] = entryIndex;
                }
            }
        }

        return filtered.AsSpan(0, filteredCount);
    }

    public string Title(int entryIndex)
    {
        ref readonly var entry = ref Entries[entryIndex];
        return entry.Online ? Loc.T(GamesOnlineText.GameName(entry.OnlineKind)) : games[entry.GameIndex].Title;
    }

    public Vector4 Accent(int entryIndex)
    {
        ref readonly var entry = ref Entries[entryIndex];
        return entry.Online ? OnlineGameArt.Accent(entry.OnlineKind) : games[entry.GameIndex].Accent;
    }

    public bool IsNew(int entryIndex) => Today - Entries[entryIndex].AddedDay <= NewBadgeDays;

    public string Best(int entryIndex) => bestLabels[entryIndex];

    public static string OnlineEntryId(string gameKind) => OnlineIdPrefix + OnlineGameArt.AccentId(gameKind);

    public string Subtitle(int entryIndex)
    {
        var best = bestLabels[entryIndex];
        return best.Length > 0 ? best : Loc.T(GameGenres.Label(Entries[entryIndex].Genre));
    }

    private bool Matches(int entryIndex, LibraryFilter kind, ReadOnlySpan<char> needle)
    {
        if (needle.Length > 0)
        {
            return Title(entryIndex).AsSpan().Contains(needle, StringComparison.OrdinalIgnoreCase);
        }

        switch (kind)
        {
            case LibraryFilter.All:
                return true;
            case LibraryFilter.New:
                return IsInLatest(entryIndex);
            case LibraryFilter.Friends:
                return Entries[entryIndex].Genre == GameGenre.Friends;
            default:
                return Entries[entryIndex].Genre == GenreOf(kind);
        }
    }

    private static GameGenre GenreOf(LibraryFilter kind)
    {
        return kind switch
        {
            LibraryFilter.Action => GameGenre.Action,
            LibraryFilter.Puzzle => GameGenre.Puzzle,
            LibraryFilter.Brain => GameGenre.Brain,
            LibraryFilter.Tabletop => GameGenre.Tabletop,
            LibraryFilter.Friends => GameGenre.Friends,
            _ => GameGenre.Arcade,
        };
    }

    private bool IsInLatest(int entryIndex)
    {
        for (var index = 0; index < latestCount; index++)
        {
            if (latest[index] == entryIndex)
            {
                return true;
            }
        }

        return false;
    }

    private void BuildOrder()
    {
        for (var index = 0; index < ordered.Length; index++)
        {
            ordered[index] = index;
        }

        for (var position = 1; position < ordered.Length; position++)
        {
            var candidate = ordered[position];
            var slot = position - 1;
            while (slot >= 0 && Entries[ordered[slot]].AddedDay < Entries[candidate].AddedDay)
            {
                ordered[slot + 1] = ordered[slot];
                slot--;
            }

            ordered[slot + 1] = candidate;
        }
    }

    private void BuildLatest()
    {
        latestCount = 0;
        if (ordered.Length == 0)
        {
            return;
        }

        var newestDay = Entries[ordered[0]].AddedDay;
        for (var position = 0; position < ordered.Length && latestCount < LatestCap; position++)
        {
            var entryIndex = ordered[position];
            if (newestDay - Entries[entryIndex].AddedDay > LatestWaveDays)
            {
                break;
            }

            latest[latestCount++] = entryIndex;
        }
    }

    private void BuildRecent()
    {
        recentCount = 0;
        for (var index = 0; index < Entries.Length; index++)
        {
            var played = stats.LastPlayed(Entries[index].Id);
            lastPlayed[index] = played;
            if (played <= 0)
            {
                continue;
            }

            var slot = recentCount - 1;
            while (slot >= 0 && lastPlayed[recent[slot]] < played)
            {
                if (slot + 1 < recent.Length)
                {
                    recent[slot + 1] = recent[slot];
                }

                slot--;
            }

            if (slot + 1 < recent.Length)
            {
                recent[slot + 1] = index;
                recentCount = Math.Min(recentCount + 1, recent.Length);
            }
        }

        recentCount = Math.Min(recentCount, RecentCap);
    }

    private void BuildBestLabels()
    {
        for (var index = 0; index < Entries.Length; index++)
        {
            bestLabels[index] = Entries[index].Online ? string.Empty : BestLabel(Entries[index].Id);
        }
    }

    private string BestLabel(string gameId)
    {
        switch (gameId)
        {
            case "2048":
            case "match3":
            case "breakout":
            case "bubbles":
            case "simon":
            case "flap":
            case "whack":
            case "snake":
            case "stack":
            case "crystaldrop":
            case "beat":
            case "blade":
            case "trivia":
            case "skyfall":
            case "invaders":
            case "capman":
            case "hop":
            case "squadron":
            case "wordrun":
            {
                var best = stats.Get(gameId).BestScore;
                return best > 0 ? BestPrefix(GameNumber.Label(best)) : string.Empty;
            }
            case "tetris":
            {
                var best = Math.Max(stats.Get(gameId).BestScore, stats.Get(TetrisApp.ModernStatId).BestScore);
                return best > 0 ? BestPrefix(GameNumber.Label(best)) : string.Empty;
            }
            case "watersort":
            case "flow":
            {
                var bestLevel = stats.Get(gameId).BestScore;
                return bestLevel > 0
                    ? BestPrefix(Loc.T(L.Games.Level) + " " + GameNumber.Label(bestLevel))
                    : string.Empty;
            }
            case "memory":
            case "solitaire":
            {
                var bestSeconds = stats.Get(gameId).BestTimeSeconds;
                return bestSeconds > 0 ? BestPrefix(TimeText.MinutesSeconds(bestSeconds)) : string.Empty;
            }
            case "minesweeper":
            case "nonogram":
            case "sudoku":
            {
                var bestSeconds = stats.Get(gameId + ".easy").BestTimeSeconds;
                return bestSeconds > 0 ? BestPrefix(TimeText.MinutesSeconds(bestSeconds)) : string.Empty;
            }
            case "reversi":
            case "chess":
            {
                var wins = stats.Get(gameId).Streak;
                return wins > 0 ? Loc.T(L.Games.Streak) + " · " + GameNumber.Label(wins) : string.Empty;
            }
            default:
                return string.Empty;
        }
    }

    private static string BestPrefix(string value) => Loc.T(L.Games.Best) + " · " + value;

    private static int AddedDay(string id)
    {
        for (var index = 0; index < Releases.Length; index++)
        {
            if (string.Equals(Releases[index].Id, id, StringComparison.Ordinal))
            {
                return Releases[index].Day;
            }
        }

        return 0;
    }
}
