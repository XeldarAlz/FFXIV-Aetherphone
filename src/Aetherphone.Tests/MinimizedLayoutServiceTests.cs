using Aetherphone.Core.Shell;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MinimizedLayoutServiceTests
{
    [Fact]
    public void FreshInstall_ShipsTheDefaultOrderWithEorzeaTimeAndWeatherPages()
    {
        var service = new MinimizedLayoutService(new FakeMinimizedConfiguration());
        var defaults = MinimizedParts.Default;

        Assert.Equal(defaults.Length, service.Slots.Length);
        for (var index = 0; index < defaults.Length; index++)
        {
            Assert.Equal(defaults[index], service.Slots[index].Part);
            Assert.Equal(MinimizedParts.EnabledByDefault(defaults[index]), service.Slots[index].Enabled);
        }

        Assert.True(service.IsEnabled(MinimizedPart.Clock));
        Assert.True(service.IsEnabled(MinimizedPart.EorzeaClock));
        Assert.True(service.IsEnabled(MinimizedPart.Weather));
        Assert.False(service.IsEnabled(MinimizedPart.Resets));
    }

    [Fact]
    public void MovePage_SkipsPartsThatAreNotPages()
    {
        var configuration = new FakeMinimizedConfiguration();
        var service = new MinimizedLayoutService(configuration);
        service.Move(MinimizedParts.Count - 1, -1);
        var rings = MinimizedParts.Count - 1;
        var badge = MinimizedParts.Count - 2;
        Assert.Equal(MinimizedPart.Rings, service.Slots[rings].Part);
        Assert.Equal(MinimizedPart.Badge, service.Slots[badge].Part);

        service.MovePage(rings, -1);

        Assert.Equal(MinimizedPart.Ventures, service.Slots[rings].Part);
        Assert.Equal(MinimizedPart.Badge, service.Slots[badge].Part);
        Assert.Equal(MinimizedPart.Rings, service.Slots[badge - 1].Part);
    }

    [Fact]
    public void MovePage_IgnoresTheFirstAndLastPage()
    {
        var configuration = new FakeMinimizedConfiguration();
        var service = new MinimizedLayoutService(configuration);
        var first = IndexOf(service, MinimizedPart.EorzeaClock);
        var last = IndexOf(service, MinimizedPart.Rings);

        service.MovePage(first, -1);
        service.MovePage(last, 1);

        Assert.Equal(MinimizedPart.EorzeaClock, service.Slots[first].Part);
        Assert.Equal(MinimizedPart.Rings, service.Slots[last].Part);
        Assert.Equal(0, configuration.Saves);
    }

    private static int IndexOf(MinimizedLayoutService service, MinimizedPart part)
    {
        var slots = service.Slots;
        for (var index = 0; index < slots.Length; index++)
        {
            if (slots[index].Part == part)
            {
                return index;
            }
        }

        return -1;
    }

    [Fact]
    public void FreshInstall_KeepsTheUnreadBadgeLast()
    {
        var service = new MinimizedLayoutService(new FakeMinimizedConfiguration());

        Assert.Equal(MinimizedPart.Badge, service.Slots[service.Slots.Length - 1].Part);
    }

    [Fact]
    public void Toggle_PersistsEveryPartInOrder()
    {
        var configuration = new FakeMinimizedConfiguration();
        var service = new MinimizedLayoutService(configuration);

        service.SetEnabled(0, false);

        var saved = Assert.IsType<MinimizedLayout>(configuration.MinimizedLayout);
        Assert.Equal(MinimizedParts.Count, saved.Items.Count);
        Assert.Equal(MinimizedParts.Id(MinimizedPart.Clock), saved.Items[0].PartId);
        Assert.False(saved.Items[0].Enabled);
        Assert.Equal(1, configuration.Saves);
    }

    [Fact]
    public void Move_SwapsWithTheNeighbourAndSurvivesAReload()
    {
        var configuration = new FakeMinimizedConfiguration();
        var service = new MinimizedLayoutService(configuration);

        service.Move(0, 1);

        Assert.Equal(MinimizedPart.Date, service.Slots[0].Part);
        Assert.Equal(MinimizedPart.Clock, service.Slots[1].Part);

        var reloaded = new MinimizedLayoutService(configuration);
        Assert.Equal(MinimizedPart.Date, reloaded.Slots[0].Part);
        Assert.Equal(MinimizedPart.Clock, reloaded.Slots[1].Part);
    }

    [Fact]
    public void Move_IgnoresStepsPastTheEnds()
    {
        var configuration = new FakeMinimizedConfiguration();
        var service = new MinimizedLayoutService(configuration);

        service.Move(0, -1);
        service.Move(MinimizedParts.Count - 1, 1);

        Assert.Equal(MinimizedPart.Clock, service.Slots[0].Part);
        Assert.Equal(MinimizedPart.Badge, service.Slots[MinimizedParts.Count - 1].Part);
        Assert.Equal(0, configuration.Saves);
    }

    [Fact]
    public void Load_DropsUnknownPartsAndAppendsMissingOnesWithTheirDefaults()
    {
        var configuration = new FakeMinimizedConfiguration
        {
            MinimizedLayout = new MinimizedLayout
            {
                Items =
                {
                    new MinimizedLayoutItem { PartId = "weather", Enabled = true },
                    new MinimizedLayoutItem { PartId = "retiredWidget", Enabled = true },
                    new MinimizedLayoutItem { PartId = "weather", Enabled = false },
                    new MinimizedLayoutItem { PartId = "clock", Enabled = false },
                },
            },
        };

        var service = new MinimizedLayoutService(configuration);

        Assert.Equal(MinimizedParts.Count, service.Slots.Length);
        Assert.Equal(MinimizedPart.Weather, service.Slots[0].Part);
        Assert.True(service.Slots[0].Enabled);
        Assert.Equal(MinimizedPart.Clock, service.Slots[1].Part);
        Assert.False(service.Slots[1].Enabled);
        Assert.True(service.IsEnabled(MinimizedPart.Date));
        Assert.False(service.IsEnabled(MinimizedPart.Rings));
    }

    [Fact]
    public void Reset_RestoresTheDefaultsAndSaves()
    {
        var configuration = new FakeMinimizedConfiguration();
        var service = new MinimizedLayoutService(configuration);
        service.Move(0, 1);
        service.SetEnabled(0, false);

        service.Reset();

        Assert.Equal(MinimizedPart.Clock, service.Slots[0].Part);
        Assert.True(service.Slots[0].Enabled);
        Assert.Equal(3, configuration.Saves);
    }

    private sealed class FakeMinimizedConfiguration : IMinimizedConfiguration
    {
        public MinimizedLayout? MinimizedLayout { get; set; }

        public int Saves { get; private set; }

        public void Save() => Saves++;
    }
}
