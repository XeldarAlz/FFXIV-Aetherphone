using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Glamour;

namespace Aetherphone.Apps.Prism;

internal sealed class ModDraft
{
    public List<GlamModDto> Mods { get; } = new();

    public List<int> Slots { get; } = new();

    public string Name { get; set; } = string.Empty;

    public int Slot { get; set; } = GearSlots.None;

    public int Count => Mods.Count;

    public void Clear()
    {
        Mods.Clear();
        Slots.Clear();
        Name = string.Empty;
        Slot = GearSlots.None;
    }

    public void Add(GlamModDto mod, int slot)
    {
        Mods.Add(mod);
        Slots.Add(slot);
    }

    public void RemoveAt(int index)
    {
        Mods.RemoveAt(index);
        Slots.RemoveAt(index);
    }

    public bool Contains(string key)
    {
        for (var index = 0; index < Mods.Count; index++)
        {
            if (PostedMods.Key(Mods[index]) == key)
            {
                return true;
            }
        }

        return false;
    }

    public void Load(GlamGearDto[] gear)
    {
        Clear();
        for (var index = 0; index < gear.Length; index++)
        {
            var mods = gear[index].Mods ?? Array.Empty<GlamModDto>();
            for (var modIndex = 0; modIndex < mods.Length; modIndex++)
            {
                Add(mods[modIndex], gear[index].Slot);
            }
        }
    }

    public GlamGearDto[] AttachTo(GlamGearDto[] pieces)
    {
        if (Mods.Count == 0)
        {
            return pieces;
        }

        var character = new List<GlamModDto>();
        for (var index = 0; index < Mods.Count; index++)
        {
            var piece = Array.FindIndex(pieces, gear => gear.Slot == Slots[index]);
            if (piece < 0)
            {
                character.Add(Mods[index]);
                continue;
            }

            var mods = pieces[piece].Mods ?? Array.Empty<GlamModDto>();
            pieces[piece] = pieces[piece] with { Mods = [.. mods, Mods[index]] };
        }

        return character.Count == 0 ? pieces : [.. pieces, new GlamGearDto(GearSlots.None, Mods: character.ToArray())];
    }
}
