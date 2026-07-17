using System.Collections.Concurrent;
using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Onboarding;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Photos;

internal sealed class PhotosApp : IPhoneApp
{
    private enum PhotoRoute : byte
    {
        Grid,
        Viewer,
    }

    private const int Columns = 3;
    private const int ThumbnailMaxDimension = 256;
    private const long ThumbnailBudgetBytes = 48L * 1024 * 1024;
    private const long FullImageBudgetBytes = 96L * 1024 * 1024;
    public string Id => "photos";
    public string DisplayName => Loc.T(L.Apps.Photos);
    public string Glyph => "P";
    public int BadgeCount => 0;
    private readonly PhotoLibrary library;
    private readonly TextureLedger thumbnails = new(ThumbnailBudgetBytes);
    private readonly TextureLedger fullImages = new(FullImageBudgetBytes);
    private readonly ConcurrentDictionary<string, byte> loading = new();
    private readonly ConcurrentDictionary<string, byte> failed = new();
    private readonly CancellationTokenSource cancellation = new();
    private string[] paths = Array.Empty<string>();
    private int? viewerIndex;
    private readonly PhotoZoomView zoomView = new();
    private readonly ViewRouter<PhotoRoute> router;
    private readonly RouterDraw<PhotoRoute> drawView;
    private PhoneTheme theme = PhoneTheme.Default;
    private INavigator navigation = null!;

    public PhotosApp(PhotoLibrary library)
    {
        this.library = library;
        router = new ViewRouter<PhotoRoute>(PhotoRoute.Grid, Id);
        drawView = DrawView;
    }

    public void OnOpened()
    {
        router.Reset();
        viewerIndex = null;
        Refresh();
    }

    public void OnClosed()
    {
        router.Reset();
    }

    public void Draw(in PhoneContext context)
    {
        theme = context.Theme;
        navigation = context.Navigation;
        if (router.Current == PhotoRoute.Viewer &&
            !(viewerIndex is { } target && target >= 0 && target < paths.Length))
        {
            router.Pop(false);
        }

        router.Draw(context.Content, context.Theme.AppBackground, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(PhotoRoute route, Rect area, int depth)
    {
        var context = new PhoneContext(area, theme, navigation);
        if (route == PhotoRoute.Viewer && viewerIndex is { } index && index >= 0 && index < paths.Length)
        {
            DrawViewer(context, index);
            return;
        }

        DrawGrid(context);
    }

    private void DrawGrid(in PhoneContext context)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;
        Typography.Draw(new Vector2(content.Min.X + 4f * scale, content.Min.Y + 2f * scale), Loc.T(L.Apps.Photos),
            theme.TextStrong, 1.7f, FontWeight.Bold);
        var countLabel = Loc.Plural(L.Photos.Count, paths.Length);
        Typography.Draw(new Vector2(content.Min.X + 4f * scale, content.Min.Y + 34f * scale), countLabel,
            theme.TextMuted, 0.85f);
        var bodyTop = content.Min.Y + 58f * scale;
        var body = new Rect(new Vector2(content.Min.X, bodyTop), content.Max);
        UiAnchors.Report("photos.grid", body);
        if (paths.Length == 0)
        {
            PhotosChrome.Empty(content, theme, scale);
            return;
        }

        ImGui.SetCursorScreenPos(body.Min);
        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(6f * scale, 6f * scale)))
        using (var child = ImRaii.Child("##grid", body.Size, false, ImGuiWindowFlags.NoBackground))
        {
            if (!child)
            {
                return;
            }

            var gap = 6f * scale;
            var avail = ScrollLayout.StableContentWidth();
            var cell = (avail - gap * (Columns - 1)) / Columns;
            var rowCount = (paths.Length + Columns - 1) / Columns;
            var rowStride = cell + gap;
            var scrollY = ImGui.GetScrollY();
            var viewHeight = ImGui.GetWindowSize().Y;
            var firstVisibleRow = Math.Max(0, (int)(scrollY / rowStride) - 1);
            var lastVisibleRow = Math.Min(rowCount - 1, (int)((scrollY + viewHeight) / rowStride) + 1);
            for (var row = 0; row < rowCount; row++)
            {
                if (row < firstVisibleRow || row > lastVisibleRow)
                {
                    ImGui.Dummy(new Vector2(avail, cell));
                    continue;
                }

                for (var column = 0; column < Columns; column++)
                {
                    var index = row * Columns + column;
                    if (index >= paths.Length)
                    {
                        break;
                    }

                    using (ImRaii.PushId(index))
                    {
                        var clicked = ImGui.InvisibleButton("cell", new Vector2(cell, cell));
                        PhotosChrome.Thumbnail(GetThumbnail(paths[index]), ImGui.GetItemRectMin(),
                            ImGui.GetItemRectMax(), ImGui.IsItemHovered(), scale);
                        if (clicked)
                        {
                            viewerIndex = index;
                            zoomView.Reset();
                            router.Push(PhotoRoute.Viewer);
                        }
                    }

                    if (column < Columns - 1 && index + 1 < paths.Length)
                    {
                        ImGui.SameLine();
                    }
                }
            }
        }
    }

    private void DrawViewer(in PhoneContext context, int index)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var theme = context.Theme;
        var content = context.Content;
        var path = paths[index];
        var texture = GetFull(path) ?? thumbnails.Get(path);
        var stage = new Rect(new Vector2(content.Min.X, content.Min.Y + 44f * scale),
            new Vector2(content.Max.X, content.Max.Y - 36f * scale));
        if (texture is not null)
        {
            zoomView.Draw(stage, texture, theme, Metrics.Radius.Sm * scale);
        }
        else
        {
            LoadingPulse.Draw(new Vector2(stage.Center.X, stage.Center.Y - 14f * scale), 13f * scale, theme.Accent,
                theme.TextMuted, Loc.T(L.Common.Loading));
        }

        var backCenter = new Vector2(content.Min.X + 18f * scale, content.Min.Y + 20f * scale);
        var backHovered = ImGui.IsMouseHoveringRect(backCenter - new Vector2(18f * scale, 18f * scale),
            backCenter + new Vector2(18f * scale, 18f * scale));
        if (BackButton.Draw("photos.viewer.back", backCenter, 15f * scale, new Vector4(1f, 1f, 1f, 1f), backHovered,
                scale, shadow: true))
        {
            router.Pop();
            return;
        }

        if (PhotosChrome.Trash(new Vector2(content.Max.X - 18f * scale, content.Min.Y + 20f * scale), theme, scale))
        {
            AskDelete(index);
            return;
        }

        Typography.DrawCentered(new Vector2(content.Center.X, content.Max.Y - 16f * scale),
            $"{index + 1} / {paths.Length}", theme.TextMuted, 0.85f);
        if (paths.Length <= 1)
        {
            return;
        }

        if (PhotosChrome.Arrow(new Vector2(content.Min.X + 16f * scale, content.Center.Y), theme.TextStrong, true,
                scale))
        {
            viewerIndex = (index - 1 + paths.Length) % paths.Length;
            zoomView.Reset();
        }

        if (PhotosChrome.Arrow(new Vector2(content.Max.X - 16f * scale, content.Center.Y), theme.TextStrong, false,
                scale))
        {
            viewerIndex = (index + 1) % paths.Length;
            zoomView.Reset();
        }
    }

    private void AskDelete(int index)
    {
        Plugin.Confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Photos.DeleteConfirmMessage),
            ConfirmLabel = Loc.T(L.Photos.DeleteConfirm),
            CancelLabel = Loc.T(L.Photos.DeleteCancel),
            Confirm = () => DeletePhoto(index),
        });
    }

    private void DeletePhoto(int index)
    {
        var path = paths[index];
        library.Delete(path);
        if (thumbnails.TryRemove(path, out var thumbWrap))
        {
            DisposeLater(thumbWrap);
        }

        if (fullImages.TryRemove(path, out var fullWrap))
        {
            DisposeLater(fullWrap);
        }

        Refresh();
        if (paths.Length == 0)
        {
            viewerIndex = null;
            router.Pop(false);
            return;
        }

        viewerIndex = Math.Clamp(index, 0, paths.Length - 1);
    }

    private void Refresh()
    {
        paths = library.List();
    }

    private IDalamudTextureWrap? GetThumbnail(string path)
    {
        if (thumbnails.Get(path) is { } wrap)
        {
            return wrap;
        }

        if (failed.ContainsKey(path) || !loading.TryAdd("thumb:" + path, 0))
        {
            return null;
        }

        _ = LoadThumbnailAsync(path);
        return null;
    }

    private IDalamudTextureWrap? GetFull(string path)
    {
        if (fullImages.Get(path) is { } wrap)
        {
            return wrap;
        }

        if (failed.ContainsKey(path) || !loading.TryAdd("full:" + path, 0))
        {
            return null;
        }

        _ = LoadFullAsync(path);
        return null;
    }

    private async Task LoadThumbnailAsync(string path)
    {
        try
        {
            var token = cancellation.Token;
            var thumbnailPath = library.ThumbnailPathFor(path);
            byte[] bytes;
            if (File.Exists(thumbnailPath) && File.GetLastWriteTimeUtc(thumbnailPath) >= File.GetLastWriteTimeUtc(path))
            {
                bytes = await File.ReadAllBytesAsync(thumbnailPath, token).ConfigureAwait(false);
            }
            else
            {
                bytes = ImageProcessor.BakeJpeg(path, ThumbnailMaxDimension).Bytes;
                Directory.CreateDirectory(Path.GetDirectoryName(thumbnailPath)!);
                await File.WriteAllBytesAsync(thumbnailPath, bytes, token).ConfigureAwait(false);
            }

            var wrap = await Plugin.TextureProvider.CreateFromImageAsync(bytes, "thumb:" + path, token)
                .ConfigureAwait(false);
            if (!thumbnails.TryAdd(path, wrap))
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
            AepLog.Warning($"[Photos] thumbnail failed for {Path.GetFileName(path)}: {exception.Message}");
        }
        finally
        {
            loading.TryRemove("thumb:" + path, out _);
        }
    }

    private async Task LoadFullAsync(string path)
    {
        try
        {
            var token = cancellation.Token;
            var bytes = await File.ReadAllBytesAsync(path, token).ConfigureAwait(false);
            var wrap = await Plugin.TextureProvider.CreateFromImageAsync(bytes, path, token).ConfigureAwait(false);
            if (!fullImages.TryAdd(path, wrap))
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
            AepLog.Warning($"[Photos] failed to load {Path.GetFileName(path)}: {exception.Message}");
        }
        finally
        {
            loading.TryRemove("full:" + path, out _);
        }
    }

    private static void DisposeLater(IDalamudTextureWrap wrap)
    {
        _ = Task.Delay(TimeSpan.FromSeconds(1))
            .ContinueWith(_ => Plugin.Framework.RunOnFrameworkThread(wrap.Dispose), TaskScheduler.Default);
    }

    public void Dispose()
    {
        cancellation.Cancel();
        thumbnails.DisposeAll();
        fullImages.DisposeAll();
        cancellation.Dispose();
    }
}
