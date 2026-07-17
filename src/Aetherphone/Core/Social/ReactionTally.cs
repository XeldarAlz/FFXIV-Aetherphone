namespace Aetherphone.Core.Social;

internal static class ReactionTally
{
    public static (int[] Counts, int Total) Shift(int[] source, int previousKind, int nextKind)
    {
        var counts = (int[])source.Clone();
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
