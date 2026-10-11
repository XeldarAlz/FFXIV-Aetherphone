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
    private static readonly string[] DefaultDockApps = { "message", "messages", "camera", "settings" };

    internal static readonly string[] DefaultFirstPageApps =
    {
        "chirper", "aethergram", "velvet", "aetherstream",
        "venues", "feedback", "market", "maps",
        "music", "games", "polls", "photos",
        "announcements", "coin", "yellowpages", "appstore",
    };

    internal static readonly string[] DefaultSecondPageApps =
    {
        "skywatcher", "collections", "inventory", "fishing",
        "clock", "notes", "calculator", "timers", "shortcuts",
        "wallet", "dailies", "calendar", "news",
        "character", "notifications", "jobs", "strats",
        "health",
    };

    private static readonly string[] MandatoryApps =
    {
        "appstore", "settings", "announcements", "messages", "camera", "photos", "notifications",
    };

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
    private readonly HashSet<string> widgetKeys = new(StringComparer.Ordinal);
    private int rows;
    private int folderCounter;
    private int stackCounter;
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

    public void Reload()
    {
        rows = ClampRows(configuration.HomeGridRows);
        Load();
        Save();
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

    public HomeTile? FindWidget(string instanceKey)
    {
        for (var page = 0; page < pages.Count; page++)
        {
            var tiles = pages[page];
            for (var index = 0; index < tiles.Count; index++)
            {
                var tile = tiles[index];
                if (!tile.IsStack)
                {
                    if (tile.IsWidget && string.Equals(tile.InstanceKey, instanceKey, StringComparison.Ordinal))
                    {
                        return tile;
                    }

                    continue;
                }

                var members = tile.Stack;
                for (var memberIndex = 0; memberIndex < members.Count; memberIndex++)
                {
                    if (string.Equals(members[memberIndex].InstanceKey, instanceKey, StringComparison.Ordinal))
                    {
                        return members[memberIndex];
                    }
                }
            }
        }

        return null;
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

    public void MoveFolderMember(HomeTile folder, int fromIndex, int toIndex)
    {
        if (!folder.IsFolder || fromIndex == toIndex
            || fromIndex < 0 || fromIndex >= folder.Members.Count
            || toIndex < 0 || toIndex >= folder.Members.Count)
        {
            return;
        }

        var member = folder.Members[fromIndex];
        folder.Members.RemoveAt(fromIndex);
        folder.Members.Insert(toIndex, member);
        Save();
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
                var tile = tiles[index];
                if (tile.IsStack)
                {
                    DropUninstalledStackMembers(tile);
                    continue;
                }

                if (tile.Widget is { } widget && !installed.Contains(widget.AppId))
                {
                    tiles.RemoveAt(index);
                }
            }
        }
    }

    private void DropUninstalledStackMembers(HomeTile stack)
    {
        var visible = stack.Visible;
        for (var memberIndex = stack.Stack.Count - 1; memberIndex >= 0; memberIndex--)
        {
            if (!installed.Contains(stack.Stack[memberIndex].Widget!.AppId))
            {
                stack.Stack.RemoveAt(memberIndex);
            }
        }

        var kept = stack.Stack.IndexOf(visible);
        stack.StackIndex = kept >= 0 ? kept : 0;
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
        pages[pageIndex].Add(HomeTile.ForWidget(NewWidgetKey(), widget, size, string.Empty));
        Commit();
        return true;
    }

    public void SetWidgetConfig(HomeTile tile, string config)
    {
        config ??= string.Empty;
        if (!tile.IsWidget || string.Equals(tile.Config, config, StringComparison.Ordinal))
        {
            return;
        }

        tile.Config = config;
        Save();
    }

    public void ResizeWidget(HomeTile tile, WidgetSize size)
    {
        if (!tile.IsWidget || tile.Size == size || !WidgetSizes.Contains(SizesOf(tile), size))
        {
            return;
        }

        ApplySize(tile, size);
        Commit();
    }

    public bool TryFitSize(HomeTile tile, WidgetSize size, out GridCell cell)
    {
        cell = HomeGridSolver.Unassigned;
        if (!tile.IsWidget || !WidgetSizes.Contains(SizesOf(tile), size) || !HomeGridSolver.IsAssigned(tile.Cell))
        {
            return false;
        }

        var (page, _) = Locate(tile);
        if (page < 0)
        {
            return false;
        }

        var columnSpan = WidgetSizes.ColumnSpan(size);
        var rowSpan = WidgetSizes.RowSpan(size);
        var candidate = new GridCell(Math.Min(tile.Cell.Column, Columns - columnSpan),
            Math.Min(tile.Cell.Row, rows - rowSpan));
        Span<bool> occupied = stackalloc bool[HomeGridSolver.MaxCells];
        occupied.Clear();
        Occupy(occupied, pages[page], tile);
        if (!HomeGridSolver.RegionFree(occupied, Columns, rows, candidate, columnSpan, rowSpan))
        {
            return false;
        }

        cell = candidate;
        return true;
    }

    public bool TryResizeInPlace(HomeTile tile, WidgetSize size)
    {
        if (tile.Size == size || !TryFitSize(tile, size, out var cell))
        {
            return false;
        }

        tile.Cell = cell;
        ApplySize(tile, size);
        Commit();
        return true;
    }

    public static WidgetSizeSet SizesOf(HomeTile tile)
    {
        if (!tile.IsStack)
        {
            return tile.Widget?.Sizes ?? WidgetSizeSet.None;
        }

        var sizes = WidgetSizeSet.Small | WidgetSizeSet.Medium | WidgetSizeSet.Large;
        for (var index = 0; index < tile.Stack.Count; index++)
        {
            sizes &= tile.Stack[index].Widget!.Sizes;
        }

        return sizes;
    }

    private static void ApplySize(HomeTile tile, WidgetSize size)
    {
        tile.Size = size;
        for (var index = 0; index < tile.Stack.Count; index++)
        {
            tile.Stack[index].Size = size;
        }
    }

    public bool CanStack(HomeTile target, HomeTile dragged)
    {
        if (ReferenceEquals(target, dragged) || !target.IsWidget || !dragged.IsWidget || target.Size != dragged.Size)
        {
            return false;
        }

        return StackCount(target) + StackCount(dragged) <= HomeTile.StackCapacity && Locate(target).Page >= 0;
    }

    public HomeTile? MakeStack(HomeTile target, HomeTile dragged)
    {
        if (!CanStack(target, dragged) || !Detach(dragged))
        {
            return null;
        }

        var (page, index) = Locate(target);
        var stack = target;
        if (!target.IsStack)
        {
            stack = HomeTile.ForStack(NextStackKey(), new[] { target }, 0, true);
            stack.Cell = target.Cell;
            pages[page][index] = stack;
        }

        var firstAdded = stack.Stack.Count;
        if (dragged.IsStack)
        {
            stack.Stack.AddRange(dragged.Stack);
        }
        else
        {
            stack.Stack.Add(dragged);
        }

        ApplySize(stack, stack.Size);
        stack.StackIndex = firstAdded;
        Commit();
        return stack;
    }

    public bool AddStack(IReadOnlyList<IHomeWidget> members, WidgetSize size, int pageIndex)
    {
        var tiles = new List<HomeTile>(Math.Min(members.Count, HomeTile.StackCapacity));
        for (var index = 0; index < members.Count && tiles.Count < HomeTile.StackCapacity; index++)
        {
            var widget = members[index];
            if (WidgetSizes.Contains(widget.Sizes, size))
            {
                tiles.Add(HomeTile.ForWidget(NewWidgetKey(), widget, size, string.Empty));
            }
        }

        if (tiles.Count == 0)
        {
            return false;
        }

        pageIndex = Math.Clamp(pageIndex, 0, Math.Max(0, pages.Count - 1));
        pages[pageIndex].Add(tiles.Count == 1 ? tiles[0] : HomeTile.ForStack(NextStackKey(), tiles, 0, true));
        Commit();
        return true;
    }

    public void RemoveStackMember(HomeTile stack, HomeTile member)
    {
        if (!stack.IsStack)
        {
            return;
        }

        var visible = stack.Visible;
        var removedIndex = stack.Stack.IndexOf(member);
        if (removedIndex < 0)
        {
            return;
        }

        stack.Stack.RemoveAt(removedIndex);
        var kept = stack.Stack.IndexOf(visible);
        stack.StackIndex = kept >= 0 ? kept : Math.Min(removedIndex, stack.Stack.Count - 1);
        Commit();
    }

    public void MoveStackMember(HomeTile stack, int fromIndex, int toIndex)
    {
        var count = stack.Stack.Count;
        if (fromIndex < 0 || fromIndex >= count || toIndex < 0 || toIndex >= count || fromIndex == toIndex)
        {
            return;
        }

        var visible = stack.Visible;
        var member = stack.Stack[fromIndex];
        stack.Stack.RemoveAt(fromIndex);
        stack.Stack.Insert(toIndex, member);
        stack.StackIndex = stack.Stack.IndexOf(visible);
        Save();
    }

    public void SetStackIndex(HomeTile stack, int index)
    {
        if (!stack.IsStack || index < 0 || index >= stack.Stack.Count || stack.StackIndex == index)
        {
            return;
        }

        stack.StackIndex = index;
        Save();
    }

    public void SetSmartRotate(HomeTile stack, bool enabled)
    {
        if (!stack.IsStack || stack.SmartRotate == enabled)
        {
            return;
        }

        stack.SmartRotate = enabled;
        Save();
    }

    public void Persist() => Save();

    private static int StackCount(HomeTile tile) => tile.IsStack ? tile.Stack.Count : 1;

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
        widgetKeys.Clear();
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
            return BuildWidget(item, null);
        }

        if (string.Equals(item.Kind, "stack", StringComparison.Ordinal))
        {
            return BuildStack(item);
        }

        return byId.TryGetValue(item.AppId, out var app) && app.IsAvailable && placed.Add(app.Id)
            ? HomeTile.ForApp(app)
            : null;
    }

    private HomeTile? BuildWidget(HomeItem item, WidgetSize? required)
    {
        if (!widgets.TryGet(item.WidgetId, out var widget) || !widgets.IsAvailable(widget))
        {
            return null;
        }

        var size = required ?? WidgetSizes.Parse(item.WidgetSize);
        if (!WidgetSizes.Contains(widget.Sizes, size))
        {
            if (required is not null)
            {
                return null;
            }

            size = WidgetSizes.Smallest(widget.Sizes);
        }

        var key = ClaimWidgetKey(item.WidgetKey, widget.Id, item.Column, item.Row);
        return HomeTile.ForWidget(key, widget, size, item.WidgetConfig);
    }

    private HomeTile? BuildStack(HomeItem item)
    {
        var size = WidgetSizes.Parse(item.WidgetSize);
        var shared = SharedSizes(item);
        if (shared != WidgetSizeSet.None && !WidgetSizes.Contains(shared, size))
        {
            size = WidgetSizes.Smallest(shared);
        }

        var members = new List<HomeTile>(item.Members.Count);
        var visibleIndex = 0;
        for (var index = 0; index < item.Members.Count && members.Count < HomeTile.StackCapacity; index++)
        {
            if (BuildWidget(item.Members[index], size) is not { } member)
            {
                continue;
            }

            if (index <= item.StackIndex)
            {
                visibleIndex = members.Count;
            }

            members.Add(member);
        }

        if (members.Count <= 1)
        {
            return members.Count == 1 ? members[0] : null;
        }

        return HomeTile.ForStack(NextStackKey(), members, visibleIndex, item.SmartRotate);
    }

    private WidgetSizeSet SharedSizes(HomeItem item)
    {
        var shared = WidgetSizeSet.Small | WidgetSizeSet.Medium | WidgetSizeSet.Large;
        var any = false;
        for (var index = 0; index < item.Members.Count; index++)
        {
            if (!widgets.TryGet(item.Members[index].WidgetId, out var widget) || !widgets.IsAvailable(widget))
            {
                continue;
            }

            shared &= widget.Sizes;
            any = true;
        }

        return any ? shared : WidgetSizeSet.None;
    }

    private void SeedDefaultLayout(HashSet<string> placed)
    {
        var firstPage = new List<HomeTile>();
        if (widgets.TryGet(DefaultWidgetId, out var widget) && widgets.IsAvailable(widget))
        {
            firstPage.Add(HomeTile.ForWidget(NewWidgetKey(), widget, WidgetSize.Medium, string.Empty));
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
        FoldDegenerateStacks();
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

    private void FoldDegenerateStacks()
    {
        for (var page = 0; page < pages.Count; page++)
        {
            for (var index = pages[page].Count - 1; index >= 0; index--)
            {
                var tile = pages[page][index];
                if (tile.Stack.Count != 1)
                {
                    continue;
                }

                var folded = tile.Stack[0];
                folded.Cell = tile.Cell;
                folded.Size = tile.Size;
                pages[page][index] = folded;
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
        if (tile.IsStack)
        {
            var stack = new HomeItem
            {
                Kind = "stack",
                WidgetSize = WidgetSizes.Serialize(tile.Size),
                StackIndex = tile.StackIndex,
                SmartRotate = tile.SmartRotate,
                Members = new List<HomeItem>(tile.Stack.Count),
            };
            for (var memberIndex = 0; memberIndex < tile.Stack.Count; memberIndex++)
            {
                stack.Members.Add(BuildStoredItem(tile.Stack[memberIndex]));
            }

            return stack;
        }

        if (tile.IsWidget)
        {
            return new HomeItem
            {
                Kind = "widget",
                WidgetId = tile.Widget!.Id,
                WidgetSize = WidgetSizes.Serialize(tile.Size),
                WidgetKey = tile.InstanceKey,
                WidgetConfig = tile.Config,
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

    private string NextStackKey() => string.Concat("stack#", (++stackCounter).ToString());

    private string NewWidgetKey()
    {
        var key = Guid.NewGuid().ToString("N");
        widgetKeys.Add(key);
        return key;
    }

    private string ClaimWidgetKey(string stored, string widgetId, int column, int row)
    {
        if (!string.IsNullOrEmpty(stored) && widgetKeys.Add(stored))
        {
            return stored;
        }

        var derived = string.Concat(widgetId, "@", column.ToString(), ".", row.ToString());
        var candidate = derived;
        for (var suffix = 2; !widgetKeys.Add(candidate); suffix++)
        {
            candidate = string.Concat(derived, "#", suffix.ToString());
        }

        return candidate;
    }
}
