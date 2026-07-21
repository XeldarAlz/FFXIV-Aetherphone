namespace Aetherphone.Core.Media;

internal static class LegacyMediaHosts
{
    private const string LegacyPrefix = "https://bucket-production-d88c.up.railway.app/aethernet-media/";
    private const string CurrentPrefix = "https://media.aetherphone.net/";

    public static string Normalize(string url)
    {
        if (!url.StartsWith(LegacyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        return string.Concat(CurrentPrefix, url.AsSpan(LegacyPrefix.Length));
    }
}
