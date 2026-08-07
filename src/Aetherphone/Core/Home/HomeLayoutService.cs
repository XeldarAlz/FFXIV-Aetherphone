using Aetherphone.Core.Apps;
using Aetherphone.Core.Shortcuts;

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
        "announcements", "camera", "photos", "feedback", "music",
        "maps", "venues", "games", "market",
        "appstore",
    };

    private static readonly string[] DefaultSecondPageApps =
    {
        "skywatcher", "collections", "inventory", "fishing",
        "clock", "notes", "calculator", "timers", "shortcuts",
        "wallet", "dailies", "calendar", "news",
        "character", "notifications", "jobs",
        "health",
    };

    private static readonly string[] MandatoryApps = { "appstore", "settings", "announcements" };

    private readonly IReadOnlyList<IPhoneApp> apps;
    private readonly WidgetRegistry widgets;
    private readonly IShortcutSource shortcuts;
    private readonly IHomeConfiguration configuration;
    private readonly Dictionary<string, IPhoneApp> byId = new();
    private readonly List<List<HomeTile>> pages = new();
    private readonly List<HomeTile> dock = new();
    private readonly List<List<GridCell>> placements = new();
    private readonly List<HomeTile> pending = new();
    private readonly List<HomeTile> overflow = new();
    private readonly bool[] availability;
    private readonly HashSet<string> installed = new();
    private readonly HashSet<string> known = new();
    private readonly List<string> revealed = new();
    private int rows;
    private int folderCounter;
    private int widgetCounter;
    private bool placementsDirty = true;

    public HomeLayoutService(IReadOnlyList<IPhoneApp> apps, WidgetRegistry widgets, IShortcutSource shortcuts,
        IHomeConfiguration configuration)
    {
        this.apps = apps;
        this.widgets = widgets;
        this.shortcuts = shortcuts;
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

    public int PageCount => pages.Count;
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
            if (availability[index] == available)
            {
                continue;
            }

            if (available && !known.Contains(apps[index].Id))
            {
                revealed.Add(apps[index].Id);
            }

            availability[index] = available;
            changed = true;
        }

        if (changed)
        {
            Load();
            InstallRevealed();
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

    public void MoveTile(HomeTile tile, int targetPage, GridCell cell)
    {
        if (targetPage < 0 || targetPage > pages.Count)
        {
            return;
        }

        if (!Detach(tile))
        {
            return;
        }

        if (targetPage == pages.Count)
        {
            pages.Add(new List<HomeTile>());
        }

        tile.Cell = cell;
        pages[targetPage].Add(tile);
        Commit();
    }

    public bool TryResolveDrop(int page, HomeTile tile, GridCell desired, out GridCell cell)
    {
        cell = HomeGridSolver.Unassigned;
        if (page < 0 || page > pages.Count)
        {
            return false;
        }

        Span<bool> occupied = stackalloc bool[HomeGridSolver.MaxCells];
        occupied.Clear();
        if (page < pages.Count)
        {
            Occupy(occupied, pages[page], tile);
        }

        return HomeGridSolver.TryFindFree(occupied, Columns, rows, tile.ColumnSpan, tile.RowSpan, desired, out cell);
    }

    private void Occupy(Span<bool> occupied, List<HomeTile> tiles, HomeTile? skip)
    {
        for (var index = 0; index < tiles.Count; index++)
        {
            var tile = tiles[index];
            if (ReferenceEquals(tile, skip) || !HomeGridSolver.IsAssigned(tile.Cell))
            {
                continue;
            }

            if (HomeGridSolver.RegionFree(occupied, Columns, rows, tile.Cell, tile.ColumnSpan, tile.RowSpan))
            {
                HomeGridSolver.Mark(occupied, Columns, tile.Cell, tile.ColumnSpan, tile.RowSpan);
            }
        }
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

        tile.Cell = HomeGridSolver.Unassigned;
        dock.Insert(Math.Clamp(insertIndex, 0, dock.Count), tile);
        Commit();
        return true;
    }

    public void MakeFolder(HomeTile target, HomeTile dragged)
    {
        if (ReferenceEquals(target, dragged) || target.IsWidget || dragged.IsWidget
            || target.IsFolder && dragged.IsFolder)
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
            AddMember(target, dragged);
        }
        else
        {
            (targetPage, targetIndex) = Locate(target);
            var folder = HomeTile.ForFolder(NextFolderKey(), string.Empty, new[] { HomeTile.AsLeaf(target) });
            folder.Cell = target.Cell;
            AddMember(folder, dragged);
            pages[targetPage][targetIndex] = folder;
        }

        Commit();
    }

    public void RemoveFromFolder(HomeTile folder, HomeTile member, int targetPage)
    {
        if (!folder.IsFolder || !folder.Members.Remove(member))
        {
            return;
        }

        targetPage = Math.Clamp(targetPage, 0, Math.Max(0, pages.Count - 1));
        pages[targetPage].Add(HomeTile.AsLeaf(member));
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

    public event Action<string>? InstalledChanged;

    public bool IsInstalled(string appId) => installed.Contains(appId);

    public static bool CanUninstall(string appId)
    {
        for (var index = 0; index < MandatoryApps.Length; index++)
        {
            if (string.Equals(appId, MandatoryApps[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    public bool Install(string appId)
    {
        if (!byId.TryGetValue(appId, out var app) || !app.IsAvailable || !installed.Add(appId))
        {
            return false;
        }

        Append(HomeTile.ForApp(app));
        Commit();
        InstalledChanged?.Invoke(appId);
        return true;
    }

    public bool Uninstall(string appId)
    {
        if (!CanUninstall(appId) || !installed.Remove(appId))
        {
            return false;
        }

        DetachApp(appId);
        DropWidgetsOfUninstalledApps();
        Commit();
        InstalledChanged?.Invoke(appId);
        return true;
    }

    private void DropWidgetsOfUninstalledApps()
    {
        for (var page = 0; page < pages.Count; page++)
        {
            var tiles = pages[page];
            for (var index = tiles.Count - 1; index >= 0; index--)
            {
                if (tiles[index].Widget is { } widget && !installed.Contains(widget.AppId))
                {
                    tiles.RemoveAt(index);
                }
            }
        }
    }

    private void DetachApp(string appId)
    {
        for (var index = dock.Count - 1; index >= 0; index--)
        {
            if (string.Equals(dock[index].App?.Id, appId, StringComparison.Ordinal))
            {
                dock.RemoveAt(index);
                return;
            }
        }

        for (var page = 0; page < pages.Count; page++)
        {
            var tiles = pages[page];
            for (var index = tiles.Count - 1; index >= 0; index--)
            {
                var tile = tiles[index];
                if (string.Equals(tile.App?.Id, appId, StringComparison.Ordinal))
                {
                    tiles.RemoveAt(index);
                    return;
                }

                for (var memberIndex = tile.Members.Count - 1; memberIndex >= 0; memberIndex--)
                {
                    if (string.Equals(tile.Members[memberIndex].App?.Id, appId, StringComparison.Ordinal))
                    {
                        tile.Members.RemoveAt(memberIndex);
                        return;
                    }
                }
            }
        }
    }

    public bool HasShortcut(Guid id)
    {
        for (var page = 0; page < pages.Count; page++)
        {
            var tiles = pages[page];
            for (var index = 0; index < tiles.Count; index++)
            {
                var tile = tiles[index];
                if (tile.Shortcut?.Id == id)
                {
                    return true;
                }

                if (!tile.IsFolder)
                {
                    continue;
                }

                for (var memberIndex = 0; memberIndex < tile.Members.Count; memberIndex++)
                {
                    if (tile.Members[memberIndex].Shortcut?.Id == id)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public bool AddShortcut(Guid id)
    {
        if (HasShortcut(id) || shortcuts.Find(id) is not { } shortcut)
        {
            return false;
        }

        Append(HomeTile.ForShortcut(shortcut));
        Commit();
        return true;
    }

    public bool RemoveShortcut(Guid id)
    {
        var removed = false;
        for (var page = 0; page < pages.Count; page++)
        {
            var tiles = pages[page];
            for (var index = tiles.Count - 1; index >= 0; index--)
            {
                var tile = tiles[index];
                if (tile.Shortcut?.Id == id)
                {
                    tiles.RemoveAt(index);
                    removed = true;
                    continue;
                }

                if (!tile.IsFolder)
                {
                    continue;
                }

                for (var memberIndex = tile.Members.Count - 1; memberIndex >= 0; memberIndex--)
                {
                    if (tile.Members[memberIndex].Shortcut?.Id == id)
                    {
                        tile.Members.RemoveAt(memberIndex);
                        removed = true;
                    }
                }
            }
        }

        if (removed)
        {
            Commit();
        }

        return removed;
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

        pageIndex = Math.Clamp(pageIndex, 0, Math.Max(0, pages.Count - 1));
        pages[pageIndex].Add(HomeTile.ForWidget(NextWidgetKey(widget.Id), widget, size));
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
        for (var memberIndex = 0; memberIndex < folder.Members.Count; memberIndex++)
        {
            var tile = HomeTile.AsLeaf(folder.Members[memberIndex]);
            if (memberIndex == 0)
            {
                tile.Cell = folder.Cell;
            }

            pages[page].Add(tile);
        }

        Commit();
    }

    public void Commit()
    {
        Arrange();
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

    private static void AddMember(HomeTile folder, HomeTile source)
    {
        if (source.IsFolder)
        {
            for (var index = 0; index < source.Members.Count; index++)
            {
                folder.Members.Add(source.Members[index]);
            }

            return;
        }

        folder.Members.Add(HomeTile.AsLeaf(source));
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

        if (pages.Count == 0)
        {
            pages.Add(new List<HomeTile>());
        }

        LoadInstalled(saved, placed);
        Arrange();
        placementsDirty = true;
    }

    private void InstallRevealed()
    {
        if (revealed.Count == 0)
        {
            return;
        }

        for (var index = 0; index < revealed.Count; index++)
        {
            known.Add(revealed[index]);
            Install(revealed[index]);
        }

        revealed.Clear();
        Save();
    }

    private void LoadKnown(HomeLayout? saved)
    {
        known.Clear();
        if (saved?.Known is { Count: > 0 } stored)
        {
            for (var index = 0; index < stored.Count; index++)
            {
                known.Add(stored[index]);
            }

            return;
        }

        for (var index = 0; index < apps.Count; index++)
        {
            if (apps[index].IsAvailable)
            {
                known.Add(apps[index].Id);
            }
        }
    }

    private void LoadInstalled(HomeLayout? saved, HashSet<string> placed)
    {
        LoadKnown(saved);
        installed.Clear();
        if (saved?.Installed is { Count: > 0 } stored)
        {
            for (var index = 0; index < stored.Count; index++)
            {
                installed.Add(stored[index]);
            }
        }
        else
        {
            SeedInstalled(saved);
        }

        for (var index = 0; index < MandatoryApps.Length; index++)
        {
            installed.Add(MandatoryApps[index]);
        }

        var queue = new List<IPhoneApp>();
        for (var index = 0; index < apps.Count; index++)
        {
            var app = apps[index];
            if (app.IsAvailable && installed.Contains(app.Id) && placed.Add(app.Id))
            {
                queue.Add(app);
            }
        }

        queue.Sort((first, second) =>
            string.Compare(first.DisplayName, second.DisplayName, StringComparison.OrdinalIgnoreCase));
        for (var index = 0; index < queue.Count; index++)
        {
            Append(HomeTile.ForApp(queue[index]));
        }

        DropWidgetsOfUninstalledApps();
    }

    private void SeedInstalled(HomeLayout? saved)
    {
        if (saved is null)
        {
            for (var index = 0; index < apps.Count; index++)
            {
                if (apps[index].IsAvailable)
                {
                    installed.Add(apps[index].Id);
                }
            }

            return;
        }

        var ids = new List<string>();
        CollectSavedHomeIds(saved, ids);
        for (var index = 0; index < ids.Count; index++)
        {
            installed.Add(ids[index]);
        }
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
        var tile = BuildItem(item, placed);
        if (tile is not null)
        {
            tile.Cell = new GridCell(item.Column, item.Row);
        }

        return tile;
    }

    private HomeTile? BuildItem(HomeItem item, HashSet<string> placed)
    {
        if (string.Equals(item.Kind, "folder", StringComparison.Ordinal))
        {
            var contents = ResolveFolderMembers(item, placed);
            if (contents.Count == 0)
            {
                return null;
            }

            return contents.Count == 1
                ? HomeTile.AsLeaf(contents[0])
                : HomeTile.ForFolder(NextFolderKey(), item.FolderName, contents, item.FolderTint);
        }

        if (string.Equals(item.Kind, "shortcut", StringComparison.Ordinal))
        {
            if (!Guid.TryParse(item.ShortcutId, out var shortcutId) || shortcuts.Find(shortcutId) is not { } shortcut)
            {
                return null;
            }

            return HomeTile.ForShortcut(shortcut);
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

    private List<HomeTile> ResolveFolderMembers(HomeItem item, HashSet<string> placed)
    {
        if (item.Members.Count > 0)
        {
            var contents = new List<HomeTile>(item.Members.Count);
            for (var index = 0; index < item.Members.Count; index++)
            {
                if (ResolveFolderMember(item.Members[index], placed) is { } member)
                {
                    contents.Add(member);
                }
            }

            return contents;
        }

        var apps = ResolveApps(item.AppIds, placed);
        var legacy = new List<HomeTile>(apps.Count);
        for (var index = 0; index < apps.Count; index++)
        {
            legacy.Add(HomeTile.ForApp(apps[index]));
        }

        return legacy;
    }

    private HomeTile? ResolveFolderMember(HomeItem item, HashSet<string> placed)
    {
        if (string.Equals(item.Kind, "shortcut", StringComparison.Ordinal))
        {
            if (!Guid.TryParse(item.ShortcutId, out var shortcutId) || shortcuts.Find(shortcutId) is not { } shortcut)
            {
                return null;
            }

            return HomeTile.ForShortcut(shortcut);
        }

        if (byId.TryGetValue(item.AppId, out var app) && app.IsAvailable && placed.Add(app.Id))
        {
            return HomeTile.ForApp(app);
        }

        return null;
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

        pages[^1].Add(tile);
    }

    private void Arrange()
    {
        FoldDegenerateFolders();
        for (var page = 0; page < pages.Count; page++)
        {
            PlacePage(pages[page]);
            if (overflow.Count == 0)
            {
                continue;
            }

            if (page + 1 >= pages.Count)
            {
                pages.Add(new List<HomeTile>());
            }

            pages[page + 1].InsertRange(0, overflow);
        }

        for (var page = pages.Count - 1; page > 0; page--)
        {
            if (pages[page].Count == 0)
            {
                pages.RemoveAt(page);
            }
        }
    }

    private void PlacePage(List<HomeTile> tiles)
    {
        Span<bool> occupied = stackalloc bool[HomeGridSolver.MaxCells];
        occupied.Clear();
        pending.Clear();
        overflow.Clear();
        for (var index = 0; index < tiles.Count; index++)
        {
            var tile = tiles[index];
            if (HomeGridSolver.IsAssigned(tile.Cell) &&
                HomeGridSolver.RegionFree(occupied, Columns, rows, tile.Cell, tile.ColumnSpan, tile.RowSpan))
            {
                HomeGridSolver.Mark(occupied, Columns, tile.Cell, tile.ColumnSpan, tile.RowSpan);
                continue;
            }

            pending.Add(tile);
        }

        for (var index = 0; index < pending.Count; index++)
        {
            var tile = pending[index];
            if (!HomeGridSolver.TryFindFree(occupied, Columns, rows, tile.ColumnSpan, tile.RowSpan, tile.Cell,
                    out var cell))
            {
                tile.Cell = HomeGridSolver.Unassigned;
                overflow.Add(tile);
                tiles.Remove(tile);
                continue;
            }

            tile.Cell = cell;
            HomeGridSolver.Mark(occupied, Columns, cell, tile.ColumnSpan, tile.RowSpan);
        }
    }

    private void FoldDegenerateFolders()
    {
        for (var page = 0; page < pages.Count; page++)
        {
            for (var index = pages[page].Count - 1; index >= 0; index--)
            {
                var tile = pages[page][index];
                if (!tile.IsFolder || tile.Members.Count > 1)
                {
                    continue;
                }

                if (tile.Members.Count == 1)
                {
                    var folded = HomeTile.AsLeaf(tile.Members[0]);
                    folded.Cell = tile.Cell;
                    pages[page][index] = folded;
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
            var tiles = pages[page];
            var cells = placements[page];
            cells.Clear();
            for (var index = 0; index < tiles.Count; index++)
            {
                cells.Add(tiles[index].Cell);
            }
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

        SerializePages(layout.Pages);
        layout.Installed = new List<string>(installed);
        layout.Known = new List<string>(known);

        configuration.Home = layout;
        configuration.Save();
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

                for (var memberIndex = 0; memberIndex < item.Members.Count; memberIndex++)
                {
                    var member = item.Members[memberIndex];
                    if (!string.IsNullOrEmpty(member.AppId))
                    {
                        target.Add(member.AppId);
                    }
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

    private void SerializePages(List<HomePage> target)
    {
        for (var page = 0; page < pages.Count; page++)
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
        var item = BuildStoredItem(tile);
        item.Column = tile.Cell.Column;
        item.Row = tile.Cell.Row;
        return item;
    }

    private static HomeItem BuildStoredItem(HomeTile tile)
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

        if (tile.IsShortcut)
        {
            return new HomeItem { Kind = "shortcut", ShortcutId = tile.Shortcut!.Id.ToString("N") };
        }

        if (tile.IsFolder)
        {
            var item = new HomeItem { Kind = "folder", FolderName = tile.FolderName, FolderTint = tile.FolderTint };
            for (var memberIndex = 0; memberIndex < tile.Members.Count; memberIndex++)
            {
                var member = tile.Members[memberIndex];
                if (member.IsShortcut)
                {
                    item.Members.Add(new HomeItem
                    {
                        Kind = "shortcut", ShortcutId = member.Shortcut!.Id.ToString("N"),
                    });
                    continue;
                }

                var appId = member.App!.Id;
                item.Members.Add(new HomeItem { Kind = "app", AppId = appId });

                // Mirrored into AppIds so rolling back to a build that only reads AppIds keeps the app grouping.
                item.AppIds.Add(appId);
            }

            return item;
        }

        return new HomeItem { Kind = "app", AppId = tile.App!.Id };
    }

    private string NextFolderKey() => string.Concat("folder#", (++folderCounter).ToString());

    private string NextWidgetKey(string widgetId) =>
        string.Concat("widget#", widgetId, "#", (++widgetCounter).ToString());
}
