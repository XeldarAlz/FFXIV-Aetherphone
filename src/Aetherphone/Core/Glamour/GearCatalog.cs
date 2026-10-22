using Aetherphone.Core.Game;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace Aetherphone.Core.Glamour;

internal readonly record struct GearPick(uint ItemId, string Name, uint IconId, byte Dyes);

internal readonly record struct DyePick(byte StainId, string Name, uint Color);

internal sealed class GearCatalog
{
    private const int Expected = 16384;
    private const long RetryAfterMilliseconds = 30_000;

    private readonly IDataManager data;
    private readonly object sync = new();
    private uint[] ids = Array.Empty<uint>();
    private string[] names = Array.Empty<string>();
    private string[] lowerNames = Array.Empty<string>();
    private uint[] icons = Array.Empty<uint>();
    private ushort[] slots = Array.Empty<ushort>();
    private byte[] dyeCounts = Array.Empty<byte>();
    private Dictionary<uint, int> indexById = new();
    private DyePick[] dyes = Array.Empty<DyePick>();
    private volatile bool building;
    private volatile bool ready;
    private long retryAt;

    public GearCatalog(IDataManager data)
    {
        this.data = data;
    }

    public bool Ready => ready;

    public DyePick[] Dyes => dyes;

    public void EnsureBuilt()
    {
        if (ready || building || !GameSheets.Available || Environment.TickCount64 < Interlocked.Read(ref retryAt))
        {
            return;
        }

        lock (sync)
        {
            if (ready || building)
            {
                return;
            }

            building = true;
        }

        _ = Task.Run(Build);
    }

    public int Search(string query, GearSlot slot, GearPick[] into)
    {
        if (!ready)
        {
            return 0;
        }

        var needle = query.Trim().ToLowerInvariant();
        var mask = (ushort)(1 << (int)slot);
        var count = 0;
        var localLower = lowerNames;
        var localSlots = slots;
        for (var index = 0; index < localLower.Length && count < into.Length; index++)
        {
            if ((localSlots[index] & mask) != 0 && localLower[index].StartsWith(needle, StringComparison.Ordinal))
            {
                into[count++] = At(index);
            }
        }

        if (needle.Length == 0)
        {
            return count;
        }

        for (var index = 0; index < localLower.Length && count < into.Length; index++)
        {
            if ((localSlots[index] & mask) != 0 && !localLower[index].StartsWith(needle, StringComparison.Ordinal) &&
                localLower[index].Contains(needle, StringComparison.Ordinal))
            {
                into[count++] = At(index);
            }
        }

        return count;
    }

    public bool TryGet(uint itemId, out GearPick pick)
    {
        pick = default;
        if (!ready || !indexById.TryGetValue(itemId, out var index))
        {
            return false;
        }

        pick = At(index);
        return true;
    }

    public string DyeName(byte stainId)
    {
        var local = dyes;
        for (var index = 0; index < local.Length; index++)
        {
            if (local[index].StainId == stainId)
            {
                return local[index].Name;
            }
        }

        return string.Empty;
    }

    private GearPick At(int index) => new(ids[index], names[index], icons[index], dyeCounts[index]);

    private void Build()
    {
        try
        {
            var rows = new List<(string Lower, uint Id, string Name, uint Icon, ushort Slots, byte Dyes)>(Expected);
            foreach (var item in data.GetExcelSheet<Item>())
            {
                if (!item.EquipSlotCategory.IsValid)
                {
                    continue;
                }

                var mask = SlotMask(item.EquipSlotCategory.Value);
                var name = item.Name.ExtractText();
                if (mask == 0 || name.Length == 0)
                {
                    continue;
                }

                rows.Add((name.ToLowerInvariant(), item.RowId, name, item.Icon, mask, item.DyeCount));
            }

            rows.Sort(static (left, right) => string.CompareOrdinal(left.Lower, right.Lower));
            var builtIds = new uint[rows.Count];
            var builtNames = new string[rows.Count];
            var builtLower = new string[rows.Count];
            var builtIcons = new uint[rows.Count];
            var builtSlots = new ushort[rows.Count];
            var builtDyes = new byte[rows.Count];
            var builtIndex = new Dictionary<uint, int>(rows.Count);
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                builtIds[index] = row.Id;
                builtNames[index] = row.Name;
                builtLower[index] = row.Lower;
                builtIcons[index] = row.Icon;
                builtSlots[index] = row.Slots;
                builtDyes[index] = row.Dyes;
                builtIndex[row.Id] = index;
            }

            var builtStains = BuildDyes();
            lock (sync)
            {
                ids = builtIds;
                names = builtNames;
                lowerNames = builtLower;
                icons = builtIcons;
                slots = builtSlots;
                dyeCounts = builtDyes;
                indexById = builtIndex;
                dyes = builtStains;
                ready = true;
            }
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Gear catalog build failed");
            Interlocked.Exchange(ref retryAt, Environment.TickCount64 + RetryAfterMilliseconds);
        }
        finally
        {
            building = false;
        }
    }

    private DyePick[] BuildDyes()
    {
        var rows = new List<(byte Shade, byte Order, DyePick Pick)>(160);
        foreach (var stain in data.GetExcelSheet<Stain>())
        {
            var name = stain.Name.ExtractText();
            if (stain.RowId == 0 || stain.RowId > byte.MaxValue || name.Length == 0)
            {
                continue;
            }

            rows.Add((stain.Shade, stain.SubOrder, new DyePick((byte)stain.RowId, name, stain.Color)));
        }

        rows.Sort(static (left, right) =>
            left.Shade != right.Shade ? left.Shade.CompareTo(right.Shade) : left.Order.CompareTo(right.Order));
        var built = new DyePick[rows.Count];
        for (var index = 0; index < rows.Count; index++)
        {
            built[index] = rows[index].Pick;
        }

        return built;
    }

    private static ushort SlotMask(EquipSlotCategory category)
    {
        var mask = 0;
        mask |= category.MainHand == 1 ? 1 << (int)GearSlot.MainHand : 0;
        mask |= category.OffHand == 1 ? 1 << (int)GearSlot.OffHand : 0;
        mask |= category.Head == 1 ? 1 << (int)GearSlot.Head : 0;
        mask |= category.Body == 1 ? 1 << (int)GearSlot.Body : 0;
        mask |= category.Gloves == 1 ? 1 << (int)GearSlot.Hands : 0;
        mask |= category.Legs == 1 ? 1 << (int)GearSlot.Legs : 0;
        mask |= category.Feet == 1 ? 1 << (int)GearSlot.Feet : 0;
        mask |= category.Ears == 1 ? 1 << (int)GearSlot.Ears : 0;
        mask |= category.Neck == 1 ? 1 << (int)GearSlot.Neck : 0;
        mask |= category.Wrists == 1 ? 1 << (int)GearSlot.Wrists : 0;
        mask |= category.FingerR == 1 || category.FingerL == 1
            ? (1 << (int)GearSlot.RingRight) | (1 << (int)GearSlot.RingLeft)
            : 0;
        return (ushort)mask;
    }
}
