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
    private const string BaseQuery = "codec=MP3&hidebroken=true";

    public static readonly RadioCategory[] Categories =
    {
        new("Lofi", "lofi", "lo-fi", "chillhop"), new("Chillout", "chillout", "chill"), new("Jazz", "jazz"),
        new("Classical", "classical"), new("Ambient", "ambient"), new("Electronic", "electronic"), new("Pop", "pop"),
        new("Rock", "rock"), new("Metal", "metal"), new("Hip-Hop", "hip hop", "rap"),
        new("Soundtrack", "soundtrack"), new("Anime", "anime"),
    };

    private readonly HttpService http;
    private readonly RequestThrottle throttle;
    private readonly CancellationTokenSource cancellation = new();
    private RadioFacet[]? countries;
    private RadioFacet[]? languages;

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
            var single = await QueryAsync(TagUrl(tags[0], filter, offset), tags[0], token).ConfigureAwait(false);
            return new RadioPage(single, single.Length >= PageSize);
        }

        var merged = new List<RadioStation>(tags.Length * PageSize);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasMore = false;
        for (var index = 0; index < tags.Length; index++)
        {
            var page = await QueryAsync(TagUrl(tags[index], filter, offset), tags[index], token).ConfigureAwait(false);
            if (page.Length >= PageSize)
            {
                hasMore = true;
            }

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

        var suffix = QuerySuffix(filter, offset);
        if (trimmed.Length == 0)
        {
            var browse = await QueryAsync($"{ApiRoot}/json/stations/search?{suffix}", "browse", token)
                .ConfigureAwait(false);
            return new RadioPage(browse, browse.Length >= PageSize);
        }

        var term = Uri.EscapeDataString(trimmed);
        var byName = $"{ApiRoot}/json/stations/search?name={term}&{suffix}";
        var nameMatches = await QueryAsync(byName, trimmed, token).ConfigureAwait(false);
        var hasMore = nameMatches.Length >= PageSize;
        if (offset > 0)
        {
            return new RadioPage(nameMatches, hasMore);
        }

        var byTag = $"{ApiRoot}/json/stations/search?tag={term}&{QuerySuffix(filter, 0)}";
        var tagMatches = await QueryAsync(byTag, trimmed, token).ConfigureAwait(false);
        return new RadioPage(Merge(nameMatches, tagMatches), hasMore);
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
            AepLog.Warning($"Radio country list failed: {exception.Message}");
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
                    .GetJsonAsync($"{ApiRoot}/json/languages", RadioJsonContext.Default.RadioLanguageDtoArray, null,
                        linked.Token)
                    .ConfigureAwait(false);
                languages = ProjectLanguages(dtos);
                return languages;
            }
        }
        catch (OperationCanceledException)
        {
            return Array.Empty<RadioFacet>();
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Radio language list failed: {exception.Message}");
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
        catch (Exception)
        {
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

    private static RadioFacet[] ProjectLanguages(RadioLanguageDto[]? dtos)
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

    private async Task<RadioStation[]> QueryAsync(string url, string label, CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, cancellation.Token);
        try
        {
            using (await throttle.EnterAsync(linked.Token).ConfigureAwait(false))
            {
                var dtos = await http
                    .GetJsonAsync(url, RadioJsonContext.Default.RadioStationDtoArray, null, linked.Token)
                    .ConfigureAwait(false);
                return Project(dtos);
            }
        }
        catch (OperationCanceledException)
        {
            return Array.Empty<RadioStation>();
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Radio fetch failed for {label}: {exception.Message}");
            return Array.Empty<RadioStation>();
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

    private static RadioStation[] Project(RadioStationDto[]? dtos)
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

            stations.Add(new RadioStation(dto.Name, stream, dto.Codec ?? string.Empty, dto.Bitrate,
                dto.Country ?? string.Empty, dto.StationUuid ?? string.Empty));
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
