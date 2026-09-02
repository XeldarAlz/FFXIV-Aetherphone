using Aetherphone.Core.Game;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace Aetherphone.Core.Inventory;

internal static unsafe class InventoryGil
{
    public static long Read()
    {
        if (!GameMemory.Attached)
        {
            return 0;
        }

        var manager = InventoryManager.Instance();
        if (manager is null)
        {
            return 0;
        }

        return manager->GetGil();
    }
}
