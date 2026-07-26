namespace Aetherphone.Core.Social;

internal static class ReactionTally
{
    public static int At(int[] counts, int kind) => kind >= 0 && kind < counts.Length ? counts[kind] : 0;

    public static (int[] Counts, int Total) Shift(int[] source, int previousKind, int nextKind)
    {
        var counts = new int[Math.Max(source.Length, nextKind + 1)];
        Array.Copy(source, counts, source.Length);
        if (previousKind >= 0 && previousKind < counts.Length && counts[previousKind] > 0)
        {
            counts[previousKind]--;
        }

        if (nextKind >= 0 && nextKind < counts.Length)
        {
            counts[nextKind]++;
        }

        var total = 0;
        for (var index = 0; index < counts.Length; index++)
        {
            total += counts[index];
        }

        return (counts, total);
    }
}
