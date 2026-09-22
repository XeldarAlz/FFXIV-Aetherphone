using System.Collections.Concurrent;

namespace Aetherphone.Core.Crypto;

internal sealed class SealedTextCache<TValue>
{
    private readonly ConcurrentDictionary<string, Entry> entries = new(StringComparer.Ordinal);

    public bool TryGet(string key, string sealedText, out TValue value)
    {
        if (entries.TryGetValue(key, out var entry)
            && string.Equals(entry.SealedText, sealedText, StringComparison.Ordinal))
        {
            value = entry.Value;
            return true;
        }

        value = default!;
        return false;
    }

    public bool TryGetLatest(string key, out TValue value)
    {
        if (entries.TryGetValue(key, out var entry))
        {
            value = entry.Value;
            return true;
        }

        value = default!;
        return false;
    }

    public void Set(string key, string sealedText, TValue value)
    {
        entries[key] = new Entry(sealedText, value);
    }

    public void Forget(string key)
    {
        entries.TryRemove(key, out _);
    }

    public void Clear()
    {
        entries.Clear();
    }

    private readonly record struct Entry(string SealedText, TValue Value);
}
