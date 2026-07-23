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
        var layout = BuildLayout(apps, new FakeHomeConfiguration());

        Assert.True(layout.TotalPageCount > layout.HomePageCount,
            $"Expected TotalPageCount ({layout.TotalPageCount}) > HomePageCount ({layout.HomePageCount})");
    }

    [Fact]
    public void FreshInstall_AppMissingFromTheSeedListLandsOnHome()
    {
        var apps = MakeApps();
        var layout = BuildLayout(apps, new FakeHomeConfiguration());

        for (var index = 0; index < apps.Count; index++)
        {
            var page = PageIndexOf(layout,apps[index].Id);
            Assert.True(page >= 0 && page < layout.HomePageCount,
                $"App '{apps[index].Id}' is not in the default seed layout, so a first-run user must still find it " +
                $"on Home, but it landed on page {page} of {layout.TotalPageCount}");
        }
    }

    [Fact]
    public void ExistingFullyPlacedSave_StillGetsAReachableLibraryPage()
    {
        var apps = MakeApps();
        var configuration = new FakeHomeConfiguration
        {
            Home = new HomeLayout
            {
                Dock = new List<string> { "a" },
                Pages = new List<HomePage> { PageOf("b", "c") },
                LibraryPages = new List<HomePage>(),
            },
        };

        var layout = BuildLayout(apps, configuration);

        Assert.Equal(1, layout.HomePageCount);
        Assert.True(layout.TotalPageCount > layout.HomePageCount,
            $"Expected TotalPageCount ({layout.TotalPageCount}) > HomePageCount ({layout.HomePageCount}) for a legacy fully-placed save");
    }

    [Fact]
    public void UnplacedApp_LandsInLibraryNotHome()
    {
        var apps = MakeApps();
        var configuration = new FakeHomeConfiguration
        {
            Home = new HomeLayout
            {
                Dock = new List<string> { "a" },
                Pages = new List<HomePage> { PageOf("b") },
                LibraryPages = new List<HomePage>(),
            },
        };

        var layout = BuildLayout(apps, configuration);

        var page = PageIndexOf(layout,"c");
        Assert.True(page >= layout.HomePageCount,
            $"App 'c' shipped after this user's layout was saved and should land in the Library, but it is on page {page}");
    }

    [Fact]
    public void AppThatBecomesUnavailable_ReturnsToHomeNotLibrary()
    {
        var apps = MakeApps();
        var hidden = (FakeApp)apps[2];
        var configuration = new FakeHomeConfiguration
        {
            Home = new HomeLayout
            {
                Dock = new List<string>(),
                Pages = new List<HomePage> { PageOf("a", "b", "c") },
                LibraryPages = new List<HomePage>(),
            },
        };

        Commit(BuildLayout(apps, configuration));
        hidden.IsAvailable = false;
        Commit(BuildLayout(apps, configuration));
        hidden.IsAvailable = true;
        var layout = BuildLayout(apps, configuration);

        var page = PageIndexOf(layout,"c");
        Assert.True(page >= 0 && page < layout.HomePageCount,
            $"App 'c' was only ever on Home and must come back to Home once it is available again, but it is on page {page}");
    }

    [Fact]
    public void LegacySaveWrittenWhileAppWasHidden_StillReturnsItToHome()
    {
        var apps = MakeApps();
        var hidden = (FakeApp)apps[2];
        var configuration = new FakeHomeConfiguration
        {
            Home = new HomeLayout
            {
                Dock = new List<string>(),
                Pages = new List<HomePage> { PageOf("a", "b", "c") },
                LibraryPages = new List<HomePage>(),
            },
        };

        hidden.IsAvailable = false;
        Commit(BuildLayout(apps, configuration));
        hidden.IsAvailable = true;
        var layout = BuildLayout(apps, configuration);

        var page = PageIndexOf(layout, "c");
        Assert.True(page >= 0 && page < layout.HomePageCount,
            $"App 'c' sat on Home in the pre-upgrade save and must not be exiled to the Library by the first save taken while it was hidden, but it is on page {page}");
    }

    [Fact]
    public void AppMovedToLibrary_StaysThereAcrossReloads()
    {
        var apps = MakeApps();
        var configuration = new FakeHomeConfiguration
        {
            Home = new HomeLayout
            {
                Dock = new List<string>(),
                Pages = new List<HomePage> { PageOf("a", "b", "c") },
                LibraryPages = new List<HomePage>(),
            },
        };

        var first = BuildLayout(apps, configuration);
        first.MoveTile(first.Page(0)[2], first.HomePageCount, 0);

        var layout = BuildLayout(apps, configuration);

        var page = PageIndexOf(layout,"c");
        Assert.True(page >= layout.HomePageCount,
            $"App 'c' was dragged into the Library and must stay there, but it is on page {page}");
    }

    [Fact]
    public void ExistingSaveWithTwoFullHomePages_ArrowStillReachesLibrary()
    {
        var apps = new List<IPhoneApp>
        {
            new FakeApp("a"), new FakeApp("b"), new FakeApp("c"), new FakeApp("d"),
            new FakeApp("e"), new FakeApp("f"), new FakeApp("g"), new FakeApp("h"),
        };
        var configuration = new FakeHomeConfiguration
        {
            Home = new HomeLayout
            {
                Dock = new List<string>(),
                Pages = new List<HomePage> { PageOf("a", "b", "c", "d"), PageOf("e", "f", "g", "h") },
                LibraryPages = new List<HomePage>(),
            },
        };

        var layout = BuildLayout(apps, configuration);

        Assert.Equal(2, layout.HomePageCount);
        Assert.True(layout.TotalPageCount > layout.HomePageCount,
            $"Expected a reachable Library page after the last of {layout.HomePageCount} home pages, " +
            $"but TotalPageCount was {layout.TotalPageCount}");

        var arrowTarget = layout.HomePageCount;
        Assert.True(arrowTarget <= layout.TotalPageCount - 1,
            "The right-arrow on the last home page would not render: TotalPageCount did not grow past HomePageCount");
    }

    private static HomeLayoutService BuildLayout(List<IPhoneApp> apps, FakeHomeConfiguration configuration) =>
        new(apps, new WidgetRegistry(Array.Empty<IHomeWidget>(), apps), configuration);

    private static void Commit(HomeLayoutService layout) => layout.MoveTile(layout.Page(0)[0], 0, 0);

    private static HomePage PageOf(params string[] appIds)
    {
        var page = new HomePage();
        for (var index = 0; index < appIds.Length; index++)
        {
            page.Items.Add(new HomeItem { Kind = "app", AppId = appIds[index] });
        }

        return page;
    }

    private static int PageIndexOf(HomeLayoutService layout, string appId)
    {
        for (var page = 0; page < layout.TotalPageCount; page++)
        {
            var tiles = layout.Page(page);
            for (var index = 0; index < tiles.Count; index++)
            {
                if (tiles[index].App?.Id == appId)
                {
                    return page;
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
