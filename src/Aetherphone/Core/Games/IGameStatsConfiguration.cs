namespace Aetherphone.Core.Games;

internal interface IGameStatsConfiguration
{
    List<GameStatRecord> GameStats { get; }
    List<GameModeChoice> GameModeChoices { get; }
    List<GameLevelProgress> GameLevelProgress { get; }
    int DailyChallengeStreak { get; set; }
    int DailyChallengeLastDay { get; set; }
    string WordRunBank { get; set; }
    bool TetrisModern { get; set; }
    void Save();
}
