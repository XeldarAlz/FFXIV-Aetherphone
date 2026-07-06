using Aetherphone.Core.Apps;

namespace Aetherphone.Core.Home;

internal sealed class HomeLayoutService
{
    public const int Columns = 4;
    public const int Rows = 8;
    public const int Capacity = Columns * Rows;
    private readonly IReadOnlyList<IPhoneApp> apps;
    private readonly Configuration configuration;
    private readonly Dictionary<string, IPhoneApp> byId = new();
    private readonly List<List<HomeTile>> pages = new();
    private readonly bool[] availability;
    private int folderCounter;

    public HomeLayoutService(IReadOnlyList<IPhoneApp> apps, Configuration configuration)
    {
        this.apps = apps;
        this.configuration = configuration;
        availability = new bool[apps.Count];
        for (var index = 0; index < apps.Count; index++)
        {
            byId[apps[index].Id] = apps[index];
            availability[index] = apps[index].IsAvailable;
        }

        Load();
    }

    public int PageCount => pages.Count;
    public IReadOnlyList<HomeTile> Page(int index) => pages[index];

    public void EnsureAvailability()
    {
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
        }
    }

    public (int Page, int Slot) Locate(HomeTile tile)
    {
        for (var page = 0; page < pages.Count; page++)
        {
            var slot = pages[page].IndexOf(tile);
            if (slot >= 0)
            {
                return (page, slot);
            }
        }

        return (-1, -1);
    }

    public void MoveTile(HomeTile tile, int targetPage, int targetSlot)
    {
        if (targetPage < 0 || targetPage >= pages.Count)
        {
            return;
        }

        var (page, slot) = Locate(tile);
        if (page < 0)
        {
            return;
        }

        pages[page].RemoveAt(slot);
        targetSlot = Math.Clamp(targetSlot, 0, pages[targetPage].Count);
        pages[targetPage].Insert(targetSlot, tile);
        Commit();
    }

    public void MakeFolder(HomeTile target, HomeTile dragged)
    {
        if (ReferenceEquals(target, dragged) || target.IsFolder && dragged.IsFolder)
        {
            return;
        }

        var (page, slot) = Locate(dragged);
        if (page >= 0)
        {
            pages[page].RemoveAt(slot);
        }

        if (target.IsFolder)
        {
            AddApps(target, dragged);
        }
        else
        {
            var (targetPage, targetSlot) = Locate(target);
            if (targetPage < 0)
            {
                return;
            }

            var folder = HomeTile.ForFolder(NextFolderKey(), string.Empty, new[] { target.App! });
            AddApps(folder, dragged);
            pages[targetPage][targetSlot] = folder;
        }

        Commit();
    }

    public void RemoveFromFolder(HomeTile folder, IPhoneApp app, int targetPage, int targetSlot)
    {
        if (!folder.IsFolder || !folder.Apps.Remove(app))
        {
            return;
        }

        targetPage = Math.Clamp(targetPage, 0, Math.Max(0, pages.Count - 1));
        targetSlot = Math.Clamp(targetSlot, 0, pages[targetPage].Count);
        pages[targetPage].Insert(targetSlot, HomeTile.ForApp(app));
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

    public void Commit()
    {
        Normalize();
        Save();
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
        var placed = new HashSet<string>();
        if (configuration.Home is { } saved && saved.Pages.Count > 0)
        {
            for (var pageIndex = 0; pageIndex < saved.Pages.Count; pageIndex++)
            {
                var page = new List<HomeTile>();
                var items = saved.Pages[pageIndex].Items;
                for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
                {
                    var item = items[itemIndex];
                    if (string.Equals(item.Kind, "folder", StringComparison.Ordinal))
                    {
                        var contents = ResolveApps(item.AppIds, placed);
                        if (contents.Count == 0)
                        {
                            continue;
                        }

                        page.Add(contents.Count == 1
                            ? HomeTile.ForApp(contents[0])
                            : HomeTile.ForFolder(NextFolderKey(), item.FolderName, contents));
                    }
                    else if (byId.TryGetValue(item.AppId, out var app) && app.IsAvailable && placed.Add(app.Id))
                    {
                        page.Add(HomeTile.ForApp(app));
                    }
                }

                if (page.Count > 0)
                {
                    pages.Add(page);
                }
            }
        }

        for (var index = 0; index < apps.Count; index++)
        {
            if (apps[index].IsAvailable && placed.Add(apps[index].Id))
            {
                Append(HomeTile.ForApp(apps[index]));
            }
        }

        if (pages.Count == 0)
        {
            pages.Add(new List<HomeTile>());
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
        if (pages.Count == 0 || pages[^1].Count >= Capacity)
        {
            pages.Add(new List<HomeTile>());
        }

        pages[^1].Add(tile);
    }

    private void Normalize()
    {
        for (var page = 0; page < pages.Count; page++)
        {
            for (var slot = pages[page].Count - 1; slot >= 0; slot--)
            {
                var tile = pages[page][slot];
                if (tile.IsFolder && tile.Apps.Count <= 1)
                {
                    pages[page][slot] = tile.Apps.Count == 1 ? HomeTile.ForApp(tile.Apps[0]) : null!;
                    if (pages[page][slot] is null)
                    {
                        pages[page].RemoveAt(slot);
                    }
                }
            }

            if (pages[page].Count > Capacity)
            {
                var overflow = pages[page].GetRange(Capacity, pages[page].Count - Capacity);
                pages[page].RemoveRange(Capacity, pages[page].Count - Capacity);
                if (page + 1 >= pages.Count)
                {
                    pages.Add(new List<HomeTile>());
                }

                pages[page + 1].InsertRange(0, overflow);
            }
        }

        for (var page = pages.Count - 1; page > 0; page--)
        {
            if (pages[page].Count == 0)
            {
                pages.RemoveAt(page);
            }
        }
    }

    private void Save()
    {
        var layout = new HomeLayout();
        for (var page = 0; page < pages.Count; page++)
        {
            var stored = new HomePage();
            for (var slot = 0; slot < pages[page].Count; slot++)
            {
                var tile = pages[page][slot];
                if (tile.IsFolder)
                {
                    var item = new HomeItem { Kind = "folder", FolderName = tile.FolderName };
                    for (var appIndex = 0; appIndex < tile.Apps.Count; appIndex++)
                    {
                        item.AppIds.Add(tile.Apps[appIndex].Id);
                    }

                    stored.Items.Add(item);
                }
                else
                {
                    stored.Items.Add(new HomeItem { Kind = "app", AppId = tile.App!.Id });
                }
            }

            layout.Pages.Add(stored);
        }

        configuration.Home = layout;
        configuration.Save();
    }

    private string NextFolderKey() => string.Concat("folder#", (++folderCounter).ToString());
}
