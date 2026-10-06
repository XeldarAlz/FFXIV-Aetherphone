using System.Text;
using Aetherphone.Apps.Games.Beat;
using Aetherphone.Apps.Games.Framework;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BeatBoardTests
{
    private const float Frame = 1f / 60f;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(1234, out var firstTrace);
        var second = Play(1234, out var secondTrace);
        Play(99, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Level, second.Level);
        Assert.Equal(first.BestCombo, second.BestCombo);
        Assert.Equal(first.Count, second.Count);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    [Fact]
    public void ANoteOnTheLineIsPerfectAndOneNearItIsGood()
    {
        var board = Empty(1);
        board.PlaceTile(2, BeatBoard.HitLine);
        Assert.Equal(BeatJudgement.Perfect, board.Tap(2));
        Assert.Equal(BeatBoard.PerfectPoints, board.Score);
        Assert.Equal(1, board.Perfects);

        board.PlaceTile(1, BeatBoard.HitLine + BeatBoard.PerfectWindow + 0.02f);
        Assert.Equal(BeatJudgement.Good, board.Tap(1));
        Assert.Equal(BeatBoard.PerfectPoints + BeatBoard.GoodPoints, board.Score);
        Assert.Equal(2, board.Hits);
        Assert.Equal(2, board.Combo.Count);
        Assert.Equal(0, board.Count);
    }

    [Fact]
    public void ATapOutsideTheWindowIsWrongAndResetsTheCombo()
    {
        var board = Empty(2);
        board.PlaceTile(0, BeatBoard.HitLine);
        Assert.Equal(BeatJudgement.Perfect, board.Tap(0));
        Assert.Equal(1, board.Combo.Count);

        board.PlaceTile(1, BeatBoard.HitLine - BeatBoard.GoodWindow - 0.05f);
        Assert.Equal(BeatJudgement.Wrong, board.Tap(1));
        Assert.Equal(0, board.Combo.Count);
        Assert.Equal(1, board.Count);
        Assert.Equal(BeatJudgement.Wrong, board.Tap(3));
    }

    [Fact]
    public void TenHitsRaiseTheLevelAndTheSpeedWithComboMultipliers()
    {
        var board = Empty(3);
        var expected = 0;
        for (var hit = 1; hit <= BeatBoard.HitsPerLevel; hit++)
        {
            board.PlaceTile(hit % BeatBoard.Lanes, BeatBoard.HitLine);
            Assert.Equal(BeatJudgement.Perfect, board.Tap(hit % BeatBoard.Lanes));
            expected += BeatBoard.PerfectPoints * ComboMeter.MultiplierFor(hit);
        }

        Assert.Equal(expected, board.Score);
        Assert.Equal(2, board.Level);
        Assert.Equal(BeatBoard.StartSpeed * BeatBoard.SpeedStep, board.Speed, 5);
        Assert.Equal(BeatBoard.HitsPerLevel, board.BestCombo);
    }

    [Fact]
    public void ThreeMissedNotesEndTheRun()
    {
        var board = Empty(4);
        for (var life = 0; life < BeatBoard.StartLives; life++)
        {
            board.PlaceTile(life, BeatBoard.MissLine - 0.01f);
            Assert.Equal(BeatJudgement.Missed, board.Step(0.05f));
            Assert.Equal(life, board.MissedLane);
            Assert.Equal(BeatBoard.StartLives - life - 1, board.Lives);
        }

        Assert.Equal(BeatState.Over, board.State);
        Assert.Equal(BeatJudgement.None, board.Tap(0));
        Assert.Equal(BeatJudgement.None, board.Step(Frame));
    }

    [Fact]
    public void TheChartNeverRepeatsALaneTwiceInARow()
    {
        var board = Seeded(5);
        for (var frame = 0; frame < 90; frame++)
        {
            board.Step(Frame);
        }

        Assert.True(board.Count >= 3);
        for (var index = 1; index < board.Count; index++)
        {
            Assert.NotEqual(board.Tile(index - 1).Lane, board.Tile(index).Lane);
        }
    }

    private static BeatBoard Seeded(int seed)
    {
        var board = new BeatBoard();
        board.Reset(GameRandom.FromSeed((ulong)seed));
        return board;
    }

    private static BeatBoard Empty(int seed)
    {
        var board = Seeded(seed);
        board.ClearTiles();
        return board;
    }

    private static BeatBoard Play(int seed, out string trace)
    {
        var board = Seeded(seed);
        var builder = new StringBuilder();
        for (var frame = 0; frame < 60 * 30 && board.State == BeatState.Playing; frame++)
        {
            var judgement = board.Step(Frame);
            if (judgement == BeatJudgement.Missed)
            {
                builder.Append('m').Append(board.MissedLane).Append(' ');
            }

            var lane = LaneInWindow(board);
            if (lane < 0)
            {
                continue;
            }

            builder.Append(lane).Append(':').Append((int)board.Tap(lane)).Append(' ');
        }

        builder.Append('|').Append(board.Score).Append('|').Append(board.Level);
        trace = builder.ToString();
        return board;
    }

    private static int LaneInWindow(BeatBoard board)
    {
        for (var index = 0; index < board.Count; index++)
        {
            var tile = board.Tile(index);
            if (MathF.Abs(tile.Y - BeatBoard.HitLine) <= BeatBoard.GoodWindow)
            {
                return tile.Lane;
            }
        }

        return -1;
    }
}
