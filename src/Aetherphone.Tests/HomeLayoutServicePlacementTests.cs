using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HomeLayoutServicePlacementTests
{
    [Fact]
    public void DroppedTile_KeepsItsCellAcrossReloads()
    {
        var apps = MakeApps("a", "b", "c");
        var configuration = ConfigurationWith(PageOf("a", "b", "c"));
        var first = BuildLayout(apps, configuration);

        first.MoveTile(TileFor(first, 0, "b"), 0, new GridCell(3, 4));

        var reloaded = BuildLayout(apps, configuration);
        Assert.Equal(new GridCell(3, 4), TileFor(reloaded, 0, "b").Cell);
    }

    [Fact]
    public void MovingATileAway_LeavesTheGapItCameFrom()
    {
        var apps = MakeApps("a", "b", "c");
        var configuration = ConfigurationWith(PageOf("a", "b", "c"));
        var layout = BuildLayout(apps, configuration);

        layout.MoveTile(TileFor(layout, 0, "a"), 0, new GridCell(0, 3));

        Assert.Equal(new GridCell(1, 0), TileFor(layout, 0, "b").Cell);
        Assert.Equal(new GridCell(2, 0), TileFor(layout, 0, "c").Cell);
    }

    [Fact]
    public void LegacySaveWithoutCells_PacksFromTheTopLeft()
    {
        var apps = MakeApps("a", "b", "c");
        var layout = BuildLayout(apps, ConfigurationWith(PageOf("a", "b", "c")));

        Assert.Equal(new GridCell(0, 0), TileFor(layout, 0, "a").Cell);
        Assert.Equal(new GridCell(1, 0), TileFor(layout, 0, "b").Cell);
        Assert.Equal(new GridCell(2, 0), TileFor(layout, 0, "c").Cell);
    }

    [Fact]
    public void DropOntoAnOccupiedCell_ResolvesToTheNearestFreeCell()
    {
        var apps = MakeApps("a", "b");
        var layout = BuildLayout(apps, ConfigurationWith(PageOf("a", "b")));
        var dragged = TileFor(layout, 0, "b");
        layout.MoveTile(dragged, 0, new GridCell(3, 3));

        var resolved = layout.TryResolveDrop(0, dragged, new GridCell(0, 0), out var cell);

        Assert.True(resolved);
        Assert.NotEqual(new GridCell(0, 0), cell);
        Assert.Equal(1, cell.Column + cell.Row);
    }

    [Fact]
    public void ATileHeldBeyondTheLastRow_RelocatesWhenTheGridShrinks()
    {
        var apps = MakeApps("a");
        var configuration = new FakeHomeConfiguration { HomeGridRows = HomeLayoutService.MaxRows };
        configuration.Home = new HomeLayout
        {
            Dock = new List<string>(),
            Pages = new List<HomePage> { PageOf(("a", 2, HomeLayoutService.MaxRows - 1)) },
            LibraryPages = new List<HomePage>(),
        };

        var layout = BuildLayout(apps, configuration);
        Assert.Equal(new GridCell(2, HomeLayoutService.MaxRows - 1), TileFor(layout, 0, "a").Cell);

        configuration.HomeGridRows = HomeLayoutService.MinRows;
        layout.EnsureCurrent();

        Assert.True(TileFor(layout, 0, "a").Cell.Row < HomeLayoutService.MinRows,
            "A tile parked on a row that no longer exists must come back inside the grid");
    }

    private static HomeLayoutService BuildLayout(List<IPhoneApp> apps, FakeHomeConfiguration configuration) =>
        new(apps, new WidgetRegistry(Array.Empty<IHomeWidget>(), apps), configuration);

    private static FakeHomeConfiguration ConfigurationWith(HomePage page) =>
        new()
        {
            Home = new HomeLayout
            {
                Dock = new List<string>(),
                Pages = new List<HomePage> { page },
                LibraryPages = new List<HomePage>(),
            },
        };

    private static HomeTile TileFor(HomeLayoutService layout, int page, string appId)
    {
        var tiles = layout.Page(page);
        for (var index = 0; index < tiles.Count; index++)
        {
            if (tiles[index].App?.Id == appId)
            {
                return tiles[index];
            }
        }

        throw new InvalidOperationException($"App '{appId}' is not on page {page}");
    }

    private static HomePage PageOf(params string[] appIds)
    {
        var page = new HomePage();
        for (var index = 0; index < appIds.Length; index++)
        {
            page.Items.Add(new HomeItem { Kind = "app", AppId = appIds[index] });
        }

        return page;
    }

    private static HomePage PageOf(params (string AppId, int Column, int Row)[] items)
    {
        var page = new HomePage();
        for (var index = 0; index < items.Length; index++)
        {
            page.Items.Add(new HomeItem
            {
                Kind = "app",
                AppId = items[index].AppId,
                Column = items[index].Column,
                Row = items[index].Row,
            });
        }

        return page;
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
