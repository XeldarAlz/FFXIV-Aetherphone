using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Theme;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HomeStackTests
{
    private const WidgetSizeSet AllSizes = WidgetSizeSet.Small | WidgetSizeSet.Medium | WidgetSizeSet.Large;

    [Fact]
    public void MakeStack_ShowsTheDroppedWidgetAndSurvivesAReload()
    {
        var apps = MakeApps();
        var weather = new FakeWidget("c.weather", "c", AllSizes);
        var clock = new FakeWidget("c.clock", "c", AllSizes);
        var configuration = Saved(Widget(weather, "small", "w1", 0, 0), Widget(clock, "small", "w2", 2, 0));
        var layout = Build(apps, configuration, weather, clock);
        var target = FindWidget(layout, "w1")!;
        var dragged = FindWidget(layout, "w2")!;

        var stack = layout.MakeStack(target, dragged);

        Assert.NotNull(stack);
        Assert.True(stack!.IsStack);
        Assert.Equal(2, stack.Stack.Count);
        Assert.Equal(1, stack.StackIndex);
        Assert.Equal("w2", stack.InstanceKey);
        Assert.Equal(new GridCell(0, 0), stack.Cell);
        layout.SetSmartRotate(stack, false);
        layout.SetWidgetConfig(stack.Stack[0], "city=Limsa");

        var reloaded = FindStack(Build(apps, configuration, weather, clock))!;

        Assert.Equal(2, reloaded.Stack.Count);
        Assert.Equal(1, reloaded.StackIndex);
        Assert.False(reloaded.SmartRotate);
        Assert.Equal(WidgetSize.Small, reloaded.Size);
        Assert.Equal("w1", reloaded.Stack[0].InstanceKey);
        Assert.Equal("city=Limsa", reloaded.Stack[0].Config);
        Assert.Equal("w2", reloaded.Stack[1].InstanceKey);
        Assert.Equal(clock.Id, reloaded.Widget!.Id);
    }

    [Fact]
    public void LegacySaveWithoutStacks_StillLoadsPlainWidgets()
    {
        var apps = MakeApps();
        var weather = new FakeWidget("c.weather", "c", AllSizes);
        var configuration = Saved(Widget(weather, "medium", "w1", 0, 0));

        var layout = Build(apps, configuration, weather);

        var tile = FindWidget(layout, "w1")!;
        Assert.False(tile.IsStack);
        Assert.True(tile.SmartRotate);
        Assert.Null(FindStack(layout));
    }

    [Fact]
    public void CanStack_RequiresMatchingSizesAndRoom()
    {
        var apps = MakeApps();
        var weather = new FakeWidget("c.weather", "c", AllSizes);
        var configuration = Saved(Widget(weather, "small", "w1", 0, 0), Widget(weather, "medium", "w2", 0, 2));
        var layout = Build(apps, configuration, weather);

        Assert.False(layout.CanStack(FindWidget(layout, "w1")!, FindWidget(layout, "w2")!));
        Assert.False(layout.CanStack(FindWidget(layout, "w1")!, FindWidget(layout, "w1")!));
    }

    [Fact]
    public void Stacks_HoldAtMostTenWidgets()
    {
        var apps = MakeApps();
        var weather = new FakeWidget("c.weather", "c", AllSizes);
        var members = new IHomeWidget[HomeTile.StackCapacity + 2];
        for (var index = 0; index < members.Length; index++)
        {
            members[index] = weather;
        }

        var configuration = Saved();
        var layout = Build(apps, configuration, weather);

        Assert.True(layout.AddStack(members, WidgetSize.Small, 0));
        var stack = FindStack(layout)!;
        Assert.Equal(HomeTile.StackCapacity, stack.Stack.Count);
        Assert.True(layout.AddWidget(weather, WidgetSize.Small, 0));
        Assert.False(layout.CanStack(stack, LastWidget(layout)));
    }

    [Fact]
    public void AddStack_SkipsWidgetsWithoutTheSizeAndFallsBackToAPlainWidget()
    {
        var apps = MakeApps();
        var weather = new FakeWidget("c.weather", "c", AllSizes);
        var mediumOnly = new FakeWidget("c.medium", "c", WidgetSizeSet.Medium);
        var layout = Build(apps, Saved(), weather, mediumOnly);

        Assert.True(layout.AddStack(new IHomeWidget[] { weather, mediumOnly }, WidgetSize.Small, 0));

        Assert.Null(FindStack(layout));
        Assert.Equal(weather.Id, LastWidget(layout).Widget!.Id);
        Assert.False(layout.AddStack(new IHomeWidget[] { mediumOnly }, WidgetSize.Large, 0));
    }

    [Fact]
    public void RemovingMembersDownToOne_TurnsTheStackBackIntoAWidget()
    {
        var apps = MakeApps();
        var weather = new FakeWidget("c.weather", "c", AllSizes);
        var clock = new FakeWidget("c.clock", "c", AllSizes);
        var configuration = Saved(Widget(weather, "small", "w1", 2, 2), Widget(clock, "small", "w2", 0, 0));
        var layout = Build(apps, configuration, weather, clock);
        var stack = layout.MakeStack(FindWidget(layout, "w1")!, FindWidget(layout, "w2")!)!;

        layout.RemoveStackMember(stack, stack.Stack[1]);

        Assert.Null(FindStack(layout));
        var remaining = FindWidget(layout, "w1")!;
        Assert.Equal(new GridCell(2, 2), remaining.Cell);
        Assert.Null(FindWidget(Build(apps, configuration, weather, clock), "w2"));
        Assert.NotNull(FindWidget(Build(apps, configuration, weather, clock), "w1"));
    }

    [Fact]
    public void MoveStackMember_KeepsTheVisibleWidget()
    {
        var apps = MakeApps();
        var weather = new FakeWidget("c.weather", "c", AllSizes);
        var layout = Build(apps, Saved(), weather);
        layout.AddStack(new IHomeWidget[] { weather, weather, weather }, WidgetSize.Medium, 0);
        var stack = FindStack(layout)!;
        layout.SetStackIndex(stack, 2);
        var visible = stack.Visible;

        layout.MoveStackMember(stack, 2, 0);

        Assert.Same(visible, stack.Visible);
        Assert.Equal(0, stack.StackIndex);
    }

    [Fact]
    public void UninstallingAnApp_DropsItsStackMembers()
    {
        var apps = MakeApps();
        var weather = new FakeWidget("c.weather", "c", AllSizes);
        var notes = new FakeWidget("b.notes", "b", AllSizes);
        var configuration = Saved(Widget(weather, "small", "w1", 0, 0), Widget(notes, "small", "w2", 2, 0));
        configuration.Home!.Installed.Add("b");
        var layout = Build(apps, configuration, weather, notes);
        layout.MakeStack(FindWidget(layout, "w1")!, FindWidget(layout, "w2")!);

        Assert.True(layout.Uninstall("b"));

        Assert.Null(FindStack(layout));
        Assert.NotNull(FindWidget(layout, "w1"));
    }

    [Fact]
    public void LookMirror_ClonesStacks()
    {
        var configuration = new FakeLookConfiguration { Home = StackLayout() };
        var look = new HomeLook { Id = Guid.NewGuid() };

        HomeLookMirror.Capture(configuration, look);
        configuration.Home!.Pages[0].Items[0].Members.Clear();

        var stored = look.Pages[0].Items[0];
        Assert.Equal("stack", stored.Kind);
        Assert.Equal(1, stored.StackIndex);
        Assert.False(stored.SmartRotate);
        Assert.Equal(2, stored.Members.Count);
        Assert.Equal("w2", stored.Members[1].WidgetKey);
    }

    [Fact]
    public void TryFitSize_RefusesToGrowIntoANeighbour()
    {
        var apps = MakeApps();
        var weather = new FakeWidget("c.weather", "c", AllSizes);
        var configuration = Saved(Widget(weather, "small", "w1", 0, 0), Widget(weather, "small", "w2", 2, 0));
        var layout = Build(apps, configuration, weather);
        var tile = FindWidget(layout, "w1")!;

        Assert.False(layout.TryFitSize(tile, WidgetSize.Medium, out _));
        Assert.False(layout.TryResizeInPlace(tile, WidgetSize.Large));
        Assert.Equal(WidgetSize.Small, tile.Size);
    }

    [Fact]
    public void TryResizeInPlace_ShiftsLeftAtTheEdgeAndPersists()
    {
        var apps = MakeApps();
        var weather = new FakeWidget("c.weather", "c", AllSizes);
        var configuration = Saved(Widget(weather, "small", "w1", 2, 0), Widget(weather, "small", "w2", 0, 4));
        var layout = Build(apps, configuration, weather);
        var tile = FindWidget(layout, "w1")!;

        Assert.True(layout.TryFitSize(tile, WidgetSize.Medium, out var cell));
        Assert.Equal(new GridCell(0, 0), cell);
        Assert.True(layout.TryFitSize(tile, WidgetSize.Large, out var largeCell));
        Assert.Equal(new GridCell(0, 0), largeCell);
        Assert.True(layout.TryResizeInPlace(tile, WidgetSize.Medium));

        var reloaded = FindWidget(Build(apps, configuration, weather), "w1")!;
        Assert.Equal(WidgetSize.Medium, reloaded.Size);
        Assert.Equal(new GridCell(0, 0), reloaded.Cell);
    }

    [Fact]
    public void TryFitSize_OnAStackNeedsEveryMemberToSupportTheSize()
    {
        var apps = MakeApps();
        var weather = new FakeWidget("c.weather", "c", AllSizes);
        var smallOrMedium = new FakeWidget("c.compact", "c", WidgetSizeSet.Small | WidgetSizeSet.Medium);
        var layout = Build(apps, Saved(), weather, smallOrMedium);
        layout.AddStack(new IHomeWidget[] { weather, smallOrMedium }, WidgetSize.Small, 0);
        var stack = FindStack(layout)!;

        Assert.Equal(WidgetSizeSet.Small | WidgetSizeSet.Medium, HomeLayoutService.SizesOf(stack));
        Assert.False(layout.TryFitSize(stack, WidgetSize.Large, out _));
        Assert.True(layout.TryResizeInPlace(stack, WidgetSize.Medium));
        Assert.Equal(WidgetSize.Medium, stack.Stack[0].Size);
        Assert.Equal(WidgetSize.Medium, stack.Stack[1].Size);
    }

    private static HomeLayout StackLayout() =>
        new()
        {
            Pages = new List<HomePage>
            {
                new()
                {
                    Items = new List<HomeItem>
                    {
                        new()
                        {
                            Kind = "stack",
                            WidgetSize = "small",
                            StackIndex = 1,
                            SmartRotate = false,
                            Members = new List<HomeItem>
                            {
                                new() { Kind = "widget", WidgetId = "c.weather", WidgetKey = "w1" },
                                new() { Kind = "widget", WidgetId = "c.clock", WidgetKey = "w2" },
                            },
                        },
                    },
                },
            },
            Dock = new List<string>(),
            Installed = new List<string> { "c" },
        };

    private static HomeItem Widget(IHomeWidget widget, string size, string key, int column, int row) =>
        new()
        {
            Kind = "widget",
            WidgetId = widget.Id,
            WidgetSize = size,
            WidgetKey = key,
            Column = column,
            Row = row,
        };

    private static FakeHomeConfiguration Saved(params HomeItem[] items)
    {
        var page = new HomePage();
        page.Items.Add(new HomeItem { Kind = "app", AppId = "c", Column = 3, Row = 5 });
        for (var index = 0; index < items.Length; index++)
        {
            page.Items.Add(items[index]);
        }

        return new FakeHomeConfiguration
        {
            Home = new HomeLayout
            {
                Dock = new List<string>(),
                Pages = new List<HomePage> { page },
                Installed = new List<string> { "c" },
            },
        };
    }

    private static HomeLayoutService Build(List<IPhoneApp> apps, FakeHomeConfiguration configuration,
        params IHomeWidget[] widgets) =>
        new(apps, new WidgetRegistry(widgets, apps), new FakeShortcutSource(), configuration);

    private static List<IPhoneApp> MakeApps() =>
        new()
        {
            new FakeApp("a"),
            new FakeApp("b"),
            new FakeApp("c"),
        };

    private static HomeTile? FindWidget(HomeLayoutService layout, string instanceKey)
    {
        for (var page = 0; page < layout.PageCount; page++)
        {
            var tiles = layout.Page(page);
            for (var index = 0; index < tiles.Count; index++)
            {
                var tile = tiles[index];
                if (tile.IsWidget && !tile.IsStack && tile.InstanceKey == instanceKey)
                {
                    return tile;
                }
            }
        }

        return null;
    }

    private static HomeTile? FindStack(HomeLayoutService layout)
    {
        for (var page = 0; page < layout.PageCount; page++)
        {
            var tiles = layout.Page(page);
            for (var index = 0; index < tiles.Count; index++)
            {
                if (tiles[index].IsStack)
                {
                    return tiles[index];
                }
            }
        }

        return null;
    }

    private static HomeTile LastWidget(HomeLayoutService layout)
    {
        HomeTile? last = null;
        for (var page = 0; page < layout.PageCount; page++)
        {
            var tiles = layout.Page(page);
            for (var index = 0; index < tiles.Count; index++)
            {
                if (tiles[index].IsWidget && !tiles[index].IsStack)
                {
                    last = tiles[index];
                }
            }
        }

        return last!;
    }

    private sealed class FakeWidget : IHomeWidget
    {
        public FakeWidget(string id, string appId, WidgetSizeSet sizes)
        {
            Id = id;
            AppId = appId;
            Sizes = sizes;
        }

        public string Id { get; }
        public string DisplayName => Id;
        public string Description => Id;
        public string AppId { get; }
        public WidgetSizeSet Sizes { get; }
        public void Draw(in WidgetContext context) { }
        public void Dispose() { }
    }

    private sealed class FakeLookConfiguration : ILookConfiguration
    {
        public HomeLayout? Home { get; set; }
        public int HomeGridRows { get; set; }
        public bool ShowAppNames { get; set; }
        public IconAppearance IconAppearance { get; set; }
        public ThemeMode ThemeMode { get; set; }
        public string AccentName { get; set; } = string.Empty;
        public string AccentCustomHex { get; set; } = string.Empty;
        public string PhoneCaseName { get; set; } = string.Empty;
        public string LightWallpaperId { get; set; } = string.Empty;
        public string DarkWallpaperId { get; set; } = string.Empty;
        public List<HomeLook> Looks { get; } = new();
        public Dictionary<ulong, Guid> LookByCharacter { get; } = new();
        public Guid ActiveLookId { get; set; }
        public void Save() { }
    }
}
