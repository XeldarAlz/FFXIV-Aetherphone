using Aetherphone.Core.Net;

namespace Aetherphone.Core.Radio;

internal readonly struct RadioCategory
{
    public readonly string Display;
    public readonly string Tag;

    public RadioCategory(string display, string tag)
    {
        Display = display;
        Tag = tag;
    }
}

internal sealed class RadioService : IDisposable
{
    private const string ApiRoot = "https://all.api.radio-browser.info";
    private const int StationLimit = 40;

    public static readonly RadioCategory[] Categories =
    {
        new("Lofi", "lofi"), new("Chillout", "chillout"), new("Jazz", "jazz"), new("Classical", "classical"),
        new("Ambient", "ambient"), new("Electronic", "electronic"), new("Pop", "pop"), new("Rock", "rock"),
        new("Metal", "metal"), new("Hip-Hop", "hip hop"), new("Soundtrack", "soundtrack"), new("Anime", "anime"),
    };

    private readonly HttpService http;
    private readonly RequestThrottle throttle;
    private readonly CancellationTokenSource cancellation = new();

    public RadioService(HttpService http)
    {
        this.http = http;
        throttle = new RequestThrottle(2, TimeSpan.FromMilliseconds(250));
    }

    public async Task<RadioStation[]> FetchStationsAsync(string tag, CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, cancellation.Token);
        try
        {
            using (await throttle.EnterAsync(linked.Token).ConfigureAwait(false))
            {
                var url =
                    $"{ApiRoot}/json/stations/search?tag={Uri.EscapeDataString(tag)}&tagExact=true&codec=MP3&hidebroken=true&order=clickcount&reverse=true&limit={StationLimit}";
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
            AepLog.Warning($"Radio fetch failed for {tag}: {exception.Message}");
            return Array.Empty<RadioStation>();
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
