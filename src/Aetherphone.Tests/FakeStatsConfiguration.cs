using Aetherphone.Core.Games;

namespace Aetherphone.Tests;

internal sealed class FakeStatsConfiguration : IGameStatsConfiguration
{
    public List<GameStatRecord> GameStats { get; } = new();
    public List<GameModeChoice> GameModeChoices { get; } = new();
    public List<GameLevelProgress> GameLevelProgress { get; } = new();
    public int DailyChallengeStreak { get; set; }
    public int DailyChallengeLastDay { get; set; }
    public ulong DailyChallengeHistory { get; set; }
    public int DailyChallengeBestStreak { get; set; }
    public string WordRunBank { get; set; } = string.Empty;
    public bool TetrisModern { get; set; }
    public int Saves { get; private set; }

    public void Save()
    {
        Saves++;
    }
}
