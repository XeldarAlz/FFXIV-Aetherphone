using System.Numerics;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class AccentRingTests
{
    private const float MinInkContrast = 3.0f;
    private const float MinRingSeparation = 22f;
    private const float MinNeighbourSeparation = 45f;
    private const float NeutralChroma = 0.04f;
    private const float DegreeTolerance = 0.05f;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    private static readonly (string Name, Vector4 Color)[] Ring =
    {
        ("Rose", AccentRing.Rose), ("Red", AccentRing.Red), ("Orange", AccentRing.Orange),
        ("Gold", AccentRing.Gold), ("Lime", AccentRing.Lime), ("Green", AccentRing.Green),
        ("Emerald", AccentRing.Emerald), ("Teal", AccentRing.Teal), ("Cyan", AccentRing.Cyan),
        ("Azure", AccentRing.Azure), ("Indigo", AccentRing.Indigo), ("Violet", AccentRing.Violet),
        ("Orchid", AccentRing.Orchid),
    };

    private static bool IsNeutral(Vector4 color) => OklabHue.Chroma(color) < NeutralChroma;

    private static float HueDistance(Vector4 first, Vector4 second) =>
        IsNeutral(first) || IsNeutral(second) ? 360f : OklabHue.Distance(first, second);

    public static TheoryData<string> AppIds()
    {
        var data = new TheoryData<string>();
        for (var index = 0; index < AppAccents.Ids.Count; index++)
        {
            data.Add(AppAccents.Ids[index]);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AppIds))]
    public void EveryAccentCarriesAWhiteGlyphAtThreeToOne(string id)
    {
        var contrast = Palette.ContrastRatio(AppAccents.For(id), White);
        Assert.True(contrast >= MinInkContrast,
            $"{id} renders white at {contrast:F2}:1, below the {MinInkContrast:F1}:1 floor");
    }

    [Theory]
    [MemberData(nameof(AppIds))]
    public void NoAccentEverFlipsToDarkInk(string id)
    {
        Assert.Equal(White, AppAccents.InkFor(id));
    }

    [Theory]
    [MemberData(nameof(AppIds))]
    public void EveryTileSurfaceCarriesWhiteAtThreeToOne(string id)
    {
        var contrast = Palette.ContrastRatio(IconTile.Surface(AppAccents.For(id)), White);
        Assert.True(contrast >= MinInkContrast,
            $"{id} renders white at {contrast:F2}:1, below the {MinInkContrast:F1}:1 floor");
    }

    [Fact]
    public void AnyTintNormalisesToCarryWhite()
    {
        for (var red = 0; red <= 4; red++)
        {
            for (var green = 0; green <= 4; green++)
            {
                for (var blue = 0; blue <= 4; blue++)
                {
                    var tint = new Vector4(red / 4f, green / 4f, blue / 4f, 1f);
                    var contrast = Palette.ContrastRatio(IconTile.Surface(tint), White);
                    Assert.True(contrast >= MinInkContrast,
                        $"tint ({tint.X:F2}, {tint.Y:F2}, {tint.Z:F2}) renders white at {contrast:F2}:1");
                }
            }
        }
    }

    [Fact]
    public void RingHuesStayApart()
    {
        for (var first = 0; first < Ring.Length; first++)
        {
            for (var second = first + 1; second < Ring.Length; second++)
            {
                var distance = HueDistance(Ring[first].Color, Ring[second].Color);
                Assert.True(distance >= MinRingSeparation - DegreeTolerance,
                    $"{Ring[first].Name} and {Ring[second].Name} sit {distance:F0} degrees apart, " +
                    $"under the {MinRingSeparation:F0} degree floor");
            }
        }
    }

    [Fact]
    public void RingTokensAreDistinct()
    {
        for (var first = 0; first < Ring.Length; first++)
        {
            for (var second = first + 1; second < Ring.Length; second++)
            {
                Assert.False(Ring[first].Color == Ring[second].Color,
                    $"{Ring[first].Name} and {Ring[second].Name} are the same color");
            }
        }
    }

    [Fact]
    public void DefaultSecondPageNeverPutsLikeColorsSideBySide()
    {
        AssertPageSeparation(HomeLayoutService.DefaultSecondPageApps);
    }

    private static void AssertPageSeparation(string[] page)
    {
        for (var index = 0; index < page.Length; index++)
        {
            var right = index + 1;
            if (right < page.Length && right % HomeLayoutService.Columns != 0)
            {
                AssertPair(page[index], page[right]);
            }

            var below = index + HomeLayoutService.Columns;
            if (below < page.Length)
            {
                AssertPair(page[index], page[below]);
            }
        }
    }

    private static void AssertPair(string first, string second)
    {
        if (AppAccents.IsBrandLocked(first) && AppAccents.IsBrandLocked(second))
        {
            return;
        }

        var distance = HueDistance(AppAccents.For(first), AppAccents.For(second));
        Assert.True(distance >= MinNeighbourSeparation - DegreeTolerance,
            $"{first} and {second} are neighbours only {distance:F0} degrees apart, " +
            $"under the {MinNeighbourSeparation:F0} degree floor");
    }
}
