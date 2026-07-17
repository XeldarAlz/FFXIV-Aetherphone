using System.Collections.Concurrent;
using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Photos;

internal sealed partial class PhotosApp : IPhoneApp
{
    private const int Columns = 3;
    private const int ThumbnailMaxDimension = 256;
    private const long ThumbnailBudgetBytes = 48L * 1024 * 1024;
    private const long FullImageBudgetBytes = 96L * 1024 * 1024;
    private const float SegmentHeight = 34f;

    public string Id => "photos";
    public Vector4 Accent => AppAccents.For(Id);
    public string DisplayName => Loc.T(L.Apps.Photos);
    public string Glyph => "P";
    public int BadgeCount => 0;

    private readonly PhotoLibrary library;
    private readonly ConfirmService confirm;
    private readonly AppSkin ui = new(AppPalettes.Photos);
    private readonly TextureLedger thumbnails = new(ThumbnailBudgetBytes);
    private readonly TextureLedger fullImages = new(FullImageBudgetBytes);
    private readonly ConcurrentDictionary<string, byte> loading = new();
    private readonly ConcurrentDictionary<string, byte> failed = new();
    private readonly CancellationTokenSource cancellation = new();
    private readonly PhotoZoomView zoomView = new();
    private readonly ViewRouter<PhotoView> router;
    private readonly RouterDraw<PhotoView> drawView;
    private readonly Action back;
    private readonly List<MonthAlbum> albums = new();
    private readonly List<GridBand> bands = new();
    private readonly string[] segmentLabels = new string[2];

    private PhotoEntry[] entries = Array.Empty<PhotoEntry>();
    private string[] viewerPaths = Array.Empty<string>();
    private int viewerIndex;
    private int segment;
    private bool resetScroll;
    private PhoneTheme frameTheme = PhoneTheme.Default;
    private INavigator frameNavigation = null!;

    public PhotosApp(PhotoLibrary library, ConfirmService confirm)
    {
        this.library = library;
        this.confirm = confirm;
        router = new ViewRouter<PhotoView>(PhotoView.Grid(), Id);
        drawView = DrawView;
        back = () => router.Pop();
    }

    public void OnOpened()
    {
        router.Reset();
        segment = 0;
        viewerPaths = Array.Empty<string>();
        viewerIndex = 0;
        resetScroll = true;
        Refresh();
    }

    public void OnClosed()
    {
        router.Reset();
    }

    public void Draw(in PhoneContext context)
    {
        frameTheme = context.Theme;
        frameNavigation = context.Navigation;
        ui.Theme = context.Theme;
        if (router.Current.Route == PhotoRoute.Viewer && viewerPaths.Length == 0)
        {
            router.Pop(false);
        }

        var scale = ImGuiHelpers.GlobalScale;
        var screen = SceneChrome.ScreenFrom(context.Content, context.Theme, scale);
        ui.Backdrop(screen);
        router.Draw(context.Content, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
    }

    private void DrawView(PhotoView view, Rect area, int depth)
    {
        if (view.Route == PhotoRoute.Viewer)
        {
            DrawViewer(area);
            return;
        }

        ui.Body(area);
        if (view.Route == PhotoRoute.Album)
        {
            DrawAlbum(area, view.AlbumKey);
            return;
        }

        DrawRoot(area);
    }

    private void DrawNavBar(Rect area, string title, Action? onBack)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowCenterY = area.Min.Y + AppHeader.Height * scale * 0.5f;
        var fitted = Typography.FitText(title, area.Width - 96f * scale, TextStyles.Title3);
        Typography.DrawCentered(new Vector2(area.Center.X, rowCenterY), fitted, ui.TitleInk, TextStyles.Title3);
        if (onBack is null)
        {
            return;
        }

        var hitMin = new Vector2(area.Min.X, area.Min.Y);
        var hitMax = new Vector2(area.Min.X + 46f * scale, area.Min.Y + AppHeader.Height * scale);
        var hovered = UiInteract.Hover(hitMin, hitMax);
        var center = new Vector2(area.Min.X + 17f * scale, rowCenterY);
        if (BackButton.Draw("photos.back", center, 15f * scale, ui.TitleInk, hovered, scale))
        {
            onBack();
        }
    }

    private void Refresh()
    {
        var paths = library.List();
        var built = new PhotoEntry[paths.Length];
        for (var index = 0; index < paths.Length; index++)
        {
            built[index] = new PhotoEntry(paths[index], ResolveTaken(paths[index]));
        }

        Array.Sort(built, static (left, right) => right.Taken.CompareTo(left.Taken));
        entries = built;
        BuildAlbums();
    }

    private void BuildAlbums()
    {
        albums.Clear();
        var index = 0;
        while (index < entries.Length)
        {
            var taken = entries[index].Taken;
            var key = taken.Year * 100 + taken.Month;
            var start = index;
            while (index < entries.Length)
            {
                var next = entries[index].Taken;
                if (next.Year * 100 + next.Month != key)
                {
                    break;
                }

                index++;
            }

            albums.Add(new MonthAlbum(key, new DateTime(taken.Year, taken.Month, 1), start, index - start));
        }
    }

    private string[] SlicePaths(int start, int count)
    {
        var slice = new string[count];
        for (var index = 0; index < count; index++)
        {
            slice[index] = entries[start + index].Path;
        }

        return slice;
    }

    private void OpenViewer(int sliceStart, int sliceCount, int absoluteIndex)
    {
        viewerPaths = SlicePaths(sliceStart, sliceCount);
        viewerIndex = Math.Clamp(absoluteIndex - sliceStart, 0, viewerPaths.Length - 1);
        zoomView.Reset();
        router.Push(PhotoView.Viewer());
    }

    private void AskDelete(string path)
    {
        confirm.Ask(new ConfirmRequest
        {
            Message = Loc.T(L.Photos.DeleteConfirmMessage),
            ConfirmLabel = Loc.T(L.Photos.DeleteConfirm),
            CancelLabel = Loc.T(L.Photos.DeleteCancel),
            Confirm = () => DeletePhoto(path),
        });
    }

    private void DeletePhoto(string path)
    {
        library.Delete(path);
        if (thumbnails.TryRemove(path, out var thumbWrap))
        {
            DisposeLater(thumbWrap);
        }

        if (fullImages.TryRemove(path, out var fullWrap))
        {
            DisposeLater(fullWrap);
        }

        var removedAt = Array.IndexOf(viewerPaths, path);
        if (removedAt >= 0)
        {
            var trimmed = new string[viewerPaths.Length - 1];
            for (var index = 0; index < removedAt; index++)
            {
                trimmed[index] = viewerPaths[index];
            }

            for (var index = removedAt + 1; index < viewerPaths.Length; index++)
            {
                trimmed[index - 1] = viewerPaths[index];
            }

            viewerPaths = trimmed;
        }

        Refresh();
        if (viewerPaths.Length == 0)
        {
            router.Pop(false);
            return;
        }

        viewerIndex = Math.Clamp(removedAt >= 0 ? removedAt : viewerIndex, 0, viewerPaths.Length - 1);
    }

    private static DateTime ResolveTaken(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (name.StartsWith("AEP_", StringComparison.Ordinal) && DateTime.TryParseExact(name.AsSpan(4),
                "yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        try
        {
            return File.GetLastWriteTime(path);
        }
        catch
        {
            return DateTime.Now;
        }
    }

    private static string DayLabel(DateTime taken)
    {
        var day = taken.Date;
        var today = DateTime.Today;
        if (day == today)
        {
            return Loc.T(L.Photos.Today);
        }

        if (day == today.AddDays(-1))
        {
            return Loc.T(L.Photos.Yesterday);
        }

        return taken.ToString("dddd, d MMMM", Loc.Culture);
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

    private readonly struct PhotoEntry
    {
        public readonly string Path;
        public readonly DateTime Taken;

        public PhotoEntry(string path, DateTime taken)
        {
            Path = path;
            Taken = taken;
        }
    }

    private readonly struct MonthAlbum
    {
        public readonly int Key;
        public readonly DateTime Month;
        public readonly int Start;
        public readonly int Count;

        public MonthAlbum(int key, DateTime month, int start, int count)
        {
            Key = key;
            Month = month;
            Start = start;
            Count = count;
        }
    }

    private struct GridBand
    {
        public bool Header;
        public DateTime Day;
        public int DayCount;
        public int PhotoStart;
        public int PhotoCount;
        public float Top;
        public float Height;
    }
}
