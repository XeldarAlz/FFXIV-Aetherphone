using System.Globalization;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.GemSwap;
using Aetherphone.Apps.Games.Gloop;
using Aetherphone.Apps.Games.Online;
using Aetherphone.Apps.Games.Siege;
using Aetherphone.Apps.Games.Slice;
using Aetherphone.Apps.Games.Snake;
using Aetherphone.Apps.Games.Solitaire;
using Aetherphone.Apps.Games.Tetris;
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
    Count,
    Stars,
}

internal enum GamesSort : byte
{
    Newest,
    Title,
    Recent,
}

internal enum GamesFilter : byte
{
    All,
    Arcade,
    Action,
    Puzzle,
    Brain,
    Strategy,
    Tabletop,
    Together,
}

internal static class GamesFilters
{
    public const int Count = (int)GamesFilter.Together + 1;

    public static GameGenre Genre(GamesFilter filter) => (GameGenre)((int)filter - 1);

    public static bool Matches(GamesFilter filter, GameGenre genre) =>
        filter == GamesFilter.All || Genre(filter) == genre;

    public static LocString Label(GamesFilter filter) => filter switch
    {
        GamesFilter.All => L.GamesHub.FilterAll,
        GamesFilter.Together => L.GamesHub.TabTogether,
        _ => GameGenres.Label(Genre(filter)),
    };
}

internal static class GamesSorts
{
    public const int Count = (int)GamesSort.Recent + 1;

    public static LocString Label(GamesSort sort) => sort switch
    {
        GamesSort.Title => L.GamesHub.SortTitle,
        GamesSort.Recent => L.GamesHub.SortRecent,
        _ => L.GamesHub.SortNewest,
    };
}

internal sealed class GamesLibrary
{
    public const int NewDays = 14;
    public const int LatestDays = 14;
    public const int RankCap = 999;
    private const int RecentCap = 8;
    private const int ShelfGenreCount = (int)GameGenre.Friends;
    private const string OnlineIdPrefix = "online.";
    private const string TileIdPrefix = "games.tile.";
    private const string PlayIdPrefix = "games.play.";
    private const string Separator = " · ";

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
        new("pinball", 2026, 10, 8),
        new("crates", 2026, 10, 8),
        new("delve", 2026, 10, 8),
        new("moogleclicker", 2026, 10, 8),
        new("claim", 2026, 10, 8),
        new("lander", 2026, 10, 8),
        new("luckydraw", 2026, 10, 8),
        new("broadside", 2026, 10, 8),
        new("pegfall", 2026, 10, 8),
        new("fling", 2026, 10, 8),
        new("siege", 2026, 10, 8),
        new("crater", 2026, 10, 8),
        new("fuse", 2026, 10, 8),
        new("snip", 2026, 10, 8),
        new("minigolf", 2026, 10, 8),
        new("online.broadside", 2026, 10, 8),
        new("online.luckydraw", 2026, 10, 8),
        new("herd", 2026, 10, 8),
        new("tempo", 2026, 10, 8),
        new("online.crater", 2026, 10, 8),
        new("online.minigolf", 2026, 10, 8),
    };

    private static readonly int GenreCount = GameGenres.Shelves.Length;
    private static readonly string[] TierSuffixes = { ".easy", ".medium", ".hard" };
    private static readonly LocString[] TierLabels = { L.Games.Easy, L.Games.Medium, L.Games.Hard };

    private readonly IMiniGame[] games;
    private readonly GameStatsStore stats;
    private readonly IRankSource ranks;
    private readonly int[] ordered;
    private readonly int[] latest;
    private readonly int[] recent;
    private readonly int[] records;
    private readonly int[] byGenre;
    private readonly int[] genreStart;
    private readonly int[] genreLength;
    private readonly int[] byTitle;
    private readonly int[] byRecent;
    private readonly int[] view;
    private readonly int[] genrePlays = new int[ShelfGenreCount];
    private readonly GameGenre[] genreOrder = new GameGenre[ShelfGenreCount];
    private readonly long[] lastPlayed;
    private readonly string[] bestValues;
    private readonly string[] bestTiers;
    private readonly RecordKind[] bestKinds;
    private readonly string[] rankLabels;
    private readonly int[] bestRanks;
    private readonly string[] hooks;
    private readonly string[] eyebrows;
    private readonly string[] metaLabels;
    private readonly string[] progressLabels;
    private readonly float[] progress;
    private readonly int[] stars;
    private readonly int[] starMax;
    private int latestCount;
    private int recentCount;
    private int recordCount;
    private int playedCount;
    private int viewCount;
    private int viewVersion = -1;
    private GamesFilter viewFilter;
    private GamesSort viewSort;
    private string viewQuery = string.Empty;
    private LanguageInfo? labelLanguage;
    private int labelFormatVersion = -1;

    public readonly GameEntry[] Entries;
    public readonly string[] IconIds;
    public readonly string[] TileIds;
    public readonly string[] PlayIds;

    public int Today { get; private set; }

    public int Version { get; private set; }

    public int TotalStars { get; private set; }

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
        records = new int[count];
        byGenre = new int[count];
        genreStart = new int[GenreCount];
        genreLength = new int[GenreCount];
        byTitle = new int[count];
        byRecent = new int[count];
        view = new int[count];
        lastPlayed = new long[count];
        bestValues = new string[count];
        bestTiers = new string[count];
        bestKinds = new RecordKind[count];
        rankLabels = new string[count];
        bestRanks = new int[count];
        hooks = new string[count];
        eyebrows = new string[count];
        metaLabels = new string[count];
        progressLabels = new string[count];
        progress = new float[count];
        stars = new int[count];
        starMax = new int[count];
        IconIds = new string[count];
        TileIds = new string[count];
        PlayIds = new string[count];
        for (var index = 0; index < count; index++)
        {
            ref readonly var entry = ref Entries[index];
            IconIds[index] = entry.Online ? OnlineGameArt.AccentId(entry.OnlineKind) : entry.Id;
            TileIds[index] = TileIdPrefix + entry.Id;
            PlayIds[index] = PlayIdPrefix + entry.Id;
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

    public ReadOnlySpan<GameGenre> GenreOrder => genreOrder;

    public int PlayedCount => playedCount;

    public ReadOnlySpan<int> Genre(GameGenre genre)
    {
        var slot = (int)genre;
        return slot < GenreCount ? byGenre.AsSpan(genreStart[slot], genreLength[slot]) : ReadOnlySpan<int>.Empty;
    }

    public void Rebuild() => Rebuild(DateOnly.FromDateTime(DateTime.UtcNow).DayNumber);

    public void Rebuild(int today)
    {
        Today = today;
        BuildLatest();
        BuildRecent();
        BuildLabels();
        BuildRecords();
        Version++;
    }

    public void EnsureLanguage()
    {
        if (ReferenceEquals(labelLanguage, Loc.Current) && labelFormatVersion == TimeText.FormatVersion)
        {
            return;
        }

        BuildLabels();
        Version++;
    }

    public ReadOnlySpan<int> View(GamesFilter filter, GamesSort sort, string query)
    {
        if (viewVersion == Version && filter == viewFilter && sort == viewSort && ReferenceEquals(query, viewQuery))
        {
            return view.AsSpan(0, viewCount);
        }

        viewVersion = Version;
        viewFilter = filter;
        viewSort = sort;
        viewQuery = query;
        viewCount = 0;
        var source = sort switch
        {
            GamesSort.Title => byTitle,
            GamesSort.Recent => byRecent,
            _ => ordered,
        };
        var needle = query.AsSpan().Trim();
        for (var position = 0; position < source.Length; position++)
        {
            var entryIndex = source[position];
            if (!GamesFilters.Matches(filter, Entries[entryIndex].Genre))
            {
                continue;
            }

            if (needle.Length > 0 && !MatchesQuery(entryIndex, needle))
            {
                continue;
            }

            view[viewCount++] = entryIndex;
        }

        return view.AsSpan(0, viewCount);
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

    public static bool IsFreshOn(int today, int addedDay) => addedDay > 0 && today - addedDay <= NewDays;

    public static bool IsNewOn(int today, int addedDay, long lastPlayed) =>
        IsFreshOn(today, addedDay) && lastPlayed <= 0;

    public bool IsNew(int entryIndex) =>
        IsNewOn(Today, Entries[entryIndex].AddedDay, lastPlayed[entryIndex]);

    public string Hook(int entryIndex) => hooks[entryIndex];

    public string Meta(int entryIndex) => metaLabels[entryIndex];

    public string Eyebrow(int entryIndex) => eyebrows[entryIndex];

    public string ProgressLabel(int entryIndex) => progressLabels[entryIndex];

    public float Progress(int entryIndex) => progress[entryIndex];

    public int Stars(int entryIndex) => stars[entryIndex];

    public int StarMax(int entryIndex) => starMax[entryIndex];

    public int StarTier(int entryIndex)
    {
        var max = starMax[entryIndex];
        var earned = stars[entryIndex];
        if (max <= 0 || earned <= 0)
        {
            return 0;
        }

        return Math.Clamp(earned * GameStatsStore.MaxStars / max, 1, GameStatsStore.MaxStars);
    }

    public int Rank(int entryIndex) => bestRanks[entryIndex];

    public string BestValue(int entryIndex) => bestValues[entryIndex];

    public string BestTier(int entryIndex) => bestTiers[entryIndex];

    public RecordKind BestKind(int entryIndex) => bestKinds[entryIndex];

    public LocString KindLabel(int entryIndex) => bestKinds[entryIndex] switch
    {
        RecordKind.Time => L.GamesHub.KindTime,
        RecordKind.Level => L.GamesHub.KindLevel,
        RecordKind.Streak => L.GamesHub.KindStreak,
        RecordKind.Stars => L.GamesHub.KindStars,
        RecordKind.Count => games[Entries[entryIndex].GameIndex].Spec.Unit ?? L.GamesHub.KindScore,
        _ => L.GamesHub.KindScore,
    };

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
        for (var position = 0; position < ordered.Length; position++)
        {
            var entryIndex = ordered[position];
            if (newestDay - Entries[entryIndex].AddedDay > LatestDays)
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

        BuildRecentOrder();
        BuildGenreOrder();
        recentCount = Math.Min(recentCount, RecentCap);
    }

    private void BuildRecentOrder()
    {
        Array.Copy(recent, byRecent, recentCount);
        var slot = recentCount;
        for (var position = 0; position < ordered.Length; position++)
        {
            var entryIndex = ordered[position];
            if (lastPlayed[entryIndex] <= 0)
            {
                byRecent[slot++] = entryIndex;
            }
        }
    }

    private void BuildGenreOrder()
    {
        Array.Clear(genrePlays);
        for (var index = 0; index < Entries.Length; index++)
        {
            var genre = (int)Entries[index].Genre;
            if (lastPlayed[index] > 0 && genre < ShelfGenreCount)
            {
                genrePlays[genre]++;
            }
        }

        for (var position = 0; position < genreOrder.Length; position++)
        {
            var candidate = (GameGenre)position;
            var slot = position - 1;
            while (slot >= 0 && genrePlays[(int)genreOrder[slot]] < genrePlays[position])
            {
                genreOrder[slot + 1] = genreOrder[slot];
                slot--;
            }

            genreOrder[slot + 1] = candidate;
        }
    }

    private void BuildTitleOrder()
    {
        Array.Copy(ordered, byTitle, ordered.Length);
        var compare = Loc.Culture.CompareInfo;
        for (var position = 1; position < byTitle.Length; position++)
        {
            var candidate = byTitle[position];
            var title = Title(candidate);
            var slot = position - 1;
            while (slot >= 0 && compare.Compare(Title(byTitle[slot]), title, CompareOptions.IgnoreCase) > 0)
            {
                byTitle[slot + 1] = byTitle[slot];
                slot--;
            }

            byTitle[slot + 1] = candidate;
        }
    }

    private bool MatchesQuery(int entryIndex, ReadOnlySpan<char> needle) =>
        Title(entryIndex).AsSpan().Contains(needle, StringComparison.OrdinalIgnoreCase)
        || Loc.T(GameGenres.Label(Entries[entryIndex].Genre)).AsSpan()
            .Contains(needle, StringComparison.OrdinalIgnoreCase);

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

    private void BuildLabels()
    {
        labelLanguage = Loc.Current;
        labelFormatVersion = TimeText.FormatVersion;
        var newGame = Loc.T(L.GamesHub.NewGame);
        var newWord = Loc.T(L.GamesHub.New);
        TotalStars = 0;
        for (var index = 0; index < Entries.Length; index++)
        {
            BuildBest(index);
            var genre = Loc.T(GameGenres.Label(Entries[index].Genre));
            hooks[index] = HookFor(index);
            eyebrows[index] = IsFreshOn(Today, Entries[index].AddedDay)
                ? string.Concat(newGame, Separator, genre)
                : genre;
            metaLabels[index] = IsNew(index) ? newWord : genre;
            BuildProgress(index);
            TotalStars += stars[index];
        }

        BuildRankLabels();
        BuildTitleOrder();
    }

    private void BuildBest(int entryIndex)
    {
        var value = string.Empty;
        var tier = -1;
        var kind = Entries[entryIndex].Online
            ? RecordKind.None
            : BestRecord(games[Entries[entryIndex].GameIndex].Spec, out value, out tier);
        bestKinds[entryIndex] = kind;
        bestValues[entryIndex] = kind == RecordKind.None ? string.Empty : value;
        bestTiers[entryIndex] = kind != RecordKind.None && tier >= 0 ? Loc.T(TierLabels[tier]) : string.Empty;
    }

    private void BuildProgress(int entryIndex)
    {
        stars[entryIndex] = 0;
        starMax[entryIndex] = 0;
        progress[entryIndex] = -1f;
        ref readonly var entry = ref Entries[entryIndex];
        if (entry.Online)
        {
            progressLabels[entryIndex] = metaLabels[entryIndex];
            return;
        }

        var spec = games[entry.GameIndex].Spec;
        if (spec.LevelCount <= 0)
        {
            progressLabels[entryIndex] = BestLine(entryIndex);
            return;
        }

        var earned = stats.TotalStars(spec.Id);
        var max = StarTotal.Max(spec.LevelCount);
        stars[entryIndex] = earned;
        starMax[entryIndex] = max;
        if (spec.Kind == ScoreKind.Level)
        {
            progress[entryIndex] = Math.Clamp((float)earned / max, 0f, 1f);
            progressLabels[entryIndex] = StarTotal.Label(earned, spec.LevelCount);
            return;
        }

        var current = stats.HighestUnlocked(spec.Id, spec.LevelCount);
        var cleared = current - 1 + (stats.Stars(spec.Id, current) > 0 ? 1 : 0);
        progress[entryIndex] = Math.Clamp((float)cleared / spec.LevelCount, 0f, 1f);
        progressLabels[entryIndex] = Loc.T(L.GamesHub.LevelOf, GameNumber.Label(current),
            GameNumber.Label(spec.LevelCount));
    }

    private string BestLine(int entryIndex)
    {
        var value = bestValues[entryIndex];
        var line = bestKinds[entryIndex] switch
        {
            RecordKind.None => string.Empty,
            RecordKind.Stars => value,
            RecordKind.Level => Loc.T(L.Stage.LevelNumber, value),
            RecordKind.Streak => Loc.T(L.GamesHub.StreakValue, value),
            _ => Loc.T(L.GamesHub.BestValue, value),
        };
        return line.Length == 0 ? metaLabels[entryIndex] : WithTier(line, bestTiers[entryIndex]);
    }

    private static string WithTier(string label, string tier) =>
        tier.Length > 0 ? string.Concat(label, Separator, tier) : label;

    private string HookFor(int entryIndex)
    {
        ref readonly var entry = ref Entries[entryIndex];
        if (entry.Online)
        {
            return OnlineGameArt.Hint(entry.OnlineKind);
        }

        var hook = games[entry.GameIndex].Spec.Hook;
        return hook.HasValue ? Loc.T(hook.Value) : string.Empty;
    }

    private void BuildRankLabels()
    {
        for (var index = 0; index < Entries.Length; index++)
        {
            var best = Entries[index].Online ? 0 : BestRank(Entries[index].Id);
            bestRanks[index] = best;
            rankLabels[index] = best is > 0 and <= RankCap
                ? Loc.T(L.Leaderboard.RankChip, GameNumber.Label(best))
                : string.Empty;
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

    private RecordKind BestRecord(in GameSpec spec, out string value, out int tier)
    {
        var stars = spec.LevelCount > 0 && spec.Kind == ScoreKind.Level ? stats.TotalStars(spec.Id) : 0;
        if (stars <= 0)
        {
            return BestRecord(spec.Id, out value, out tier);
        }

        tier = -1;
        value = StarTotal.Label(stars, spec.LevelCount);
        return RecordKind.Stars;
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
            case "pinball":
            case "claim":
            case "lander":
            case "pegfall":
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
            case "moogleclicker":
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
            case "siege":
                return EndlessWaves(out value);
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
            case "luckydraw":
            case "broadside":
            case "crater":
            case "fuse":
                return BestStreakAcrossTiers(gameId, out value, out tier);
            case "minigolf":
                return Count(stats.Best(gameId, ScoreKind.Count), out value);
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

    private RecordKind EndlessWaves(out string value)
    {
        var waves = stats.Get(SiegeApp.EndlessStatId).BestScore;
        value = waves > 0 ? Loc.T(L.Siege.WavesRecord, GameNumber.Label(waves)) : string.Empty;
        return waves > 0 ? RecordKind.Score : RecordKind.None;
    }

    private static RecordKind Score(int best, out string value)
    {
        value = best > 0 ? GameNumber.Label(best) : string.Empty;
        return best > 0 ? RecordKind.Score : RecordKind.None;
    }

    private static RecordKind Count(int best, out string value)
    {
        value = best > 0 ? GameNumber.Label(best) : string.Empty;
        return best > 0 ? RecordKind.Count : RecordKind.None;
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
