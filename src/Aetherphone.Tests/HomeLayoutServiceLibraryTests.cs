using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HomeLayoutServiceLibraryTests
{
    [Fact]
    public void FreshInstall_AlwaysHasAReachableLibraryPage()
    {
        var apps = MakeApps();
        var widgets = new WidgetRegistry(Array.Empty<IHomeWidget>(), apps);
        var configuration = new FakeHomeConfiguration();

        var layout = new HomeLayoutService(apps, widgets, configuration);

        Assert.True(layout.TotalPageCount > layout.HomePageCount,
            $"Expected TotalPageCount ({layout.TotalPageCount}) > HomePageCount ({layout.HomePageCount})");
    }

    [Fact]
    public void ExistingFullyPlacedSave_StillGetsAReachableLibraryPage()
    {
        var apps = MakeApps();
        var widgets = new WidgetRegistry(Array.Empty<IHomeWidget>(), apps);
        var configuration = new FakeHomeConfiguration
        {
            Home = new HomeLayout
            {
                Dock = new List<string> { "a" },
                Pages = new List<HomePage>
                {
                    new()
                    {
                        Items = new List<HomeItem>
                        {
                            new() { Kind = "app", AppId = "b" },
                            new() { Kind = "app", AppId = "c" },
                        },
                    },
                },
                LibraryPages = new List<HomePage>(),
            },
        };

        var layout = new HomeLayoutService(apps, widgets, configuration);

        Assert.Equal(1, layout.HomePageCount);
        Assert.True(layout.TotalPageCount > layout.HomePageCount,
            $"Expected TotalPageCount ({layout.TotalPageCount}) > HomePageCount ({layout.HomePageCount}) for a legacy fully-placed save");
    }

    [Fact]
    public void UnplacedApp_LandsInLibraryNotHome()
    {
        var apps = MakeApps();
        var widgets = new WidgetRegistry(Array.Empty<IHomeWidget>(), apps);
        var configuration = new FakeHomeConfiguration
        {
            Home = new HomeLayout
            {
                Dock = new List<string> { "a" },
                Pages = new List<HomePage>
                {
                    new() { Items = new List<HomeItem> { new() { Kind = "app", AppId = "b" } } },
                },
                LibraryPages = new List<HomePage>(),
            },
        };

        var layout = new HomeLayoutService(apps, widgets, configuration);

        var foundOnHome = false;
        for (var page = 0; page < layout.HomePageCount; page++)
        {
            var tiles = layout.Page(page);
            for (var index = 0; index < tiles.Count; index++)
            {
                if (tiles[index].App?.Id == "c")
                {
                    foundOnHome = true;
                }
            }
        }

        var foundInLibrary = false;
        for (var page = layout.HomePageCount; page < layout.TotalPageCount; page++)
        {
            var tiles = layout.Page(page);
            for (var index = 0; index < tiles.Count; index++)
            {
                if (tiles[index].App?.Id == "c")
                {
                    foundInLibrary = true;
                }
            }
        }

        Assert.False(foundOnHome, "App 'c' was never placed anywhere and should not be force-appended to Home");
        Assert.True(foundInLibrary, "App 'c' should have been auto-appended to the Library section");
    }

    [Fact]
    public void ExistingSaveWithTwoFullHomePages_ArrowStillReachesLibrary()
    {
        // Mirrors a real account: two home pages, every app already placed, nothing unplaced.
        var apps = new List<IPhoneApp>
        {
            new FakeApp("a"), new FakeApp("b"), new FakeApp("c"), new FakeApp("d"),
            new FakeApp("e"), new FakeApp("f"), new FakeApp("g"), new FakeApp("h"),
        };
        var widgets = new WidgetRegistry(Array.Empty<IHomeWidget>(), apps);
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
                            new() { Kind = "app", AppId = "b" },
                            new() { Kind = "app", AppId = "c" },
                            new() { Kind = "app", AppId = "d" },
                        },
                    },
                    new()
                    {
                        Items = new List<HomeItem>
                        {
                            new() { Kind = "app", AppId = "e" },
                            new() { Kind = "app", AppId = "f" },
                            new() { Kind = "app", AppId = "g" },
                            new() { Kind = "app", AppId = "h" },
                        },
                    },
                },
                LibraryPages = new List<HomePage>(),
            },
        };

        var layout = new HomeLayoutService(apps, widgets, configuration);

        Assert.Equal(2, layout.HomePageCount);
        Assert.True(layout.TotalPageCount > layout.HomePageCount,
            $"Expected a reachable Library page after the last of {layout.HomePageCount} home pages, " +
            $"but TotalPageCount was {layout.TotalPageCount}");

        // Simulate the page-arrow gate: on the last home page, target = HomePageCount must still be < TotalPageCount.
        var onLastHomePage = layout.HomePageCount - 1;
        var arrowTarget = onLastHomePage + 1;
        Assert.True(arrowTarget <= layout.TotalPageCount - 1,
            "The right-arrow on the last home page would not render — TotalPageCount didn't grow past HomePageCount");
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
        public void OnOpened() { }
        public void OnClosed() { }
        public void Draw(in PhoneContext context) { }
        public void Dispose() { }
    }

    // Deliberately not `Configuration` — that type implements Dalamud's IPluginConfiguration, which
    // pulls in the Dalamud assembly the moment it's loaded. This satisfies only the narrow surface
    // HomeLayoutService actually needs, so these tests stay Dalamud-free like the rest of the suite.
    private sealed class FakeHomeConfiguration : IHomeConfiguration
    {
        public HomeLayout? Home { get; set; }
        public int HomeGridRows { get; set; }
        public void Save() { }
    }
}
