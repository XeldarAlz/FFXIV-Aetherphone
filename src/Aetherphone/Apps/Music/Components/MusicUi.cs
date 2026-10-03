using Aetherphone.Core.Localization;
using Aetherphone.Core.Songs;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Music.Components;

internal static class MusicUi
{
    public const float Inset = FeedCell.PadX;
    public const float SectionGap = 22f;
    private const int SecondsPerMinute = 60;
    private const int MinutesPerHour = 60;
    private const int CacheLimit = 512;

    private static readonly Dictionary<int, string> SongCounts = new();
    private static readonly Dictionary<int, string> LongDurations = new();
    private static readonly Dictionary<int, string> Remainders = new();
    private static string cacheLanguage = string.Empty;

    public static string Duration(int seconds) => TimeText.Duration(seconds);

    public static string Remaining(int seconds)
    {
        var clamped = Math.Max(0, seconds);
        if (Remainders.TryGetValue(clamped, out var cached))
        {
            return cached;
        }

        var text = string.Concat("-", TimeText.Duration(clamped));
        Remember(Remainders, clamped, text);
        return text;
    }

    public static string SongCount(int count)
    {
        EnsureLanguage();
        if (SongCounts.TryGetValue(count, out var cached))
        {
            return cached;
        }

        var text = count == 1 ? Loc.T(L.Music.SongOne) : string.Format(Loc.Culture, Loc.T(L.Music.SongsMany), count);
        Remember(SongCounts, count, text);
        return text;
    }

    public static string LongDuration(int totalSeconds)
    {
        EnsureLanguage();
        var minutes = Math.Max(0, totalSeconds) / SecondsPerMinute;
        if (LongDurations.TryGetValue(minutes, out var cached))
        {
            return cached;
        }

        var text = minutes >= MinutesPerHour
            ? string.Format(Loc.Culture, Loc.T(L.Music.DurationHoursMinutes), minutes / MinutesPerHour,
                minutes % MinutesPerHour)
            : string.Format(Loc.Culture, Loc.T(L.Music.DurationMinutes), Math.Max(1, minutes));
        Remember(LongDurations, minutes, text);
        return text;
    }

    public static int TotalSeconds(ReadOnlySpan<Song> songs)
    {
        var total = 0;
        for (var index = 0; index < songs.Length; index++)
        {
            total += Math.Max(0, songs[index].DurationSeconds);
        }

        return total;
    }

    private static void Remember(Dictionary<int, string> cache, int key, string text)
    {
        if (cache.Count >= CacheLimit)
        {
            cache.Clear();
        }

        cache[key] = text;
    }

    private static void EnsureLanguage()
    {
        var code = Loc.Current.Code;
        if (string.Equals(code, cacheLanguage, StringComparison.Ordinal))
        {
            return;
        }

        cacheLanguage = code;
        SongCounts.Clear();
        LongDurations.Clear();
    }
}
