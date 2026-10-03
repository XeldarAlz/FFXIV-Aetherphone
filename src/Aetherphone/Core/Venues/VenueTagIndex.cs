namespace Aetherphone.Core.Venues;

internal readonly record struct VenueTagIndexKey(int DataVersion, int Source, IReadOnlySet<string>? DataCenters,
    string World, bool HideAdult, int TagsStamp);

internal readonly record struct VenueTagCount(string Tag, int Count);

internal sealed class VenueTagIndex
{
    private readonly Dictionary<string, int> counts = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<VenueTagCount> entries = new();
    private readonly EntryOrder order = new();
    private VenueTagIndexKey key;
    private bool built;

    public IReadOnlyList<VenueTagCount> Entries => entries;
    public int MatchCount { get; private set; }

    public bool Update(in VenueTagIndexKey wanted, IReadOnlyList<VenueEvent> source,
        IReadOnlyList<string> selectedTags)
    {
        if (built && key == wanted)
        {
            return false;
        }

        key = wanted;
        built = true;
        counts.Clear();
        entries.Clear();
        MatchCount = 0;
        var noTags = Array.Empty<string>();
        for (var index = 0; index < source.Count; index++)
        {
            var venue = source[index];
            if (!VenueFilter.MatchesScope(venue, wanted.Source, wanted.DataCenters, wanted.World, noTags,
                    wanted.HideAdult))
            {
                continue;
            }

            if (VenueFilter.MatchesTags(venue, selectedTags))
            {
                MatchCount++;
            }

            for (var tagIndex = 0; tagIndex < venue.Tags.Count; tagIndex++)
            {
                var tag = venue.Tags[tagIndex];
                if (VenueFilter.IsRatingTag(tag))
                {
                    continue;
                }

                counts[tag] = counts.TryGetValue(tag, out var count) ? count + 1 : 1;
            }
        }

        foreach (var pair in counts)
        {
            entries.Add(new VenueTagCount(pair.Key, pair.Value));
        }

        entries.Sort(order);
        return true;
    }

    private sealed class EntryOrder : IComparer<VenueTagCount>
    {
        public int Compare(VenueTagCount left, VenueTagCount right)
        {
            var byCount = right.Count.CompareTo(left.Count);
            return byCount != 0 ? byCount : string.Compare(left.Tag, right.Tag, StringComparison.OrdinalIgnoreCase);
        }
    }
}
