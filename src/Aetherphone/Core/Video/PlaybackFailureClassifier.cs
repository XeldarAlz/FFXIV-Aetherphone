namespace Aetherphone.Core.Video;

internal enum PlaybackFailureKind : byte
{
    Unknown,
    BotCheck,
    DrmProtected,
    RegionLocked,
    LoginRequired,
    UnsupportedSite,
    SiteChanged,
}

internal static class PlaybackFailureClassifier
{
    private static readonly string[] DrmMarkers = ["drm protected", "drm-protected"];

    private static readonly string[] RegionMarkers =
    [
        "available in your country", "geo restriction", "geo-restricted", "not available from your location",
        "not available in your region",
    ];

    private static readonly string[] BotCheckMarkers = ["not a bot"];

    private static readonly string[] LoginMarkers =
    [
        "--cookies", "logged-in", "login required", "requires authentication", "account credentials", "sign in to",
        "members-only", "private video",
    ];

    private static readonly string[] UnsupportedMarkers = ["unsupported url"];

    private static readonly string[] SiteChangedMarkers =
    [
        "unable to extract", "please report this issue", "keyerror", "marked as broken",
    ];

    internal static PlaybackFailureKind Classify(string text)
    {
        if (ContainsAny(text, DrmMarkers))
        {
            return PlaybackFailureKind.DrmProtected;
        }

        if (ContainsAny(text, RegionMarkers))
        {
            return PlaybackFailureKind.RegionLocked;
        }

        if (ContainsAny(text, BotCheckMarkers))
        {
            return PlaybackFailureKind.BotCheck;
        }

        if (ContainsAny(text, LoginMarkers))
        {
            return PlaybackFailureKind.LoginRequired;
        }

        if (ContainsAny(text, UnsupportedMarkers))
        {
            return PlaybackFailureKind.UnsupportedSite;
        }

        return ContainsAny(text, SiteChangedMarkers) ? PlaybackFailureKind.SiteChanged : PlaybackFailureKind.Unknown;
    }

    internal static bool RetryCannotHelp(PlaybackFailureKind kind) =>
        kind is PlaybackFailureKind.DrmProtected or PlaybackFailureKind.RegionLocked
            or PlaybackFailureKind.LoginRequired or PlaybackFailureKind.UnsupportedSite;

    private static bool ContainsAny(string text, string[] markers)
    {
        for (var index = 0; index < markers.Length; index++)
        {
            if (text.Contains(markers[index], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
