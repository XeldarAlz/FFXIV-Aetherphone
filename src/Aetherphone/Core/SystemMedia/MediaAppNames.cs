namespace Aetherphone.Core.SystemMedia;

internal static class MediaAppNames
{
    private const string ExecutableSuffix = ".exe";
    private const char PackageSeparator = '!';
    private const char PublisherSeparator = '_';
    private const string GenericPackageAppId = "App";

    private static readonly (string Key, string Name)[] Known =
    {
        ("spotify", "Spotify"),
        ("spotifyab.spotifymusic", "Spotify"),
        ("chrome", "Google Chrome"),
        ("msedge", "Microsoft Edge"),
        ("firefox", "Firefox"),
        ("brave", "Brave"),
        ("opera", "Opera"),
        ("vivaldi", "Vivaldi"),
        ("foobar2000", "foobar2000"),
        ("vlc", "VLC"),
        ("musicbee", "MusicBee"),
        ("aimp", "AIMP"),
        ("winamp", "Winamp"),
        ("tidal", "TIDAL"),
        ("discord", "Discord"),
        ("microsoft.zunemusic", "Media Player"),
        ("microsoft.zunevideo", "Movies & TV"),
        ("appleinc.applemusicwin", "Apple Music"),
    };

    public static string FromAppUserModelId(string appUserModelId) =>
        KnownName(appUserModelId) ?? Fallback(appUserModelId);

    private static string? KnownName(string appUserModelId)
    {
        var value = appUserModelId.AsSpan().Trim();
        if (value.IsEmpty)
        {
            return null;
        }

        var separator = value.IndexOf(PackageSeparator);
        if (separator < 0)
        {
            return Lookup(ExecutableStem(value));
        }

        return Lookup(PackageName(value[..separator])) ?? Lookup(value[(separator + 1)..]);
    }

    public static bool IsOwnProcess(string appUserModelId, string processFileName)
    {
        if (appUserModelId.Length == 0 || processFileName.Length == 0)
        {
            return false;
        }

        return ExecutableStem(appUserModelId.AsSpan().Trim())
            .Equals(ExecutableStem(processFileName.AsSpan()), StringComparison.OrdinalIgnoreCase);
    }

    private static string Fallback(string appUserModelId)
    {
        var value = appUserModelId.AsSpan().Trim();
        if (value.IsEmpty)
        {
            return string.Empty;
        }

        var separator = value.IndexOf(PackageSeparator);
        if (separator < 0)
        {
            return ExecutableStem(value).ToString();
        }

        var applicationId = value[(separator + 1)..];
        if (!applicationId.IsEmpty && !applicationId.Equals(GenericPackageAppId, StringComparison.OrdinalIgnoreCase))
        {
            return LastSegment(applicationId).ToString();
        }

        return LastSegment(PackageName(value[..separator])).ToString();
    }

    private static ReadOnlySpan<char> PackageName(ReadOnlySpan<char> familyName)
    {
        var publisher = familyName.LastIndexOf(PublisherSeparator);
        return publisher > 0 ? familyName[..publisher] : familyName;
    }

    private static ReadOnlySpan<char> ExecutableStem(ReadOnlySpan<char> value)
    {
        var slash = value.LastIndexOfAny('\\', '/');
        var fileName = slash >= 0 ? value[(slash + 1)..] : value;
        return fileName.EndsWith(ExecutableSuffix, StringComparison.OrdinalIgnoreCase)
            ? fileName[..^ExecutableSuffix.Length]
            : fileName;
    }

    private static ReadOnlySpan<char> LastSegment(ReadOnlySpan<char> value)
    {
        var dot = value.LastIndexOf('.');
        return dot >= 0 && dot < value.Length - 1 ? value[(dot + 1)..] : value;
    }

    private static string? Lookup(ReadOnlySpan<char> key)
    {
        for (var index = 0; index < Known.Length; index++)
        {
            if (key.Equals(Known[index].Key, StringComparison.OrdinalIgnoreCase))
            {
                return Known[index].Name;
            }
        }

        return null;
    }
}
