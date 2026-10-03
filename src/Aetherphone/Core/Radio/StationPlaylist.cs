namespace Aetherphone.Core.Radio;

internal static class StationPlaylist
{
    public const int MaxTextBytes = 512 * 1024;
    private const string PlsHeader = "[playlist]";
    private const string PlsFileKey = "File";

    private static readonly string[] PlaylistContentTypes =
    {
        "mpegurl", "scpls", "pls+xml", "x-mpegurl",
    };

    private static readonly string[] PlaylistExtensions = { ".m3u8", ".m3u", ".pls" };

    // A playlist is text and audio is not, so the head settles it once a label or an extension has
    // raised the question; a body that opens with a playlist header needs no label at all.
    public static bool LooksLikePlaylist(string? contentType, Uri uri, ReadOnlySpan<byte> head)
    {
        var text = TextStart(head);
        if (StartsWithIgnoreCase(text, "#EXTM3U") || StartsWithIgnoreCase(text, PlsHeader))
        {
            return true;
        }

        if (!IsPlaylistContentType(contentType) && !HasPlaylistExtension(uri))
        {
            return false;
        }

        return text.Length > 0 && (text[0] == '#' || StartsWithIgnoreCase(text, "http"));
    }

    public static bool IsPlaylistContentType(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            return false;
        }

        for (var index = 0; index < PlaylistContentTypes.Length; index++)
        {
            if (contentType.Contains(PlaylistContentTypes[index], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool HasPlaylistExtension(Uri uri)
    {
        var path = uri.AbsolutePath;
        for (var index = 0; index < PlaylistExtensions.Length; index++)
        {
            if (path.EndsWith(PlaylistExtensions[index], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool TryFirstEntry(string text, Uri baseUri, out Uri entry)
    {
        entry = baseUri;
        var trimmed = text.AsSpan().TrimStart(HlsPlaylist.ByteOrderMark).TrimStart();
        return trimmed.StartsWith(PlsHeader, StringComparison.OrdinalIgnoreCase)
            ? TryFirstPlsEntry(text, baseUri, out entry)
            : TryFirstM3uEntry(text, baseUri, out entry);
    }

    private static bool TryFirstPlsEntry(string text, Uri baseUri, out Uri entry)
    {
        entry = baseUri;
        var bestNumber = int.MaxValue;
        var found = false;
        var lines = text.Split('\n');
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex].AsSpan().Trim();
            if (!line.StartsWith(PlsFileKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var equals = line.IndexOf('=');
            if (equals < 0 || !int.TryParse(line[PlsFileKey.Length..equals], out var number) || number >= bestNumber)
            {
                continue;
            }

            if (!HlsPlaylist.TryResolve(baseUri, line[(equals + 1)..].ToString(), out var candidate))
            {
                continue;
            }

            bestNumber = number;
            entry = candidate;
            found = true;
        }

        return found;
    }

    private static bool TryFirstM3uEntry(string text, Uri baseUri, out Uri entry)
    {
        entry = baseUri;
        var lines = text.Split('\n');
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex].Trim().TrimStart(HlsPlaylist.ByteOrderMark);
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            if (HlsPlaylist.TryResolve(baseUri, line, out entry))
            {
                return true;
            }
        }

        entry = baseUri;
        return false;
    }

    private static ReadOnlySpan<char> TextStart(ReadOnlySpan<byte> head)
    {
        var start = 0;
        if (head.Length >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF)
        {
            start = 3;
        }

        while (start < head.Length && head[start] is (byte)' ' or (byte)'\r' or (byte)'\n' or (byte)'\t')
        {
            start++;
        }

        var length = Math.Min(16, head.Length - start);
        var characters = new char[length];
        for (var index = 0; index < length; index++)
        {
            characters[index] = (char)head[start + index];
        }

        return characters;
    }

    private static bool StartsWithIgnoreCase(ReadOnlySpan<char> text, string prefix)
    {
        return text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
