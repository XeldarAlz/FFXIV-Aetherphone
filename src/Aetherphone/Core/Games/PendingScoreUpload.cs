namespace Aetherphone.Core.Games;

[Serializable]
internal sealed class PendingScoreUpload
{
    public string StatId { get; set; } = string.Empty;
    public string GameId { get; set; } = string.Empty;
    public int Value { get; set; }
    public ScoreKind Kind { get; set; }
    public ulong Seed { get; set; }
    public bool Daily { get; set; }
    public long QueuedAtUnix { get; set; }
}
