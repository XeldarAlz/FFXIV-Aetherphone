using Aetherphone.Core.Aethernet.Contracts;

namespace Aetherphone.Apps.Prism;

internal static class PostedMods
{
    public static string Key(GlamModDto mod) => string.Concat(mod.Name, "|", mod.Author);

    public static GlamGearDto[] Unmodded(GlamGearDto[] gear)
    {
        var kept = new List<GlamGearDto>(gear.Length);
        for (var index = 0; index < gear.Length; index++)
        {
            var piece = gear[index];
            if (piece.ItemId != 0)
            {
                kept.Add(piece.Mods is null ? piece : piece with { Mods = null });
            }
        }

        return kept.ToArray();
    }

    public static GlamGearDto[] Without(GlamGearDto[] gear, HashSet<string> dropped)
    {
        if (dropped.Count == 0)
        {
            return gear;
        }

        var kept = new List<GlamGearDto>(gear.Length);
        for (var index = 0; index < gear.Length; index++)
        {
            var piece = gear[index];
            if (piece.Mods is not { Length: > 0 } mods)
            {
                kept.Add(piece);
                continue;
            }

            var shared = new List<GlamModDto>(mods.Length);
            for (var modIndex = 0; modIndex < mods.Length; modIndex++)
            {
                if (!dropped.Contains(Key(mods[modIndex])))
                {
                    shared.Add(mods[modIndex]);
                }
            }

            if (piece.ItemId == 0 && shared.Count == 0)
            {
                continue;
            }

            kept.Add(piece with { Mods = shared.Count > 0 ? shared.ToArray() : null });
        }

        return kept.ToArray();
    }
}
