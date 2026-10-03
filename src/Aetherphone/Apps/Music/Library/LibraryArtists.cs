using Aetherphone.Core.Songs;

namespace Aetherphone.Apps.Music.Library;

internal readonly struct ArtistEntry
{
    public readonly string ChannelId;
    public readonly string Name;
    public readonly string ThumbnailUrl;
    public readonly int SongCount;
    public readonly bool Followed;

    public ArtistEntry(string channelId, string name, string thumbnailUrl, int songCount, bool followed)
    {
        ChannelId = channelId;
        Name = name;
        ThumbnailUrl = thumbnailUrl;
        SongCount = songCount;
        Followed = followed;
    }

    public ArtistEntry WithSong(string thumbnailUrl) =>
        new(ChannelId, Name, ThumbnailUrl.Length > 0 ? ThumbnailUrl : thumbnailUrl, SongCount + 1, Followed);
}

internal static class LibraryArtists
{
    public static ArtistEntry[] Build(IReadOnlyList<ArtistRecord> followed, IReadOnlyList<SongRecord> songs)
    {
        var entries = new List<ArtistEntry>(followed.Count + songs.Count / 2);
        var byKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < followed.Count; index++)
        {
            var artist = followed[index];
            if (string.IsNullOrEmpty(artist.ChannelId) || byKey.ContainsKey(artist.ChannelId))
            {
                continue;
            }

            byKey[artist.ChannelId] = entries.Count;
            entries.Add(new ArtistEntry(artist.ChannelId, artist.Name, artist.ThumbnailUrl, 0, true));
        }

        for (var index = 0; index < songs.Count; index++)
        {
            var song = songs[index];
            var key = KeyOf(song.ChannelId, song.Author);
            if (key.Length == 0)
            {
                continue;
            }

            if (byKey.TryGetValue(key, out var slot))
            {
                entries[slot] = entries[slot].WithSong(song.ThumbnailUrl);
                continue;
            }

            byKey[key] = entries.Count;
            entries.Add(new ArtistEntry(song.ChannelId, song.Author, song.ThumbnailUrl, 1, false));
        }

        var result = entries.ToArray();
        Array.Sort(result, static (left, right) =>
            string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase));
        return result;
    }

    public static bool Matches(in Song song, string channelId, string name)
    {
        if (channelId.Length > 0)
        {
            return string.Equals(song.ChannelId, channelId, StringComparison.Ordinal);
        }

        return song.ChannelId.Length == 0 && string.Equals(song.Author, name, StringComparison.OrdinalIgnoreCase);
    }

    public static Song[] Filter(ReadOnlySpan<Song> songs, string channelId, string name)
    {
        var matched = new List<Song>();
        for (var index = 0; index < songs.Length; index++)
        {
            if (Matches(songs[index], channelId, name))
            {
                matched.Add(songs[index]);
            }
        }

        return matched.ToArray();
    }

    private static string KeyOf(string channelId, string author)
    {
        if (!string.IsNullOrEmpty(channelId))
        {
            return channelId;
        }

        return string.IsNullOrWhiteSpace(author) ? string.Empty : "author:" + author.Trim();
    }
}
