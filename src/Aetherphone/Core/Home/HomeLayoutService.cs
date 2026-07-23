using Aetherphone.Core.Apps;

namespace Aetherphone.Core.Home;

internal sealed class HomeLayoutService
{
    public const int Columns = 4;
    public const int DockCapacity = 4;
    public const int MinRows = 5;
    public const int MaxRows = 8;
    public const int DefaultRows = 6;
    private const string DefaultWidgetId = "skywatcher.forecast";
    private static readonly string[] DefaultDockApps = { "message", "messages", "settings" };

    private static readonly string[] DefaultFirstPageApps =
    {
        "chirper", "aethergram", "velvet", "polls",
        "camera", "photos", "feedback", "music",
        "maps", "venues", "games", "market",
    };

    private static readonly string[] DefaultSecondPageApps =
    {
        "skywatcher", "collections", "inventory", "fishing",
        "clock", "notes", "calculator", "timers",
        "wallet", "dailies", "calendar", "news",
        "character", "notifications", "jobs",
    };

    private static readonly string[] DefaultTrailingApps = { "dev" };

    private readonly IReadOnlyList<IPhoneApp> apps;
    private readonly WidgetRegistry widgets;
    private readonly IHomeConfiguration configuration;
    private readonly Dictionary<string, IPhoneApp> byId = new();
    private readonly List<List<HomeTile>> pages = new();
    private readonly List<HomeTile> dock = new();
    private readonly List<List<GridCell>> placements = new();
    private readonly bool[] availability;
    private int rows;
    private int folderCounter;
    private int widgetCounter;
    private int homePageCount;
    private bool placementsDirty = true;

    public HomeLayoutService(IReadOnlyList<IPhoneApp> apps, WidgetRegistry widgets, IHomeConfiguration configuration)
    {
        this.apps = apps;
        this.widgets = widgets;
        this.configuration = configuration;
        availability = new bool[apps.Count];
        rows = ClampRows(configuration.HomeGridRows);
        for (var index = 0; index < apps.Count; index++)
        {
            byId[apps[index].Id] = apps[index];
            availability[index] = apps[index].IsAvailable;
        }

        Load();
    }

    public int HomePageCount => homePageCount;
    public int TotalPageCount => pages.Count;
    public int Rows => rows;
    public IReadOnlyList<HomeTile> Page(int index) => pages[index];
    public IReadOnlyList<HomeTile> Dock => dock;

    public IReadOnlyList<GridCell> Placements(int index)
    {
        if (placementsDirty)
        {
            SolveAll();
        }

        return placements[index];
    }

    public void EnsureCurrent()
    {
        if (configuration.Home is null)
        {
            Load();
            Save();
            return;
        }

        var changed = false;
        for (var index = 0; index < apps.Count; index++)
        {
            var available = apps[index].IsAvailable;
            if (availability[index] != available)
            {
                availability[index] = available;
                changed = true;
            }
        }

        if (changed)
        {
            Load();
            return;
        }

        var configuredRows = ClampRows(configuration.HomeGridRows);
        if (configuredRows != rows)
        {
            rows = configuredRows;
            Commit();
        }
    }

    public static int ClampRows(int value) => Math.Clamp(value <= 0 ? DefaultRows : value, MinRows, MaxRows);

    public (int Page, int Index) Locate(HomeTile tile)
    {
        for (var page = 0; page < pages.Count; page++)
        {
            var index = pages[page].IndexOf(tile);
            if (index >= 0)
            {
                return (page, index);
            }
        }

        return (-1, -1);
    }

    public int DockIndexOf(HomeTile tile) => dock.IndexOf(tile);

    public bool CanDock(HomeTile tile) => tile.App is not null && dock.Count < DockCapacity && !dock.Contains(tile);

    public void MoveTile(HomeTile tile, int targetPage, int insertIndex)
    {
        if (targetPage < 0 || targetPage >= pages.Count)
        {
            return;
        }

        if (!Detach(tile))
        {
            return;
        }

        var page = pages[targetPage];
        page.Insert(Math.Clamp(insertIndex, 0, page.Count), tile);
        Commit();
    }

    public bool MoveToDock(HomeTile tile, int insertIndex)
    {
        if (tile.App is null || dock.Count >= DockCapacity && !dock.Contains(tile))
        {
            return false;
        }

        if (!Detach(tile))
        {
            return false;
        }

        dock.Insert(Math.Clamp(insertIndex, 0, dock.Count), tile);
        Commit();
        return true;
    }

    public void MakeFolder(HomeTile target, HomeTile dragged)
    {
        if (ReferenceEquals(target, dragged) || target.IsWidget || dragged.IsWidget ||
            target.IsFolder && dragged.IsFolder)
        {
            return;
        }

        var (targetPage, targetIndex) = Locate(target);
        if (targetPage < 0 || !Detach(dragged))
        {
            return;
        }

        if (target.IsFolder)
        {
            AddApps(target, dragged);
        }
        else
        {
            (targetPage, targetIndex) = Locate(target);
            var folder = HomeTile.ForFolder(NextFolderKey(), string.Empty, new[] { target.App! });
            AddApps(folder, dragged);
            pages[targetPage][targetIndex] = folder;
        }

        Commit();
    }

    public void RemoveFromFolder(HomeTile folder, IPhoneApp app, int targetPage, int insertIndex)
    {
        if (!folder.IsFolder || !folder.Apps.Remove(app))
        {
            return;
        }

        targetPage = Math.Clamp(targetPage, 0, Math.Max(0, pages.Count - 1));
        var page = pages[targetPage];
        page.Insert(Math.Clamp(insertIndex, 0, page.Count), HomeTile.ForApp(app));
        Commit();
    }

    public void Rename(HomeTile folder, string name)
    {
        if (!folder.IsFolder)
        {
            return;
        }

        folder.FolderName = name;
        Save();
    }

    public void SetFolderTint(HomeTile folder, string tint)
    {
        if (!folder.IsFolder)
        {
            return;
        }

        folder.FolderTint = tint;
        Save();
    }

    public bool AddWidget(IHomeWidget widget, WidgetSize size, int pageIndex)
    {
        if (!WidgetSizes.Contains(widget.Sizes, size))
        {
            return false;
        }

        pageIndex = Math.Clamp(pageIndex, 0, Math.Max(0, homePageCount - 1));
        pages[pageIndex].Insert(0, HomeTile.ForWidget(NextWidgetKey(widget.Id), widget, size));
        Commit();
        return true;
    }

    public void ResizeWidget(HomeTile tile, WidgetSize size)
    {
        if (!tile.IsWidget || tile.Size == size || !WidgetSizes.Contains(tile.Widget!.Sizes, size))
        {
            return;
        }

        tile.Size = size;
        Commit();
    }

    public void RemoveTile(HomeTile tile)
    {
        if (Detach(tile))
        {
            Commit();
        }
    }

    public void DisbandFolder(HomeTile folder)
    {
        if (!folder.IsFolder)
        {
            return;
        }

        var (page, index) = Locate(folder);
        if (page < 0)
        {
            return;
        }

        pages[page].RemoveAt(index);
        for (var appIndex = 0; appIndex < folder.Apps.Count; appIndex++)
        {
            pages[page].Insert(index + appIndex, HomeTile.ForApp(folder.Apps[appIndex]));
        }

        Commit();
    }

    public void Commit()
    {
        Normalize();
        Save();
        placementsDirty = true;
    }

    private bool Detach(HomeTile tile)
    {
        if (dock.Remove(tile))
        {
            return true;
        }

        var (page, index) = Locate(tile);
        if (page < 0)
        {
            return false;
        }

        pages[page].RemoveAt(index);
        return true;
    }

    private static void AddApps(HomeTile folder, HomeTile source)
    {
        if (source.IsFolder)
        {
            for (var index = 0; index < source.Apps.Count; index++)
            {
                folder.Apps.Add(source.Apps[index]);
            }

            return;
        }

        folder.Apps.Add(source.App!);
    }

    private void Load()
    {
        pages.Clear();
        dock.Clear();
        var placed = new HashSet<string>();
        var saved = configuration.Home;
        var dockIds = ResolveDockIds(saved);
        for (var index = 0; index < dockIds.Count; index++)
        {
            if (byId.TryGetValue(dockIds[index], out var app) && app.IsAvailable && placed.Add(app.Id) &&
                dock.Count < DockCapacity)
            {
                dock.Add(HomeTile.ForApp(app));
            }
        }

        if (saved is not null && saved.Pages.Count > 0)
        {
            LoadPages(saved.Pages, placed);
        }
        else if (saved is null)
        {
            SeedDefaultLayout(placed);
        }

        if (saved is null)
        {
            AppendTrailingDefaults();
        }

        if (pages.Count == 0)
        {
            pages.Add(new List<HomeTile>());
        }

        homePageCount = pages.Count;

        if (saved is not null && saved.LibraryPages.Count > 0)
        {
            LoadPages(saved.LibraryPages, placed);
        }

        var unplaced = new List<IPhoneApp>();
        for (var index = 0; index < apps.Count; index++)
        {
            if (apps[index].IsAvailable && placed.Add(apps[index].Id))
            {
                unplaced.Add(apps[index]);
            }
        }

        unplaced.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        var knownHome = CollectKnownHome(saved);
        for (var index = 0; index < unplaced.Count; index++)
        {
            var tile = HomeTile.ForApp(unplaced[index]);
            if (saved is null || knownHome.Contains(unplaced[index].Id))
            {
                AppendToHome(tile);
            }
            else
            {
                AppendToLibrary(tile);
            }
        }

        Normalize();
        placementsDirty = true;
    }

    private List<string> ResolveDockIds(HomeLayout? saved)
    {
        if (saved?.Dock is { } storedDock)
        {
            return storedDock;
        }

        var defaults = new List<string>(DefaultDockApps.Length);
        for (var index = 0; index < DefaultDockApps.Length; index++)
        {
            defaults.Add(DefaultDockApps[index]);
        }

        return defaults;
    }

    private void LoadPages(List<HomePage> source, HashSet<string> placed)
    {
        for (var pageIndex = 0; pageIndex < source.Count; pageIndex++)
        {
            var page = new List<HomeTile>();
            var items = source[pageIndex].Items;
            for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                if (LoadItem(items[itemIndex], placed) is { } tile)
                {
                    page.Add(tile);
                }
            }

            if (page.Count > 0)
            {
                pages.Add(page);
            }
        }
    }

    private HomeTile? LoadItem(HomeItem item, HashSet<string> placed)
    {
        if (string.Equals(item.Kind, "folder", StringComparison.Ordinal))
        {
            var contents = ResolveApps(item.AppIds, placed);
            if (contents.Count == 0)
            {
                return null;
            }

            return contents.Count == 1
                ? HomeTile.ForApp(contents[0])
                : HomeTile.ForFolder(NextFolderKey(), item.FolderName, contents, item.FolderTint);
        }

        if (string.Equals(item.Kind, "widget", StringComparison.Ordinal))
        {
            if (!widgets.TryGet(item.WidgetId, out var widget) || !widgets.IsAvailable(widget))
            {
                return null;
            }

            var size = WidgetSizes.Parse(item.WidgetSize);
            if (!WidgetSizes.Contains(widget.Sizes, size))
            {
                size = WidgetSizes.Smallest(widget.Sizes);
            }

            return HomeTile.ForWidget(NextWidgetKey(widget.Id), widget, size);
        }

        return byId.TryGetValue(item.AppId, out var app) && app.IsAvailable && placed.Add(app.Id)
            ? HomeTile.ForApp(app)
            : null;
    }

    private void SeedDefaultLayout(HashSet<string> placed)
    {
        var firstPage = new List<HomeTile>();
        if (widgets.TryGet(DefaultWidgetId, out var widget) && widgets.IsAvailable(widget))
        {
            firstPage.Add(HomeTile.ForWidget(NextWidgetKey(widget.Id), widget, WidgetSize.Medium));
        }

        AppendSeedApps(firstPage, DefaultFirstPageApps, placed);
        var secondPage = new List<HomeTile>();
        AppendSeedApps(secondPage, DefaultSecondPageApps, placed);
        pages.Add(firstPage);
        pages.Add(secondPage);
        for (var index = 0; index < DefaultTrailingApps.Length; index++)
        {
            placed.Add(DefaultTrailingApps[index]);
        }
    }

    private void AppendSeedApps(List<HomeTile> page, string[] ids, HashSet<string> placed)
    {
        for (var index = 0; index < ids.Length; index++)
        {
            if (byId.TryGetValue(ids[index], out var app) && app.IsAvailable && placed.Add(app.Id))
            {
                page.Add(HomeTile.ForApp(app));
            }
        }
    }

    private void AppendTrailingDefaults()
    {
        for (var index = 0; index < DefaultTrailingApps.Length; index++)
        {
            if (byId.TryGetValue(DefaultTrailingApps[index], out var app) && app.IsAvailable)
            {
                Append(HomeTile.ForApp(app));
            }
        }
    }

    private List<IPhoneApp> ResolveApps(List<string> ids, HashSet<string> placed)
    {
        var contents = new List<IPhoneApp>(ids.Count);
        for (var index = 0; index < ids.Count; index++)
        {
            if (byId.TryGetValue(ids[index], out var app) && app.IsAvailable && placed.Add(app.Id))
            {
                contents.Add(app);
            }
        }

        return contents;
    }

    private void Append(HomeTile tile)
    {
        if (pages.Count == 0)
        {
            pages.Add(new List<HomeTile>());
        }

        var last = pages[^1];
        last.Add(tile);
        if (HomeGridSolver.Fits(last, Columns, rows))
        {
            return;
        }

        last.RemoveAt(last.Count - 1);
        pages.Add(new List<HomeTile> { tile });
    }

    private void AppendToHome(HomeTile tile)
    {
        var last = pages[homePageCount - 1];
        last.Add(tile);
        if (HomeGridSolver.Fits(last, Columns, rows))
        {
            return;
        }

        last.RemoveAt(last.Count - 1);
        pages.Insert(homePageCount, new List<HomeTile> { tile });
        homePageCount++;
    }

    private void AppendToLibrary(HomeTile tile)
    {
        if (pages.Count == homePageCount)
        {
            pages.Add(new List<HomeTile>());
        }

        var last = pages[^1];
        last.Add(tile);
        if (HomeGridSolver.Fits(last, Columns, rows))
        {
            return;
        }

        last.RemoveAt(last.Count - 1);
        pages.Add(new List<HomeTile> { tile });
    }

    private void Normalize()
    {
        FoldDegenerateFolders();
        var scratch = new List<GridCell>();
        homePageCount = NormalizeRange(scratch, 0, homePageCount);
        var libraryEnd = NormalizeRange(scratch, homePageCount, pages.Count);
        if (libraryEnd == homePageCount)
        {
            pages.Add(new List<HomeTile>());
        }
    }

    private int NormalizeRange(List<GridCell> scratch, int start, int end)
    {
        for (var page = start; page < end; page++)
        {
            var tiles = pages[page];
            var fitCount = HomeGridSolver.Solve(tiles, Columns, rows, scratch);
            if (fitCount >= tiles.Count)
            {
                continue;
            }

            if (page + 1 >= end)
            {
                pages.Insert(page + 1, new List<HomeTile>());
                end++;
            }

            var overflow = tiles.GetRange(fitCount, tiles.Count - fitCount);
            tiles.RemoveRange(fitCount, tiles.Count - fitCount);
            pages[page + 1].InsertRange(0, overflow);
        }

        for (var page = end - 1; page > start; page--)
        {
            if (pages[page].Count == 0)
            {
                pages.RemoveAt(page);
                end--;
            }
        }

        return end;
    }

    private void FoldDegenerateFolders()
    {
        for (var page = 0; page < pages.Count; page++)
        {
            for (var index = pages[page].Count - 1; index >= 0; index--)
            {
                var tile = pages[page][index];
                if (!tile.IsFolder || tile.Apps.Count > 1)
                {
                    continue;
                }

                if (tile.Apps.Count == 1)
                {
                    pages[page][index] = HomeTile.ForApp(tile.Apps[0]);
                }
                else
                {
                    pages[page].RemoveAt(index);
                }
            }
        }
    }

    private void SolveAll()
    {
        while (placements.Count < pages.Count)
        {
            placements.Add(new List<GridCell>());
        }

        placements.RemoveRange(pages.Count, placements.Count - pages.Count);
        for (var page = 0; page < pages.Count; page++)
        {
            HomeGridSolver.Solve(pages[page], Columns, rows, placements[page]);
        }

        placementsDirty = false;
    }

    private void Save()
    {
        var layout = new HomeLayout { Dock = new List<string>(dock.Count) };
        for (var index = 0; index < dock.Count; index++)
        {
            layout.Dock.Add(dock[index].App!.Id);
        }

        SerializePages(layout.Pages, 0, homePageCount);
        SerializePages(layout.LibraryPages, homePageCount, pages.Count);
        layout.KnownHome = BuildKnownHome();

        configuration.Home = layout;
        configuration.Save();
    }

    private static HashSet<string> CollectKnownHome(HomeLayout? saved)
    {
        var known = new HashSet<string>();
        if (saved is null)
        {
            return known;
        }

        for (var index = 0; index < saved.KnownHome.Count; index++)
        {
            known.Add(saved.KnownHome[index]);
        }

        return known;
    }

    private List<string> BuildKnownHome()
    {
        var known = CollectKnownHome(configuration.Home);
        var ids = new List<string>();
        CollectSavedHomeIds(configuration.Home, ids);
        AddAll(known, ids);

        ids.Clear();
        for (var page = homePageCount; page < pages.Count; page++)
        {
            CollectAppIds(pages[page], ids);
        }

        RemoveAll(known, ids);

        ids.Clear();
        for (var page = 0; page < homePageCount; page++)
        {
            CollectAppIds(pages[page], ids);
        }

        for (var index = 0; index < dock.Count; index++)
        {
            ids.Add(dock[index].App!.Id);
        }

        AddAll(known, ids);
        return new List<string>(known);
    }

    private static void AddAll(HashSet<string> target, List<string> ids)
    {
        for (var index = 0; index < ids.Count; index++)
        {
            target.Add(ids[index]);
        }
    }

    private static void RemoveAll(HashSet<string> target, List<string> ids)
    {
        for (var index = 0; index < ids.Count; index++)
        {
            target.Remove(ids[index]);
        }
    }

    private static void CollectSavedHomeIds(HomeLayout? saved, List<string> target)
    {
        if (saved is null)
        {
            return;
        }

        for (var page = 0; page < saved.Pages.Count; page++)
        {
            var items = saved.Pages[page].Items;
            for (var index = 0; index < items.Count; index++)
            {
                var item = items[index];
                if (!string.IsNullOrEmpty(item.AppId))
                {
                    target.Add(item.AppId);
                }

                for (var appIndex = 0; appIndex < item.AppIds.Count; appIndex++)
                {
                    target.Add(item.AppIds[appIndex]);
                }
            }
        }

        if (saved.Dock is null)
        {
            return;
        }

        for (var index = 0; index < saved.Dock.Count; index++)
        {
            target.Add(saved.Dock[index]);
        }
    }

    private static void CollectAppIds(List<HomeTile> tiles, List<string> target)
    {
        for (var index = 0; index < tiles.Count; index++)
        {
            var tile = tiles[index];
            if (tile.App is not null)
            {
                target.Add(tile.App.Id);
                continue;
            }

            for (var appIndex = 0; appIndex < tile.Apps.Count; appIndex++)
            {
                target.Add(tile.Apps[appIndex].Id);
            }
        }
    }

    private void SerializePages(List<HomePage> target, int start, int end)
    {
        for (var page = start; page < end; page++)
        {
            var stored = new HomePage();
            var tiles = pages[page];
            for (var index = 0; index < tiles.Count; index++)
            {
                stored.Items.Add(SerializeTile(tiles[index]));
            }

            target.Add(stored);
        }
    }

    private static HomeItem SerializeTile(HomeTile tile)
    {
        if (tile.IsWidget)
        {
            return new HomeItem
            {
                Kind = "widget",
                WidgetId = tile.Widget!.Id,
                WidgetSize = WidgetSizes.Serialize(tile.Size),
            };
        }

        if (tile.IsFolder)
        {
            var item = new HomeItem { Kind = "folder", FolderName = tile.FolderName, FolderTint = tile.FolderTint };
            for (var appIndex = 0; appIndex < tile.Apps.Count; appIndex++)
            {
                item.AppIds.Add(tile.Apps[appIndex].Id);
            }

            return item;
        }

        return new HomeItem { Kind = "app", AppId = tile.App!.Id };
    }

    private string NextFolderKey() => string.Concat("folder#", (++folderCounter).ToString());

    private string NextWidgetKey(string widgetId) =>
        string.Concat("widget#", widgetId, "#", (++widgetCounter).ToString());
}
