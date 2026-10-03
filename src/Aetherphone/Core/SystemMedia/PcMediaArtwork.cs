using Aetherphone.Core.Media;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core.SystemMedia;

internal sealed class PcMediaArtwork : IDisposable
{
    private const string TextureTag = "Aetherphone.PcMedia";

    private readonly Lock gate = new();
    private readonly IDalamudTextureWrap?[] levels = new IDalamudTextureWrap?[TextureSizes.LevelCount + 1];
    private readonly int[] requested = new int[TextureSizes.LevelCount + 1];
    private CancellationTokenSource cancellation = new();
    private byte[]? source;
    private int revision;
    private int generation = 1;
    private bool disposed;

    public IDalamudTextureWrap? Get(in MediaSessionSnapshot snapshot, float drawnPixels)
    {
        lock (gate)
        {
            if (disposed)
            {
                return null;
            }

            if (snapshot.ArtworkRevision != revision || !ReferenceEquals(snapshot.Artwork, source))
            {
                Swap(snapshot.Artwork, snapshot.ArtworkRevision);
            }

            if (source is null)
            {
                return null;
            }

            var level = TextureSizes.LevelFor(drawnPixels);
            var texture = levels[level];
            if (texture is not null)
            {
                return texture;
            }

            if (requested[level] != generation)
            {
                requested[level] = generation;
                _ = LoadAsync(source, level, generation, cancellation.Token);
            }

            return Nearest(level);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            cancellation.Cancel();
            for (var level = 0; level < levels.Length; level++)
            {
                levels[level]?.Dispose();
                levels[level] = null;
            }
        }
    }

    private void Swap(byte[]? artwork, int artworkRevision)
    {
        cancellation.Cancel();
        cancellation = new CancellationTokenSource();
        for (var level = 0; level < levels.Length; level++)
        {
            DeferredDispose.Later(levels[level]);
            levels[level] = null;
        }

        source = artwork;
        revision = artworkRevision;
        generation++;
    }

    private IDalamudTextureWrap? Nearest(int level)
    {
        for (var distance = 1; distance < levels.Length; distance++)
        {
            var above = level + distance;
            if (above < levels.Length && levels[above] is { } larger)
            {
                return larger;
            }

            var below = level - distance;
            if (below > TextureSizes.Native && levels[below] is { } smaller)
            {
                return smaller;
            }
        }

        return null;
    }

    private async Task LoadAsync(byte[] bytes, int level, int loadGeneration, CancellationToken token)
    {
        try
        {
            var wrap = await ImageProcessor.DecodeToTextureAsync(Plugin.TextureProvider, bytes, TextureTag,
                ImageProcessor.MaxDecodePixels, TextureSizes.SizeOf(level), token).ConfigureAwait(false);
            lock (gate)
            {
                if (disposed || loadGeneration != generation)
                {
                    wrap.Dispose();
                    return;
                }

                levels[level] = wrap;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, "[SystemMedia] failed to decode media artwork");
        }
    }
}
