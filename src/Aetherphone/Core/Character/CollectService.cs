using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Character;

internal enum CollectState : byte
{
    Idle,
    Loading,
    Ready,
    Unavailable,
}

internal sealed class CollectEntry
{
    public volatile CollectState State = CollectState.Idle;
    public CollectCharacter? Character;
    public DateTime FetchedUtc;
}

internal sealed class CollectService : IDisposable
{
    private const string ApiRoot = "https://ffxivcollect.com/api/characters";

    private static readonly TimeSpan MemoryFreshFor = TimeSpan.FromHours(6);
    private static readonly TimeSpan DiskFreshFor = TimeSpan.FromHours(24);
    private static readonly TimeSpan UnavailableRetryFor = TimeSpan.FromMinutes(2);

    private readonly HttpService http;
    private readonly DiskCache disk;
    private readonly RequestThrottle throttle;
    private readonly CancellationTokenSource cancellation = new();
    private readonly object sync = new();
    private readonly Dictionary<string, CollectEntry> entries = new();

    public CollectService(HttpService http, DiskCache disk)
    {
        this.http = http;
        this.disk = disk;
        throttle = new RequestThrottle(1, TimeSpan.FromMilliseconds(1500));
    }

    public CollectEntry Request(string lodestoneId)
    {
        if (lodestoneId.Length == 0)
        {
            return new CollectEntry { State = CollectState.Unavailable };
        }

        CollectEntry entry;
        lock (sync)
        {
            if (!entries.TryGetValue(lodestoneId, out var existing))
            {
                existing = new CollectEntry();
                entries[lodestoneId] = existing;
            }

            entry = existing;
        }

        if (entry.State == CollectState.Loading)
        {
            return entry;
        }

        var freshFor = entry.State == CollectState.Unavailable ? UnavailableRetryFor : MemoryFreshFor;
        var stale = entry.State == CollectState.Idle || DateTime.UtcNow - entry.FetchedUtc >= freshFor;
        if (stale)
        {
            entry.State = CollectState.Loading;
            _ = LoadAsync(lodestoneId, entry);
        }

        return entry;
    }

    private async Task LoadAsync(string lodestoneId, CollectEntry entry)
    {
        try
        {
            var token = cancellation.Token;
            var cacheKey = $"ffxivcollect:{lodestoneId}";

            var cached = disk.Get(cacheKey, DiskFreshFor);
            if (cached is not null && TryDeserialize(cached, out var fromDisk))
            {
                Apply(entry, fromDisk);
                return;
            }

            using (await throttle.EnterAsync(token).ConfigureAwait(false))
            {
                var bytes = await http.GetBytesAsync(new Uri($"{ApiRoot}/{lodestoneId}"), token).ConfigureAwait(false);
                if (bytes is null || !TryDeserialize(bytes, out var fromApi))
                {
                    entry.FetchedUtc = DateTime.UtcNow;
                    entry.State = CollectState.Unavailable;
                    return;
                }

                disk.Set(cacheKey, bytes);
                Apply(entry, fromApi);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            entry.FetchedUtc = DateTime.UtcNow;
            entry.State = CollectState.Unavailable;
            AepLog.Warning($"FFXIV Collect fetch failed for {lodestoneId}: {exception.Message}");
        }
    }

    private static void Apply(CollectEntry entry, CollectCharacter character)
    {
        entry.Character = character;
        entry.FetchedUtc = DateTime.UtcNow;
        entry.State = CollectState.Ready;
    }

    private static bool TryDeserialize(byte[] bytes, out CollectCharacter character)
    {
        character = default!;
        try
        {
            var reader = new Utf8JsonReader(bytes);
            var parsed = JsonSerializer.Deserialize(ref reader, CollectJsonContext.Default.CollectCharacter);
            if (parsed is null)
            {
                return false;
            }

            character = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        throttle.Dispose();
        cancellation.Dispose();
    }
}
