using System.Collections.Frozen;
using System.Net.Http.Headers;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetherphone.Core;

namespace Aetherphone.Apps.Calendar;

internal sealed class CalendarEvents : IDisposable
{
    private const string SupabaseUrl = "https://xzwnvwjxgmaqtrxewngh.supabase.co/rest/v1/Events?select=*";
    private const string AnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Inh6d252d2p4Z21hcXRyeGV3bmdoIiwicm9sZSI6ImFub24iLCJpYXQiOjE2ODk3NzcwMDIsImV4cCI6MjAwNTM1MzAwMn0.aNYTnhY_Sagi9DyH5Q9tCz9lwaRCYzMC12SZ7q7jZBc";
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

    private readonly HttpClient client;
    private readonly string cachePath;
    private FrozenDictionary<long, ParsedEvent[]> events = FrozenDictionary<long, ParsedEvent[]>.Empty;
    private bool loaded;
    private bool loading;
    private bool failed;

    public bool IsLoaded => loaded;
    public bool IsLoading => loading;
    public bool HasFailed => failed;
    public FrozenDictionary<long, ParsedEvent[]> Events => events;

    public CalendarEvents()
    {
        client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Aetherphone-Calendar/1.0");
        client.DefaultRequestHeaders.TryAddWithoutValidation("apikey", AnonKey);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AnonKey);
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
            var json = await client.GetStringAsync(SupabaseUrl).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(json))
            {
                File.WriteAllText(cachePath, json);
            }

            BuildDictionary(json);
            loaded = true;
        }
        catch (Exception exception)
        {
            AepLog.Warning($"Calendar events fetch failed: {exception.Message}");
            if (TryLoadCache(out var cacheData, out _))
            {
                BuildDictionary(cacheData);
                loaded = true;
                loading = false;
                return;
            }

            failed = true;
        }

        loading = false;
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
            var records = JsonSerializer.Deserialize<EventRecord[]>(json);
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
        catch (Exception exception)
        {
            AepLog.Warning($"Calendar events parse failed: {exception.Message}");
        }
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
        client.Dispose();
    }
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
