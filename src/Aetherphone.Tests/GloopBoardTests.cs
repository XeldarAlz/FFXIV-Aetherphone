using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Gloop;
using Xunit;

namespace Aetherphone.Tests;

public sealed class GloopBoardTests
{
    private const float Frame = 1f / 60f;
    private const int MaxFrames = 2400;
    private const byte Red = 1;
    private const byte Green = 2;
    private const byte Blue = 3;
    private const byte Yellow = 4;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = PlayWithBot(1234, BotSkill.Hard, 1800);
        var second = PlayWithBot(1234, BotSkill.Hard, 1800);
        var other = PlayWithBot(99, BotSkill.Hard, 1800);

        Assert.Equal(first.Grid.ToArray(), second.Grid.ToArray());
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Pieces, second.Pieces);
        Assert.Equal(first.BestChain, second.BestChain);
        Assert.True(first.Pieces > 10);
        Assert.False(first.Grid.SequenceEqual(other.Grid) && first.Score == other.Score);
    }

    [Fact]
    public void FourConnectedOfAColorPopAndThreeOrDiagonalsDoNot()
    {
        var grid = Grid(
            "R.....",
            "RR..G.",
            "R..G.G");
        var pop = new bool[GloopRules.Cells];
        var group = new int[GloopRules.Cells];
        var seen = new bool[GloopRules.Cells];

        var popped = GloopRules.FindPops(grid, pop, group, seen, out var colorMask, out var groupBonus);

        Assert.Equal(4, popped);
        Assert.Equal(1 << Red, colorMask);
        Assert.Equal(0, groupBonus);
        Assert.True(pop[GloopRules.Index(0, 10)]);
        Assert.True(pop[GloopRules.Index(1, 11)]);
        Assert.False(pop[GloopRules.Index(4, 11)]);
        Assert.False(pop[GloopRules.Index(3, 12)]);
    }

    [Fact]
    public void FloodFillCountsTheWholeGroupButNeverTheHiddenRow()
    {
        var grid = new byte[GloopRules.Cells];
        grid[GloopRules.Index(0, 0)] = Blue;
        grid[GloopRules.Index(0, 1)] = Blue;
        grid[GloopRules.Index(0, 2)] = Blue;
        grid[GloopRules.Index(1, 2)] = Blue;
        var group = new int[GloopRules.Cells];
        var seen = new bool[GloopRules.Cells];

        var size = GloopRules.FloodFill(grid, GloopRules.Index(0, 1), Blue, group, seen);

        Assert.Equal(3, size);
        Assert.False(seen[GloopRules.Index(0, 0)]);
    }

    [Fact]
    public void RocksNextToAPopBreakAndOthersStay()
    {
        var grid = Grid("RRRR##");
        var pop = new bool[GloopRules.Cells];

        GloopRules.FindPops(grid, pop, new int[GloopRules.Cells], new bool[GloopRules.Cells], out _, out _);

        Assert.True(pop[GloopRules.Index(4, 12)]);
        Assert.False(pop[GloopRules.Index(5, 12)]);
    }

    [Fact]
    public void ChainPowerAndBonusesFollowTheTable()
    {
        int[] expected = { 0, 8, 16, 32, 64, 96, 128, 160 };
        for (var chain = 1; chain <= expected.Length; chain++)
        {
            Assert.Equal(expected[chain - 1], GloopRules.ChainPowerFor(chain));
        }

        Assert.Equal(40, GloopRules.StepScore(4, 1, 1, 0));
        Assert.Equal(320, GloopRules.StepScore(4, 2, 1, 0));
        Assert.Equal(10 * 8 * (16 + 3), GloopRules.StepScore(8, 3, 2, 0));
        Assert.Equal(0, GloopRules.GroupBonus(4));
        Assert.Equal(2, GloopRules.GroupBonus(5));
        Assert.Equal(10, GloopRules.GroupBonus(14));
    }

    [Fact]
    public void ResolvingACascadeScoresEachLinkInOrder()
    {
        var grid = Grid(
            "GG....",
            "RR....",
            "RRGG..");
        var pop = new bool[GloopRules.Cells];

        var score = GloopRules.Resolve(grid, pop, new int[GloopRules.Cells], new bool[GloopRules.Cells],
            new byte[GloopRules.Cells], out var chains);

        Assert.Equal(2, chains);
        Assert.Equal(40 + 320, score);
        Assert.True(GloopRules.IsClear(grid));
    }

    [Fact]
    public void ADroppedPieceSetsOffChainsInOrderOnTheBoard()
    {
        var board = Fresh(false);
        board.Load(Grid(
            "G.....",
            "RG....",
            "RR.GGB"));
        board.SetPiece(Red, Green);
        board.HardDrop();
        var afterDrop = board.Score;
        var chains = new List<int>();

        RunUntilFalling(board, chains);

        Assert.Equal(new[] { 1, 2 }, chains);
        Assert.Equal(40 + 10 * 5 * (8 + GloopRules.GroupBonus(5)), board.Score - afterDrop);
        Assert.Equal(2, board.BestChain);
        Assert.Equal(GloopPhase.Falling, board.Phase);
    }

    [Fact]
    public void ClearingTheWholeWellPaysTheAllClearBonus()
    {
        var board = Fresh(false);
        board.Load(Grid("RR...."));
        board.SetPiece(Red, Red);
        board.HardDrop();
        var afterDrop = board.Score;
        var sawAllClear = false;

        for (var frame = 0; frame < MaxFrames && !board.Falling; frame++)
        {
            board.Step(Frame);
            sawAllClear |= board.AllClearNow;
        }

        Assert.True(sawAllClear);
        Assert.Equal(40 + GloopRules.AllClearBonus, board.Score - afterDrop);
    }

    [Fact]
    public void IncomingGarbageDropsAfterThePieceSettles()
    {
        var board = Fresh(true);
        board.AddIncoming(14);
        board.SetPiece(Red, Green);
        board.HardDrop();
        var dropped = 0;

        for (var frame = 0; frame < MaxFrames && !board.Falling; frame++)
        {
            board.Step(Frame);
            dropped += board.GarbageDropped;
        }

        Assert.Equal(14, dropped);
        Assert.Equal(0, board.Incoming);
        Assert.Equal(14, CountRocks(board));
        for (var column = 0; column < GloopRules.Columns; column++)
        {
            var rocks = CountRocks(board, column);
            Assert.InRange(rocks, 2, 3);
        }
    }

    [Fact]
    public void AtMostThirtyRocksFallAtOnce()
    {
        var board = Fresh(true);
        board.AddIncoming(45);
        board.SetPiece(Red, Green);
        board.HardDrop();

        for (var frame = 0; frame < MaxFrames && !board.Falling; frame++)
        {
            board.Step(Frame);
        }

        Assert.Equal(GloopRules.MaxGarbageDrop, CountRocks(board));
        Assert.Equal(15, board.Incoming);
    }

    [Fact]
    public void ChainsCancelIncomingGarbageBeforeSendingAny()
    {
        var board = Fresh(true);
        board.Load(Grid(
            "G.....",
            "RG....",
            "RR.GGB"));
        board.AddIncoming(5);
        board.SetPiece(Red, Green);
        board.HardDrop();

        RunUntilFalling(board, new List<int>());

        var produced = (40 + 10 * 5 * (8 + GloopRules.GroupBonus(5))) / GloopRules.NuisanceRate;
        Assert.Equal(0, board.Incoming);
        Assert.Equal(produced - 5, board.TakeOutgoing());
        Assert.Equal(produced - 5, board.Sent);
        Assert.Equal(0, CountRocks(board));
    }

    [Fact]
    public void PlanningGarbageFillsRowsThenDistinctColumns()
    {
        var random = GameRandom.FromSeed(5);
        var perColumn = new int[GloopRules.Columns];

        GloopRules.PlanGarbage(16, ref random, perColumn);

        var total = 0;
        var extra = 0;
        for (var column = 0; column < GloopRules.Columns; column++)
        {
            Assert.InRange(perColumn[column], 2, 3);
            total += perColumn[column];
            extra += perColumn[column] == 3 ? 1 : 0;
        }

        Assert.Equal(16, total);
        Assert.Equal(4, extra);
    }

    [Fact]
    public void ABlockedSpawnEndsTheGame()
    {
        var board = Fresh(false);
        var rows = new string[GloopRules.VisibleRows - 1];
        for (var row = 0; row < rows.Length; row++)
        {
            rows[row] = row % 2 == 0 ? "..R..." : "..G...";
        }

        board.Load(Grid(rows));
        board.SetPiece(Blue, Yellow);
        board.HardDrop();
        var died = false;

        for (var frame = 0; frame < MaxFrames && !board.Over; frame++)
        {
            board.Step(Frame);
            died |= board.DiedNow;
        }

        Assert.True(died);
        Assert.True(board.Over);
    }

    [Fact]
    public void RotatingIntoAWallKicksAndANarrowGapFlips()
    {
        var board = Fresh(false);
        for (var step = 0; step < 3; step++)
        {
            Assert.True(board.Shift(1));
        }

        Assert.False(board.Shift(1));
        Assert.True(board.Rotate(1));
        Assert.Equal(1, board.PieceOrientation);
        Assert.Equal(4, board.PieceColumn);

        var narrow = Fresh(false);
        var rows = new string[GloopRules.VisibleRows];
        for (var row = 0; row < rows.Length; row++)
        {
            rows[row] = row % 2 == 0 ? ".R.R.." : ".G.G..";
        }

        narrow.Load(Grid(rows));
        Assert.True(narrow.Rotate(1));
        Assert.Equal(2, narrow.PieceOrientation);
        Assert.Equal(GloopRules.SpawnColumn, narrow.PieceColumn);
    }

    [Fact]
    public void TheBotPlansTheSameDropsForTheSameSeed()
    {
        var first = PlayWithBot(77, BotSkill.Easy, 1500);
        var second = PlayWithBot(77, BotSkill.Easy, 1500);

        Assert.Equal(first.Grid.ToArray(), second.Grid.ToArray());
        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Pieces, second.Pieces);
    }

    [Fact]
    public void AHardBotTakesAnOpenPop()
    {
        var board = Fresh(false);
        board.Load(Grid("RRR..."));
        board.SetPiece(Red, Green);
        board.SetNext(0, Blue, Yellow);
        var bot = new GloopBot();
        bot.Reset(GameRandom.FromSeed(3), BotSkill.Hard);

        bot.Plan(board);

        var grid = board.Grid.ToArray();
        GloopRules.Place(grid, bot.TargetColumn, bot.TargetOrientation, Red, Green);
        GloopRules.Resolve(grid, new bool[GloopRules.Cells], new int[GloopRules.Cells], new bool[GloopRules.Cells],
            new byte[GloopRules.Cells], out var chains);
        Assert.True(chains >= 1);
    }

    [Fact]
    public void ADrivenBotLandsItsPlannedPlacement()
    {
        var board = Fresh(false);
        board.Load(Grid("RRR..."));
        board.SetPiece(Red, Green);
        board.SetNext(0, Blue, Yellow);
        var bot = new GloopBot();
        bot.Reset(GameRandom.FromSeed(8), BotSkill.Hard);
        var pieces = board.Pieces;

        for (var frame = 0; frame < MaxFrames && board.Pieces == pieces; frame++)
        {
            bot.Drive(board, Frame);
            board.Step(Frame);
        }

        RunUntilFalling(board, new List<int>());

        Assert.Equal(pieces + 1, board.Pieces);
        Assert.True(board.Popped >= 4);
    }

    private static GloopBoard Fresh(bool versus)
    {
        var board = new GloopBoard();
        board.Reset(GameRandom.FromSeed(1), GameRandom.FromSeed(2), versus);
        return board;
    }

    private static GloopBoard PlayWithBot(ulong seed, BotSkill skill, int frames)
    {
        var board = new GloopBoard();
        board.Reset(GameRandom.FromSeed(seed), GameRandom.FromSeed(seed + 1), false);
        board.Gravity = 4f;
        var bot = new GloopBot();
        bot.Reset(GameRandom.FromSeed(seed + 2), skill);
        for (var frame = 0; frame < frames && !board.Over; frame++)
        {
            bot.Drive(board, Frame);
            board.Step(Frame);
        }

        return board;
    }

    private static void RunUntilFalling(GloopBoard board, List<int> chains)
    {
        for (var frame = 0; frame < MaxFrames && !board.Falling; frame++)
        {
            board.Step(Frame);
            if (board.PoppedCount > 0)
            {
                chains.Add(board.PopChain);
            }
        }
    }

    private static int CountRocks(GloopBoard board, int column = -1)
    {
        var count = 0;
        for (var cell = 0; cell < GloopRules.Cells; cell++)
        {
            if (board.Cell(cell) == GloopRules.Rock && (column < 0 || GloopRules.ColumnOf(cell) == column))
            {
                count++;
            }
        }

        return count;
    }

    private static byte[] Grid(params string[] bottomRows)
    {
        var grid = new byte[GloopRules.Cells];
        for (var line = 0; line < bottomRows.Length; line++)
        {
            var row = GloopRules.Rows - bottomRows.Length + line;
            var text = bottomRows[line];
            for (var column = 0; column < GloopRules.Columns; column++)
            {
                grid[GloopRules.Index(column, row)] = text[column] switch
                {
                    'R' => Red,
                    'G' => Green,
                    'B' => Blue,
                    'Y' => Yellow,
                    '#' => GloopRules.Rock,
                    _ => GloopRules.Empty,
                };
            }
        }

        return grid;
    }
}
