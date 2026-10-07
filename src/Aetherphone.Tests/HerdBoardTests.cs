using System.Text;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Herd;
using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HerdBoardTests
{
    private const string Air = "................";
    private const string Ground = "################";

    private static readonly string[] Solutions =
    {
        "",
        "D40,48",
        "R48,72",
        "B70,72",
        "C*,* C*,* C*,* C*,* C*,* C*,*",
        "F*,* F*,* F*,* F*,* F*,* F*,*",
        "H60,96",
        "B70,48 D30,48",
        "R46,96 R72,84",
        "R30,96 R56,84",
        "D60,32 D100,64",
        "H34,96 H66,96 H98,96",
        "F*,*#0 R78,104,-1",
        "B64,48 D96,48#1",
        "C*,* C*,* C*,* C*,* C*,* C*,* F*,* F*,* F*,* F*,* F*,* F*,*",
        "B80,72 R40,72,-1",
        "H36,96 R72,104",
        "R16,88 R56,88 R96,88",
        "H58,88",
        "H44,88 D92,88",
        "D72,24",
        "R48,88",
        "D36,24 D52,64 D96,104",
        "H36,80 R64,80 D108,80",
        "B40,32 F*,* F*,* F*,* F*,* F*,* F*,* F*,* F*,* F*,*",
        "R14,104 R40,92 R80,80",
        "C*,*#0 B24,80,-1 R72,80#0 D112,80#0 +H52,80,1",
        "B40,72 H76,72,1 R96,72",
        "H28,48 R56,48 D104,48 H100,88,-1",
        "D40,24 H52,64,1 R72,64 D100,64",
    };

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = new HerdBoard();
        var second = new HerdBoard();
        var other = new HerdBoard();
        Solve(first, HerdLevels.Get(8), Solutions[7], 1234, out var firstLog);
        Solve(second, HerdLevels.Get(8), Solutions[7], 1234, out var secondLog);
        Solve(other, HerdLevels.Get(8), Solutions[7], 99, out _);

        Assert.Equal(firstLog, secondLog);
        Assert.Equal(first.Ticks, second.Ticks);
        Assert.Equal(first.SavedCount, second.SavedCount);
        Assert.Equal(Trace(first), Trace(second));
        Assert.NotEqual(Trace(first), Trace(other));
    }

    [Fact]
    public void EveryLevelParses()
    {
        Assert.Equal(30, HerdLevels.Count);
        for (var number = 1; number <= HerdLevels.Count; number++)
        {
            var level = HerdLevels.Get(number);
            Assert.Equal(HerdLevel.Rows, level.MapRows);
            Assert.Equal(1, level.Doors);
            Assert.Equal(1, level.Exits);
            Assert.InRange(level.Count, 1, HerdBoard.MaxMoogles);
            Assert.InRange(level.Required, 1, level.Count);
            Assert.True(level.ReleaseTicks > 0, $"level {number} release");
            Assert.True(level.Seconds > 0, $"level {number} time");
            for (var row = 0; row < HerdLevel.Rows; row++)
            {
                var line = level.Row(row);
                Assert.Equal(HerdLevel.Columns, line.Length);
                for (var column = 0; column < line.Length; column++)
                {
                    Assert.Contains(line[column], "#-_/\\~.DdE");
                }
            }

            Assert.Equal(HerdLevel.Solid, level.At(level.ExitX, level.ExitY + 1));
            Assert.Equal(HerdLevel.Air, level.At(level.DoorX, level.DoorY + 1));
        }
    }

    [Fact]
    public void EveryLevelHasAWorkingSolution()
    {
        for (var number = 1; number <= HerdLevels.Count; number++)
        {
            var board = new HerdBoard();
            var saved = Solve(board, HerdLevels.Get(number), Solutions[number - 1], 7, out var log);
            Assert.True(saved >= board.Required, $"level {number} saved {saved} of {board.Required}: {log}");
            Assert.False(log.Contains("UNUSED", StringComparison.Ordinal), $"level {number}: {log}");
        }
    }

    [Fact]
    public void NoLevelAfterTheFirstClearsWithoutSkills()
    {
        for (var number = 2; number <= HerdLevels.Count; number++)
        {
            var board = new HerdBoard();
            var saved = Solve(board, HerdLevels.Get(number), string.Empty, 7, out _);
            Assert.True(saved < board.Required, $"level {number} saves {saved} with no skills");
        }
    }

    [Fact]
    public void WalkersClimbSmallStepsAndTurnAtWalls()
    {
        var board = Load(Flat(), "000000");
        board.Terrain.FillRect(Cells(60, 85, 63, 87));
        board.Terrain.FillRect(Cells(90, 84, 93, 87));
        RunUntil(board, () => board.MoogleCount > 0 && board.Moogle(0).Column == 70);

        Assert.Equal(HerdAction.Walking, board.Moogle(0).Action);
        Assert.Equal(88, board.Moogle(0).Row);
        RunUntil(board, () => board.Moogle(0).Direction < 0);

        Assert.Equal(89, board.Moogle(0).Column);
        Assert.Equal(88, board.Moogle(0).Row);
    }

    [Fact]
    public void FallsUpToFiveUnitsAreSafeAndLongerOnesSplat()
    {
        var safe = Load(DropFrom(6), "000000");
        RunUntil(safe, () => safe.MoogleCount > 0 && safe.Moogle(0).Action != HerdAction.Falling);
        Assert.Equal(HerdAction.Walking, safe.Moogle(0).Action);

        var deadly = Load(DropFrom(5), "000000");
        RunUntil(deadly, () => deadly.MoogleCount > 0 && deadly.Moogle(0).Action != HerdAction.Falling);
        Assert.Equal(HerdAction.Splatting, deadly.Moogle(0).Action);
        Assert.Equal(1, deadly.Lost);

        var floater = Load(DropFrom(1), "000010");
        RunUntil(floater, () => floater.MoogleCount > 0);
        Assert.True(floater.Assign(0, HerdSkill.Float));
        RunUntil(floater, () => floater.Moogle(0).Action is HerdAction.Walking or HerdAction.Splatting);
        Assert.Equal(HerdAction.Walking, floater.Moogle(0).Action);
        Assert.Equal(0, floater.Lost);
    }

    [Fact]
    public void BlockersTurnTheOthersAround()
    {
        var board = Load(Flat(), "100000", count: 2, release: 30);
        RunUntil(board, () => board.MoogleCount > 0 && board.Moogle(0).Column == 60);
        Assert.True(board.Assign(0, HerdSkill.Block));
        RunUntil(board, () => board.MoogleCount > 1 && board.Moogle(1).Direction < 0);

        Assert.InRange(board.Moogle(1).Column, 60 - HerdBoard.BlockerReach - 1, 59);
        Assert.Equal(HerdAction.Blocking, board.Moogle(0).Action);
        Assert.False(board.Assign(1, HerdSkill.Block));
    }

    [Fact]
    public void DiggersCarveStraightDownAndDropThrough()
    {
        var board = Load(new[]
        {
            Air, Air, Air, Air, Air, Air, "..D.............", Air, Air, Air, Air, Ground, Air, Air, Air, Air,
            "...........E....", Ground, Ground, Air, Air, Air,
        }, "010000", count: 1);
        RunUntil(board, () => board.MoogleCount > 0 && board.Moogle(0).Column == 40);
        Assert.True(board.Assign(0, HerdSkill.Dig));
        Assert.Equal(0, board.SkillsLeft(HerdSkill.Dig));
        RunUntil(board, () => board.Moogle(0).Action == HerdAction.Falling);

        Assert.Equal(96, board.Moogle(0).Row);
        for (var row = 88; row < 96; row++)
        {
            Assert.False(board.IsSolid(40, row));
            Assert.False(board.IsSolid(36, row));
            Assert.True(board.IsSolid(30, row));
        }

        RunUntil(board, () => board.Over);
        Assert.Equal(1, board.SavedCount);
    }

    [Fact]
    public void BuildersLayTwelveRisingBricks()
    {
        var board = Load(Flat(), "002000", count: 1);
        RunUntil(board, () => board.MoogleCount > 0 && board.Moogle(0).Column == 30);
        Assert.True(board.Assign(0, HerdSkill.Bridge));
        RunUntil(board, () => board.Moogle(0).Action == HerdAction.Walking);

        Assert.Equal(30 + HerdBoard.BricksPerBridge * 2, board.Moogle(0).Column);
        Assert.Equal(88 - HerdBoard.BricksPerBridge, board.Moogle(0).Row);
        for (var brick = 0; brick < HerdBoard.BricksPerBridge; brick++)
        {
            Assert.True(board.IsSolid(30 + brick * 2 + HerdBoard.BrickLength - 1, 87 - brick));
            Assert.False(board.IsSolid(30 + brick * 2, 86 - brick));
        }
    }

    [Fact]
    public void ClimbersScaleWallsButFallBackUnderOverhangs()
    {
        var wall = Load(new[]
        {
            Air, Air, Air, Air, Air, Air, ".......#........", "..D....#........", ".......#........",
            ".......#........", ".......#....E...", Ground, Ground, Air, Air, Air, Air, Air, Air, Air, Air, Air,
        }, "000100", count: 1);
        RunUntil(wall, () => wall.MoogleCount > 0);
        Assert.True(wall.Assign(0, HerdSkill.Climb));
        Assert.False(wall.Assign(0, HerdSkill.Climb));
        RunUntil(wall, () => wall.Moogle(0).Action == HerdAction.Climbing);
        RunUntil(wall, () => wall.Moogle(0).Action != HerdAction.Climbing);

        Assert.Equal(HerdAction.Walking, wall.Moogle(0).Action);
        Assert.Equal(48, wall.Moogle(0).Row);
        Assert.Equal(56, wall.Moogle(0).Column);

        var overhang = Load(new[]
        {
            Air, Air, Air, Air, Air, Air, "......##........", "..D....#........", ".......#........",
            ".......#........", ".......#....E...", Ground, Ground, Air, Air, Air, Air, Air, Air, Air, Air, Air,
        }, "000100", count: 1);
        RunUntil(overhang, () => overhang.MoogleCount > 0);
        Assert.True(overhang.Assign(0, HerdSkill.Climb));
        RunUntil(overhang, () => overhang.Moogle(0).Action == HerdAction.Climbing);
        RunUntil(overhang, () => overhang.Moogle(0).Action != HerdAction.Climbing);

        Assert.Equal(HerdAction.Falling, overhang.Moogle(0).Action);
        Assert.Equal(-1, overhang.Moogle(0).Direction);
    }

    [Fact]
    public void BashersTunnelThroughWallsAndStopAfterwards()
    {
        var board = Load(new[]
        {
            Air, Air, Air, Air, Air, Air, Air, Air, "..D.............", "........##......", "........##......",
            "........##..E...", Ground, Ground, Air, Air, Air, Air, Air, Air, Air, Air,
        }, "000001", count: 1);
        RunUntil(board, () => board.MoogleCount > 0 && board.Moogle(0).Column == 60);
        Assert.True(board.Assign(0, HerdSkill.Bash));
        RunUntil(board, () => board.Moogle(0).Action != HerdAction.Bashing);

        Assert.Equal(HerdAction.Walking, board.Moogle(0).Action);
        for (var column = 64; column < 80; column++)
        {
            Assert.False(board.IsSolid(column, 95));
            Assert.False(board.IsSolid(column, 89));
            Assert.True(board.IsSolid(column, 80));
        }

        RunUntil(board, () => board.Over);
        Assert.Equal(1, board.SavedCount);
    }

    [Fact]
    public void WaterDrownsAndTheExitSaves()
    {
        var board = Load(new[]
        {
            Air, Air, Air, Air, Air, Air, Air, "..D.............", Air, Air, "............E...", "#####~~#########",
            "#####~~#########", Ground, Air, Air, Air, Air, Air, Air, Air, Air,
        }, "000000", count: 2, release: 200);
        RunUntil(board, () => board.Over);

        Assert.Equal(0, board.SavedCount);
        Assert.Equal(2, board.Lost);

        var dry = Load(Flat(), "000000", count: 2);
        RunUntil(dry, () => dry.Over);
        Assert.Equal(2, dry.SavedCount);
        Assert.Equal(0, dry.Lost);
    }

    [Fact]
    public void NukePopsEveryoneAndEndsTheLevel()
    {
        var board = Load(Flat(), "000000", count: 10, release: 10);
        RunUntil(board, () => board.MoogleCount >= 3);
        board.Nuke();
        RunUntil(board, () => board.Over);

        Assert.True(board.Nuking);
        Assert.Equal(3, board.Spawned);
        Assert.Equal(3, board.Lost);
        Assert.Equal(0, board.SavedCount);
    }

    [Fact]
    public void SkillsAreSpentOnlyOnEligibleMoogles()
    {
        var board = Load(DropFrom(1), "010000", count: 1);
        RunUntil(board, () => board.MoogleCount > 0);

        Assert.False(board.CanAssign(0, HerdSkill.Dig));
        Assert.False(board.CanAssign(0, HerdSkill.Bash));
        Assert.False(board.Assign(0, HerdSkill.Dig));
        Assert.Equal(1, board.SkillsLeft(HerdSkill.Dig));
        Assert.Equal(0, board.SkillsUsed);
    }

    [Fact]
    public void StarsFollowTheSavedCount()
    {
        Assert.Equal(0, HerdBoard.Stars(7, 8, 10));
        Assert.Equal(1, HerdBoard.Stars(8, 8, 10));
        Assert.Equal(1, HerdBoard.Stars(9, 8, 10));
        Assert.Equal(2, HerdBoard.Stars(10, 8, 12));
        Assert.Equal(3, HerdBoard.Stars(10, 8, 10));
        Assert.Equal(10, HerdBoard.TwoStarTarget(8, 12));
        Assert.Equal(10, HerdBoard.TwoStarTarget(8, 10));
    }

    private static string[] Flat() => new[]
    {
        Air, Air, Air, Air, Air, Air, Air, "..D.............", Air, Air, "............E...", Ground, Ground, Air, Air,
        Air, Air, Air, Air, Air, Air, Air,
    };

    private static string[] DropFrom(int doorRow)
    {
        var rows = new string[HerdLevel.Rows];
        for (var row = 0; row < rows.Length; row++)
        {
            rows[row] = Air;
        }

        rows[doorRow] = "..D.............";
        rows[10] = "............E...";
        rows[11] = Ground;
        rows[12] = Ground;
        return rows;
    }

    private static HerdBoard Load(string[] map, string skills, int count = 1, int release = 18)
    {
        var board = new HerdBoard();
        board.Load(new HerdLevel(count, 1, release, 300, skills, map), GameRandom.FromSeed(5));
        return board;
    }

    private static Rect Cells(int firstColumn, int firstRow, int lastColumn, int lastRow) =>
        new(HerdBoard.CellPoint(firstColumn, firstRow), HerdBoard.CellPoint(lastColumn + 1, lastRow + 1));

    private static void RunUntil(HerdBoard board, Func<bool> condition)
    {
        for (var tick = 0; tick < 5000; tick++)
        {
            if (condition())
            {
                return;
            }

            board.Tick();
        }

        Assert.Fail("condition never held");
    }

    private static string Trace(HerdBoard board)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < board.MoogleCount; index++)
        {
            ref readonly var moogle = ref board.Moogle(index);
            builder.Append(moogle.Column).Append(',').Append(moogle.Row).Append(',').Append(moogle.Action).Append(',')
                .Append(moogle.Variant).Append(';');
        }

        return builder.ToString();
    }

    private static int Solve(HerdBoard board, HerdLevel level, string solution, ulong seed, out string log)
    {
        board.Load(level, GameRandom.FromSeed(seed));
        var steps = solution.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var done = new bool[steps.Length];
        var builder = new StringBuilder();
        while (!board.Over && board.Ticks < 20000)
        {
            for (var index = 0; index < steps.Length; index++)
            {
                if (done[index])
                {
                    continue;
                }

                var step = steps[index];
                var sequential = step[0] == '+';
                if (sequential && !done[index - 1])
                {
                    continue;
                }

                if (!TryStep(board, sequential ? step.Substring(1) : step))
                {
                    continue;
                }

                done[index] = true;
                builder.Append(step).Append('@').Append(board.Ticks).Append(' ');
            }

            board.Tick();
        }

        for (var index = 0; index < steps.Length; index++)
        {
            if (!done[index])
            {
                builder.Append("UNUSED ").Append(steps[index]).Append(' ');
            }
        }

        log = builder.ToString();
        return board.SavedCount;
    }

    private static bool TryStep(HerdBoard board, string step)
    {
        var skill = step[0] switch
        {
            'B' => HerdSkill.Block,
            'D' => HerdSkill.Dig,
            'R' => HerdSkill.Bridge,
            'C' => HerdSkill.Climb,
            'F' => HerdSkill.Float,
            _ => HerdSkill.Bash,
        };
        var body = step.Substring(1);
        var target = -1;
        var indexMark = body.IndexOf('#');
        if (indexMark >= 0)
        {
            target = int.Parse(body.Substring(indexMark + 1));
            body = body.Substring(0, indexMark);
        }

        var parts = body.Split(',');
        var column = parts[0] != "*" ? int.Parse(parts[0]) : -1;
        var row = parts.Length > 1 && parts[1] != "*" ? int.Parse(parts[1]) : -1;
        var direction = parts.Length > 2 ? int.Parse(parts[2]) : 0;
        for (var index = 0; index < board.MoogleCount; index++)
        {
            ref readonly var moogle = ref board.Moogle(index);
            if ((target >= 0 && index != target) || (column >= 0 && moogle.Column != column) ||
                (row >= 0 && moogle.Row != row) || (direction != 0 && moogle.Direction != direction))
            {
                continue;
            }

            if (board.Assign(index, skill))
            {
                return true;
            }
        }

        return false;
    }
}
