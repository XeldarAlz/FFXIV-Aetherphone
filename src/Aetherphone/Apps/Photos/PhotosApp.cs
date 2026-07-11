using System.Collections.Concurrent;
using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
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
    public string Id => "photos";
    public string DisplayName => Loc.T(L.Apps.Photos);
    public string Glyph => "P";
    public int BadgeCount => 0;
    private readonly PhotoLibrary library;
    private readonly ConcurrentDictionary<string, IDalamudTextureWrap> ready = new();
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
            var avail = ImGui.GetContentRegionAvail().X;
            var cell = (avail - gap * (Columns - 1)) / Columns;
            for (var index = 0; index < paths.Length; index++)
            {
                using (ImRaii.PushId(index))
                {
                    var clicked = ImGui.InvisibleButton("cell", new Vector2(cell, cell));
                    PhotosChrome.Thumbnail(Get(paths[index]), ImGui.GetItemRectMin(), ImGui.GetItemRectMax(),
                        ImGui.IsItemHovered(), scale);
                    if (clicked)
                    {
                        viewerIndex = index;
                        zoomView.Reset();
                        router.Push(PhotoRoute.Viewer);
                    }
                }

                if (index % Columns != Columns - 1)
                {
                    ImGui.SameLine();
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
        var texture = Get(path);
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
        if (ready.TryRemove(path, out var wrap))
        {
            wrap.Dispose();
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
            AepLog.Warning($"[Photos] failed to load {path}: {exception.Message}");
        }
        finally
        {
            loading.TryRemove(path, out _);
        }
    }

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
}
