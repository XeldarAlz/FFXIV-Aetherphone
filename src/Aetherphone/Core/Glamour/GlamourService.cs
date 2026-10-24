using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Game;
using Aetherphone.Core.GameChat;
using Aetherphone.Core.Inventory;
using Dalamud.Plugin;

namespace Aetherphone.Core.Glamour;

internal sealed class GlamourService : IDisposable
{
    private readonly PenumbraBridge penumbra;
    private readonly GlamourerBridge glamourer;
    private readonly Dictionary<int, HashSet<string>> foldersBySlot = new();
    private readonly ModMetaCache modMeta = new();
    private bool penumbraReady;
    private bool glamourerReady;
    private bool probed;
    private bool tryingOn;
    private string before = string.Empty;
    private ulong snapshotFor;

    public GlamourService(IDalamudPluginInterface pluginInterface)
    {
        penumbra = new PenumbraBridge(pluginInterface);
        glamourer = new GlamourerBridge(pluginInterface);
    }

    public bool HasPenumbra
    {
        get
        {
            Refresh();
            return penumbraReady;
        }
    }

    public bool HasGlamourer
    {
        get
        {
            Refresh();
            return glamourerReady;
        }
    }

    public bool TryingOn => tryingOn && !SnapshotStale();

    public GlamGearDto[] Capture() => Capture(out _, out _);

    public GlamGearDto[] Capture(out int race, out int gender)
    {
        race = 0;
        gender = 0;
        var gear = HasGlamourer
            ? GlamourerDesign.Read(glamourer.State(), out race, out gender)
            : Array.Empty<GlamGearDto>();
        if (gear.Length == 0)
        {
            gear = EquippedOutfit.Read();
        }

        if (!HasPenumbra || !penumbra.Enabled() || penumbra.PlayerPaths() is not { } paths)
        {
            return gear;
        }

        var root = penumbra.ModDirectory();
        foldersBySlot.Clear();
        ModAttribution.Collect(paths, root, foldersBySlot);
        if (foldersBySlot.Count == 0)
        {
            return gear;
        }

        var names = penumbra.ModList();
        var hasCollection = penumbra.TryPlayerCollection(out var collection);
        var captured = new List<GlamGearDto>(gear.Length + 1);
        for (var index = 0; index < gear.Length; index++)
        {
            var piece = gear[index];
            captured.Add(foldersBySlot.TryGetValue(piece.Slot, out var folders)
                ? piece with { Mods = Describe(root, folders, names, hasCollection, collection) }
                : piece);
        }

        if (foldersBySlot.TryGetValue(GearSlots.None, out var character))
        {
            captured.Add(new GlamGearDto(GearSlots.None, Mods: Describe(root, character, names, hasCollection, collection)));
        }

        return captured.ToArray();
    }

    public bool TryBody(out int race, out int gender)
    {
        race = 0;
        gender = 0;
        if (HasGlamourer)
        {
            GlamourerDesign.Read(glamourer.State(), out race, out gender);
        }

        return race > 0;
    }

    public static bool IsVanilla(GlamGearDto[] gear)
    {
        for (var index = 0; index < gear.Length; index++)
        {
            if (gear[index].Mods is { Length: > 0 })
            {
                return false;
            }
        }

        return true;
    }

    public int MissingMods(GlamGearDto[] gear, out int total)
    {
        total = 0;
        var installed = HasPenumbra ? penumbra.ModList() : null;
        var missing = 0;
        for (var index = 0; index < gear.Length; index++)
        {
            if (gear[index].Mods is not { } mods)
            {
                continue;
            }

            for (var modIndex = 0; modIndex < mods.Length; modIndex++)
            {
                total++;
                if (installed is null || ModAttribution.MatchFolder(installed, mods[modIndex]).Length == 0)
                {
                    missing++;
                }
            }
        }

        return missing;
    }

    public bool HasMod(GlamModDto mod) =>
        HasPenumbra && ModAttribution.MatchFolder(penumbra.ModList(), mod).Length > 0;

    public static int OwnedPieces(GlamGearDto[] gear, out int total, HashSet<uint>? owned = null)
    {
        owned?.Clear();
        total = 0;
        var count = 0;
        for (var index = 0; index < gear.Length; index++)
        {
            var itemId = gear[index].ItemId;
            if (itemId == 0)
            {
                continue;
            }

            total++;
            if (OwnedItems.Owns(itemId))
            {
                count++;
                owned?.Add(itemId);
            }
        }

        return count;
    }

    public void TryOn(GlamGearDto[] gear)
    {
        if (!HasGlamourer)
        {
            TryOnInGame(gear);
            return;
        }

        var replacing = Prepare();
        var attempted = 0;
        var applied = 0;
        for (var index = 0; index < gear.Length; index++)
        {
            var piece = gear[index];
            if (piece.ItemId == 0 || !GearSlots.IsGear(piece.Slot))
            {
                continue;
            }

            attempted++;
            if (glamourer.SetItem((GearSlot)piece.Slot, piece.ItemId, piece.Stain0, piece.Stain1))
            {
                applied++;
            }
        }

        if (attempted > 0 && applied == 0)
        {
            Forget();
            TryOnInGame(gear);
            return;
        }

        WearMods(gear, replacing);
    }

    public void TryOnPiece(GlamGearDto piece)
    {
        if (piece.ItemId == 0)
        {
            return;
        }

        if (HasGlamourer && GearSlots.IsGear(piece.Slot))
        {
            Snapshot();
            if (glamourer.SetItem((GearSlot)piece.Slot, piece.ItemId, piece.Stain0, piece.Stain1))
            {
                return;
            }
        }

        GameLinkActions.TryOn(piece.ItemId, piece.Stain0, piece.Stain1);
    }

    public void Revert()
    {
        if (!tryingOn)
        {
            return;
        }

        if (SnapshotStale())
        {
            Forget();
            return;
        }

        tryingOn = false;
        if (HasGlamourer)
        {
            RestoreEquipment();
        }

        before = string.Empty;
        if (HasPenumbra)
        {
            penumbra.ClearTemporary();
            penumbra.Redraw();
        }
    }

    public string ReadState() => HasGlamourer ? glamourer.State() : string.Empty;

    public bool ApplyState(string state, GlamGearDto[] gear)
    {
        if (!HasGlamourer || state.Length == 0)
        {
            return false;
        }

        var replacing = Prepare();
        var applied = glamourer.ApplyState(state);
        WearMods(applied ? gear : Array.Empty<GlamGearDto>(), replacing);
        return applied;
    }

    public bool ApplyDesign(Guid id)
    {
        if (!HasGlamourer)
        {
            return false;
        }

        var replacing = Prepare();
        var applied = glamourer.ApplyDesign(id);
        WearMods(Array.Empty<GlamGearDto>(), replacing);
        return applied;
    }

    public Dictionary<Guid, (string, string, uint, bool)> Designs() =>
        HasGlamourer ? glamourer.Designs() : new Dictionary<Guid, (string, string, uint, bool)>();

    public string Design(Guid id) => HasGlamourer ? glamourer.Design(id) : string.Empty;

    public bool HasDesign(Guid id) => HasGlamourer && glamourer.Designs().ContainsKey(id);

    public Guid SaveDesign(string state, string name) => HasGlamourer ? glamourer.AddDesign(state, name) : Guid.Empty;

    public void Dispose()
    {
        Revert();
        penumbra.Dispose();
        glamourer.Dispose();
    }

    private bool SnapshotStale()
    {
        var current = InventoryReader.ReadLocalContentId();
        return current != 0 && current != snapshotFor;
    }

    private void Forget()
    {
        tryingOn = false;
        before = string.Empty;
        if (HasPenumbra)
        {
            penumbra.ClearTemporary();
        }
    }

    private void Snapshot()
    {
        if (tryingOn && SnapshotStale())
        {
            Forget();
        }

        if (tryingOn)
        {
            return;
        }

        before = glamourer.State();
        snapshotFor = InventoryReader.ReadLocalContentId();
        tryingOn = true;
    }

    private bool Prepare()
    {
        if (tryingOn && SnapshotStale())
        {
            Forget();
        }

        if (!tryingOn)
        {
            Snapshot();
            return false;
        }

        RestoreEquipment();
        if (HasPenumbra)
        {
            penumbra.ClearTemporary();
        }

        return true;
    }

    private void WearMods(GlamGearDto[] gear, bool replacing)
    {
        if (HasPenumbra && (ApplyMods(gear) || replacing))
        {
            penumbra.Redraw();
        }
    }

    private void RestoreEquipment()
    {
        if (before.Length == 0 || !glamourer.ApplyState(before))
        {
            glamourer.RevertEquipment();
        }
    }

    private static void TryOnInGame(GlamGearDto[] gear)
    {
        for (var index = 0; index < gear.Length; index++)
        {
            var piece = gear[index];
            if (piece.ItemId != 0)
            {
                GameLinkActions.TryOn(piece.ItemId, piece.Stain0, piece.Stain1);
            }
        }
    }

    private bool ApplyMods(GlamGearDto[] gear)
    {
        var installed = penumbra.ModList();
        var applied = false;
        for (var index = 0; index < gear.Length; index++)
        {
            if (gear[index].Mods is not { } mods)
            {
                continue;
            }

            for (var modIndex = 0; modIndex < mods.Length; modIndex++)
            {
                var folder = ModAttribution.MatchFolder(installed, mods[modIndex]);
                if (folder.Length > 0 && penumbra.SetTemporary(folder, ModAttribution.Settings(mods[modIndex].Options)))
                {
                    applied = true;
                }
            }
        }

        return applied;
    }

    private GlamModDto[] Describe(string root, HashSet<string> folders, Dictionary<string, string> names,
        bool hasCollection, Guid collection)
    {
        var mods = new GlamModDto[folders.Count];
        var index = 0;
        foreach (var folder in folders)
        {
            var listed = names.TryGetValue(folder, out var name) ? name : string.Empty;
            var mod = modMeta.Describe(root, folder, listed);
            mods[index++] = hasCollection
                ? mod with { Options = ModAttribution.Options(penumbra.CurrentOptions(collection, folder)) }
                : mod;
        }

        return mods;
    }

    private void Refresh()
    {
        if (!probed || penumbra.ConsumeReady() || penumbra.ConsumeDisposing())
        {
            penumbraReady = penumbra.Probe();
            modMeta.Clear();
        }

        if (!probed || glamourer.ConsumeReady() || glamourer.ConsumeDisposing())
        {
            glamourerReady = glamourer.Probe();
        }

        probed = true;
    }
}
