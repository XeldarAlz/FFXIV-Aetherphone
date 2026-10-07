using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.GemSwap;
using Aetherphone.Apps.Games.Gloop;
using Aetherphone.Apps.Games.Herd;
using Aetherphone.Apps.Games.Online;
using Aetherphone.Apps.Games.Slice;
using Aetherphone.Apps.Games.Snake;
using Aetherphone.Apps.Games.Solitaire;
using Aetherphone.Apps.Games.Tempo;
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

internal enum RecordKind : byte
{
    None,
    Score,
    Time,
    Level,
    Streak,
}

internal sealed class GamesLibrary
{
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
        new("coil", 2026, 10, 3), new("updraft", 2026, 10, 3), new("swoop", 2026, 10, 3),
        new("online.connectfour", 2026, 10, 3),
        new("slice", 2026, 10, 8),
        new("spiral", 2026, 10, 8),
        new("mahjong", 2026, 10, 8),
        new("gloop", 2026, 10, 8),
        new("drift", 2026, 10, 8),
        new("crawler", 2026, 10, 8),
        new("trails", 2026, 10, 8),
        new("trailblaze", 2026, 10, 8),
        new("thrust", 2026, 10, 8),
        new("herd", 2026, 10, 8),
        new("tempo", 2026, 10, 8),
    };

    private static readonly int GenreCount = GameGenres.Shelves.Length;
    private static readonly string[] TierSuffixes = { ".easy", ".medium", ".hard" };
    private static readonly LocString[] TierLabels = { L.Games.Easy, L.Games.Medium, L.Games.Hard };

    private readonly IMiniGame[] games;
    private readonly GameStatsStore stats;
    private readonly int[] ordered;
    private readonly int[] latest;
    private readonly int[] recent;
    private readonly int[] searched;
    private readonly int[] records;
    private readonly int[] byGenre;
    private readonly int[] genreStart;
    private readonly int[] genreLength;
    private readonly long[] lastPlayed;
    private readonly string[] bestLabels;
    private readonly string[] bestValues;
    private readonly string[] bestTiers;
    private readonly RecordKind[] bestKinds;
    private readonly string[] rankLabels;
    private readonly IRankSource ranks;
    private int latestCount;
    private int recentCount;
    private int searchedCount;
    private int recordCount;
    private int playedCount;
    private string searchedQuery = string.Empty;
    private bool searchDirty = true;
    private LanguageInfo? labelLanguage;
    private int labelFormatVersion = -1;

    public readonly GameEntry[] Entries;
    public readonly Spring[] Lift;
    public readonly string[] MarqueeIds;

    public int Today { get; private set; }

    public int Version { get; private set; }

    public GamesLibrary(IMiniGame[] games, GameStatsStore stats, IRankSource? ranks = null)
    {
        this.games = games;
        this.stats = stats;
        this.ranks = ranks ?? NullRankSource.Instance;
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
        searched = new int[count];
        records = new int[count];
        byGenre = new int[count];
        genreStart = new int[GenreCount];
        genreLength = new int[GenreCount];
        lastPlayed = new long[count];
        bestLabels = new string[count];
        bestValues = new string[count];
        bestTiers = new string[count];
        bestKinds = new RecordKind[count];
        rankLabels = new string[count];
        Lift = new Spring[count];
        MarqueeIds = new string[count];
        for (var index = 0; index < count; index++)
        {
            Lift[index] = new Spring(1f);
            MarqueeIds[index] = "games.tile." + Entries[index].Id;
            bestLabels[index] = string.Empty;
            bestValues[index] = string.Empty;
            bestTiers[index] = string.Empty;
            rankLabels[index] = string.Empty;
        }

        BuildOrder();
        BuildGenres();
        Rebuild();
    }

    public ReadOnlySpan<int> Ordered => ordered;

    public ReadOnlySpan<int> Latest => latest.AsSpan(0, latestCount);

    public ReadOnlySpan<int> Recent => recent.AsSpan(0, recentCount);

    public ReadOnlySpan<int> Records => records.AsSpan(0, recordCount);

    public int PlayedCount => playedCount;

    public ReadOnlySpan<int> Genre(GameGenre genre)
    {
        var slot = (int)genre;
        return slot < GenreCount ? byGenre.AsSpan(genreStart[slot], genreLength[slot]) : ReadOnlySpan<int>.Empty;
    }

    public void Rebuild()
    {
        Today = DateOnly.FromDateTime(DateTime.UtcNow).DayNumber;
        BuildLatest();
        BuildRecent();
        BuildBestLabels();
        BuildRecords();
        searchDirty = true;
        Version++;
    }

    public void EnsureLanguage()
    {
        if (ReferenceEquals(labelLanguage, Loc.Current) && labelFormatVersion == TimeText.FormatVersion)
        {
            return;
        }

        BuildBestLabels();
        searchDirty = true;
        Version++;
    }

    public ReadOnlySpan<int> Search(string query)
    {
        if (!searchDirty && string.Equals(query, searchedQuery, StringComparison.Ordinal))
        {
            return searched.AsSpan(0, searchedCount);
        }

        searchedQuery = query;
        searchDirty = false;
        searchedCount = 0;
        var needle = query.AsSpan().Trim();
        if (needle.Length == 0)
        {
            return ReadOnlySpan<int>.Empty;
        }

        for (var position = 0; position < ordered.Length; position++)
        {
            var entryIndex = ordered[position];
            if (Title(entryIndex).AsSpan().Contains(needle, StringComparison.OrdinalIgnoreCase)
                || Loc.T(GameGenres.Label(Entries[entryIndex].Genre)).AsSpan()
                    .Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                searched[searchedCount++] = entryIndex;
            }
        }

        return searched.AsSpan(0, searchedCount);
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

    public string BestValue(int entryIndex) => bestValues[entryIndex];

    public string BestTier(int entryIndex) => bestTiers[entryIndex];

    public RecordKind BestKind(int entryIndex) => bestKinds[entryIndex];

    public string RankLabel(int entryIndex) => rankLabels[entryIndex];

    public void RefreshRanks()
    {
        BuildRankLabels();
        Version++;
    }

    public int IndexOf(string id)
    {
        for (var index = 0; index < Entries.Length; index++)
        {
            if (string.Equals(Entries[index].Id, id, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    public static string OnlineEntryId(string gameKind) => OnlineIdPrefix + OnlineGameArt.AccentId(gameKind);

    public string Subtitle(int entryIndex)
    {
        var best = bestLabels[entryIndex];
        return best.Length > 0 ? best : Loc.T(GameGenres.Label(Entries[entryIndex].Genre));
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

    private void BuildGenres()
    {
        var cursor = 0;
        for (var genre = 0; genre < GenreCount; genre++)
        {
            genreStart[genre] = cursor;
            for (var position = 0; position < ordered.Length; position++)
            {
                var entryIndex = ordered[position];
                if ((int)Entries[entryIndex].Genre == genre)
                {
                    byGenre[cursor++] = entryIndex;
                }
            }

            genreLength[genre] = cursor - genreStart[genre];
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
        playedCount = 0;
        for (var index = 0; index < Entries.Length; index++)
        {
            var played = stats.LastPlayed(Entries[index].Id);
            lastPlayed[index] = played;
            if (played <= 0)
            {
                continue;
            }

            playedCount++;
            InsertByRecency(recent, ref recentCount, index);
        }

        recentCount = Math.Min(recentCount, RecentCap);
    }

    private void BuildRecords()
    {
        recordCount = 0;
        for (var index = 0; index < Entries.Length; index++)
        {
            if (bestKinds[index] != RecordKind.None)
            {
                InsertByRecency(records, ref recordCount, index);
            }
        }
    }

    private void InsertByRecency(int[] target, ref int count, int entryIndex)
    {
        var played = lastPlayed[entryIndex];
        var slot = count - 1;
        while (slot >= 0 && lastPlayed[target[slot]] < played)
        {
            target[slot + 1] = target[slot];
            slot--;
        }

        target[slot + 1] = entryIndex;
        count++;
    }

    private void BuildBestLabels()
    {
        labelLanguage = Loc.Current;
        labelFormatVersion = TimeText.FormatVersion;
        for (var index = 0; index < Entries.Length; index++)
        {
            var value = string.Empty;
            var tier = -1;
            var kind = Entries[index].Online ? RecordKind.None : BestRecord(Entries[index].Id, out value, out tier);
            bestKinds[index] = kind;
            bestValues[index] = kind == RecordKind.None ? string.Empty : value;
            bestTiers[index] = kind != RecordKind.None && tier >= 0 ? Loc.T(TierLabels[tier]) : string.Empty;
            var label = kind switch
            {
                RecordKind.None => string.Empty,
                RecordKind.Streak => Loc.T(L.Games.Streak) + " · " + value,
                RecordKind.Level => Loc.T(L.Games.Best) + " · " + Loc.T(L.Games.Level) + " " + value,
                _ => Loc.T(L.Games.Best) + " · " + value,
            };
            bestLabels[index] = bestTiers[index].Length > 0 ? label + " · " + bestTiers[index] : label;
        }

        BuildRankLabels();
    }

    private void BuildRankLabels()
    {
        for (var index = 0; index < Entries.Length; index++)
        {
            var best = Entries[index].Online ? 0 : BestRank(Entries[index].Id);
            rankLabels[index] = best > 0 ? Loc.T(L.Leaderboard.RankChip, GameNumber.Label(best)) : string.Empty;
        }
    }

    private int BestRank(string gameId)
    {
        var best = 0;
        var ids = ScoreStatIds.All;
        for (var index = 0; index < ids.Length; index++)
        {
            if (!ScoreStatIds.BelongsTo(ids[index], gameId) || !ranks.TryGetRank(ids[index], out var rank)
                || !rank.IsRanked)
            {
                continue;
            }

            if (best == 0 || rank.Rank < best)
            {
                best = rank.Rank;
            }
        }

        return best;
    }

    private RecordKind BestRecord(string gameId, out string value, out int tier)
    {
        value = string.Empty;
        tier = -1;
        switch (gameId)
        {
            case "2048":
            case "breakout":
            case "bubbles":
            case "simon":
            case "flap":
            case "whack":
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
            case "coil":
            case "updraft":
            case "swoop":
            case "spiral":
            case "drift":
            case "crawler":
            case "trailblaze":
            case "thrust":
                return Score(stats.Get(gameId).BestScore, out value);
            case "match3":
                return Score(Math.Max(stats.Get(gameId).BestScore, stats.Get(GemSwapApp.BlitzStatId).BestScore),
                    out value);
            case "tetris":
                return Score(Math.Max(stats.Get(gameId).BestScore, stats.Get(TetrisApp.ModernStatId).BestScore),
                    out value);
            case "snake":
                return Score(Math.Max(stats.Get(gameId).BestScore, stats.Get(SnakeApp.WrapStatId).BestScore),
                    out value);
            case "slice":
                return Score(Math.Max(stats.Get(gameId).BestScore, stats.Get(SliceApp.ArcadeStatId).BestScore),
                    out value);
            case "gloop":
            {
                var endless = Score(stats.Get(gameId).BestScore, out value);
                if (endless != RecordKind.None)
                {
                    return endless;
                }

                var streak = stats.Get(GloopApp.VersusStatId).Streak;
                value = streak > 0 ? GameNumber.Label(streak) : string.Empty;
                return streak > 0 ? RecordKind.Streak : RecordKind.None;
            }
            case "watersort":
            {
                var bestLevel = stats.Get(gameId).BestScore;
                if (bestLevel <= 0)
                {
                    return RecordKind.None;
                }

                value = GameNumber.Label(bestLevel);
                return RecordKind.Level;
            }
            case "flow":
                return BestLevelAcrossTiers(gameId, out value, out tier);
            case "memory":
                return Time(stats.Get(gameId).BestTimeSeconds, out value);
            case "solitaire":
            {
                var classic = Time(stats.Get(gameId).BestTimeSeconds, out value);
                return classic != RecordKind.None
                    ? classic
                    : Score(stats.Get(SolitaireApp.VegasStatId).BestScore, out value);
            }
            case "minesweeper":
            case "nonogram":
            case "sudoku":
            case "mahjong":
                return BestTimeAcrossTiers(gameId, out value, out tier);
            case "reversi":
            case "chess":
            case "trails":
                return BestStreakAcrossTiers(gameId, out value, out tier);
            case "herd":
            case "tempo":
            {
                var stars = stats.TotalStars(gameId);
                var levels = gameId == "herd" ? HerdLevels.Count : TempoLevels.Count;
                value = stars > 0
                    ? Loc.T(L.Stage.StarsOf, GameNumber.Label(stars),
                        GameNumber.Label(levels * GameStatsStore.MaxStars))
                    : string.Empty;
                return stars > 0 ? RecordKind.Score : RecordKind.None;
            }
            default:
                return RecordKind.None;
        }
    }

    private RecordKind BestStreakAcrossTiers(string gameId, out string value, out int tier)
    {
        value = string.Empty;
        tier = -1;
        var bestStreak = stats.Get(gameId).Streak;
        for (var index = 0; index < TierSuffixes.Length; index++)
        {
            var streak = stats.Get(string.Concat(gameId, TierSuffixes[index])).Streak;
            if (streak <= bestStreak)
            {
                continue;
            }

            bestStreak = streak;
            tier = index;
        }

        if (bestStreak <= 0)
        {
            return RecordKind.None;
        }

        value = GameNumber.Label(bestStreak);
        return RecordKind.Streak;
    }

    private RecordKind BestLevelAcrossTiers(string gameId, out string value, out int tier)
    {
        value = string.Empty;
        tier = -1;
        var bestLevel = 0;
        for (var index = 0; index < TierSuffixes.Length; index++)
        {
            var level = stats.Get(string.Concat(gameId, TierSuffixes[index])).BestScore;
            if (level <= bestLevel)
            {
                continue;
            }

            bestLevel = level;
            tier = index;
        }

        if (bestLevel <= 0)
        {
            return RecordKind.None;
        }

        value = GameNumber.Label(bestLevel);
        return RecordKind.Level;
    }

    private RecordKind BestTimeAcrossTiers(string gameId, out string value, out int tier)
    {
        value = string.Empty;
        tier = -1;
        var bestSeconds = 0;
        for (var index = 0; index < TierSuffixes.Length; index++)
        {
            var seconds = stats.Get(string.Concat(gameId, TierSuffixes[index])).BestTimeSeconds;
            if (seconds <= 0 || (bestSeconds > 0 && seconds >= bestSeconds))
            {
                continue;
            }

            bestSeconds = seconds;
            tier = index;
        }

        return Time(bestSeconds, out value);
    }

    private static RecordKind Score(int best, out string value)
    {
        value = best > 0 ? GameNumber.Label(best) : string.Empty;
        return best > 0 ? RecordKind.Score : RecordKind.None;
    }

    private static RecordKind Time(int seconds, out string value)
    {
        value = seconds > 0 ? TimeText.MinutesSeconds(seconds) : string.Empty;
        return seconds > 0 ? RecordKind.Time : RecordKind.None;
    }

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
