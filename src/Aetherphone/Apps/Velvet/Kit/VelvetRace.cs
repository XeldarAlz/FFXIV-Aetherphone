using Aetherphone.Core.Game;

namespace Aetherphone.Apps.Velvet.Kit;

internal static class VelvetRace
{
    public static readonly int[] All = { 1, 2, 4, 5, 6, 7, 8 };

    private static readonly int AllowedMask = MaskOf(All);

    public static int Bit(int raceId) => raceId >= 1 && raceId <= 8 ? 1 << (raceId - 1) : 0;

    public static int Sanitize(int mask) => mask & AllowedMask;

    public static bool Has(int mask, int raceId) => (mask & Bit(raceId)) != 0;

    public static int Toggle(int mask, int raceId) =>
        Has(mask, raceId) ? mask & ~Bit(raceId) : mask | Bit(raceId);

    public static int Count(int mask)
    {
        var count = 0;
        for (var index = 0; index < All.Length; index++)
        {
            if (Has(mask, All[index]))
            {
                count++;
            }
        }

        return count;
    }

    private static readonly string[] LabelCache = new string[9];

    public static string Label(GameData gameData, int raceId)
    {
        if (raceId < 1 || raceId > 8)
        {
            return string.Empty;
        }

        var cached = LabelCache[raceId];
        if (cached is not null)
        {
            return cached;
        }

        var label = gameData.RaceName((uint)raceId, false);
        if (label.Length > 0)
        {
            LabelCache[raceId] = label;
        }

        return label;
    }

    private static int MaskOf(int[] raceIds)
    {
        var mask = 0;
        for (var index = 0; index < raceIds.Length; index++)
        {
            mask |= Bit(raceIds[index]);
        }

        return mask;
    }
}
