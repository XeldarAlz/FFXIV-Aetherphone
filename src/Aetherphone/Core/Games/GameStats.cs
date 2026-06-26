namespace Aetherphone.Core.Games;

internal readonly struct GameStats
{
    public readonly int BestScore;

    public readonly int BestTimeSeconds;

    public readonly int Streak;

    public GameStats(int bestScore, int bestTimeSeconds, int streak)
    {
        BestScore = bestScore;
        BestTimeSeconds = bestTimeSeconds;
        Streak = streak;
    }

    public bool HasScore => BestScore > 0;

    public bool HasTime => BestTimeSeconds > 0;
}
