using Aetherphone.Core.Songs;

namespace Aetherphone.Apps.Music.Library;

internal enum PlaylistSort : byte
{
    RecentlyUpdated,
    Title,
    RecentlyAdded,
}

internal enum SongSort : byte
{
    RecentlyAdded,
    Title,
    Artist,
}

internal static class LibrarySorting
{
    public static PlaylistRecord[] Playlists(IReadOnlyList<PlaylistRecord> source, PlaylistSort order)
    {
        var count = source.Count;
        var sorted = new PlaylistRecord[count];
        var positions = new int[count];
        for (var index = 0; index < count; index++)
        {
            sorted[index] = source[index];
            positions[index] = index;
        }

        Array.Sort(positions, (left, right) =>
        {
            var compared = ComparePlaylists(sorted[left], sorted[right], order);
            return compared != 0 ? compared : left.CompareTo(right);
        });
        return Reorder(sorted, positions);
    }

    public static Song[] Songs(Song[] source, SongSort order)
    {
        var count = source.Length;
        var sorted = new Song[count];
        Array.Copy(source, sorted, count);
        if (order == SongSort.RecentlyAdded || count < 2)
        {
            return sorted;
        }

        var positions = new int[count];
        for (var index = 0; index < count; index++)
        {
            positions[index] = index;
        }

        Array.Sort(positions, (left, right) =>
        {
            var compared = CompareSongs(source[left], source[right], order);
            return compared != 0 ? compared : left.CompareTo(right);
        });
        for (var index = 0; index < count; index++)
        {
            sorted[index] = source[positions[index]];
        }

        return sorted;
    }

    private static int ComparePlaylists(PlaylistRecord left, PlaylistRecord right, PlaylistSort order)
    {
        return order switch
        {
            PlaylistSort.Title => string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase),
            PlaylistSort.RecentlyAdded => right.CreatedUnix.CompareTo(left.CreatedUnix),
            _ => right.UpdatedUnix.CompareTo(left.UpdatedUnix),
        };
    }

    private static int CompareSongs(in Song left, in Song right, SongSort order)
    {
        if (order == SongSort.Artist)
        {
            var byArtist = string.Compare(left.Author, right.Author, StringComparison.CurrentCultureIgnoreCase);
            if (byArtist != 0)
            {
                return byArtist;
            }
        }

        return string.Compare(left.Title, right.Title, StringComparison.CurrentCultureIgnoreCase);
    }

    private static PlaylistRecord[] Reorder(PlaylistRecord[] items, int[] positions)
    {
        var result = new PlaylistRecord[items.Length];
        for (var index = 0; index < items.Length; index++)
        {
            result[index] = items[positions[index]];
        }

        return result;
    }
}
