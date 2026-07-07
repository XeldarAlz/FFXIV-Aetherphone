using System.Collections.Concurrent;
using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Core.Photos;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Windows.Widgets;

internal sealed class PhotosWidget : IHomeWidget
{
    private const float ListRefreshSeconds = 20f;
    private const float SlideSeconds = 12f;
    private const float FadeSeconds = 0.8f;

    private readonly PhotoLibrary library;
    private readonly ConcurrentDictionary<string, IDalamudTextureWrap> ready = new();
    private readonly ConcurrentDictionary<string, byte> loading = new();
    private readonly ConcurrentDictionary<string, byte> failed = new();
    private readonly CancellationTokenSource cancellation = new();
    private string[] paths = Array.Empty<string>();
    private float sinceListRefresh = ListRefreshSeconds;
    private float sinceSlide;
    private float fade = 1f;
    private int index;
    private int previousIndex = -1;

    public PhotosWidget(PhotoLibrary library)
    {
        this.library = library;
    }

    public string Id => "photos.shuffle";
    public string DisplayName => Loc.T(L.Apps.Photos);
    public string AppId => "photos";
    public WidgetSizeSet Sizes => WidgetSizeSet.Small | WidgetSizeSet.Medium | WidgetSizeSet.Large;

    public void Draw(in WidgetContext context)
    {
        Advance(context.Delta);
        WidgetChrome.Card(context.DrawList, context.Bounds, context.Scale, context.Opacity);
        if (paths.Length == 0)
        {
            DrawEmpty(context);
            return;
        }

        var radius = WidgetChrome.Radius(context.Scale) * 0.95f;
        if (previousIndex >= 0 && fade < 1f && previousIndex < paths.Length)
        {
            DrawPhoto(context, paths[previousIndex], radius, context.Opacity);
        }

        if (index < paths.Length)
        {
            DrawPhoto(context, paths[index], radius, context.Opacity * (previousIndex >= 0 ? fade : 1f));
        }

        Material.EdgeSquircle(context.DrawList, context.Bounds.Min, context.Bounds.Max,
            WidgetChrome.Radius(context.Scale), context.Scale, context.Opacity);
    }

    private void Advance(float delta)
    {
        sinceListRefresh += delta;
        if (sinceListRefresh >= ListRefreshSeconds || paths.Length == 0 && sinceListRefresh >= 5f)
        {
            sinceListRefresh = 0f;
            paths = library.List();
            if (index >= paths.Length)
            {
                index = 0;
                previousIndex = -1;
            }
        }

        if (paths.Length < 2)
        {
            return;
        }

        sinceSlide += delta;
        if (fade < 1f)
        {
            fade = MathF.Min(1f, fade + delta / FadeSeconds);
        }

        if (sinceSlide < SlideSeconds)
        {
            return;
        }

        sinceSlide = 0f;
        previousIndex = index;
        index = (index + 1) % paths.Length;
        fade = 0f;
        EvictDistant();
    }

    private void DrawPhoto(in WidgetContext context, string path, float radius, float alpha)
    {
        if (Get(path) is not { } wrap)
        {
            return;
        }

        var bounds = context.Bounds;
        var (uv0, uv1) = ImageFit.Cover(wrap.Width, wrap.Height, bounds.Width, bounds.Height);
        var tint = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha));
        context.DrawList.AddImageRounded(wrap.Handle, bounds.Min, bounds.Max, uv0, uv1, tint, radius,
            ImDrawFlags.RoundCornersAll);
    }

    private void DrawEmpty(in WidgetContext context)
    {
        var bounds = context.Bounds;
        var scale = context.Scale;
        AppIconArt.TryDraw(context.DrawList, "photos", bounds.Center - new Vector2(0f, 8f * scale), 34f * scale,
            context.Theme.TextMuted, new Vector4(0f, 0f, 0f, 0f));
        Typography.DrawCentered(context.DrawList, new Vector2(bounds.Center.X, bounds.Center.Y + 20f * scale),
            Loc.T(L.Photos.NoPhotos), Palette.WithAlpha(context.Theme.TextMuted, context.Opacity),
            TextStyles.Caption1);
    }

    private IDalamudTextureWrap? Get(string path)
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
            AepLog.Warning($"[PhotosWidget] failed to load {path}: {exception.Message}");
        }
        finally
        {
            loading.TryRemove(path, out _);
        }
    }

    private void EvictDistant()
    {
        if (ready.Count <= 4 || paths.Length == 0)
        {
            return;
        }

        var keep = new HashSet<string>(4)
        {
            paths[index],
            paths[(index + 1) % paths.Length],
        };
        if (previousIndex >= 0 && previousIndex < paths.Length)
        {
            keep.Add(paths[previousIndex]);
        }

        foreach (var pair in ready)
        {
            if (!keep.Contains(pair.Key) && ready.TryRemove(pair.Key, out var wrap))
            {
                wrap.Dispose();
            }
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
        foreach (var pair in ready)
        {
            pair.Value.Dispose();
        }

        ready.Clear();
    }
}
