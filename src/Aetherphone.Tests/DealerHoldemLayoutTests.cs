using System.Numerics;
using Aetherphone.Apps.Casino.DealerHoldem;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class DealerHoldemLayoutTests
{
    private const float PhoneWidth = 360f;
    private const float PhoneHeight = 780f;
    private const float Tolerance = 0.01f;
    private const float PrintedTableHeight = 136f;

    public static IEnumerable<object[]> Scales()
    {
        yield return new object[] { 0.75f };
        yield return new object[] { 1f };
        yield return new object[] { 1.5f };
    }

    private static DealerHoldemLayout LayoutAt(float scale, float widthFactor = 1f, float heightFactor = 1f)
    {
        var full = new Rect(Vector2.Zero, new Vector2(PhoneWidth * scale * widthFactor, PhoneHeight * scale * heightFactor));
        var stage = CasinoStageLayout.Compute(full, false, false, DealerHoldemCabinet.DeckHeight, scale);
        return DealerHoldemLayout.Compute(stage.Safe, scale, PrintedTableHeight * scale);
    }

    private static Rect[] Regions(in DealerHoldemLayout layout)
    {
        var regions = new List<Rect>
        {
            layout.DealerCards,
            layout.DealerNameBand,
            layout.Board,
            layout.StateBand,
            layout.Circles,
            layout.Hero,
            layout.HeroNameBand,
        };
        return regions.ToArray();
    }

    private static bool Overlaps(Rect first, Rect second) =>
        first.Min.X < second.Max.X - Tolerance && second.Min.X < first.Max.X - Tolerance
        && first.Min.Y < second.Max.Y - Tolerance && second.Min.Y < first.Max.Y - Tolerance;

    private static bool Inside(Rect inner, Rect outer) =>
        inner.Min.X >= outer.Min.X - Tolerance && inner.Min.Y >= outer.Min.Y - Tolerance
        && inner.Max.X <= outer.Max.X + Tolerance && inner.Max.Y <= outer.Max.Y + Tolerance;

    [Theory]
    [MemberData(nameof(Scales))]
    public void TheTableZonesNeverOverlapAndStayInsideSafe(float scale)
    {
        var layout = LayoutAt(scale);
        var regions = Regions(layout);
        for (var index = 0; index < regions.Length; index++)
        {
            Assert.True(Inside(regions[index], layout.Safe), $"region {index} leaves safe at {scale}");
            for (var other = index + 1; other < regions.Length; other++)
            {
                Assert.False(Overlaps(regions[index], regions[other]), $"regions {index} and {other} at {scale}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void ThePayTablesFlankTheDealerWithoutTouchingTheCards(float scale)
    {
        var layout = LayoutAt(scale);
        Assert.True(layout.HasTables);
        Assert.False(Overlaps(layout.BlindTable, layout.DealerCards));
        Assert.False(Overlaps(layout.TripsTable, layout.DealerCards));
        Assert.False(Overlaps(layout.BlindTable, layout.TripsTable));
        Assert.True(layout.BlindTable.Max.Y <= layout.DealerNameBand.Min.Y + Tolerance);
        Assert.True(layout.BlindTable.Height >= PrintedTableHeight * scale - Tolerance);
        Assert.True(layout.TripsTable.Height >= PrintedTableHeight * scale - Tolerance);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void TheBettingCirclesAreTouchSizedAndApart(float scale)
    {
        var layout = LayoutAt(scale);
        Assert.True(layout.CircleRadius * 2f >= CasinoStageLayout.TouchTarget * scale - Tolerance);
        for (var index = 0; index < DealerHoldemRules.SpotCount - 1; index++)
        {
            var left = layout.CircleRect((DealerHoldemSpot)index);
            var right = layout.CircleRect((DealerHoldemSpot)(index + 1));
            Assert.True(left.Max.X < right.Min.X);
            Assert.True(Inside(left, layout.Safe));
            Assert.True(Inside(right, layout.Safe));
        }
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void TheBoardCardsSitInARowWithoutTouching(float scale)
    {
        var layout = LayoutAt(scale);
        for (var index = 0; index < DealerHoldemRules.BoardCards - 1; index++)
        {
            var gap = layout.BoardCard(index + 1).X - layout.BoardCard(index).X;
            Assert.True(gap > layout.BoardCardWidth);
        }

        Assert.True(layout.HeroCardWidth > layout.BoardCardWidth);
        Assert.True(layout.HeroCardWidth > layout.DealerCardWidth);
    }

    [Fact]
    public void AShortPhoneStillKeepsEveryZoneApart()
    {
        var layout = LayoutAt(1f, 1f, 0.82f);
        var regions = Regions(layout);
        for (var index = 0; index < regions.Length; index++)
        {
            for (var other = index + 1; other < regions.Length; other++)
            {
                Assert.False(Overlaps(regions[index], regions[other]), $"regions {index} and {other}");
            }
        }
    }
}
