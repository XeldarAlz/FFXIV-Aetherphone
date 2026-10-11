using Aetherphone.Apps.Casino.Machines;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MachineCellOutlineTests
{
    private const int Reels = CrystalCascadeRules.Reels;
    private const int Rows = CrystalCascadeRules.Rows;

    [Fact]
    public void ASingleCellIsOneSquare()
    {
        var outline = new MachineCellOutline();
        Assert.Equal(4, outline.Build(new[] { Cell(2, 3) }, Reels, Rows));
        Assert.Equal(4, Perimeter(outline));
    }

    [Fact]
    public void ABlockIsOneOutlineNotABoxPerCell()
    {
        var outline = new MachineCellOutline();
        var block = new[] { Cell(1, 1), Cell(1, 2), Cell(2, 1), Cell(2, 2) };
        Assert.Equal(4, outline.Build(block, Reels, Rows));
        Assert.Equal(8, Perimeter(outline));
        for (var index = 0; index < outline.Count; index++)
        {
            var segment = outline.SegmentAt(index);
            Assert.Equal(2, Length(segment));
        }
    }

    [Fact]
    public void AnLShapeTurnsSixCorners()
    {
        var outline = new MachineCellOutline();
        var shape = new[] { Cell(0, 0), Cell(0, 1), Cell(0, 2), Cell(1, 2) };
        Assert.Equal(6, outline.Build(shape, Reels, Rows));
        Assert.Equal(10, Perimeter(outline));
    }

    [Fact]
    public void DiagonalNeighboursStaySeparateShapes()
    {
        var outline = new MachineCellOutline();
        Assert.Equal(8, outline.Build(new[] { Cell(0, 0), Cell(1, 1) }, Reels, Rows));
        Assert.Equal(8, Perimeter(outline));
    }

    [Fact]
    public void ThePerimeterMatchesEveryExposedCellEdge()
    {
        var random = new Random(7);
        var outline = new MachineCellOutline();
        for (var trial = 0; trial < 200; trial++)
        {
            var mask = new bool[Reels * Rows];
            var picked = new List<int>();
            for (var cell = 0; cell < mask.Length; cell++)
            {
                if (random.NextDouble() < 0.4)
                {
                    mask[cell] = true;
                    picked.Add(cell);
                }
            }

            outline.Build(picked.ToArray(), Reels, Rows);
            Assert.Equal(ExposedEdges(mask), Perimeter(outline));
            Assert.True(outline.Count <= MachineCellOutline.MaxSegments);
            for (var index = 0; index < outline.Count; index++)
            {
                var segment = outline.SegmentAt(index);
                Assert.True(segment.FromColumn == segment.ToColumn || segment.FromRow == segment.ToRow);
                Assert.InRange(segment.ToColumn, 0, Reels);
                Assert.InRange(segment.ToRow, 0, Rows);
            }
        }
    }

    [Fact]
    public void BadCellsAreIgnored()
    {
        var outline = new MachineCellOutline();
        Assert.Equal(0, outline.Build(null, Reels, Rows));
        Assert.Equal(0, outline.Build(new[] { -1, Reels * Rows }, Reels, Rows));
        Assert.Equal(-1, MachineCellOutline.Anchor(new[] { -4, 99 }, Reels, Rows));
    }

    [Fact]
    public void TheLabelSitsOnTheCellNearestTheMiddle()
    {
        var row = new[] { Cell(0, 2), Cell(1, 2), Cell(2, 2), Cell(3, 2), Cell(4, 2) };
        Assert.Equal(Cell(2, 2), MachineCellOutline.Anchor(row, Reels, Rows));
        var scattered = new[] { Cell(0, 0), Cell(5, 4), Cell(3, 2) };
        Assert.Equal(Cell(3, 2), MachineCellOutline.Anchor(scattered, Reels, Rows));
        Assert.Equal(Cell(1, 1), MachineCellOutline.Anchor(new[] { Cell(1, 1) }, Reels, Rows));
    }

    private static int Cell(int reel, int row) => reel * Rows + row;

    private static int Length(OutlineSegment segment) =>
        Math.Abs(segment.ToColumn - segment.FromColumn) + Math.Abs(segment.ToRow - segment.FromRow);

    private static int Perimeter(MachineCellOutline outline)
    {
        var total = 0;
        for (var index = 0; index < outline.Count; index++)
        {
            total += Length(outline.SegmentAt(index));
        }

        return total;
    }

    private static int ExposedEdges(bool[] mask)
    {
        var edges = 0;
        for (var cell = 0; cell < mask.Length; cell++)
        {
            if (!mask[cell])
            {
                continue;
            }

            var reel = cell / Rows;
            var row = cell % Rows;
            edges += Inside(mask, reel - 1, row) ? 0 : 1;
            edges += Inside(mask, reel + 1, row) ? 0 : 1;
            edges += Inside(mask, reel, row - 1) ? 0 : 1;
            edges += Inside(mask, reel, row + 1) ? 0 : 1;
        }

        return edges;
    }

    private static bool Inside(bool[] mask, int reel, int row) =>
        reel >= 0 && reel < Reels && row >= 0 && row < Rows && mask[reel * Rows + row];
}
