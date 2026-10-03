using System.Diagnostics;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;

namespace Aetherphone.Core.Shell;

internal sealed class LiveBackdrop : IDisposable
{
    private const int FrameTolerance = 1;
    private const int IdleFramesBeforeRelease = 120;
    private const int LevelCount = LiveBackdropPlan.Depth + 1;
    private const int SmoothIndex = LiveBackdropPlan.Depth;
    private const uint OpaqueWhite = 0xFFFFFFFFu;
    private const uint WhiteChannels = 0x00FFFFFFu;
    private const string CaptureDebugName = "Aetherphone.LiveBackdrop.Capture";

    private readonly ITextureProvider textures;
    private readonly Configuration configuration;
    private readonly CancellationTokenSource cancellation = new();
    private readonly IDrawListTextureWrap?[] levels = new IDrawListTextureWrap?[LevelCount];
    private IDalamudTextureWrap? capture;
    private IDalamudTextureWrap? arrivedCapture;
    private LiveGlassSource captureSource;
    private LiveGlassSource arrivedSource;
    private bool captureRequested;
    private ImDrawListPtr drawList;
    private bool drawListOwned;
    private volatile bool faulted;
    private Rect requestScreen;
    private Vector4 requestBackground;
    private int requestFrame = -1;
    private int surfaceRequestFrame = -1;
    private int lastWantedFrame = -1;
    private Rect passScreen;
    private int passFrame = -1;

    public LiveBackdrop(ITextureProvider textures, Configuration configuration)
    {
        this.textures = textures;
        this.configuration = configuration;
    }

    public static double LastPassMilliseconds { get; private set; }

    public static Vector2 LastCaptureSize { get; private set; }

    public bool Faulted => faulted;

    public LiveGlassSource? ActiveSource => capture is null ? null : captureSource;

    public bool WorldSourceWanted =>
        !faulted && configuration.LiveGlass && configuration.LiveGlassSource == LiveGlassSource.World;

    public void Prepare()
    {
        if (faulted || !configuration.LiveGlass)
        {
            Release();
            return;
        }

        var frame = ImGui.GetFrameCount();
        var surfaceWanted = frame - surfaceRequestFrame <= FrameTolerance;
        var wanted = frame - requestFrame <= FrameTolerance && (WallpaperBackdrop.Requested || surfaceWanted);
        if (!wanted)
        {
            if (lastWantedFrame >= 0 && frame - lastWantedFrame > IdleFramesBeforeRelease)
            {
                Release();
            }

            return;
        }

        lastWantedFrame = frame;
        try
        {
            AdoptArrivedCapture();
            EnsureCapture();
            if (capture is not { Width: > 0, Height: > 0 } source)
            {
                return;
            }

            RunPasses(source, frame);
        }
        catch (Exception exception)
        {
            Fault(exception);
        }
    }

    public void Record(Rect screen, Vector4 background)
    {
        if (faulted || !configuration.LiveGlass)
        {
            return;
        }

        requestBackground = background;
        Request(screen);
        if (FreshPass() is not { } smooth)
        {
            return;
        }

        WallpaperBackdrop.Record(passScreen, smooth.Handle, Vector2.Zero, Vector2.One, null);
    }

    public bool TryRecordFor(Rect rect)
    {
        if (!WorldSourceWanted)
        {
            return false;
        }

        var scale = UiScale.Current;
        surfaceRequestFrame = ImGui.GetFrameCount();
        Request(LiveBackdropPlan.Pad(rect, LiveBackdropPlan.RegionPadding * scale));
        if (FreshPass() is not { } smooth)
        {
            return false;
        }

        if (!LiveBackdropPlan.Covers(passScreen, LiveBackdropPlan.Pad(rect, LiveBackdropPlan.SampleReach * scale)))
        {
            return false;
        }

        WallpaperBackdrop.Record(passScreen, smooth.Handle, Vector2.Zero, Vector2.One, null);
        return true;
    }

    private void Request(Rect region)
    {
        var frame = ImGui.GetFrameCount();
        requestScreen = frame == requestFrame ? LiveBackdropPlan.Union(requestScreen, region) : region;
        requestFrame = frame;
    }

    private IDrawListTextureWrap? FreshPass() =>
        ImGui.GetFrameCount() - passFrame > FrameTolerance ? null : levels[SmoothIndex];

    public void Release()
    {
        capture?.Dispose();
        capture = null;
        Interlocked.Exchange(ref arrivedCapture, null)?.Dispose();
        captureRequested = false;
        for (var index = 0; index < LevelCount; index++)
        {
            levels[index]?.Dispose();
            levels[index] = null;
        }

        passFrame = -1;
        lastWantedFrame = -1;
    }

    public void Dispose()
    {
        cancellation.Cancel();
        Release();
        if (drawListOwned)
        {
            drawList.Destroy();
            drawListOwned = false;
        }

        cancellation.Dispose();
    }

    private void AdoptArrivedCapture()
    {
        var arrived = Interlocked.Exchange(ref arrivedCapture, null);
        if (arrived is null)
        {
            return;
        }

        captureRequested = false;
        capture?.Dispose();
        capture = arrived;
        captureSource = arrivedSource;
    }

    private void EnsureCapture()
    {
        var source = configuration.LiveGlassSource;
        if (capture is not null && captureSource != source)
        {
            capture.Dispose();
            capture = null;
        }

        if (capture is not null || captureRequested)
        {
            return;
        }

        captureRequested = true;
        var args = new ImGuiViewportTextureArgs
        {
            ViewportId = ImGui.GetMainViewport().ID,
            AutoUpdate = true,
            TakeBeforeImGuiRender = source == LiveGlassSource.World,
            KeepTransparency = false,
        };
        _ = CreateCaptureAsync(args, source);
    }

    private async Task CreateCaptureAsync(ImGuiViewportTextureArgs args, LiveGlassSource source)
    {
        try
        {
            var wrap = await textures.CreateFromImGuiViewportAsync(args, CaptureDebugName, cancellation.Token)
                .ConfigureAwait(false);
            arrivedSource = source;
            Interlocked.Exchange(ref arrivedCapture, wrap)?.Dispose();
            if (cancellation.IsCancellationRequested)
            {
                Interlocked.Exchange(ref arrivedCapture, null)?.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Fault(exception);
        }
    }

    private void RunPasses(IDalamudTextureWrap source, int frame)
    {
        var viewport = ImGui.GetMainViewport();
        var viewportRect = new Rect(viewport.Pos, viewport.Pos + viewport.Size);
        var window = LiveBackdropPlan.Window(requestScreen, viewportRect);
        if (!window.Visible)
        {
            return;
        }

        var captureSize = new Vector2(source.Width, source.Height);
        var regionPixels = requestScreen.Size * (captureSize / viewportRect.Size);
        var started = Stopwatch.GetTimestamp();
        EnsureLevels(regionPixels);
        EnsureDrawList();
        var first = levels[0]!;
        first.ClearColor = requestBackground with { W = 1f };
        Blit(source.Handle, first, in window, PackWhite(LiveBackdropPlan.FeedbackBlend(captureSource)));
        for (var level = 1; level < LiveBackdropPlan.Depth; level++)
        {
            Blit(levels[level - 1]!.Handle, levels[level]!, in CaptureWindow.Full, OpaqueWhite);
        }

        Blit(levels[LiveBackdropPlan.Depth - 1]!.Handle, levels[SmoothIndex]!, in CaptureWindow.Full, OpaqueWhite);
        LastPassMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        LastCaptureSize = captureSize;
        passScreen = requestScreen;
        passFrame = frame;
    }

    private void EnsureLevels(Vector2 regionPixels)
    {
        for (var level = 0; level < LiveBackdropPlan.Depth; level++)
        {
            var (width, height) = LiveBackdropPlan.LevelSize(regionPixels, level);
            EnsureLevel(level, width, height);
        }

        var (smoothWidth, smoothHeight) = LiveBackdropPlan.SmoothSize(regionPixels);
        EnsureLevel(SmoothIndex, smoothWidth, smoothHeight);
    }

    private void EnsureLevel(int index, int width, int height)
    {
        var wrap = levels[index] ??= textures.CreateDrawListTexture($"Aetherphone.LiveBackdrop.Level{index}");
        if (wrap.Width == width && wrap.Height == height)
        {
            return;
        }

        wrap.Size = new Vector2(width, height);
    }

    private void EnsureDrawList()
    {
        if (drawListOwned)
        {
            return;
        }

        drawList = ImGui.ImDrawList(ImGui.GetDrawListSharedData());
        drawListOwned = true;
    }

    private void Blit(ImTextureID source, IDrawListTextureWrap target, in CaptureWindow window, uint tint)
    {
        var size = target.Size;
        drawList._ResetForNewFrame();
        drawList.PushClipRect(Vector2.Zero, size, false);
        drawList.AddImage(source, window.DestinationMin * size, window.DestinationMax * size, window.Uv0, window.Uv1,
            tint);
        drawList.PopClipRect();
        drawList._PopUnusedDrawCmd();
        target.Draw(drawList, Vector2.Zero, Vector2.One);
    }

    private void Fault(Exception exception)
    {
        if (faulted)
        {
            return;
        }

        faulted = true;
        AepLog.Warning(exception, "[LiveBackdrop] live glass disabled for this session");
    }

    private static uint PackWhite(float alpha)
    {
        var channel = (uint)Math.Clamp((int)MathF.Round(alpha * 255f), 0, 255);
        return (channel << 24) | WhiteChannels;
    }
}
