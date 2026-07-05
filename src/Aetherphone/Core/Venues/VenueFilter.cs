namespace Aetherphone.Core.Venues;

internal static class VenueFilter
{
    public const int SourceAll = 0;
    public const int SourceFfxiv = 1;
    public const int SourcePartake = 2;
    private const int UpcomingWindowDays = 14;

    public static void Apply(IReadOnlyList<VenueEvent> source, List<VenueEvent> into, VenueTimeFilter time,
        int sourceFilter, string dataCenter, bool favoritesOnly, IReadOnlyList<string> favorites,
        IReadOnlyList<string> selectedTags, string search, DateTime nowUtc)
    {
        into.Clear();
        var query = search.Trim();
        for (var index = 0; index < source.Count; index++)
        {
            var venue = source[index];
            if (!MatchesSource(venue, sourceFilter))
            {
                continue;
            }

            if (dataCenter.Length > 0 &&
                !string.Equals(venue.DataCenter, dataCenter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!MatchesTime(venue, time, nowUtc))
            {
                continue;
            }

            if (favoritesOnly && !Contains(favorites, venue.Id))
            {
                continue;
            }

            if (!MatchesTags(venue, selectedTags))
            {
                continue;
            }

            if (query.Length > 0 && !MatchesSearch(venue, query))
            {
                continue;
            }

            into.Add(venue);
        }
    }

    public static void CollectTags(IReadOnlyList<VenueEvent> source, int sourceFilter, string dataCenter,
        SortedSet<string> into)
    {
        into.Clear();
        for (var index = 0; index < source.Count; index++)
        {
            var venue = source[index];
            if (!MatchesSource(venue, sourceFilter))
            {
                continue;
            }

            if (dataCenter.Length > 0 &&
                !string.Equals(venue.DataCenter, dataCenter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            for (var tagIndex = 0; tagIndex < venue.Tags.Count; tagIndex++)
            {
                into.Add(venue.Tags[tagIndex]);
            }
        }
    }

    private static bool MatchesSource(VenueEvent venue, int sourceFilter)
    {
        return sourceFilter switch
        {
            SourceFfxiv => venue.Source == VenueSource.FfxivVenues,
            SourcePartake => venue.Source == VenueSource.Partake,
            _ => true,
        };
    }

    private static bool MatchesTime(VenueEvent venue, VenueTimeFilter time, DateTime nowUtc)
    {
        switch (time)
        {
            case VenueTimeFilter.LiveNow:
                return venue.IsLive(nowUtc);
            case VenueTimeFilter.Upcoming:
                return venue.StartUtc > nowUtc && venue.StartUtc <= nowUtc.AddDays(UpcomingWindowDays);
            case VenueTimeFilter.Today:
                return MatchesToday(venue);
            default:
                return true;
        }
    }

    private static bool MatchesToday(VenueEvent venue)
    {
        var today = DateTime.Now.Date;
        var startDate = venue.StartUtc.ToLocalTime().Date;
        if (startDate == today)
        {
            return true;
        }

        if (venue.EndUtc is { } end)
        {
            var endDate = end.ToLocalTime().Date;
            return startDate <= today && endDate >= today;
        }

        return false;
    }

    private static bool MatchesTags(VenueEvent venue, IReadOnlyList<string> selectedTags)
    {
        for (var index = 0; index < selectedTags.Count; index++)
        {
            if (!Contains(venue.Tags, selectedTags[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesSearch(VenueEvent venue, string query)
    {
        if (Found(venue.Title, query) || Found(venue.Host, query) || Found(venue.LocationLine, query) ||
            Found(venue.World, query) || Found(venue.DataCenter, query))
        {
            return true;
        }

        for (var index = 0; index < venue.Tags.Count; index++)
        {
            if (Found(venue.Tags[index], query))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Found(string value, string query) =>
        value.Length > 0 && value.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static bool Contains(IReadOnlyList<string> values, string target)
    {
        for (var index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], target, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
