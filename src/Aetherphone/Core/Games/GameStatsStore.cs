namespace Aetherphone.Core.Games;

internal sealed class GameStatsStore
{
    private readonly Configuration configuration;

    public GameStatsStore(Configuration configuration)
    {
        this.configuration = configuration;
    }

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
        var record = GetOrCreate(gameId);
        record.Streak += 1;
        configuration.Save();
        return record.Streak;
    }

    public void ResetStreak(string gameId)
    {
        var record = Find(gameId);
        if (record is null || record.Streak == 0)
        {
            return;
        }

        record.Streak = 0;
        configuration.Save();
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

        var created = new GameStatRecord
        {
            GameId = gameId,
        };

        configuration.GameStats.Add(created);
        return created;
    }
}
