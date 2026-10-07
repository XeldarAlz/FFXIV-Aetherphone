using System.Text;
using Aetherphone.Apps.Games.Broadside;
using Aetherphone.Apps.Games.Framework;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BroadsideBoardTests
{
    private const int ShotGuard = BroadsideFleet.CellCount * 2 + 4;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = PlayMatch(1234, out var firstTrace);
        var second = PlayMatch(1234, out var secondTrace);
        PlayMatch(99, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.Winner, second.Winner);
        Assert.True(first.Over);
        Assert.True(first.Fleet(BroadsideBoard.Opponent(first.Winner)).AllSunk);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    [Fact]
    public void PlacementRejectsOverlapsAndShipsOffTheGrid()
    {
        var fleet = new BroadsideFleet();

        Assert.True(fleet.Place(0, 0, 0, true));
        Assert.False(fleet.CanPlace(1, 4, 0, false));
        Assert.False(fleet.Place(1, 2, 0, true));
        Assert.False(fleet.CanPlace(1, 7, 3, true));
        Assert.False(fleet.CanPlace(1, 3, 7, false));
        Assert.False(fleet.CanPlace(4, -1, 0, true));
        Assert.True(fleet.CanPlace(1, 6, 0, true));
        Assert.True(fleet.CanPlace(0, 5, 0, true));
        Assert.True(fleet.Place(0, 0, 5, false));
        Assert.Equal(BroadsideFleet.NoShip, fleet.OccupantAt(BroadsideFleet.CellOf(0, 0)));
        Assert.Equal(0, fleet.OccupantAt(BroadsideFleet.CellOf(0, 9)));
        Assert.False(fleet.AllPlaced);
    }

    [Fact]
    public void AutoPlacementAlwaysSeatsTheWholeFleetWithoutOverlap()
    {
        Span<int> cells = stackalloc int[BroadsideFleet.LongestShip];
        for (ulong seed = 1; seed <= 60; seed++)
        {
            var fleet = new BroadsideFleet();
            var random = GameRandom.FromSeed(seed);
            fleet.AutoPlace(ref random);

            Assert.True(fleet.AllPlaced);
            var occupied = 0;
            for (var cell = 0; cell < BroadsideFleet.CellCount; cell++)
            {
                if (fleet.OccupantAt(cell) != BroadsideFleet.NoShip)
                {
                    occupied++;
                }
            }

            Assert.Equal(BroadsideFleet.ShipCells, occupied);
            for (var ship = 0; ship < BroadsideFleet.ShipCount; ship++)
            {
                var count = fleet.ShipCellsOf(ship, cells);
                Assert.Equal(BroadsideFleet.Length(ship), count);
                for (var index = 0; index < count; index++)
                {
                    Assert.Equal(ship, fleet.OccupantAt(cells[index]));
                }
            }
        }
    }

    [Fact]
    public void AShipSinksOnlyWhenEveryCellIsHit()
    {
        var fleet = new BroadsideFleet();
        fleet.Place(2, 3, 4, true);

        Assert.Equal(ShotResult.Miss, fleet.Fire(BroadsideFleet.CellOf(0, 0), out _));
        Assert.Equal(ShotResult.Hit, fleet.Fire(BroadsideFleet.CellOf(3, 4), out var first));
        Assert.Equal(2, first);
        Assert.Equal(ShotResult.Hit, fleet.Fire(BroadsideFleet.CellOf(5, 4), out _));
        Assert.False(fleet.IsSunk(2));
        Assert.False(fleet.IsSunkCell(BroadsideFleet.CellOf(3, 4)));
        Assert.Equal(ShotResult.Sunk, fleet.Fire(BroadsideFleet.CellOf(4, 4), out var sunk));
        Assert.Equal(2, sunk);
        Assert.True(fleet.IsSunk(2));
        Assert.True(fleet.IsSunkCell(BroadsideFleet.CellOf(3, 4)));
        Assert.Equal(4, fleet.ShotsTaken);
    }

    [Fact]
    public void FiringAtTheSameCellTwiceIsRefusedAndKeepsTheTurn()
    {
        var board = new BroadsideBoard();
        board.Reset();
        PlaceRow(board.Fleet(0));
        PlaceRow(board.Fleet(1));

        Assert.Equal(ShotResult.Miss, board.Fire(BroadsideFleet.CellOf(9, 9), out _));
        Assert.Equal(1, board.Turn);
        Assert.Equal(ShotResult.Hit, board.Fire(BroadsideFleet.CellOf(0, 0), out _));
        Assert.Equal(0, board.Turn);
        Assert.Equal(ShotResult.Invalid, board.Fire(BroadsideFleet.CellOf(9, 9), out _));
        Assert.Equal(0, board.Turn);
        Assert.Equal(1, board.Shots(0));
        Assert.Equal(1, board.Shots(1));
        Assert.Equal(1, board.Hits(1));
        Assert.Equal(100, board.Accuracy(1));
    }

    [Fact]
    public void SinkingTheLastShipWinsTheBattle()
    {
        var board = new BroadsideBoard();
        board.Reset();
        PlaceRow(board.Fleet(0));
        PlaceRow(board.Fleet(1));
        var spare = 0;
        for (var ship = 0; ship < BroadsideFleet.ShipCount; ship++)
        {
            for (var step = 0; step < BroadsideFleet.Length(ship); step++)
            {
                Assert.Equal(0, board.Turn);
                var result = board.Fire(BroadsideFleet.CellOf(step, ship * 2), out _);
                Assert.NotEqual(ShotResult.Invalid, result);
                if (board.Over)
                {
                    break;
                }

                Assert.Equal(ShotResult.Miss, board.Fire(BroadsideFleet.CellOf(9 - spare % 5, 1 + 2 * (spare / 5)), out _));
                spare++;
            }
        }

        Assert.True(board.Over);
        Assert.Equal(0, board.Winner);
        Assert.True(board.Fleet(1).AllSunk);
        Assert.Equal(ShotResult.Invalid, board.Fire(BroadsideFleet.CellOf(9, 9), out _));
    }

    [Fact]
    public void TheAiNeverFiresAtTheSameCellTwice()
    {
        for (var level = 0; level < 2; level++)
        {
            for (ulong seed = 1; seed <= 12; seed++)
            {
                var fleet = new BroadsideFleet();
                var placement = GameRandom.FromSeed(seed);
                fleet.AutoPlace(ref placement);
                var ai = new BroadsideAi();
                var random = GameRandom.FromSeed(seed * 31);
                var fired = new HashSet<int>();
                for (var shot = 0; shot < BroadsideFleet.CellCount && !fleet.AllSunk; shot++)
                {
                    var cell = ai.ChooseShot(fleet, level == 1, ref random);
                    Assert.InRange(cell, 0, BroadsideFleet.CellCount - 1);
                    Assert.True(fired.Add(cell));
                    Assert.Equal(CellMark.None, fleet.MarkAt(cell));
                    Assert.NotEqual(ShotResult.Invalid, fleet.Fire(cell, out _));
                }

                Assert.True(fleet.AllSunk);
            }
        }
    }

    [Fact]
    public void TheDensityMapPrefersCellsThatFitTheRemainingShips()
    {
        var empty = new BroadsideFleet();
        PlaceRow(empty);
        var ai = new BroadsideAi();
        ai.BuildDensity(empty);
        Assert.True(ai.Density[BroadsideFleet.CellOf(4, 4)] > ai.Density[BroadsideFleet.CellOf(0, 0)]);

        var fleet = new BroadsideFleet();
        fleet.Place(0, 0, 9, true);
        fleet.Place(1, 5, 9, true);
        fleet.Place(2, 0, 7, true);
        fleet.Place(3, 4, 7, true);
        fleet.Place(4, 8, 0, false);
        fleet.Fire(BroadsideFleet.CellOf(8, 0), out _);
        fleet.Fire(BroadsideFleet.CellOf(8, 1), out _);
        Assert.True(fleet.IsSunk(4));
        fleet.Fire(BroadsideFleet.CellOf(2, 0), out _);
        fleet.Fire(BroadsideFleet.CellOf(0, 1), out _);
        fleet.Fire(BroadsideFleet.CellOf(1, 1), out _);

        ai.BuildDensity(fleet);
        Assert.Equal(0, ai.Density[BroadsideFleet.CellOf(0, 0)]);
        Assert.Equal(0, ai.Density[BroadsideFleet.CellOf(1, 0)]);
        Assert.True(ai.Density[BroadsideFleet.CellOf(3, 0)] > 0);
        Assert.True(ai.Density[BroadsideFleet.CellOf(4, 4)] > ai.Density[BroadsideFleet.CellOf(9, 0)]);
    }

    [Fact]
    public void TheHardAiFollowsUpAnOpenHit()
    {
        var fleet = new BroadsideFleet();
        PlaceRow(fleet);
        Assert.Equal(ShotResult.Hit, fleet.Fire(BroadsideFleet.CellOf(2, 4), out _));
        var ai = new BroadsideAi();

        for (ulong seed = 1; seed <= 8; seed++)
        {
            var random = GameRandom.FromSeed(seed);
            var cell = ai.ChooseShot(fleet, true, ref random);
            var distance = Math.Abs(BroadsideFleet.ColumnOf(cell) - 2) + Math.Abs(BroadsideFleet.RowOf(cell) - 4);
            Assert.Equal(1, distance);
        }
    }

    [Fact]
    public void TheEasyAiHuntsOnParityAndTargetsAroundAHit()
    {
        var fleet = new BroadsideFleet();
        PlaceRow(fleet);
        var ai = new BroadsideAi();
        for (ulong seed = 1; seed <= 20; seed++)
        {
            var random = GameRandom.FromSeed(seed);
            var cell = ai.ChooseShot(fleet, false, ref random);
            Assert.Equal(0, (BroadsideFleet.ColumnOf(cell) + BroadsideFleet.RowOf(cell)) % 2);
        }

        fleet.Fire(BroadsideFleet.CellOf(1, 0), out _);
        var follow = GameRandom.FromSeed(3);
        var next = ai.ChooseShot(fleet, false, ref follow);
        Assert.Equal(1, Math.Abs(BroadsideFleet.ColumnOf(next) - 1) + Math.Abs(BroadsideFleet.RowOf(next)));
    }

    private static void PlaceRow(BroadsideFleet fleet)
    {
        for (var ship = 0; ship < BroadsideFleet.ShipCount; ship++)
        {
            Assert.True(fleet.Place(ship, 0, ship * 2, true));
        }
    }

    private static BroadsideBoard PlayMatch(ulong seed, out string trace)
    {
        var board = new BroadsideBoard();
        board.Reset();
        var placement = GameRandom.FromSeed(seed);
        board.Fleet(0).AutoPlace(ref placement);
        board.Fleet(1).AutoPlace(ref placement);
        var easy = new BroadsideAi();
        var hard = new BroadsideAi();
        var random = GameRandom.FromSeed(seed ^ 0xABCDUL);
        var builder = new StringBuilder();
        for (var shot = 0; shot < ShotGuard && !board.Over; shot++)
        {
            var shooter = board.Turn;
            var target = board.Fleet(BroadsideBoard.Opponent(shooter));
            var cell = shooter == 0 ? hard.ChooseShot(target, true, ref random) : easy.ChooseShot(target, false, ref random);
            var result = board.Fire(cell, out var ship);
            builder.Append(shooter).Append(':').Append(cell).Append(':').Append((int)result).Append(':').Append(ship)
                .Append(';');
        }

        trace = builder.ToString();
        return board;
    }
}
