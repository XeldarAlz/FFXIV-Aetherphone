using System.Numerics;
using Aetherphone.Apps.Games.Crater;
using Aetherphone.Apps.Games.Framework.World;
using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CraterBoardTests
{
    private const float GroundY = 12f;
    private const int TicksPerSecond = 120;
    private const int TurnTickLimit = TicksPerSecond * 60;

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = ScriptedMatch(0xC0FFEEUL);
        var second = ScriptedMatch(0xC0FFEEUL);
        var other = ScriptedMatch(0xBADCAFEUL);

        AssertSameState(first, second);
        Assert.True(first.CraterCount > 0, "the scripted shots should leave craters");
        Assert.True(TerrainDiffers(first.Terrain, other.Terrain), "a different seed should shape different terrain");
    }

    [Fact]
    public void BotMatchReplaysIdentically()
    {
        var first = BotMatch(77UL, TicksPerSecond * 30);
        var second = BotMatch(77UL, TicksPerSecond * 30);

        AssertSameState(first, second);
        Assert.True(first.CraterCount > 0, "bots should have fired within thirty seconds");
    }

    [Fact]
    public void CarvingUnderAMoogleMakesItFallAndLandLower()
    {
        var board = FlatBoard(2);
        var moogle = 0;
        var before = board.Moogle(moogle).Position;
        Assert.True(board.Moogle(moogle).Grounded);

        board.Terrain.Carve(before + new Vector2(0f, 1f), 0.9f);
        Run(board, TicksPerSecond * 2);

        var after = board.Moogle(moogle);
        Assert.True(after.Grounded);
        Assert.True(after.Position.Y > before.Y + 0.5f, "the moogle should drop into the hole");
        Assert.Equal(CraterRules.MaxHealth, after.Health);
        Assert.False(board.Terrain.IsSolid(before + new Vector2(0f, 1f)));
    }

    [Fact]
    public void ProjectilesIntegrateGravityAndWind()
    {
        var empty = new TerrainMask(CraterRules.Columns, CraterRules.Rows, CraterRules.MetresPerCell);
        const float wind = 2f;
        const int ticks = TicksPerSecond;
        var windy = Fly(empty, ProjectileKind.Shell, wind, ticks);
        var calm = Fly(empty, ProjectileKind.Shell, 0f, ticks);
        var grenade = Fly(empty, ProjectileKind.Grenade, wind, ticks);
        var seconds = ticks * CraterRules.TickSeconds;

        var expectedX = 5f + 6f * seconds + 0.5f * wind * seconds * seconds;
        var expectedY = 5f - 8f * seconds + 0.5f * CraterRules.Gravity * seconds * seconds;
        Assert.InRange(windy.X, expectedX - 0.06f, expectedX + 0.06f);
        Assert.InRange(windy.Y, expectedY - 0.08f, expectedY + 0.08f);
        Assert.InRange(windy.X - calm.X, 0.9f, 1.1f);
        Assert.Equal(calm.X, grenade.X, 4);
    }

    [Fact]
    public void BotFindsAHittingSolutionOnFlatTerrainWithoutWind()
    {
        var board = FlatBoard(2, botMask: 0b10);
        board.PlaceMoogle(0, new Vector2(12f, GroundY - CraterRules.MoogleRadius));
        board.PlaceMoogle(1, new Vector2(3f, GroundY - CraterRules.MoogleRadius));
        board.PlaceMoogle(2, new Vector2(22f, GroundY - CraterRules.MoogleRadius));
        board.PlaceMoogle(3, new Vector2(30f, GroundY - CraterRules.MoogleRadius));
        board.SetWind(0);

        var plan = CraterBot.PlanBest(board, 2, CraterWeapon.Shell);

        Assert.True(plan.Hits);
        Assert.True(plan.Score > 0f);
        var projectile = board.MakeProjectile(2, plan.Shot);
        var impact = Vector2.Zero;
        for (var tick = 0; tick < TicksPerSecond * 8; tick++)
        {
            var step = CraterBallistics.Advance(ref projectile, board.Terrain, 0f, board.WaterLevel, board.Moogles,
                CraterRules.TickSeconds);
            if (step.Outcome == FlightOutcome.Flying)
            {
                continue;
            }

            Assert.Equal(FlightOutcome.Impact, step.Outcome);
            impact = step.Point;
            break;
        }

        var nearestEnemy = MathF.Min(Vector2.Distance(impact, board.Moogle(0).Position),
            Vector2.Distance(impact, board.Moogle(1).Position));
        Assert.True(nearestEnemy < CraterRules.ShellBlast.Radius + CraterRules.MoogleRadius,
            "the planned shell should land within its blast of an enemy");
        Assert.True(Vector2.Distance(impact, board.Moogle(2).Position) >
            CraterRules.ShellBlast.Radius + CraterRules.MoogleRadius);
    }

    [Fact]
    public void ABotTurnEndsInALaunch()
    {
        var board = FlatBoard(2, botMask: 0b11);
        var launched = false;
        for (var tick = 0; tick < TurnTickLimit && !launched; tick++)
        {
            board.Tick();
            while (board.TryTakeEvent(out var entry))
            {
                Assert.NotEqual(CraterEventKind.TimeUp, entry.Kind);
                launched |= entry.Kind == CraterEventKind.Launched;
            }
        }

        Assert.True(launched);
    }

    [Fact]
    public void MooglesOverOpenWaterDrownAndTheirTeamIsEliminated()
    {
        var board = FlatBoard(2, groundRight: 16f);
        board.PlaceMoogle(0, new Vector2(4f, GroundY - CraterRules.MoogleRadius));
        board.PlaceMoogle(1, new Vector2(10f, GroundY - CraterRules.MoogleRadius));
        board.PlaceMoogle(2, new Vector2(20f, GroundY - CraterRules.MoogleRadius));
        board.PlaceMoogle(3, new Vector2(26f, GroundY - CraterRules.MoogleRadius));

        var drowned = 0;
        for (var tick = 0; tick < TurnTickLimit && !board.Over; tick++)
        {
            board.Tick();
            while (board.TryTakeEvent(out var entry))
            {
                drowned += entry.Kind == CraterEventKind.Drowned ? 1 : 0;
            }
        }

        Assert.Equal(2, drowned);
        Assert.False(board.Moogle(2).Alive);
        Assert.True(board.Moogle(2).Sunk);
        Assert.False(board.TeamAlive(1));
        Assert.True(board.Over);
        Assert.Equal(0, board.Winner);
    }

    [Fact]
    public void TurnsCycleTeamsAndMembersAndSkipEliminatedTeams()
    {
        var board = FlatBoard(3);
        var first = board.ActiveTeam;
        Assert.Equal(first * CraterRules.MooglesPerTeam, board.ActiveMoogle);
        Assert.Equal(1, board.Round);

        PassTurn(board);
        Assert.Equal((first + 1) % 3, board.ActiveTeam);
        PassTurn(board);
        Assert.Equal((first + 2) % 3, board.ActiveTeam);
        PassTurn(board);
        Assert.Equal(first, board.ActiveTeam);
        Assert.Equal(first * CraterRules.MooglesPerTeam + 1, board.ActiveMoogle);
        Assert.Equal(2, board.Round);

        var doomed = (first + 1) % 3;
        for (var member = 0; member < CraterRules.MooglesPerTeam; member++)
        {
            var x = board.Moogle(doomed * CraterRules.MooglesPerTeam + member).Position.X;
            board.Terrain.CarveRect(new Rect(new Vector2(x - 0.8f, GroundY - 1f),
                new Vector2(x + 0.8f, CraterRules.WorldHeight)));
        }

        PassTurn(board);
        Assert.False(board.TeamAlive(doomed));
        Assert.Equal((first + 2) % 3, board.ActiveTeam);
        PassTurn(board);
        Assert.Equal(first, board.ActiveTeam);
        Assert.Equal(first * CraterRules.MooglesPerTeam, board.ActiveMoogle);
        Assert.Equal(3, board.Round);
    }

    [Fact]
    public void FallDamageStartsAboveThreeMetres()
    {
        Assert.Equal(0, CraterRules.FallDamage(2.9f));
        Assert.Equal(0, CraterRules.FallDamage(CraterRules.SafeFall));
        Assert.True(CraterRules.FallDamage(3.2f) > 0);

        var board = FlatBoard(2);
        board.PlaceMoogle(0, new Vector2(6f, GroundY - CraterRules.MoogleRadius - 2.5f));
        board.PlaceMoogle(2, new Vector2(20f, GroundY - CraterRules.MoogleRadius - 5f));
        Run(board, TicksPerSecond * 3);

        Assert.True(board.Moogle(0).Grounded);
        Assert.Equal(CraterRules.MaxHealth, board.Moogle(0).Health);
        Assert.True(board.Moogle(2).Grounded);
        var expected = CraterRules.MaxHealth - CraterRules.FallDamage(5f);
        Assert.InRange(board.Moogle(2).Health, expected - 1, expected + 1);
    }

    [Fact]
    public void BlastDamageFallsOffWithDistance()
    {
        var blast = CraterRules.ShellBlast;
        var direct = CraterRules.BlastDamage(blast, CraterRules.MoogleRadius);
        var near = CraterRules.BlastDamage(blast, CraterRules.MoogleRadius + blast.Radius * 0.5f);
        var outside = CraterRules.BlastDamage(blast, CraterRules.MoogleRadius + blast.Radius + 0.01f);

        Assert.Equal(blast.Damage, direct);
        Assert.InRange(near, 1, direct - 1);
        Assert.Equal(0, outside);
    }

    [Fact]
    public void AClusterSplitsIntoFiveBomblets()
    {
        var board = FlatBoard(2);
        AdvanceToAiming(board);
        Assert.True(board.SelectWeapon(CraterWeapon.Cluster));
        board.SetAim(1.2f);
        Assert.True(board.Fire(0.45f));

        var splits = 0;
        var blasts = 0;
        var bomblets = 0;
        for (var tick = 0; tick < TurnTickLimit && board.Phase != CraterPhase.TurnIntro; tick++)
        {
            board.Tick();
            while (board.TryTakeEvent(out var entry))
            {
                splits += entry.Kind == CraterEventKind.ClusterSplit ? 1 : 0;
                blasts += entry.Kind == CraterEventKind.Exploded ? 1 : 0;
                bomblets += entry.Kind == CraterEventKind.Exploded && entry.Projectile == ProjectileKind.Bomblet ? 1 : 0;
            }
        }

        Assert.Equal(1, splits);
        Assert.Equal(CraterRules.BombletCount, bomblets);
        Assert.Equal(CraterRules.BombletCount + 1, blasts);
        Assert.Equal(CraterRules.StartingAmmo(CraterWeapon.Cluster) - 1, board.Ammo(1 - board.ActiveTeam, CraterWeapon.Cluster));
    }

    [Fact]
    public void ADrillDigsATunnelBeforeItExplodes()
    {
        var board = FlatBoard(2);
        AdvanceToAiming(board);
        Assert.True(board.SelectWeapon(CraterWeapon.Drill));
        board.SetAim(-1f);
        Assert.True(board.Fire(0.4f));

        var started = Vector2.Zero;
        var exploded = Vector2.Zero;
        for (var tick = 0; tick < TurnTickLimit && exploded == Vector2.Zero; tick++)
        {
            board.Tick();
            while (board.TryTakeEvent(out var entry))
            {
                if (entry.Kind == CraterEventKind.DrillStarted)
                {
                    started = entry.Position;
                }

                if (entry.Kind == CraterEventKind.Exploded)
                {
                    exploded = entry.Position;
                }
            }
        }

        Assert.NotEqual(Vector2.Zero, started);
        Assert.True(exploded.Y > GroundY + 2.5f, "the drill should burrow before it blows");
        Assert.False(board.Terrain.IsSolid((started + exploded) * 0.5f));
    }

    [Fact]
    public void WalkingClimbsASlopeSteeperThanSixtyDegrees()
    {
        var board = FlatBoard(2);
        AdvanceToAiming(board);
        var active = board.ActiveMoogle;
        board.PlaceMoogle(active, new Vector2(4f, GroundY - CraterRules.MoogleRadius));
        Ramp(board.Terrain, 5f, 3f, 0.6f);

        board.SetWalk(1);
        Run(board, TicksPerSecond * 3);

        var moogle = board.Moogle(active);
        Assert.True(moogle.Grounded);
        Assert.True(moogle.Position.Y < GroundY - 1.5f, "the moogle should walk up the ramp onto the ledge");
    }

    [Fact]
    public void WalkingStopsAtASheerWall()
    {
        var board = FlatBoard(2);
        AdvanceToAiming(board);
        var active = board.ActiveMoogle;
        board.PlaceMoogle(active, new Vector2(4f, GroundY - CraterRules.MoogleRadius));
        board.Terrain.FillRect(new Rect(new Vector2(5f, GroundY - 2f), new Vector2(6f, GroundY)));

        board.SetWalk(1);
        Run(board, TicksPerSecond * 2);

        var moogle = board.Moogle(active);
        Assert.True(moogle.Position.X < 5f - CraterRules.MoogleRadius * 0.5f);
        Assert.Equal(GroundY - CraterRules.MoogleRadius, moogle.Position.Y, 2);
    }

    [Fact]
    public void WalkingOffTheIslandEdgeFallsIntoTheWater()
    {
        var board = FlatBoard(2, groundRight: 16f);
        AdvanceToAiming(board);
        var active = board.ActiveMoogle;
        board.PlaceMoogle(active, new Vector2(15f, GroundY - CraterRules.MoogleRadius));

        board.SetWalk(1);
        for (var tick = 0; tick < TicksPerSecond * 4 && !board.Moogle(active).Sunk; tick++)
        {
            board.Tick();
        }

        Assert.True(board.Moogle(active).Sunk);
        Assert.False(board.Moogle(active).Alive);
    }

    [Fact]
    public void TeleportLandsOnTheGroundBelowTheTarget()
    {
        var board = FlatBoard(2, groundRight: 24f);
        AdvanceToAiming(board);
        var team = board.ActiveTeam;
        Assert.True(board.SelectWeapon(CraterWeapon.Teleport));

        Assert.False(board.Teleport(new Vector2(28f, 4f)));
        Assert.True(board.Teleport(new Vector2(18f, 4f)));

        var moogle = board.Moogle(board.ActiveMoogle);
        Assert.Equal(18f, moogle.Position.X, 3);
        Assert.Equal(GroundY - CraterRules.MoogleRadius, moogle.Position.Y, 2);
        Assert.Equal(CraterRules.StartingAmmo(CraterWeapon.Teleport) - 1, board.Ammo(team, CraterWeapon.Teleport));
        Assert.Equal(CraterPhase.Settling, board.Phase);
    }

    [Fact]
    public void TheWaterRisesOnceSuddenDeathBegins()
    {
        var board = FlatBoard(2);
        var start = board.WaterLevel;
        while (board.Round <= CraterRules.SuddenDeathRound)
        {
            Assert.False(board.SuddenDeath);
            PassTurn(board);
        }

        Assert.True(board.SuddenDeath);
        Assert.True(board.WaterTarget < start);
        Run(board, TicksPerSecond * 2);
        Assert.True(board.WaterLevel < start);
    }

    private static CraterBoard FlatBoard(int teams, int botMask = 0, float groundRight = CraterRules.WorldWidth)
    {
        var board = new CraterBoard();
        board.Start(5UL, new CraterSetup(teams, botMask, CraterLevel.Easy));
        board.Terrain.Clear();
        board.Terrain.FillRect(new Rect(new Vector2(0f, GroundY), new Vector2(groundRight, CraterRules.WorldHeight)));
        for (var index = 0; index < board.MoogleCount; index++)
        {
            board.PlaceMoogle(index, new Vector2(3f + index * 3.5f, GroundY - CraterRules.MoogleRadius));
        }

        board.SetWind(0);
        while (board.TryTakeEvent(out _))
        {
        }

        return board;
    }

    private static void Ramp(TerrainMask terrain, float left, float slope, float width)
    {
        var cells = (int)MathF.Round(width / CraterRules.MetresPerCell);
        for (var column = 0; column < cells; column++)
        {
            var x = left + column * CraterRules.MetresPerCell;
            var top = GroundY - (column + 1) * CraterRules.MetresPerCell * slope;
            terrain.FillRect(new Rect(new Vector2(x, top), new Vector2(x + CraterRules.MetresPerCell, GroundY)));
        }

        var plateau = GroundY - cells * CraterRules.MetresPerCell * slope;
        terrain.FillRect(new Rect(new Vector2(left + width, plateau), new Vector2(left + width + 4f, GroundY)));
    }

    private static CraterBoard ScriptedMatch(ulong seed)
    {
        var board = new CraterBoard();
        board.Start(seed, new CraterSetup(2, 0, CraterLevel.Easy));
        Shoot(board, CraterWeapon.Shell, 0.75f, 0.62f, CraterRules.DefaultFuse);
        Shoot(board, CraterWeapon.Grenade, 0.9f, 0.55f, 2);
        Shoot(board, CraterWeapon.Cluster, 0.8f, 0.6f, CraterRules.DefaultFuse);
        return board;
    }

    private static CraterBoard BotMatch(ulong seed, int ticks)
    {
        var board = new CraterBoard();
        board.Start(seed, new CraterSetup(2, 0b11, CraterLevel.Hard));
        Run(board, ticks);
        return board;
    }

    private static void Shoot(CraterBoard board, CraterWeapon weapon, float elevation, float power, int fuse)
    {
        AdvanceToAiming(board);
        if (board.Over)
        {
            return;
        }

        board.SetWalk(1);
        Run(board, 40);
        board.SetWalk(0);
        board.Jump();
        Run(board, TicksPerSecond);
        var active = board.Moogle(board.ActiveMoogle);
        board.SetFacing(active.Position.X < CraterRules.WorldWidth * 0.5f ? 1 : -1);
        board.SelectWeapon(weapon);
        board.SetFuse(fuse);
        board.SetAim(elevation);
        board.Fire(power);
        for (var tick = 0; tick < TurnTickLimit && board.Phase != CraterPhase.TurnIntro && !board.Over; tick++)
        {
            board.Tick();
        }
    }

    private static void AdvanceToAiming(CraterBoard board)
    {
        for (var tick = 0; tick < TurnTickLimit && board.Phase != CraterPhase.Aiming && !board.Over; tick++)
        {
            board.Tick();
        }
    }

    private static void PassTurn(CraterBoard board)
    {
        AdvanceToAiming(board);
        for (var tick = 0; tick < TurnTickLimit && board.Phase != CraterPhase.TurnIntro && !board.Over; tick++)
        {
            board.Tick();
        }
    }

    private static void Run(CraterBoard board, int ticks)
    {
        for (var tick = 0; tick < ticks; tick++)
        {
            board.Tick();
        }

        while (board.TryTakeEvent(out _))
        {
        }
    }

    private static Vector2 Fly(TerrainMask terrain, ProjectileKind kind, float wind, int ticks)
    {
        var projectile = new CraterProjectile
        {
            Position = new Vector2(5f, 5f),
            Velocity = new Vector2(6f, -8f),
            Kind = kind,
            Alive = true,
            Fuse = 10f,
            Owner = CraterBoard.NoOwner,
        };
        for (var tick = 0; tick < ticks; tick++)
        {
            var step = CraterBallistics.Advance(ref projectile, terrain, wind, CraterRules.WorldHeight, ReadOnlySpan<CraterMoogle>.Empty,
                CraterRules.TickSeconds);
            Assert.Equal(FlightOutcome.Flying, step.Outcome);
        }

        return projectile.Position;
    }

    private static void AssertSameState(CraterBoard first, CraterBoard second)
    {
        Assert.Equal(first.TickCount, second.TickCount);
        Assert.Equal(first.Phase, second.Phase);
        Assert.Equal(first.Round, second.Round);
        Assert.Equal(first.ActiveTeam, second.ActiveTeam);
        Assert.Equal(first.ActiveMoogle, second.ActiveMoogle);
        Assert.Equal(first.WindLevel, second.WindLevel);
        Assert.Equal(first.WaterLevel, second.WaterLevel);
        Assert.Equal(first.Winner, second.Winner);
        Assert.Equal(first.CraterCount, second.CraterCount);
        for (var index = 0; index < first.CraterCount; index++)
        {
            Assert.Equal(first.Craters[index].Center, second.Craters[index].Center);
            Assert.Equal(first.Craters[index].Radius, second.Craters[index].Radius);
        }

        for (var index = 0; index < first.MoogleCount; index++)
        {
            var left = first.Moogle(index);
            var right = second.Moogle(index);
            Assert.Equal(left.Position, right.Position);
            Assert.Equal(left.Health, right.Health);
            Assert.Equal(left.Alive, right.Alive);
            Assert.Equal(left.Sunk, right.Sunk);
            Assert.Equal(left.Grounded, right.Grounded);
        }

        for (var team = 0; team < first.TeamCount; team++)
        {
            Assert.Equal(first.DamageDealt(team), second.DamageDealt(team));
        }

        Assert.False(TerrainDiffers(first.Terrain, second.Terrain));
    }

    private static bool TerrainDiffers(TerrainMask first, TerrainMask second)
    {
        for (var row = 0; row < first.Height; row++)
        {
            for (var column = 0; column < first.Width; column++)
            {
                if (first.IsSolid(column, row) != second.IsSolid(column, row))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
