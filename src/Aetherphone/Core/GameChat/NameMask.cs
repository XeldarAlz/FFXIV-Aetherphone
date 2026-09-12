namespace Aetherphone.Core.GameChat;

internal static class NameMask
{
    private const string Prefix = "Player ";
    private const uint FnvOffset = 2166136261;
    private const uint FnvPrime = 16777619;

    private static readonly Dictionary<string, string> Cache = new(StringComparer.Ordinal);
    private static readonly uint Salt = (uint)Random.Shared.Next(1, int.MaxValue);

    public static bool Enabled { get; private set; }

    public static void Set(bool enabled)
    {
        if (Enabled == enabled)
        {
            return;
        }

        Enabled = enabled;
        Cache.Clear();
    }

    public static string Display(string name) => Enabled && name.Length > 0 ? Of(name) : name;

    public static string Of(string name)
    {
        if (Cache.TryGetValue(name, out var cached))
        {
            return cached;
        }

        var hash = FnvOffset ^ Salt;
        for (var index = 0; index < name.Length; index++)
        {
            hash ^= char.ToUpperInvariant(name[index]);
            hash *= FnvPrime;
        }

        var label = string.Concat(Prefix, hash.ToString("X8", System.Globalization.CultureInfo.InvariantCulture));
        Cache[name] = label;
        return label;
    }
}
