using Newtonsoft.Json;

namespace Aetherphone.Core.Songs;

internal sealed class LibraryStore : IDisposable
{
    public const int NameLimit = 60;
    public const int DescriptionLimit = 300;
    public const int PlayHistoryCapacity = 500;
    public const int RecentSearchCapacity = 20;
    public const int ListeningHistoryDays = 400;
    public const int LoudnessCapacity = 4000;
    private const int LoudnessEvictBatch = 200;
    private const string FileName = "library.json";
    private const int SaveDelayMilliseconds = 750;

    private readonly string path;
    private readonly object gate = new();
    private readonly object writeGate = new();
    private readonly HashSet<string> libraryIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> lovedIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> downloadIds = new(StringComparer.Ordinal);
    private readonly Timer saveTimer;
    private readonly MusicLibraryData data;
    private bool dirty;

    public LibraryStore(DirectoryInfo root, Configuration? legacy = null)
    {
        if (!root.Exists)
        {
            root.Create();
        }

        path = Path.Combine(root.FullName, FileName);
        saveTimer = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
        data = Load(path) ?? new MusicLibraryData();
        if (legacy is not null && !File.Exists(path))
        {
            MigrateLegacy(legacy);
        }

        RebuildIndexes();
    }

    public int Version { get; private set; }

    public IReadOnlyList<PlaylistRecord> Playlists => data.Playlists;
    public IReadOnlyList<SongRecord> Songs => data.Songs;
    public IReadOnlyList<ArtistRecord> Artists => data.Artists;
    public IReadOnlyList<string> RecentSearches => data.RecentSearches;
    public int LovedCount => data.Loved.Count;
    public int DownloadCount => data.Downloads.Count;

    public bool InLibrary(string videoId) => libraryIds.Contains(videoId);
    public bool IsLoved(string videoId) => lovedIds.Contains(videoId);
    public bool IsDownloaded(string videoId) => downloadIds.Contains(videoId);

    public PlaylistRecord? FindPlaylist(string id)
    {
        var list = data.Playlists;
        for (var index = 0; index < list.Count; index++)
        {
            if (string.Equals(list[index].Id, id, StringComparison.Ordinal))
            {
                return list[index];
            }
        }

        return null;
    }

    public SongRecord? FindSong(string videoId)
    {
        var list = data.Songs;
        for (var index = 0; index < list.Count; index++)
        {
            if (string.Equals(list[index].VideoId, videoId, StringComparison.Ordinal))
            {
                return list[index];
            }
        }

        return null;
    }

    public string CreatePlaylist(string name, string description = "", string sourceUrl = "")
    {
        lock (gate)
        {
            var now = Now();
            var record = new PlaylistRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = Clip(name, NameLimit),
                Description = Clip(description, DescriptionLimit),
                SourceUrl = sourceUrl ?? string.Empty,
                CreatedUnix = now,
                UpdatedUnix = now,
            };
            data.Playlists.Insert(0, record);
            Touch();
            return record.Id;
        }
    }

    public void RenamePlaylist(string id, string name)
    {
        Edit(id, record => record.Name = Clip(name, NameLimit));
    }

    public void SetPlaylistDescription(string id, string description)
    {
        Edit(id, record => record.Description = Clip(description, DescriptionLimit));
    }

    public void SetPlaylistCover(string id, string coverPath)
    {
        Edit(id, record => record.CoverPath = coverPath ?? string.Empty);
    }

    public void DeletePlaylist(string id)
    {
        lock (gate)
        {
            var list = data.Playlists;
            for (var index = 0; index < list.Count; index++)
            {
                if (!string.Equals(list[index].Id, id, StringComparison.Ordinal))
                {
                    continue;
                }

                list.RemoveAt(index);
                Touch();
                return;
            }
        }
    }

    public void MovePlaylist(int fromIndex, int toIndex)
    {
        lock (gate)
        {
            if (MoveWithin(data.Playlists, fromIndex, toIndex))
            {
                Touch();
            }
        }
    }

    public bool PlaylistContains(string id, string videoId)
    {
        return FindPlaylist(id) is { } record && IndexOf(record.Songs, videoId) >= 0;
    }

    public bool AddToPlaylist(string id, in Song song)
    {
        lock (gate)
        {
            if (song.IsEmpty || FindPlaylist(id) is not { } record || IndexOf(record.Songs, song.VideoId) >= 0)
            {
                return false;
            }

            record.Songs.Add(SongRecord.From(song));
            record.UpdatedUnix = Now();
            Touch();
            return true;
        }
    }

    public int AddRangeToPlaylist(string id, ReadOnlySpan<Song> songs)
    {
        lock (gate)
        {
            if (FindPlaylist(id) is not { } record)
            {
                return 0;
            }

            var added = 0;
            for (var index = 0; index < songs.Length; index++)
            {
                if (songs[index].IsEmpty || IndexOf(record.Songs, songs[index].VideoId) >= 0)
                {
                    continue;
                }

                record.Songs.Add(SongRecord.From(songs[index]));
                added++;
            }

            if (added > 0)
            {
                record.UpdatedUnix = Now();
                Touch();
            }

            return added;
        }
    }

    public void RemoveFromPlaylist(string id, string videoId)
    {
        lock (gate)
        {
            if (FindPlaylist(id) is not { } record)
            {
                return;
            }

            var index = IndexOf(record.Songs, videoId);
            if (index < 0)
            {
                return;
            }

            record.Songs.RemoveAt(index);
            record.UpdatedUnix = Now();
            Touch();
        }
    }

    public void MoveInPlaylist(string id, int fromIndex, int toIndex)
    {
        lock (gate)
        {
            if (FindPlaylist(id) is not { } record || !MoveWithin(record.Songs, fromIndex, toIndex))
            {
                return;
            }

            record.UpdatedUnix = Now();
            Touch();
        }
    }

    public Song[] PlaylistSongs(string id)
    {
        return FindPlaylist(id) is { } record ? ToSongs(record.Songs) : Array.Empty<Song>();
    }

    public void AddToLibrary(in Song song)
    {
        lock (gate)
        {
            if (song.IsEmpty || !libraryIds.Add(song.VideoId))
            {
                return;
            }

            data.Songs.Insert(0, SongRecord.From(song));
            Touch();
        }
    }

    public void RemoveFromLibrary(string videoId)
    {
        lock (gate)
        {
            if (!libraryIds.Remove(videoId))
            {
                return;
            }

            var index = IndexOf(data.Songs, videoId);
            if (index >= 0)
            {
                data.Songs.RemoveAt(index);
            }

            if (lovedIds.Remove(videoId))
            {
                data.Loved.Remove(videoId);
            }

            Touch();
        }
    }

    public void SetLoved(in Song song, bool loved)
    {
        lock (gate)
        {
            if (song.IsEmpty)
            {
                return;
            }

            if (loved)
            {
                AddToLibrary(song);
                if (lovedIds.Add(song.VideoId))
                {
                    data.Loved.Insert(0, song.VideoId);
                    Touch();
                }

                return;
            }

            if (lovedIds.Remove(song.VideoId))
            {
                data.Loved.Remove(song.VideoId);
                Touch();
            }
        }
    }

    public Song[] LovedSongs()
    {
        var loved = data.Loved;
        var songs = new List<Song>(loved.Count);
        for (var index = 0; index < loved.Count; index++)
        {
            if (FindSong(loved[index]) is { } record)
            {
                songs.Add(record.ToSong());
            }
        }

        return songs.ToArray();
    }

    public Song[] LibrarySongs()
    {
        return ToSongs(data.Songs);
    }

    public void SetDownloaded(in Song song, bool downloaded)
    {
        lock (gate)
        {
            if (song.IsEmpty)
            {
                return;
            }

            if (downloaded)
            {
                AddToLibrary(song);
                if (downloadIds.Add(song.VideoId))
                {
                    data.Downloads.Insert(0, song.VideoId);
                    Touch();
                }

                return;
            }

            if (downloadIds.Remove(song.VideoId))
            {
                data.Downloads.Remove(song.VideoId);
                Touch();
            }
        }
    }

    public string[] DownloadedIds()
    {
        return data.Downloads.ToArray();
    }

    public bool FollowsArtist(string channelId)
    {
        return IndexOfArtist(channelId) >= 0;
    }

    public void SetFollowArtist(string channelId, string name, string thumbnailUrl, bool follow)
    {
        lock (gate)
        {
            if (string.IsNullOrEmpty(channelId))
            {
                return;
            }

            var index = IndexOfArtist(channelId);
            if (follow && index < 0)
            {
                data.Artists.Insert(0, new ArtistRecord
                {
                    ChannelId = channelId,
                    Name = Clip(name, NameLimit),
                    ThumbnailUrl = thumbnailUrl ?? string.Empty,
                    FollowedUnix = Now(),
                });
                Touch();
                return;
            }

            if (!follow && index >= 0)
            {
                data.Artists.RemoveAt(index);
                Touch();
            }
        }
    }

    public void RecordPlay(in Song song)
    {
        if (song.IsEmpty)
        {
            return;
        }

        lock (gate)
        {
            var plays = data.Plays;
            var now = Now();
            PlayRecord? record = null;
            for (var index = 0; index < plays.Count; index++)
            {
                if (!string.Equals(plays[index].Song.VideoId, song.VideoId, StringComparison.Ordinal))
                {
                    continue;
                }

                record = plays[index];
                plays.RemoveAt(index);
                break;
            }

            record ??= new PlayRecord { Song = SongRecord.From(song), FirstPlayedUnix = now };
            record.Count++;
            record.LastPlayedUnix = now;
            var today = Today();
            AddListening(record.Days, today, 1, 0);
            AddListening(data.Days, today, 1, 0);
            plays.Insert(0, record);
            while (plays.Count > PlayHistoryCapacity)
            {
                plays.RemoveAt(plays.Count - 1);
            }
        }

        Touch();
    }

    public void RecordListening(in Song song, int seconds)
    {
        if (song.IsEmpty || seconds <= 0)
        {
            return;
        }

        lock (gate)
        {
            var today = Today();
            data.ListenedSeconds += seconds;
            AddListening(data.Days, today, 0, seconds);
            var plays = data.Plays;
            for (var index = 0; index < plays.Count; index++)
            {
                if (!string.Equals(plays[index].Song.VideoId, song.VideoId, StringComparison.Ordinal))
                {
                    continue;
                }

                plays[index].ListenedSeconds += seconds;
                AddListening(plays[index].Days, today, 0, seconds);
                break;
            }
        }

        Touch();
    }

    public bool TryGetLoudnessGain(string videoId, out float decibels)
    {
        lock (gate)
        {
            return data.LoudnessGains.TryGetValue(videoId, out decibels);
        }
    }

    public void RecordLoudnessGain(string videoId, float decibels)
    {
        if (string.IsNullOrEmpty(videoId) || !float.IsFinite(decibels))
        {
            return;
        }

        lock (gate)
        {
            var gains = data.LoudnessGains;
            if (!gains.ContainsKey(videoId) && gains.Count >= LoudnessCapacity)
            {
                EvictLoudness(gains);
            }

            gains[videoId] = decibels;
            dirty = true;
        }

        saveTimer.Change(SaveDelayMilliseconds, Timeout.Infinite);
    }

    public ListeningSummary BuildListening(ReplayPeriod period, DateOnly today, int topCount)
    {
        lock (gate)
        {
            return ListeningStats.Build(data.Plays, data.Days, data.ListenedSeconds, period, today, topCount);
        }
    }

    public Song[] RecentlyPlayed(int max)
    {
        lock (gate)
        {
            var plays = data.Plays;
            var count = Math.Min(max, plays.Count);
            if (count <= 0)
            {
                return Array.Empty<Song>();
            }

            var songs = new Song[count];
            for (var index = 0; index < count; index++)
            {
                songs[index] = plays[index].Song.ToSong();
            }

            return songs;
        }
    }

    public Song[] MostPlayed(int max, long sinceUnix = 0)
    {
        PlayRecord[] snapshot;
        lock (gate)
        {
            snapshot = data.Plays.ToArray();
        }

        Array.Sort(snapshot, static (left, right) => right.Count.CompareTo(left.Count));
        var songs = new List<Song>(Math.Min(max, snapshot.Length));
        for (var index = 0; index < snapshot.Length && songs.Count < max; index++)
        {
            if (snapshot[index].LastPlayedUnix >= sinceUnix)
            {
                songs.Add(snapshot[index].Song.ToSong());
            }
        }

        return songs.ToArray();
    }

    public Song[] MostPlayedBefore(int max, long beforeUnix)
    {
        PlayRecord[] snapshot;
        lock (gate)
        {
            snapshot = data.Plays.ToArray();
        }

        Array.Sort(snapshot, static (left, right) => right.Count.CompareTo(left.Count));
        var songs = new List<Song>(Math.Min(max, snapshot.Length));
        for (var index = 0; index < snapshot.Length && songs.Count < max; index++)
        {
            if (snapshot[index].LastPlayedUnix < beforeUnix)
            {
                songs.Add(snapshot[index].Song.ToSong());
            }
        }

        return songs.ToArray();
    }

    public int PlayCount(string videoId)
    {
        lock (gate)
        {
            var plays = data.Plays;
            for (var index = 0; index < plays.Count; index++)
            {
                if (string.Equals(plays[index].Song.VideoId, videoId, StringComparison.Ordinal))
                {
                    return plays[index].Count;
                }
            }

            return 0;
        }
    }

    public void RecordSearch(string query)
    {
        lock (gate)
        {
            var trimmed = (query ?? string.Empty).Trim();
            if (trimmed.Length == 0)
            {
                return;
            }

            var searches = data.RecentSearches;
            for (var index = 0; index < searches.Count; index++)
            {
                if (string.Equals(searches[index], trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    searches.RemoveAt(index);
                    break;
                }
            }

            searches.Insert(0, Clip(trimmed, NameLimit * 2));
            while (searches.Count > RecentSearchCapacity)
            {
                searches.RemoveAt(searches.Count - 1);
            }

            Touch();
        }
    }

    public void ClearRecentSearches()
    {
        lock (gate)
        {
            if (data.RecentSearches.Count == 0)
            {
                return;
            }

            data.RecentSearches.Clear();
            Touch();
        }
    }

    public void Flush()
    {
        lock (writeGate)
        {
            string json;
            lock (gate)
            {
                if (!dirty)
                {
                    return;
                }

                dirty = false;
                json = JsonConvert.SerializeObject(data);
            }

            try
            {
                var temp = path + ".tmp";
                File.WriteAllText(temp, json);
                File.Move(temp, path, true);
            }
            catch (Exception exception)
            {
                AepLog.Warning(exception, "Music library write failed");
                lock (gate)
                {
                    dirty = true;
                }
            }
        }
    }

    public void Dispose()
    {
        saveTimer.Change(Timeout.Infinite, Timeout.Infinite);
        Flush();
        saveTimer.Dispose();
    }

    private void Edit(string id, Action<PlaylistRecord> change)
    {
        lock (gate)
        {
            if (FindPlaylist(id) is not { } record)
            {
                return;
            }

            change(record);
            record.UpdatedUnix = Now();
            Touch();
        }
    }

    private void Touch()
    {
        lock (gate)
        {
            dirty = true;
            Version++;
        }

        saveTimer.Change(SaveDelayMilliseconds, Timeout.Infinite);
    }

    private void MigrateLegacy(Configuration legacy)
    {
        var legacyPlaylists = legacy.Playlists;
        var legacyRecents = legacy.SongRecents;
        if (legacyPlaylists.Count == 0 && legacyRecents.Count == 0)
        {
            return;
        }

        for (var index = 0; index < legacyPlaylists.Count; index++)
        {
            var playlist = legacyPlaylists[index];
            if (playlist.UpdatedUnix == 0)
            {
                playlist.UpdatedUnix = playlist.CreatedUnix;
            }

            data.Playlists.Add(playlist);
        }

        var now = Now();
        for (var index = 0; index < legacyRecents.Count; index++)
        {
            data.Plays.Add(new PlayRecord
            {
                Song = legacyRecents[index],
                Count = 1,
                FirstPlayedUnix = now,
                LastPlayedUnix = now,
            });
        }

        dirty = true;
        Flush();
        if (dirty)
        {
            return;
        }

        legacyPlaylists.Clear();
        legacyRecents.Clear();
        legacy.Save();
    }

    private void RebuildIndexes()
    {
        data.Playlists ??= new List<PlaylistRecord>();
        data.Songs ??= new List<SongRecord>();
        data.Loved ??= new List<string>();
        data.Downloads ??= new List<string>();
        data.Plays ??= new List<PlayRecord>();
        data.Artists ??= new List<ArtistRecord>();
        data.RecentSearches ??= new List<string>();
        data.Days ??= new List<ListeningDay>();
        data.LoudnessGains ??= new Dictionary<string, float>();
        data.Playlists.RemoveAll(static record => record is null || string.IsNullOrEmpty(record.Id));
        data.Songs.RemoveAll(static record => record is null || string.IsNullOrEmpty(record.VideoId));
        data.Plays.RemoveAll(static record => record?.Song is null || string.IsNullOrEmpty(record.Song.VideoId));
        MigrateListening();
        libraryIds.Clear();
        lovedIds.Clear();
        downloadIds.Clear();
        for (var index = 0; index < data.Songs.Count; index++)
        {
            libraryIds.Add(data.Songs[index].VideoId);
        }

        for (var index = 0; index < data.Loved.Count; index++)
        {
            lovedIds.Add(data.Loved[index]);
        }

        for (var index = 0; index < data.Downloads.Count; index++)
        {
            downloadIds.Add(data.Downloads[index]);
        }
    }

    private void MigrateListening()
    {
        var oldest = Today() - ListeningHistoryDays;
        var plays = data.Plays;
        var estimate = data.Version < MusicLibraryData.CurrentVersion;
        var estimatedSeconds = 0L;
        for (var index = 0; index < plays.Count; index++)
        {
            var record = plays[index];
            record.Days ??= new List<ListeningDay>();
            ListeningStats.Prune(record.Days, oldest);
            if (!estimate || record.ListenedSeconds > 0)
            {
                continue;
            }

            record.ListenedSeconds = (long)record.Count * Math.Max(0, record.Song.DurationSeconds);
            estimatedSeconds += record.ListenedSeconds;
        }

        ListeningStats.Prune(data.Days, oldest);
        if (!estimate)
        {
            return;
        }

        data.ListenedSeconds += estimatedSeconds;
        data.Version = MusicLibraryData.CurrentVersion;
        dirty = true;
    }

    private static void EvictLoudness(Dictionary<string, float> gains)
    {
        var stale = new string[Math.Min(LoudnessEvictBatch, gains.Count)];
        var count = 0;
        foreach (var key in gains.Keys)
        {
            if (count == stale.Length)
            {
                break;
            }

            stale[count++] = key;
        }

        for (var index = 0; index < count; index++)
        {
            gains.Remove(stale[index]);
        }
    }

    private static void AddListening(List<ListeningDay> days, int today, int plays, int seconds)
    {
        ListeningStats.Add(days, today, plays, seconds);
        ListeningStats.Prune(days, today - ListeningHistoryDays);
    }

    private static int Today() => DateOnly.FromDateTime(DateTime.Now).DayNumber;

    private static MusicLibraryData? Load(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonConvert.DeserializeObject<MusicLibraryData>(File.ReadAllText(path));
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Music library load failed");
            return null;
        }
    }

    private int IndexOfArtist(string channelId)
    {
        var artists = data.Artists;
        for (var index = 0; index < artists.Count; index++)
        {
            if (string.Equals(artists[index].ChannelId, channelId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static int IndexOf(List<SongRecord> songs, string videoId)
    {
        for (var index = 0; index < songs.Count; index++)
        {
            if (string.Equals(songs[index].VideoId, videoId, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool MoveWithin<T>(List<T> list, int fromIndex, int toIndex)
    {
        if ((uint)fromIndex >= (uint)list.Count || fromIndex == toIndex)
        {
            return false;
        }

        var item = list[fromIndex];
        list.RemoveAt(fromIndex);
        list.Insert(Math.Clamp(toIndex, 0, list.Count), item);
        return true;
    }

    private static Song[] ToSongs(List<SongRecord> records)
    {
        if (records.Count == 0)
        {
            return Array.Empty<Song>();
        }

        var songs = new Song[records.Count];
        for (var index = 0; index < records.Count; index++)
        {
            songs[index] = records[index].ToSong();
        }

        return songs;
    }

    private static string Clip(string? text, int limit)
    {
        var trimmed = (text ?? string.Empty).Trim();
        return trimmed.Length > limit ? trimmed[..limit] : trimmed;
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
