using System.Collections.Concurrent;
using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Media;
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
    private const int ThumbnailMaxDimension = 256;
    private const long ThumbnailBudgetBytes = 48L * 1024 * 1024;
    private const long FullImageBudgetBytes = 96L * 1024 * 1024;
    private const int FirstCustomAlbumId = 100;

    public string Id => "photos";
    public Vector4 Accent => AppAccents.For(Id);
    public string DisplayName => Loc.T(L.Apps.Photos);
    public string Glyph => "P";
    public int BadgeCount => 0;

    private static readonly SocialInk Ink = new(AppPalettes.Photos);

    private readonly PhotoLibrary library;
    private readonly ConfirmService confirm;
    private readonly ShareService share;
    private readonly Configuration configuration;
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
    private readonly Action<Rect> drawNameSheet;
    private readonly List<MonthAlbum> albums = new();
    private readonly List<GridBand> bands = new();
    private readonly List<CustomAlbum> customAlbums = new();
    private readonly List<string> customAlbumOrder = new();
    private readonly Dictionary<string, List<string>> customAlbumPhotos = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> customAlbumIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, string[]> cachedCustomAlbumPaths = new();
    private readonly List<string> pickerSelection = new();
    private readonly HashSet<string> pickerMembership = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> pickerSelectionOrder = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> favorites = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> selection = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> selectionOrder = new();
    private readonly BottomTabBar tabs = new();
    private readonly NavTab[] navTabs = new NavTab[2];
    private readonly PanRail monthsRail = new();
    private readonly ActionSheet albumSheet = new();
    private readonly ActionSheet.Item[] albumSheetItems = new ActionSheet.Item[AlbumSheetItemCount];
    private readonly ActionSheet photoSheet = new();
    private readonly ActionSheet.Item[] photoSheetItems = new ActionSheet.Item[1];
    private readonly SheetSurface nameSheet = new("photos.albumName");
    private readonly DropdownMenu sortMenu = new();
    private readonly DropdownMenu.Item[] sortMenuItems = new DropdownMenu.Item[SortMenuItemCount];
    private readonly Dictionary<string, long> sizeCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> pixelCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Comparison<PhotoEntry> compareEntries;
    private readonly Comparison<string> comparePaths;
    private readonly Dictionary<string, DateTime> trashExpiry = new(StringComparer.OrdinalIgnoreCase);
    private readonly string[] daysLeftLabels = new string[PhotoLibrary.TrashRetentionDays + 1];
    private string daysLeftLocale = string.Empty;
    private bool scanningDimensions;
    private readonly SheetSurface infoSheet = new("photos.info");
    private readonly Action<Rect> drawInfoSheet;
    private readonly ActionSheet trashSheet = new();
    private readonly ActionSheet.Item[] trashSheetItems = new ActionSheet.Item[TrashSheetItemCount];
    private int nextCustomAlbumId = FirstCustomAlbumId;
    private int? pickerMembershipAlbumKey;
    private int albumSheetKey;
    private int photoSheetAlbumKey;
    private string photoSheetPath = string.Empty;
    private NameSheetMode nameSheetMode;
    private int nameSheetAlbumKey;
    private string[]? nameSheetPhotoPaths;
    private string nameDraft = string.Empty;
    private bool nameSheetFocus;
    private string albumsLocale = string.Empty;

    private PhotoEntry[] entries = Array.Empty<PhotoEntry>();
    private string[] viewerPaths = Array.Empty<string>();
    private string[] favoritePaths = Array.Empty<string>();
    private string[] trashPaths = Array.Empty<string>();
    private string[] addTargets = Array.Empty<string>();
    private bool viewerInTrash;
    private bool selecting;
    private SelectionScope selectionScope;
    private int viewerIndex;
    private int segment;
    private bool resetScroll;
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
        drawInfoSheet = DrawInfoSheetContent;
        compareEntries = CompareEntries;
        comparePaths = ComparePaths;
        back = () => router.Pop();
        LoadCustomAlbums();
        LoadFavorites();
    }

    public void OnOpened()
    {
        router.Reset();
        segment = Math.Clamp(configuration.PhotosSegment, LibraryTab, AlbumsTab);
        viewerPaths = Array.Empty<string>();
        viewerIndex = 0;
        resetScroll = true;
        viewerInTrash = false;
        addTargets = Array.Empty<string>();
        monthsRail.Reset();
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
        infoSheet.Close();
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

        if (!string.Equals(albumsLocale, Loc.Current.Code, StringComparison.Ordinal))
        {
            BuildAlbums();
        }

        albumSheet.Gate();
        photoSheet.Gate();
        sortMenu.Gate();
        trashSheet.Gate();

        var scale = UiScale.Current;
        var screen = SceneChrome.ScreenFrom(context.Content, context.Theme, scale);
        frameScreen = screen;
        ui.Backdrop(screen);
        using (InputShield.Engage(nameSheet.CapturesPointer || infoSheet.CapturesPointer))
        {
            router.Draw(screen, AppSkin.Transparent, ImGui.GetIO().DeltaTime, drawView);
        }

        DrawSortMenu(screen);
        DrawAlbumSheet(screen);
        DrawPhotoSheet(screen);
        DrawTrashSheet(screen);
        var sheetArea = AppAreaWithin(screen);
        DrawNameSheet(sheetArea);
        DrawInfoSheet(sheetArea);
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

        var content = AppAreaWithin(area);
        ui.Body(content);
        if (view.Route == PhotoRoute.AlbumPicker)
        {
            DrawAlbumPicker(content, view.AlbumKey);
            return;
        }

        if (view.Route == PhotoRoute.AddToAlbum)
        {
            DrawAddToAlbumPage(content);
            return;
        }

        if (view.Route == PhotoRoute.Album)
        {
            DrawAlbum(content, view.AlbumKey);
            return;
        }

        DrawRoot(content);
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

    private void Refresh()
    {
        var paths = library.List();
        var pathSet = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
        var visible = FilteredPaths(paths);
        var built = new PhotoEntry[visible.Length];
        for (var index = 0; index < visible.Length; index++)
        {
            built[index] = MetadataFor(visible[index]);
        }

        Array.Sort(built, compareEntries);
        entries = built;
        if (SortKey == PhotoSortKey.Dimensions)
        {
            ScanMissingDimensions(visible);
        }

        BuildAlbums();
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
        library.PurgeExpired();
        RefreshTrash();
    }
    
    private bool PruneCustomAlbumPaths(HashSet<string> validPaths)
    {
        var removedAny = false;
        foreach (var name in customAlbumOrder)
        {
            if (!customAlbumPhotos.TryGetValue(name, out var photos))
            {
                continue;
            }
            for (var index = photos.Count - 1; index >= 0; index--)
            {
                if (validPaths.Contains(photos[index]))
                {
                    continue;
                }
                photos.RemoveAt(index);
                removedAny = true;
            }
        }

        return removedAny;
    }

    private void LoadCustomAlbums()
    {
        customAlbumOrder.Clear();
        customAlbumOrder.AddRange(configuration.CustomAlbumOrder);
        customAlbumPhotos.Clear();
        foreach (var entry in configuration.CustomAlbumPhotos)
        {
            customAlbumPhotos[entry.Key] = new List<string>(entry.Value);
        }

        customAlbumIds.Clear();
        nextCustomAlbumId = FirstCustomAlbumId;
    }

    private void SaveCustomAlbums()
    {
        configuration.CustomAlbumOrder = new List<string>(customAlbumOrder);
        configuration.CustomAlbumPhotos = new Dictionary<string, List<string>>(customAlbumPhotos,
            StringComparer.OrdinalIgnoreCase);
        configuration.SaveNow();
    }

    private void BuildAlbums()
    {
        albums.Clear();
        albumsLocale = Loc.Current.Code;
        if (SortKey != PhotoSortKey.Date)
        {
            return;
        }

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

            var month = new DateTime(taken.Year, taken.Month, 1);
            albums.Add(new MonthAlbum(key, start, index - start, Capitalize(month.ToString("MMMM yyyy", Loc.Culture))));
        }
    }
    
    private void BuildCustomAlbums()
    {
        customAlbums.Clear();
        foreach (var name in customAlbumOrder)
        {
            if (!customAlbumPhotos.TryGetValue(name, out var photos))
            {
                continue;
            }
            var key = -GetOrAssignCustomAlbumId(name);
            customAlbums.Add(new CustomAlbum(key, 0, photos.Count, name));
            var paths = SortedCustomAlbumPaths(key);
            cachedCustomAlbumPaths[key] = paths;
        }
    }
    
    private bool TryFindCustomAlbum(int key, out CustomAlbum result)
    {
        for (var index = 0; index < customAlbums.Count; index++)
        {
            if (customAlbums[index].Key == key)
            {
                result = customAlbums[index];
                return true;
            }
        }
        result = default;
        return false;
    }
    
    private int CreateCustomAlbumInternal(string name)
    {
        name = name.Trim();
        if (name.Length == 0)
        {
            return 0;
        }

        if (ContainsOrdinalIgnoreCase(customAlbumOrder, name))
        {
            return 0;
        }

        customAlbumOrder.Add(name);
        customAlbumPhotos[name] = new List<string>();
        BuildCustomAlbums();
        SaveCustomAlbums();
        return -GetOrAssignCustomAlbumId(name);
    }

    private void DeleteCustomAlbumInternal(int key)
    {
        
        if (!TryFindCustomAlbum(key, out var found))
        {
            return;
        }
        customAlbumOrder.Remove(found.Name);
        customAlbumPhotos.Remove(found.Name);
        customAlbumIds.Remove(found.Name);
        BuildCustomAlbums();
        SaveCustomAlbums();
    }
    
    private void RenameCustomAlbumInternal(int key, string newName)
    {
        newName = newName.Trim();
        if (newName.Length == 0)
        {
            return;
        }
        if (!TryFindCustomAlbum(key, out var found))
        {
            return;
        }
        if (string.Equals(found.Name, newName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        if (ContainsOrdinalIgnoreCase(customAlbumOrder, newName))
        {
            return;
        }
        if (!customAlbumPhotos.TryGetValue(found.Name, out var photos))
        {
            return;
        }
        if (customAlbumIds.TryGetValue(found.Name, out var id))
        {
            customAlbumIds.Remove(found.Name);
            customAlbumIds[newName] = id;
        }
        customAlbumOrder[customAlbumOrder.IndexOf(found.Name)] = newName;
        customAlbumPhotos.Remove(found.Name);
        customAlbumPhotos[newName] = photos;
        BuildCustomAlbums();
        SaveCustomAlbums();
    }

    private void AddPhotosToCustomAlbum(int key, string[] paths)
    {
        if (!TryFindCustomAlbum(key, out var found))
        {
            return;
        }
        if (!customAlbumPhotos.TryGetValue(found.Name, out var photos))
        {
            return;
        }
        foreach (var path in paths)
        {
            if (!ContainsOrdinalIgnoreCase(photos, path))
            {
                photos.Add(path);
            }
        }
        BuildCustomAlbums();
        SaveCustomAlbums();
        InvalidatePickerMembership();
    }
    
    private void RemovePhotoFromCustomAlbum(int key, string path)
    {
        if (!TryFindCustomAlbum(key, out var found))
        {
            return;
        }
        if (!customAlbumPhotos.TryGetValue(found.Name, out var photos))
        {
            return;
        }
        photos.Remove(path);
        BuildCustomAlbums();
        SaveCustomAlbums();
        InvalidatePickerMembership();
    }

    private string[] SortedCustomAlbumPaths(int key)
    {
        if (!TryFindCustomAlbum(key, out var album))
        {
            return Array.Empty<string>();
        }
        if (!customAlbumPhotos.TryGetValue(album.Name, out var unordered))
        {
            return Array.Empty<string>();
        }
        var sorted = unordered.ToArray();
        Array.Sort(sorted, comparePaths);
        return sorted;
    }

    private PhotoSortKey SortKey =>
        (PhotoSortKey)Math.Clamp(configuration.PhotosSortKey, 0, (int)PhotoSortKey.Dimensions);

    private PhotoFilter Filter => (PhotoFilter)Math.Clamp(configuration.PhotosFilter, 0, (int)PhotoFilter.NotInAlbum);

    private int Columns => Math.Clamp(configuration.PhotosGridColumns == 0 ? DefaultColumns : configuration.PhotosGridColumns,
        MinColumns, MaxColumns);

    private string[] FilteredPaths(string[] paths)
    {
        var filter = Filter;
        if (filter == PhotoFilter.All)
        {
            return paths;
        }

        var kept = new List<string>(paths.Length);
        if (filter == PhotoFilter.Favorites)
        {
            for (var index = 0; index < paths.Length; index++)
            {
                if (favorites.Contains(paths[index]))
                {
                    kept.Add(paths[index]);
                }
            }

            return kept.ToArray();
        }

        var inAlbums = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var photos in customAlbumPhotos.Values)
        {
            inAlbums.UnionWith(photos);
        }

        for (var index = 0; index < paths.Length; index++)
        {
            if (!inAlbums.Contains(paths[index]))
            {
                kept.Add(paths[index]);
            }
        }

        return kept.ToArray();
    }

    private int DaysLeft(string trashPath)
    {
        if (!trashExpiry.TryGetValue(trashPath, out var expiry))
        {
            return 0;
        }

        var days = (int)Math.Ceiling((expiry - DateTime.Now).TotalDays);
        return Math.Clamp(days, 0, PhotoLibrary.TrashRetentionDays);
    }

    private string DaysLeftLabel(int days)
    {
        if (!string.Equals(daysLeftLocale, Loc.Current.Code, StringComparison.Ordinal))
        {
            Array.Clear(daysLeftLabels);
            daysLeftLocale = Loc.Current.Code;
        }

        return daysLeftLabels[days] ??= Loc.Plural(L.Photos.DaysLeft, days);
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
    
    private void EnsurePickerMembership(int albumKey)
    {
        if (pickerMembershipAlbumKey == albumKey)
        {
            return;
        }

        pickerMembership.Clear();

        if (pickerMembershipAlbumKey == albumKey)
        {
            return;
        }

        if (TryFindCustomAlbum(albumKey, out var album) && customAlbumPhotos.TryGetValue(album.Name, out var existing))
        {
            pickerMembership.UnionWith(existing);
        }

        pickerMembershipAlbumKey = albumKey;
    }
    
    private void InvalidatePickerMembership()
    {
        pickerMembershipAlbumKey = null;
    }
    
    private void AddToPickerSelection(string path)
    {
        pickerSelection.Add(path);
        pickerSelectionOrder[path] = pickerSelection.Count;
    }
    
    private int GetOrAssignCustomAlbumId(string name)
    {
        if (customAlbumIds.TryGetValue(name, out var id))
        {
            return id;
        }

        id = nextCustomAlbumId++;
        customAlbumIds[name] = id;
        return id;
    }
    
    private void RemoveFromPickerSelection(string path)
    {
        if (!pickerSelection.Remove(path))
        {
            return;
        }

        pickerSelectionOrder.Remove(path);
        for (var index = 0; index < pickerSelection.Count; index++)
        {
            pickerSelectionOrder[pickerSelection[index]] = index + 1;
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
        viewerInTrash = false;
        zoomView.Reset();
        router.Push(PhotoView.Viewer());
    }
    
    private void OpenViewerFromPaths(string[] paths, int index)
    {
        viewerPaths = paths;
        viewerIndex = Math.Clamp(index, 0, paths.Length - 1);
        viewerInTrash = false;
        zoomView.Reset();
        router.Push(PhotoView.Viewer());
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
        catch (Exception exception)
        {
            AepLog.Warning(exception, $"[Photos] could not read the timestamp of {Path.GetFileName(path)}");
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
            loading.TryRemove("thumb:" + path, out _);
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
            loading.TryRemove("full:" + path, out _);
        }
    }

    public void Dispose()
    {
        cancellation.Cancel();
        editSession.Dispose();
        thumbnails.DisposeAll();
        fullImages.DisposeAll();
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

    private readonly struct MonthAlbum
    {
        public readonly int Key;
        public readonly int Start;
        public readonly int Count;
        public readonly string Title;

        public MonthAlbum(int key, int start, int count, string title)
        {
            Key = key;
            Start = start;
            Count = count;
            Title = title;
        }
    }

    private readonly struct CustomAlbum
    {
        public readonly int Key;
        public readonly int Start;
        public readonly int Count;
        public readonly string Name;

        public CustomAlbum(int key, int start, int count, string name)
        {
            Key = key;
            Start = start;
            Count = count;
            Name = name;
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
