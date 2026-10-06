using Aetherphone.Apps.Games.Flow;
using Xunit;

namespace Aetherphone.Tests;

public sealed class FlowBoardTests
{
    private const ulong DailySalt = 0xDA11UL;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = new FlowBoard();
        var second = new FlowBoard();
        first.Reset(4, 1, DailySalt);
        second.Reset(4, 1, DailySalt);

        Assert.Equal(first.Columns, second.Columns);
        Assert.Equal(first.Rows, second.Rows);
        Assert.Equal(first.ColorCount, second.ColorCount);
        for (var color = 0; color < first.ColorCount; color++)
        {
            Assert.Equal(first.EndpointA(color), second.EndpointA(color));
            Assert.Equal(first.EndpointB(color), second.EndpointB(color));
            Assert.Equal(first.SolutionLength(color), second.SolutionLength(color));
            for (var index = 0; index < first.SolutionLength(color); index++)
            {
                Assert.Equal(first.SolutionCell(color, index), second.SolutionCell(color, index));
            }
        }
    }

    [Fact]
    public void TheDailySaltLaysOutADifferentBoardThanNormalPlay()
    {
        var normal = new FlowBoard();
        var daily = new FlowBoard();
        normal.Reset(4, 1);
        daily.Reset(4, 1, DailySalt);

        var same = true;
        for (var color = 0; color < normal.ColorCount && same; color++)
        {
            same = normal.EndpointA(color) == daily.EndpointA(color) && normal.EndpointB(color) == daily.EndpointB(color);
        }

        Assert.False(same);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 6)]
    [InlineData(1, 3)]
    [InlineData(1, 12)]
    [InlineData(2, 1)]
    [InlineData(2, 12)]
    public void SolutionSegmentsCoverEveryCellOnceAndStayAdjacent(int difficulty, int level)
    {
        var board = new FlowBoard();
        board.Reset(level, difficulty);
        var seen = new bool[board.CellCount];
        var covered = 0;
        for (var color = 0; color < board.ColorCount; color++)
        {
            var length = board.SolutionLength(color);
            Assert.True(length >= 2);
            Assert.Equal(board.EndpointA(color), board.SolutionCell(color, 0));
            Assert.Equal(board.EndpointB(color), board.SolutionCell(color, length - 1));
            for (var index = 0; index < length; index++)
            {
                var cell = board.SolutionCell(color, index);
                Assert.False(seen[cell], $"cell {cell} appears twice in the solution");
                seen[cell] = true;
                covered++;
                if (index > 0)
                {
                    Assert.True(Adjacent(board, board.SolutionCell(color, index - 1), cell),
                        $"color {color} jumps at index {index}");
                }
            }
        }

        Assert.Equal(board.CellCount, covered);
    }

    [Fact]
    public void AHintLaysTheWholeSegmentAndConnectsItsColor()
    {
        var board = new FlowBoard();
        board.Reset(3, 0);
        var color = board.HintColor();
        Assert.True(color >= 0);
        board.Press(board.EndpointA(color));
        board.Release();

        Assert.True(board.ApplyHint(color));

        Assert.True(board.IsConnected(color));
        Assert.Equal(board.SolutionLength(color), board.PathLength(color));
        for (var index = 0; index < board.PathLength(color); index++)
        {
            var cell = board.SolutionCell(color, index);
            Assert.Equal(cell, board.PathCell(color, index));
            Assert.Equal(color, board.Owner(cell));
        }

        Assert.False(board.ApplyHint(color));
    }

    [Fact]
    public void HintingEveryColorSolvesTheBoard()
    {
        var board = new FlowBoard();
        board.Reset(5, 2);
        var guard = 0;
        while (board.HintColor() >= 0 && guard++ < FlowBoard.MaxColors)
        {
            Assert.True(board.ApplyHint(board.HintColor()));
        }

        Assert.True(board.IsSolved());
        Assert.Equal(board.ColorCount, board.ConnectedColors());
        Assert.Equal(board.CellCount, board.FilledCells());
    }

    private static bool Adjacent(FlowBoard board, int first, int second)
    {
        var columnDelta = Math.Abs(first % board.Columns - second % board.Columns);
        var rowDelta = Math.Abs(first / board.Columns - second / board.Columns);
        return columnDelta + rowDelta == 1;
    }
}
