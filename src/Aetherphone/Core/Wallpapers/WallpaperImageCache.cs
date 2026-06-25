using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core.Wallpapers;

internal sealed class WallpaperImageCache : IDisposable
{
    private readonly ConcurrentDictionary<string, IDalamudTextureWrap> ready = new();
    private readonly ConcurrentDictionary<string, byte> loading = new();
    private readonly ConcurrentDictionary<string, byte> failed = new();
    private readonly CancellationTokenSource cancellation = new();

    public IDalamudTextureWrap? Get(string path)
    {
        if (ready.TryGetValue(path, out var wrap))
        {
            return wrap;
        }

        if (failed.ContainsKey(path) || !loading.TryAdd(path, 0))
        {
            return null;
        }

        _ = LoadAsync(path);
        return null;
    }

    public bool Failed(string path) => failed.ContainsKey(path);

    public void Dispose()
    {
        cancellation.Cancel();
        foreach (var wrap in ready.Values)
        {
            wrap.Dispose();
        }

        ready.Clear();
        cancellation.Dispose();
    }

    private async Task LoadAsync(string path)
    {
        try
        {
            var token = cancellation.Token;
            var bytes = await File.ReadAllBytesAsync(path, token).ConfigureAwait(false);
            var wrap = await Plugin.TextureProvider.CreateFromImageAsync(bytes, path, token).ConfigureAwait(false);
            if (!ready.TryAdd(path, wrap))
            {
                wrap.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            failed.TryAdd(path, 0);
            AepLog.Warning($"[Wallpaper] failed to load {path}: {exception.Message}");
        }
        finally
        {
            loading.TryRemove(path, out _);
        }
    }
}
