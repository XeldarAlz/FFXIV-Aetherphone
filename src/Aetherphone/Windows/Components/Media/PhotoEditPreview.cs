using Aetherphone.Core;
using Aetherphone.Core.Media;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Windows.Components;

internal sealed class PhotoEditPreview : IDisposable
{
    public const int ProxyMaxDimension = 720;
    public const int LookThumbnailDimension = 96;
    private const double RenderDebounceSeconds = 0.04;

    private readonly IDalamudTextureWrap?[] lookTextures = new IDalamudTextureWrap?[PhotoLooks.All.Length];
    private PixelImage proxy = PixelImage.Empty;
    private IDalamudTextureWrap? texture;
    private PhotoEdit renderedEdit = PhotoEdit.None;
    private bool hasRendered;
    private PhotoEdit pendingEdit = PhotoEdit.None;
    private bool hasPending;
    private double pendingSince;
    private bool rendering;
    private bool looksRequested;
    private int generation;
    private int textureSerial;
    private volatile bool ready;
    private volatile bool failed;
    private CancellationTokenSource cancellation = new();

    public bool Ready => ready;

    public bool Failed => failed;

    public Vector2 ProxySize => proxy.Size;

    public void Open(string path)
    {
        cancellation.Cancel();
        cancellation.Dispose();
        cancellation = new CancellationTokenSource();
        generation++;
        ready = false;
        failed = false;
        hasRendered = false;
        hasPending = false;
        rendering = false;
        looksRequested = false;
        proxy = PixelImage.Empty;
        ReleaseTextures();
        var token = cancellation.Token;
        var openedGeneration = generation;
        _ = Task.Run(() => LoadProxy(path, openedGeneration, token), token);
    }

    private void LoadProxy(string path, int openedGeneration, CancellationToken token)
    {
        try
        {
            var decoded = ImageProcessor.DecodeLocalRgba32(path, ProxyMaxDimension);
            token.ThrowIfCancellationRequested();
            if (openedGeneration != generation)
            {
                return;
            }

            proxy = decoded;
            ready = true;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[Photos] could not open {Path.GetFileName(path)} for editing");
            failed = true;
        }
    }

    public IDalamudTextureWrap? Texture(in PhotoEdit edit, double now)
    {
        if (!ready)
        {
            return null;
        }

        var stale = !hasRendered || !renderedEdit.SameAs(edit);
        if (stale && (!hasPending || !pendingEdit.SameAs(edit)))
        {
            pendingEdit = edit;
            hasPending = true;
            pendingSince = now;
        }

        if (hasPending && !rendering && (!hasRendered || now - pendingSince >= RenderDebounceSeconds))
        {
            StartRender(pendingEdit);
        }

        return texture;
    }

    public IDalamudTextureWrap? LookTexture(int lookIndex)
    {
        if (!ready)
        {
            return null;
        }

        if (!looksRequested)
        {
            looksRequested = true;
            _ = RenderLooksAsync(proxy, generation, cancellation.Token);
        }

        return lookIndex >= 0 && lookIndex < lookTextures.Length ? lookTextures[lookIndex] : null;
    }

    private void StartRender(PhotoEdit edit)
    {
        rendering = true;
        hasPending = false;
        var token = cancellation.Token;
        var renderGeneration = generation;
        var source = proxy;
        textureSerial++;
        var tag = $"Aetherphone.PhotoEdit.{renderGeneration}.{textureSerial}";
        _ = RenderAsync(source, edit, renderGeneration, tag, token);
    }

    private async Task RenderAsync(PixelImage source, PhotoEdit edit, int renderGeneration, string tag,
        CancellationToken token)
    {
        IDalamudTextureWrap? wrap = null;
        try
        {
            var rendered = await Task.Run(() => PhotoEditor.Apply(source, edit), token).ConfigureAwait(false);
            wrap = await Plugin.TextureProvider.CreateFromRawAsync(
                RawImageSpecification.Rgba32(rendered.Width, rendered.Height), rendered.Pixels, tag, token)
                .ConfigureAwait(false);
            var built = wrap;
            await Plugin.Framework.RunOnFrameworkThread(() => Swap(built, edit, renderGeneration))
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            wrap?.Dispose();
            await Plugin.Framework.RunOnFrameworkThread(() => FinishRender(renderGeneration)).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            wrap?.Dispose();
            AepLog.Warning(exception, "[Photos] edit preview render failed");
            await Plugin.Framework.RunOnFrameworkThread(() => FinishRender(renderGeneration)).ConfigureAwait(false);
        }
    }

    private async Task RenderLooksAsync(PixelImage source, int renderGeneration, CancellationToken token)
    {
        var looks = PhotoLooks.All;
        var built = new IDalamudTextureWrap?[looks.Length];
        try
        {
            var thumbnailBase = await Task.Run(() => PhotoEditor.Downscale(source, LookThumbnailDimension), token)
                .ConfigureAwait(false);
            for (var index = 0; index < looks.Length; index++)
            {
                var look = looks[index];
                var graded = await Task
                    .Run(() => PhotoEditor.Grade(thumbnailBase, PhotoEdit.None.WithLook(look, PhotoEdit.MaxLookStrength)),
                        token)
                    .ConfigureAwait(false);
                built[index] = await Plugin.TextureProvider.CreateFromRawAsync(
                        RawImageSpecification.Rgba32(graded.Width, graded.Height), graded.Pixels,
                        $"Aetherphone.PhotoLook.{renderGeneration}.{index}", token)
                    .ConfigureAwait(false);
            }

            await Plugin.Framework.RunOnFrameworkThread(() => SwapLooks(built, renderGeneration))
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            DisposeAll(built);
        }
        catch (Exception exception)
        {
            DisposeAll(built);
            AepLog.Warning(exception, "[Photos] look thumbnails failed to render");
        }
    }

    private void Swap(IDalamudTextureWrap built, PhotoEdit edit, int renderGeneration)
    {
        if (renderGeneration != generation)
        {
            built.Dispose();
            return;
        }

        var previous = texture;
        texture = built;
        renderedEdit = edit;
        hasRendered = true;
        rendering = false;
        DeferredDispose.Later(previous);
    }

    private void SwapLooks(IDalamudTextureWrap?[] built, int renderGeneration)
    {
        if (renderGeneration != generation)
        {
            DisposeAll(built);
            return;
        }

        for (var index = 0; index < lookTextures.Length; index++)
        {
            DeferredDispose.Later(lookTextures[index]);
            lookTextures[index] = built[index];
        }
    }

    private void FinishRender(int renderGeneration)
    {
        if (renderGeneration == generation)
        {
            rendering = false;
        }
    }

    private static void DisposeAll(IDalamudTextureWrap?[] wraps)
    {
        for (var index = 0; index < wraps.Length; index++)
        {
            wraps[index]?.Dispose();
            wraps[index] = null;
        }
    }

    private void ReleaseTextures()
    {
        var previous = texture;
        texture = null;
        DeferredDispose.Later(previous);
        for (var index = 0; index < lookTextures.Length; index++)
        {
            DeferredDispose.Later(lookTextures[index]);
            lookTextures[index] = null;
        }
    }

    public void Close()
    {
        cancellation.Cancel();
        generation++;
        ready = false;
        failed = false;
        hasRendered = false;
        hasPending = false;
        rendering = false;
        looksRequested = false;
        proxy = PixelImage.Empty;
        ReleaseTextures();
    }

    public void Dispose()
    {
        Close();
        cancellation.Dispose();
    }
}
