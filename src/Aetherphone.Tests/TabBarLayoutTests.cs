using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TabBarLayoutTests
{
    private const float Tolerance = 1e-3f;
    private static readonly Rect Area = new(new Vector2(100f, 200f), new Vector2(460f, 900f));

    public static TheoryData<float> Scales()
    {
        var data = new TheoryData<float>();
        float[] scales = { 1f, 1.5f, 2f };
        for (var index = 0; index < scales.Length; index++)
        {
            data.Add(scales[index]);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void FullCapsuleFloatsInsetFromTheSidesAndAboveTheBottom(float scale)
    {
        var capsule = TabBarLayout.FullCapsule(Area, scale, false);
        Assert.Equal(TabBarLayout.Height * scale, capsule.Height, Tolerance);
        Assert.Equal(Area.Min.X + TabBarLayout.SideInset * scale, capsule.Min.X, Tolerance);
        Assert.Equal(Area.Max.X - TabBarLayout.SideInset * scale, capsule.Max.X, Tolerance);
        Assert.Equal(Area.Max.Y - TabBarLayout.BottomInset * scale, capsule.Max.Y, Tolerance);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void ActionCircleTakesTheTrailingRoomTheCapsuleGivesUp(float scale)
    {
        var capsule = TabBarLayout.FullCapsule(Area, scale, true);
        var circle = TabBarLayout.ActionCircle(Area, scale);
        Assert.Equal(TabBarLayout.ActionDiameter * scale, circle.Width, Tolerance);
        Assert.Equal(TabBarLayout.ActionDiameter * scale, circle.Height, Tolerance);
        Assert.Equal(Area.Max.X - TabBarLayout.SideInset * scale, circle.Max.X, Tolerance);
        Assert.Equal(circle.Min.X - TabBarLayout.ActionGap * scale, capsule.Max.X, Tolerance);
        Assert.Equal(capsule.Center.Y, circle.Center.Y, Tolerance);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void CellsPartitionTheInnerWidthEvenly(int count)
    {
        var capsule = TabBarLayout.FullCapsule(Area, 1f, false);
        var inner = capsule.Width - TabBarLayout.CapsulePadding * 2f;
        for (var index = 0; index < count; index++)
        {
            var cell = TabBarLayout.Cell(capsule, count, index, 1f);
            Assert.Equal(inner / count, cell.Width, Tolerance);
            Assert.Equal(capsule.Min.Y, cell.Min.Y, Tolerance);
            Assert.Equal(capsule.Max.Y, cell.Max.Y, Tolerance);
            if (index > 0)
            {
                Assert.Equal(TabBarLayout.Cell(capsule, count, index - 1, 1f).Max.X, cell.Min.X, Tolerance);
            }
        }

        Assert.Equal(capsule.Min.X + TabBarLayout.CapsulePadding, TabBarLayout.Cell(capsule, count, 0, 1f).Min.X,
            Tolerance);
        Assert.Equal(capsule.Max.X - TabBarLayout.CapsulePadding,
            TabBarLayout.Cell(capsule, count, count - 1, 1f).Max.X, Tolerance);
    }

    [Fact]
    public void IconSitsAtTheCentreOfItsCell()
    {
        var capsule = TabBarLayout.FullCapsule(Area, 1f, false);
        var cell = TabBarLayout.Cell(capsule, 4, 1, 1f);
        Assert.Equal(cell.Center, TabBarLayout.IconCenter(cell));
    }

    [Fact]
    public void MiddleHighlightStaysInsideItsCell()
    {
        var capsule = TabBarLayout.FullCapsule(Area, 1f, false);
        var cell = TabBarLayout.Cell(capsule, 3, 1, 1f);
        var highlight = TabBarLayout.Highlight(capsule, 3, 1, 1f);
        Assert.Equal(cell.Min.X + TabBarLayout.HighlightInset, highlight.Min.X, Tolerance);
        Assert.Equal(cell.Max.X - TabBarLayout.HighlightInset, highlight.Max.X, Tolerance);
        Assert.Equal(cell.Height - TabBarLayout.HighlightInset * 2f, highlight.Height, Tolerance);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void EndHighlightKeepsOneGapToTheCapsuleAllRound(float scale)
    {
        var capsule = TabBarLayout.FullCapsule(Area, scale, false);
        var gap = TabBarLayout.HighlightInset * scale;
        var first = TabBarLayout.Highlight(capsule, 5, 0, scale);
        var last = TabBarLayout.Highlight(capsule, 5, 4, scale);
        Assert.Equal(capsule.Min.X + gap, first.Min.X, Tolerance);
        Assert.Equal(capsule.Max.X - gap, last.Max.X, Tolerance);
        Assert.Equal(capsule.Min.Y + gap, first.Min.Y, Tolerance);
        Assert.Equal(capsule.Max.Y - gap, first.Max.Y, Tolerance);
        Assert.Equal(capsule.Min.X + capsule.Height * 0.5f, first.Min.X + first.Height * 0.5f, Tolerance);
        Assert.Equal(capsule.Max.X - capsule.Height * 0.5f, last.Max.X - last.Height * 0.5f, Tolerance);
    }

    [Fact]
    public void SlidingHighlightBlendsTheSlotsEitherSide()
    {
        var capsule = TabBarLayout.FullCapsule(Area, 1f, false);
        var from = TabBarLayout.Highlight(capsule, 4, 0, 1f);
        var to = TabBarLayout.Highlight(capsule, 4, 1, 1f);
        var halfway = TabBarLayout.Highlight(capsule, 4, 0.5f, 1f);
        Assert.Equal((from.Min.X + to.Min.X) * 0.5f, halfway.Min.X, Tolerance);
        Assert.Equal((from.Width + to.Width) * 0.5f, halfway.Width, Tolerance);
        Assert.Equal(TabBarLayout.Highlight(capsule, 4, 3, 1f), TabBarLayout.Highlight(capsule, 4, 3f, 1f));
        Assert.Equal(TabBarLayout.Highlight(capsule, 4, 0, 1f), TabBarLayout.Highlight(capsule, 4, -0.2f, 1f));
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void ContentInsetClearsTheBarAndItsGap(float scale)
    {
        var inset = TabBarLayout.ContentInset(scale);
        Assert.Equal((TabBarLayout.Height + TabBarLayout.BottomInset + TabBarLayout.ContentGap) * scale, inset,
            Tolerance);
        var content = TabBarLayout.ContentArea(Area, scale);
        Assert.Equal(Area.Max.Y - inset, content.Max.Y, Tolerance);
        Assert.Equal(Area.Min, content.Min);
        var zone = TabBarLayout.Zone(Area, scale);
        Assert.Equal(TabBarLayout.FullCapsule(Area, scale, false).Min.Y, zone.Min.Y, Tolerance);
        Assert.Equal(Area.Max, zone.Max);
    }
}
