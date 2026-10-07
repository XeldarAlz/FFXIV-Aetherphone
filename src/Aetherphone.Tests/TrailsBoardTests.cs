using System.Globalization;
using System.Text;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Trails;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TrailsBoardTests
{
    private const float Frame = 1f / 60f;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(808, out var firstTrace);
        var second = Play(808, out var secondTrace);
        Play(91, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.PlayerWins, second.PlayerWins);
        Assert.Equal(first.PlayerLosses, second.PlayerLosses);
        Assert.Equal(first.Round, second.Round);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    [Fact]
    public void RidingIntoATrailCrashes()
    {
        var board = Arena(1, 2);
        board.PlaceRider(TrailsBoard.Player, 5, 30, Heading.Up, false);
        board.PlaceRider(1, 10, 10, Heading.Right, false);
        board.PlaceRider(2, 20, 20, Heading.Down, false);
        board.Block(11, 10);

        board.Tick();

        Assert.False(board.RiderAt(1).Alive);
        Assert.Equal(1, board.CrashCount);
        Assert.Equal(1, board.CrashAt(0).Rider);
        Assert.True(board.RiderAt(TrailsBoard.Player).Alive);
        Assert.Equal(RoundPhase.Racing, board.Phase);
    }

    [Fact]
    public void RidingIntoTheWallCrashes()
    {
        var board = Arena(2, 2);
        board.PlaceRider(TrailsBoard.Player, 5, 30, Heading.Up, false);
        board.PlaceRider(1, TrailsBoard.Columns - 1, 10, Heading.Right, false);
        board.PlaceRider(2, 20, 20, Heading.Down, false);

        board.Tick();

        Assert.False(board.RiderAt(1).Alive);
        Assert.True(board.RiderAt(2).Alive);
    }

    [Fact]
    public void RidersMeetingInTheSameCellBothCrashAndTheRoundIsDrawn()
    {
        var board = Arena(3, 1);
        board.PlaceRider(TrailsBoard.Player, 5, 5, Heading.Right, false);
        board.PlaceRider(1, 7, 5, Heading.Left, false);

        board.Tick();

        Assert.False(board.RiderAt(TrailsBoard.Player).Alive);
        Assert.False(board.RiderAt(1).Alive);
        Assert.Equal(RoundResult.Drawn, board.LastResult);
        Assert.Equal(0, board.PlayerWins);
        Assert.Equal(0, board.PlayerLosses);
        Assert.Equal(RoundPhase.Ended, board.Phase);
    }

    [Fact]
    public void OutlastingEveryBotWinsTheRound()
    {
        var board = Arena(4, 2);
        board.PlaceRider(TrailsBoard.Player, 5, 30, Heading.Up, false);
        board.PlaceRider(1, TrailsBoard.Columns - 1, 10, Heading.Right, false);
        board.PlaceRider(2, 0, 20, Heading.Left, false);

        board.Tick();

        Assert.True(board.RoundEndedThisFrame);
        Assert.Equal(RoundResult.Won, board.LastResult);
        Assert.Equal(1, board.PlayerWins);
        Assert.Equal(RoundPhase.Ended, board.Phase);
        Assert.Equal(MatchVerdict.Ongoing, board.Verdict);
    }

    [Fact]
    public void CrashingWhileABotRidesOnLosesTheRound()
    {
        var board = Arena(5, 2);
        board.PlaceRider(TrailsBoard.Player, 0, 30, Heading.Left, false);
        board.PlaceRider(1, 10, 10, Heading.Right, false);
        board.PlaceRider(2, 20, 20, Heading.Down, false);

        board.Tick();

        Assert.Equal(RoundResult.Lost, board.LastResult);
        Assert.Equal(1, board.PlayerLosses);
        Assert.Equal(0, board.PlayerWins);
    }

    [Fact]
    public void ABotRidingIntoYourTrailIsATakedown()
    {
        var board = Arena(6, 2);
        board.PlaceRider(TrailsBoard.Player, 5, 10, Heading.Up, false);
        board.PlaceRider(1, 4, 10, Heading.Right, false);
        board.PlaceRider(2, 20, 20, Heading.Down, false);

        board.Tick();

        Assert.False(board.RiderAt(1).Alive);
        Assert.True(board.CrashAt(0).Takedown);
        Assert.Equal(1, board.Takedowns);
        Assert.True(board.RiderAt(TrailsBoard.Player).Alive);
    }

    [Fact]
    public void ACrashedRidersTrailFadesAndFreesItsCells()
    {
        var board = Arena(7, 2);
        board.PlaceRider(TrailsBoard.Player, 5, 30, Heading.Up, false);
        board.PlaceRider(1, 10, 10, Heading.Right, false);
        board.PlaceRider(2, TrailsBoard.Columns - 1, 5, Heading.Right, false);
        board.Tick();
        Assert.False(board.RiderAt(2).Alive);
        Assert.Equal(3, board.OwnerAt(TrailsBoard.Columns - 1, 5));

        board.Step(TrailsBoard.DerezzSeconds + 0.05f);

        Assert.Equal(0, board.OwnerAt(TrailsBoard.Columns - 1, 5));
        Assert.True(board.RiderAt(TrailsBoard.Player).Alive);
        Assert.Equal(RoundPhase.Racing, board.Phase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void BotsNeverTurnIntoAnImmediateWallWhenAnExitExists(int skillIndex)
    {
        var skill = (BotSkill)skillIndex;
        var board = Arena(8, 1, skill);
        board.PlaceRider(1, TrailsBoard.Columns - 1, 10, Heading.Right, true);
        var choice = board.ChooseDirection(1);
        Assert.True(choice is Heading.Up or Heading.Down);

        board = Arena(9, 1, skill);
        board.PlaceRider(1, 5, 5, Heading.Up, true);
        board.Block(5, 4);
        board.Block(4, 5);
        Assert.Equal(Heading.Right, board.ChooseDirection(1));

        var random = GameRandom.FromSeed(10);
        for (var trial = 0; trial < 300; trial++)
        {
            board = Arena((ulong)(100 + trial), 1, skill);
            for (var cell = 0; cell < TrailsBoard.CellCount; cell++)
            {
                if (random.Chance(0.35f))
                {
                    board.Block(cell % TrailsBoard.Columns, cell / TrailsBoard.Columns);
                }
            }

            var column = random.Next(TrailsBoard.Columns);
            var row = random.Next(TrailsBoard.Rows);
            var heading = (Heading)random.Next(4);
            board.PlaceRider(1, column, row, heading, true);
            var exits = 0;
            exits += Free(board, column, row, heading) ? 1 : 0;
            exits += Free(board, column, row, TrailsBoard.TurnLeft(heading)) ? 1 : 0;
            exits += Free(board, column, row, TrailsBoard.TurnRight(heading)) ? 1 : 0;
            var chosen = board.ChooseDirection(1);
            Assert.False(TrailsBoard.Opposite(chosen, heading));
            if (exits > 0)
            {
                Assert.True(Free(board, column, row, chosen));
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void BotsStayOutOfAOneCellPocket(int skillIndex)
    {
        var board = Arena(11, 1, (BotSkill)skillIndex);
        board.PlaceRider(1, 5, 20, Heading.Up, true);
        board.Block(5, 19);
        board.Block(3, 20);
        board.Block(4, 19);
        board.Block(4, 21);

        Assert.Equal(Heading.Right, board.ChooseDirection(1));
    }

    [Fact]
    public void HardBotsDodgeAHeadOnWhenAnotherWayIsOpen()
    {
        var board = Arena(12, 1, BotSkill.Hard);
        board.PlaceRider(TrailsBoard.Player, 12, 10, Heading.Left, false);
        board.PlaceRider(1, 10, 10, Heading.Right, true);

        var choice = board.ChooseDirection(1);

        Assert.True(choice is Heading.Up or Heading.Down);
    }

    [Fact]
    public void TheFirstToThreeRoundsTakesTheMatch()
    {
        Assert.Equal(MatchVerdict.Won, TrailsBoard.Resolve(3, 1, 4));
        Assert.Equal(MatchVerdict.Lost, TrailsBoard.Resolve(1, 3, 4));
        Assert.Equal(MatchVerdict.Ongoing, TrailsBoard.Resolve(2, 2, 4));
        Assert.Equal(MatchVerdict.Ongoing, TrailsBoard.Resolve(0, 0, 6));
        Assert.Equal(MatchVerdict.Won, TrailsBoard.Resolve(2, 1, TrailsBoard.MaxRounds));
        Assert.Equal(MatchVerdict.Lost, TrailsBoard.Resolve(1, 2, TrailsBoard.MaxRounds));
        Assert.Equal(MatchVerdict.Drawn, TrailsBoard.Resolve(2, 2, TrailsBoard.MaxRounds));
    }

    [Fact]
    public void ThePlayerTurnQueueIgnoresReversalsAndAppliesOnTheNextTick()
    {
        var board = Arena(13, 1);
        board.PlaceRider(TrailsBoard.Player, 10, 10, Heading.Up, false);
        board.PlaceRider(1, 20, 30, Heading.Up, false);

        board.QueueHeading(Heading.Down);
        board.Tick();
        Assert.Equal(Heading.Up, board.RiderAt(TrailsBoard.Player).Heading);
        Assert.Equal(9, board.RiderAt(TrailsBoard.Player).Row);

        board.QueueTurn(-1);
        board.QueueTurn(-1);
        board.Tick();
        Assert.Equal(Heading.Left, board.RiderAt(TrailsBoard.Player).Heading);
        Assert.Equal(9, board.RiderAt(TrailsBoard.Player).Column);
        board.Tick();
        Assert.Equal(Heading.Down, board.RiderAt(TrailsBoard.Player).Heading);
        Assert.Equal(10, board.RiderAt(TrailsBoard.Player).Row);
    }

    [Fact]
    public void AMatchAlwaysEndsWithAVerdict()
    {
        var board = new TrailsBoard();
        board.Reset(GameRandom.FromSeed(14), 3, BotSkill.Hard, 11f, true);
        for (var frame = 0; frame < 60 * 60 * 20 && board.Phase != RoundPhase.MatchOver; frame++)
        {
            board.Step(Frame);
        }

        Assert.Equal(RoundPhase.MatchOver, board.Phase);
        Assert.NotEqual(MatchVerdict.Ongoing, board.Verdict);
        Assert.True(board.PlayerWins == TrailsBoard.WinsNeeded || board.PlayerLosses == TrailsBoard.WinsNeeded ||
                    board.Round == TrailsBoard.MaxRounds);
    }

    [Fact]
    public void EasyBotsAreBeatableAndHardBotsAreNot()
    {
        var easyWins = AttentiveMatchesWon(2, BotSkill.Easy, 9f, 24);
        var hardWins = AttentiveMatchesWon(3, BotSkill.Hard, 11f, 24);

        Assert.True(easyWins >= 9, $"An attentive player won only {easyWins} of 24 Easy matches.");
        Assert.True(hardWins <= 4, $"An attentive player won {hardWins} of 24 Hard matches.");
    }

    private static TrailsBoard Arena(ulong seed, int bots, BotSkill skill = BotSkill.Easy)
    {
        var board = new TrailsBoard();
        board.Reset(GameRandom.FromSeed(seed), bots, skill, 10f);
        board.ClearArena();
        return board;
    }

    private static bool Free(TrailsBoard board, int column, int row, Heading heading) =>
        board.IsFree(column + TrailsBoard.DeltaX(heading), row + TrailsBoard.DeltaY(heading));

    private static int AttentiveMatchesWon(int bots, BotSkill skill, float speed, int matches)
    {
        var sight = new TrailsBot();
        var won = 0;
        for (var match = 0; match < matches; match++)
        {
            var board = new TrailsBoard();
            board.Reset(GameRandom.FromSeed((ulong)(match * 7919 + 13)), bots, skill, speed);
            for (var frame = 0; frame < 60 * 60 * 20 && board.Phase != RoundPhase.MatchOver; frame++)
            {
                if (board.Phase == RoundPhase.Racing && board.PlayerAlive)
                {
                    SteerAttentively(board, sight);
                }

                board.Step(Frame);
            }

            if (board.Verdict == MatchVerdict.Won)
            {
                won++;
            }
        }

        return won;
    }

    private static void SteerAttentively(TrailsBoard board, TrailsBot sight)
    {
        ref readonly var player = ref board.RiderAt(TrailsBoard.Player);
        var heading = player.Heading;
        var best = heading;
        var bestSpace = Space(board, sight, player.Column, player.Row, heading);
        var left = TrailsBoard.TurnLeft(heading);
        var right = TrailsBoard.TurnRight(heading);
        var leftSpace = Space(board, sight, player.Column, player.Row, left);
        var rightSpace = Space(board, sight, player.Column, player.Row, right);
        if (leftSpace > bestSpace)
        {
            best = left;
            bestSpace = leftSpace;
        }

        if (rightSpace > bestSpace)
        {
            best = right;
        }

        if (best != heading)
        {
            board.QueueHeading(best);
        }
    }

    private static int Space(TrailsBoard board, TrailsBot sight, int column, int row, Heading heading)
    {
        var nextColumn = column + TrailsBoard.DeltaX(heading);
        var nextRow = row + TrailsBoard.DeltaY(heading);
        return board.IsFree(nextColumn, nextRow) ? sight.Flood(board, nextColumn, nextRow, 200) : -1;
    }

    private static TrailsBoard Play(ulong seed, out string trace)
    {
        var board = new TrailsBoard();
        board.Reset(GameRandom.FromSeed(seed), 2, BotSkill.Easy, 9f);
        var builder = new StringBuilder();
        for (var frame = 0; frame < 60 * 60 && board.Phase != RoundPhase.MatchOver; frame++)
        {
            if (frame % 37 == 0)
            {
                board.QueueTurn(frame / 37 % 2 == 0 ? -1 : 1);
            }

            board.Step(Frame);
            if (frame % 30 != 0)
            {
                continue;
            }

            builder.Append(board.Round).Append(',').Append((int)board.Phase).Append(',').Append(board.PlayerWins)
                .Append(',').Append(board.PlayerLosses);
            for (var index = 0; index < board.RiderCount; index++)
            {
                var head = board.HeadPosition(index);
                builder.Append(',').Append(head.X.ToString("F2", CultureInfo.InvariantCulture)).Append(':')
                    .Append(head.Y.ToString("F2", CultureInfo.InvariantCulture));
            }

            builder.Append(';');
        }

        trace = builder.ToString();
        return board;
    }
}
