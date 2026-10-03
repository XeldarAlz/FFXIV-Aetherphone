namespace Aetherphone.Core.Songs;

internal enum PlaylistLinkKind : byte
{
    None,
    Playlist,
    Mix,
    Channel,
    Video,
}

internal enum ImportPhase : byte
{
    Idle,
    Fetching,
    Ready,
    Failed,
}

internal enum ImportFailure : byte
{
    None,
    InvalidLink,
    NotInstalled,
    Unavailable,
    Empty,
}

internal readonly record struct PlaylistLink(PlaylistLinkKind Kind, string Url, string VideoId)
{
    public static readonly PlaylistLink Empty = new(PlaylistLinkKind.None, string.Empty, string.Empty);

    public bool IsValid => Kind != PlaylistLinkKind.None;

    public bool IsCollection => Kind is PlaylistLinkKind.Playlist or PlaylistLinkKind.Mix or PlaylistLinkKind.Channel;
}

internal sealed class ImportPreview
{
    public ImportPreview(PlaylistLink link, string title, string author, Song[] songs)
    {
        Link = link;
        Title = title;
        Author = author;
        Songs = songs;
    }

    public PlaylistLink Link { get; }

    public string Title { get; }

    public string Author { get; }

    public Song[] Songs { get; }

    public int TotalSeconds
    {
        get
        {
            var total = 0;
            for (var index = 0; index < Songs.Length; index++)
            {
                total += Math.Max(0, Songs[index].DurationSeconds);
            }

            return total;
        }
    }
}

internal sealed class PlaylistImporter : IDisposable
{
    public const int Limit = SongLinkResolver.PlaylistLimit;
    private const int VideoIdLength = 11;
    private const int ChannelIdLength = 24;
    private const int MinimumListIdLength = 2;
    private const string ChannelPrefix = "UC";
    private const string UploadsPrefix = "UU";
    private const string MixPrefix = "RD";
    private const string BrowsePlaylistPrefix = "VL";
    private const string WatchFormat = "https://www.youtube.com/watch?v={0}";
    private const string MixFormat = "https://www.youtube.com/watch?v={0}&list={1}";
    private const string PlaylistFormat = "https://www.youtube.com/playlist?list={0}";
    private const string ChannelVideosFormat = "https://www.youtube.com/{0}/videos";

    private readonly SongLinkResolver resolver;
    private readonly object gate = new();
    private CancellationTokenSource? cancellation;
    private volatile ImportPreview? preview;
    private volatile int phase;
    private volatile int failure;
    private int generation;

    public PlaylistImporter(SongLinkResolver resolver)
    {
        this.resolver = resolver;
    }

    public ImportPhase Phase => (ImportPhase)phase;

    public ImportFailure Failure => (ImportFailure)failure;

    public ImportPreview? Preview => preview;

    public PlaylistLink Link { get; private set; } = PlaylistLink.Empty;

    public bool Busy => Phase == ImportPhase.Fetching;

    public void Start(string text)
    {
        var link = Classify(text);
        Start(link);
    }

    public void Start(PlaylistLink link)
    {
        int ticket;
        CancellationToken token;
        lock (gate)
        {
            CancelRunning();
            Link = link;
            preview = null;
            ticket = ++generation;
            if (!link.IsValid)
            {
                Fail(ImportFailure.InvalidLink);
                return;
            }

            if (!resolver.IsInstalled)
            {
                Fail(ImportFailure.NotInstalled);
                return;
            }

            failure = (int)ImportFailure.None;
            phase = (int)ImportPhase.Fetching;
            cancellation = new CancellationTokenSource();
            token = cancellation.Token;
        }

        _ = Task.Run(() => Fetch(link, ticket, token), token);
    }

    public void Reset()
    {
        lock (gate)
        {
            CancelRunning();
            generation++;
            Link = PlaylistLink.Empty;
            preview = null;
            failure = (int)ImportFailure.None;
            phase = (int)ImportPhase.Idle;
        }
    }

    public void Dispose() => Reset();

    public static PlaylistLink Classify(string? text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0 || trimmed.Contains(' '))
        {
            return PlaylistLink.Empty;
        }

        if (!trimmed.Contains("://", StringComparison.Ordinal))
        {
            trimmed = "https://" + trimmed;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return PlaylistLink.Empty;
        }

        var host = NormalizeHost(uri.Host);
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var query = uri.Query;
        if (string.Equals(host, "youtu.be", StringComparison.Ordinal))
        {
            var shortId = segments.Length > 0 ? segments[0] : string.Empty;
            return FromWatch(IsVideoId(shortId) ? shortId : string.Empty, QueryValue(query, "list"));
        }

        if (!string.Equals(host, "youtube.com", StringComparison.Ordinal) &&
            !string.Equals(host, "music.youtube.com", StringComparison.Ordinal))
        {
            return PlaylistLink.Empty;
        }

        var videoId = QueryValue(query, "v");
        var listId = QueryValue(query, "list");
        if (segments.Length == 0)
        {
            return FromWatch(IsVideoId(videoId) ? videoId : string.Empty, listId);
        }

        var head = segments[0];
        if (string.Equals(head, "watch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(head, "playlist", StringComparison.OrdinalIgnoreCase))
        {
            return FromWatch(IsVideoId(videoId) ? videoId : string.Empty, listId);
        }

        var tail = segments.Length > 1 ? segments[1] : string.Empty;
        if (IsVideoPath(head) && IsVideoId(tail))
        {
            return FromWatch(tail, listId);
        }

        if (string.Equals(head, "browse", StringComparison.OrdinalIgnoreCase) &&
            tail.StartsWith(BrowsePlaylistPrefix, StringComparison.Ordinal))
        {
            return FromWatch(string.Empty, tail[BrowsePlaylistPrefix.Length..]);
        }

        if (string.Equals(head, "channel", StringComparison.OrdinalIgnoreCase) && IsChannelId(tail))
        {
            return new PlaylistLink(PlaylistLinkKind.Channel,
                string.Format(PlaylistFormat, UploadsPrefix + tail[ChannelPrefix.Length..]), string.Empty);
        }

        if (head.Length > 1 && head[0] == '@' && IsSlug(head.AsSpan(1)))
        {
            return new PlaylistLink(PlaylistLinkKind.Channel, string.Format(ChannelVideosFormat, head), string.Empty);
        }

        if ((string.Equals(head, "c", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(head, "user", StringComparison.OrdinalIgnoreCase)) && tail.Length > 0 && IsSlug(tail))
        {
            return new PlaylistLink(PlaylistLinkKind.Channel,
                string.Format(ChannelVideosFormat, head.ToLowerInvariant() + "/" + tail), string.Empty);
        }

        return PlaylistLink.Empty;
    }

    public static Song[] ToSongs(SongSearchEntry[] entries)
    {
        if (entries.Length == 0)
        {
            return Array.Empty<Song>();
        }

        var seen = new HashSet<string>(entries.Length, StringComparer.Ordinal);
        var songs = new List<Song>(entries.Length);
        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if (string.IsNullOrEmpty(entry.VideoId) || !seen.Add(entry.VideoId))
            {
                continue;
            }

            songs.Add(new Song(entry.VideoId, entry.Title, entry.Author, entry.ThumbnailUrl, entry.DurationSeconds,
                entry.ChannelId));
        }

        return songs.ToArray();
    }

    private void Fetch(PlaylistLink link, int ticket, CancellationToken token)
    {
        SongPlaylistResult? result;
        try
        {
            var limit = link.Kind == PlaylistLinkKind.Video ? 1 : Limit;
            result = resolver.FetchPlaylist(link.Url, limit, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Playlist import failed");
            result = null;
        }

        lock (gate)
        {
            if (ticket != generation || token.IsCancellationRequested)
            {
                return;
            }

            if (result is not { } fetched)
            {
                Fail(ImportFailure.Unavailable);
                return;
            }

            var songs = ToSongs(fetched.Entries);
            if (songs.Length == 0)
            {
                Fail(ImportFailure.Empty);
                return;
            }

            var title = fetched.Title;
            var author = fetched.Author;
            if (link.Kind == PlaylistLinkKind.Video)
            {
                title = songs[0].Title;
                author = songs[0].Author;
            }

            preview = new ImportPreview(link, title, author, songs);
            phase = (int)ImportPhase.Ready;
        }
    }

    private void Fail(ImportFailure reason)
    {
        failure = (int)reason;
        phase = (int)ImportPhase.Failed;
    }

    private void CancelRunning()
    {
        var running = cancellation;
        cancellation = null;
        if (running is null)
        {
            return;
        }

        running.Cancel();
        running.Dispose();
    }

    private static PlaylistLink FromWatch(string videoId, string listId)
    {
        if (IsListId(listId))
        {
            if (listId.StartsWith(MixPrefix, StringComparison.Ordinal) && videoId.Length > 0)
            {
                return new PlaylistLink(PlaylistLinkKind.Mix, string.Format(MixFormat, videoId, listId), videoId);
            }

            return new PlaylistLink(PlaylistLinkKind.Playlist, string.Format(PlaylistFormat, listId), videoId);
        }

        return videoId.Length > 0
            ? new PlaylistLink(PlaylistLinkKind.Video, string.Format(WatchFormat, videoId), videoId)
            : PlaylistLink.Empty;
    }

    private static string NormalizeHost(string host)
    {
        var lowered = host.ToLowerInvariant();
        if (lowered.StartsWith("www.", StringComparison.Ordinal))
        {
            return lowered[4..];
        }

        return lowered.StartsWith("m.", StringComparison.Ordinal) ? lowered[2..] : lowered;
    }

    private static bool IsVideoPath(string head)
    {
        return string.Equals(head, "shorts", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(head, "live", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(head, "embed", StringComparison.OrdinalIgnoreCase);
    }

    private static string QueryValue(string query, string key)
    {
        var span = query.AsSpan();
        if (span.Length > 0 && span[0] == '?')
        {
            span = span[1..];
        }

        while (span.Length > 0)
        {
            var separator = span.IndexOf('&');
            var pair = separator < 0 ? span : span[..separator];
            span = separator < 0 ? ReadOnlySpan<char>.Empty : span[(separator + 1)..];
            var equals = pair.IndexOf('=');
            if (equals <= 0 || !pair[..equals].Equals(key, StringComparison.Ordinal))
            {
                continue;
            }

            return Uri.UnescapeDataString(pair[(equals + 1)..].ToString());
        }

        return string.Empty;
    }

    internal static bool IsVideoId(string? value)
    {
        return value is { Length: VideoIdLength } && IsIdCharacters(value);
    }

    private static bool IsListId(string value)
    {
        return value.Length >= MinimumListIdLength && IsIdCharacters(value);
    }

    private static bool IsChannelId(string value)
    {
        return value.Length == ChannelIdLength && value.StartsWith(ChannelPrefix, StringComparison.Ordinal) &&
               IsIdCharacters(value);
    }

    private static bool IsIdCharacters(ReadOnlySpan<char> value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (!char.IsAsciiLetterOrDigit(character) && character != '-' && character != '_')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSlug(ReadOnlySpan<char> value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (!char.IsAsciiLetterOrDigit(character) && character != '-' && character != '_' && character != '.')
            {
                return false;
            }
        }

        return true;
    }
}
