using System.Numerics;
using Aetherphone.Core.Fishing;
using Xunit;

namespace Aetherphone.Tests;

public sealed class FishPatchChipsTests
{
    private const string SampleLine = "8752|58|1080|360|7,8|2|2599,4978|5031x3|600|WH|BLA|2.4";
    private const int Dawntrail = 5;
    private const int Endwalker = 4;

    [Fact]
    public void CollectsDistinctPatchesOfOneExpansionInAscendingOrder()
    {
        var entries = Entries(7.2f, 7.0f, 7.55f, 7.2f, 6.3f, 7.1f, 7.0f);
        Span<float> patches = stackalloc float[FishPatchChips.PatchSlots];

        var count = FishPatchChips.Collect(entries, Dawntrail, patches);

        Assert.Equal(4, count);
        Assert.Equal("7.0", FishingCatalog.PatchText(patches[0]));
        Assert.Equal("7.1", FishingCatalog.PatchText(patches[1]));
        Assert.Equal("7.2", FishingCatalog.PatchText(patches[2]));
        Assert.Equal("7.55", FishingCatalog.PatchText(patches[3]));
    }

    [Fact]
    public void ExcludesPatchesOfOtherExpansions()
    {
        var entries = Entries(7.2f, 6.3f, 6.1f, 5.5f);
        Span<float> patches = stackalloc float[FishPatchChips.PatchSlots];

        var count = FishPatchChips.Collect(entries, Endwalker, patches);

        Assert.Equal(2, count);
        Assert.Equal(6.1f, patches[0]);
        Assert.Equal(6.3f, patches[1]);
    }

    [Fact]
    public void SkipsFishWithoutAPatchAndExpansionsWithoutFish()
    {
        var entries = Entries(0f, 2.4f);
        Span<float> patches = stackalloc float[FishPatchChips.PatchSlots];

        Assert.Equal(1, FishPatchChips.Collect(entries, 0, patches));
        Assert.Equal(2.4f, patches[0]);
        Assert.Equal(0, FishPatchChips.Collect(entries, Endwalker, patches));
    }

    [Fact]
    public void KeepsTheLowestPatchesWhenAnExpansionOverflowsTheSlots()
    {
        var values = new float[FishPatchChips.PatchSlots + 2];
        for (var index = 0; index < values.Length; index++)
        {
            values[index] = 7f + (values.Length - 1 - index) * 0.01f;
        }

        var entries = Entries(values);
        Span<float> patches = stackalloc float[FishPatchChips.PatchSlots];

        var count = FishPatchChips.Collect(entries, Dawntrail, patches);

        Assert.Equal(FishPatchChips.PatchSlots, count);
        Assert.Equal(700, FishPatchChips.Key(patches[0]));
        Assert.Equal(700 + FishPatchChips.PatchSlots - 1, FishPatchChips.Key(patches[count - 1]));
    }

    [Theory]
    [InlineData(2f, 200)]
    [InlineData(5.55f, 555)]
    [InlineData(7.2f, 720)]
    [InlineData(7.2000003f, 720)]
    public void KeysRoundAPatchToItsHundredths(float patch, int key)
    {
        Assert.Equal(key, FishPatchChips.Key(patch));
        Assert.Equal(FishingCatalog.PatchText(patch), FishingCatalog.PatchText(key / 100f));
    }

    private static FishEntry[] Entries(params float[] patches)
    {
        Assert.True(TimedFishParser.TryParseLine(SampleLine, out var sample));
        var entries = new FishEntry[patches.Length];
        for (var index = 0; index < patches.Length; index++)
        {
            entries[index] = new FishEntry
            {
                Fish = sample with { Patch = patches[index] },
                Name = string.Empty,
                SearchKey = string.Empty,
                IconId = 0,
                SpotName = string.Empty,
                ZoneName = string.Empty,
                TerritoryId = 0,
                MapId = 0,
                MapPosition = Vector2.Zero,
                Odds = Array.Empty<WeatherOdds>(),
                LogRow = 0,
            };
        }

        return entries;
    }
}
