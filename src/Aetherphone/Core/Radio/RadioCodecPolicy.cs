namespace Aetherphone.Core.Radio;

internal static class RadioCodecPolicy
{
    private const string OpusHint = "opus";
    private const string Ogg = "OGG";
    private const string Unknown = "UNKNOWN";

    private static readonly string[] DecodableCodecs = { "MP3", "AAC", "AAC+", "AACP", "HE-AAC", "HEAAC", "OPUS" };

    private static readonly string[] PlayableExtensions = { ".mp3", ".aac", ".aacp", ".m3u8", ".opus", ".pls", ".m3u" };

    // Radio Browser files Vorbis and Opus together as OGG and we only decode Opus, so an OGG station
    // passes only when its address names Opus. Unknown codecs pass when the address or the HLS flag
    // says what the bytes will be, since the player sniffs the real format on connect anyway.
    public static bool IsPlayable(string? codec, string? url, bool hls)
    {
        if (string.IsNullOrWhiteSpace(codec) || codec.Trim().Equals(Unknown, StringComparison.OrdinalIgnoreCase))
        {
            return hls || HasPlayableExtension(url);
        }

        var tokens = codec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            return hls || HasPlayableExtension(url);
        }

        for (var index = 0; index < tokens.Length; index++)
        {
            if (!IsPlayableToken(tokens[index], url))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPlayableToken(string token, string? url)
    {
        if (token.Equals(Ogg, StringComparison.OrdinalIgnoreCase))
        {
            return url is not null && url.Contains(OpusHint, StringComparison.OrdinalIgnoreCase);
        }

        for (var index = 0; index < DecodableCodecs.Length; index++)
        {
            if (token.Equals(DecodableCodecs[index], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasPlayableExtension(string? url)
    {
        if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var path = uri.AbsolutePath;
        for (var index = 0; index < PlayableExtensions.Length; index++)
        {
            if (path.EndsWith(PlayableExtensions[index], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
