using Aetherphone.Core.Game;
using Aetherphone.Core.Localization;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Aetherphone.Core.Wallet;

internal static unsafe class WalletReader
{
    private const long SealCap = 4000;
    private const long ScripCap = 4000;
    private const long TomestoneCap = 2000;
    private const long PvpCap = 20000;
    private const long SkybuildersCap = 10000;
    private const long BicolorCap = 1500;
    private const uint GilItemId = 1;
    private const uint MgpItemId = 29;
    private const uint VentureItemId = 21072;
    private const uint StormSealItemId = 20;
    private const uint SerpentSealItemId = 21;
    private const uint FlameSealItemId = 22;

    private readonly struct Def
    {
        public readonly uint ItemId;
        public readonly long Cap;

        public Def(uint itemId, long cap)
        {
            ItemId = itemId;
            Cap = cap;
        }
    }

    private static readonly Def[] HuntDefs = { new(27, SealCap), new(10307, SealCap), new(26533, SealCap), };
    private static readonly Def[] PvpDefs = { new(25, PvpCap), new(36656, PvpCap), };

    private static readonly Def[] ScripDefs =
    {
        new(33913, ScripCap), new(33914, ScripCap), new(41784, ScripCap), new(41785, ScripCap),
        new(28063, SkybuildersCap),
    };

    private static readonly Def[] OtherDefs = { new(26807, BicolorCap), };

    private static readonly List<uint> TomestoneScratch = new(4);

    public static WalletEntry BuildGil(GameData gameData)
    {
        ResolveItem(gameData, GilItemId, out var iconId, out var name);
        if (name.Length == 0)
        {
            name = "Gil";
        }

        return new WalletEntry(GilItemId, iconId, name, 0, CurrencyKind.Gil);
    }

    public static WalletSection[] BuildSections(GameData gameData)
    {
        var sections = new List<WalletSection>(6) { new(L.Wallet.SectionCurrency, BuildCurrency(gameData)), };
        AddDefSection(sections, gameData, L.Wallet.SectionHunt, HuntDefs);
        AddTomestones(sections, gameData);
        AddDefSection(sections, gameData, L.Wallet.SectionPvp, PvpDefs);
        AddDefSection(sections, gameData, L.Wallet.SectionCrafting, ScripDefs);
        AddDefSection(sections, gameData, L.Wallet.SectionOther, OtherDefs);
        return sections.ToArray();
    }

    public static void RefreshAmounts(WalletEntry gil, WalletSection[] sections)
    {
        var manager = InventoryManager.Instance();
        if (manager is null)
        {
            gil.Amount = 0;
            ClearAmounts(sections);
            return;
        }

        gil.Amount = (long)manager->GetGil();
        for (var sectionIndex = 0; sectionIndex < sections.Length; sectionIndex++)
        {
            var entries = sections[sectionIndex].Entries;
            for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
            {
                entries[entryIndex].Amount = ReadAmount(manager, entries[entryIndex]);
            }
        }
    }

    public static int CountCapped(GameData gameData)
    {
        var manager = InventoryManager.Instance();
        if (manager is null)
        {
            return 0;
        }

        var count = CountCappedDefs(manager, HuntDefs) + CountCappedDefs(manager, PvpDefs) +
                    CountCappedDefs(manager, ScripDefs) + CountCappedDefs(manager, OtherDefs);
        TomestoneScratch.Clear();
        gameData.CollectTomestoneItemIds(TomestoneScratch);
        for (var index = 0; index < TomestoneScratch.Count; index++)
        {
            if ((long)manager->GetTomestoneCount(TomestoneScratch[index]) >= TomestoneCap)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountCappedDefs(InventoryManager* manager, Def[] defs)
    {
        var count = 0;
        for (var index = 0; index < defs.Length; index++)
        {
            if (defs[index].Cap <= 0)
            {
                continue;
            }

            if ((long)manager->GetInventoryItemCount(defs[index].ItemId, false, true, true, 0) >= defs[index].Cap)
            {
                count++;
            }
        }

        return count;
    }

    private static WalletEntry[] BuildCurrency(GameData gameData)
    {
        var entries = new List<WalletEntry>(4);
        AddEntry(entries, gameData, MgpItemId, 0);
        AddEntry(entries, gameData, VentureItemId, 0);
        var sealItemId = GrandCompanySealItemId();
        if (sealItemId != 0)
        {
            AddEntry(entries, gameData, sealItemId, 0);
        }

        return entries.ToArray();
    }

    private static void AddDefSection(List<WalletSection> sections, GameData gameData, LocString title, Def[] defs)
    {
        var entries = new List<WalletEntry>(defs.Length);
        for (var index = 0; index < defs.Length; index++)
        {
            AddEntry(entries, gameData, defs[index].ItemId, defs[index].Cap);
        }

        if (entries.Count > 0)
        {
            sections.Add(new WalletSection(title, entries.ToArray()));
        }
    }

    private static void AddTomestones(List<WalletSection> sections, GameData gameData)
    {
        var ids = new List<uint>(4);
        gameData.CollectTomestoneItemIds(ids);
        var entries = new List<WalletEntry>(ids.Count);
        for (var index = 0; index < ids.Count; index++)
        {
            ResolveItem(gameData, ids[index], out var iconId, out var name);
            if (name.Length == 0)
            {
                continue;
            }

            entries.Add(new WalletEntry(ids[index], iconId, name, TomestoneCap, CurrencyKind.Tomestone));
        }

        if (entries.Count > 0)
        {
            sections.Add(new WalletSection(L.Wallet.SectionTomestones, entries.ToArray()));
        }
    }

    private static void AddEntry(List<WalletEntry> entries, GameData gameData, uint itemId, long cap)
    {
        ResolveItem(gameData, itemId, out var iconId, out var name);
        if (name.Length == 0)
        {
            return;
        }

        entries.Add(new WalletEntry(itemId, iconId, name, cap, CurrencyKind.Generic));
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
            CurrencyKind.Gil => (long)manager->GetGil(),
            CurrencyKind.Tomestone => (long)manager->GetTomestoneCount(entry.ItemId),
            _ => (long)manager->GetInventoryItemCount(entry.ItemId, false, true, true, 0),
        };
    }

    private static void ClearAmounts(WalletSection[] sections)
    {
        for (var sectionIndex = 0; sectionIndex < sections.Length; sectionIndex++)
        {
            var entries = sections[sectionIndex].Entries;
            for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
            {
                entries[entryIndex].Amount = 0;
            }
        }
    }

    private static uint GrandCompanySealItemId()
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
}
