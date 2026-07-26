namespace Aetherphone.Core.Games;

internal sealed class GameStatsStore
{
    private readonly Configuration configuration;

    public GameStatsStore(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public static int TodayIndex => (int)(DateTime.UtcNow.Ticks / TimeSpan.TicksPerDay);
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

    public bool SubmitScore(string gameId, int score)
    {
        RecordDailyPlay(gameId);
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
        RecordDailyPlay(gameId);
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
        RecordDailyPlay(gameId);
        var record = GetOrCreate(gameId);
        record.Streak += 1;
        configuration.Save();
        return record.Streak;
    }

    public void ResetStreak(string gameId)
    {
        RecordDailyPlay(gameId);
        var record = Find(gameId);
        if (record is null || record.Streak == 0)
        {
            return;
        }

        record.Streak = 0;
        configuration.Save();
    }

    private void RecordDailyPlay(string gameId)
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
