using System.Collections.Concurrent;
using Aetherphone.Core.Media;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Core.Wallpapers;

internal sealed class WallpaperImageCache : IDisposable
{
    private const long TextureBudgetBytes = 64L * 1024 * 1024;
    private readonly TextureLedger ready = new(TextureBudgetBytes);
    private readonly ConcurrentDictionary<string, byte> loading = new();
    private readonly ConcurrentDictionary<string, byte> failed = new();
    private readonly CancellationTokenSource cancellation = new();

    public IDalamudTextureWrap? Get(string path)
    {
        if (ready.Get(path) is { } wrap)
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
        ready.DisposeAll();
        cancellation.Dispose();
    }

    private async Task LoadAsync(string path)
    {
        try
        {
            var token = cancellation.Token;
            var bytes = await File.ReadAllBytesAsync(path, token).ConfigureAwait(false);
            var wrap = await ImageProcessor.DecodeToTextureAsync(Plugin.TextureProvider, bytes, path, token)
                .ConfigureAwait(false);
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
