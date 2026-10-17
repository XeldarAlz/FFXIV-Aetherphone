using System.Collections.Concurrent;
using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
using Aetherphone.Core.Net;
using Aetherphone.Core.Photos;
using Aetherphone.Core.Sharing;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.Photos;

internal sealed partial class PhotosApp : IPhoneApp
{
    private const int DefaultColumns = 3;
    private const int MinColumns = 2;
    private const int MaxColumns = 5;
    private const long ThumbnailBudgetBytes = 48L * 1024 * 1024;
    private const long FullImageBudgetBytes = 96L * 1024 * 1024;
    private const int MaxConcurrentDecodes = 3;
    private const int ThumbLoad = 0;
    private const int FullLoad = 1;
    private const int CoverLoad = 2;

    public string Id => "photos";
    public Vector4 Accent => AppAccents.For(Id);
    public string DisplayName => Loc.T(L.Apps.Photos);
    public string Glyph => "P";
    public int BadgeCount => 0;
    public bool WantsSystemTheme => true;

    private readonly PhotoLibrary library;
    private readonly ConfirmService confirm;
    private readonly ShareService share;
    private readonly Configuration configuration;
    private readonly AppSkin ui = new(AppPalettes.PhotosThemed(PhoneTheme.Default));
    private readonly TextureLedger thumbnails = new(ThumbnailBudgetBytes);
    private readonly TextureLedger fullImages = new(FullImageBudgetBytes);
    private readonly ConcurrentDictionary<(string Path, int Kind), byte> loading = new();
    private readonly ConcurrentDictionary<string, byte> failed = new();
    private readonly RequestThrottle decodeSlots = new(MaxConcurrentDecodes, TimeSpan.Zero);
    private readonly CancellationTokenSource cancellation = new();
    private readonly ViewRouter<PhotoView> router;
    private readonly RouterDraw<PhotoView> drawView;
    private readonly Action back;
    private readonly Action popOnly;
    private readonly Dictionary<string, long> sizeCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> pixelCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Comparison<PhotoEntry> compareEntries;
    private readonly Comparison<string> comparePaths;
    private readonly List<PhotoRun> monthRuns = new();
    private readonly List<PhotoRun> yearRuns = new();
    private readonly List<int> runKeys = new();
    private MonthAlbum[] monthAlbums = Array.Empty<MonthAlbum>();
    private PhotoEntry[] entries = Array.Empty<PhotoEntry>();
    private PhotoEntry[] filteredEntries = Array.Empty<PhotoEntry>();
    private string[] filteredPaths = Array.Empty<string>();
    private string[] recentPaths = Array.Empty<string>();
    private bool scanningDimensions;
    private int seenLibraryVersion;
    private string pendingLaunch = string.Empty;
    private int launchCheckedVersion = -1;
    private PhotosTab activeTab;
    private PhoneTheme frameTheme = PhoneTheme.Default;
    private INavigator frameNavigation = null!;
    private Rect frameScreen;

    public PhotosApp(PhotoLibrary library, ConfirmService confirm, ShareService share, Configuration configuration)
    {
        this.library = library;
        this.confirm = confirm;
        this.share = share;
        this.configuration = configuration;
        router = new ViewRouter<PhotoView>(PhotoView.Grid());
        drawView = DrawView;
        drawNameSheet = DrawNameSheetContent;
        compareEntries = CompareEntries;
        comparePaths = ComparePaths;
        popOnly = () => router.Pop();
        back = () =>
        {
            EndSelect();
            router.Pop();
        };
        LoadCustomAlbums();
        LoadFavorites();
    }

    public void OnOpened()
    {
        router.Reset();
        activeTab = (PhotosTab)Math.Clamp(configuration.PhotosSegment, 0, (int)PhotosTab.Collections);
        viewerPaths = Array.Empty<string>();
        viewerIndex = 0;
        viewerInTrash = false;
        addTargets = Array.Empty<string>();
        pendingJump = JumpTarget.None;
        monthsRail.Reset();
        placesRail.Reset();
        EndSelect();
        CloseSheets();
        LoadCustomAlbums();
        LoadFavorites();
        Refresh();
    }

    public void OnClosed()
    {
        editSession.Close();
        CloseSheets();
        router.Reset();
    }

    private void CloseSheets()
    {
        albumSheet.Close();
        photoSheet.Close();
        nameSheet.Close();
        sortMenu.Close();
        trashSheet.Close();
        viewerMenu.Close();
        infoSheet.CloseImmediately();
    }

    public void Draw(in PhoneContext context)
    {
        frameTheme = context.Theme;
        frameNavigation = context.Navigation;
        ui.Theme = context.Theme;
        ui.Palette = AppPalettes.PhotosThemed(context.Theme);
        SyncLabels();
        if (library.Version != seenLibraryVersion)
        {
            failed.Clear();
            Refresh();
        }

        ConsumeLaunch();
        if (router.Current.Route == PhotoRoute.Viewer && viewerPaths.Length == 0)
        {
            router.Pop(false);
        }

        albumSheet.Gate();
        photoSheet.Gate();
        sortMenu.Gate();
        trashSheet.Gate();
        viewerMenu.Gate();

        var scale = UiScale.Current;
        var screen = SceneChrome.ScreenFrom(context.Content, context.Theme, scale);
        frameScreen = screen;
        ui.Backdrop(screen);
        using (InputShield.Engage(nameSheet.CapturesPointer || infoSheet.CapturesPointer))
        {
            router.Draw(screen, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        }

        DrawSortMenu(screen);
        DrawViewerMenu(screen);
        DrawAlbumSheet(screen);
        DrawPhotoSheet(screen);
        DrawTrashSheet(screen);
        var sheetArea = AppAreaWithin(screen);
        DrawNameSheet(sheetArea);
        DrawInfoSheet(screen);
    }

    private void DrawView(PhotoView view, Rect area, int depth)
    {
        if (view.Route == PhotoRoute.Viewer)
        {
            DrawViewer(area);
            return;
        }

        if (view.Route == PhotoRoute.Editor)
        {
            DrawEditor(area);
            return;
        }

        ui.Body(area);
        var context = new PhoneContext(ContentWithin(area), frameTheme, frameNavigation);
        switch (view.Route)
        {
            case PhotoRoute.AlbumPicker:
                DrawAlbumPicker(context, area, view.AlbumKey);
                return;
            case PhotoRoute.AddToAlbum:
                DrawAddToAlbumPage(context);
                return;
            case PhotoRoute.Album:
                DrawAlbum(context, area, view.AlbumKey);
                return;
            default:
                DrawRoot(context, area);
                return;
        }
    }

    private Rect ContentWithin(Rect screen)
    {
        var scale = UiScale.Current;
        var min = new Vector2(screen.Min.X + frameTheme.SidePadding * scale,
            screen.Min.Y + frameTheme.TopZoneHeight * scale);
        var max = new Vector2(screen.Max.X - frameTheme.SidePadding * scale,
            screen.Max.Y - frameTheme.BottomZoneHeight * scale);
        return new Rect(min, max);
    }

    private Rect AppAreaWithin(Rect screen)
    {
        var scale = UiScale.Current;
        return new Rect(new Vector2(screen.Min.X, screen.Min.Y + frameTheme.TopZoneHeight * scale),
            new Vector2(screen.Max.X, screen.Max.Y - frameTheme.BottomZoneHeight * scale));
    }

    private void ConsumeLaunch()
    {
        if (library.TryConsumeOpen(out var requested))
        {
            pendingLaunch = requested;
            launchCheckedVersion = -1;
        }

        if (pendingLaunch.Length == 0 || launchCheckedVersion == seenLibraryVersion)
        {
            return;
        }

        launchCheckedVersion = seenLibraryVersion;

        var index = IndexOf(filteredPaths, pendingLaunch);
        var source = filteredPaths;
        if (index < 0)
        {
            index = IndexOf(recentPaths, pendingLaunch);
            source = recentPaths;
        }

        if (index < 0)
        {
            return;
        }

        var target = pendingLaunch;
        pendingLaunch = string.Empty;
        if (router.Current.Route == PhotoRoute.Viewer && viewerIndex < viewerPaths.Length &&
            string.Equals(viewerPaths[viewerIndex], target, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        EndSelect();
        router.Reset();
        activeTab = PhotosTab.Library;
        ShowViewer(source, index, false, false);
    }

    private static int IndexOf(string[] paths, string path)
    {
        for (var index = 0; index < paths.Length; index++)
        {
            if (string.Equals(paths[index], path, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private void Refresh()
    {
        seenLibraryVersion = library.Version;
        var paths = library.List();
        var pathSet = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
        var built = new PhotoEntry[paths.Length];
        for (var index = 0; index < paths.Length; index++)
        {
            built[index] = MetadataFor(paths[index]);
        }

        Array.Sort(built, compareEntries);
        entries = built;
        recentPaths = SlicePaths(built, 0, built.Length);
        if (SortKey == PhotoSortKey.Dimensions)
        {
            ScanMissingDimensions(paths);
        }

        var pruned = PruneCustomAlbumPaths(pathSet);
        BuildCustomAlbums();
        if (pruned)
        {
            SaveCustomAlbums();
        }

        if (PruneFavorites(pathSet))
        {
            SaveFavorites();
        }

        BuildFavorites();
        BuildMonthAlbums();
        ApplyFilter();
        library.PurgeExpired();
        RefreshTrash();
        PrunePlaces(paths);
        BuildPlaces();
        InvalidateLayouts();
    }

    private PhotoSortKey SortKey =>
        (PhotoSortKey)Math.Clamp(configuration.PhotosSortKey, 0, (int)PhotoSortKey.Dimensions);

    private PhotoFilter Filter => (PhotoFilter)Math.Clamp(configuration.PhotosFilter, 0, (int)PhotoFilter.NotInAlbum);

    private int Columns => Math.Clamp(configuration.PhotosGridColumns == 0 ? DefaultColumns : configuration.PhotosGridColumns,
        MinColumns, MaxColumns);

    private bool SortedByDate => SortKey == PhotoSortKey.Date;

    private void ApplyFilter()
    {
        var filter = Filter;
        if (filter == PhotoFilter.All)
        {
            filteredEntries = entries;
        }
        else if (filter == PhotoFilter.Favorites)
        {
            var kept = new List<PhotoEntry>(entries.Length);
            for (var index = 0; index < entries.Length; index++)
            {
                if (favorites.Contains(entries[index].Path))
                {
                    kept.Add(entries[index]);
                }
            }

            filteredEntries = kept.ToArray();
        }
        else
        {
            var inAlbums = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var photos in customAlbumPhotos.Values)
            {
                inAlbums.UnionWith(photos);
            }

            var kept = new List<PhotoEntry>(entries.Length);
            for (var index = 0; index < entries.Length; index++)
            {
                if (!inAlbums.Contains(entries[index].Path))
                {
                    kept.Add(entries[index]);
                }
            }

            filteredEntries = kept.ToArray();
        }

        filteredPaths = SlicePaths(filteredEntries, 0, filteredEntries.Length);
        BuildRuns();
        InvalidateLayouts();
    }

    private void BuildMonthAlbums()
    {
        var keys = new uint[entries.Length];
        for (var index = 0; index < entries.Length; index++)
        {
            keys[index] = (uint)PhotoGrouping.MonthKey(entries[index].Taken);
        }

        var buckets = PhotoGrouping.Buckets(keys);
        var built = new MonthAlbum[buckets.Length];
        for (var bucketIndex = 0; bucketIndex < buckets.Length; bucketIndex++)
        {
            var indices = buckets[bucketIndex].Indices;
            var paths = new string[indices.Length];
            for (var index = 0; index < indices.Length; index++)
            {
                paths[index] = entries[indices[index]].Path;
            }

            built[bucketIndex] = new MonthAlbum((int)buckets[bucketIndex].Key, paths);
        }

        Array.Sort(built, static (left, right) => right.Key.CompareTo(left.Key));
        monthAlbums = built;
    }

    private void BuildRuns()
    {
        runKeys.Clear();
        for (var index = 0; index < filteredEntries.Length; index++)
        {
            runKeys.Add(PhotoGrouping.MonthKey(filteredEntries[index].Taken));
        }

        PhotoGrouping.Runs(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(runKeys), monthRuns);
        for (var index = 0; index < runKeys.Count; index++)
        {
            runKeys[index] = filteredEntries[index].Taken.Year;
        }

        PhotoGrouping.Runs(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(runKeys), yearRuns);
    }

    private PhotoEntry MetadataFor(string path) =>
        new(path, ResolveTaken(path), SizeOf(path), pixelCache.TryGetValue(path, out var pixels) ? pixels : 0L);

    private long SizeOf(string path)
    {
        if (sizeCache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        if (SortKey != PhotoSortKey.Size)
        {
            return 0L;
        }

        long size;
        try
        {
            size = new FileInfo(path).Length;
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[Photos] could not read the size of {Path.GetFileName(path)}");
            size = 0L;
        }

        sizeCache[path] = size;
        return size;
    }

    private void ScanMissingDimensions(string[] paths)
    {
        if (scanningDimensions)
        {
            return;
        }

        var missing = new List<string>();
        for (var index = 0; index < paths.Length; index++)
        {
            if (!pixelCache.ContainsKey(paths[index]))
            {
                missing.Add(paths[index]);
            }
        }

        if (missing.Count == 0)
        {
            return;
        }

        scanningDimensions = true;
        _ = ScanDimensionsAsync(missing);
    }

    private async Task ScanDimensionsAsync(List<string> paths)
    {
        try
        {
            await Task.Run(() =>
            {
                for (var index = 0; index < paths.Count; index++)
                {
                    if (cancellation.IsCancellationRequested)
                    {
                        return;
                    }

                    try
                    {
                        var (width, height) = ImageProcessor.IdentifyDimensions(paths[index]);
                        pixelCache[paths[index]] = (long)width * height;
                    }
                    catch (Exception exception)
                    {
                        pixelCache[paths[index]] = 0L;
                        AepLog.Warning(exception,
                            $"[Photos] could not read the dimensions of {Path.GetFileName(paths[index])}");
                    }
                }
            }, cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await Plugin.Framework.RunOnFrameworkThread(() =>
        {
            scanningDimensions = false;
            if (SortKey == PhotoSortKey.Dimensions)
            {
                Refresh();
            }
        }).ConfigureAwait(false);
    }

    private int CompareEntries(PhotoEntry left, PhotoEntry right)
    {
        var result = SortKey switch
        {
            PhotoSortKey.Name => Path.GetFileName(left.Path.AsSpan())
                .CompareTo(Path.GetFileName(right.Path.AsSpan()), StringComparison.OrdinalIgnoreCase),
            PhotoSortKey.Size => left.Size.CompareTo(right.Size),
            PhotoSortKey.Dimensions => left.Pixels.CompareTo(right.Pixels),
            _ => 0,
        };
        if (result == 0)
        {
            result = left.Taken.CompareTo(right.Taken);
        }

        return configuration.PhotosSortAscending ? result : -result;
    }

    private int ComparePaths(string left, string right) => CompareEntries(MetadataFor(left), MetadataFor(right));

    private static bool DefaultAscending(PhotoSortKey key) => key == PhotoSortKey.Name;

    private static string[] SlicePaths(PhotoEntry[] source, int start, int count)
    {
        var slice = new string[count];
        for (var index = 0; index < count; index++)
        {
            slice[index] = source[start + index].Path;
        }

        return slice;
    }

    private static DateTime ResolveTaken(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (name.StartsWith("AEP_", StringComparison.Ordinal) && name.Length >= 23 &&
            DateTime.TryParseExact(name.AsSpan(4, 19), "yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        try
        {
            return File.GetLastWriteTime(path);
        }
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[Photos] could not read the timestamp of {Path.GetFileName(path)}");
            return DateTime.Now;
        }
    }

    private IDalamudTextureWrap? GetThumbnail(string path)
    {
        if (thumbnails.Get(path) is { } wrap)
        {
            return wrap;
        }

        if (failed.ContainsKey(path) || !loading.TryAdd((path, ThumbLoad), 0))
        {
            return null;
        }

        QueueLoad(path, ThumbLoad);
        return null;
    }

    private IDalamudTextureWrap? GetFull(string path)
    {
        if (fullImages.Get(path) is { } wrap)
        {
            return wrap;
        }

        if (failed.ContainsKey(path) || !loading.TryAdd((path, FullLoad), 0))
        {
            return null;
        }

        QueueLoad(path, FullLoad);
        return null;
    }

    private void QueueLoad(string path, int kind) => _ = Task.Run(() => kind switch
    {
        ThumbLoad => LoadThumbnailAsync(path),
        FullLoad => LoadFullAsync(path),
        _ => LoadCoverAsync(path),
    });

    private async Task LoadThumbnailAsync(string path)
    {
        try
        {
            var token = cancellation.Token;
            using var slot = await decodeSlots.EnterAsync(token).ConfigureAwait(false);
            var bytes = await library.ThumbnailBytesAsync(path, token).ConfigureAwait(false);
            var wrap = await ImageProcessor.DecodeToTextureAsync(Plugin.TextureProvider, bytes, "thumb:" + path,
                ImageProcessor.MaxDecodePixels, token).ConfigureAwait(false);
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
            AepLog.Warning(exception, $"[Photos] thumbnail failed for {Path.GetFileName(path)}");
        }
        finally
        {
            loading.TryRemove((path, ThumbLoad), out _);
        }
    }

    private async Task LoadFullAsync(string path)
    {
        try
        {
            var token = cancellation.Token;
            var bytes = await File.ReadAllBytesAsync(path, token).ConfigureAwait(false);
            var wrap = await ImageProcessor.DecodeToTextureAsync(Plugin.TextureProvider, bytes, path,
                ImageProcessor.MaxLocalDecodePixels, token).ConfigureAwait(false);
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
            AepLog.Warning(exception, $"[Photos] failed to load {Path.GetFileName(path)}");
        }
        finally
        {
            loading.TryRemove((path, FullLoad), out _);
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        editSession.Dispose();
        thumbnails.DisposeAll();
        fullImages.DisposeAll();
        covers.DisposeAll();
        cancellation.Dispose();
    }

    private enum PhotoSortKey : byte
    {
        Date,
        Name,
        Size,
        Dimensions,
    }

    private enum PhotoFilter : byte
    {
        All,
        Favorites,
        NotInAlbum,
    }

    private readonly struct MonthAlbum
    {
        public readonly int Key;
        public readonly string[] Paths;

        public MonthAlbum(int key, string[] paths)
        {
            Key = key;
            Paths = paths;
        }
    }

    private readonly struct PhotoEntry
    {
        public readonly string Path;
        public readonly DateTime Taken;
        public readonly long Size;
        public readonly long Pixels;

        public PhotoEntry(string path, DateTime taken, long size, long pixels)
        {
            Path = path;
            Taken = taken;
            Size = size;
            Pixels = pixels;
        }
    }
}
