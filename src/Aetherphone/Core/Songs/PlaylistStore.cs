namespace Aetherphone.Core.Songs;

internal sealed class PlaylistStore
{
    private const int NameLimit = 60;
    private readonly Configuration configuration;

    public PlaylistStore(Configuration configuration)
    {
        this.configuration = configuration;
    }

    public IReadOnlyList<PlaylistRecord> All => configuration.Playlists;

    public int Count => configuration.Playlists.Count;

    public PlaylistRecord? Find(string id)
    {
        var list = configuration.Playlists;
        for (var index = 0; index < list.Count; index++)
        {
            if (string.Equals(list[index].Id, id, StringComparison.Ordinal))
            {
                return list[index];
            }
        }

        return null;
    }

    public int SongCount(string id)
    {
        return Find(id) is { } record ? record.Songs.Count : 0;
    }

    public string Create(string name)
    {
        var record = new PlaylistRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = Sanitize(name),
            CreatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };
        configuration.Playlists.Insert(0, record);
        configuration.Save();
        return record.Id;
    }

    public void Rename(string id, string name)
    {
        if (Find(id) is not { } record)
        {
            return;
        }

        record.Name = Sanitize(name);
        configuration.Save();
    }

    public void Delete(string id)
    {
        configuration.Playlists.RemoveAll(record => string.Equals(record.Id, id, StringComparison.Ordinal));
        configuration.Save();
    }

    public bool Contains(string id, string videoId)
    {
        if (Find(id) is not { } record)
        {
            return false;
        }

        for (var index = 0; index < record.Songs.Count; index++)
        {
            if (string.Equals(record.Songs[index].VideoId, videoId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public void Add(string id, in Song song)
    {
        if (string.IsNullOrEmpty(song.VideoId) || Find(id) is not { } record)
        {
            return;
        }

        for (var index = 0; index < record.Songs.Count; index++)
        {
            if (string.Equals(record.Songs[index].VideoId, song.VideoId, StringComparison.Ordinal))
            {
                return;
            }
        }

        record.Songs.Add(SongRecord.From(song));
        configuration.Save();
    }

    public void Remove(string id, string videoId)
    {
        if (Find(id) is not { } record)
        {
            return;
        }

        record.Songs.RemoveAll(entry => string.Equals(entry.VideoId, videoId, StringComparison.Ordinal));
        configuration.Save();
    }

    public Song[] Songs(string id)
    {
        if (Find(id) is not { } record || record.Songs.Count == 0)
        {
            return Array.Empty<Song>();
        }

        var songs = new Song[record.Songs.Count];
        for (var index = 0; index < record.Songs.Count; index++)
        {
            songs[index] = record.Songs[index].ToSong();
        }

        return songs;
    }

    private static string Sanitize(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length > NameLimit)
        {
            trimmed = trimmed[..NameLimit];
        }

        return trimmed;
    }
}
