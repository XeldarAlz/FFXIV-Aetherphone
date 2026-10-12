namespace Aetherphone.Core.Lodestone;

internal static class LookupRanking
{
    private const int ExactRank = 0;
    private const int PrefixRank = 1;
    private const int ContainsRank = 2;
    private const int OtherRank = 3;

    public static int Rank(string name, string query)
    {
        if (string.Equals(name, query, StringComparison.OrdinalIgnoreCase))
        {
            return ExactRank;
        }

        if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return PrefixRank;
        }

        if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return ContainsRank;
        }

        return OtherRank;
    }

    public static void SortByRank<TMatch>(TMatch[] matches, string query) where TMatch : ILookupMatch
    {
        var keys = new int[matches.Length];
        for (var index = 0; index < matches.Length; index++)
        {
            keys[index] = Rank(matches[index].Name, query) * matches.Length + index;
        }

        Array.Sort(keys, matches);
    }
}
