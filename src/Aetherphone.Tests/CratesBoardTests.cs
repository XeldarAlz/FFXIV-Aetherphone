using System.Text;
using Aetherphone.Apps.Games.Crates;
using Aetherphone.Apps.Games.Framework;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CratesBoardTests
{
    private static readonly CratesLevel Corridor = new(3,
        "#######",
        "#@-$-.#",
        "#######");

    private static readonly CratesLevel Dead = new(1,
        "######",
        "#.@$-#",
        "######");

    private static readonly CratesLevel Pair = new(4,
        "########",
        "#@$$-..#",
        "########");

    private static readonly CratesLevel Room = new(9,
        "######",
        "#----#",
        "#-$$-#",
        "#-.@.#",
        "######");

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Replay(4242);
        var second = Replay(4242);
        var other = Replay(77);

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public void WalkingCountsAMoveWithoutAPush()
    {
        var board = Loaded(Corridor);

        Assert.Equal(CratesStep.Walked, board.Move(CratesDirection.Right));

        Assert.Equal(1, board.Moves);
        Assert.Equal(0, board.Pushes);
        Assert.Equal(board.Cell(2, 1), board.PlayerCell);
    }

    [Fact]
    public void PushingMovesTheCrateOneCellAhead()
    {
        var board = Loaded(Corridor);
        board.Move(CratesDirection.Right);

        Assert.Equal(CratesStep.Pushed, board.Move(CratesDirection.Right));

        Assert.Equal(0, board.CrateAt(4, 1));
        Assert.Equal(CratesBoard.NoCrate, board.CrateAt(3, 1));
        Assert.Equal(board.Cell(3, 1), board.PlayerCell);
        Assert.Equal(2, board.Moves);
        Assert.Equal(1, board.Pushes);
        Assert.Equal(0, board.LastCrate);
    }

    [Fact]
    public void ACrateCannotBePushedIntoAWall()
    {
        var board = Loaded(Dead);
        Assert.Equal(CratesStep.Pushed, board.Move(CratesDirection.Right));

        Assert.Equal(CratesStep.Blocked, board.Move(CratesDirection.Right));

        Assert.Equal(1, board.Moves);
        Assert.Equal(1, board.Pushes);
        Assert.Equal(0, board.CrateAt(4, 1));
        Assert.Equal(board.Cell(3, 1), board.PlayerCell);
    }

    [Fact]
    public void ASolvedBoardRefusesFurtherMoves()
    {
        var board = Loaded(Corridor);
        board.Move(CratesDirection.Right);
        board.Move(CratesDirection.Right);
        board.Move(CratesDirection.Right);

        Assert.True(board.Solved);
        Assert.Equal(CratesStep.Blocked, board.Move(CratesDirection.Left));
        Assert.Equal(3, board.Moves);
    }

    [Fact]
    public void TwoCratesInARowCannotBePushed()
    {
        var board = Loaded(Pair);

        Assert.Equal(CratesStep.Blocked, board.Move(CratesDirection.Right));

        Assert.Equal(0, board.Moves);
        Assert.Equal(0, board.Pushes);
        Assert.Equal(board.Cell(1, 1), board.PlayerCell);
        Assert.Equal(0, board.CrateAt(2, 1));
        Assert.Equal(1, board.CrateAt(3, 1));
    }

    [Fact]
    public void WallsBlockThePlayer()
    {
        var board = Loaded(Corridor);

        Assert.Equal(CratesStep.Blocked, board.Move(CratesDirection.Up));
        Assert.Equal(CratesStep.Blocked, board.Move(CratesDirection.Left));
        Assert.Equal(0, board.Moves);
        Assert.Equal(CratesDirection.Left, board.Facing);
    }

    [Fact]
    public void LandingOnATargetIsReportedAndCounted()
    {
        var board = Loaded(Corridor);
        board.Move(CratesDirection.Right);
        board.Move(CratesDirection.Right);

        board.Move(CratesDirection.Right);

        Assert.True(board.LastCrateLanded);
        Assert.Equal(1, board.CratesOnTargets);
        Assert.True(board.CrateOnTarget(0));
        Assert.True(board.Solved);
    }

    [Fact]
    public void UndoRestoresThePlayerTheCrateAndTheCounters()
    {
        var board = Loaded(Corridor);
        board.Move(CratesDirection.Right);
        board.Move(CratesDirection.Right);
        board.Move(CratesDirection.Right);

        Assert.True(board.Undo(out var undone));

        Assert.Equal(CratesDirection.Right, undone);
        Assert.False(board.Solved);
        Assert.True(board.LastCrateLeft);
        Assert.Equal(0, board.CrateAt(4, 1));
        Assert.Equal(board.Cell(3, 1), board.PlayerCell);
        Assert.Equal(2, board.Moves);
        Assert.Equal(1, board.Pushes);
        Assert.Equal(1, board.Undos);

        Assert.True(board.Undo(out _));
        Assert.True(board.Undo(out _));
        Assert.False(board.Undo(out _));
        Assert.Equal(board.Cell(1, 1), board.PlayerCell);
        Assert.Equal(0, board.CrateAt(3, 1));
        Assert.Equal(0, board.Moves);
        Assert.Equal(0, board.Pushes);
        Assert.False(board.CanUndo);
    }

    [Fact]
    public void UndoIsUnlimited()
    {
        var board = Loaded(Room);
        for (var step = 0; step < 600; step++)
        {
            board.Move(step % 2 == 0 ? CratesDirection.Left : CratesDirection.Right);
        }

        var moves = board.Moves;
        Assert.True(moves > 500);
        for (var step = 0; step < moves; step++)
        {
            Assert.True(board.Undo(out _));
        }

        Assert.False(board.CanUndo);
        Assert.Equal(board.Cell(3, 3), board.PlayerCell);
        Assert.Equal(0, board.Moves);
    }

    [Fact]
    public void RestartPutsEverythingBack()
    {
        var board = Loaded(Room);
        board.Move(CratesDirection.Up);
        board.Move(CratesDirection.Left);

        board.Restart();

        Assert.Equal(board.Cell(3, 3), board.PlayerCell);
        Assert.Equal(0, board.CrateAt(2, 2));
        Assert.Equal(1, board.CrateAt(3, 2));
        Assert.Equal(0, board.Moves);
        Assert.Equal(0, board.Pushes);
        Assert.False(board.CanUndo);
    }

    [Fact]
    public void PathToWalksAroundCratesAndNeverThroughThem()
    {
        var board = Loaded(Room);
        Span<CratesDirection> path = stackalloc CratesDirection[CratesBoard.MaxCells];

        var length = board.PathTo(2, 1, path);

        Assert.Equal(5, length);
        for (var index = 0; index < length; index++)
        {
            Assert.NotEqual(CratesStep.Pushed, board.Move(path[index]));
        }

        Assert.Equal(board.Cell(2, 1), board.PlayerCell);
        Assert.Equal(-1, board.PathTo(2, 2, path));
        Assert.Equal(-1, board.PathTo(0, 0, path));
    }

    [Fact]
    public void StarsFollowTheParRule()
    {
        Assert.Equal(3, CratesBoard.Stars(20, 20));
        Assert.Equal(3, CratesBoard.Stars(18, 20));
        Assert.Equal(2, CratesBoard.Stars(21, 20));
        Assert.Equal(2, CratesBoard.Stars(30, 20));
        Assert.Equal(1, CratesBoard.Stars(31, 20));
        Assert.Equal(2, CratesBoard.Stars(4, 3));
        Assert.Equal(1, CratesBoard.Stars(5, 3));
    }

    [Fact]
    public void MalformedLevelsAreRefused()
    {
        var board = new CratesBoard();

        Assert.False(board.Load(new CratesLevel(1, "#####", "#$..#", "#@--#", "#####")));
        Assert.False(board.Load(new CratesLevel(1, "#####", "#$.-#", "#----#", "#####")));
        Assert.False(board.Load(new CratesLevel(1, "######", "#@$.@#", "######")));
        Assert.False(board.Load(new CratesLevel(1, "#####", "#@$?#", "#####")));
    }

    [Fact]
    public void EveryLevelParsesWithAsManyCratesAsTargets()
    {
        Assert.Equal(60, CratesLevels.Count);
        var board = new CratesBoard();
        for (var level = 1; level <= CratesLevels.Count; level++)
        {
            var data = CratesLevels.Get(level);

            Assert.True(board.Load(data), $"Level {level} does not parse.");
            Assert.True(board.CrateCount > 0, $"Level {level} has no crates.");
            Assert.Equal(board.TargetCount, board.CrateCount);
            Assert.False(board.Solved, $"Level {level} starts solved.");
            Assert.True(data.Par > 0, $"Level {level} has no par.");
        }
    }

    [Fact]
    public void LevelsGrowFromSmallTutorialsToLargeRooms()
    {
        var board = new CratesBoard();
        board.Load(CratesLevels.Get(1));
        Assert.True(board.Columns <= 5 && board.Rows <= 5);
        for (var level = 1; level <= CratesLevels.Count; level++)
        {
            board.Load(CratesLevels.Get(level));

            Assert.True(board.Columns <= 11, $"Level {level} is {board.Columns} wide.");
            Assert.True(board.Rows <= 12, $"Level {level} is {board.Rows} tall.");
        }
    }

    [Fact]
    public void EveryLevelIsSolvableWithinParAndAHalf()
    {
        var board = new CratesBoard();
        for (var level = 1; level <= CratesLevels.Count; level++)
        {
            var data = CratesLevels.Get(level);
            board.Load(data);
            var limit = data.Par * 3 / 2;

            var optimal = CratesSolver.MinimumMoves(board, limit);

            Assert.True(optimal >= 0, $"Level {level} has no solution within {limit} moves.");
            Assert.True(optimal <= data.Par, $"Level {level} needs {optimal} moves but its par is {data.Par}.");
        }
    }

    private static CratesBoard Loaded(in CratesLevel level)
    {
        var board = new CratesBoard();
        Assert.True(board.Load(level));
        return board;
    }

    private static string Replay(ulong seed)
    {
        var random = GameRandom.FromSeed(seed);
        var board = new CratesBoard();
        board.Load(CratesLevels.Get(38));
        var trace = new StringBuilder();
        for (var step = 0; step < 400; step++)
        {
            if (random.Chance(0.15f))
            {
                board.Undo(out _);
            }
            else
            {
                board.Move((CratesDirection)random.Next(4));
            }

            trace.Append(board.PlayerCell).Append(':').Append(board.Moves).Append(':').Append(board.Pushes).Append(';');
        }

        for (var crate = 0; crate < board.CrateCount; crate++)
        {
            trace.Append(board.CrateCell(crate)).Append(',');
        }

        return trace.ToString();
    }

    private static class CratesSolver
    {
        public static int MinimumMoves(CratesBoard board, int limit)
        {
            var columns = board.Columns;
            var cells = columns * board.Rows;
            Assert.True(cells <= 128, "The solver packs crates into 128 bits.");
            var floor = new bool[cells];
            var goal = UInt128.Zero;
            var start = UInt128.Zero;
            for (var cell = 0; cell < cells; cell++)
            {
                var column = cell % columns;
                var row = cell / columns;
                floor[cell] = board.TileAt(column, row) == CratesTile.Floor;
                if (board.IsTarget(column, row))
                {
                    goal |= UInt128.One << cell;
                }
            }

            for (var crate = 0; crate < board.CrateCount; crate++)
            {
                start |= UInt128.One << board.CrateCell(crate);
            }

            var live = LiveCells(board, floor, goal);
            var seen = new HashSet<(UInt128, int)> { (start, board.PlayerCell) };
            var frontier = new List<(UInt128 Crates, int Player)> { (start, board.PlayerCell) };
            var next = new List<(UInt128 Crates, int Player)>();
            for (var depth = 1; depth <= limit && frontier.Count > 0; depth++)
            {
                next.Clear();
                for (var index = 0; index < frontier.Count; index++)
                {
                    var (crates, player) = frontier[index];
                    for (var direction = 0; direction < 4; direction++)
                    {
                        var ahead = board.Neighbour(player, (CratesDirection)direction);
                        if (ahead < 0 || !floor[ahead])
                        {
                            continue;
                        }

                        var moved = crates;
                        if ((crates >> ahead & UInt128.One) != UInt128.Zero)
                        {
                            var beyond = board.Neighbour(ahead, (CratesDirection)direction);
                            if (beyond < 0 || !floor[beyond] || !live[beyond] ||
                                (crates >> beyond & UInt128.One) != UInt128.Zero)
                            {
                                continue;
                            }

                            moved = (crates & ~(UInt128.One << ahead)) | (UInt128.One << beyond);
                        }

                        if (!seen.Add((moved, ahead)))
                        {
                            continue;
                        }

                        if (moved == goal)
                        {
                            return depth;
                        }

                        next.Add((moved, ahead));
                    }
                }

                (frontier, next) = (next, frontier);
            }

            return -1;
        }

        private static bool[] LiveCells(CratesBoard board, bool[] floor, UInt128 goal)
        {
            var live = new bool[floor.Length];
            var queue = new Queue<int>();
            for (var cell = 0; cell < floor.Length; cell++)
            {
                if ((goal >> cell & UInt128.One) == UInt128.Zero)
                {
                    continue;
                }

                live[cell] = true;
                queue.Enqueue(cell);
            }

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                for (var direction = 0; direction < 4; direction++)
                {
                    var from = board.Neighbour(cell, (CratesDirection)direction);
                    var standing = from < 0 ? -1 : board.Neighbour(from, (CratesDirection)direction);
                    if (from < 0 || standing < 0 || live[from] || !floor[from] || !floor[standing])
                    {
                        continue;
                    }

                    live[from] = true;
                    queue.Enqueue(from);
                }
            }

            return live;
        }
    }
}
