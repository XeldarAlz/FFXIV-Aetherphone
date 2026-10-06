using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Hop;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HopBoardTests
{
    private const float Step = 1f / 60f;
    private const ulong Seed = 4096;

    [Theory]
    [InlineData(12.5f, 0.6f, 0f, 1, true)]
    [InlineData(0.2f, 0.6f, 12.5f, 1, true)]
    [InlineData(3f, 0.6f, 5f, 2, false)]
    [InlineData(4.5f, 0.6f, 3f, 2, true)]
    public void SpansOverlapAcrossTheWrapSeam(float aStart, float aLength, float bStart, int bLength, bool expected)
    {
        Assert.Equal(expected, HopBoard.SpansOverlap(aStart, aLength, bStart, bLength));
    }

    [Theory]
    [InlineData(4f, 4f, 3, true)]
    [InlineData(6.7f, 4f, 3, true)]
    [InlineData(7.9f, 4f, 3, false)]
    [InlineData(3.6f, 4f, 3, true)]
    [InlineData(3.1f, 4f, 3, false)]
    [InlineData(12.6f, 0f, 3, true)]
    public void PadSupportHonoursGripSlackAndTheWrapSeam(float hopperX, float padX, int length, bool expected)
    {
        Assert.Equal(expected, HopBoard.PadSupports(hopperX, padX, length));
    }

    [Theory]
    [InlineData(5f, 6f, 1, 1f, true)]
    [InlineData(5f, 6f, 1, -1f, false)]
    [InlineData(5f, 3.5f, 1, -1f, true)]
    [InlineData(5f, 3.5f, 1, 1f, false)]
    [InlineData(5f, 5.5f, 1, 1f, false)]
    [InlineData(5f, 8f, 1, 1f, false)]
    [InlineData(12f, 0.2f, 1, 1f, true)]
    public void ANearMissIsACarWithinOneCellOnTheSideItIsLeavingBy(float hopperX, float vehicleX, int length,
        float direction, bool expected)
    {
        Assert.Equal(expected, HopBoard.NearMiss(hopperX, vehicleX, length, direction));
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(1f, 0)]
    [InlineData(1.5f, -1)]
    [InlineData(2.1f, 1)]
    [InlineData(12f, 4)]
    public void DensCatchAFullCellEitherSide(float x, int expected)
    {
        Assert.Equal(expected, HopBoard.BayAt(x));
    }

    [Fact]
    public void BankPointsPayTheHopTheDenAndACappedTimeBonus()
    {
        Assert.Equal(300, HopBoard.BankPoints(45f));
        Assert.Equal(210, HopBoard.BankPoints(0.9f));
        Assert.Equal(230, HopBoard.BankPoints(10.4f));
    }

    [Fact]
    public void ALevelIsWorthAtMostTwoThousandFiveHundredAndFifty()
    {
        Assert.Equal(2550, HopBoard.PerLevelMaximum());
    }

    [Theory]
    [InlineData(0, 1, 2)]
    [InlineData(1, 1, 3)]
    [InlineData(0, 3, 3)]
    [InlineData(1, 9, 3)]
    public void RoadDensityRampsEveryTwoLevelsAndCapsAtThree(int lane, int level, int expected)
    {
        Assert.Equal(expected, HopBoard.RoadCountForLevel(lane, level));
    }

    [Fact]
    public void HoppingUpPaysOnlyForNewRowsAndTheFirstHopLeavesTheStartRow()
    {
        var board = new HopBoard();
        board.StartGame(GameRandom.FromSeed(Seed));
        Assert.Equal(HopBoard.StartRow, board.Row);
        board.Hop(0, 1);
        Assert.Equal(1, board.Row);
        Assert.Equal(HopBoard.HopPoints, board.Score);
        board.Hop(0, -1);
        board.Hop(0, 1);
        Assert.Equal(HopBoard.HopPoints, board.Score);
    }

    [Fact]
    public void TheTimerRunsOutIntoADeathAndRefillsOnRespawn()
    {
        var board = new HopBoard();
        board.StartGame(GameRandom.FromSeed(Seed));
        var elapsed = 0f;
        while (!board.Dying && elapsed < HopBoard.LifeTimerSeconds + 1f)
        {
            board.Tick(Step);
            elapsed += Step;
        }

        Assert.True(board.Dying);
        Assert.Equal(HopBoard.StartLives - 1, board.Lives);
        while (board.Dying)
        {
            board.Tick(Step);
        }

        Assert.Equal(HopBoard.LifeTimerSeconds, board.TimerRemaining, 1);
        Assert.Equal(HopBoard.StartRow, board.Row);
    }

    [Fact]
    public void HoppingInBehindAPassingCarFiresOneNearMissPerCar()
    {
        var board = new HopBoard();
        board.StartGame(GameRandom.FromSeed(Seed));
        var elapsed = 0f;
        while (elapsed < 20f && !CarJustPassedTheStartColumn(board))
        {
            board.Tick(Step);
            elapsed += Step;
        }

        Assert.True(CarJustPassedTheStartColumn(board));
        board.Hop(0, 1);
        var misses = 0;
        var watched = 0f;
        while (watched < 1f && !board.Dying)
        {
            board.Tick(Step);
            watched += Step;
            if (!board.NearMissThisFrame)
            {
                continue;
            }

            misses++;
            Assert.Equal(1f, board.NearMissDirection);
            Assert.InRange(board.NearMissX, 0f, HopBoard.Columns);
        }

        Assert.False(board.Dying);
        Assert.Equal(1, misses);
    }

    private static bool CarJustPassedTheStartColumn(HopBoard board)
    {
        for (var index = 0; index < board.RoadCount(0); index++)
        {
            var vehicle = board.RoadEntity(0, index);
            if (HopBoard.NearMiss(6f, vehicle.X, vehicle.Length, 1f))
            {
                return true;
            }
        }

        return false;
    }

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = new HopBoard();
        var second = new HopBoard();
        first.StartGame(GameRandom.FromSeed(Seed));
        second.StartGame(GameRandom.FromSeed(Seed));
        var elapsed = 0f;
        var nextHop = 0.5f;
        var hop = 0;
        while (elapsed < 120f && !first.GameOver)
        {
            if (elapsed >= nextHop)
            {
                var sideways = hop % 4 == 3 ? (hop % 8 == 3 ? 1 : -1) : 0;
                var upward = sideways == 0 ? 1 : 0;
                first.Hop(sideways, upward);
                second.Hop(sideways, upward);
                hop++;
                nextHop += 0.5f;
            }

            first.Tick(Step);
            second.Tick(Step);
            elapsed += Step;
            Assert.Equal(first.Score, second.Score);
            Assert.Equal(first.Lives, second.Lives);
            Assert.Equal(first.Level, second.Level);
            Assert.Equal(first.Row, second.Row);
            Assert.Equal(first.X, second.X);
            Assert.Equal(first.TimerRemaining, second.TimerRemaining);
            Assert.Equal(first.Dying, second.Dying);
            Assert.Equal(first.NearMissThisFrame, second.NearMissThisFrame);
            Assert.Equal(first.BankedTotal, second.BankedTotal);
            for (var lane = 0; lane < HopBoard.LaneCount; lane++)
            {
                for (var index = 0; index < first.RoadCount(lane); index++)
                {
                    Assert.Equal(first.RoadEntity(lane, index).X, second.RoadEntity(lane, index).X);
                }

                for (var index = 0; index < first.PadCount(lane); index++)
                {
                    Assert.Equal(first.Pad(lane, index).X, second.Pad(lane, index).X);
                }
            }
        }

        Assert.True(first.Score > 0);
        Assert.Equal(first.LastBankPoints, second.LastBankPoints);
    }
}
