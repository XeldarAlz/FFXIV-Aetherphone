namespace Aetherphone.Core.Video;

internal sealed class VideoLibrary
{
    internal const int MaxHistory = 50;
    internal const int MaxPlaylists = 30;
    internal const int MaxPlaylistNameLength = 60;

    private const double ResumeEdgeSeconds = 30d;

    private readonly Configuration configuration;

    internal VideoLibrary(Configuration configuration)
    {
        this.configuration = configuration;
    }

    internal IReadOnlyList<VideoHistoryRecord> History => configuration.VideoHistory;

    internal IReadOnlyList<VideoPlaylistRecord> Playlists => configuration.VideoPlaylists;

    internal static bool Remembers(string url) => url.Length > 0 && !LocalMediaToken.IsToken(url);

    internal void NotePlayed(VideoQueueEntry entry)
    {
        if (!Remembers(entry.Url))
        {
            return;
        }

        var history = configuration.VideoHistory;
        var position = 0d;
        for (var index = history.Count - 1; index >= 0; index--)
        {
            if (string.Equals(history[index].Url, entry.Url, StringComparison.Ordinal))
            {
                position = history[index].PositionSeconds;
                history.RemoveAt(index);
            }
        }

        history.Insert(0, new VideoHistoryRecord
        {
            Url = entry.Url,
            Title = entry.Title,
            Source = entry.Source,
            DurationSeconds = entry.Duration?.TotalSeconds,
            ThumbnailUrl = entry.ThumbnailUrl,
            PositionSeconds = position,
            WatchedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        });
        while (history.Count > MaxHistory)
        {
            history.RemoveAt(history.Count - 1);
        }
    }

    internal void NoteDetails(VideoQueueEntry entry)
    {
        var record = Find(entry.Url);
        if (record is null)
        {
            return;
        }

        record.Title = entry.Title;
        record.Source = entry.Source;
        record.DurationSeconds = entry.Duration?.TotalSeconds;
        record.ThumbnailUrl = entry.ThumbnailUrl;
        record.SubtitleCache = null;
    }

    internal static string Subtitle(VideoHistoryRecord record) =>
        record.SubtitleCache ??= MediaInput.Subtitle(record.Source, record.DurationSeconds);

    internal void NotePosition(string url, double positionSeconds, double durationSeconds)
    {
        var record = Find(url);
        if (record is null)
        {
            return;
        }

        record.PositionSeconds = ResumeFrom(positionSeconds, durationSeconds);
        if (durationSeconds > 0d && record.DurationSeconds is null)
        {
            record.DurationSeconds = durationSeconds;
        }
    }

    internal void NoteFinished(string url)
    {
        var record = Find(url);
        if (record is not null)
        {
            record.PositionSeconds = 0d;
        }
    }

    internal static double ResumeFrom(double positionSeconds, double durationSeconds)
    {
        if (positionSeconds < ResumeEdgeSeconds)
        {
            return 0d;
        }

        if (durationSeconds > 0d && positionSeconds > durationSeconds - ResumeEdgeSeconds)
        {
            return 0d;
        }

        return positionSeconds;
    }

    internal void RemoveHistory(VideoHistoryRecord record)
    {
        configuration.VideoHistory.Remove(record);
        configuration.Save();
    }

    internal void ClearHistory()
    {
        configuration.VideoHistory.Clear();
        configuration.Save();
    }

    internal VideoPlaylistRecord? SavePlaylist(string name, string sourceUrl, IReadOnlyList<VideoQueueRecord> entries)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0 || entries.Count == 0)
        {
            return null;
        }

        if (trimmed.Length > MaxPlaylistNameLength)
        {
            trimmed = trimmed[..MaxPlaylistNameLength];
        }

        var playlists = configuration.VideoPlaylists;
        for (var index = playlists.Count - 1; index >= 0; index--)
        {
            var replaces = sourceUrl.Length > 0
                ? string.Equals(playlists[index].SourceUrl, sourceUrl, StringComparison.Ordinal)
                : playlists[index].SourceUrl.Length == 0
                    && string.Equals(playlists[index].Name, trimmed, StringComparison.OrdinalIgnoreCase);
            if (replaces)
            {
                playlists.RemoveAt(index);
            }
        }

        var playlist = new VideoPlaylistRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = trimmed,
            SourceUrl = sourceUrl,
            Entries = new List<VideoQueueRecord>(entries),
        };
        playlists.Insert(0, playlist);
        while (playlists.Count > MaxPlaylists)
        {
            playlists.RemoveAt(playlists.Count - 1);
        }

        configuration.Save();
        return playlist;
    }

    internal string NextPlaylistName(string format, IFormatProvider culture)
    {
        var playlists = configuration.VideoPlaylists;
        for (var number = playlists.Count + 1;; number++)
        {
            var name = string.Format(culture, format, number);
            var taken = false;
            for (var index = 0; index < playlists.Count && !taken; index++)
            {
                taken = string.Equals(playlists[index].Name, name, StringComparison.OrdinalIgnoreCase);
            }

            if (!taken)
            {
                return name;
            }
        }
    }

    internal void RemovePlaylist(VideoPlaylistRecord playlist)
    {
        configuration.VideoPlaylists.Remove(playlist);
        configuration.Save();
    }

    private VideoHistoryRecord? Find(string url)
    {
        var history = configuration.VideoHistory;
        for (var index = 0; index < history.Count; index++)
        {
            if (string.Equals(history[index].Url, url, StringComparison.Ordinal))
            {
                return history[index];
            }
        }

        return null;
    }
}
