using Dalamud.Plugin.Ipc.Exceptions;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using Character = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

namespace Aetherphone.Core.Game;

[Flags]
internal enum ModSigns : byte
{
    None = 0,
    Penumbra = 1,
    Appearance = 2,
    BodyScaling = 4,
    Posing = 8,
    Unchecked = 16,
}

internal static unsafe class ModScan
{
    private const string PenumbraEnabled = "Penumbra.GetEnabledState";
    private const string PenumbraPlayerPaths = "Penumbra.GetPlayerResourcePaths.V5";
    private const string CustomizePlusProfile = "CustomizePlus.Profile.GetActiveProfileIdOnCharacter";
    private static readonly int[] DrawnToEquipped = { 2, 3, 4, 6, 7, 8, 9, 10, 11, 12 };
    private static readonly string[] PosingPlugins = { "Brio", "Ktisis" };

    public static ModSigns Scan()
    {
        var player = GameMemory.Attached ? Plugin.ObjectTable.LocalPlayer : null;
        if (player is null)
        {
            return ModSigns.Unchecked;
        }

        var signs = PenumbraOnPlayer() | BodyScaled(player.ObjectIndex);
        if (DrawnDiffersFromGlamour((Character*)player.Address))
        {
            signs |= ModSigns.Appearance;
        }

        for (var index = 0; index < PosingPlugins.Length; index++)
        {
            if (Loaded(PosingPlugins[index]))
            {
                signs |= ModSigns.Posing;
            }
        }

        return signs;
    }

    public static ModSigns? Stamped(Dictionary<string, byte> stamps, string path) =>
        stamps.TryGetValue(Path.GetFileName(path), out var signs) ? (ModSigns)signs : null;

    public static void Stamp(Dictionary<string, byte> stamps, string path, ModSigns signs) =>
        stamps[Path.GetFileName(path)] = (byte)signs;

    private static ModSigns PenumbraOnPlayer()
    {
        try
        {
            if (!Plugin.PluginInterface.GetIpcSubscriber<bool>(PenumbraEnabled).InvokeFunc())
            {
                return ModSigns.None;
            }

            var trees = Plugin.PluginInterface
                .GetIpcSubscriber<Dictionary<ushort, Dictionary<string, HashSet<string>>>>(PenumbraPlayerPaths)
                .InvokeFunc();
            foreach (var tree in trees.Values)
            {
                foreach (var (actual, gamePaths) in tree)
                {
                    if (Path.IsPathRooted(actual) || !Contains(gamePaths, actual))
                    {
                        return ModSigns.Penumbra;
                    }
                }
            }

            return ModSigns.None;
        }
        catch (IpcNotReadyError)
        {
            return ModSigns.None;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[ModScan] could not read Penumbra's resource paths");
            return ModSigns.Unchecked;
        }
    }

    private static ModSigns BodyScaled(ushort objectIndex)
    {
        try
        {
            var (error, profile) = Plugin.PluginInterface
                .GetIpcSubscriber<ushort, (int, Guid?)>(CustomizePlusProfile).InvokeFunc(objectIndex);
            return error == 0 && profile is not null ? ModSigns.BodyScaling : ModSigns.None;
        }
        catch (IpcNotReadyError)
        {
            return ModSigns.None;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[ModScan] could not read the Customize+ profile");
            return ModSigns.Unchecked;
        }
    }

    private static bool DrawnDiffersFromGlamour(Character* character)
    {
        var manager = InventoryManager.Instance();
        var container = manager is null ? null : manager->GetInventoryContainer(InventoryType.EquippedItems);
        if (container is null || !container->IsLoaded)
        {
            return false;
        }

        var drawn = character->DrawData.EquipmentModelIds;
        var items = Plugin.DataManager.GetExcelSheet<Item>();
        var slots = Math.Min(drawn.Length, DrawnToEquipped.Length);
        for (var slot = 0; slot < slots; slot++)
        {
            var model = drawn[slot];
            var item = container->GetInventorySlot(DrawnToEquipped[slot]);
            if (item is null || item->ItemId == 0 || model.Id == 0)
            {
                continue;
            }

            var look = item->GlamourId != 0 ? item->GlamourId : item->ItemId;
            if (!items.TryGetRow(look, out var row))
            {
                continue;
            }

            var main = row.ModelMain;
            if (model.Id != (ushort)main || model.Variant != (byte)(main >> 16) || model.Stain0 != item->GetStain(0) ||
                model.Stain1 != item->GetStain(1))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Contains(HashSet<string> gamePaths, string actual)
    {
        foreach (var gamePath in gamePaths)
        {
            if (string.Equals(gamePath, actual, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Loaded(string internalName)
    {
        foreach (var plugin in Plugin.PluginInterface.InstalledPlugins)
        {
            if (plugin.IsLoaded && string.Equals(plugin.InternalName, internalName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
