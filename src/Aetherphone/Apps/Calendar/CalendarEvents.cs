using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Net;

namespace Aetherphone.Apps.Calendar;

internal sealed class CalendarEvents : IDisposable
{
    private const string CacheFileName = "calendar_events.json";
    private static readonly TimeSpan CacheMaxAge = TimeSpan.FromHours(24);

    private static readonly Vector4[] EventPalette =
    {
        new(0.200f, 0.780f, 0.349f, 1f),
        new(0.000f, 0.478f, 1.000f, 1f),
        new(0.557f, 0.267f, 0.678f, 1f),
        new(1.000f, 0.584f, 0.000f, 1f),
        new(1.000f, 0.341f, 0.494f, 1f),
        new(1.000f, 0.855f, 0.298f, 1f),
        new(1.000f, 0.231f, 0.188f, 1f),
        new(0.157f, 0.761f, 0.796f, 1f),
    };

    private readonly HttpService http;
    private readonly AethernetSession session;
    private readonly CancellationTokenSource cancellation = new();
    private readonly string cachePath;
    private FrozenDictionary<long, ParsedEvent[]> events = FrozenDictionary<long, ParsedEvent[]>.Empty;
    private bool loaded;
    private bool loading;
    private bool failed;

    public bool IsLoaded => loaded;
    public bool IsLoading => loading;
    public bool HasFailed => failed;
    public FrozenDictionary<long, ParsedEvent[]> Events => events;

    public CalendarEvents(HttpService http, AethernetSession session)
    {
        this.http = http;
        this.session = session;
        var configDir = Plugin.PluginInterface.ConfigDirectory.FullName;
        cachePath = Path.Combine(configDir, CacheFileName);
    }

    public void Initialize()
    {
        if (loaded || loading)
        {
            return;
        }

        if (TryLoadCache(out var cacheData, out var cacheAge) && cacheAge < CacheMaxAge)
        {
            BuildDictionary(cacheData);
            loaded = true;
            return;
        }

        loading = true;
        _ = Task.Run(FetchAndProcessAsync);
    }

    private async Task FetchAndProcessAsync()
    {
        try
        {
            var url = $"{session.BaseUrl.TrimEnd('/')}/calendar/events";
            var records = await http.GetJsonAsync(url, CalendarJsonContext.Default.EventRecordArray, null,
                cancellation.Token).ConfigureAwait(false);
            if (records is null)
            {
                LoadFromCacheOrFail();
                return;
            }

            TryWriteCache(records);
            Build(records);
            loaded = true;
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            loading = false;
        }
    }

    private void LoadFromCacheOrFail()
    {
        if (TryLoadCache(out var cacheData, out _))
        {
            BuildDictionary(cacheData);
            loaded = true;
            return;
        }

        failed = true;
    }

    private void TryWriteCache(EventRecord[] records)
    {
        try
        {
            File.WriteAllText(cachePath,
                JsonSerializer.Serialize(records, CalendarJsonContext.Default.EventRecordArray));
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Calendar cache write failed: {exception.Message}");
        }
    }

    private bool TryLoadCache(out string json, out TimeSpan age)
    {
        json = string.Empty;
        age = TimeSpan.MaxValue;

        if (!File.Exists(cachePath))
        {
            return false;
        }

        try
        {
            json = File.ReadAllText(cachePath);
            age = DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath);
            return json.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private void BuildDictionary(string json)
    {
        try
        {
            Build(JsonSerializer.Deserialize(json, CalendarJsonContext.Default.EventRecordArray));
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Calendar events parse failed: {exception.Message}");
        }
    }

    private void Build(EventRecord[]? records)
    {
        if (records is null || records.Length == 0)
        {
            events = FrozenDictionary<long, ParsedEvent[]>.Empty;
            return;
        }

        var builder = new Dictionary<long, List<ParsedEvent>>();
        var colorQueue = new Queue<int>(Enumerable.Range(0, EventPalette.Length));
        var usedColors = new Dictionary<long, int>(records.Length);

        for (var index = 0; index < records.Length; index++)
        {
            var record = records[index];
            if (record.Begin > record.End)
            {
                continue;
            }

            if (!usedColors.TryGetValue(record.Id, out var colorIndex))
            {
                colorIndex = colorQueue.Dequeue();
                colorQueue.Enqueue(colorIndex);
                usedColors[record.Id] = colorIndex;
            }

            var paletteColor = EventPalette[colorIndex];
            var dimmed = new Vector4(paletteColor.X, paletteColor.Y, paletteColor.Z, 0.42f);

            foreach (var day in EachDay(record.Begin, record.End))
            {
                var key = day.Ticks;
                if (!builder.TryGetValue(key, out var dayEvents))
                {
                    dayEvents = new List<ParsedEvent>();
                    builder[key] = dayEvents;
                }

                dayEvents.Add(new ParsedEvent
                {
                    Id = record.Id,
                    Name = record.Name,
                    Begin = record.Begin,
                    End = record.End,
                    Special = record.Special,
                    IsPvP = record.Pvp,
                    Url = record.Url,
                    Color = paletteColor,
                    DimColor = dimmed,
                });
            }
        }

        var frozen = new Dictionary<long, ParsedEvent[]>(builder.Count);
        foreach (var pair in builder)
        {
            var array = pair.Value.ToArray();
            for (var index = 0; index < array.Length; index++)
            {
                array[index].Spacing = index * 10f;
            }

            frozen[pair.Key] = array;
        }

        events = frozen.ToFrozenDictionary();
    }

    public ParsedEvent[] GetEvents(DateTime day)
    {
        var key = day.Date.Ticks;
        return events.TryGetValue(key, out var dayEvents) ? dayEvents : Array.Empty<ParsedEvent>();
    }

    public bool HasEvents(DateTime day)
    {
        return events.ContainsKey(day.Date.Ticks);
    }

    private static IEnumerable<DateTime> EachDay(DateTime begin, DateTime end)
    {
        for (var day = begin.Date; day <= end.Date; day = day.AddDays(1))
        {
            yield return day;
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
    }
}

[JsonSerializable(typeof(EventRecord[]))]
internal sealed partial class CalendarJsonContext : JsonSerializerContext
{
}

[Serializable]
internal sealed class EventRecord
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("begin")] public DateTime Begin { get; set; }
    [JsonPropertyName("end")] public DateTime End { get; set; }
    [JsonPropertyName("special")] public bool Special { get; set; }
    [JsonPropertyName("pvp")] public bool Pvp { get; set; }
    [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;
}

internal struct ParsedEvent
{
    public long Id;
    public string Name;
    public DateTime Begin;
    public DateTime End;
    public bool Special;
    public bool IsPvP;
    public string Url;
    public Vector4 Color;
    public Vector4 DimColor;
    public float Spacing;
    public bool IsCustom;
    public Guid CustomId;
}
