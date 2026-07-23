using Aetherphone.Core.Game;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace Aetherphone.Core.Jobs;

internal static unsafe class GearsetActions
{
    public static bool Equip(int gearsetId)
    {
        var module = RaptureGearsetModule.Instance();
        if (module is null || !module->IsValidGearset(gearsetId))
        {
            return false;
        }

        return module->EquipGearset(gearsetId) == 0;
    }

    /// <summary>Hand/Land jobs have no gearset, so switching jobs means equipping that job's tool straight from the Armoury Chest.</summary>
    public static bool EquipTool(GameData gameData, uint classJobId)
    {
        var manager = InventoryManager.Instance();
        var armory = manager is null ? null : manager->GetInventoryContainer(InventoryType.ArmoryMainHand);
        if (armory is null || !armory->IsLoaded)
        {
            return false;
        }

        var agent = AgentInventoryContext.Instance();
        if (agent is null)
        {
            return false;
        }

        for (var index = 0; index < armory->Size; index++)
        {
            var item = armory->GetInventorySlot(index);
            if (item is null || item->ItemId == 0 ||
                !gameData.TryGetItemClassJobUse(item->ItemId, out var itemClassJobId, out _) ||
                itemClassJobId != classJobId)
            {
                continue;
            }

            agent->UseItem(item->ItemId, InventoryType.ArmoryMainHand, (uint)index);
            return true;
        }

        return false;
    }
}
