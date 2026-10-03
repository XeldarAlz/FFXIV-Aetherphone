using Aetherphone.Core.Game;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Aetherphone.Core.Wallet;

internal static unsafe class WalletReader
{
    private const long ScripCap = 4000;
    private const long SkybuildersCap = 10000;
    private const long HuntCap = 4000;
    private const long TomestoneCap = 2000;
    private const long PvpCap = 20000;
    private const long BicolorCap = 1500;
    private const long VentureCap = 65000;
    private const long GoldSaucerCap = 9_999_999;
    private const uint GilItemId = 1;
    private const uint StormSealItemId = 20;
    private const uint SerpentSealItemId = 21;
    private const uint FlameSealItemId = 22;
    private const int GroupCount = 6;

    private readonly struct Definition
    {
        public readonly uint ItemId;
        public readonly long Cap;
        public readonly WalletGroup Group;

        public Definition(uint itemId, long cap, WalletGroup group)
        {
            ItemId = itemId;
            Cap = cap;
            Group = group;
        }
    }

    private static readonly Definition[] Definitions =
    {
        new(33913, ScripCap, WalletGroup.Scrips),
        new(33914, ScripCap, WalletGroup.Scrips),
        new(41784, ScripCap, WalletGroup.Scrips),
        new(41785, ScripCap, WalletGroup.Scrips),
        new(28063, SkybuildersCap, WalletGroup.Scrips),
        new(27, HuntCap, WalletGroup.Hunt),
        new(10307, HuntCap, WalletGroup.Hunt),
        new(26533, HuntCap, WalletGroup.Hunt),
        new(25, PvpCap, WalletGroup.Pvp),
        new(36656, PvpCap, WalletGroup.Pvp),
        new(29, GoldSaucerCap, WalletGroup.Other),
        new(21072, VentureCap, WalletGroup.Other),
        new(26807, BicolorCap, WalletGroup.Other),
    };

    public static WalletEntry BuildGil(GameData gameData)
    {
        ResolveItem(gameData, GilItemId, out var iconId, out var name);
        if (name.Length == 0)
        {
            name = "Gil";
        }

        return new WalletEntry(GilItemId, iconId, name, 0, CurrencyKind.Gil, WalletGroup.Other);
    }

    public static long CurrentGil()
    {
        var manager = InventoryManager.Instance();
        return manager is null ? 0 : manager->GetGil();
    }

    public static WalletSection[] BuildSections(GameData gameData, uint sealItemId)
    {
        var buckets = new List<WalletEntry>[GroupCount];
        for (var index = 0; index < GroupCount; index++)
        {
            buckets[index] = new List<WalletEntry>(5);
        }

        AddTomestones(buckets[(int)WalletGroup.Tomestones], gameData);
        if (sealItemId != 0)
        {
            AddEntry(buckets[(int)WalletGroup.GrandCompany], gameData, sealItemId, 0, CurrencyKind.GrandCompanySeal,
                WalletGroup.GrandCompany);
        }

        for (var index = 0; index < Definitions.Length; index++)
        {
            var definition = Definitions[index];
            AddEntry(buckets[(int)definition.Group], gameData, definition.ItemId, definition.Cap, CurrencyKind.Generic,
                definition.Group);
        }

        var sections = new List<WalletSection>(GroupCount);
        for (var index = 0; index < GroupCount; index++)
        {
            if (buckets[index].Count > 0)
            {
                sections.Add(new WalletSection((WalletGroup)index, buckets[index].ToArray()));
            }
        }

        return sections.ToArray();
    }

    public static bool IsCurrencyLoaded()
    {
        var manager = InventoryManager.Instance();
        if (manager is null)
        {
            return false;
        }

        var container = manager->GetInventoryContainer(InventoryType.Currency);
        return container is not null && container->IsLoaded;
    }

    public static void RefreshAmounts(WalletEntry gil, WalletEntry[] entries)
    {
        var manager = InventoryManager.Instance();
        if (manager is null)
        {
            gil.Amount = 0;
            for (var index = 0; index < entries.Length; index++)
            {
                entries[index].Amount = 0;
                entries[index].WeeklyAmount = 0;
            }

            return;
        }

        gil.Amount = manager->GetGil();
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            entry.Amount = ReadAmount(manager, entry);
            if (entry.Kind == CurrencyKind.GrandCompanySeal)
            {
                entry.Cap = manager->GetMaxCompanySeals(CompanyFor(entry.ItemId));
            }
            else if (entry.Kind == CurrencyKind.LimitedTomestone)
            {
                entry.WeeklyAmount = manager->GetWeeklyAcquiredTomestoneCount();
                entry.WeeklyCap = InventoryManager.GetLimitedTomestoneWeeklyLimit();
            }
        }
    }

    public static uint GrandCompanySealItemId()
    {
        var playerState = PlayerState.Instance();
        if (playerState is null)
        {
            return 0;
        }

        return playerState->GrandCompany switch
        {
            1 => StormSealItemId,
            2 => SerpentSealItemId,
            3 => FlameSealItemId,
            _ => 0,
        };
    }

    private static void AddTomestones(List<WalletEntry> into, GameData gameData)
    {
        var ids = new List<uint>(4);
        gameData.CollectTomestoneItemIds(ids, out var limitedItemId);
        for (var index = 0; index < ids.Count; index++)
        {
            var kind = ids[index] == limitedItemId ? CurrencyKind.LimitedTomestone : CurrencyKind.Tomestone;
            AddEntry(into, gameData, ids[index], TomestoneCap, kind, WalletGroup.Tomestones);
        }
    }

    private static void AddEntry(List<WalletEntry> into, GameData gameData, uint itemId, long cap, CurrencyKind kind,
        WalletGroup group)
    {
        ResolveItem(gameData, itemId, out var iconId, out var name);
        if (name.Length == 0)
        {
            return;
        }

        into.Add(new WalletEntry(itemId, iconId, name, cap, kind, group));
    }

    private static void ResolveItem(GameData gameData, uint itemId, out uint iconId, out string name)
    {
        if (gameData.TryGetItem(itemId, out var resolvedName, out var resolvedIcon, out _))
        {
            iconId = resolvedIcon;
            name = resolvedName;
            return;
        }

        iconId = 0;
        name = string.Empty;
    }

    private static long ReadAmount(InventoryManager* manager, WalletEntry entry)
    {
        return entry.Kind switch
        {
            CurrencyKind.Gil => manager->GetGil(),
            CurrencyKind.Tomestone or CurrencyKind.LimitedTomestone => manager->GetTomestoneCount(entry.ItemId),
            CurrencyKind.GrandCompanySeal => manager->GetCompanySeals(CompanyFor(entry.ItemId)),
            _ => manager->GetInventoryItemCount(entry.ItemId, false, true, true, 0),
        };
    }

    private static byte CompanyFor(uint sealItemId) => sealItemId switch
    {
        StormSealItemId => 1,
        SerpentSealItemId => 2,
        FlameSealItemId => 3,
        _ => 0,
    };
}
