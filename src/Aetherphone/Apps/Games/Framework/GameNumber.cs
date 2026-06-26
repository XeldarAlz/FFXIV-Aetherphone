using System.Collections.Generic;

namespace Aetherphone.Apps.Games.Framework;

internal static class GameNumber
{
    private static readonly Dictionary<int, string> Cache = new();

    public static string Label(int value)
    {
        if (Cache.TryGetValue(value, out var label))
        {
            return label;
        }

        label = value.ToString();
        Cache[value] = label;
        return label;
    }
}
