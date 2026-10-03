using Aetherphone.Core.Inventory;
using Aetherphone.Core.Localization;

namespace Aetherphone.Apps.Inventory;

internal sealed class InventoryText
{
    private const string Separator = " · ";
    private const int BreadcrumbSources = 2;
    private static readonly LocString[] ArmourySlots =
    {
        L.Inventory.SlotMainHand, L.Inventory.SlotOffHand, L.Inventory.SlotHead, L.Inventory.SlotBody,
        L.Inventory.SlotHands, L.Inventory.SlotWaist, L.Inventory.SlotLegs, L.Inventory.SlotFeet,
        L.Inventory.SlotEars, L.Inventory.SlotNeck, L.Inventory.SlotWrists, L.Inventory.SlotRings,
        L.Inventory.SlotSoulCrystal,
    };

    public string[] SourceTitles { get; private set; } = Array.Empty<string>();
    public string[] SourceStatus { get; private set; } = Array.Empty<string>();
    public string[] SourceUsage { get; private set; } = Array.Empty<string>();
    public string[] SourceSlots { get; private set; } = Array.Empty<string>();
    public string[] ItemQuantity { get; private set; } = Array.Empty<string>();
    public string[] ItemBreadcrumb { get; private set; } = Array.Empty<string>();
    public string[] ItemPlaces { get; private set; } = Array.Empty<string>();
    public string[] PlacementDetail { get; private set; } = Array.Empty<string>();
    public string[] PlacementPage { get; private set; } = Array.Empty<string>();
    public string[] PlacementQuantity { get; private set; } = Array.Empty<string>();
    public string[] TidyDetail { get; private set; } = Array.Empty<string>();
    public string GilEyebrow { get; private set; } = string.Empty;
    public string GilTotal { get; private set; } = string.Empty;
    public string GilOnYou { get; private set; } = string.Empty;
    public string GilBreakdown { get; private set; } = string.Empty;
    public string TidySummary { get; private set; } = string.Empty;
    public string RetainersUpdated { get; private set; } = string.Empty;
    public string ValueTotal { get; private set; } = string.Empty;
    public string ValueCoverage { get; private set; } = string.Empty;
    public string[] RankedValue { get; private set; } = Array.Empty<string>();
    public string[] RankedDetail { get; private set; } = Array.Empty<string>();
    public string[] RetainerGil { get; private set; } = Array.Empty<string>();
    public string LanguageCode { get; private set; } = string.Empty;
    public DateTime BuiltUtc { get; private set; }
    public int Version { get; private set; }

    public void Build(InventoryCatalog catalog)
    {
        LanguageCode = Loc.Current.Code;
        BuiltUtc = DateTime.UtcNow;
        BuildSources(catalog);
        BuildItems(catalog);
        BuildPlacements(catalog);
        BuildTidy(catalog);
        BuildGil(catalog);
        Version++;
    }

    public void BuildValue(InventoryCatalog catalog, InventoryValuation valuation, string scopeName, bool pricing)
    {
        ValueTotal = valuation.PricedCount > 0
            ? NumberText.Group(valuation.Total)
            : pricing ? Loc.T(L.Inventory.Pricing) : Loc.T(L.Inventory.NoPrices);
        ValueCoverage = scopeName.Length > 0 && valuation.MarketableCount > 0
            ? Loc.T(L.Inventory.ValueCoverage, valuation.PricedCount, valuation.MarketableCount, scopeName)
            : string.Empty;
        var ranked = valuation.Ranked;
        var values = new string[ranked.Length];
        var details = new string[ranked.Length];
        for (var position = 0; position < ranked.Length; position++)
        {
            var itemIndex = ranked[position];
            values[position] = NumberText.Group(valuation.Value(itemIndex));
            details[position] = string.Concat(Quantity(catalog.Items[itemIndex].Quantity), Separator,
                Loc.T(L.Inventory.MarketEach, NumberText.Group(valuation.UnitPrice(itemIndex))));
        }

        RankedValue = values;
        RankedDetail = details;
        Version++;
    }

    public static string Quantity(long quantity) => string.Concat("×", NumberText.Group(quantity));

    public static string SourceTitle(InventorySourceEntry source) =>
        source.Kind switch
        {
            InventorySourceKind.Inventory => Loc.T(L.Inventory.SourceBags),
            InventorySourceKind.Armoury => Loc.T(L.Inventory.SourceArmoury),
            InventorySourceKind.Crystals => Loc.T(L.Inventory.SourceCrystals),
            InventorySourceKind.Saddlebag => Loc.T(L.Inventory.SourceSaddlebag),
            InventorySourceKind.Equipped => Loc.T(L.Inventory.SourceEquipped),
            InventorySourceKind.Retainer => source.OwnerName.Length > 0
                ? source.OwnerName
                : Loc.T(L.Inventory.SourceRetainer),
            _ => source.OwnerName.Length > 0 ? source.OwnerName : Loc.T(L.Inventory.SourceFreeCompany),
        };

    public static string PageLabel(InventorySourceKind kind, int page) =>
        kind switch
        {
            InventorySourceKind.Inventory => Loc.T(L.Inventory.PageBag, page + 1),
            InventorySourceKind.Armoury => page >= 0 && page < ArmourySlots.Length
                ? Loc.T(ArmourySlots[page])
                : string.Empty,
            InventorySourceKind.Saddlebag => page >= InventoryPages.SaddlebagPremiumFirstPage
                ? Loc.T(L.Inventory.PagePremium, page - InventoryPages.SaddlebagPremiumFirstPage + 1)
                : Loc.T(L.Inventory.Page, page + 1),
            InventorySourceKind.Retainer or InventorySourceKind.FreeCompany => InventoryPages.IsCrystalPage(kind, page)
                ? Loc.T(L.Inventory.SourceCrystals)
                : Loc.T(L.Inventory.Page, page + 1),
            _ => string.Empty,
        };

    private void BuildSources(InventoryCatalog catalog)
    {
        var sources = catalog.Sources;
        var titles = new string[sources.Count];
        var status = new string[sources.Count];
        var usage = new string[sources.Count];
        var slots = new string[sources.Count];
        for (var index = 0; index < sources.Count; index++)
        {
            var source = sources[index];
            titles[index] = SourceTitle(source);
            status[index] = Status(source);
            if (source.HasMeter)
            {
                usage[index] = string.Concat(NumberText.Group(source.Used), " / ", NumberText.Group(source.Capacity));
                slots[index] = Loc.T(L.Inventory.SlotsUsed, NumberText.Group(source.Used),
                    NumberText.Group(source.Capacity));
            }
            else
            {
                var count = source.Kind == InventorySourceKind.Crystals ? source.Quantity : source.Placements.Length;
                usage[index] = NumberText.Group(count);
                slots[index] = Loc.Plural(L.Inventory.ItemCount, (int)Math.Min(int.MaxValue, count));
            }
        }

        SourceTitles = titles;
        SourceStatus = status;
        SourceUsage = usage;
        SourceSlots = slots;
    }

    private static string Status(InventorySourceEntry source)
    {
        var selling = source.MarketCount > 0 ? Loc.T(L.Inventory.Selling, source.MarketCount) : string.Empty;
        string head;
        if (source.Kind == InventorySourceKind.Retainer && !source.Browsable)
        {
            head = Loc.T(L.Inventory.NotOpened);
        }
        else if (source.IsCached)
        {
            head = Loc.T(L.Inventory.Updated, TimeText.Ago(source.CapturedUtc));
        }
        else
        {
            head = string.Empty;
        }

        if (selling.Length == 0)
        {
            return head;
        }

        return head.Length == 0 ? selling : string.Concat(head, Separator, selling);
    }

    private void BuildItems(InventoryCatalog catalog)
    {
        var items = catalog.Items;
        var quantity = new string[items.Count];
        var breadcrumb = new string[items.Count];
        var places = new string[items.Count];
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            quantity[index] = Quantity(item.Quantity);
            breadcrumb[index] = Breadcrumb(catalog, item.Placements);
            places[index] = Loc.Plural(L.Inventory.InPlaces, item.Placements.Length);
        }

        ItemQuantity = quantity;
        ItemBreadcrumb = breadcrumb;
        ItemPlaces = places;
    }

    private string Breadcrumb(InventoryCatalog catalog, int[] placementIndices)
    {
        var first = string.Empty;
        var second = string.Empty;
        var distinct = 0;
        var previous = -1;
        for (var index = 0; index < placementIndices.Length; index++)
        {
            var sourceIndex = catalog.Placements[placementIndices[index]].SourceIndex;
            if (sourceIndex == previous)
            {
                continue;
            }

            previous = sourceIndex;
            if (distinct == 0)
            {
                first = SourceTitles[sourceIndex];
            }
            else if (distinct == 1)
            {
                second = SourceTitles[sourceIndex];
            }

            distinct++;
        }

        if (distinct <= 1)
        {
            return first;
        }

        var joined = string.Concat(first, ", ", second);
        return distinct <= BreadcrumbSources
            ? joined
            : string.Concat(joined, " +", NumberText.Group(distinct - BreadcrumbSources));
    }

    private void BuildPlacements(InventoryCatalog catalog)
    {
        var placements = catalog.Placements;
        var detail = new string[placements.Length];
        var pages = new string[placements.Length];
        var quantity = new string[placements.Length];
        for (var index = 0; index < placements.Length; index++)
        {
            var placement = placements[index];
            var source = catalog.Sources[placement.SourceIndex];
            var page = PageLabel(source.Kind, placement.Page);
            pages[index] = page;
            quantity[index] = Quantity(placement.Quantity);
            var prefix = source.Kind switch
            {
                InventorySourceKind.Retainer => Loc.T(L.Inventory.SourceRetainer),
                InventorySourceKind.FreeCompany => source.OwnerName.Length > 0
                    ? Loc.T(L.Inventory.SourceFreeCompany)
                    : string.Empty,
                _ => string.Empty,
            };
            var stale = source.IsCached ? Loc.T(L.Inventory.Updated, TimeText.Ago(source.CapturedUtc)) : string.Empty;
            detail[index] = Join(Join(prefix, page), stale);
        }

        PlacementDetail = detail;
        PlacementPage = pages;
        PlacementQuantity = quantity;
    }

    private void BuildTidy(InventoryCatalog catalog)
    {
        var tidy = catalog.Tidy;
        var detail = new string[tidy.Count];
        for (var index = 0; index < tidy.Count; index++)
        {
            var entry = tidy[index];
            detail[index] = Join(Loc.T(L.Inventory.TidyStacks, entry.Stacks, entry.Minimum),
                ItemBreadcrumb[entry.ItemIndex]);
        }

        TidyDetail = detail;
        TidySummary = catalog.TidySlotsSaved > 0 ? Loc.Plural(L.Inventory.TidyFrees, catalog.TidySlotsSaved) : string.Empty;
    }

    private void BuildGil(InventoryCatalog catalog)
    {
        GilEyebrow = Loc.Upper(Loc.T(L.Inventory.Gil));
        GilTotal = NumberText.Group(catalog.Gil + catalog.RetainerGil);
        GilOnYou = NumberText.Group(catalog.Gil);
        GilBreakdown = catalog.RetainersWithGil > 0
            ? Join(Loc.T(L.Inventory.GilOnYou, GilOnYou), Loc.T(L.Inventory.GilRetainers,
                NumberText.Group(catalog.RetainerGil)))
            : Loc.T(L.Inventory.RetainerGilUnknown);
        RetainersUpdated = catalog.RetainersCapturedUtc != default
            ? Loc.T(L.Inventory.Updated, TimeText.Ago(catalog.RetainersCapturedUtc))
            : string.Empty;
        var sources = catalog.Sources;
        var gil = new string[sources.Count];
        for (var index = 0; index < sources.Count; index++)
        {
            gil[index] = sources[index].Gil >= 0 ? NumberText.Group(sources[index].Gil) : string.Empty;
        }

        RetainerGil = gil;
    }

    private static string Join(string left, string right)
    {
        if (left.Length == 0)
        {
            return right;
        }

        return right.Length == 0 ? left : string.Concat(left, Separator, right);
    }
}
