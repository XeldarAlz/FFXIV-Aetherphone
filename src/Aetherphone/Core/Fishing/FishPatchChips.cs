namespace Aetherphone.Core.Fishing;

internal static class FishPatchChips
{
    public const int PatchSlots = 12;

    private const float Hundredths = 100f;

    public static int Key(float patch) => (int)MathF.Round(patch * Hundredths);

    public static int Collect(ReadOnlySpan<FishEntry> entries, int expansion, Span<float> patches)
    {
        var count = 0;
        for (var index = 0; index < entries.Length; index++)
        {
            var fish = entries[index].Fish;
            if (fish.Patch <= 0f || fish.Expansion != expansion)
            {
                continue;
            }

            count = Insert(patches, count, fish.Patch);
        }

        return count;
    }

    private static int Insert(Span<float> patches, int count, float patch)
    {
        var key = Key(patch);
        var slot = 0;
        while (slot < count && Key(patches[slot]) < key)
        {
            slot++;
        }

        if (slot == patches.Length || (slot < count && Key(patches[slot]) == key))
        {
            return count;
        }

        var last = Math.Min(count, patches.Length - 1);
        for (var shift = last; shift > slot; shift--)
        {
            patches[shift] = patches[shift - 1];
        }

        patches[slot] = patch;
        return Math.Min(count + 1, patches.Length);
    }
}
