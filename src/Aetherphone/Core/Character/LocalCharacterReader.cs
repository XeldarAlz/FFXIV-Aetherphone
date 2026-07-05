using Aetherphone.Core.Game;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Aetherphone.Core.Character;

internal static unsafe class LocalCharacterReader
{
    private const int MainHandSlot = 0;
    private const int OffHandSlot = 1;
    private const int WaistSlot = 5;
    private const int SoulCrystalSlot = 13;
    private const int ItemLevelSlotCount = 13;

    public static LocalCharacter? Read(GameData gameData)
    {
        var player = gameData.LocalPlayer;

        if (player is null)
        {
            return null;
        }

        var playerState = PlayerState.Instance();

        if (playerState is null)
        {
            return null;
        }

        var female = playerState->Sex == 1;
        var gear = new List<EquippedItem>();
        var averageItemLevel = ReadGear(gear, gameData);

        return new LocalCharacter(player.Name.TextValue, gameData.WorldName(player.HomeWorld.RowId),
            gameData.DataCenterName(player.HomeWorld.RowId), gameData.JobName(player.ClassJob.RowId), player.Level,
            averageItemLevel, gameData.RaceName(playerState->Race, female),
            gameData.ClanName(playerState->Tribe, female), female ? "♀" : "♂",
            FormatNameday(playerState->BirthMonth, playerState->BirthDay),
            gameData.GuardianDeityName(playerState->GuardianDeity), gameData.CityStateName(playerState->StartTown),
            gameData.GrandCompanyName(playerState->GrandCompany), gear);
    }

    private static int ReadGear(List<EquippedItem> into, GameData gameData)
    {
        var manager = InventoryManager.Instance();

        if (manager is null)
        {
            return 0;
        }

        var container = manager->GetInventoryContainer(InventoryType.EquippedItems);

        if (container is null)
        {
            return 0;
        }

        var sum = 0;
        var mainHandItemLevel = 0;
        var hasOffHand = false;
        var size = (int)container->Size;

        for (var slotIndex = 0; slotIndex < size; slotIndex++)
        {
            var slot = container->GetInventorySlot(slotIndex);

            if (slot is null || slot->ItemId == 0 || slotIndex == WaistSlot)
            {
                continue;
            }

            if (!gameData.TryGetItem(slot->ItemId, out var name, out var iconId, out var itemLevel))
            {
                continue;
            }

            into.Add(new EquippedItem(slot->ItemId, iconId, name, itemLevel));

            if (slotIndex == SoulCrystalSlot)
            {
                continue;
            }

            sum += itemLevel;

            if (slotIndex == MainHandSlot)
            {
                mainHandItemLevel = itemLevel;
            }
            else if (slotIndex == OffHandSlot)
            {
                hasOffHand = true;
            }
        }

        if (!hasOffHand)
        {
            sum += mainHandItemLevel;
        }

        return sum / ItemLevelSlotCount;
    }

    private static string FormatNameday(int month, int day)
    {
        if (month < 1 || day < 1)
        {
            return string.Empty;
        }

        var moon = (month + 1) / 2;
        var phase = month % 2 == 1 ? "Astral" : "Umbral";
        return $"{Ordinal(day)} Sun of the {Ordinal(moon)} {phase} Moon";
    }

    private static string Ordinal(int value)
    {
        if (value % 100 is >= 11 and <= 13)
        {
            return $"{value}th";
        }

        var suffix = (value % 10) switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th",
        };
        return $"{value}{suffix}";
    }
}
