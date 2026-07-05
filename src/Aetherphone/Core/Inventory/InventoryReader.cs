using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace Aetherphone.Core.Inventory;

internal static unsafe class InventoryReader
{
    public static ulong ReadLocalContentId()
    {
        var playerState = PlayerState.Instance();
        if (playerState is null)
        {
            return 0;
        }

        return playerState->ContentId;
    }

    private static readonly InventoryType[] BagTypes =
    {
        InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4,
    };

    private static readonly InventoryType[] ArmouryTypes =
    {
        InventoryType.ArmoryMainHand, InventoryType.ArmoryOffHand, InventoryType.ArmoryHead,
        InventoryType.ArmoryBody, InventoryType.ArmoryHands, InventoryType.ArmoryWaist, InventoryType.ArmoryLegs,
        InventoryType.ArmoryFeets, InventoryType.ArmoryEar, InventoryType.ArmoryNeck, InventoryType.ArmoryWrist,
        InventoryType.ArmoryRings, InventoryType.ArmorySoulCrystal,
    };

    private static readonly InventoryType[] SaddlebagTypes =
    {
        InventoryType.SaddleBag1, InventoryType.SaddleBag2, InventoryType.PremiumSaddleBag1,
        InventoryType.PremiumSaddleBag2,
    };

    private static readonly InventoryType[] RetainerBagTypes =
    {
        InventoryType.RetainerPage1, InventoryType.RetainerPage2, InventoryType.RetainerPage3,
        InventoryType.RetainerPage4, InventoryType.RetainerPage5, InventoryType.RetainerPage6,
        InventoryType.RetainerPage7, InventoryType.RetainerCrystals,
    };

    private static readonly InventoryType[] FreeCompanyBagTypes =
    {
        InventoryType.FreeCompanyPage1, InventoryType.FreeCompanyPage2, InventoryType.FreeCompanyPage3,
        InventoryType.FreeCompanyPage4, InventoryType.FreeCompanyPage5, InventoryType.FreeCompanyCrystals,
    };

    public static bool ReadLocal(List<InventoryStack> bags, List<InventoryStack> armoury, List<InventoryStack> crystals,
        List<InventoryStack> saddlebag, List<InventoryStack> equipped)
    {
        bags.Clear();
        armoury.Clear();
        crystals.Clear();
        saddlebag.Clear();
        equipped.Clear();
        var manager = InventoryManager.Instance();
        if (manager is null)
        {
            return false;
        }

        ReadInto(manager, BagTypes, bags);
        ReadInto(manager, ArmouryTypes, armoury);
        ReadOne(manager, InventoryType.Crystals, crystals);
        ReadInto(manager, SaddlebagTypes, saddlebag);
        ReadOne(manager, InventoryType.EquippedItems, equipped);
        return true;
    }

    public static bool ReadActiveRetainer(List<InventoryStack> into, out ulong retainerId, out string retainerName)
    {
        into.Clear();
        retainerId = 0;
        retainerName = string.Empty;
        var retainerManager = RetainerManager.Instance();
        if (retainerManager is null || !retainerManager->IsReady)
        {
            return false;
        }

        var active = retainerManager->GetActiveRetainer();
        if (active is null || active->RetainerId == 0)
        {
            return false;
        }

        var manager = InventoryManager.Instance();
        if (manager is null)
        {
            return false;
        }

        if (!AnyLoaded(manager, RetainerBagTypes))
        {
            return false;
        }

        retainerId = active->RetainerId;
        retainerName = active->NameString;
        ReadInto(manager, RetainerBagTypes, into);
        return true;
    }

    public static bool ReadFreeCompany(List<InventoryStack> into, out ulong freeCompanyId, out string freeCompanyName)
    {
        into.Clear();
        freeCompanyId = 0;
        freeCompanyName = string.Empty;
        var manager = InventoryManager.Instance();
        if (manager is null)
        {
            return false;
        }

        if (!AnyLoaded(manager, FreeCompanyBagTypes))
        {
            return false;
        }

        var infoProxy = InfoProxyFreeCompany.Instance();
        if (infoProxy is null || infoProxy->Id == 0)
        {
            return false;
        }

        freeCompanyId = infoProxy->Id;
        freeCompanyName = infoProxy->NameString;
        ReadInto(manager, FreeCompanyBagTypes, into);
        return into.Count > 0;
    }

    private static bool AnyLoaded(InventoryManager* manager, InventoryType[] types)
    {
        for (var typeIndex = 0; typeIndex < types.Length; typeIndex++)
        {
            var container = manager->GetInventoryContainer(types[typeIndex]);
            if (container is not null && container->IsLoaded)
            {
                return true;
            }
        }

        return false;
    }

    private static void ReadInto(InventoryManager* manager, InventoryType[] types, List<InventoryStack> into)
    {
        for (var typeIndex = 0; typeIndex < types.Length; typeIndex++)
        {
            ReadOne(manager, types[typeIndex], into);
        }
    }

    private static void ReadOne(InventoryManager* manager, InventoryType type, List<InventoryStack> into)
    {
        var container = manager->GetInventoryContainer(type);
        if (container is null || !container->IsLoaded)
        {
            return;
        }

        var size = container->Size;
        for (var slot = 0; slot < size; slot++)
        {
            var item = container->GetInventorySlot(slot);
            if (item is null || item->ItemId == 0 || item->Quantity <= 0)
            {
                continue;
            }

            var highQuality = (item->Flags & InventoryItem.ItemFlags.HighQuality) != 0;
            into.Add(new InventoryStack(item->ItemId, item->Quantity, highQuality, slot));
        }
    }
}
