using System.Text;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Twenty48;
using Xunit;

namespace Aetherphone.Tests;

public sealed class Twenty48BoardTests
{
    private const int ScriptedMoves = 40;
    private static readonly SwipeDirection[] Script =
    {
        SwipeDirection.Left, SwipeDirection.Up, SwipeDirection.Right, SwipeDirection.Down, SwipeDirection.Left,
        SwipeDirection.Left, SwipeDirection.Up, SwipeDirection.Right,
    };

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(1234, out var firstTrace);
        var second = Play(1234, out var secondTrace);
        Play(99, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.MaxTile, second.MaxTile);
        Assert.Equal(first.Moves, second.Moves);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    [Fact]
    public void ARowOfFourEqualTilesMergesIntoTwoPairs()
    {
        var board = new Twenty48Board();
        board.SetCell(0, 2);
        board.SetCell(1, 2);
        board.SetCell(2, 2);
        board.SetCell(3, 2);

        Assert.True(board.TryMove(SwipeDirection.Left));

        Assert.Equal(4, board.Value(0));
        Assert.Equal(4, board.Value(1));
        Assert.True(board.Merged(0));
        Assert.True(board.Merged(1));
        Assert.Equal(8, board.Score);
        Assert.Equal(4, board.LastMergeMax);
        Assert.Equal(4, board.MaxTile);
        Assert.Equal(1, board.Moves);
        Assert.True(board.SpawnIndex >= 0);
    }

    [Fact]
    public void AMergedTileNeverMergesTwiceInOneMove()
    {
        var board = new Twenty48Board();
        board.SetCell(0, 4);
        board.SetCell(1, 2);
        board.SetCell(2, 2);

        Assert.True(board.TryMove(SwipeDirection.Left));

        Assert.Equal(4, board.Value(0));
        Assert.Equal(4, board.Value(1));
        Assert.Equal(4, board.Score);
        Assert.False(board.Merged(0));
        Assert.True(board.Merged(1));
    }

    [Fact]
    public void TilesMergeTowardTheSwipeEdge()
    {
        var board = new Twenty48Board();
        board.SetCell(0, 2);
        board.SetCell(1, 2);
        board.SetCell(2, 2);

        Assert.True(board.TryMove(SwipeDirection.Right));

        Assert.Equal(4, board.Value(3));
        Assert.Equal(2, board.Value(2));
        Assert.Equal(4, board.Score);
        Assert.Equal(0, board.SlideFrom(2));
    }

    [Fact]
    public void AMoveThatChangesNothingIsRejectedWithoutASpawn()
    {
        var board = new Twenty48Board();
        board.SetCell(0, 2);
        board.SetCell(1, 4);

        Assert.False(board.TryMove(SwipeDirection.Left));

        Assert.Equal(0, board.Moves);
        Assert.Equal(-1, board.SpawnIndex);
        Assert.Equal(0, board.Score);
        Assert.False(board.CanUndo);
    }

    [Fact]
    public void CanMoveIsFalseOnlyWhenTheBoardIsFullWithoutEqualNeighbours()
    {
        var board = new Twenty48Board();
        for (var index = 0; index < Twenty48Board.CellCount; index++)
        {
            var row = index / Twenty48Board.Size;
            var column = index % Twenty48Board.Size;
            board.SetCell(index, (row + column) % 2 == 0 ? 2 : 4);
        }

        Assert.False(board.CanMove());

        board.SetCell(1, 2);

        Assert.True(board.CanMove());
    }

    [Fact]
    public void UndoRestoresTheBoardExactlyOnce()
    {
        var board = new Twenty48Board();
        board.Reset(GameRandom.FromSeed(5));
        var snapshot = Snapshot(board);
        var scoreBefore = board.Score;
        var maxTileBefore = board.MaxTile;
        var direction = FirstMovingDirection(board);
        Assert.True(board.CanUndo);
        Assert.Equal(1, board.Moves);

        Assert.True(board.Undo());

        Assert.Equal(snapshot, Snapshot(board));
        Assert.Equal(scoreBefore, board.Score);
        Assert.Equal(maxTileBefore, board.MaxTile);
        Assert.Equal(0, board.Moves);
        Assert.Equal(-1, board.SpawnIndex);
        Assert.False(board.CanUndo);
        Assert.False(board.Undo());

        Assert.True(board.TryMove(direction));

        Assert.False(board.CanUndo);
        Assert.False(board.Undo());
        Assert.Equal(0, board.UndoLeft);
    }

    [Fact]
    public void UndoReplaysTheSameSpawnOnTheNextMove()
    {
        var board = new Twenty48Board();
        board.Reset(GameRandom.FromSeed(7));
        var direction = FirstMovingDirection(board);
        var firstOutcome = Snapshot(board);

        Assert.True(board.Undo());
        Assert.True(board.TryMove(direction));

        Assert.Equal(firstOutcome, Snapshot(board));
    }

    [Fact]
    public void ResetRestoresTheUndoCharge()
    {
        var board = new Twenty48Board();
        board.Reset(GameRandom.FromSeed(3));
        FirstMovingDirection(board);
        Assert.True(board.Undo());

        board.Reset(GameRandom.FromSeed(3));
        FirstMovingDirection(board);

        Assert.True(board.CanUndo);
        Assert.Equal(Twenty48Board.UndoCharges, board.UndoLeft);
    }

    private static SwipeDirection FirstMovingDirection(Twenty48Board board)
    {
        for (var direction = 0; direction < 4; direction++)
        {
            if (board.TryMove((SwipeDirection)direction))
            {
                return (SwipeDirection)direction;
            }
        }

        throw new InvalidOperationException("A fresh board always has a legal move.");
    }

    private static Twenty48Board Play(ulong seed, out string trace)
    {
        var board = new Twenty48Board();
        board.Reset(GameRandom.FromSeed(seed));
        var builder = new StringBuilder();
        for (var step = 0; step < ScriptedMoves; step++)
        {
            board.TryMove(Script[step % Script.Length]);
            builder.Append(Snapshot(board)).Append('|');
        }

        trace = builder.ToString();
        return board;
    }

    private static string Snapshot(Twenty48Board board)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < Twenty48Board.CellCount; index++)
        {
            builder.Append(board.Value(index)).Append(',');
        }

        builder.Append(board.Score);
        return builder.ToString();
    }
}
