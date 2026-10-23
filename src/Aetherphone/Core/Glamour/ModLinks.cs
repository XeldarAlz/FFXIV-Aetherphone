namespace Aetherphone.Core.Glamour;

internal static class ModLinks
{
    private static readonly string[] AllowedHosts =
    {
        "xivmodarchive.com", "heliosphere.app", "patreon.com", "ko-fi.com", "gumroad.com", "carrd.co",
    };

    public static bool IsAllowed(string link)
    {
        if (link.Length == 0 || !Uri.TryCreate(link, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var host = uri.Host;
        for (var index = 0; index < AllowedHosts.Length; index++)
        {
            var allowed = AllowedHosts[index];
            if (string.Equals(host, allowed, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
