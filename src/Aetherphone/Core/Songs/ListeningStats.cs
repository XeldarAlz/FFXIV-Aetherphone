namespace Aetherphone.Core.Songs;

internal enum ReplayPeriod : byte
{
    Week,
    Month,
    Year,
    AllTime,
}

internal enum ReplayBarUnit : byte
{
    Day,
    Month,
}

internal readonly record struct ReplaySong(Song Song, int Plays, long Seconds);

internal readonly record struct ReplayArtist(string Name, string ChannelId, string ThumbnailUrl, int Plays,
    long Seconds);

internal readonly record struct ReplayBar(DateOnly Start, long Seconds);

internal sealed class ListeningSummary
{
    public static readonly ListeningSummary Empty = new(ReplayPeriod.Week, 0, 0, Array.Empty<ReplaySong>(),
        Array.Empty<ReplayArtist>(), Array.Empty<ReplayBar>(), ReplayBarUnit.Day);

    public ListeningSummary(ReplayPeriod period, long totalSeconds, int totalPlays, ReplaySong[] topSongs,
        ReplayArtist[] topArtists, ReplayBar[] bars, ReplayBarUnit unit)
    {
        Period = period;
        TotalSeconds = totalSeconds;
        TotalPlays = totalPlays;
        TopSongs = topSongs;
        TopArtists = topArtists;
        Bars = bars;
        Unit = unit;
        for (var index = 0; index < bars.Length; index++)
        {
            PeakBarSeconds = Math.Max(PeakBarSeconds, bars[index].Seconds);
        }
    }

    public ReplayPeriod Period { get; }
    public long TotalSeconds { get; }
    public int TotalPlays { get; }
    public ReplaySong[] TopSongs { get; }
    public ReplayArtist[] TopArtists { get; }
    public ReplayBar[] Bars { get; }
    public ReplayBarUnit Unit { get; }
    public long PeakBarSeconds { get; }
    public bool IsEmpty => TotalSeconds <= 0 && TotalPlays <= 0 && TopSongs.Length == 0;
}

internal static class ListeningStats
{
    public const int MonthsInYear = 12;
    private const int DaysInWeek = 7;

    public static (int First, int Last) Range(ReplayPeriod period, DateOnly today)
    {
        var last = today.DayNumber;
        return period switch
        {
            ReplayPeriod.Week => (last - ((int)today.DayOfWeek + 6) % DaysInWeek, last),
            ReplayPeriod.Month => (new DateOnly(today.Year, today.Month, 1).DayNumber, last),
            ReplayPeriod.Year => (new DateOnly(today.Year, 1, 1).DayNumber, last),
            _ => (int.MinValue, last),
        };
    }

    public static ListeningSummary Build(IReadOnlyList<PlayRecord> plays, IReadOnlyList<ListeningDay> days,
        long allTimeSeconds, ReplayPeriod period, DateOnly today, int topCount)
    {
        var (first, last) = Range(period, today);
        var allTime = period == ReplayPeriod.AllTime;
        var songs = new List<ReplaySong>(plays.Count);
        var totalPlays = 0;
        for (var index = 0; index < plays.Count; index++)
        {
            var record = plays[index];
            var (songPlays, songSeconds) = allTime
                ? (record.Count, record.ListenedSeconds)
                : Sum(record.Days, first, last);
            totalPlays += allTime ? songPlays : 0;
            if (songPlays <= 0 && songSeconds <= 0)
            {
                continue;
            }

            songs.Add(new ReplaySong(record.Song.ToSong(), songPlays, songSeconds));
        }

        var (periodPlays, periodSeconds) = Sum(days, first, last);
        var totalSeconds = allTime ? Math.Max(allTimeSeconds, periodSeconds) : periodSeconds;
        if (!allTime)
        {
            totalPlays = periodPlays;
        }

        var artists = GroupArtists(songs, topCount);
        songs.Sort(CompareSongs);
        if (songs.Count > topCount)
        {
            songs.RemoveRange(topCount, songs.Count - topCount);
        }

        var unit = period is ReplayPeriod.Week or ReplayPeriod.Month ? ReplayBarUnit.Day : ReplayBarUnit.Month;
        return new ListeningSummary(period, totalSeconds, totalPlays, songs.ToArray(), artists,
            Bars(days, period, today), unit);
    }

    public static string ArtistKey(string channelId, string author)
    {
        return channelId.Length > 0 ? channelId : author.Trim().ToLowerInvariant();
    }

    public static ReplayBar[] Bars(IReadOnlyList<ListeningDay> days, ReplayPeriod period, DateOnly today)
    {
        switch (period)
        {
            case ReplayPeriod.Week:
            {
                var monday = DateOnly.FromDayNumber(Range(period, today).First);
                return DayBars(days, monday, DaysInWeek);
            }
            case ReplayPeriod.Month:
                return DayBars(days, new DateOnly(today.Year, today.Month, 1),
                    DateTime.DaysInMonth(today.Year, today.Month));
            case ReplayPeriod.Year:
                return MonthBars(days, new DateOnly(today.Year, 1, 1));
            default:
                return MonthBars(days, new DateOnly(today.Year, today.Month, 1).AddMonths(1 - MonthsInYear));
        }
    }

    public static void Add(List<ListeningDay> days, int day, int plays, int seconds)
    {
        var insertAt = days.Count;
        for (var index = days.Count - 1; index >= 0; index--)
        {
            var entry = days[index];
            if (entry.Day == day)
            {
                entry.Plays += plays;
                entry.Seconds += seconds;
                days[index] = entry;
                return;
            }

            if (entry.Day < day)
            {
                break;
            }

            insertAt = index;
        }

        days.Insert(insertAt, new ListeningDay { Day = day, Plays = plays, Seconds = seconds });
    }

    public static void Prune(List<ListeningDay> days, int oldestDay)
    {
        var stale = 0;
        while (stale < days.Count && days[stale].Day < oldestDay)
        {
            stale++;
        }

        if (stale > 0)
        {
            days.RemoveRange(0, stale);
        }
    }

    private static (int Plays, long Seconds) Sum(IReadOnlyList<ListeningDay>? days, int first, int last)
    {
        if (days is null)
        {
            return (0, 0);
        }

        var plays = 0;
        var seconds = 0L;
        for (var index = 0; index < days.Count; index++)
        {
            var entry = days[index];
            if (entry.Day < first || entry.Day > last)
            {
                continue;
            }

            plays += entry.Plays;
            seconds += entry.Seconds;
        }

        return (plays, seconds);
    }

    private static ReplayArtist[] GroupArtists(List<ReplaySong> songs, int topCount)
    {
        var order = new List<ReplayArtist>();
        var indexByKey = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < songs.Count; index++)
        {
            var entry = songs[index];
            var song = entry.Song;
            var key = ArtistKey(song.ChannelId ?? string.Empty, song.Author ?? string.Empty);
            if (key.Length == 0)
            {
                continue;
            }

            if (indexByKey.TryGetValue(key, out var existing))
            {
                var artist = order[existing];
                order[existing] = artist with
                {
                    Plays = artist.Plays + entry.Plays,
                    Seconds = artist.Seconds + entry.Seconds,
                };
                continue;
            }

            indexByKey[key] = order.Count;
            order.Add(new ReplayArtist(song.Author ?? string.Empty, song.ChannelId ?? string.Empty,
                song.ThumbnailUrl ?? string.Empty, entry.Plays, entry.Seconds));
        }

        order.Sort(static (left, right) =>
        {
            var byPlays = right.Plays.CompareTo(left.Plays);
            return byPlays != 0 ? byPlays : right.Seconds.CompareTo(left.Seconds);
        });
        if (order.Count > topCount)
        {
            order.RemoveRange(topCount, order.Count - topCount);
        }

        return order.ToArray();
    }

    private static int CompareSongs(ReplaySong left, ReplaySong right)
    {
        var byPlays = right.Plays.CompareTo(left.Plays);
        return byPlays != 0 ? byPlays : right.Seconds.CompareTo(left.Seconds);
    }

    private static ReplayBar[] DayBars(IReadOnlyList<ListeningDay> days, DateOnly start, int count)
    {
        var bars = new ReplayBar[count];
        var firstDay = start.DayNumber;
        var seconds = new long[count];
        for (var index = 0; index < days.Count; index++)
        {
            var offset = days[index].Day - firstDay;
            if ((uint)offset < (uint)count)
            {
                seconds[offset] += days[index].Seconds;
            }
        }

        for (var index = 0; index < count; index++)
        {
            bars[index] = new ReplayBar(start.AddDays(index), seconds[index]);
        }

        return bars;
    }

    private static ReplayBar[] MonthBars(IReadOnlyList<ListeningDay> days, DateOnly firstMonth)
    {
        var bars = new ReplayBar[MonthsInYear];
        var seconds = new long[MonthsInYear];
        var origin = firstMonth.Year * MonthsInYear + firstMonth.Month - 1;
        for (var index = 0; index < days.Count; index++)
        {
            var date = DateOnly.FromDayNumber(days[index].Day);
            var offset = date.Year * MonthsInYear + date.Month - 1 - origin;
            if ((uint)offset < MonthsInYear)
            {
                seconds[offset] += days[index].Seconds;
            }
        }

        for (var index = 0; index < MonthsInYear; index++)
        {
            bars[index] = new ReplayBar(firstMonth.AddMonths(index), seconds[index]);
        }

        return bars;
    }
}
