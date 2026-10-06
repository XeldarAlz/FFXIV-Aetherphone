using System.Diagnostics;
using Aetherphone.Apps.Games.Reversi;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ReversiBoardTests
{
    private const int Plies = 16;
    private const int SearchDepth = 3;
    private const int HardDepth = 6;
    private const int D3 = 2 * ReversiBoard.Size + 3;
    private const int D4 = 3 * ReversiBoard.Size + 3;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = new ReversiBoard();
        var second = new ReversiBoard();
        first.Reset();
        second.Reset();
        var player = ReversiBoard.Dark;
        var plies = 0;
        while (plies < Plies)
        {
            if (!first.HasAnyMove(player))
            {
                player = ReversiBoard.Opponent(player);
                if (!first.HasAnyMove(player))
                {
                    break;
                }
            }

            var move = first.BestMove(player, SearchDepth);
            Assert.Equal(move, second.BestMove(player, SearchDepth));
            Assert.True(first.ApplyMove(move, player, new int[ReversiBoard.MaxFlips]) > 0);
            Assert.True(second.ApplyMove(move, player, new int[ReversiBoard.MaxFlips]) > 0);
            player = ReversiBoard.Opponent(player);
            plies++;
        }

        Assert.True(plies > 0);
        for (var cell = 0; cell < ReversiBoard.CellCount; cell++)
        {
            Assert.Equal(first.Cell(cell), second.Cell(cell));
        }
    }

    [Fact]
    public void TheSearchLeavesThePositionUntouched()
    {
        var board = new ReversiBoard();
        board.Reset();
        var before = Snapshot(board);

        var move = board.BestMove(ReversiBoard.Light, HardDepth);

        Assert.True(move >= 0);
        Assert.True(board.IsLegal(move, ReversiBoard.Light));
        Assert.Equal(before, Snapshot(board));
    }

    [Fact]
    public void ApplyMoveFlipsTheOutflankedRun()
    {
        var board = new ReversiBoard();
        board.Reset();
        var flipped = new int[ReversiBoard.MaxFlips];

        Assert.True(board.IsLegal(D3, ReversiBoard.Dark));
        Assert.Equal(1, board.ApplyMove(D3, ReversiBoard.Dark, flipped));
        Assert.Equal(D4, flipped[0]);
        Assert.Equal(ReversiBoard.Dark, board.Cell(D3));
        Assert.Equal(ReversiBoard.Dark, board.Cell(D4));
        board.Counts(out var dark, out var light);
        Assert.Equal(4, dark);
        Assert.Equal(1, light);
    }

    [Fact]
    public void AnOccupiedOrNonFlippingCellIsRejected()
    {
        var board = new ReversiBoard();
        board.Reset();
        var flipped = new int[ReversiBoard.MaxFlips];

        Assert.Equal(0, board.ApplyMove(D4, ReversiBoard.Dark, flipped));
        Assert.Equal(0, board.ApplyMove(0, ReversiBoard.Dark, flipped));
        Assert.Equal(4UL, PopCount(board.LegalMask(ReversiBoard.Dark)));
    }

    [Fact]
    public void TheHardDepthAnswersTheOpeningQuickly()
    {
        var board = new ReversiBoard();
        board.Reset();
        var flipped = new int[ReversiBoard.MaxFlips];
        board.ApplyMove(D3, ReversiBoard.Dark, flipped);
        var stopwatch = Stopwatch.StartNew();

        var move = board.BestMove(ReversiBoard.Light, HardDepth);

        stopwatch.Stop();
        Assert.True(board.IsLegal(move, ReversiBoard.Light));
        Assert.True(stopwatch.ElapsedMilliseconds < 2000, $"depth {HardDepth} took {stopwatch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void CopyFromMirrorsEveryCell()
    {
        var source = new ReversiBoard();
        var copy = new ReversiBoard();
        source.Reset();
        source.ApplyMove(D3, ReversiBoard.Dark, new int[ReversiBoard.MaxFlips]);

        copy.CopyFrom(source);

        Assert.Equal(Snapshot(source), Snapshot(copy));
    }

    private static ulong PopCount(ulong mask) => (ulong)System.Numerics.BitOperations.PopCount(mask);

    private static string Snapshot(ReversiBoard board)
    {
        var builder = new System.Text.StringBuilder(ReversiBoard.CellCount);
        for (var cell = 0; cell < ReversiBoard.CellCount; cell++)
        {
            builder.Append((char)('0' + board.Cell(cell)));
        }

        return builder.ToString();
    }
}
