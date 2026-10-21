using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Game;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace Aetherphone.Core.Inventory;

internal static unsafe class EquippedOutfit
{
    private const int SoulCrystalSlot = 13;

    public static PrismGearDto[] Read()
    {
        var manager = GameMemory.Attached ? InventoryManager.Instance() : null;
        var container = manager is null ? null : manager->GetInventoryContainer(InventoryType.EquippedItems);
        if (container is null || !container->IsLoaded)
        {
            return Array.Empty<PrismGearDto>();
        }

        var pieces = new List<PrismGearDto>(SoulCrystalSlot);
        var size = Math.Min((int)container->Size, SoulCrystalSlot);
        for (var slot = 0; slot < size; slot++)
        {
            var item = container->GetInventorySlot(slot);
            if (item is null || item->ItemId == 0)
            {
                continue;
            }

            var look = item->GlamourId != 0 ? item->GlamourId : item->ItemId;
            pieces.Add(new PrismGearDto(slot, look, item->GetStain(0), item->GetStain(1)));
        }

        return pieces.ToArray();
    }
}
