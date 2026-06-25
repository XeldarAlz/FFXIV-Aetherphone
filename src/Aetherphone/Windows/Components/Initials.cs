namespace Aetherphone.Windows.Components;

internal static class Initials
{
    private static readonly Dictionary<string, string> Cache = new();

    public static string Of(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "?";
        }

        if (Cache.TryGetValue(name, out var initial))
        {
            return initial;
        }

        initial = name.Substring(0, 1).ToUpperInvariant();
        Cache[name] = initial;
        return initial;
    }
}
