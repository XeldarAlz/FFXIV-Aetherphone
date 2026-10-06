using System.Numerics;
using Aetherphone.Apps.Games.Framework.World;
using Xunit;

namespace Aetherphone.Tests;

public sealed class LaneGridTests
{
    private const byte Sprout = 1;
    private const byte Thornwall = 2;

    [Fact]
    public void PlacingOccupiesTheCellAndBlocksASecondPlacement()
    {
        var grid = new LaneGrid(5, 8);

        Assert.True(grid.CanPlace(2, 3));
        Assert.True(grid.Place(2, 3, Sprout, 41));

        Assert.False(grid.CanPlace(2, 3));
        Assert.False(grid.Place(2, 3, Thornwall, 42));
        Assert.Equal(Sprout, grid.KindAt(2, 3));
        Assert.Equal(41, grid.EntityAt(2, 3));
        Assert.True(grid.CanPlace(3, 3));
    }

    [Fact]
    public void PlacementOutsideTheGridOrWithTheEmptyKindFails()
    {
        var grid = new LaneGrid(5, 8);

        Assert.False(grid.CanPlace(-1, 0));
        Assert.False(grid.CanPlace(5, 0));
        Assert.False(grid.CanPlace(0, 8));
        Assert.False(grid.Place(5, 0, Sprout, 1));
        Assert.False(grid.Place(0, 0, LaneGrid.EmptyKind, 1));
        Assert.True(grid.CanPlace(0, 0));
        Assert.Equal(LaneGrid.EmptyKind, grid.KindAt(9, 9));
        Assert.Equal(LaneGrid.NoEntity, grid.EntityAt(-3, 2));
    }

    [Fact]
    public void RemovingFreesTheCellOnce()
    {
        var grid = new LaneGrid(5, 8);
        grid.Place(1, 6, Thornwall, 7);

        Assert.True(grid.Remove(1, 6));
        Assert.False(grid.Remove(1, 6));
        Assert.False(grid.Remove(40, 6));
        Assert.True(grid.CanPlace(1, 6));
        Assert.Equal(LaneGrid.NoEntity, grid.EntityAt(1, 6));
    }

    [Fact]
    public void CellCentresAreInWorldUnits()
    {
        var grid = new LaneGrid(5, 8, 1.5f);

        Assert.Equal(new Vector2(0.75f, 0.75f), grid.CellCenter(0, 0));
        Assert.Equal(new Vector2(6.75f, 11.25f), grid.CellCenter(4, 7));
        Assert.Equal(7.5f, grid.WorldWidth);
        Assert.Equal(12f, grid.WorldHeight);
    }

    [Fact]
    public void CellAtMapsAWorldPointBackToItsCell()
    {
        var grid = new LaneGrid(5, 8, 1.5f);

        Assert.True(grid.CellAt(new Vector2(3.1f, 10.6f), out var column, out var row));
        Assert.Equal(2, column);
        Assert.Equal(7, row);
        Assert.False(grid.CellAt(new Vector2(-0.1f, 2f), out _, out _));
        Assert.False(grid.CellAt(new Vector2(2f, 12.01f), out _, out _));
    }

    [Fact]
    public void NextOccupiedScansAColumnInEitherDirection()
    {
        var grid = new LaneGrid(5, 8);
        grid.Place(2, 2, Sprout, 1);
        grid.Place(2, 5, Thornwall, 2);

        Assert.Equal(2, grid.NextOccupied(2, 0, 1));
        Assert.Equal(5, grid.NextOccupied(2, 3, 1));
        Assert.Equal(-1, grid.NextOccupied(2, 6, 1));
        Assert.Equal(5, grid.NextOccupied(2, 7, -1));
        Assert.Equal(2, grid.NextOccupied(2, 4, -1));
        Assert.Equal(-1, grid.NextOccupied(3, 0, 1));
        Assert.Equal(-1, grid.NextOccupied(2, 0, 0));
        Assert.Equal(-1, grid.NextOccupied(9, 0, 1));
    }

    [Fact]
    public void ClearEmptiesEveryCell()
    {
        var grid = new LaneGrid(5, 8);
        grid.Place(0, 0, Sprout, 1);
        grid.Place(4, 7, Thornwall, 2);

        grid.Clear();

        Assert.True(grid.CanPlace(0, 0));
        Assert.True(grid.CanPlace(4, 7));
        Assert.Equal(LaneGrid.NoEntity, grid.EntityAt(4, 7));
    }
}
