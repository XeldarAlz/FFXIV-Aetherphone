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
        hidden.MoveTile(hidden.Page(0)[0], 0, new GridCell(1, 1));
        toggled.IsAvailable = true;
        var layout = BuildLayout(apps, configuration);

        Assert.True(layout.IsInstalled("c"));
        Assert.True(PageIndexOf(layout, "c") >= 0,
            "An installed app that was hidden for a while must come back to Home, not be treated as uninstalled");
    }

    [Fact]
    public void AppLaunchedByTheServer_ArrivesOnHomeByItself()
    {
        var apps = MakeApps();
        var launched = (FakeApp)apps[2];
        launched.IsAvailable = false;
        var configuration = SavedWith("a", "b");

        var layout = BuildLayout(apps, configuration);
        launched.IsAvailable = true;
        layout.EnsureCurrent();

        Assert.True(layout.IsInstalled("c"), "An app nobody has seen before should install itself when it launches");
        Assert.True(PageIndexOf(layout, "c") >= 0);
    }

    [Fact]
    public void AppTheUserRemoved_StaysOffHomeWhenTheServerRelaunchesIt()
    {
        var apps = MakeApps();
        var launched = (FakeApp)apps[2];
        var configuration = SavedWith("a", "b", "c");

        BuildLayout(apps, configuration).Uninstall("c");
        launched.IsAvailable = false;
        var layout = BuildLayout(apps, configuration);
        launched.IsAvailable = true;
        layout.EnsureCurrent();

        Assert.False(layout.IsInstalled("c"), "An app the user removed must not be resurrected by a server flag");
    }

    [Theory]
    [InlineData("appstore")]
    [InlineData("settings")]
    [InlineData("announcements")]
    public void MandatoryApp_IsAlwaysInstalledAndCannotBeUninstalled(string appId)
    {
        var apps = new List<IPhoneApp> { new FakeApp("a"), new FakeApp(appId) };
        var configuration = SavedWith("a");

        var layout = BuildLayout(apps, configuration);

        Assert.True(layout.IsInstalled(appId), $"'{appId}' must stay on the phone or the user has no way back");
        Assert.True(PageIndexOf(layout, appId) >= 0);
        Assert.False(layout.Uninstall(appId));
        Assert.True(layout.IsInstalled(appId));
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

    [Fact]
    public void Uninstall_TakesTheAppsWidgetsWithIt()
    {
        var apps = MakeApps();
        var widget = new FakeWidget("c.widget", "c");
        var configuration = SavedWith("a", "b", "c");
        configuration.Home!.Pages[0].Items.Add(new HomeItem
        {
            Kind = "widget",
            WidgetId = widget.Id,
            WidgetSize = WidgetSizes.Serialize(WidgetSize.Medium),
        });

        var layout = BuildLayout(apps, configuration, widget);
        Assert.True(WidgetIsOnHome(layout, widget.Id));
        Assert.True(layout.Uninstall("c"));

        Assert.False(WidgetIsOnHome(layout, widget.Id));
        Assert.False(WidgetIsOnHome(BuildLayout(apps, configuration, widget), widget.Id));
    }

    [Fact]
    public void InstallAndUninstall_RaiseInstalledChanged()
    {
        var apps = MakeApps();
        var layout = BuildLayout(apps, SavedWith("a", "b"));
        var changed = new List<string>();
        layout.InstalledChanged += changed.Add;

        layout.Install("c");
        layout.Uninstall("c");

        Assert.Equal(new[] { "c", "c" }, changed);
    }

    [Fact]
    public void Gate_ClosesWhenTheAppIsUninstalledAndReopensOnInstall()
    {
        var apps = MakeApps();
        var installer = new AppInstaller();
        installer.Bind(BuildLayout(apps, SavedWith("a", "b", "c")));
        var gate = installer.Gate("c");

        Assert.True(gate.Open);
        installer.Uninstall("c");
        Assert.False(gate.Open);
        installer.Install("c");
        Assert.True(gate.Open);
    }

    [Fact]
    public void UnboundGate_IsOpen()
    {
        Assert.True(default(AppGate).Open);
    }

    private static bool WidgetIsOnHome(HomeLayoutService layout, string widgetId)
    {
        for (var page = 0; page < layout.PageCount; page++)
        {
            var tiles = layout.Page(page);
            for (var index = 0; index < tiles.Count; index++)
            {
                if (tiles[index].Widget?.Id == widgetId)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static HomeLayoutService BuildLayout(List<IPhoneApp> apps, FakeHomeConfiguration configuration,
        params IHomeWidget[] widgets) =>
        new(apps, new WidgetRegistry(widgets, apps), new FakeShortcutSource(), configuration);

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

                for (var memberIndex = 0; memberIndex < tile.Members.Count; memberIndex++)
                {
                    if (tile.Members[memberIndex].App?.Id == appId)
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

    private sealed class FakeWidget : IHomeWidget
    {
        public FakeWidget(string id, string appId)
        {
            Id = id;
            AppId = appId;
        }

        public string Id { get; }
        public string DisplayName => Id;
        public string AppId { get; }
        public WidgetSizeSet Sizes => WidgetSizeSet.Medium;
        public void Draw(in WidgetContext context) { }
        public void Dispose() { }
    }

    private sealed class FakeHomeConfiguration : IHomeConfiguration
    {
        public HomeLayout? Home { get; set; }
        public int HomeGridRows { get; set; }
        public void Save() { }
    }
}
