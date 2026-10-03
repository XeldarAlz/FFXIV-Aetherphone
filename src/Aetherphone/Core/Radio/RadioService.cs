using System.Text;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Radio;

internal readonly struct RadioCategory
{
    public readonly string Display;
    public readonly string Tag;
    public readonly string[] Tags;

    public RadioCategory(string display, string tag, params string[] related)
    {
        Display = display;
        Tag = tag;
        if (related.Length == 0)
        {
            Tags = new[] { tag };
            return;
        }

        Tags = new string[related.Length + 1];
        Tags[0] = tag;
        Array.Copy(related, 0, Tags, 1, related.Length);
    }
}

internal sealed class RadioService : IDisposable
{
    private const string ApiRoot = "https://all.api.radio-browser.info";
    public const int PageSize = 40;
    private const int MaxMerged = PageSize * 2;
    private const string BaseQuery = "hidebroken=true";
    private const int MaxUuidsPerLookup = 50;
    private const int TagFacetLimit = 200;

    public static readonly RadioCategory[] Categories =
    {
        new("Lofi", "lofi", "lo-fi", "chillhop"), new("Chillout", "chillout", "chill"), new("Jazz", "jazz"),
        new("Classical", "classical"), new("Ambient", "ambient"), new("Electronic", "electronic"), new("Pop", "pop"),
        new("Rock", "rock"), new("Metal", "metal"), new("Hip-Hop", "hip hop", "rap"),
        new("Soundtrack", "soundtrack"), new("Anime", "anime"),
    };

    public static readonly RadioCategory[] GameCategories =
    {
        new("Video Game Music", "video game", "videogame", "vgm", "gaming"),
        new("Anime", "anime", "anime openings"), new("J-Pop", "j-pop", "jpop", "vocaloid"),
        new("Chiptune", "chiptune", "retro games"), new("Soundtrack", "soundtrack", "ost"),
        new("Celtic", "celtic", "medieval", "fantasy"),
    };

    private readonly HttpService http;
    private readonly RequestThrottle throttle;
    private readonly CancellationTokenSource cancellation = new();
    private RadioFacet[]? countries;
    private RadioFacet[]? languages;
    private RadioFacet[]? tagFacets;

    public RadioService(HttpService http)
    {
        this.http = http;
        throttle = new RequestThrottle(2, TimeSpan.FromMilliseconds(250));
    }

    public async Task<RadioPage> FetchStationsAsync(string[] tags, RadioFilter filter, int offset,
        CancellationToken token)
    {
        if (tags.Length == 1)
        {
            return (await QueryAsync(TagUrl(tags[0], filter, offset), tags[0], token).ConfigureAwait(false)).ToPage();
        }

        var merged = new List<RadioStation>(tags.Length * PageSize);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasMore = false;
        for (var index = 0; index < tags.Length; index++)
        {
            var batch = await QueryAsync(TagUrl(tags[index], filter, offset), tags[index], token).ConfigureAwait(false);
            hasMore |= batch.Full;
            var page = batch.Stations;
            for (var station = 0; station < page.Length; station++)
            {
                if (seen.Add(page[station].StreamUrl))
                {
                    merged.Add(page[station]);
                }
            }
        }

        return new RadioPage(merged.ToArray(), hasMore);
    }

    private static string TagUrl(string tag, in RadioFilter filter, int offset)
    {
        return $"{ApiRoot}/json/stations/search?tag={Uri.EscapeDataString(tag)}&{QuerySuffix(filter, offset)}";
    }

    private static string QuerySuffix(in RadioFilter filter, int offset)
    {
        var builder = new StringBuilder(128);
        builder.Append(BaseQuery);
        builder.Append("&order=").Append(OrderValue(filter.Order));
        if (filter.Order != RadioOrder.Name)
        {
            builder.Append("&reverse=true");
        }

        if (filter.CountryCode.Length > 0)
        {
            builder.Append("&countrycode=").Append(Uri.EscapeDataString(filter.CountryCode));
        }

        if (filter.Language.Length > 0)
        {
            builder.Append("&language=").Append(Uri.EscapeDataString(filter.Language));
        }

        builder.Append("&offset=").Append(offset).Append("&limit=").Append(PageSize);
        return builder.ToString();
    }

    private static string OrderValue(RadioOrder order)
    {
        return order switch
        {
            RadioOrder.Trending => "clicktrend",
            RadioOrder.TopVoted => "votes",
            RadioOrder.Name => "name",
            RadioOrder.Bitrate => "bitrate",
            _ => "clickcount",
        };
    }

    public async Task<RadioPage> SearchStationsAsync(string query, RadioFilter filter, int offset,
        CancellationToken token)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0 && filter.IsDefault)
        {
            return RadioPage.Empty;
        }

        if (trimmed.Length == 0)
        {
            return await BrowseAsync(filter, offset, token).ConfigureAwait(false);
        }

        var nameMatches = await QueryAsync(NameUrl(trimmed, filter, offset), trimmed, token).ConfigureAwait(false);
        if (offset > 0)
        {
            return nameMatches.ToPage();
        }

        var term = Uri.EscapeDataString(trimmed);
        var byTag = $"{ApiRoot}/json/stations/search?tag={term}&{QuerySuffix(filter, 0)}";
        var tagMatches = await QueryAsync(byTag, trimmed, token).ConfigureAwait(false);
        return new RadioPage(Merge(nameMatches.Stations, tagMatches.Stations), nameMatches.Full);
    }

    public async Task<RadioPage> SearchByNameAsync(string name, RadioFilter filter, int offset,
        CancellationToken token)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
        {
            return RadioPage.Empty;
        }

        return (await QueryAsync(NameUrl(trimmed, filter, offset), trimmed, token).ConfigureAwait(false)).ToPage();
    }

    public Task<RadioPage> FetchTopAsync(RadioOrder order, int offset, CancellationToken token)
    {
        return BrowseAsync(new RadioFilter(string.Empty, string.Empty, order), offset, token);
    }

    public Task<RadioPage> FetchByTagAsync(string tag, RadioOrder order, int offset, CancellationToken token)
    {
        return FetchStationsAsync(new[] { tag }, new RadioFilter(string.Empty, string.Empty, order), offset, token);
    }

    public Task<RadioPage> FetchByCountryAsync(string countryCode, RadioOrder order, int offset,
        CancellationToken token)
    {
        return BrowseAsync(new RadioFilter(countryCode, string.Empty, order), offset, token);
    }

    public Task<RadioPage> FetchByLanguageAsync(string language, RadioOrder order, int offset,
        CancellationToken token)
    {
        return BrowseAsync(new RadioFilter(string.Empty, language, order), offset, token);
    }

    public async Task<RadioStation?> FetchStationAsync(string uuid, CancellationToken token)
    {
        if (string.IsNullOrEmpty(uuid))
        {
            return null;
        }

        var found = await FetchStationsByUuidAsync(new[] { uuid }, token).ConfigureAwait(false);
        return found.Length > 0 ? found[0] : null;
    }

    public async Task<RadioStation[]> FetchStationsByUuidAsync(string[] uuids, CancellationToken token)
    {
        if (uuids.Length == 0)
        {
            return Array.Empty<RadioStation>();
        }

        var found = new List<RadioStation>(uuids.Length);
        for (var start = 0; start < uuids.Length; start += MaxUuidsPerLookup)
        {
            var count = Math.Min(MaxUuidsPerLookup, uuids.Length - start);
            var joined = Uri.EscapeDataString(string.Join(',', uuids, start, count));
            var batch = await QueryAsync($"{ApiRoot}/json/stations/byuuid?uuids={joined}", "uuid lookup", token)
                .ConfigureAwait(false);
            found.AddRange(batch.Stations);
        }

        return found.ToArray();
    }

    private async Task<RadioPage> BrowseAsync(RadioFilter filter, int offset, CancellationToken token)
    {
        var url = $"{ApiRoot}/json/stations/search?{QuerySuffix(filter, offset)}";
        return (await QueryAsync(url, "browse", token).ConfigureAwait(false)).ToPage();
    }

    private static string NameUrl(string name, in RadioFilter filter, int offset)
    {
        return $"{ApiRoot}/json/stations/search?name={Uri.EscapeDataString(name)}&{QuerySuffix(filter, offset)}";
    }

    public async Task<RadioFacet[]> FetchCountriesAsync(CancellationToken token)
    {
        if (countries is not null)
        {
            return countries;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, cancellation.Token);
        try
        {
            using (await throttle.EnterAsync(linked.Token).ConfigureAwait(false))
            {
                var dtos = await http
                    .GetJsonAsync($"{ApiRoot}/json/countries", RadioJsonContext.Default.RadioCountryDtoArray, null,
                        linked.Token)
                    .ConfigureAwait(false);
                countries = ProjectCountries(dtos);
                return countries;
            }
        }
        catch (OperationCanceledException)
        {
            return Array.Empty<RadioFacet>();
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Radio country list failed");
            return Array.Empty<RadioFacet>();
        }
    }

    public async Task<RadioFacet[]> FetchLanguagesAsync(CancellationToken token)
    {
        if (languages is not null)
        {
            return languages;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, cancellation.Token);
        try
        {
            using (await throttle.EnterAsync(linked.Token).ConfigureAwait(false))
            {
                var dtos = await http
                    .GetJsonAsync($"{ApiRoot}/json/languages", RadioJsonContext.Default.RadioNamedCountDtoArray, null,
                        linked.Token)
                    .ConfigureAwait(false);
                languages = ProjectNamedCounts(dtos);
                return languages;
            }
        }
        catch (OperationCanceledException)
        {
            return Array.Empty<RadioFacet>();
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Radio language list failed");
            return Array.Empty<RadioFacet>();
        }
    }

    public async Task<RadioFacet[]> FetchTagsAsync(CancellationToken token)
    {
        if (tagFacets is not null)
        {
            return tagFacets;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, cancellation.Token);
        try
        {
            using (await throttle.EnterAsync(linked.Token).ConfigureAwait(false))
            {
                var url = $"{ApiRoot}/json/tags?order=stationcount&reverse=true&hidebroken=true&limit={TagFacetLimit}";
                var dtos = await http
                    .GetJsonAsync(url, RadioJsonContext.Default.RadioNamedCountDtoArray, null, linked.Token)
                    .ConfigureAwait(false);
                tagFacets = ProjectNamedCounts(dtos);
                return tagFacets;
            }
        }
        catch (OperationCanceledException)
        {
            return Array.Empty<RadioFacet>();
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "Radio tag list failed");
            return Array.Empty<RadioFacet>();
        }
    }

    public void ReportClick(string stationUuid)
    {
        if (string.IsNullOrEmpty(stationUuid))
        {
            return;
        }

        _ = ReportClickAsync(stationUuid);
    }

    private async Task ReportClickAsync(string stationUuid)
    {
        try
        {
            var uri = new Uri($"{ApiRoot}/json/url/{Uri.EscapeDataString(stationUuid)}");
            await http.GetBytesAsync(uri, cancellation.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            AepLog.Debug(exception, $"[Radio] click report failed for station {stationUuid}");
        }
    }

    private static RadioFacet[] ProjectCountries(RadioCountryDto[]? dtos)
    {
        if (dtos is null || dtos.Length == 0)
        {
            return Array.Empty<RadioFacet>();
        }

        var facets = new List<RadioFacet>(dtos.Length);
        for (var index = 0; index < dtos.Length; index++)
        {
            var dto = dtos[index];
            if (string.IsNullOrEmpty(dto.Name) || string.IsNullOrEmpty(dto.IsoCode) || dto.StationCount <= 0)
            {
                continue;
            }

            facets.Add(new RadioFacet(dto.Name, dto.IsoCode, dto.StationCount));
        }

        return SortByCount(facets);
    }

    private static RadioFacet[] ProjectNamedCounts(RadioNamedCountDto[]? dtos)
    {
        if (dtos is null || dtos.Length == 0)
        {
            return Array.Empty<RadioFacet>();
        }

        var facets = new List<RadioFacet>(dtos.Length);
        for (var index = 0; index < dtos.Length; index++)
        {
            var dto = dtos[index];
            if (string.IsNullOrEmpty(dto.Name) || dto.StationCount <= 0)
            {
                continue;
            }

            facets.Add(new RadioFacet(Capitalize(dto.Name), dto.Name, dto.StationCount));
        }

        return SortByCount(facets);
    }

    private static RadioFacet[] SortByCount(List<RadioFacet> facets)
    {
        var sorted = facets.ToArray();
        Array.Sort(sorted, static (left, right) => right.Count.CompareTo(left.Count));
        return sorted;
    }

    private static string Capitalize(string value)
    {
        if (value.Length == 0 || char.IsUpper(value[0]))
        {
            return value;
        }

        return string.Concat(char.ToUpperInvariant(value[0]).ToString(), value.AsSpan(1));
    }

    private async Task<StationBatch> QueryAsync(string url, string label, CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, cancellation.Token);
        try
        {
            using (await throttle.EnterAsync(linked.Token).ConfigureAwait(false))
            {
                var dtos = await http
                    .GetJsonAsync(url, RadioJsonContext.Default.RadioStationDtoArray, null, linked.Token)
                    .ConfigureAwait(false);
                return new StationBatch(Project(dtos), dtos is not null && dtos.Length >= PageSize);
            }
        }
        catch (OperationCanceledException)
        {
            return StationBatch.Empty;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"Radio fetch failed for {label}");
            return StationBatch.Empty;
        }
    }

    private static RadioStation[] Merge(RadioStation[] primary, RadioStation[] secondary)
    {
        if (secondary.Length == 0)
        {
            return primary;
        }

        if (primary.Length == 0)
        {
            return secondary;
        }

        var merged = new List<RadioStation>(MaxMerged);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Append(merged, seen, primary);
        Append(merged, seen, secondary);
        return merged.ToArray();
    }

    private static void Append(List<RadioStation> target, HashSet<string> seen, RadioStation[] source)
    {
        for (var index = 0; index < source.Length && target.Count < MaxMerged; index++)
        {
            var station = source[index];
            if (seen.Add(station.StreamUrl))
            {
                target.Add(station);
            }
        }
    }

    public static RadioStation[] Project(RadioStationDto[]? dtos)
    {
        if (dtos is null || dtos.Length == 0)
        {
            return Array.Empty<RadioStation>();
        }

        var stations = new List<RadioStation>(dtos.Length);
        for (var index = 0; index < dtos.Length; index++)
        {
            var dto = dtos[index];
            var stream = !string.IsNullOrEmpty(dto.UrlResolved) ? dto.UrlResolved : dto.Url;
            if (string.IsNullOrEmpty(dto.Name) || string.IsNullOrEmpty(stream))
            {
                continue;
            }

            if (!RadioCodecPolicy.IsPlayable(dto.Codec, stream, dto.Hls != 0))
            {
                continue;
            }

            stations.Add(new RadioStation(dto.Name.Trim(), stream, dto.Codec ?? string.Empty, dto.Bitrate,
                dto.Country ?? string.Empty, dto.StationUuid ?? string.Empty, string.Empty, dto.Favicon ?? string.Empty,
                dto.Tags ?? string.Empty, dto.CountryCode ?? string.Empty, dto.Language ?? string.Empty, dto.Votes,
                dto.ClickCount));
        }

        return stations.ToArray();
    }

    public void Dispose()
    {
        cancellation.Cancel();
        throttle.Dispose();
        cancellation.Dispose();
    }
}

internal readonly struct StationBatch
{
    public static readonly StationBatch Empty = new(Array.Empty<RadioStation>(), false);

    public readonly RadioStation[] Stations;
    public readonly bool Full;

    public StationBatch(RadioStation[] stations, bool full)
    {
        Stations = stations;
        Full = full;
    }

    public RadioPage ToPage() => new(Stations, Full);
}
