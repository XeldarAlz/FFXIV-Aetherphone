using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HomeLayoutServiceInstallTests
{
    [Fact]
    public void FreshInstall_InstallsEveryAvailableApp()
    {
        var apps = MakeApps();
        var layout = BuildLayout(apps, new FakeHomeConfiguration());

        for (var index = 0; index < apps.Count; index++)
        {
            Assert.True(layout.IsInstalled(apps[index].Id), $"App '{apps[index].Id}' should ship installed");
            Assert.True(PageIndexOf(layout, apps[index].Id) >= 0, $"App '{apps[index].Id}' should be on Home");
        }
    }

    [Fact]
    public void AppShippedAfterTheSaveWasWritten_IsNotInstalled()
    {
        var apps = MakeApps();
        var configuration = SavedWith("a", "b");

        var layout = BuildLayout(apps, configuration);

        Assert.False(layout.IsInstalled("c"), "An app the user has never had should not install itself");
        Assert.Equal(-1, PageIndexOf(layout, "c"));
    }

    [Fact]
    public void Install_PutsTheAppOnHomeAndSurvivesAReload()
    {
        var apps = MakeApps();
        var configuration = SavedWith("a", "b");

        var first = BuildLayout(apps, configuration);
        Assert.True(first.Install("c"));
        Assert.True(PageIndexOf(first, "c") >= 0);

        var reloaded = BuildLayout(apps, configuration);

        Assert.True(reloaded.IsInstalled("c"));
        Assert.True(PageIndexOf(reloaded, "c") >= 0, "An installed app should still be on Home after a reload");
    }

    [Fact]
    public void Uninstall_TakesTheAppOffHomeAndSurvivesAReload()
    {
        var apps = MakeApps();
        var configuration = SavedWith("a", "b", "c");

        var first = BuildLayout(apps, configuration);
        Assert.True(first.Uninstall("c"));
        Assert.Equal(-1, PageIndexOf(first, "c"));

        var reloaded = BuildLayout(apps, configuration);

        Assert.False(reloaded.IsInstalled("c"));
        Assert.Equal(-1, PageIndexOf(reloaded, "c"));
    }

    [Fact]
    public void UninstalledApp_DoesNotComeBackWhenItsAvailabilityFlips()
    {
        var apps = MakeApps();
        var toggled = (FakeApp)apps[2];
        var configuration = SavedWith("a", "b", "c");

        BuildLayout(apps, configuration).Uninstall("c");
        toggled.IsAvailable = false;
        BuildLayout(apps, configuration);
        toggled.IsAvailable = true;
        var layout = BuildLayout(apps, configuration);

        Assert.False(layout.IsInstalled("c"), "An app the user removed must stay removed");
    }

    [Fact]
    public void InstalledAppThatGoesUnavailable_ReturnsToHomeWhenItComesBack()
    {
        var apps = MakeApps();
        var toggled = (FakeApp)apps[2];
        var configuration = SavedWith("a", "b", "c");

        BuildLayout(apps, configuration).Install("c");
        toggled.IsAvailable = false;
        var hidden = BuildLayout(apps, configuration);
        hidden.MoveTile(hidden.Page(0)[0], 0, 0);
        toggled.IsAvailable = true;
        var layout = BuildLayout(apps, configuration);

        Assert.True(layout.IsInstalled("c"));
        Assert.True(PageIndexOf(layout, "c") >= 0,
            "An installed app that was hidden for a while must come back to Home, not be treated as uninstalled");
    }

    [Fact]
    public void AppStore_IsAlwaysInstalledAndCannotBeUninstalled()
    {
        var apps = new List<IPhoneApp> { new FakeApp("a"), new FakeApp("appstore") };
        var configuration = SavedWith("a");

        var layout = BuildLayout(apps, configuration);

        Assert.True(layout.IsInstalled("appstore"), "The store has to stay reachable or there is no way back");
        Assert.True(PageIndexOf(layout, "appstore") >= 0);
        Assert.False(layout.Uninstall("appstore"));
        Assert.True(layout.IsInstalled("appstore"));
    }

    [Fact]
    public void UninstallingAnAppInsideAFolder_RemovesItFromThatFolder()
    {
        var apps = MakeApps();
        var configuration = new FakeHomeConfiguration
        {
            Home = new HomeLayout
            {
                Dock = new List<string>(),
                Pages = new List<HomePage>
                {
                    new()
                    {
                        Items = new List<HomeItem>
                        {
                            new() { Kind = "app", AppId = "a" },
                            new() { Kind = "folder", FolderName = "Stuff", AppIds = new List<string> { "b", "c" } },
                        },
                    },
                },
                Installed = new List<string> { "a", "b", "c" },
            },
        };

        var layout = BuildLayout(apps, configuration);
        Assert.True(layout.Uninstall("b"));

        var reloaded = BuildLayout(apps, configuration);

        Assert.False(reloaded.IsInstalled("b"));
        Assert.Equal(-1, PageIndexOf(reloaded, "b"));
        Assert.True(PageIndexOf(reloaded, "c") >= 0, "The other folder member should survive as a plain icon");
    }

    private static HomeLayoutService BuildLayout(List<IPhoneApp> apps, FakeHomeConfiguration configuration) =>
        new(apps, new WidgetRegistry(Array.Empty<IHomeWidget>(), apps), configuration);

    private static FakeHomeConfiguration SavedWith(params string[] appIds)
    {
        var page = new HomePage();
        for (var index = 0; index < appIds.Length; index++)
        {
            page.Items.Add(new HomeItem { Kind = "app", AppId = appIds[index] });
        }

        return new FakeHomeConfiguration
        {
            Home = new HomeLayout
            {
                Dock = new List<string>(),
                Pages = new List<HomePage> { page },
                Installed = new List<string>(appIds),
            },
        };
    }

    private static int PageIndexOf(HomeLayoutService layout, string appId)
    {
        for (var page = 0; page < layout.PageCount; page++)
        {
            var tiles = layout.Page(page);
            for (var index = 0; index < tiles.Count; index++)
            {
                var tile = tiles[index];
                if (tile.App?.Id == appId)
                {
                    return page;
                }

                for (var appIndex = 0; appIndex < tile.Apps.Count; appIndex++)
                {
                    if (tile.Apps[appIndex].Id == appId)
                    {
                        return page;
                    }
                }
            }
        }

        return -1;
    }

    private static List<IPhoneApp> MakeApps() =>
        new()
        {
            new FakeApp("a"),
            new FakeApp("b"),
            new FakeApp("c"),
        };

    private sealed class FakeApp : IPhoneApp
    {
        public FakeApp(string id) => Id = id;
        public string Id { get; }
        public string DisplayName => Id;
        public string Glyph => string.Empty;
        public int BadgeCount => 0;
        public bool IsAvailable { get; set; } = true;
        public void OnOpened() { }
        public void OnClosed() { }
        public void Draw(in PhoneContext context) { }
        public void Dispose() { }
    }

    private sealed class FakeHomeConfiguration : IHomeConfiguration
    {
        public HomeLayout? Home { get; set; }
        public int HomeGridRows { get; set; }
        public void Save() { }
    }
}
