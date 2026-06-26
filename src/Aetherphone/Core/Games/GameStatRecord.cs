namespace Aetherphone.Core.Games;

[Serializable]
internal sealed class GameStatRecord
{
    public string GameId { get; set; } = string.Empty;

    public int BestScore { get; set; }

    public int BestTimeSeconds { get; set; }

    public int Streak { get; set; }
}
