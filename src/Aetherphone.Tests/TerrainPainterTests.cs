using System.Numerics;
using Aetherphone.Apps.Games.Framework.World;
using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TerrainPainterTests
{
    private const int Size = 32;
    private const int SurfaceRow = 12;

    [Fact]
    public void AirIsTransparentAndGroundIsOpaque()
    {
        var mask = FlatGround();
        var painter = new TerrainPainter(mask, TerrainMaterial.Earth);

        Assert.Equal(0, Alpha(painter, 5, SurfaceRow - 1));
        Assert.Equal(255, Alpha(painter, 5, SurfaceRow));
        Assert.Equal(255, Alpha(painter, 5, Size - 1));
        Assert.Equal(Size * Size * TerrainPainter.BytesPerPixel, painter.Pixels.Length);
    }

    [Fact]
    public void TheSurfaceCellIsLitWithTheEdgeColour()
    {
        var mask = FlatGround();
        var painter = new TerrainPainter(mask, TerrainMaterial.Earth);

        var surface = Rgb(painter, 5, SurfaceRow);
        var deep = Rgb(painter, 5, Size - 2);
        var edge = TerrainMaterial.Earth.Edge * 255f;

        Assert.InRange(surface.Y, edge.Y * 0.9f, edge.Y * 1.1f);
        Assert.True(surface.Y > deep.Y + 40f, "the lit edge should be much brighter than deep ground");
    }

    [Fact]
    public void RepaintingACarvedRegionOpensItToTheSky()
    {
        var mask = FlatGround();
        var painter = new TerrainPainter(mask, TerrainMaterial.Stone);
        mask.TakeDirty(out _);

        mask.Carve(new Vector2(16f, 16f), 3f);
        Assert.True(mask.TakeDirty(out var region));
        painter.Paint(region);

        Assert.Equal(0, Alpha(painter, 16, 16));
        Assert.Equal(255, Alpha(painter, 16, 20));
        var crater = Rgb(painter, 16, 19);
        var edge = TerrainMaterial.Stone.Edge * 255f;
        Assert.InRange(crater.Y, edge.Y * 0.9f, edge.Y * 1.1f);
    }

    [Fact]
    public void SwappingTheMaterialRepaintsEverything()
    {
        var mask = FlatGround();
        var painter = new TerrainPainter(mask, TerrainMaterial.Earth);
        var before = Rgb(painter, 5, SurfaceRow);

        painter.SetMaterial(TerrainMaterial.Lunar);

        Assert.NotEqual(before, Rgb(painter, 5, SurfaceRow));
        Assert.Equal(TerrainMaterial.Lunar.Edge.X, painter.Material.Edge.X);
    }

    [Fact]
    public void ARepaintCoversTheRowAboveAndTheTopsoilBelowInsideTheMask()
    {
        var painter = new TerrainPainter(FlatGround(), TerrainMaterial.Earth);

        var inside = painter.Coverage(new CellRegion(4, 6, 9, 8));
        Assert.Equal(4, inside.MinColumn);
        Assert.Equal(5, inside.MinRow);
        Assert.Equal(9, inside.MaxColumn);
        Assert.Equal(8 + TerrainPainter.TopsoilCells, inside.MaxRow);

        var edge = painter.Coverage(new CellRegion(-6, -3, 40, 30));
        Assert.Equal(0, edge.MinColumn);
        Assert.Equal(0, edge.MinRow);
        Assert.Equal(Size - 1, edge.MaxColumn);
        Assert.Equal(Size - 1, edge.MaxRow);
        Assert.Equal(Size, painter.Whole.Columns);
        Assert.Equal(Size, painter.Whole.Rows);
    }

    [Fact]
    public void RegionsUniteAndOverlapByCell()
    {
        var first = new CellRegion(2, 3, 5, 6);
        var second = new CellRegion(4, 1, 9, 4);
        var apart = new CellRegion(6, 7, 8, 9);

        var union = CellRegion.Union(first, second);
        Assert.Equal(2, union.MinColumn);
        Assert.Equal(1, union.MinRow);
        Assert.Equal(9, union.MaxColumn);
        Assert.Equal(6, union.MaxRow);
        Assert.True(first.Overlaps(second));
        Assert.True(second.Overlaps(first));
        Assert.False(first.Overlaps(apart));
        Assert.True(new CellRegion(5, 6, 5, 6).Overlaps(first));
    }

    [Fact]
    public void AnOverlayPaintsOverEveryRepaintedRegion()
    {
        var mask = FlatGround();
        var overlay = new MarkingOverlay();
        var painter = new TerrainPainter(mask, TerrainMaterial.Earth);
        overlay.Paint(painter.Canvas, Size, painter.Coverage(new CellRegion(5, SurfaceRow, 5, SurfaceRow)));

        Assert.Equal(1, overlay.Calls);
        Assert.Equal(MarkingOverlay.Marker, painter.Pixels[(SurfaceRow * Size + 5) * TerrainPainter.BytesPerPixel]);
        Assert.False(overlay.TakeDirty(out _));
    }

    private sealed class MarkingOverlay : ITerrainOverlay
    {
        public const byte Marker = 7;

        public int Calls { get; private set; }

        public bool TakeDirty(out CellRegion region)
        {
            region = default;
            return false;
        }

        public void Paint(Span<byte> pixels, int width, in CellRegion region)
        {
            Calls++;
            for (var row = region.MinRow; row <= region.MaxRow; row++)
            {
                for (var column = region.MinColumn; column <= region.MaxColumn; column++)
                {
                    pixels[(row * width + column) * TerrainPainter.BytesPerPixel] = Marker;
                }
            }
        }
    }

    private static TerrainMask FlatGround()
    {
        var mask = new TerrainMask(Size, Size, 1f);
        mask.FillRect(new Rect(new Vector2(0f, SurfaceRow), new Vector2(Size, Size)));
        return mask;
    }

    private static byte Alpha(TerrainPainter painter, int column, int row) =>
        painter.Pixels[(row * Size + column) * TerrainPainter.BytesPerPixel + 3];

    private static Vector3 Rgb(TerrainPainter painter, int column, int row)
    {
        var offset = (row * Size + column) * TerrainPainter.BytesPerPixel;
        return new Vector3(painter.Pixels[offset], painter.Pixels[offset + 1], painter.Pixels[offset + 2]);
    }
}
