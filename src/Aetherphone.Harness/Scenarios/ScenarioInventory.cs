using Aetherphone.Core.Game;
using FFXIVClientStructs.FFXIV.Client.Game;
using Newtonsoft.Json.Linq;

namespace Aetherphone.Harness.Scenarios;

internal sealed class ScenarioInventory
{
    private const int BagSlots = 35;
    private const int ArmourySlots = 35;
    private const int SaddlebagSlots = 35;
    private const int CrystalSlots = 18;
    private const int EquippedSlots = 14;
    private const int RetainerPageSlots = 25;
    private const int FreeCompanyPageSlots = 50;
    private const int SoulCrystalEquippedSlot = 13;
    private const uint FirstCrystalItemId = 2;

    private static readonly InventoryType[] BagPages =
    {
        InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4,
    };

    private static readonly InventoryType[] SaddlebagPages = { InventoryType.SaddleBag1, InventoryType.SaddleBag2 };

    private static readonly InventoryType[] ArmouryPages =
    {
        InventoryType.ArmoryMainHand, InventoryType.ArmoryOffHand, InventoryType.ArmoryHead,
        InventoryType.ArmoryBody, InventoryType.ArmoryHands, InventoryType.ArmoryWaist, InventoryType.ArmoryLegs,
        InventoryType.ArmoryFeets, InventoryType.ArmoryEar, InventoryType.ArmoryNeck, InventoryType.ArmoryWrist,
        InventoryType.ArmoryRings, InventoryType.ArmorySoulCrystal,
    };

    private static readonly InventoryType[] RetainerPages =
    {
        InventoryType.RetainerPage1, InventoryType.RetainerPage2, InventoryType.RetainerPage3,
        InventoryType.RetainerPage4, InventoryType.RetainerPage5, InventoryType.RetainerPage6,
        InventoryType.RetainerPage7,
    };

    private static readonly InventoryType[] FreeCompanyPages =
    {
        InventoryType.FreeCompanyPage1, InventoryType.FreeCompanyPage2, InventoryType.FreeCompanyPage3,
        InventoryType.FreeCompanyPage4, InventoryType.FreeCompanyPage5,
    };

    private readonly SheetLookup sheets;
    private readonly List<string> warnings;

    public ScenarioInventory(SheetLookup sheets, List<string> warnings)
    {
        this.sheets = sheets;
        this.warnings = warnings;
    }

    public SimulatedContainer[] Local(JObject? inventory)
    {
        var containers = new List<SimulatedContainer>(24);
        containers.AddRange(Paged(inventory?["bags"], BagPages, BagSlots, "bags"));
        containers.AddRange(Armoury(inventory?["armoury"]));
        containers.Add(Crystals(inventory?["crystals"], InventoryType.Crystals));
        containers.AddRange(Paged(inventory?["saddlebag"], SaddlebagPages, SaddlebagSlots, "saddlebag"));
        containers.Add(Equipped(inventory?["equipped"]));
        return containers.ToArray();
    }

    public SimulatedContainer[] Retainer(JObject retainer)
    {
        var containers = new List<SimulatedContainer>(RetainerPages.Length + 1);
        containers.AddRange(Paged(retainer["items"], RetainerPages, RetainerPageSlots, "retainer items"));
        containers.Add(Crystals(retainer["crystals"], InventoryType.RetainerCrystals));
        return containers.ToArray();
    }

    public SimulatedContainer[] FreeCompany(JObject freeCompany)
    {
        var containers = new List<SimulatedContainer>(FreeCompanyPages.Length + 1);
        containers.AddRange(Paged(freeCompany["items"], FreeCompanyPages, FreeCompanyPageSlots, "free company items"));
        containers.Add(Crystals(freeCompany["crystals"], InventoryType.FreeCompanyCrystals));
        return containers.ToArray();
    }

    private SimulatedContainer[] Paged(JToken? items, InventoryType[] pages, int pageSize, string label)
    {
        var buckets = NewBuckets(pages.Length);
        var stacks = Stacks(items);
        for (var index = 0; index < stacks.Count; index++)
        {
            var page = index / pageSize;
            if (page >= pages.Length)
            {
                warnings.Add($"{label}: {stacks.Count - index} stacks do not fit and were dropped");
                break;
            }

            var stack = stacks[index];
            buckets[page].Add(stack with { Slot = index % pageSize });
        }

        var containers = new SimulatedContainer[pages.Length];
        for (var page = 0; page < pages.Length; page++)
        {
            containers[page] = new SimulatedContainer(pages[page], pageSize, buckets[page].ToArray());
        }

        return containers;
    }

    private SimulatedContainer[] Armoury(JToken? items)
    {
        var buckets = NewBuckets(ArmouryPages.Length);
        var stacks = Stacks(items);
        for (var index = 0; index < stacks.Count; index++)
        {
            var stack = stacks[index];
            var category = sheets.EquipCategory(stack.ItemId);
            if (category == SheetLookup.NoEquipCategory)
            {
                warnings.Add($"armoury: item {stack.ItemId} is not equipment");
                continue;
            }

            var bucket = buckets[category];
            if (bucket.Count >= ArmourySlots)
            {
                warnings.Add($"armoury: page {ArmouryPages[category]} is full");
                continue;
            }

            bucket.Add(stack with { Slot = bucket.Count });
        }

        var containers = new SimulatedContainer[ArmouryPages.Length];
        for (var page = 0; page < ArmouryPages.Length; page++)
        {
            containers[page] = new SimulatedContainer(ArmouryPages[page], ArmourySlots, buckets[page].ToArray());
        }

        return containers;
    }

    private SimulatedContainer Equipped(JToken? items)
    {
        var stacks = Stacks(items);
        var placed = new List<SimulatedStack>(EquippedSlots);
        var ringsPlaced = 0;
        for (var index = 0; index < stacks.Count; index++)
        {
            var stack = stacks[index];
            var category = sheets.EquipCategory(stack.ItemId);
            if (category == SheetLookup.NoEquipCategory)
            {
                warnings.Add($"equipped: item {stack.ItemId} is not equipment");
                continue;
            }

            var slot = category switch
            {
                SheetLookup.RingCategory => SheetLookup.RingCategory + ringsPlaced++,
                SheetLookup.EquipCategoryCount - 1 => SoulCrystalEquippedSlot,
                _ => category,
            };
            if (slot >= EquippedSlots || Occupied(placed, slot))
            {
                warnings.Add($"equipped: slot {slot} is already taken by another item");
                continue;
            }

            placed.Add(stack with { Slot = slot, Quantity = 1 });
        }

        return new SimulatedContainer(InventoryType.EquippedItems, EquippedSlots, placed.ToArray());
    }

    private SimulatedContainer Crystals(JToken? items, InventoryType type)
    {
        var stacks = Stacks(items);
        var placed = new List<SimulatedStack>(CrystalSlots);
        for (var index = 0; index < stacks.Count; index++)
        {
            var stack = stacks[index];
            var slot = (int)(stack.ItemId - FirstCrystalItemId);
            if (stack.ItemId < FirstCrystalItemId || slot >= CrystalSlots)
            {
                warnings.Add($"crystals: item {stack.ItemId} is not a shard, crystal, or cluster");
                continue;
            }

            placed.Add(stack with { Slot = slot });
        }

        return new SimulatedContainer(type, CrystalSlots, placed.ToArray());
    }

    private List<SimulatedStack> Stacks(JToken? items)
    {
        var stacks = new List<SimulatedStack>();
        if (items is not JArray array)
        {
            return stacks;
        }

        for (var index = 0; index < array.Count; index++)
        {
            var entry = array[index];
            var itemId = entry is JObject detail ? sheets.Item(detail["item"]) : sheets.Item(entry);
            if (itemId == 0)
            {
                continue;
            }

            var quantity = entry is JObject withQuantity ? withQuantity.Value<int?>("quantity") ?? 1 : 1;
            var highQuality = entry is JObject withQuality && (withQuality.Value<bool?>("hq") ?? false);
            stacks.Add(new SimulatedStack(0, itemId, Math.Max(1, quantity), highQuality));
        }

        return stacks;
    }

    private static bool Occupied(List<SimulatedStack> stacks, int slot)
    {
        for (var index = 0; index < stacks.Count; index++)
        {
            if (stacks[index].Slot == slot)
            {
                return true;
            }
        }

        return false;
    }

    private static List<SimulatedStack>[] NewBuckets(int count)
    {
        var buckets = new List<SimulatedStack>[count];
        for (var index = 0; index < count; index++)
        {
            buckets[index] = new List<SimulatedStack>();
        }

        return buckets;
    }
}
