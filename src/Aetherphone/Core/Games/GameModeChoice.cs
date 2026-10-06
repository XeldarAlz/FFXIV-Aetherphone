namespace Aetherphone.Core.Games;

[Serializable]
internal sealed class GameModeChoice
{
    public string GameId { get; set; } = string.Empty;
    public int Mode { get; set; }
}
