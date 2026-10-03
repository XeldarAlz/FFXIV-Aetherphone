namespace Aetherphone.Core.Strats;

internal sealed class StratsFightSelection
{
    public string Strat { get; set; } = string.Empty;
    public int Slot { get; set; }
    public Dictionary<string, string> Toggles { get; set; } = new(StringComparer.Ordinal);
    public string Alignment { get; set; } = string.Empty;
    public int Tab { get; set; }
    public long OpenedUnix { get; set; }
    public int ReadingEntry { get; set; } = -1;
    public string ReadingLabel { get; set; } = string.Empty;
    public float ReadingProgress { get; set; }
}

internal sealed class StratsSnapshot
{
    public Dictionary<string, StratsFightSelection> Fights { get; set; } = new(StringComparer.Ordinal);
    public int DefaultSlot { get; set; }
}
