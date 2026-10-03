using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Lumina.Data.Files;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Aetherphone.Core.Maps;

internal sealed class ZoneMapLadder : IDisposable
{
    private const int Capacity = 4;
    private const int SmallestLevel = 256;
    private const int LevelCount = 3;

    private readonly ZoneMapTextures textures;
    private readonly IDataManager data;
    private readonly ITextureProvider provider;
    private readonly Dictionary<uint, Entry> entries = new();
    private readonly List<uint> stale = new();
    private int frame;

    private sealed class Entry
    {
        public readonly object Gate = new();
        public readonly IDalamudTextureWrap?[] Levels = new IDalamudTextureWrap?[LevelCount];
        public volatile bool Ready;
        public bool Disposed;
        public int LastFrame;
    }

    public ZoneMapLadder(ZoneMapTextures textures, IDataManager data, ITextureProvider provider)
    {
        this.textures = textures;
        this.data = data;
        this.provider = provider;
    }

    public void BeginFrame()
    {
        frame++;
        if (entries.Count <= Capacity)
        {
            return;
        }

        var oldestFrame = int.MaxValue;
        var oldest = 0u;
        foreach (var pair in entries)
        {
            if (pair.Value.LastFrame < oldestFrame)
            {
                oldestFrame = pair.Value.LastFrame;
                oldest = pair.Key;
            }
        }

        if (oldestFrame < frame - 1)
        {
            Release(oldest);
        }
    }

    public IDalamudTextureWrap? Get(uint mapRowId, float drawnCanvasPixels)
    {
        var native = textures.ForMap(mapRowId);
        if (native is null)
        {
            return null;
        }

        var nativeSize = Math.Max(native.Width, native.Height);
        if (drawnCanvasPixels * 2f >= nativeSize)
        {
            return native;
        }

        var entry = Touch(mapRowId, nativeSize);
        if (entry is null || !entry.Ready)
        {
            return native;
        }

        var levelSize = SmallestLevel;
        for (var levelIndex = 0; levelIndex < LevelCount; levelIndex++, levelSize *= 2)
        {
            if (levelSize >= drawnCanvasPixels && entry.Levels[levelIndex] is { } level)
            {
                return level;
            }
        }

        return native;
    }

    private Entry? Touch(uint mapRowId, int nativeSize)
    {
        if (entries.TryGetValue(mapRowId, out var existing))
        {
            existing.LastFrame = frame;
            return existing;
        }

        var path = textures.TexturePath(mapRowId);
        if (path is null)
        {
            return null;
        }

        var entry = new Entry { LastFrame = frame };
        entries[mapRowId] = entry;
        _ = Task.Run(() => BuildAsync(entry, path, nativeSize));
        return entry;
    }

    private async Task BuildAsync(Entry entry, string path, int nativeSize)
    {
        try
        {
            var file = data.GetFile<TexFile>(path);
            if (file is null)
            {
                return;
            }

            var width = file.Header.Width;
            var height = file.Header.Height;
            using var image = Image.LoadPixelData<Bgra32>(file.ImageData, width, height);
            var built = new IDalamudTextureWrap?[LevelCount];
            for (var levelIndex = LevelCount - 1; levelIndex >= 0; levelIndex--)
            {
                var levelSize = SmallestLevel << levelIndex;
                if (levelSize >= nativeSize || levelSize >= Math.Max(width, height))
                {
                    continue;
                }

                var levelWidth = Math.Max(1, width * levelSize / Math.Max(width, height));
                var levelHeight = Math.Max(1, height * levelSize / Math.Max(width, height));
                image.Mutate(context => context.Resize(levelWidth, levelHeight, KnownResamplers.Box));
                using var rgba = image.CloneAs<Rgba32>();
                var pixels = new byte[levelWidth * levelHeight * 4];
                rgba.CopyPixelDataTo(pixels);
                built[levelIndex] = await provider.CreateFromRawAsync(RawImageSpecification.Rgba32(levelWidth,
                    levelHeight), pixels, $"Aetherphone.ZoneMap.{levelSize}").ConfigureAwait(false);
            }

            Publish(entry, built);
        }
        catch (Exception exception)
        {
            AepLog.Debug(exception, $"Zone map ladder for '{path}' failed to build");
        }
    }

    private static void Publish(Entry entry, IDalamudTextureWrap?[] built)
    {
        lock (entry.Gate)
        {
            if (entry.Disposed)
            {
                DisposeAll(built);
                return;
            }

            for (var levelIndex = 0; levelIndex < LevelCount; levelIndex++)
            {
                entry.Levels[levelIndex] = built[levelIndex];
            }

            entry.Ready = true;
        }
    }

    private void Release(uint mapRowId)
    {
        if (!entries.Remove(mapRowId, out var entry))
        {
            return;
        }

        lock (entry.Gate)
        {
            entry.Disposed = true;
            entry.Ready = false;
            DisposeAll(entry.Levels);
        }
    }

    private static void DisposeAll(IDalamudTextureWrap?[] levels)
    {
        for (var levelIndex = 0; levelIndex < levels.Length; levelIndex++)
        {
            levels[levelIndex]?.Dispose();
            levels[levelIndex] = null;
        }
    }

    public void Dispose()
    {
        stale.Clear();
        foreach (var pair in entries)
        {
            stale.Add(pair.Key);
        }

        for (var index = 0; index < stale.Count; index++)
        {
            Release(stale[index]);
        }
    }
}
