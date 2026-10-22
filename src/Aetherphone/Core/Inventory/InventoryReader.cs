using Aetherphone.Core.Game;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace Aetherphone.Core.Inventory;

internal sealed class InventoryReadBuffer
{
    public readonly List<InventoryStack> Stacks = new();
    public int Capacity;
    public ushort LoadedPages;

    public void Clear()
    {
        Stacks.Clear();
        Capacity = 0;
        LoadedPages = 0;
    }
}

internal sealed class InventoryLocalRead
{
    public readonly InventoryReadBuffer Bags = new();
    public readonly InventoryReadBuffer Armoury = new();
    public readonly InventoryReadBuffer Crystals = new();
    public readonly InventoryReadBuffer Saddlebag = new();
    public readonly InventoryReadBuffer Equipped = new();
    public long Gil;

    public void Clear()
    {
        Bags.Clear();
        Armoury.Clear();
        Crystals.Clear();
        Saddlebag.Clear();
        Equipped.Clear();
        Gil = 0;
    }
}

internal static unsafe class InventoryReader
{
    private const int NoCrystalPage = -1;

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

    private static readonly InventoryType[] CrystalTypes = { InventoryType.Crystals };

    private static readonly InventoryType[] EquippedTypes = { InventoryType.EquippedItems };

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

    public static ulong ReadLocalContentId()
    {
        if (!GameMemory.Attached)
        {
            return GameMemory.Simulated?.ContentId ?? 0;
        }

        var playerState = PlayerState.Instance();
        if (playerState is null)
        {
            return 0;
        }

        return playerState->ContentId;
    }

    public static bool ReadLocal(InventoryLocalRead into)
    {
        into.Clear();
        if (!GameMemory.Attached)
        {
            return GameMemory.Simulated is { } simulated && ReadSimulatedLocal(simulated, into);
        }

        var manager = InventoryManager.Instance();
        if (manager is null)
        {
            return false;
        }

        ReadInto(manager, BagTypes, NoCrystalPage, into.Bags);
        ReadInto(manager, ArmouryTypes, NoCrystalPage, into.Armoury);
        ReadInto(manager, CrystalTypes, NoCrystalPage, into.Crystals);
        ReadInto(manager, SaddlebagTypes, NoCrystalPage, into.Saddlebag);
        ReadInto(manager, EquippedTypes, NoCrystalPage, into.Equipped);
        into.Gil = manager->GetGil();
        return into.Bags.LoadedPages != 0;
    }

    public static bool ReadActiveRetainer(InventoryReadBuffer into, out ulong retainerId, out string retainerName)
    {
        into.Clear();
        retainerId = 0;
        retainerName = string.Empty;
        if (!GameMemory.Attached)
        {
            return GameMemory.Simulated is { } simulated &&
                   ReadSimulatedRetainer(simulated, into, out retainerId, out retainerName);
        }

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

        ReadInto(manager, RetainerBagTypes, InventoryPages.RetainerCrystalPage, into);
        if (into.LoadedPages == 0)
        {
            return false;
        }

        retainerId = active->RetainerId;
        retainerName = active->NameString;
        return true;
    }

    public static bool ReadRetainerRoster(List<RetainerSummary> into)
    {
        into.Clear();
        if (!GameMemory.Attached)
        {
            return GameMemory.Simulated is { } simulated && ReadSimulatedRoster(simulated, into);
        }

        var manager = RetainerManager.Instance();
        if (manager is null)
        {
            return false;
        }

        var count = manager->GetRetainerCount();
        for (var index = 0u; index < count; index++)
        {
            var retainer = manager->GetRetainerBySortedIndex(index);
            if (retainer is null || retainer->RetainerId == 0)
            {
                continue;
            }

            into.Add(new RetainerSummary(retainer->RetainerId, retainer->NameString, retainer->Gil,
                retainer->ItemCount, retainer->MarketItemCount));
        }

        return into.Count > 0;
    }

    public static bool ReadFreeCompany(InventoryReadBuffer into, out ulong freeCompanyId, out string freeCompanyName)
    {
        into.Clear();
        freeCompanyId = 0;
        freeCompanyName = string.Empty;
        if (!GameMemory.Attached)
        {
            return GameMemory.Simulated is { } simulated &&
                   ReadSimulatedFreeCompany(simulated, into, out freeCompanyId, out freeCompanyName);
        }

        var manager = InventoryManager.Instance();
        if (manager is null)
        {
            return false;
        }

        var infoProxy = InfoProxyFreeCompany.Instance();
        if (infoProxy is null || infoProxy->Id == 0)
        {
            return false;
        }

        ReadInto(manager, FreeCompanyBagTypes, InventoryPages.FreeCompanyCrystalPage, into);
        if (into.LoadedPages == 0 || into.Stacks.Count == 0)
        {
            into.Clear();
            return false;
        }

        freeCompanyId = infoProxy->Id;
        freeCompanyName = infoProxy->NameString;
        return true;
    }

    private static void ReadInto(InventoryManager* manager, InventoryType[] types, int crystalPage,
        InventoryReadBuffer into)
    {
        for (var page = 0; page < types.Length; page++)
        {
            var container = manager->GetInventoryContainer(types[page]);
            if (container is null || !container->IsLoaded)
            {
                continue;
            }

            var size = container->Size;
            AddPage(into, page, size, crystalPage);
            for (var slot = 0; slot < size; slot++)
            {
                var item = container->GetInventorySlot(slot);
                if (item is null || item->ItemId == 0 || item->Quantity <= 0)
                {
                    continue;
                }

                var highQuality = (item->Flags & InventoryItem.ItemFlags.HighQuality) != 0;
                into.Stacks.Add(new InventoryStack(item->ItemId, item->Quantity, highQuality, slot, page));
            }
        }
    }

    private static void AddPage(InventoryReadBuffer into, int page, int size, int crystalPage)
    {
        into.LoadedPages |= (ushort)(1 << page);
        if (page != crystalPage)
        {
            into.Capacity += size;
        }
    }

    private static bool ReadSimulatedLocal(SimulatedGame simulated, InventoryLocalRead into)
    {
        ReadInto(simulated.Containers, BagTypes, NoCrystalPage, into.Bags);
        ReadInto(simulated.Containers, ArmouryTypes, NoCrystalPage, into.Armoury);
        ReadInto(simulated.Containers, CrystalTypes, NoCrystalPage, into.Crystals);
        ReadInto(simulated.Containers, SaddlebagTypes, NoCrystalPage, into.Saddlebag);
        ReadInto(simulated.Containers, EquippedTypes, NoCrystalPage, into.Equipped);
        into.Gil = simulated.Gil;
        return into.Bags.LoadedPages != 0;
    }

    private static bool ReadSimulatedRetainer(SimulatedGame simulated, InventoryReadBuffer into, out ulong retainerId,
        out string retainerName)
    {
        retainerId = 0;
        retainerName = string.Empty;
        if (simulated.Retainer(simulated.ActiveRetainer) is not { } retainer)
        {
            return false;
        }

        ReadInto(retainer.Containers, RetainerBagTypes, InventoryPages.RetainerCrystalPage, into);
        if (into.LoadedPages == 0)
        {
            return false;
        }

        retainerId = retainer.RetainerId;
        retainerName = retainer.Name;
        return true;
    }

    private static bool ReadSimulatedRoster(SimulatedGame simulated, List<RetainerSummary> into)
    {
        for (var index = 0; index < simulated.Retainers.Length; index++)
        {
            var retainer = simulated.Retainers[index];
            into.Add(new RetainerSummary(retainer.RetainerId, retainer.Name, retainer.Gil,
                CountStacks(retainer.Containers), retainer.MarketItemCount));
        }

        return into.Count > 0;
    }

    private static bool ReadSimulatedFreeCompany(SimulatedGame simulated, InventoryReadBuffer into,
        out ulong freeCompanyId, out string freeCompanyName)
    {
        freeCompanyId = 0;
        freeCompanyName = string.Empty;
        if (simulated.FreeCompany is not { } freeCompany)
        {
            return false;
        }

        ReadInto(freeCompany.Containers, FreeCompanyBagTypes, InventoryPages.FreeCompanyCrystalPage, into);
        if (into.LoadedPages == 0 || into.Stacks.Count == 0)
        {
            into.Clear();
            return false;
        }

        freeCompanyId = freeCompany.Id;
        freeCompanyName = freeCompany.Name;
        return true;
    }

    private static void ReadInto(SimulatedContainer[] containers, InventoryType[] types, int crystalPage,
        InventoryReadBuffer into)
    {
        for (var page = 0; page < types.Length; page++)
        {
            if (SimulatedGame.Find(containers, types[page]) is not { } container)
            {
                continue;
            }

            AddPage(into, page, container.Size, crystalPage);
            for (var index = 0; index < container.Stacks.Length; index++)
            {
                var stack = container.Stacks[index];
                into.Stacks.Add(new InventoryStack(stack.ItemId, stack.Quantity, stack.HighQuality, stack.Slot, page));
            }
        }
    }

    private static int CountStacks(SimulatedContainer[] containers)
    {
        var count = 0;
        for (var index = 0; index < containers.Length; index++)
        {
            if (containers[index].Type != InventoryType.RetainerCrystals)
            {
                count += containers[index].Stacks.Length;
            }
        }

        return count;
    }
}
