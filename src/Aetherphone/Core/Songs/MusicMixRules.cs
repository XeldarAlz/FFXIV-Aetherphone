namespace Aetherphone.Core.Songs;

internal static class MusicMixRules
{
    public const long SecondsPerDay = 24 * 60 * 60;
    public const int BecauseWindow = 5;
    public const int RediscoverAgeDays = 30;

    public static long Day(long unixSeconds) => unixSeconds / SecondsPerDay;

    public static Song[] Favourites(ReadOnlySpan<Song> mostPlayed, ReadOnlySpan<Song> loved, int max)
    {
        if (max <= 0)
        {
            return Array.Empty<Song>();
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var picked = new List<Song>(Math.Min(max, mostPlayed.Length + loved.Length));
        var longest = Math.Max(mostPlayed.Length, loved.Length);
        for (var index = 0; index < longest && picked.Count < max; index++)
        {
            if (index < mostPlayed.Length)
            {
                AddUnique(picked, seen, mostPlayed[index], max);
            }

            if (index < loved.Length)
            {
                AddUnique(picked, seen, loved[index], max);
            }
        }

        return picked.ToArray();
    }

    public static Song[] Discovery(Song[][] mixes, HashSet<string> known, int max)
    {
        if (max <= 0 || mixes.Length == 0)
        {
            return Array.Empty<Song>();
        }

        var seen = new HashSet<string>(known, StringComparer.Ordinal);
        var picked = new List<Song>(max);
        var longest = 0;
        for (var mixIndex = 0; mixIndex < mixes.Length; mixIndex++)
        {
            longest = Math.Max(longest, mixes[mixIndex]?.Length ?? 0);
        }

        for (var position = 0; position < longest && picked.Count < max; position++)
        {
            for (var mixIndex = 0; mixIndex < mixes.Length && picked.Count < max; mixIndex++)
            {
                var source = mixes[mixIndex];
                if (source is null || position >= source.Length)
                {
                    continue;
                }

                AddUnique(picked, seen, source[position], max);
            }
        }

        return picked.ToArray();
    }

    public static string[] PickSeeds(ReadOnlySpan<Song> loved, ReadOnlySpan<Song> mostPlayed, int count, long day)
    {
        if (count <= 0)
        {
            return Array.Empty<string>();
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pool = new List<string>(loved.Length + mostPlayed.Length);
        for (var index = 0; index < loved.Length; index++)
        {
            if (!loved[index].IsEmpty && seen.Add(loved[index].VideoId))
            {
                pool.Add(loved[index].VideoId);
            }
        }

        for (var index = 0; index < mostPlayed.Length; index++)
        {
            if (!mostPlayed[index].IsEmpty && seen.Add(mostPlayed[index].VideoId))
            {
                pool.Add(mostPlayed[index].VideoId);
            }
        }

        if (pool.Count == 0)
        {
            return Array.Empty<string>();
        }

        var taken = Math.Min(count, pool.Count);
        var seeds = new string[taken];
        var start = (int)(Math.Abs(day) % pool.Count);
        for (var index = 0; index < taken; index++)
        {
            seeds[index] = pool[(start + index) % pool.Count];
        }

        return seeds;
    }

    public static Song BecauseSeed(ReadOnlySpan<Song> recent, long day)
    {
        var window = Math.Min(BecauseWindow, recent.Length);
        if (window == 0)
        {
            return default;
        }

        return recent[(int)(Math.Abs(day) % window)];
    }

    public static Song[] DailyShuffle(Song[] songs, long day)
    {
        var shuffled = (Song[])songs.Clone();
        var random = new Random(unchecked((int)day));
        for (var index = shuffled.Length - 1; index > 0; index--)
        {
            var swap = random.Next(index + 1);
            (shuffled[index], shuffled[swap]) = (shuffled[swap], shuffled[index]);
        }

        return shuffled;
    }

    public static string Signature(string[] seeds, long day)
    {
        return day.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + string.Join(',', seeds);
    }

    public static HashSet<string> KnownIds(ReadOnlySpan<Song> first, ReadOnlySpan<Song> second,
        ReadOnlySpan<Song> third)
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        AddIds(known, first);
        AddIds(known, second);
        AddIds(known, third);
        return known;
    }

    private static void AddIds(HashSet<string> known, ReadOnlySpan<Song> songs)
    {
        for (var index = 0; index < songs.Length; index++)
        {
            if (!songs[index].IsEmpty)
            {
                known.Add(songs[index].VideoId);
            }
        }
    }

    private static void AddUnique(List<Song> picked, HashSet<string> seen, in Song song, int max)
    {
        if (picked.Count >= max || song.IsEmpty || !seen.Add(song.VideoId))
        {
            return;
        }

        picked.Add(song);
    }
}
