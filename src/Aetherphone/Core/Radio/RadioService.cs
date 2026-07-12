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
    private const string CommonQuery = "codec=MP3&hidebroken=true&order=clickcount&reverse=true";

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

    public RadioService(HttpService http)
    {
        this.http = http;
        throttle = new RequestThrottle(2, TimeSpan.FromMilliseconds(250));
    }

    public async Task<RadioPage> FetchStationsAsync(string[] tags, int offset, CancellationToken token)
    {
        if (tags.Length == 1)
        {
            var single = await QueryAsync(TagUrl(tags[0], offset), tags[0], token).ConfigureAwait(false);
            return new RadioPage(single, single.Length >= PageSize);
        }

        var merged = new List<RadioStation>(tags.Length * PageSize);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasMore = false;
        for (var index = 0; index < tags.Length; index++)
        {
            var page = await QueryAsync(TagUrl(tags[index], offset), tags[index], token).ConfigureAwait(false);
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

    private static string TagUrl(string tag, int offset)
    {
        return $"{ApiRoot}/json/stations/search?tag={Uri.EscapeDataString(tag)}&{CommonQuery}&offset={offset}&limit={PageSize}";
    }

    public async Task<RadioPage> SearchStationsAsync(string query, int offset, CancellationToken token)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0)
        {
            return RadioPage.Empty;
        }

        var term = Uri.EscapeDataString(trimmed);
        var byName = $"{ApiRoot}/json/stations/search?name={term}&{CommonQuery}&offset={offset}&limit={PageSize}";
        var nameMatches = await QueryAsync(byName, trimmed, token).ConfigureAwait(false);
        var hasMore = nameMatches.Length >= PageSize;
        if (offset > 0)
        {
            return new RadioPage(nameMatches, hasMore);
        }

        var byTag = $"{ApiRoot}/json/stations/search?tag={term}&{CommonQuery}&offset=0&limit={PageSize}";
        var tagMatches = await QueryAsync(byTag, trimmed, token).ConfigureAwait(false);
        return new RadioPage(Merge(nameMatches, tagMatches), hasMore);
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
                dto.Country ?? string.Empty));
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
