namespace Aetherphone.Core.Games;

[Serializable]
internal sealed class GameLevelProgress
{
    public string GameId { get; set; } = string.Empty;
    public string Stars { get; set; } = string.Empty;
}
