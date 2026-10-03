using System.Reflection;
using Aetherphone.Core.Fishing;
using Aetherphone.Core.Game;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TimedFishDataTests
{
    [Fact]
    public void ParsesEveryFieldOfALine()
    {
        Assert.True(TimedFishParser.TryParseLine("8752|58|1080|360|7,8|2|2599,4978|5031x3|600|WH|BLA|2.4",
            out var fish));

        Assert.Equal(8752u, fish.ItemId);
        Assert.Equal(58u, fish.SpotId);
        Assert.Equal(1080, fish.Rule.StartMinute);
        Assert.Equal(360, fish.Rule.EndMinute);
        Assert.Equal(new byte[] { 7, 8 }, fish.Rule.Weather);
        Assert.Equal(new byte[] { 2 }, fish.Rule.PreviousWeather);
        Assert.Equal(new uint[] { 2599, 4978 }, fish.BaitChain);
        Assert.Equal(new[] { new FishPredator(5031, 3) }, fish.Predators);
        Assert.Equal(600, fish.IntuitionSeconds);
        Assert.Equal(FishHookset.Powerful, fish.Hookset);
        Assert.Equal(FishTug.Heavy, fish.Tug);
        Assert.True(fish.IsBigFish);
        Assert.Equal(TimedFishFlags.BigFish | TimedFishFlags.Folklore | TimedFishFlags.AmbitiousLure, fish.Flags);
        Assert.Equal(2.4f, fish.Patch);
    }

    [Theory]
    [InlineData("")]
    [InlineData("4898|52|1080")]
    [InlineData("0|52|1080|360|||2596|||PL||2")]
    [InlineData("4898|52|1500|360|||2596|||PL||2")]
    [InlineData("abc|52|1080|360|||2596|||PL||2")]
    public void RejectsMalformedLines(string line)
    {
        Assert.False(TimedFishParser.TryParseLine(line, out _));
    }

    [Fact]
    public void BundledDatasetParsesCompletely()
    {
        var text = File.ReadAllText(Path.Combine(PluginSourceDirectory(), "Fishing", "TimedFish.txt"));
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var parsed = TimedFishParser.Parse(text);

        Assert.Equal(lines.Length, parsed.Count);
        Assert.True(parsed.Count > 400);
        var seen = new HashSet<uint>();
        foreach (var fish in parsed)
        {
            Assert.True(seen.Add(fish.ItemId), $"item {fish.ItemId} appears twice");
            Assert.True(fish.SpotId > 0);
            Assert.True(fish.IsBigFish || !fish.Rule.AlwaysOpen, $"item {fish.ItemId} has no condition");
        }
    }

    [Theory]
    [InlineData('N', 'D', 1u)]
    [InlineData('B', 'S', 8u)]
    [InlineData('T', 'N', 12u)]
    [InlineData('V', 'N', 21u)]
    [InlineData('X', 'D', 0u)]
    public void RouteRowsFollowTheGameSheetOrder(char destination, char time, uint expected)
    {
        Assert.Equal(expected, OceanItinerary.RouteRow(destination, time));
    }

    [Fact]
    public void StopsCycleTheTimeOfDayTowardTheFinalStop()
    {
        Span<OceanStop> stops = stackalloc OceanStop[OceanItinerary.StopCount];

        Assert.True(OceanItinerary.TryStops('B', 'D', stops));

        Assert.Equal(new OceanStop(5, OceanTimeOfDay.Sunset), stops[0]);
        Assert.Equal(new OceanStop(3, OceanTimeOfDay.Night), stops[1]);
        Assert.Equal(new OceanStop(6, OceanTimeOfDay.Day), stops[2]);
    }

    [Theory]
    [InlineData('B', 'D', new uint[] { 32094 })]
    [InlineData('B', 'S', new uint[] { 32074, 29791 })]
    [InlineData('N', 'N', new uint[0])]
    [InlineData('O', 'D', new uint[] { 40560, 40600 })]
    [InlineData('V', 'D', new uint[] { 51228 })]
    public void BlueFishFollowEachStopsTimeOfDay(char destination, char time, uint[] expected)
    {
        var buffer = new OceanBlueFish[OceanItinerary.StopCount];

        var count = OceanItinerary.BlueFishFor(destination, time, buffer);

        Assert.Equal(expected.Length, count);
        for (var index = 0; index < count; index++)
        {
            Assert.Equal(expected[index], buffer[index].ItemId);
        }
    }

    private static string PluginSourceDirectory()
    {
        var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        while (!string.IsNullOrEmpty(directory))
        {
            var candidate = Path.Combine(directory, "src", "Aetherphone");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = Path.GetDirectoryName(directory)!;
        }

        throw new DirectoryNotFoundException("Could not locate src/Aetherphone from the test assembly.");
    }
}
