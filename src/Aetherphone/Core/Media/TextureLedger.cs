using System.Collections.Concurrent;
using System.Numerics;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core.Media;

internal sealed class TextureLedger
{
    private sealed class Entry
    {
        public readonly IDalamudTextureWrap Wrap;
        public readonly long Bytes;
        public long LastAccessTicks;

        public Entry(IDalamudTextureWrap wrap)
        {
            Wrap = wrap;
            Bytes = (long)wrap.Width * wrap.Height * 4;
            LastAccessTicks = Environment.TickCount64;
        }
    }

    private static readonly TimeSpan EvictionIdleFloor = TimeSpan.FromSeconds(5);
    private readonly ConcurrentDictionary<string, Entry> entries = new();
    private readonly long budgetBytes;
    private long totalBytes;

    public TextureLedger(long budgetBytes)
    {
        this.budgetBytes = budgetBytes;
    }

    public IDalamudTextureWrap? Get(string key)
    {
        if (!entries.TryGetValue(key, out var entry))
        {
            return null;
        }

        Volatile.Write(ref entry.LastAccessTicks, Environment.TickCount64);
        return entry.Wrap;
    }

    public Vector2 SizeOf(string key)
    {
        return entries.TryGetValue(key, out var entry) ? entry.Wrap.Size : Vector2.Zero;
    }

    public bool TryAdd(string key, IDalamudTextureWrap wrap)
    {
        var entry = new Entry(wrap);
        if (!entries.TryAdd(key, entry))
        {
            return false;
        }

        Interlocked.Add(ref totalBytes, entry.Bytes);
        EvictPastBudget();
        return true;
    }

    public bool TryRemove(string key, out IDalamudTextureWrap wrap)
    {
        if (entries.TryRemove(key, out var entry))
        {
            Interlocked.Add(ref totalBytes, -entry.Bytes);
            wrap = entry.Wrap;
            return true;
        }

        wrap = null!;
        return false;
    }

    public void DisposeAll()
    {
        foreach (var key in entries.Keys)
        {
            if (entries.TryRemove(key, out var entry))
            {
                Interlocked.Add(ref totalBytes, -entry.Bytes);
                entry.Wrap.Dispose();
            }
        }
    }

    private void EvictPastBudget()
    {
        if (Interlocked.Read(ref totalBytes) <= budgetBytes)
        {
            return;
        }

        var now = Environment.TickCount64;
        var idleFloorMs = (long)EvictionIdleFloor.TotalMilliseconds;
        var candidates = new List<KeyValuePair<string, Entry>>();
        foreach (var pair in entries)
        {
            if (now - Volatile.Read(ref pair.Value.LastAccessTicks) >= idleFloorMs)
            {
                candidates.Add(pair);
            }
        }

        candidates.Sort(static (left, right) =>
            Volatile.Read(ref left.Value.LastAccessTicks).CompareTo(Volatile.Read(ref right.Value.LastAccessTicks)));

        for (var index = 0; index < candidates.Count; index++)
        {
            if (Interlocked.Read(ref totalBytes) <= budgetBytes)
            {
                return;
            }

            if (entries.TryRemove(candidates[index].Key, out var removed))
            {
                Interlocked.Add(ref totalBytes, -removed.Bytes);
                _ = Plugin.Framework.RunOnFrameworkThread(removed.Wrap.Dispose);
            }
        }
    }
}
