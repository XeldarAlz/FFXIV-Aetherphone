namespace Aetherphone.Core.Home;

internal static class HomeLookMirror
{
    public static Guid Resolve(IReadOnlyList<HomeLook> looks, IReadOnlyDictionary<ulong, Guid> assignments,
        ulong contentId)
    {
        if (looks.Count == 0)
        {
            return Guid.Empty;
        }

        if (assignments.TryGetValue(contentId, out var assigned) && IndexOf(looks, assigned) >= 0)
        {
            return assigned;
        }

        return looks[0].Id;
    }

    public static int IndexOf(IReadOnlyList<HomeLook> looks, Guid id)
    {
        for (var index = 0; index < looks.Count; index++)
        {
            if (looks[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }

    public static void Capture(ILookConfiguration configuration, HomeLook target)
    {
        var home = configuration.Home;
        target.Pages = home is null ? new List<HomePage>() : ClonePages(home.Pages);
        target.Dock = home?.Dock is null ? null : new List<string>(home.Dock);
        target.GridRows = configuration.HomeGridRows;
        target.ShowAppNames = configuration.ShowAppNames;
        target.ThemeMode = configuration.ThemeMode;
        target.AccentName = configuration.AccentName;
        target.AccentCustomHex = configuration.AccentCustomHex;
        target.PhoneCaseName = configuration.PhoneCaseName;
        target.LightWallpaperId = configuration.LightWallpaperId;
        target.DarkWallpaperId = configuration.DarkWallpaperId;
    }

    public static void Restore(ILookConfiguration configuration, HomeLook source)
    {
        if (source.Pages.Count == 0 && source.Dock is null)
        {
            configuration.Home = null;
        }
        else
        {
            var home = configuration.Home ?? new HomeLayout();
            home.Pages = ClonePages(source.Pages);
            home.Dock = source.Dock is null ? null : new List<string>(source.Dock);
            configuration.Home = home;
        }

        configuration.HomeGridRows = source.GridRows;
        configuration.ShowAppNames = source.ShowAppNames;
        configuration.ThemeMode = source.ThemeMode;
        configuration.AccentName = source.AccentName;
        configuration.AccentCustomHex = source.AccentCustomHex;
        configuration.PhoneCaseName = source.PhoneCaseName;
        configuration.LightWallpaperId = source.LightWallpaperId;
        configuration.DarkWallpaperId = source.DarkWallpaperId;
    }

    private static List<HomePage> ClonePages(List<HomePage> pages)
    {
        var copy = new List<HomePage>(pages.Count);
        for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
        {
            var items = pages[pageIndex].Items;
            var page = new HomePage { Items = new List<HomeItem>(items.Count) };
            for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
            {
                page.Items.Add(CloneItem(items[itemIndex]));
            }

            copy.Add(page);
        }

        return copy;
    }

    private static HomeItem CloneItem(HomeItem item)
    {
        var copy = new HomeItem
        {
            Kind = item.Kind,
            Column = item.Column,
            Row = item.Row,
            AppId = item.AppId,
            FolderName = item.FolderName,
            FolderTint = item.FolderTint,
            AppIds = new List<string>(item.AppIds),
            Members = new List<HomeItem>(item.Members.Count),
            WidgetId = item.WidgetId,
            WidgetSize = item.WidgetSize,
            ShortcutId = item.ShortcutId,
        };
        for (var index = 0; index < item.Members.Count; index++)
        {
            copy.Members.Add(CloneItem(item.Members[index]));
        }

        return copy;
    }
}
