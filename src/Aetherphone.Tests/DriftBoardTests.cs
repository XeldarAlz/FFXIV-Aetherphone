using System.Globalization;
using System.Numerics;
using System.Text;
using Aetherphone.Apps.Games.Drift;
using Aetherphone.Apps.Games.Framework;
using Xunit;

namespace Aetherphone.Tests;

public sealed class DriftBoardTests
{
    private const float Frame = 1f / 60f;
    private static readonly DriftControls Idle = new(0f, false, false, false);
    private static readonly DriftControls FireOnly = new(0f, false, true, false);

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(4242, out var firstTrace);
        var second = Play(4242, out var secondTrace);
        Play(77, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.RockCount, second.RockCount);
        Assert.Equal(first.ShipPosition, second.ShipPosition);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    [Fact]
    public void WrapKeepsEveryCoordinateInsideTheWorld()
    {
        Assert.Equal(99f, DriftBoard.Wrap(-1f, 100f), 4);
        Assert.Equal(0f, DriftBoard.Wrap(100f, 100f), 4);
        Assert.Equal(50f, DriftBoard.Wrap(250f, 100f), 4);
        var wrapped = DriftBoard.WrapPosition(new Vector2(-2f, DriftBoard.Height + 4f));
        Assert.Equal(DriftBoard.Width - 2f, wrapped.X, 4);
        Assert.Equal(4f, wrapped.Y, 4);
    }

    [Fact]
    public void WrapDeltaTakesTheShortWayAcrossTheSeam()
    {
        Assert.Equal(new Vector2(2f, 0f), DriftBoard.WrapDelta(new Vector2(99f, 10f), new Vector2(1f, 10f)));
        Assert.Equal(new Vector2(-2f, 0f), DriftBoard.WrapDelta(new Vector2(1f, 10f), new Vector2(99f, 10f)));
        var vertical = DriftBoard.WrapDelta(new Vector2(50f, 2f), new Vector2(50f, DriftBoard.Height - 2f));
        Assert.Equal(-4f, vertical.Y, 4);
        Assert.Equal(new Vector2(10f, -5f), DriftBoard.WrapDelta(new Vector2(40f, 40f), new Vector2(50f, 35f)));
    }

    [Fact]
    public void ARockLeavingOneEdgeComesBackOnTheOpposite()
    {
        var board = Fresh(1);
        board.ClearRocks();
        board.SpawnRock(new Vector2(DriftBoard.Width - 1f, 20f), new Vector2(20f, 0f), RockSize.Small);

        board.Step(0.1f, Idle);
        board.Step(0.1f, Idle);

        Assert.Equal(1, board.RockCount);
        Assert.Equal(3f, board.RockAt(0).Position.X, 3);
        Assert.Equal(20f, board.RockAt(0).Position.Y, 3);
    }

    [Fact]
    public void ALargeRockSplitsIntoTwoMediumsWorthTwentyPoints()
    {
        var board = Fresh(2);
        board.ClearRocks();
        board.SpawnRock(new Vector2(DriftBoard.Width * 0.5f, 40f), Vector2.Zero, RockSize.Large);

        ShootUntilBreak(board);

        Assert.Equal(2, board.RockCount);
        Assert.Equal(RockSize.Medium, board.RockAt(0).Size);
        Assert.Equal(RockSize.Medium, board.RockAt(1).Size);
        Assert.Equal(DriftBoard.RockPoints(RockSize.Large), board.Score);
        Assert.Equal(1, board.RocksBroken);
        Assert.Equal(1, board.ShotsHit);
    }

    [Fact]
    public void AMediumRockSplitsIntoTwoSmallsAndASmallRockVanishes()
    {
        var board = Fresh(3);
        board.ClearRocks();
        board.SpawnRock(new Vector2(DriftBoard.Width * 0.5f, 40f), Vector2.Zero, RockSize.Medium);
        ShootUntilBreak(board);
        Assert.Equal(2, board.RockCount);
        Assert.Equal(RockSize.Small, board.RockAt(0).Size);
        Assert.Equal(RockSize.Small, board.RockAt(1).Size);

        board.ClearRocks();
        board.SpawnRock(new Vector2(DriftBoard.Width * 0.5f, 40f), Vector2.Zero, RockSize.Small);
        Run(board, Idle, DriftBoard.FireCooldown);
        ShootUntilBreak(board);
        Assert.Equal(0, board.RockCount);
    }

    [Fact]
    public void SmallerRocksPayMore()
    {
        Assert.True(DriftBoard.RockPoints(RockSize.Small) > DriftBoard.RockPoints(RockSize.Medium));
        Assert.True(DriftBoard.RockPoints(RockSize.Medium) > DriftBoard.RockPoints(RockSize.Large));
        Assert.True(DriftBoard.RockRadius(RockSize.Small) < DriftBoard.RockRadius(RockSize.Medium));
        Assert.True(DriftBoard.RockRadius(RockSize.Medium) < DriftBoard.RockRadius(RockSize.Large));
    }

    [Fact]
    public void ClearingEveryRockStartsTheNextWaveAfterADelay()
    {
        var board = Fresh(4);
        board.ClearRocks();
        board.Step(Frame, Idle);
        Assert.True(board.WaveClearedThisFrame);
        Assert.Equal(1, board.Wave);

        Run(board, Idle, DriftBoard.WaveDelay + 0.05f);

        Assert.Equal(2, board.Wave);
        Assert.Equal(DriftBoard.RocksForWave(2), board.RockCount);
    }

    [Fact]
    public void SaucersOnlyComeFromWaveThree()
    {
        Assert.False(DriftBoard.SaucerAllowed(1));
        Assert.False(DriftBoard.SaucerAllowed(2));
        Assert.True(DriftBoard.SaucerAllowed(3));
        Assert.Equal(0f, DriftBoard.SmallSaucerChance(3));
        Assert.Equal(0.2f, DriftBoard.SmallSaucerChance(5), 4);
        Assert.Equal(0.8f, DriftBoard.SmallSaucerChance(30), 4);
        Assert.True(DriftBoard.FirstSaucerDelay(3) > DriftBoard.FirstSaucerDelay(6));
        Assert.Equal(6f, DriftBoard.FirstSaucerDelay(40));
    }

    [Fact]
    public void NoSaucerAppearsInTheFirstTwoWaves()
    {
        var board = new DriftBoard();
        board.Reset(GameRandom.FromSeed(5), 2);

        for (var frame = 0; frame < 60 * 30; frame++)
        {
            board.Step(Frame, Idle);
            Assert.False(board.SaucerActive);
        }
    }

    [Fact]
    public void TheFirstSaucerOfWaveThreeArrivesOnSchedule()
    {
        var board = new DriftBoard();
        board.Reset(GameRandom.FromSeed(6), 3);
        var elapsed = 0f;
        while (!board.SaucerActive && elapsed < 30f && !board.GameOver)
        {
            board.Step(Frame, Idle);
            elapsed += Frame;
        }

        Assert.True(board.SaucerActive);
        Assert.InRange(elapsed, DriftBoard.FirstSaucerDelay(3) - 0.02f, DriftBoard.FirstSaucerDelay(3) + 0.05f);
    }

    [Fact]
    public void ARockCostsALifeAndTheShipRespawnsShielded()
    {
        var board = Fresh(7);
        Run(board, Idle, DriftBoard.InvulnerableSeconds + 0.05f);
        board.ClearRocks();
        board.SpawnRock(board.ShipPosition, Vector2.Zero, RockSize.Large);

        board.Step(Frame, Idle);

        Assert.True(board.ShipLostThisFrame);
        Assert.Equal(DriftBoard.StartLives - 1, board.Lives);
        Assert.Equal(ShipState.Wrecked, board.State);
        var waited = 0f;
        while (board.State != ShipState.Flying && waited < 6f)
        {
            board.Step(Frame, Idle);
            waited += Frame;
        }

        Assert.Equal(ShipState.Flying, board.State);
        Assert.True(board.Invulnerable);
        Assert.Equal(Vector2.Zero, board.ShipVelocity);
    }

    [Fact]
    public void WarpingMovesTheShipAfterADelayAndThenCoolsDown()
    {
        var board = Fresh(8);
        var start = board.ShipPosition;
        board.Step(Frame, new DriftControls(0f, false, false, true));
        Assert.Equal(ShipState.Warping, board.State);
        Assert.True(board.WarpStartedThisFrame);

        Run(board, Idle, DriftBoard.WarpSeconds + 0.05f);

        Assert.Equal(ShipState.Flying, board.State);
        Assert.Equal(board.WarpTarget, board.ShipPosition);
        Assert.NotEqual(start, board.ShipPosition);
        board.Step(Frame, new DriftControls(0f, false, false, true));
        Assert.Equal(ShipState.Flying, board.State);
    }

    [Fact]
    public void LosingTheLastShipEndsTheGame()
    {
        var board = Fresh(9);
        for (var life = 0; life < DriftBoard.StartLives; life++)
        {
            for (var frame = 0; frame < 600 && (board.State != ShipState.Flying || board.Invulnerable); frame++)
            {
                board.Step(Frame, Idle);
            }

            board.ClearRocks();
            board.SpawnRock(board.ShipPosition, Vector2.Zero, RockSize.Small);
            board.Step(Frame, Idle);
            Assert.True(board.ShipLostThisFrame);
        }

        Run(board, Idle, DriftBoard.WreckSeconds + 0.1f);

        Assert.True(board.GameOver);
        Assert.Equal(0, board.Lives);
    }

    private static DriftBoard Fresh(ulong seed)
    {
        var board = new DriftBoard();
        board.Reset(GameRandom.FromSeed(seed));
        return board;
    }

    private static void Run(DriftBoard board, in DriftControls controls, float seconds)
    {
        var frames = (int)MathF.Ceiling(seconds / Frame);
        for (var frame = 0; frame < frames; frame++)
        {
            board.Step(Frame, controls);
        }
    }

    private static void ShootUntilBreak(DriftBoard board)
    {
        board.Step(Frame, FireOnly);
        Assert.Equal(1, board.FiredCount);
        for (var frame = 0; frame < 120; frame++)
        {
            board.Step(Frame, Idle);
            if (board.BreakCount > 0)
            {
                return;
            }
        }

        Assert.Fail("The shot never reached the rock.");
    }

    private static DriftBoard Play(ulong seed, out string trace)
    {
        var board = Fresh(seed);
        var builder = new StringBuilder();
        for (var frame = 0; frame < 60 * 40 && !board.GameOver; frame++)
        {
            var turn = (frame / 40 % 3) - 1;
            var controls = new DriftControls(turn, frame / 25 % 4 == 0, true, frame == 300);
            board.Step(Frame, controls);
            if (frame % 30 != 0)
            {
                continue;
            }

            builder.Append(board.Score).Append(',').Append(board.RockCount).Append(',').Append(board.Lives)
                .Append(',').Append(board.Wave).Append(',')
                .Append(board.ShipPosition.X.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                .Append(board.ShipPosition.Y.ToString("F3", CultureInfo.InvariantCulture)).Append(';');
        }

        trace = builder.ToString();
        return board;
    }
}
