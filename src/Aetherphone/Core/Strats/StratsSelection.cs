namespace Aetherphone.Core.Strats;

internal static class StratsRoles
{
    public const int SlotCount = 8;
    private static readonly string[] Names = { "Tank", "Healer", "Melee", "Ranged" };
    private static readonly string[] EnglishLabels = { "MT", "OT", "H1", "H2", "M1", "M2", "R1", "R2" };
    private static readonly string[] JapaneseLabels = { "MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4" };

    public static string RoleName(int slot) => Names[Math.Clamp(slot, 0, SlotCount - 1) / 2];

    public static int Party(int slot) => Math.Clamp(slot, 0, SlotCount - 1) % 2 + 1;

    public static string Label(int slot, bool japanese) =>
        japanese ? JapaneseLabels[Math.Clamp(slot, 0, SlotCount - 1)] : EnglishLabels[Math.Clamp(slot, 0, SlotCount - 1)];
}

internal sealed class StratsSelection
{
    private const float ProgressStep = 0.01f;

    public string FightKey = string.Empty;
    public string StratId = string.Empty;
    public int Slot;
    public readonly Dictionary<string, string> Toggles = new(StringComparer.Ordinal);
    public string Alignment = string.Empty;
    public int Tab;
    public int Revision;
    public bool Fresh;
    public long OpenedUnix;
    public int ReadingEntry = -1;
    public string ReadingLabel = string.Empty;
    public float ReadingProgress;

    public void Touch() => Revision++;

    public void Load(string fightKey, StratsFightSelection? saved, int defaultSlot, long nowUnix)
    {
        FightKey = fightKey;
        Toggles.Clear();
        OpenedUnix = nowUnix;
        Fresh = saved is null;
        if (saved is null)
        {
            StratId = string.Empty;
            Slot = Math.Clamp(defaultSlot, 0, StratsRoles.SlotCount - 1);
            Alignment = string.Empty;
            Tab = 0;
            ClearReading();
            Touch();
            return;
        }

        StratId = saved.Strat;
        Slot = Math.Clamp(saved.Slot, 0, StratsRoles.SlotCount - 1);
        foreach (var pair in saved.Toggles)
        {
            Toggles[pair.Key] = pair.Value;
        }

        Alignment = saved.Alignment;
        Tab = Math.Max(0, saved.Tab);
        ReadingEntry = saved.ReadingEntry;
        ReadingLabel = saved.ReadingLabel;
        ReadingProgress = Math.Clamp(saved.ReadingProgress, 0f, 1f);
        Touch();
    }

    public void ClearReading()
    {
        ReadingEntry = -1;
        ReadingLabel = string.Empty;
        ReadingProgress = 0f;
    }

    public bool MarkReading(int entry, string label, float progress)
    {
        var clamped = Math.Clamp(progress, 0f, 1f);
        if (entry == ReadingEntry && MathF.Abs(clamped - ReadingProgress) < ProgressStep)
        {
            return false;
        }

        ReadingEntry = entry;
        ReadingLabel = label;
        ReadingProgress = clamped;
        return true;
    }

    public StratsFightSelection Capture()
    {
        var snapshot = new StratsFightSelection
        {
            Strat = StratId,
            Slot = Slot,
            Alignment = Alignment,
            Tab = Tab,
            OpenedUnix = OpenedUnix,
            ReadingEntry = ReadingEntry,
            ReadingLabel = ReadingLabel,
            ReadingProgress = ReadingProgress,
        };
        foreach (var pair in Toggles)
        {
            snapshot.Toggles[pair.Key] = pair.Value;
        }

        return snapshot;
    }
}
