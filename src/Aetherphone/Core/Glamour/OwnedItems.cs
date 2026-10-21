using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Aetherphone.Core.Game;
using CabinetSheet = Lumina.Excel.Sheets.Cabinet;

namespace Aetherphone.Core.Glamour;

internal static unsafe class OwnedItems
{
    private const uint HighQualityOffset = 1_000_000;

    private static Dictionary<uint, uint>? cabinetRows;

    public static bool StorageLoaded
    {
        get
        {
            if (!GameMemory.Attached)
            {
                return false;
            }

            var mirage = MirageManager.Instance();
            var state = UIState.Instance();
            return mirage is not null && mirage->PrismBoxLoaded && state is not null && state->Cabinet.IsCabinetLoaded();
        }
    }

    public static bool Owns(uint itemId)
    {
        if (itemId == 0 || !GameMemory.Attached)
        {
            return false;
        }

        var manager = InventoryManager.Instance();
        if (manager is not null &&
            (manager->GetInventoryItemCount(itemId) > 0 || manager->GetInventoryItemCount(itemId, true) > 0))
        {
            return true;
        }

        return InDresser(itemId) || InArmoire(itemId);
    }

    private static bool InDresser(uint itemId)
    {
        var mirage = MirageManager.Instance();
        if (mirage is null)
        {
            return false;
        }

        var stored = mirage->PrismBoxItemIds;
        for (var index = 0; index < stored.Length; index++)
        {
            if (stored[index] % HighQualityOffset == itemId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool InArmoire(uint itemId)
    {
        var state = UIState.Instance();
        if (state is null || !state->Cabinet.IsCabinetLoaded())
        {
            return false;
        }

        return CabinetRows().TryGetValue(itemId, out var row) && state->Cabinet.IsItemInCabinet(row);
    }

    private static Dictionary<uint, uint> CabinetRows()
    {
        if (cabinetRows is not null)
        {
            return cabinetRows;
        }

        var rows = new Dictionary<uint, uint>();
        foreach (var cabinet in Plugin.DataManager.GetExcelSheet<CabinetSheet>())
        {
            var item = cabinet.Item.RowId;
            if (item != 0)
            {
                rows[item] = cabinet.RowId;
            }
        }

        cabinetRows = rows;
        return rows;
    }
}
