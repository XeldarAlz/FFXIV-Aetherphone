using System.Globalization;
using System.Numerics;
using System.Text;
using Aetherphone.Apps.Games.Claim;
using Aetherphone.Apps.Games.Framework;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ClaimBoardTests
{
    private const float Frame = 1f / 60f;
    private const int Middle = ClaimBoard.Width / 2;
    private const int Bottom = ClaimBoard.Height - 1;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(2024, out var firstTrace);
        var second = Play(2024, out var secondTrace);
        Play(77, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.ClaimedInterior, second.ClaimedInterior);
        Assert.True(first.Claims > 0);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    [Fact]
    public void AClosedLineFillsTheSideWithoutTheBoss()
    {
        var board = Quiet(1);
        board.PlaceBoss(new Vector2(30f, 120f), new Vector2(40f, 125f), true);

        CutColumn(board, ClaimMove.Up, ClaimDraw.Fast);

        Assert.Equal(1, board.Claims);
        Assert.False(board.Drawing);
        Assert.Equal(ClaimCell.Claimed, board.CellAt(120, 120));
        Assert.Equal(ClaimCell.Claimed, board.CellAt(Middle, 120));
        Assert.Equal(ClaimCell.Open, board.CellAt(35, 120));
        Assert.Equal(ClaimCell.Open, board.CellAt(Middle - 1, 120));
        Assert.Equal(50, board.Percent);
    }

    [Fact]
    public void TheFillFollowsTheBossToTheOtherSide()
    {
        var board = Quiet(2);
        board.PlaceBoss(new Vector2(120f, 100f), new Vector2(130f, 105f), true);

        CutColumn(board, ClaimMove.Up, ClaimDraw.Fast);

        Assert.Equal(ClaimCell.Claimed, board.CellAt(35, 120));
        Assert.Equal(ClaimCell.Open, board.CellAt(125, 102));
        Assert.Equal(ClaimCell.Open, board.CellAt(Middle + 1, 120));
    }

    [Fact]
    public void PercentageCountsOnlyTheInterior()
    {
        var half = (ClaimBoard.Height - 2) * (ClaimBoard.Width / 2 - 1) + (ClaimBoard.Height - 2);

        Assert.Equal(100, ClaimBoard.PercentOf(ClaimBoard.InteriorCells));
        Assert.Equal(0, ClaimBoard.PercentOf(0));
        Assert.Equal(50, ClaimBoard.PercentOf(half));
        Assert.Equal(74, ClaimBoard.PercentOf(ClaimBoard.InteriorCells * 3 / 4 - 1));
        Assert.Equal(75, ClaimBoard.PercentOf((ClaimBoard.InteriorCells * 3 + 3) / 4));
    }

    [Fact]
    public void SlowLinesScoreDouble()
    {
        var fast = Quiet(3);
        fast.PlaceBoss(new Vector2(30f, 120f), new Vector2(40f, 125f), true);
        CutColumn(fast, ClaimMove.Up, ClaimDraw.Fast);
        var slow = Quiet(3);
        slow.PlaceBoss(new Vector2(30f, 120f), new Vector2(40f, 125f), true);
        CutColumn(slow, ClaimMove.Up, ClaimDraw.Slow);

        Assert.Equal(fast.ClaimedInterior, slow.ClaimedInterior);
        Assert.Equal(1, slow.SlowLines);
        Assert.Equal(0, fast.SlowLines);
        Assert.InRange(slow.Score, fast.Score * 2 - 1, fast.Score * 2 + 1);
    }

    [Fact]
    public void ASingleFastStepMakesTheLineFast()
    {
        var board = Quiet(4);
        board.PlaceBoss(new Vector2(30f, 120f), new Vector2(40f, 125f), true);
        Hold(board, ClaimMove.Up, ClaimDraw.Slow, 0.5f);
        Hold(board, ClaimMove.Up, ClaimDraw.Fast, 0.1f);

        RunUntil(board, ClaimMove.Up, ClaimDraw.Slow, () => !board.Drawing, 30f);

        Assert.Equal(1, board.Claims);
        Assert.Equal(0, board.SlowLines);
    }

    [Fact]
    public void ThePlayerCannotLeaveTheBorderWithoutDrawing()
    {
        var board = Quiet(5);

        Hold(board, ClaimMove.Up, ClaimDraw.None, 1f);

        Assert.False(board.Drawing);
        Assert.Equal(ClaimBoard.Index(Middle, Bottom), board.PlayerCell);
    }

    [Fact]
    public void TheBorderCanBeWalked()
    {
        var board = Quiet(6);

        Hold(board, ClaimMove.Left, ClaimDraw.None, 0.5f);

        Assert.True(ClaimBoard.ColumnOf(board.PlayerCell) < Middle - 10);
        Assert.Equal(Bottom, ClaimBoard.RowOf(board.PlayerCell));
        Assert.True(board.IsWalkable(board.PlayerCell));
    }

    [Fact]
    public void ALineCannotTouchItself()
    {
        var board = Quiet(7);
        board.PlaceBoss(new Vector2(30f, 60f), new Vector2(40f, 65f), true);
        RunUntil(board, ClaimMove.Up, ClaimDraw.Fast, () => board.Trail.Length >= 10, 5f);
        RunUntil(board, ClaimMove.Right, ClaimDraw.Fast, () => board.Trail.Length >= 11, 5f);
        var length = board.Trail.Length;
        var cell = board.PlayerCell;

        Hold(board, ClaimMove.Down, ClaimDraw.Fast, 0.2f);
        Hold(board, ClaimMove.Left, ClaimDraw.Fast, 0.2f);

        Assert.Equal(length, board.Trail.Length);
        Assert.Equal(cell, board.PlayerCell);
        Assert.True(board.Drawing);
    }

    [Fact]
    public void TheBossTouchingAnUnfinishedLineCostsALife()
    {
        var board = Quiet(8);
        board.PlaceBoss(new Vector2(20f, 60f), new Vector2(30f, 60f), true);
        RunUntil(board, ClaimMove.Up, ClaimDraw.Fast, () => board.Trail.Length >= 30, 5f);
        var start = board.TrailStart;

        board.PlaceBoss(new Vector2(Middle - 10f, Bottom - 15.5f), new Vector2(Middle + 10f, Bottom - 15.5f), true);
        board.Step(Frame, ClaimMove.Up, ClaimDraw.Fast);

        Assert.Equal(ClaimDeath.Boss, board.DeathThisStep);
        Assert.Equal(ClaimState.Dying, board.State);
        Assert.Equal(ClaimBoard.StartLives - 1, board.Lives);

        board.PlaceBoss(new Vector2(20f, 60f), new Vector2(30f, 60f), true);
        RunUntil(board, ClaimMove.None, ClaimDraw.None, () => board.State == ClaimState.Playing, 3f);

        Assert.False(board.Drawing);
        Assert.Equal(start, board.PlayerCell);
        Assert.Equal(ClaimCell.Open, board.CellAt(Middle, Bottom - 10));
        Assert.True(board.Shielded);
    }

    [Fact]
    public void AStoppedLineLightsAFuseThatBurnsToThePlayer()
    {
        var board = Quiet(9);
        board.PlaceBoss(new Vector2(20f, 60f), new Vector2(30f, 60f), true);
        RunUntil(board, ClaimMove.Up, ClaimDraw.Fast, () => board.Trail.Length >= 18, 5f);

        Hold(board, ClaimMove.None, ClaimDraw.Fast, ClaimBoard.FuseDelay + 0.05f);
        Assert.True(board.FuseLit);
        Assert.Equal(ClaimState.Playing, board.State);

        var burn = board.Trail.Length / ClaimBoard.FuseSpeed;
        var died = RunUntil(board, ClaimMove.None, ClaimDraw.Fast, () => board.State == ClaimState.Dying, burn + 0.3f);

        Assert.True(died);
        Assert.Equal(ClaimBoard.StartLives - 1, board.Lives);
    }

    [Fact]
    public void MovingAgainSnuffsTheFuse()
    {
        var board = Quiet(10);
        board.PlaceBoss(new Vector2(20f, 60f), new Vector2(30f, 60f), true);
        RunUntil(board, ClaimMove.Up, ClaimDraw.Fast, () => board.Trail.Length >= 40, 5f);
        Hold(board, ClaimMove.None, ClaimDraw.Fast, ClaimBoard.FuseDelay + 0.1f);
        Assert.True(board.FuseLit);

        Hold(board, ClaimMove.Up, ClaimDraw.Fast, ClaimBoard.FuseSnuffSeconds + 0.1f);

        Assert.False(board.FuseLit);
        Assert.Equal(ClaimState.Playing, board.State);
    }

    [Fact]
    public void ASparkOnTheBorderCatchesThePlayer()
    {
        var board = Quiet(11);
        board.EndShield();
        board.PlaceSpark(Middle + 6, Bottom, ClaimMove.Left);

        var caught = RunUntil(board, ClaimMove.None, ClaimDraw.None, () => board.State == ClaimState.Dying, 1f);

        Assert.True(caught);
        Assert.Equal(ClaimDeath.Spark, board.DeathThisStep);
        Assert.Equal(ClaimBoard.StartLives - 1, board.Lives);
    }

    [Fact]
    public void SparksOnlyEverWalkTheBorder()
    {
        var board = new ClaimBoard();
        board.Reset(GameRandom.FromSeed(12));
        var pilot = ClaimPilot.Create(13);
        for (var frame = 0; frame < 60 * 30; frame++)
        {
            pilot.Decide(board, Frame, out var move, out var draw);
            board.Step(Frame, move, draw);
            if (board.State != ClaimState.Playing)
            {
                continue;
            }

            for (var spark = 0; spark < board.SparkCount; spark++)
            {
                var position = board.SparkPosition(spark);
                Assert.True(board.IsWalkable(ClaimBoard.Index((int)position.X, (int)position.Y)));
            }

            AssertBossInOpenField(board);
        }
    }

    [Fact]
    public void ReachingSeventyFivePercentClearsTheLevelWithABonus()
    {
        var board = Quiet(14);
        board.PlaceBoss(new Vector2(15f, 120f), new Vector2(20f, 125f), true);
        CutColumn(board, ClaimMove.Up, ClaimDraw.Fast);
        RunUntil(board, ClaimMove.Left, ClaimDraw.None, () => ClaimBoard.ColumnOf(board.PlayerCell) <= 40, 5f);
        var scoreBefore = board.Score;

        var cleared = RunUntil(board, ClaimMove.Down, ClaimDraw.Fast, () => board.State == ClaimState.Cleared, 8f);

        Assert.True(cleared);
        Assert.True(board.Percent >= ClaimBoard.TargetPercent);
        Assert.True(board.Score - scoreBefore >= ClaimBoard.ClearBonusPerLevel);
        var speed = board.BossSpeed;

        RunUntil(board, ClaimMove.None, ClaimDraw.None, () => board.State == ClaimState.Playing,
            ClaimBoard.ClearSeconds + 0.5f);

        Assert.Equal(2, board.Level);
        Assert.Equal(0, board.Percent);
        Assert.Equal(ClaimBoard.SparkCountFor(2), board.SparkCount);
        Assert.True(board.BossSpeed > speed);
    }

    [Fact]
    public void LevelsRaiseTheSparkCount()
    {
        Assert.Equal(1, ClaimBoard.SparkCountFor(1));
        Assert.Equal(2, ClaimBoard.SparkCountFor(2));
        Assert.True(ClaimBoard.SparkCountFor(6) > ClaimBoard.SparkCountFor(3));
        Assert.Equal(ClaimBoard.MaxSparks, ClaimBoard.SparkCountFor(40));
    }

    [Fact]
    public void LosingTheLastLifeEndsTheRun()
    {
        var board = Quiet(15);
        for (var life = 0; life < ClaimBoard.StartLives; life++)
        {
            board.EndShield();
            board.PlaceSpark(ClaimBoard.ColumnOf(board.PlayerCell) + 3, ClaimBoard.RowOf(board.PlayerCell),
                ClaimMove.Left);
            RunUntil(board, ClaimMove.None, ClaimDraw.None, () => board.State != ClaimState.Playing, 1f);
            RunUntil(board, ClaimMove.None, ClaimDraw.None, () => board.State != ClaimState.Dying, 2f);
            board.RemoveSparks();
        }

        Assert.Equal(0, board.Lives);
        Assert.Equal(ClaimState.Over, board.State);
    }

    private static ClaimBoard Quiet(ulong seed)
    {
        var board = new ClaimBoard();
        board.Reset(GameRandom.FromSeed(seed));
        board.RemoveSparks();
        return board;
    }

    private static void CutColumn(ClaimBoard board, ClaimMove move, ClaimDraw draw)
    {
        var claims = board.Claims;
        var done = RunUntil(board, move, draw, () => board.Claims > claims, 30f);
        Assert.True(done);
    }

    private static void Hold(ClaimBoard board, ClaimMove move, ClaimDraw draw, float seconds)
    {
        var frames = (int)MathF.Ceiling(seconds / Frame);
        for (var frame = 0; frame < frames; frame++)
        {
            board.Step(Frame, move, draw);
        }
    }

    private static bool RunUntil(ClaimBoard board, ClaimMove move, ClaimDraw draw, Func<bool> done, float seconds)
    {
        var frames = (int)MathF.Ceiling(seconds / Frame);
        for (var frame = 0; frame < frames; frame++)
        {
            board.Step(Frame, move, draw);
            if (done())
            {
                return true;
            }
        }

        return done();
    }

    private static void AssertBossInOpenField(ClaimBoard board)
    {
        var head = board.BossHead;
        var tail = board.BossTail;
        Assert.NotEqual(ClaimCell.Claimed, board.CellAt((int)head.X, (int)head.Y));
        Assert.NotEqual(ClaimCell.Claimed, board.CellAt((int)tail.X, (int)tail.Y));
    }

    private static ClaimBoard Play(ulong seed, out string trace)
    {
        var board = new ClaimBoard();
        board.Reset(GameRandom.FromSeed(seed));
        var pilot = ClaimPilot.Create(seed ^ 0x5A5Au);
        var builder = new StringBuilder();
        for (var frame = 0; frame < 60 * 40; frame++)
        {
            pilot.Decide(board, Frame, out var move, out var draw);
            board.Step(Frame, move, draw);
            if (frame % 30 != 0)
            {
                continue;
            }

            builder.Append(board.Score).Append(',').Append(board.ClaimedInterior).Append(',')
                .Append(board.Lives).Append(',').Append(board.PlayerCell).Append(',')
                .Append(board.BossHead.X.ToString("0.000", CultureInfo.InvariantCulture))
                .Append(';');
        }

        trace = builder.ToString();
        return board;
    }
}
