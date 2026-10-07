using System.Text;
using Aetherphone.Apps.Games.Delve;
using Aetherphone.Apps.Games.Framework;
using Xunit;

namespace Aetherphone.Tests;

public sealed class DelveBoardTests
{
    private const int Up = 0;
    private const int Right = 1;
    private const int Left = 3;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Replay(9001);
        var second = Replay(9001);
        var other = Replay(31);

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public void ABoulderFallsIntoEmptySpaceAndLands()
    {
        var board = Loaded(1, 60,
            "#######",
            "#@.O.*#",
            "#..-..#",
            "#..-..#",
            "#X....#",
            "#######");

        board.Tick();
        Assert.Equal(DelveTile.Empty, board.TileAt(3, 1));
        Assert.Equal(DelveTile.Boulder, board.TileAt(3, 2));
        Assert.True(board.FallingAt(board.Cell(3, 2)));

        board.Tick();
        board.Tick();

        Assert.Equal(DelveTile.Boulder, board.TileAt(3, 3));
        Assert.False(board.FallingAt(board.Cell(3, 3)));
        Assert.True(HasEvent(board, DelveEventKind.Landed));
    }

    [Fact]
    public void ABoulderRollsOffAnotherRoundedObject()
    {
        var board = Loaded(1, 60,
            "#######",
            "#@....#",
            "#.-O..#",
            "#.-O..#",
            "#..*.X#",
            "#######");

        board.Tick();

        Assert.Equal(DelveTile.Boulder, board.TileAt(2, 2));
        Assert.Equal(DelveTile.Empty, board.TileAt(3, 2));
        Assert.Equal(DelveTile.Boulder, board.TileAt(3, 3));
        Assert.True(board.FallingAt(board.Cell(2, 2)));

        board.Tick();

        Assert.Equal(DelveTile.Boulder, board.TileAt(2, 3));
    }

    [Fact]
    public void BouldersNeverRollOffWallsOrDirt()
    {
        var board = Loaded(1, 60,
            "#######",
            "#@....#",
            "#.-O..#",
            "#.-#..#",
            "#.-O..#",
            "#*-..X#",
            "#######");

        for (var tick = 0; tick < 5; tick++)
        {
            board.Tick();
        }

        Assert.Equal(DelveTile.Boulder, board.TileAt(3, 2));
        Assert.Equal(DelveTile.Boulder, board.TileAt(3, 4));
    }

    [Fact]
    public void ARestingBoulderDoesNotCrushThePlayerUnderIt()
    {
        var board = Loaded(1, 60,
            "#######",
            "#..O..#",
            "#..@..#",
            "#.....#",
            "#*...X#",
            "#######");

        board.Tick();
        board.Tick();
        Assert.Equal(DelveState.Playing, board.State);

        board.Hold(Left);
        board.Tick();
        board.Hold(DelveBoard.NoDirection);
        board.Tick();

        Assert.Equal(DelveState.Playing, board.State);
        Assert.Equal(board.Cell(2, 2), board.PlayerCell);
        Assert.Equal(DelveTile.Boulder, board.TileAt(3, 2));
        Assert.Equal(DelveTile.Empty, board.TileAt(3, 1));
    }

    [Fact]
    public void AFallingBoulderCrushesThePlayer()
    {
        var board = Loaded(1, 60,
            "#######",
            "#..O..#",
            "#..-..#",
            "#.@...#",
            "#*...X#",
            "#######");

        board.Hold(Right);
        board.Tick();
        board.Hold(DelveBoard.NoDirection);
        board.Tick();

        Assert.Equal(DelveState.Dead, board.State);
        Assert.True(HasEvent(board, DelveEventKind.Crushed));
        Assert.True(HasEvent(board, DelveEventKind.Died));
        Assert.Equal(DelveTile.Blast, board.TileAt(3, 3));
    }

    [Fact]
    public void CrushingABatLeavesABlastThatBurnsAway()
    {
        var board = Loaded(1, 60,
            "########",
            "#@..O..#",
            "#...-..#",
            "#...b..#",
            "#......#",
            "#*....X#",
            "########");

        board.Tick();
        board.Tick();

        Assert.True(HasEvent(board, DelveEventKind.Crushed));
        Assert.Equal(DelveTile.Blast, board.TileAt(4, 3));
        Assert.Equal(DelveState.Playing, board.State);
        for (var tick = 0; tick < DelveBoard.BlastTicks; tick++)
        {
            board.Tick();
        }

        for (var row = 2; row <= 4; row++)
        {
            for (var column = 3; column <= 5; column++)
            {
                Assert.Equal(DelveTile.Empty, board.TileAt(column, row));
            }
        }
    }

    [Fact]
    public void ACrushedSlimeTurnsIntoGems()
    {
        var board = Loaded(1, 60,
            "########",
            "#@..O..#",
            "#...-..#",
            "#...s..#",
            "#......#",
            "#*....X#",
            "########");

        for (var tick = 0; tick < 2 + DelveBoard.BlastTicks; tick++)
        {
            board.Tick();
        }

        for (var row = 2; row <= 4; row++)
        {
            for (var column = 3; column <= 5; column++)
            {
                Assert.Equal(DelveTile.Gem, board.TileAt(column, row));
            }
        }
    }

    [Fact]
    public void CollectingTheQuotaOpensTheExitAndWalkingInWins()
    {
        var board = Loaded(2, 60,
            "#######",
            "#@**X.#",
            "#.....#",
            "#######");
        board.Hold(Right);

        board.Tick();
        Assert.Equal(1, board.Gems);
        Assert.False(board.ExitOpen);

        board.Tick();
        Assert.Equal(2, board.Gems);
        Assert.True(board.ExitOpen);
        Assert.True(HasEvent(board, DelveEventKind.ExitOpened));

        board.Tick();
        Assert.Equal(DelveState.Won, board.State);
        Assert.True(HasEvent(board, DelveEventKind.Escaped));
    }

    [Fact]
    public void AClosedExitBlocksThePlayer()
    {
        var board = Loaded(2, 60,
            "#######",
            "#@X.**#",
            "#######");
        board.Hold(Right);

        board.Tick();
        board.Tick();

        Assert.Equal(DelveState.Playing, board.State);
        Assert.Equal(board.Cell(1, 1), board.PlayerCell);
        Assert.Equal(DelveTile.Exit, board.TileAt(2, 1));
    }

    [Fact]
    public void DiggingTurnsDirtIntoTunnel()
    {
        var board = Loaded(1, 60,
            "######",
            "#@..*#",
            "#...X#",
            "######");
        board.Hold(Right);

        board.Tick();

        Assert.Equal(board.Cell(2, 1), board.PlayerCell);
        Assert.Equal(DelveTile.Empty, board.TileAt(1, 1));
        Assert.True(HasEvent(board, DelveEventKind.Dug));
    }

    [Fact]
    public void BatsTurnLeftWheneverTheyCan()
    {
        var board = Loaded(1, 60,
            "#########",
            "#@......#",
            "#..---..#",
            "#..b--..#",
            "#..---..#",
            "#.....*X#",
            "#########");

        board.Tick();
        Assert.Equal(DelveTile.Bat, board.TileAt(3, 4));

        board.Tick();
        Assert.Equal(DelveTile.Bat, board.TileAt(4, 4));

        board.Tick();
        Assert.Equal(DelveTile.Bat, board.TileAt(4, 3));
    }

    [Fact]
    public void SlimesTurnRightWheneverTheyCan()
    {
        var board = Loaded(1, 60,
            "#########",
            "#@......#",
            "#..---..#",
            "#..s--..#",
            "#..---..#",
            "#.....*X#",
            "#########");

        board.Tick();
        Assert.Equal(DelveTile.Slime, board.TileAt(3, 4));

        board.Tick();
        Assert.Equal(DelveTile.Slime, board.TileAt(3, 4));

        board.Tick();
        Assert.Equal(DelveTile.Slime, board.TileAt(4, 4));
    }

    [Fact]
    public void AnEnemyTouchingThePlayerExplodes()
    {
        var board = Loaded(1, 60,
            "#######",
            "#@b...#",
            "#.....#",
            "#*...X#",
            "#######");

        board.Tick();

        Assert.Equal(DelveState.Dead, board.State);
        Assert.True(HasEvent(board, DelveEventKind.Touched));
        Assert.Equal(DelveTile.Blast, board.TileAt(1, 1));
    }

    [Fact]
    public void RunningOutOfTimeEndsTheRun()
    {
        var board = Loaded(1, 1,
            "######",
            "#@..*#",
            "#...X#",
            "######");

        board.Step(0.6f);
        Assert.Equal(DelveState.Playing, board.State);
        board.Step(0.6f);

        Assert.Equal(DelveState.Dead, board.State);
        Assert.Equal(0f, board.TimeLeft);
    }

    [Fact]
    public void ABoulderIsPushedSidewaysOnlyIntoEmptySpace()
    {
        var board = Loaded(1, 60,
            "########",
            "#@O-..*#",
            "#.....X#",
            "########");
        board.Hold(Right);

        var ticks = 0;
        while (board.TileAt(3, 1) != DelveTile.Boulder && ticks < 60)
        {
            board.Tick();
            ticks++;
        }

        Assert.Equal(DelveTile.Boulder, board.TileAt(3, 1));
        Assert.Equal(board.Cell(2, 1), board.PlayerCell);

        var jammed = Loaded(1, 60,
            "########",
            "#@OO..*#",
            "#.....X#",
            "########");
        jammed.Hold(Right);
        for (var tick = 0; tick < 60; tick++)
        {
            jammed.Tick();
        }

        Assert.Equal(DelveTile.Boulder, jammed.TileAt(2, 1));
        Assert.Equal(jammed.Cell(1, 1), jammed.PlayerCell);
    }

    [Fact]
    public void BouldersCannotBePushedUpward()
    {
        var board = Loaded(1, 60,
            "######",
            "#.-.*#",
            "#.O.X#",
            "#.@..#",
            "######");
        board.Hold(Up);

        for (var tick = 0; tick < 30; tick++)
        {
            board.Tick();
        }

        Assert.Equal(board.Cell(2, 3), board.PlayerCell);
        Assert.Equal(DelveTile.Boulder, board.TileAt(2, 2));
    }

    [Fact]
    public void StarsRewardSpareGemsAndSpareTime()
    {
        Assert.Equal(0, DelveBoard.Stars(false, 20, 10, 50f, 60f));
        Assert.Equal(1, DelveBoard.Stars(true, 8, 10, 10f, 60f));
        Assert.Equal(2, DelveBoard.Stars(true, 10, 10, 10f, 60f));
        Assert.Equal(2, DelveBoard.Stars(true, 8, 10, 25f, 60f));
        Assert.Equal(3, DelveBoard.Stars(true, 12, 10, 30f, 60f));
    }

    [Fact]
    public void BonusGemsSitHalfwayBetweenTheQuotaAndEveryGem()
    {
        var board = Loaded(2, 60,
            "#########",
            "#@*****X#",
            "#########");

        Assert.Equal(5, board.GemsInLevel);
        Assert.Equal(4, board.BonusGems);
    }

    [Fact]
    public void MalformedLevelsAreRefused()
    {
        var board = new DelveBoard();

        Assert.False(board.Load(new DelveLevel(1, 60, "#####", "#@.X#", "#####"), GameRandom.FromSeed(1)));
        Assert.False(board.Load(new DelveLevel(2, 60, "#####", "#@*X#", "#####"), GameRandom.FromSeed(1)));
        Assert.False(board.Load(new DelveLevel(1, 60, "######", "#@*X@#", "######"), GameRandom.FromSeed(1)));
        Assert.False(board.Load(new DelveLevel(1, 60, "#####", "#@*.#", "#####"), GameRandom.FromSeed(1)));
        Assert.False(board.Load(new DelveLevel(1, 60, "#####", "#@*X?", "#####"), GameRandom.FromSeed(1)));
    }

    [Fact]
    public void EveryLevelParsesAndHasEnoughGemsForItsQuota()
    {
        Assert.Equal(30, DelveLevels.Count);
        var board = new DelveBoard();
        for (var level = 1; level <= DelveLevels.Count; level++)
        {
            var data = DelveLevels.Get(level);

            Assert.True(board.Load(data, GameRandom.FromSeed(7)), $"Level {level} does not parse.");
            Assert.True(data.Quota > 0, $"Level {level} has no quota.");
            Assert.True(board.GemsInLevel >= data.Quota, $"Level {level} has too few gems.");
            Assert.True(data.Seconds >= 30, $"Level {level} gives too little time.");
            Assert.True(board.Columns > 13 || board.Rows > 19, $"Level {level} fits on one screen.");
        }
    }

    [Fact]
    public void EveryLevelCanBeFinishedWithoutMovingABoulder()
    {
        var board = new DelveBoard();
        for (var level = 1; level <= DelveLevels.Count; level++)
        {
            board.Load(DelveLevels.Get(level), GameRandom.FromSeed(7));

            var reached = Reachable(board, out var gems);

            Assert.True(gems >= board.Quota, $"Level {level} reaches {gems} gems for a quota of {board.Quota}.");
            Assert.True(ExitTouched(board, reached), $"Level {level} cannot reach its exit.");
        }
    }

    [Fact]
    public void EveryLevelStartsAtRest()
    {
        var board = new DelveBoard();
        for (var level = 1; level <= DelveLevels.Count; level++)
        {
            board.Load(DelveLevels.Get(level), GameRandom.FromSeed(7));

            board.Tick();

            for (var cell = 0; cell < board.Columns * board.Rows; cell++)
            {
                var tile = board.TileAt(cell);
                if (tile is DelveTile.Boulder or DelveTile.Gem)
                {
                    Assert.False(board.FallingAt(cell), $"Level {level} drops a stone on its first tick.");
                }
            }

            Assert.Equal(DelveState.Playing, board.State);
        }
    }

    private static DelveBoard Loaded(int quota, int seconds, params string[] rows)
    {
        var board = new DelveBoard();
        Assert.True(board.Load(new DelveLevel(quota, seconds, rows), GameRandom.FromSeed(1)));
        return board;
    }

    private static bool HasEvent(DelveBoard board, DelveEventKind kind)
    {
        for (var index = 0; index < board.EventCount; index++)
        {
            if (board.Event(index).Kind == kind)
            {
                return true;
            }
        }

        return false;
    }

    private static string Replay(ulong seed)
    {
        var board = new DelveBoard();
        board.Load(DelveLevels.Get(5), GameRandom.FromSeed(seed));
        var input = GameRandom.FromSeed(seed ^ 0x5DEECE66DUL);
        var trace = new StringBuilder();
        for (var step = 0; step < 240; step++)
        {
            board.Hold(input.Chance(0.2f) ? DelveBoard.NoDirection : input.Next(4));
            board.Step(DelveBoard.TickSeconds);
            trace.Append(board.PlayerCell).Append(':').Append(board.Gems).Append(':').Append((int)board.State)
                .Append(';');
        }

        for (var cell = 0; cell < board.Columns * board.Rows; cell++)
        {
            trace.Append((char)('A' + (int)board.TileAt(cell)));
        }

        return trace.ToString();
    }

    private static bool[] Reachable(DelveBoard board, out int gems)
    {
        var reached = new bool[board.Columns * board.Rows];
        var queue = new Queue<int>();
        reached[board.PlayerCell] = true;
        queue.Enqueue(board.PlayerCell);
        gems = 0;
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            if (board.TileAt(cell) == DelveTile.Gem)
            {
                gems++;
            }

            var column = cell % board.Columns;
            var row = cell / board.Columns;
            Visit(board, reached, queue, column + 1, row);
            Visit(board, reached, queue, column - 1, row);
            Visit(board, reached, queue, column, row + 1);
            Visit(board, reached, queue, column, row - 1);
        }

        return reached;
    }

    private static void Visit(DelveBoard board, bool[] reached, Queue<int> queue, int column, int row)
    {
        if (!board.InBounds(column, row))
        {
            return;
        }

        var cell = board.Cell(column, row);
        var tile = board.TileAt(cell);
        if (reached[cell] || tile is DelveTile.Wall or DelveTile.Boulder or DelveTile.Exit)
        {
            return;
        }

        reached[cell] = true;
        queue.Enqueue(cell);
    }

    private static bool ExitTouched(DelveBoard board, bool[] reached)
    {
        var exit = board.ExitCell;
        var column = exit % board.Columns;
        var row = exit / board.Columns;
        return Touches(board, reached, column + 1, row) || Touches(board, reached, column - 1, row) ||
               Touches(board, reached, column, row + 1) || Touches(board, reached, column, row - 1);
    }

    private static bool Touches(DelveBoard board, bool[] reached, int column, int row) =>
        board.InBounds(column, row) && reached[board.Cell(column, row)];
}
