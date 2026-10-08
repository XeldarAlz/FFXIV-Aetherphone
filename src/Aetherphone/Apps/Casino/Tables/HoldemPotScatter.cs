using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Casino.Tables;

internal static class HoldemPotScatter
{
    public const int Count = 6;

    private const ulong Mix = 0x9E3779B97F4A7C15UL;

    public static ulong SeedOf(long handIndex) => (ulong)handIndex * Mix ^ 0x5EED_C0DEUL;

    public static void Fill(ulong seed, Span<Vector2> offsets)
    {
        var random = GameRandom.FromSeed(seed);
        for (var index = 0; index < offsets.Length; index++)
        {
            offsets[index] = new Vector2(random.Range(-1f, 1f), random.Range(-0.45f, 0.45f));
        }
    }
}
