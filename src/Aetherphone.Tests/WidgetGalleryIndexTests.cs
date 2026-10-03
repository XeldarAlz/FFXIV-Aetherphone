using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Shell.Home;
using Xunit;

namespace Aetherphone.Tests;

public sealed class WidgetGalleryIndexTests
{
    [Fact]
    public void AppsAreSortedAlphabeticallyAndCountTheirWidgets()
    {
        var harness = new Harness();
        harness.Index.Refresh(string.Empty, true);

        var apps = harness.Index.Apps;
        Assert.Equal(3, apps.Count);
        Assert.Equal("calendar", apps[0].App.Id);
        Assert.Equal("clock", apps[1].App.Id);
        Assert.Equal("skywatcher", apps[2].App.Id);
        Assert.Equal(2, apps[1].Widgets.Count);
        Assert.Equal("2 widgets", apps[1].CountText);
    }

    [Fact]
    public void QueryMatchesWidgetNamesAppNamesAndDescriptions()
    {
        var harness = new Harness();
        harness.Index.Refresh("forecast", true);
        Assert.Single(harness.Index.Apps);
        Assert.Equal("skywatcher", harness.Index.Apps[0].App.Id);

        harness.Index.Refresh("CLOCK", false);
        Assert.Single(harness.Index.Apps);
        Assert.Equal("clock", harness.Index.Apps[0].App.Id);

        harness.Index.Refresh("nothing like this", false);
        Assert.True(harness.Index.IsEmpty);
    }

    [Fact]
    public void UninstalledAndUnavailableAppsAreHidden()
    {
        var harness = new Harness();
        harness.Installed.Remove("calendar");
        harness.Index.Refresh(string.Empty, false);
        Assert.Equal(2, harness.Index.Apps.Count);

        harness.Apps[0].IsAvailable = false;
        harness.Index.Refresh(string.Empty, false);

        Assert.Single(harness.Index.Apps);
        Assert.Equal("clock", harness.Index.Apps[0].App.Id);
    }

    [Fact]
    public void FeaturedPicksOneWidgetPerAppPreferringRelevanceThenCuratedOrder()
    {
        var harness = new Harness();
        harness.Widgets[1].RelevanceValue = 0.8f;
        harness.Index.Refresh(string.Empty, true);

        var featured = harness.Index.Featured;
        Assert.Equal(3, featured.Count);
        Assert.Equal("clock.alarm", featured[0].Widget.Id);
        Assert.Equal(WidgetSize.Small, featured[0].Size);
        Assert.Equal("skywatcher.forecast", featured[1].Widget.Id);
        Assert.Equal(WidgetSize.Medium, featured[1].Size);
        Assert.Equal("calendar.upcoming", featured[2].Widget.Id);
    }

    [Fact]
    public void SmartStackSuggestsDistinctAppsThatSupportTheSize()
    {
        var harness = new Harness();
        harness.Index.Refresh(string.Empty, true);

        var small = harness.Index.Suggestions(WidgetSize.Small);
        Assert.Equal(3, small.Count);
        var medium = harness.Index.Suggestions(WidgetSize.Medium);
        Assert.Equal(2, medium.Count);
        Assert.NotEqual(medium[0].AppId, medium[1].AppId);
        Assert.True(harness.Index.ShowsSmartStack);
    }

    private sealed class Harness
    {
        public Harness()
        {
            Apps = new[] { new FakeApp("skywatcher"), new FakeApp("clock"), new FakeApp("calendar") };
            Widgets = new[]
            {
                new FakeWidget("skywatcher.forecast", "skywatcher", "Weather", "Forecast ahead",
                    WidgetSizeSet.Small | WidgetSizeSet.Medium),
                new FakeWidget("clock.alarm", "clock", "Alarm", "Next alarm", WidgetSizeSet.Small),
                new FakeWidget("clock.faces", "clock", "Clock", "Time at a glance",
                    WidgetSizeSet.Small | WidgetSizeSet.Medium),
                new FakeWidget("calendar.upcoming", "calendar", "Up Next", "Your next events",
                    WidgetSizeSet.Small | WidgetSizeSet.Large),
            };
            Installed = new HashSet<string> { "skywatcher", "clock", "calendar" };
            var registry = new WidgetRegistry(Widgets, Apps);
            Index = new WidgetGalleryIndex(registry, Installed.Contains);
        }

        public FakeApp[] Apps { get; }
        public FakeWidget[] Widgets { get; }
        public HashSet<string> Installed { get; }
        public WidgetGalleryIndex Index { get; }
    }

    private sealed class FakeWidget : IHomeWidget
    {
        public FakeWidget(string id, string appId, string displayName, string description, WidgetSizeSet sizes)
        {
            Id = id;
            AppId = appId;
            DisplayName = displayName;
            Description = description;
            Sizes = sizes;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string AppId { get; }
        public WidgetSizeSet Sizes { get; }
        public float RelevanceValue { get; set; }

        public void Draw(in WidgetContext context)
        {
        }

        public float Relevance(string config) => RelevanceValue;

        public void Dispose()
        {
        }
    }
}
