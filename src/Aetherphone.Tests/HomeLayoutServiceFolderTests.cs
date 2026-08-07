using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Shortcuts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HomeLayoutServiceFolderTests
{
    [Fact]
    public void AShortcutDraggedOntoAnApp_MakesAFolderThatSurvivesAReload()
    {
        var apps = MakeApps("a", "b");
        var shortcuts = SourceWith(out var shortcut);
        var configuration = ConfigurationWith(AppItem("a"), AppItem("b"), ShortcutItem(shortcut));

        var layout = BuildLayout(apps, shortcuts, configuration);
        layout.MakeFolder(AppTile(layout, "a"), ShortcutTile(layout, shortcut));

        var reloaded = BuildLayout(apps, shortcuts, configuration);
        var folder = FolderTile(reloaded);

        Assert.Equal(2, folder.Members.Count);
        Assert.True(HoldsShortcut(folder, shortcut), "A shortcut dropped into a folder must come back after a reload");
        Assert.True(HoldsApp(folder, "a"));
    }

    [Fact]
    public void AShortcutInsideAFolder_CountsAsPinned()
    {
        var apps = MakeApps("a", "b");
        var shortcuts = SourceWith(out var shortcut);
        var configuration = ConfigurationWith(AppItem("a"), AppItem("b"), ShortcutItem(shortcut));

        var layout = BuildLayout(apps, shortcuts, configuration);
        layout.MakeFolder(AppTile(layout, "a"), ShortcutTile(layout, shortcut));

        Assert.True(layout.HasShortcut(shortcut.Id), "Shortcuts must not look unpinned once they move into a folder");
        Assert.False(layout.AddShortcut(shortcut.Id), "Pinning again would put a duplicate tile on Home");
    }

    [Fact]
    public void DeletingAShortcutThatLivesInAFolder_TakesItOutOfThatFolder()
    {
        var apps = MakeApps("a", "b");
        var shortcuts = SourceWith(out var shortcut);
        var configuration = ConfigurationWith(AppItem("a"), AppItem("b"), ShortcutItem(shortcut));

        var layout = BuildLayout(apps, shortcuts, configuration);
        layout.MakeFolder(AppTile(layout, "a"), ShortcutTile(layout, shortcut));

        Assert.True(layout.RemoveShortcut(shortcut.Id));
        Assert.False(layout.HasShortcut(shortcut.Id));

        shortcuts.Entries.Clear();
        var reloaded = BuildLayout(apps, shortcuts, configuration);

        Assert.Equal("a", AppTile(reloaded, "a").App!.Id);
    }

    [Fact]
    public void AFolderSavedByAnOlderBuild_StillGroupsItsApps()
    {
        var apps = MakeApps("a", "b", "c");
        var shortcuts = new FakeShortcutSource();
        var configuration = ConfigurationWith(AppItem("a"),
            new HomeItem { Kind = "folder", FolderName = "Stuff", AppIds = new List<string> { "b", "c" } });

        var layout = BuildLayout(apps, shortcuts, configuration);
        var folder = FolderTile(layout);

        Assert.Equal("Stuff", folder.FolderName);
        Assert.True(HoldsApp(folder, "b"));
        Assert.True(HoldsApp(folder, "c"));
    }

    [Fact]
    public void ASavedFolder_StillWritesAppIdsForBuildsThatOnlyReadThem()
    {
        var apps = MakeApps("a", "b");
        var shortcuts = SourceWith(out var shortcut);
        var configuration = ConfigurationWith(AppItem("a"), AppItem("b"), ShortcutItem(shortcut));

        var layout = BuildLayout(apps, shortcuts, configuration);
        layout.MakeFolder(AppTile(layout, "a"), ShortcutTile(layout, shortcut));

        var stored = SavedFolder(configuration);
        Assert.Equal(new[] { "a" }, stored.AppIds);
    }

    private static HomeLayoutService BuildLayout(List<IPhoneApp> apps, FakeShortcutSource shortcuts,
        FakeHomeConfiguration configuration) =>
        new(apps, new WidgetRegistry(Array.Empty<IHomeWidget>(), apps), shortcuts, configuration);

    private static FakeShortcutSource SourceWith(out ShortcutEntry shortcut)
    {
        shortcut = new ShortcutEntry { Name = "Launch" };
        var source = new FakeShortcutSource();
        source.Entries.Add(shortcut);
        return source;
    }

    private static FakeHomeConfiguration ConfigurationWith(params HomeItem[] items)
    {
        var page = new HomePage();
        var installed = new List<string>();
        for (var index = 0; index < items.Length; index++)
        {
            page.Items.Add(items[index]);
            if (!string.IsNullOrEmpty(items[index].AppId))
            {
                installed.Add(items[index].AppId);
            }

            for (var appIndex = 0; appIndex < items[index].AppIds.Count; appIndex++)
            {
                installed.Add(items[index].AppIds[appIndex]);
            }
        }

        return new FakeHomeConfiguration
        {
            Home = new HomeLayout
            {
                Dock = new List<string>(),
                Pages = new List<HomePage> { page },
                Installed = installed,
            },
        };
    }

    private static HomeItem AppItem(string appId) => new() { Kind = "app", AppId = appId };

    private static HomeItem ShortcutItem(ShortcutEntry shortcut) =>
        new() { Kind = "shortcut", ShortcutId = shortcut.Id.ToString("N") };

    private static HomeItem SavedFolder(FakeHomeConfiguration configuration)
    {
        var items = configuration.Home!.Pages[0].Items;
        for (var index = 0; index < items.Count; index++)
        {
            if (string.Equals(items[index].Kind, "folder", StringComparison.Ordinal))
            {
                return items[index];
            }
        }

        throw new InvalidOperationException("The saved layout has no folder");
    }

    private static HomeTile FolderTile(HomeLayoutService layout)
    {
        var tiles = layout.Page(0);
        for (var index = 0; index < tiles.Count; index++)
        {
            if (tiles[index].IsFolder)
            {
                return tiles[index];
            }
        }

        throw new InvalidOperationException("Page 0 has no folder");
    }

    private static HomeTile AppTile(HomeLayoutService layout, string appId)
    {
        var tiles = layout.Page(0);
        for (var index = 0; index < tiles.Count; index++)
        {
            if (tiles[index].App?.Id == appId)
            {
                return tiles[index];
            }
        }

        throw new InvalidOperationException($"App '{appId}' is not on page 0");
    }

    private static HomeTile ShortcutTile(HomeLayoutService layout, ShortcutEntry shortcut)
    {
        var tiles = layout.Page(0);
        for (var index = 0; index < tiles.Count; index++)
        {
            if (tiles[index].Shortcut?.Id == shortcut.Id)
            {
                return tiles[index];
            }
        }

        throw new InvalidOperationException("The shortcut is not on page 0");
    }

    private static bool HoldsShortcut(HomeTile folder, ShortcutEntry shortcut)
    {
        for (var index = 0; index < folder.Members.Count; index++)
        {
            if (folder.Members[index].Shortcut?.Id == shortcut.Id)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HoldsApp(HomeTile folder, string appId)
    {
        for (var index = 0; index < folder.Members.Count; index++)
        {
            if (folder.Members[index].App?.Id == appId)
            {
                return true;
            }
        }

        return false;
    }

    private static List<IPhoneApp> MakeApps(params string[] ids)
    {
        var apps = new List<IPhoneApp>(ids.Length);
        for (var index = 0; index < ids.Length; index++)
        {
            apps.Add(new FakeApp(ids[index]));
        }

        return apps;
    }
}
