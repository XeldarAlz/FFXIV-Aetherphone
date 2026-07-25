using System.Collections.Concurrent;

namespace Aetherphone.Core.Net;

internal sealed class EtagCache
{
    private const int MaxEntries = 256;
    private const int MaxBodyBytes = 256 * 1024;
    private const int EvictionBatch = 64;

    private readonly ConcurrentDictionary<string, Entry> entries = new();

    private sealed class Entry
    {
        public required string ETag;
        public required byte[] Body;
        public long LastUsed;
    }

    public static string Key(string? bearer, string? appScope, Uri? uri)
    {
        return string.Concat(bearer ?? string.Empty, "\n", appScope ?? string.Empty, "\n", uri?.ToString() ?? string.Empty);
    }

    public bool TryGet(string key, out string etag, out byte[] body)
    {
        if (entries.TryGetValue(key, out var entry))
        {
            entry.LastUsed = Environment.TickCount64;
            etag = entry.ETag;
            body = entry.Body;
            return true;
        }

        etag = string.Empty;
        body = Array.Empty<byte>();
        return false;
    }

    public void Store(string key, string etag, byte[] body)
    {
        if (body.Length > MaxBodyBytes)
        {
            entries.TryRemove(key, out _);
            return;
        }

        entries[key] = new Entry { ETag = etag, Body = body, LastUsed = Environment.TickCount64 };
        if (entries.Count > MaxEntries)
        {
            EvictOldest();
        }
    }

    private void EvictOldest()
    {
        var snapshot = new KeyValuePair<string, Entry>[entries.Count + EvictionBatch];
        var count = 0;
        foreach (var pair in entries)
        {
            if (count == snapshot.Length)
            {
                break;
            }

            snapshot[count] = pair;
            count++;
        }

        var ticks = new long[count];
        for (var index = 0; index < count; index++)
        {
            ticks[index] = snapshot[index].Value.LastUsed;
        }

        Array.Sort(ticks, snapshot, 0, count);
        var removeCount = Math.Min(EvictionBatch, count);
        for (var index = 0; index < removeCount; index++)
        {
            entries.TryRemove(snapshot[index].Key, out _);
        }
    }
}
