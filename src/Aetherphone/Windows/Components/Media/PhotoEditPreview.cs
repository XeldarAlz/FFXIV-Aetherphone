using Aetherphone.Core;
using Aetherphone.Core.Media;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Windows.Components;

internal sealed class PhotoEditPreview : IDisposable
{
    public const int ProxyMaxDimension = 720;
    private const double RenderDebounceSeconds = 0.04;

    private PixelImage proxy = PixelImage.Empty;
    private IDalamudTextureWrap? texture;
    private PhotoEdit renderedEdit = PhotoEdit.None;
    private bool hasRendered;
    private PhotoEdit pendingEdit = PhotoEdit.None;
    private bool hasPending;
    private double pendingSince;
    private bool rendering;
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
        proxy = PixelImage.Empty;
        ReleaseTexture();
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

    private void FinishRender(int renderGeneration)
    {
        if (renderGeneration == generation)
        {
            rendering = false;
        }
    }

    private void ReleaseTexture()
    {
        var previous = texture;
        texture = null;
        DeferredDispose.Later(previous);
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
        proxy = PixelImage.Empty;
        ReleaseTexture();
    }

    public void Dispose()
    {
        Close();
        cancellation.Dispose();
    }
}
