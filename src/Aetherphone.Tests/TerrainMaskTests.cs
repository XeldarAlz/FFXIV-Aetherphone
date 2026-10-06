using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.World;
using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TerrainMaskTests
{
    private const int Width = 640;
    private const int Height = 360;
    private const float Cell = 0.05f;
    private const float Tolerance = 0.0001f;

    [Theory]
    [InlineData((byte)TerrainStyle.Hills)]
    [InlineData((byte)TerrainStyle.Islands)]
    [InlineData((byte)TerrainStyle.Caverns)]
    public void SameSeedGeneratesIdenticalTerrain(byte style)
    {
        var first = Generated((TerrainStyle)style, 0xC0FFEEUL);
        var second = Generated((TerrainStyle)style, 0xC0FFEEUL);
        var other = Generated((TerrainStyle)style, 0xBADCAFEUL);

        var differences = 0;
        for (var row = 0; row < Height; row++)
        {
            for (var column = 0; column < Width; column++)
            {
                Assert.Equal(first.IsSolid(column, row), second.IsSolid(column, row));
                if (first.IsSolid(column, row) != other.IsSolid(column, row))
                {
                    differences++;
                }
            }
        }

        Assert.True(differences > Width, "a different seed should shape different terrain");
        Assert.Equal(first.Plateaus.Length, second.Plateaus.Length);
        for (var plateauIndex = 0; plateauIndex < first.Plateaus.Length; plateauIndex++)
        {
            Assert.Equal(first.Plateaus[plateauIndex].Center, second.Plateaus[plateauIndex].Center);
        }
    }

    [Fact]
    public void GenerationAdvancesTheCallersRandom()
    {
        var mask = new TerrainMask(Width, Height, Cell);
        var random = GameRandom.FromSeed(7UL);
        var untouched = GameRandom.FromSeed(7UL);

        mask.Generate(ref random, TerrainStyle.Hills);

        Assert.NotEqual(untouched.NextUInt(), random.NextUInt());
    }

    [Theory]
    [InlineData((byte)TerrainStyle.Hills, 1UL)]
    [InlineData((byte)TerrainStyle.Hills, 99UL)]
    [InlineData((byte)TerrainStyle.Islands, 2UL)]
    [InlineData((byte)TerrainStyle.Islands, 1234UL)]
    [InlineData((byte)TerrainStyle.Caverns, 3UL)]
    [InlineData((byte)TerrainStyle.Caverns, 4321UL)]
    public void SpawnPlateausAreFlatAndSolidUnderneath(byte style, ulong seed)
    {
        var mask = Generated((TerrainStyle)style, seed);

        Assert.Equal(TerrainMask.MaxPlateaus, mask.Plateaus.Length);
        for (var plateauIndex = 0; plateauIndex < mask.Plateaus.Length; plateauIndex++)
        {
            var plateau = mask.Plateaus[plateauIndex];
            var surfaceRow = mask.RowOf(plateau.Center.Y + Cell * 0.5f);
            var firstColumn = mask.ColumnOf(plateau.Left + Cell * 0.5f);
            var lastColumn = mask.ColumnOf(plateau.Right - Cell * 0.5f);
            Assert.True(lastColumn - firstColumn >= 6, "a plateau should be wide enough to stand on");
            for (var column = firstColumn; column <= lastColumn; column++)
            {
                for (var below = 0; below < 8; below++)
                {
                    Assert.True(mask.IsSolid(column, surfaceRow + below), $"plateau {plateauIndex} column {column} row +{below}");
                }

                for (var above = 1; above <= 12; above++)
                {
                    Assert.False(mask.IsSolid(column, surfaceRow - above), $"plateau {plateauIndex} column {column} row -{above}");
                }

                var x = (column + 0.5f) * Cell;
                Assert.Equal(plateau.Center.Y, mask.SurfaceY(x, plateau.Center.Y - Cell * 10f), Tolerance);
            }

            var normal = mask.Normal(plateau.Center);
            Assert.Equal(0f, normal.X, 0.001f);
            Assert.Equal(-1f, normal.Y, 0.001f);
        }
    }

    [Fact]
    public void SpawnPointsSpreadAcrossThePlateausFromLeftToRight()
    {
        var mask = Generated(TerrainStyle.Hills, 42UL);

        var four = mask.SpawnPoints(4).ToArray();
        var everyone = mask.SpawnPoints(20);
        var one = mask.SpawnPoints(1);

        Assert.Equal(4, four.Length);
        Assert.Equal(mask.Plateaus[0].Center, four[0]);
        Assert.Equal(mask.Plateaus[TerrainMask.MaxPlateaus - 1].Center, four[3]);
        for (var spawnIndex = 1; spawnIndex < four.Length; spawnIndex++)
        {
            Assert.True(four[spawnIndex].X > four[spawnIndex - 1].X);
        }

        Assert.Equal(TerrainMask.MaxPlateaus, everyone.Length);
        Assert.Equal(1, one.Length);
        Assert.Equal(0, mask.SpawnPoints(0).Length);
    }

    [Fact]
    public void CarveRemovesTheCellsInsideTheCircleAndReturnsTheCount()
    {
        var mask = new TerrainMask(64, 64, 0.1f);
        Assert.Equal(64 * 64, mask.FillRect(new Rect(Vector2.Zero, new Vector2(6.4f, 6.4f))));
        var center = new Vector2(3.23f, 2.87f);
        const float radius = 1.05f;

        var removed = mask.Carve(center, radius);

        var expected = 0;
        for (var row = 0; row < 64; row++)
        {
            for (var column = 0; column < 64; column++)
            {
                var inside = Vector2.DistanceSquared(mask.CellCenter(column, row), center) <= radius * radius;
                if (inside)
                {
                    expected++;
                }

                Assert.Equal(!inside, mask.IsSolid(column, row));
            }
        }

        Assert.True(expected > 300);
        Assert.Equal(expected, removed);
        Assert.Equal(0, mask.Carve(center, radius));
    }

    [Fact]
    public void CarveAtTheEdgeClipsToTheGrid()
    {
        var mask = new TerrainMask(100, 40, 1f);
        mask.FillRect(new Rect(Vector2.Zero, new Vector2(100f, 40f)));

        var removed = mask.Carve(new Vector2(0f, 0f), 5f);

        var expected = 0;
        for (var row = 0; row < 5; row++)
        {
            for (var column = 0; column < 5; column++)
            {
                if ((column + 0.5f) * (column + 0.5f) + (row + 0.5f) * (row + 0.5f) <= 25f)
                {
                    expected++;
                }
            }
        }

        Assert.Equal(expected, removed);
        Assert.Equal(0, mask.Carve(new Vector2(-50f, -50f), 5f));
    }

    [Fact]
    public void CarveAcrossWordBoundariesCountsEveryCell()
    {
        var mask = new TerrainMask(200, 3, 1f);
        mask.FillRect(new Rect(Vector2.Zero, new Vector2(200f, 3f)));

        var removed = mask.CarveRect(new Rect(new Vector2(60f, 1f), new Vector2(140f, 2f)));

        Assert.Equal(80, removed);
        Assert.True(mask.IsSolid(59, 1));
        Assert.False(mask.IsSolid(60, 1));
        Assert.False(mask.IsSolid(139, 1));
        Assert.True(mask.IsSolid(140, 1));
        Assert.True(mask.IsSolid(100, 0));
    }

    [Fact]
    public void FillAddsOnlyMissingCells()
    {
        var mask = new TerrainMask(64, 64, 0.1f);
        var center = new Vector2(3.2f, 3.2f);

        var first = mask.Fill(center, 0.8f);
        var second = mask.Fill(center, 0.8f);
        var carved = mask.Carve(center, 0.8f);

        Assert.True(first > 150);
        Assert.Equal(0, second);
        Assert.Equal(first, carved);
    }

    [Fact]
    public void TakeDirtyReportsTheChangedRegionOnce()
    {
        var mask = new TerrainMask(64, 64, 1f);
        Assert.False(mask.TakeDirty(out _));
        mask.Clear();
        Assert.True(mask.TakeDirty(out var whole));
        Assert.Equal(64, whole.Columns);
        Assert.Equal(64, whole.Rows);
        Assert.False(mask.TakeDirty(out _));

        mask.Carve(new Vector2(30f, 30f), 4f);
        Assert.False(mask.TakeDirty(out _));

        mask.Fill(new Vector2(30f, 30f), 4f);
        Assert.True(mask.TakeDirty(out var region));
        Assert.Equal(26, region.MinColumn);
        Assert.Equal(33, region.MaxColumn);
        Assert.Equal(26, region.MinRow);
        Assert.Equal(33, region.MaxRow);
        Assert.False(mask.TakeDirty(out _));
    }

    [Fact]
    public void CollideCircleNormalPointsOutOfFlatGround()
    {
        var mask = FlatGround(out var surfaceY);
        var center = new Vector2(3.2f, surfaceY - 0.5f + 0.2f);

        var touching = mask.CollideCircle(center, 0.5f, out var normal, out var depth);

        Assert.True(touching);
        Assert.Equal(0f, normal.X, 0.001f);
        Assert.Equal(-1f, normal.Y, 0.001f);
        Assert.Equal(0.2f, depth, 0.01f);
        Assert.False(mask.CollideCircle(new Vector2(3.2f, surfaceY - 0.52f), 0.5f, out _, out _));
    }

    [Fact]
    public void CollideCircleNormalLeansAwayFromASlope()
    {
        var mask = new TerrainMask(128, 128, 0.1f);
        for (var column = 0; column < 128; column++)
        {
            var top = 100 - column / 2;
            mask.FillRect(new Rect(new Vector2(column * 0.1f, top * 0.1f), new Vector2((column + 1) * 0.1f, 12.8f)));
        }

        var x = 6.4f;
        var center = new Vector2(x, mask.SurfaceY(x) - 0.3f);

        var touching = mask.CollideCircle(center, 0.5f, out var normal, out var depth);

        Assert.True(touching);
        Assert.True(depth > 0f);
        Assert.True(normal.Y < -0.8f);
        Assert.True(normal.X < -0.2f, "ground rising to the right pushes the circle up and to the left");
        Assert.InRange(normal.Length(), 0.999f, 1.001f);
    }

    [Fact]
    public void CollideCircleMissesInOpenAir()
    {
        var mask = FlatGround(out _);

        Assert.False(mask.CollideCircle(new Vector2(3.2f, 1f), 0.5f, out var normal, out var depth));
        Assert.Equal(new Vector2(0f, -1f), normal);
        Assert.Equal(0f, depth);
    }

    [Fact]
    public void RaycastStraightDownHitsTheSurface()
    {
        var mask = FlatGround(out var surfaceY);

        var hit = mask.Raycast(new Vector2(3.25f, 0.5f), new Vector2(0f, 3f), 20f, out var result);

        Assert.True(hit);
        Assert.Equal(surfaceY, result.Point.Y, 0.001f);
        Assert.Equal(surfaceY - 0.5f, result.Distance, 0.001f);
        Assert.Equal(0f, result.Normal.X, 0.001f);
        Assert.Equal(-1f, result.Normal.Y, 0.001f);
        Assert.Equal(mask.RowOf(surfaceY + 0.01f), result.Row);
    }

    [Fact]
    public void RaycastAtAnAngleLandsOnTheSurface()
    {
        var mask = FlatGround(out var surfaceY);
        var origin = new Vector2(1f, 1f);
        var direction = Vector2.Normalize(new Vector2(1f, 1f));

        Assert.True(mask.Raycast(origin, direction, 20f, out var result));
        Assert.Equal(surfaceY, result.Point.Y, 0.001f);
        Assert.Equal(1f + (surfaceY - 1f), result.Point.X, 0.001f);
        Assert.Equal((result.Point - origin).Length(), result.Distance, 0.001f);
    }

    [Fact]
    public void RaycastFromOutsideTheGridEntersIt()
    {
        var mask = FlatGround(out var surfaceY);

        Assert.True(mask.Raycast(new Vector2(3.2f, -10f), new Vector2(0f, 1f), 50f, out var result));
        Assert.Equal(surfaceY, result.Point.Y, 0.001f);
        Assert.Equal(surfaceY + 10f, result.Distance, 0.001f);
    }

    [Fact]
    public void RaycastMissesWhenPointingAwayOrTooShort()
    {
        var mask = FlatGround(out var surfaceY);

        Assert.False(mask.Raycast(new Vector2(3.2f, 1f), new Vector2(0f, -1f), 50f, out _));
        Assert.False(mask.Raycast(new Vector2(3.2f, 1f), new Vector2(0f, 1f), surfaceY - 1.5f, out _));
        Assert.False(mask.Raycast(new Vector2(3.2f, 1f), new Vector2(1f, 0f), 50f, out _));
        Assert.False(mask.Raycast(new Vector2(3.2f, 1f), Vector2.Zero, 50f, out _));
    }

    [Fact]
    public void RaycastStartingInsideTheGroundHitsAtZero()
    {
        var mask = FlatGround(out var surfaceY);

        Assert.True(mask.Raycast(new Vector2(3.2f, surfaceY + 1f), new Vector2(1f, 0f), 5f, out var result));
        Assert.Equal(0f, result.Distance);
    }

    [Fact]
    public void SurfaceYFindsTheTopOfTheGround()
    {
        var mask = FlatGround(out var surfaceY);

        Assert.Equal(surfaceY, mask.SurfaceY(2.5f), Tolerance);
        Assert.Equal(mask.WorldHeight, mask.SurfaceY(-1f));
        Assert.Equal(mask.WorldHeight, mask.SurfaceY(100f));
        mask.Carve(new Vector2(2.55f, surfaceY), 0.3f);
        Assert.True(mask.SurfaceY(2.55f) > surfaceY);
    }

    private static TerrainMask Generated(TerrainStyle style, ulong seed)
    {
        var mask = new TerrainMask(Width, Height, Cell);
        var random = GameRandom.FromSeed(seed);
        mask.Generate(ref random, style);
        return mask;
    }

    private static TerrainMask FlatGround(out float surfaceY)
    {
        var mask = new TerrainMask(64, 64, 0.1f);
        surfaceY = 4f;
        mask.FillRect(new Rect(new Vector2(0f, surfaceY), new Vector2(6.4f, 6.4f)));
        return mask;
    }
}
