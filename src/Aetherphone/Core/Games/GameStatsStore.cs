namespace Aetherphone.Core.Games;

internal sealed class GameStatsStore
{
    public const int MaxStars = 3;
    public const int MaxLevels = 999;
    private const string TetrisGameId = "tetris";
    private const int TetrisModernMode = 1;
    private const int StackStarsLimit = 256;

    private readonly IGameStatsConfiguration configuration;

    public GameStatsStore(IGameStatsConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public static int TodayIndex => (int)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerDay);

    public bool TetrisModern
    {
        get => LastMode(TetrisGameId) == TetrisModernMode;
        set => SetLastMode(TetrisGameId, value ? TetrisModernMode : 0);
    }

    public int LastMode(string gameId)
    {
        var choices = configuration.GameModeChoices;
        for (var index = 0; index < choices.Count; index++)
        {
            if (string.Equals(choices[index].GameId, gameId, StringComparison.Ordinal))
            {
                return choices[index].Mode;
            }
        }

        return string.Equals(gameId, TetrisGameId, StringComparison.Ordinal) && configuration.TetrisModern
            ? TetrisModernMode
            : 0;
    }

    public void SetLastMode(string gameId, int mode)
    {
        var choices = configuration.GameModeChoices;
        for (var index = 0; index < choices.Count; index++)
        {
            var choice = choices[index];
            if (!string.Equals(choice.GameId, gameId, StringComparison.Ordinal))
            {
                continue;
            }

            if (choice.Mode == mode)
            {
                return;
            }

            choice.Mode = mode;
            configuration.Save();
            return;
        }

        choices.Add(new GameModeChoice { GameId = gameId, Mode = mode, });
        configuration.Save();
    }

    public string WordBank
    {
        get => configuration.WordRunBank;
        set
        {
            if (string.Equals(configuration.WordRunBank, value, StringComparison.Ordinal))
            {
                return;
            }

            configuration.WordRunBank = value;
            configuration.Save();
        }
    }
    public string DailyGameId { get; set; } = string.Empty;
    public bool DailyDone => configuration.DailyChallengeLastDay == TodayIndex;
    public int DailyStreak =>
        configuration.DailyChallengeLastDay >= TodayIndex - 1 ? configuration.DailyChallengeStreak : 0;

    public GameStats Get(string gameId)
    {
        var record = Find(gameId);
        if (record is null)
        {
            return default;
        }

        return new GameStats(record.BestScore, record.BestTimeSeconds, record.Streak);
    }

    public long LastPlayed(string gameId)
    {
        var record = Find(gameId);
        return record?.LastPlayedUnixSeconds ?? 0L;
    }

    public void MarkPlayed(string gameId)
    {
        var record = GetOrCreate(gameId);
        record.LastPlayedUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        configuration.Save();
    }

    public bool SubmitScore(string gameId, int score)
    {
        CompleteDaily(gameId);
        if (score <= 0)
        {
            return false;
        }

        var record = GetOrCreate(gameId);
        if (score <= record.BestScore)
        {
            return false;
        }

        record.BestScore = score;
        configuration.Save();
        return true;
    }

    public bool SubmitTime(string gameId, int seconds)
    {
        CompleteDaily(gameId);
        if (seconds <= 0)
        {
            return false;
        }

        var record = GetOrCreate(gameId);
        if (record.BestTimeSeconds > 0 && seconds >= record.BestTimeSeconds)
        {
            return false;
        }

        record.BestTimeSeconds = seconds;
        configuration.Save();
        return true;
    }

    public int RecordWin(string gameId)
    {
        CompleteDaily(gameId);
        var record = GetOrCreate(gameId);
        record.Streak += 1;
        configuration.Save();
        return record.Streak;
    }

    public void ResetStreak(string gameId)
    {
        CompleteDaily(gameId);
        var record = Find(gameId);
        if (record is null || record.Streak == 0)
        {
            return;
        }

        record.Streak = 0;
        configuration.Save();
    }

    public int Stars(string gameId, int level)
    {
        var progress = FindProgress(gameId);
        return progress is null ? 0 : StarsAt(progress.Stars, level);
    }

    public bool SetStars(string gameId, int level, int stars)
    {
        var earned = Math.Clamp(stars, 0, MaxStars);
        if (level < 1 || level > MaxLevels || earned == 0)
        {
            return false;
        }

        var progress = FindProgress(gameId);
        if (progress is not null && StarsAt(progress.Stars, level) >= earned)
        {
            return false;
        }

        if (progress is null)
        {
            progress = new GameLevelProgress { GameId = gameId, };
            configuration.GameLevelProgress.Add(progress);
        }

        progress.Stars = WithStars(progress.Stars, level, earned);
        configuration.Save();
        return true;
    }

    public int TotalStars(string gameId)
    {
        var progress = FindProgress(gameId);
        if (progress is null)
        {
            return 0;
        }

        var total = 0;
        for (var level = 1; level <= progress.Stars.Length; level++)
        {
            total += StarsAt(progress.Stars, level);
        }

        return total;
    }

    public bool IsUnlocked(string gameId, int level) =>
        level == 1 || (level > 1 && Stars(gameId, level - 1) > 0);

    public int HighestUnlocked(string gameId, int levelCount = MaxLevels)
    {
        var limit = Math.Clamp(levelCount, 1, MaxLevels);
        var progress = FindProgress(gameId);
        var highest = 1;
        if (progress is null)
        {
            return highest;
        }

        for (var level = 1; level < limit && level <= progress.Stars.Length; level++)
        {
            if (StarsAt(progress.Stars, level) > 0)
            {
                highest = level + 1;
            }
        }

        return highest;
    }

    public int NextLevel(string gameId, int levelCount)
    {
        if (levelCount <= 0)
        {
            return 0;
        }

        var limit = Math.Min(levelCount, MaxLevels);
        for (var level = 1; level <= limit; level++)
        {
            if (IsUnlocked(gameId, level) && Stars(gameId, level) == 0)
            {
                return level;
            }
        }

        for (var level = 1; level <= limit; level++)
        {
            if (IsUnlocked(gameId, level) && Stars(gameId, level) < MaxStars)
            {
                return level;
            }
        }

        return 1;
    }

    public void CompleteDaily(string gameId)
    {
        var daily = DailyGameId;
        if (daily.Length == 0 || !MatchesGame(gameId, daily))
        {
            return;
        }

        var today = TodayIndex;
        if (configuration.DailyChallengeLastDay == today)
        {
            return;
        }

        configuration.DailyChallengeStreak =
            configuration.DailyChallengeLastDay == today - 1 ? configuration.DailyChallengeStreak + 1 : 1;
        configuration.DailyChallengeLastDay = today;
        configuration.Save();
    }

    private static bool MatchesGame(string statId, string gameId)
    {
        if (string.Equals(statId, gameId, StringComparison.Ordinal))
        {
            return true;
        }

        return statId.Length > gameId.Length && statId[gameId.Length] == '.' &&
               statId.AsSpan(0, gameId.Length).SequenceEqual(gameId.AsSpan());
    }

    private GameStatRecord? Find(string gameId)
    {
        var records = configuration.GameStats;
        for (var index = 0; index < records.Count; index++)
        {
            if (string.Equals(records[index].GameId, gameId, StringComparison.Ordinal))
            {
                return records[index];
            }
        }

        return null;
    }

    private GameLevelProgress? FindProgress(string gameId)
    {
        var progress = configuration.GameLevelProgress;
        for (var index = 0; index < progress.Count; index++)
        {
            if (string.Equals(progress[index].GameId, gameId, StringComparison.Ordinal))
            {
                return progress[index];
            }
        }

        return null;
    }

    private static int StarsAt(string stars, int level)
    {
        if (level < 1 || level > stars.Length)
        {
            return 0;
        }

        var digit = stars[level - 1] - '0';
        return digit is >= 0 and <= MaxStars ? digit : 0;
    }

    private static string WithStars(string stars, int level, int earned)
    {
        var length = Math.Max(stars.Length, level);
        var buffer = length <= StackStarsLimit ? stackalloc char[length] : new char[length];
        for (var index = 0; index < length; index++)
        {
            buffer[index] = index < stars.Length ? stars[index] : '0';
        }

        buffer[level - 1] = (char)('0' + earned);
        return new string(buffer);
    }

    private GameStatRecord GetOrCreate(string gameId)
    {
        var existing = Find(gameId);
        if (existing is not null)
        {
            return existing;
        }

        var created = new GameStatRecord { GameId = gameId, };
        configuration.GameStats.Add(created);
        return created;
    }
}
