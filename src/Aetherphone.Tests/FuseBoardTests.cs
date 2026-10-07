using System.Globalization;
using System.Numerics;
using System.Text;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Fuse;
using Xunit;

namespace Aetherphone.Tests;

public sealed class FuseBoardTests
{
    private const float Frame = 1f / 60f;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(2024, out var firstTrace);
        var second = Play(2024, out var secondTrace);
        Play(77, out var otherTrace);

        Assert.Equal(firstTrace, secondTrace);
        Assert.Equal(first.Round, second.Round);
        Assert.Equal(first.PlayerCrates, second.PlayerCrates);
        Assert.Equal(first.Verdict, second.Verdict);
        Assert.NotEqual(firstTrace, otherTrace);
    }

    [Fact]
    public void BlastsStopAtPillarsAndBreakOnlyTheFirstCrate()
    {
        var board = Sandbox();
        board.SetTile(4, 2, FuseTile.Crate);
        board.SetTile(5, 2, FuseTile.Crate);
        board.PlaceBomb(2, 2, FuseBoard.NoMoogle, 3, 0.05f);
        board.PlaceBomb(9, 4, FuseBoard.NoMoogle, 3, 0.05f);

        board.Step(0.1f);

        Assert.Equal(2, board.BlastCount);
        Assert.True(Burning(board, 2, 2));
        Assert.True(Burning(board, 2, 0));
        Assert.True(Burning(board, 0, 2));
        Assert.True(Burning(board, 2, 5));
        Assert.False(Burning(board, 2, 6));
        Assert.True(Burning(board, 3, 2));
        Assert.True(Burning(board, 4, 2));
        Assert.False(Burning(board, 5, 2));
        Assert.Equal(FuseTile.Floor, board.Tile(4, 2));
        Assert.Equal(FuseTile.Crate, board.Tile(5, 2));
        Assert.Equal(1, board.BrokenCrates.Count);

        var pillared = board.BlastAt(1);
        Assert.Equal(FuseBoard.CellIndex(9, 4), pillared.Cell);
        Assert.Equal(0, pillared.Up);
        Assert.Equal(0, pillared.Down);
        Assert.Equal(3, pillared.Left);
        Assert.Equal(3, pillared.Right);
        Assert.False(Burning(board, 9, 3));
        Assert.False(Burning(board, 9, 5));
        Assert.Equal(FuseTile.Pillar, board.Tile(9, 3));
    }

    [Fact]
    public void ABlastSetsOffEveryBombItReaches()
    {
        var board = Sandbox();
        var first = board.PlaceBomb(0, 0, FuseBoard.NoMoogle, 4, 0.05f);
        var second = board.PlaceBomb(2, 0, FuseBoard.NoMoogle, 3, 5f);
        var third = board.PlaceBomb(2, 3, FuseBoard.NoMoogle, 1, 5f);

        board.Step(0.1f);

        Assert.False(board.BombAt(first).Alive);
        Assert.False(board.BombAt(second).Alive);
        Assert.False(board.BombAt(third).Alive);
        Assert.Equal(3, board.BlastCount);
        Assert.Equal(0, board.BlastAt(0).Chain);
        Assert.Equal(2, board.BlastAt(0).Right);
        Assert.Equal(1, board.BlastAt(1).Chain);
        Assert.Equal(3, board.BlastAt(1).Down);
        Assert.Equal(2, board.BlastAt(2).Chain);
        Assert.True(Burning(board, 5, 0));
        Assert.True(Burning(board, 2, 3));
        Assert.True(Burning(board, 2, 4));
        Assert.False(Burning(board, 2, 5));
        Assert.False(Burning(board, 6, 0));
    }

    [Fact]
    public void ChainsReachTheDangerMapBeforeTheirOwnFuse()
    {
        var board = Sandbox();
        board.PlaceBomb(0, 0, FuseBoard.NoMoogle, 4, 0.5f);
        board.PlaceBomb(2, 0, FuseBoard.NoMoogle, 3, 1.9f);
        board.PlaceMoogle(1, 12, 10, true);

        board.Bot.Prepare(board);

        Assert.Equal(0.5f, board.Bot.Danger(FuseBoard.CellIndex(2, 3)), 3);
        Assert.Equal(0.5f, board.Bot.Danger(FuseBoard.CellIndex(5, 0)), 3);
        Assert.Equal(FuseBot.Never, board.Bot.Danger(FuseBoard.CellIndex(6, 0)));
    }

    [Fact]
    public void CrateDropsAreSeeded()
    {
        var first = new FuseBoard();
        var second = new FuseBoard();
        var other = new FuseBoard();
        first.Reset(GameRandom.FromSeed(5), FuseSkill.Easy);
        second.Reset(GameRandom.FromSeed(5), FuseSkill.Hard);
        other.Reset(GameRandom.FromSeed(6), FuseSkill.Easy);
        var differs = false;
        for (var cell = 0; cell < FuseBoard.CellCount; cell++)
        {
            Assert.Equal(first.Tile(cell), second.Tile(cell));
            Assert.Equal(first.HiddenAt(cell), second.HiddenAt(cell));
            differs |= first.Tile(cell) != other.Tile(cell) || first.HiddenAt(cell) != other.HiddenAt(cell);
        }

        Assert.True(differs);
        var crates = 0;
        var drops = 0;
        Span<int> kinds = stackalloc int[5];
        for (var seed = 1; seed <= 200; seed++)
        {
            var board = new FuseBoard();
            board.Reset(GameRandom.FromSeed((ulong)seed), FuseSkill.Easy);
            for (var cell = 0; cell < FuseBoard.CellCount; cell++)
            {
                var column = FuseBoard.ColumnOf(cell);
                var row = FuseBoard.RowOf(cell);
                if (FuseBoard.InStartZone(column, row))
                {
                    Assert.Equal(FuseTile.Floor, board.Tile(cell));
                }

                Assert.Equal(column % 2 == 1 && row % 2 == 1, board.Tile(cell) == FuseTile.Pillar);
                if (board.Tile(cell) != FuseTile.Crate)
                {
                    Assert.Equal(PowerUp.None, board.HiddenAt(cell));
                    continue;
                }

                crates++;
                if (board.HiddenAt(cell) == PowerUp.None)
                {
                    continue;
                }

                drops++;
                kinds[(int)board.HiddenAt(cell)]++;
            }
        }

        var rate = drops / (float)crates;
        Assert.InRange(rate, 0.28f, 0.40f);
        Assert.True(kinds[(int)PowerUp.ExtraBomb] > 0 && kinds[(int)PowerUp.Range] > 0 &&
                    kinds[(int)PowerUp.Speed] > 0 && kinds[(int)PowerUp.Kick] > 0);
    }

    [Fact]
    public void ABrokenCrateRevealsItsDropOnceTheFlamesDieAndALaterBlastBurnsIt()
    {
        var board = Sandbox();
        board.SetTile(4, 0, FuseTile.Crate);
        board.SetHidden(4, 0, PowerUp.Range);
        board.PlaceBomb(2, 0, FuseBoard.NoMoogle, 3, 0.05f);

        board.Step(0.1f);
        Assert.Equal(FuseTile.Floor, board.Tile(4, 0));
        Assert.Equal(PowerUp.None, board.ItemAt(FuseBoard.CellIndex(4, 0)));

        StepFor(board, FuseBoard.FireSeconds + 0.1f);
        Assert.Equal(PowerUp.Range, board.ItemAt(FuseBoard.CellIndex(4, 0)));

        board.PlaceBomb(2, 0, FuseBoard.NoMoogle, 3, 0.05f);
        board.Step(0.1f);
        Assert.Equal(PowerUp.None, board.ItemAt(FuseBoard.CellIndex(4, 0)));
        Assert.Equal(1, board.BurnedItems.Count);
    }

    [Fact]
    public void WalkingOverAPowerUpCollectsIt()
    {
        var board = Sandbox();
        board.PlaceMoogle(FuseBoard.Player, 0, 0, false);
        board.SetItem(1, 0, PowerUp.Range);
        board.SetItem(2, 0, PowerUp.ExtraBomb);
        board.SetItem(3, 0, PowerUp.Speed);
        board.SetItem(4, 0, PowerUp.Kick);

        board.SetPlayerInput(FuseDirection.Right, false);
        StepFor(board, 1.4f);

        ref readonly var moogle = ref board.MoogleAt(FuseBoard.Player);
        Assert.Equal(FuseBoard.StartRange + 1, moogle.Range);
        Assert.Equal(FuseBoard.StartBombs + 1, moogle.Bombs);
        Assert.Equal(1, moogle.SpeedLevel);
        Assert.True(moogle.Kick);
        Assert.Equal(4, board.PlayerPowerUps);
        Assert.Equal(PowerUp.None, board.ItemAt(FuseBoard.CellIndex(1, 0)));
    }

    [Fact]
    public void AKickedBombSlidesUntilSomethingStopsIt()
    {
        var board = Sandbox();
        board.PlaceMoogle(FuseBoard.Player, 0, 0, false);
        board.Equip(FuseBoard.Player, 1, 2, 0, true);
        var slot = board.PlaceBomb(2, 0, FuseBoard.NoMoogle, 2, 5f);
        board.SetTile(6, 0, FuseTile.Crate);

        board.SetPlayerInput(FuseDirection.Right, false);
        StepFor(board, 0.5f);
        board.SetPlayerInput(FuseDirection.None, false);
        StepFor(board, 1f);

        Assert.True(board.BombAt(slot).Alive);
        Assert.Equal(FuseDirection.None, board.BombAt(slot).Sliding);
        Assert.Equal(FuseBoard.CellIndex(5, 0), board.BombAt(slot).Cell);
        Assert.Equal(slot, board.BombIndexAt(FuseBoard.CellIndex(5, 0)));
        Assert.Equal(-1, board.BombIndexAt(FuseBoard.CellIndex(2, 0)));
    }

    [Fact]
    public void WithoutKickABombBlocksTheWay()
    {
        var board = Sandbox();
        board.PlaceMoogle(FuseBoard.Player, 0, 0, false);
        board.PlaceBomb(2, 0, FuseBoard.NoMoogle, 2, 5f);

        board.SetPlayerInput(FuseDirection.Right, false);
        StepFor(board, 1f);

        Assert.Equal(1.5f, board.MoogleAt(FuseBoard.Player).Position.X, 3);
        Assert.True(board.BombIndexAt(FuseBoard.CellIndex(2, 0)) >= 0);
    }

    [Fact]
    public void CornerAssistSlidesYouRoundAPillarWhenYouAreCloseEnough()
    {
        var board = Sandbox();
        board.PlaceMoogle(FuseBoard.Player, new Vector2(1.8f, 0.5f), false);
        board.SetPlayerInput(FuseDirection.Down, false);
        StepFor(board, 0.6f);

        var assisted = board.MoogleAt(FuseBoard.Player).Position;
        Assert.Equal(2, FuseBoard.ColumnOf(FuseBoard.CellAt(assisted)));
        Assert.True(assisted.Y > 1f);

        board = Sandbox();
        board.PlaceMoogle(FuseBoard.Player, new Vector2(1.6f, 0.5f), false);
        board.SetPlayerInput(FuseDirection.Down, false);
        StepFor(board, 0.6f);

        var blocked = board.MoogleAt(FuseBoard.Player).Position;
        Assert.Equal(1.6f, blocked.X, 3);
        Assert.Equal(0.5f, blocked.Y, 3);
    }

    [Fact]
    public void TurningAtACrossingSnapsToTheTileCentre()
    {
        var board = Sandbox();
        board.PlaceMoogle(FuseBoard.Player, new Vector2(2.2f, 0.5f), false);
        board.SetPlayerInput(FuseDirection.Down, false);
        StepFor(board, 0.5f);

        var position = board.MoogleAt(FuseBoard.Player).Position;
        Assert.Equal(2.5f, position.X, 3);
        Assert.True(position.Y > 1.2f);
    }

    [Fact]
    public void StandingInFireKnocksYouOut()
    {
        var board = Sandbox();
        board.PlaceMoogle(FuseBoard.Player, 0, 0, false);
        board.PlaceMoogle(1, 2, 0, false);
        board.PlaceBomb(2, 0, FuseBoard.Player, 2, 0.05f);

        board.Step(0.1f);

        Assert.False(board.MoogleAt(FuseBoard.Player).Alive);
        Assert.False(board.MoogleAt(1).Alive);
        Assert.Equal(1, board.PlayerKnockouts);
        Assert.Equal(2, board.KnockoutCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void BotsNeverStandInACellThatWillBurnWhenASafeCellIsReachable(int skillIndex)
    {
        var skill = (FuseSkill)skillIndex;
        var reaction = skill == FuseSkill.Easy ? FuseBot.EasyReaction : 0f;
        var random = GameRandom.FromSeed(99);
        var tested = 0;
        for (var trial = 0; trial < 400; trial++)
        {
            var board = new FuseBoard();
            board.Reset(GameRandom.FromSeed((ulong)(trial + 1)), skill);
            board.ClearArena();
            for (var cell = 0; cell < FuseBoard.CellCount; cell++)
            {
                if (board.Tile(cell) == FuseTile.Floor && random.Chance(0.3f))
                {
                    board.SetTile(FuseBoard.ColumnOf(cell), FuseBoard.RowOf(cell), FuseTile.Crate);
                }
            }

            var start = RandomFloor(board, ref random);
            board.SetTile(FuseBoard.ColumnOf(start), FuseBoard.RowOf(start), FuseTile.Floor);
            board.PlaceMoogle(1, FuseBoard.ColumnOf(start), FuseBoard.RowOf(start), true);
            board.Equip(1, 1, 2, random.Next(3), false);
            var bombs = 1 + random.Next(3);
            for (var bomb = 0; bomb < bombs; bomb++)
            {
                var column = Math.Clamp(FuseBoard.ColumnOf(start) + random.Next(-2, 3), 0, FuseBoard.Columns - 1);
                var row = Math.Clamp(FuseBoard.RowOf(start) + random.Next(-2, 3), 0, FuseBoard.Rows - 1);
                if (board.Tile(column, row) != FuseTile.Floor)
                {
                    continue;
                }

                board.PlaceBomb(column, row, 1, 1 + random.Next(3), random.Range(1f, 2f));
            }

            board.Bot.Prepare(board);
            if (board.Bot.IsSafe(board, start) || !SafetyReachable(board, start, board.SpeedOf(1), 0.2f + reaction))
            {
                continue;
            }

            tested++;
            for (var frame = 0; frame < 60 * 4 && Active(board); frame++)
            {
                board.Step(Frame);
            }

            Assert.True(board.MoogleAt(1).Alive, $"Trial {trial}: the bot burned although a safe cell was reachable.");
        }

        Assert.True(tested >= 100, $"Only {tested} trials put the bot in danger.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ASafeBotNeverStepsTowardDanger(int skillIndex)
    {
        var skill = (FuseSkill)skillIndex;
        var random = GameRandom.FromSeed(4242);
        var tested = 0;
        for (var trial = 0; trial < 300; trial++)
        {
            var board = new FuseBoard();
            board.Reset(GameRandom.FromSeed((ulong)(trial + 500)), skill);
            board.ClearArena();
            for (var cell = 0; cell < FuseBoard.CellCount; cell++)
            {
                if (board.Tile(cell) == FuseTile.Floor && random.Chance(0.25f))
                {
                    board.SetTile(FuseBoard.ColumnOf(cell), FuseBoard.RowOf(cell), FuseTile.Crate);
                }
            }

            var start = RandomFloor(board, ref random);
            board.SetTile(FuseBoard.ColumnOf(start), FuseBoard.RowOf(start), FuseTile.Floor);
            board.PlaceMoogle(1, FuseBoard.ColumnOf(start), FuseBoard.RowOf(start), true);
            board.PlaceMoogle(2, FuseBoard.Columns - 1 - FuseBoard.ColumnOf(start), FuseBoard.Rows - 1 - FuseBoard.RowOf(start),
                false);
            for (var bomb = 0; bomb < 4; bomb++)
            {
                var cell = RandomFloor(board, ref random);
                board.PlaceBomb(FuseBoard.ColumnOf(cell), FuseBoard.RowOf(cell), 2, 1 + random.Next(4), random.Range(0.5f, 2f));
            }

            for (var decision = 0; decision < 20; decision++)
            {
                var cell = FuseBoard.CellAt(board.MoogleAt(1).Position);
                var input = board.DecideFor(1);
                if (!board.Bot.IsSafe(board, cell) || input.Direction == FuseDirection.None)
                {
                    continue;
                }

                tested++;
                var next = FuseBoard.Neighbour(cell, input.Direction);
                Assert.True(next >= 0 && board.Bot.IsSafe(board, next) && board.Walkable(next),
                    $"Trial {trial}: a safe bot stepped toward danger.");
            }
        }

        Assert.True(tested >= 200, $"Only {tested} decisions were tested.");
    }

    [Fact]
    public void SuddenDeathDropsBlocksInASpiralFromTheEdges()
    {
        var spiral = FuseBoard.Spiral;
        Assert.Equal(FuseBoard.CellCount, spiral.Length);
        var seen = new HashSet<int>();
        for (var index = 0; index < spiral.Length; index++)
        {
            Assert.True(seen.Add(spiral[index]));
        }

        for (var column = 0; column < FuseBoard.Columns; column++)
        {
            Assert.Equal(FuseBoard.CellIndex(column, 0), spiral[column]);
        }

        var cursor = FuseBoard.Columns;
        for (var row = 1; row < FuseBoard.Rows; row++)
        {
            Assert.Equal(FuseBoard.CellIndex(FuseBoard.Columns - 1, row), spiral[cursor++]);
        }

        for (var column = FuseBoard.Columns - 2; column >= 0; column--)
        {
            Assert.Equal(FuseBoard.CellIndex(column, FuseBoard.Rows - 1), spiral[cursor++]);
        }

        for (var row = FuseBoard.Rows - 2; row >= 1; row--)
        {
            Assert.Equal(FuseBoard.CellIndex(0, row), spiral[cursor++]);
        }

        Assert.Equal(FuseBoard.CellIndex(1, 1), spiral[cursor]);
        Assert.Equal(FuseBoard.CellIndex(6, 5), spiral[^2]);

        var board = Sandbox();
        StepFor(board, FuseBoard.SuddenDeathStart - 0.05f);
        Assert.False(board.SuddenDeath);
        StepFor(board, 10 * FuseBoard.FallInterval + 0.1f);
        Assert.True(board.SuddenDeath);
        var landed = 0;
        for (var index = 0; index < spiral.Length; index++)
        {
            var cell = spiral[index];
            if (board.Tile(cell) == FuseTile.Pillar)
            {
                continue;
            }

            Assert.Equal(landed < 10, board.Tile(cell) == FuseTile.Block);
            landed++;
        }

        StepFor(board, FuseBoard.SuddenDeathSeconds);
        for (var cell = 0; cell < FuseBoard.CellCount; cell++)
        {
            Assert.NotEqual(FuseTile.Floor, board.Tile(cell));
        }
    }

    [Fact]
    public void AFallingBlockCrushesWhoeverStandsUnderIt()
    {
        var board = Sandbox();
        board.PlaceMoogle(FuseBoard.Player, 0, 0, false);
        board.PlaceMoogle(1, 6, 6, false);
        StepFor(board, FuseBoard.SuddenDeathStart + FuseBoard.FallInterval * 0.5f);
        Assert.True(board.MoogleAt(FuseBoard.Player).Alive);

        StepFor(board, FuseBoard.FallInterval);

        Assert.False(board.MoogleAt(FuseBoard.Player).Alive);
        Assert.True(board.MoogleAt(1).Alive);
        Assert.Equal(FuseTile.Block, board.Tile(0, 0));
    }

    [Fact]
    public void TheLastMoogleStandingTakesTheRoundAndTwoRoundsTakeTheMatch()
    {
        var board = new FuseBoard();
        board.Reset(GameRandom.FromSeed(31), FuseSkill.Easy);
        KnockOutBots(board);
        StepFor(board, 1f);

        Assert.Equal(FusePhase.Ended, board.Phase);
        Assert.Equal(FuseBoard.Player, board.LastWinner);
        Assert.Equal(1, board.MoogleAt(FuseBoard.Player).Wins);
        Assert.Equal(FuseVerdict.Ongoing, board.Verdict);
        Assert.Equal(3, board.PlayerKnockouts);

        StepFor(board, FuseBoard.EndSeconds + 0.1f);
        Assert.Equal(2, board.Round);
        Assert.Equal(FusePhase.Ready, board.Phase);
        Assert.Equal(4, board.AliveCount);
        StepFor(board, FuseBoard.ReadySeconds + 0.1f);
        Assert.Equal(FusePhase.Fighting, board.Phase);

        KnockOutBots(board);
        StepFor(board, 1f);
        Assert.Equal(FuseVerdict.Won, board.Verdict);
        StepFor(board, FuseBoard.EndSeconds + 0.1f);
        Assert.Equal(FusePhase.MatchOver, board.Phase);
    }

    [Fact]
    public void KnockingEveryoneOutAtOnceIsADrawnRound()
    {
        var board = new FuseBoard();
        board.Reset(GameRandom.FromSeed(32), FuseSkill.Easy);
        for (var index = 0; index < FuseBoard.MaxMoogles; index++)
        {
            var cell = FuseBoard.CellAt(board.MoogleAt(index).Position);
            board.PlaceBomb(FuseBoard.ColumnOf(cell), FuseBoard.RowOf(cell), FuseBoard.NoMoogle, 1, 0.01f);
        }

        StepFor(board, 1f);

        Assert.Equal(FusePhase.Ended, board.Phase);
        Assert.Equal(FuseBoard.NoMoogle, board.LastWinner);
        for (var index = 0; index < FuseBoard.MaxMoogles; index++)
        {
            Assert.Equal(0, board.MoogleAt(index).Wins);
        }
    }

    [Fact]
    public void TheMatchGoesToTheFirstToTwoRounds()
    {
        Assert.Equal(FuseVerdict.Won, FuseBoard.Resolve(new[] { 2, 1, 0, 0 }, 3));
        Assert.Equal(FuseVerdict.Lost, FuseBoard.Resolve(new[] { 1, 0, 2, 0 }, 3));
        Assert.Equal(FuseVerdict.Ongoing, FuseBoard.Resolve(new[] { 1, 1, 0, 0 }, 2));
        Assert.Equal(FuseVerdict.Ongoing, FuseBoard.Resolve(new[] { 0, 0, 0, 0 }, 4));
        Assert.Equal(FuseVerdict.Won, FuseBoard.Resolve(new[] { 1, 0, 0, 0 }, FuseBoard.MaxRounds));
        Assert.Equal(FuseVerdict.Lost, FuseBoard.Resolve(new[] { 0, 0, 1, 0 }, FuseBoard.MaxRounds));
        Assert.Equal(FuseVerdict.Drawn, FuseBoard.Resolve(new[] { 1, 1, 0, 0 }, FuseBoard.MaxRounds));
    }

    [Fact]
    public void AMatchAlwaysEndsWithAVerdict()
    {
        var board = new FuseBoard();
        board.Reset(GameRandom.FromSeed(14), FuseSkill.Hard, true);
        for (var frame = 0; frame < 60 * 60 * 12 && board.Phase != FusePhase.MatchOver; frame++)
        {
            board.Step(Frame);
        }

        Assert.Equal(FusePhase.MatchOver, board.Phase);
        Assert.NotEqual(FuseVerdict.Ongoing, board.Verdict);
    }

    [Fact]
    public void EasyBotsAreBeatableAndHardBotsAreNot()
    {
        var easyWins = AutopilotMatchesWon(FuseSkill.Easy, 24);
        var hardWins = AutopilotMatchesWon(FuseSkill.Hard, 24);

        Assert.True(easyWins >= 11, $"A careful player won only {easyWins} of 24 matches against Easy bots.");
        Assert.True(hardWins <= 8, $"A careful player won {hardWins} of 24 matches against Hard bots.");
    }

    private static int AutopilotMatchesWon(FuseSkill skill, int matches)
    {
        var won = 0;
        for (var match = 0; match < matches; match++)
        {
            var board = new FuseBoard();
            board.Reset(GameRandom.FromSeed((ulong)(match * 7919 + 13)), skill, true);
            for (var frame = 0; frame < 60 * 60 * 12 && board.Phase != FusePhase.MatchOver; frame++)
            {
                board.Step(Frame);
            }

            if (board.Verdict == FuseVerdict.Won)
            {
                won++;
            }
        }

        return won;
    }

    private static FuseBoard Sandbox()
    {
        var board = new FuseBoard();
        board.Reset(GameRandom.FromSeed(1), FuseSkill.Hard);
        board.ClearArena();
        return board;
    }

    private static bool Burning(FuseBoard board, int column, int row) =>
        board.FireAt(FuseBoard.CellIndex(column, row)) > 0f;

    private static void StepFor(FuseBoard board, float seconds)
    {
        var frames = (int)MathF.Ceiling(seconds / Frame);
        for (var frame = 0; frame < frames; frame++)
        {
            board.Step(Frame);
        }
    }

    private static void KnockOutBots(FuseBoard board)
    {
        for (var index = 1; index < FuseBoard.MaxMoogles; index++)
        {
            var cell = FuseBoard.CellAt(board.MoogleAt(index).Position);
            board.PlaceBomb(FuseBoard.ColumnOf(cell), FuseBoard.RowOf(cell), FuseBoard.Player, 1, 0.01f);
        }
    }

    private static int RandomFloor(FuseBoard board, ref GameRandom random)
    {
        while (true)
        {
            var cell = random.Next(FuseBoard.CellCount);
            if (board.Tile(cell) != FuseTile.Pillar && board.BombIndexAt(cell) < 0)
            {
                return cell;
            }
        }
    }

    private static bool Active(FuseBoard board)
    {
        for (var slot = 0; slot < FuseBoard.BombCapacity; slot++)
        {
            if (board.BombAt(slot).Alive)
            {
                return true;
            }
        }

        for (var cell = 0; cell < FuseBoard.CellCount; cell++)
        {
            if (board.FireAt(cell) > 0f)
            {
                return true;
            }
        }

        return false;
    }

    private static bool SafetyReachable(FuseBoard board, int start, float speed, float slack)
    {
        var stepSeconds = 1f / speed;
        var steps = new int[FuseBoard.CellCount];
        Array.Fill(steps, -1);
        var queue = new Queue<int>();
        steps[start] = 0;
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            if (cell != start && board.Bot.Danger(cell) == FuseBot.Never)
            {
                return true;
            }

            foreach (var direction in FuseBoard.Directions.ToArray())
            {
                var next = FuseBoard.Neighbour(cell, direction);
                if (next < 0 || steps[next] >= 0 || !board.Walkable(next))
                {
                    continue;
                }

                var arrival = (steps[cell] + 1) * stepSeconds;
                var danger = board.Bot.Danger(next);
                if (danger != FuseBot.Never && danger <= arrival + stepSeconds * 0.5f + slack)
                {
                    continue;
                }

                steps[next] = steps[cell] + 1;
                queue.Enqueue(next);
            }
        }

        return false;
    }

    private static FuseBoard Play(ulong seed, out string trace)
    {
        var board = new FuseBoard();
        board.Reset(GameRandom.FromSeed(seed), FuseSkill.Hard);
        var builder = new StringBuilder();
        var directions = FuseBoard.Directions.ToArray();
        for (var frame = 0; frame < 60 * 45 && board.Phase != FusePhase.MatchOver; frame++)
        {
            board.SetPlayerInput(directions[frame / 50 % directions.Length], frame % 90 == 0);
            board.Step(Frame);
            if (frame % 30 != 0)
            {
                continue;
            }

            builder.Append(board.Round).Append(',').Append((int)board.Phase).Append(',');
            for (var index = 0; index < FuseBoard.MaxMoogles; index++)
            {
                ref readonly var moogle = ref board.MoogleAt(index);
                builder.Append(moogle.Position.X.ToString("F3", CultureInfo.InvariantCulture)).Append(':')
                    .Append(moogle.Position.Y.ToString("F3", CultureInfo.InvariantCulture)).Append(':')
                    .Append(moogle.Alive ? 1 : 0).Append(':').Append(moogle.Wins).Append(',');
            }

            for (var cell = 0; cell < FuseBoard.CellCount; cell++)
            {
                builder.Append((int)board.Tile(cell));
            }

            builder.Append(';');
        }

        trace = builder.ToString();
        return board;
    }
}
