using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.World;
using Aetherphone.Apps.Games.Siege;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SiegeBoardTests
{
    private const int TicksPerSecond = 60;
    private const string Token = @"(\+?[wrafdb][0-4?]|\.)";
    private const string Wave = @"\d+(\.\d+)?:" + Token + "( " + Token + ")*";
    private static readonly Regex LevelPattern = new("^" + Wave + @"(\|" + Wave + ")*$");

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Play(1234);
        var second = Play(1234);
        var other = Play(99);

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
        Assert.Contains("|", first);
    }

    [Fact]
    public void PlantingFollowsThePlacementRules()
    {
        var board = Campaign(1);

        Assert.Equal(PlantResult.Locked, board.Plant(DefenderKind.Frostbud, 0, 6));
        Assert.Equal(PlantResult.OutOfBounds, board.Plant(DefenderKind.Sprout, SiegeRules.Columns, 0));
        Assert.Equal(PlantResult.OutOfBounds, board.Plant(DefenderKind.Sprout, 0, SiegeRules.Rows));
        Assert.Equal(PlantResult.Planted, board.Plant(DefenderKind.Sprout, 0, 6));
        Assert.Equal((int)SiegeRules.CampaignSunlight - SiegeRules.Cost(DefenderKind.Sprout), board.Sunlight);
        Assert.Equal(1, board.Planted);
        Assert.Equal(DefenderKind.Sprout, board.DefenderAt(0, 6).Kind);
        Assert.Equal(PlantResult.Occupied, board.Plant(DefenderKind.Sprout, 0, 6));
        Assert.Equal(PlantResult.CoolingDown, board.Plant(DefenderKind.Sprout, 1, 6));

        board.Grant(500f);
        Run(board, SiegeRules.Cooldown(DefenderKind.Sprout) + 0.1f);

        Assert.True(board.Ready(DefenderKind.Sprout));
        Assert.Equal(PlantResult.Planted, board.Plant(DefenderKind.Sprout, 1, 6));
    }

    [Fact]
    public void AnUnaffordableCardCannotBePlanted()
    {
        var board = Campaign(4);

        Assert.Equal(PlantResult.Planted, board.Plant(DefenderKind.Sprout, 0, 6));
        Assert.Equal(PlantResult.Planted, board.Plant(DefenderKind.Thornwall, 0, 5));
        Assert.True(board.Sunlight < SiegeRules.Cost(DefenderKind.Sunbloom));
        Assert.False(board.Affordable(DefenderKind.Sunbloom));
        Assert.Equal(PlantResult.Unaffordable, board.Plant(DefenderKind.Sunbloom, 1, 7));
        Assert.False(board.DefenderAt(1, 7).Occupied);
    }

    [Fact]
    public void TheShovelDigsUpADefenderAndFreesItsCell()
    {
        var board = Campaign(1);
        Assert.True(board.Place(DefenderKind.Sprout, 0, 3));

        Assert.True(board.Dig(0, 3));
        Assert.False(board.DefenderAt(0, 3).Occupied);
        Assert.False(board.Dig(0, 3));
        Assert.Equal(PlantResult.Planted, board.Plant(DefenderKind.Sprout, 0, 3));
    }

    [Fact]
    public void ASproutKillsAWalkerWithOneSeedPerPointOfHealth()
    {
        var board = Campaign(1);
        board.Place(DefenderKind.Sprout, 0, 7);
        var walker = board.Spawn(EnemyKind.Walker, 0, 0f);
        var hits = 0;
        var killed = false;

        for (var tick = 0; tick < TicksPerSecond * 20 && !killed; tick++)
        {
            board.BeginFrame();
            board.Tick();
            var events = board.Events;
            for (var index = 0; index < events.Length; index++)
            {
                if (events[index].Kind == SiegeEventKind.EnemyHit && events[index].Column == 0)
                {
                    hits++;
                }

                killed |= events[index].Kind == SiegeEventKind.EnemyKilled && events[index].Column == 0;
            }
        }

        Assert.True(killed);
        Assert.False(board.Enemy(walker).Alive);
        Assert.Equal((int)SiegeRules.Health(EnemyKind.Walker), hits);
        Assert.Equal(1, board.Defeated);
    }

    [Fact]
    public void SeedsOnlyFlyUpTheirOwnColumn()
    {
        var board = Campaign(1);
        board.Place(DefenderKind.Sprout, 0, 2);
        board.Spawn(EnemyKind.Walker, 0, 5f);
        board.Spawn(EnemyKind.Walker, 4, 1f);

        var events = Record(board, 3f);

        Assert.DoesNotContain(events, item => item.Kind == SiegeEventKind.SeedFired);
    }

    [Fact]
    public void AnUnslowedWalkerMarchesAtItsFullSpeed()
    {
        var board = Campaign(1);
        var walker = board.Spawn(EnemyKind.Walker, 4, 0f);

        Run(board, 1f);

        Assert.Equal(SiegeRules.Speed(EnemyKind.Walker), board.Enemy(walker).Y, 3);
    }

    [Fact]
    public void FrostSeedsHalveAWalkersSpeed()
    {
        var board = Campaign(6);
        board.Place(DefenderKind.Frostbud, 0, 7);
        var walker = board.Spawn(EnemyKind.Walker, 0, 0f);
        for (var tick = 0; tick < TicksPerSecond * 5 && !board.Enemy(walker).Slowed; tick++)
        {
            board.Tick();
        }

        Assert.True(board.Enemy(walker).Slowed);
        Assert.Equal(SiegeRules.SlowSeconds, board.Enemy(walker).SlowSeconds, 1);
        var before = board.Enemy(walker).Y;
        Run(board, 1f);

        Assert.True(board.Enemy(walker).Slowed);
        Assert.Equal(SiegeRules.Speed(EnemyKind.Walker) * SiegeRules.SlowFactor, board.Enemy(walker).Y - before, 3);
    }

    [Fact]
    public void ASlowedMandragoraBitesHalfAsOften()
    {
        var board = Campaign(6);
        board.Place(DefenderKind.Thornwall, 0, 4);
        board.Place(DefenderKind.Frostbud, 0, 6);
        board.Place(DefenderKind.Thornwall, 4, 4);
        board.Spawn(EnemyKind.Walker, 0, SiegeRules.ContactY(EnemyKind.Walker, 4) - 0.01f);
        board.Spawn(EnemyKind.Walker, 4, SiegeRules.ContactY(EnemyKind.Walker, 4) - 0.01f);

        var events = Record(board, 6f);
        var slowedBites = events.Count(item => item.Kind == SiegeEventKind.DefenderBitten && item.Column == 0);
        var normalBites = events.Count(item => item.Kind == SiegeEventKind.DefenderBitten && item.Column == 4);

        Assert.InRange(normalBites, 11, 12);
        Assert.InRange(slowedBites, 5, 7);
    }

    [Fact]
    public void AThornwallBlocksItsColumnAndGetsChewed()
    {
        var board = Campaign(1);
        board.Place(DefenderKind.Thornwall, 0, 3);
        var walker = board.Spawn(EnemyKind.Walker, 0, 0f);

        Run(board, 20f);

        ref readonly var enemy = ref board.Enemy(walker);
        Assert.Equal(EnemyState.Eating, enemy.State);
        Assert.Equal(SiegeRules.ContactY(EnemyKind.Walker, 3), enemy.Y, 3);
        Assert.Equal(3, enemy.BiteRow);
        var wall = board.DefenderAt(0, 3);
        Assert.True(wall.Occupied);
        Assert.InRange(wall.Health, 1f, SiegeRules.Health(DefenderKind.Thornwall) - 10f);
    }

    [Fact]
    public void AMandragoraEatsThroughADefenderAndMarchesOn()
    {
        var board = Campaign(2);
        board.Place(DefenderKind.Sunbloom, 0, 3);
        var walker = board.Spawn(EnemyKind.Walker, 0, 2f);

        var events = Record(board, 8f);

        Assert.Contains(events, item => item.Kind == SiegeEventKind.DefenderLost && item.Column == 0 && item.Row == 3);
        Assert.False(board.DefenderAt(0, 3).Occupied);
        Assert.Equal(EnemyState.Walking, board.Enemy(walker).State);
        Assert.True(board.Enemy(walker).Y > SiegeRules.ContactY(EnemyKind.Walker, 3));
    }

    [Fact]
    public void ABombcapBlastsEverythingInTheThreeByThreeAroundIt()
    {
        var board = Campaign(8);
        board.Place(DefenderKind.Bombcap, 2, 4);
        var inside = new[]
        {
            board.Spawn(EnemyKind.Walker, 1, 3.2f), board.Spawn(EnemyKind.Walker, 2, 3.2f),
            board.Spawn(EnemyKind.Armoured, 2, 4.3f), board.Spawn(EnemyKind.Walker, 3, 5.2f),
            board.Spawn(EnemyKind.Flyer, 1, 5.3f),
        };
        var outside = new[]
        {
            board.Spawn(EnemyKind.Walker, 0, 4.3f), board.Spawn(EnemyKind.Walker, 4, 4.3f),
            board.Spawn(EnemyKind.Walker, 2, 2.1f), board.Spawn(EnemyKind.Walker, 2, 6.3f),
        };

        var events = Record(board, SiegeRules.BombFuseSeconds + 0.1f);

        Assert.Single(events, item => item.Kind == SiegeEventKind.BombExploded);
        Assert.False(board.DefenderAt(2, 4).Occupied);
        for (var index = 0; index < inside.Length; index++)
        {
            Assert.False(board.Enemy(inside[index]).Alive);
        }

        for (var index = 0; index < outside.Length; index++)
        {
            Assert.True(board.Enemy(outside[index]).Alive);
            Assert.Equal(board.Enemy(outside[index]).MaxHealth, board.Enemy(outside[index]).Health);
        }

        Assert.True(SiegeRules.InBlast(2, 4, 1, 3.9f));
        Assert.False(SiegeRules.InBlast(2, 4, 2, 2.99f));
        Assert.False(SiegeRules.InBlast(2, 4, 4, 4.5f));
    }

    [Fact]
    public void ADiggerPopsUpBehindTheFirstDefenderOfItsColumn()
    {
        var board = Campaign(11);
        board.Place(DefenderKind.Sunbloom, 0, 2);
        board.Place(DefenderKind.Sunbloom, 0, 5);
        var digger = board.Spawn(EnemyKind.Digger, 0, SiegeRules.SpawnY);
        Assert.False(board.Enemy(digger).Targetable);

        var surfaced = false;
        for (var tick = 0; tick < TicksPerSecond * 15 && !surfaced; tick++)
        {
            board.BeginFrame();
            board.Tick();
            surfaced = board.Events.ToArray().Any(item => item.Kind == SiegeEventKind.Surfaced);
        }

        Assert.True(surfaced);
        ref readonly var enemy = ref board.Enemy(digger);
        Assert.Equal(EnemyState.Surfacing, enemy.State);
        Assert.Equal(3, SiegeRules.RowAt(enemy.Y));
        Assert.Equal(2, enemy.BiteRow);

        Run(board, 1.5f);

        Assert.True(board.DefenderAt(0, 2).Health < board.DefenderAt(0, 2).MaxHealth);
        Assert.Equal(board.DefenderAt(0, 5).MaxHealth, board.DefenderAt(0, 5).Health);
    }

    [Fact]
    public void ADiggerWithNothingToTunnelUnderSurfacesNearTheGarden()
    {
        var board = Campaign(11);
        var digger = board.Spawn(EnemyKind.Digger, 4, SiegeRules.SpawnY);

        var surfaced = false;
        for (var tick = 0; tick < TicksPerSecond * 20 && !surfaced; tick++)
        {
            board.BeginFrame();
            board.Tick();
            surfaced = board.Events.ToArray().Any(item => item.Kind == SiegeEventKind.Surfaced);
        }

        Assert.True(surfaced);
        Assert.InRange(board.Enemy(digger).Y, SiegeRules.DiggerLastSurface, SiegeRules.GardenLine);
        Assert.Equal(-1, board.Enemy(digger).BiteRow);
    }

    [Fact]
    public void FlyersGlideOverBlockers()
    {
        var board = Campaign(7);
        board.Place(DefenderKind.Thornwall, 0, 3);
        var flyer = board.Spawn(EnemyKind.Flyer, 0, 0f);

        var events = Record(board, 15f);

        Assert.True(board.Enemy(flyer).Y > 4.5f);
        Assert.Equal(EnemyState.Walking, board.Enemy(flyer).State);
        Assert.DoesNotContain(events, item => item.Kind == SiegeEventKind.DefenderBitten);
        Assert.Equal(board.DefenderAt(0, 3).MaxHealth, board.DefenderAt(0, 3).Health);
    }

    [Fact]
    public void FlyersStillFallToSeeds()
    {
        var board = Campaign(7);
        board.Place(DefenderKind.Sprout, 0, 7);
        board.Spawn(EnemyKind.Flyer, 0, 0f);

        var events = Record(board, 12f);

        Assert.Contains(events, item => item.Kind == SiegeEventKind.EnemyKilled && item.Column == 0 &&
            item.Detail == (byte)EnemyKind.Flyer);
        Assert.DoesNotContain(events, item => item.Kind == SiegeEventKind.GardenHit);
    }

    [Fact]
    public void ArmouredMandragorasLoseTheirPotAtHalfHealth()
    {
        var board = Campaign(5);
        board.Place(DefenderKind.Sprout, 0, 7);
        var armoured = board.Spawn(EnemyKind.Armoured, 0, 0f);
        Assert.True(board.Enemy(armoured).HasPot);

        var lost = false;
        for (var tick = 0; tick < TicksPerSecond * 25 && !lost; tick++)
        {
            board.BeginFrame();
            board.Tick();
            lost = board.Events.ToArray().Any(item => item.Kind == SiegeEventKind.PotLost);
            if (!lost)
            {
                Assert.True(board.Enemy(armoured).Health > board.Enemy(armoured).MaxHealth * SiegeRules.PotShare);
            }
        }

        Assert.True(lost);
        ref readonly var enemy = ref board.Enemy(armoured);
        Assert.False(enemy.HasPot);
        Assert.True(enemy.Alive);
        Assert.InRange(enemy.Health, enemy.MaxHealth * SiegeRules.PotShare - SiegeRules.SeedDamage,
            enemy.MaxHealth * SiegeRules.PotShare);
    }

    [Fact]
    public void TheBossCallsWalkersIntoANeighbouringColumn()
    {
        var board = Campaign(10);
        board.Spawn(EnemyKind.Boss, 0, 0.5f);

        var events = Record(board, SiegeRules.BossSummonSeconds + 0.1f);

        Assert.Contains(events, item => item.Kind == SiegeEventKind.Summoned && item.Column == 1);
        Assert.Contains(events, item => item.Kind == SiegeEventKind.EnemySpawned && item.Column == 1 &&
            item.Detail == (byte)EnemyKind.Walker);
    }

    [Fact]
    public void WaveTablePlaybackMatchesTheLevelData()
    {
        var board = Campaign(1);
        var random = GameRandom.FromSeed(7);
        var expected = new WaveEntry[16];
        var count = SiegeLevels.BuildWave(1, 0, ref random, expected);
        var spawns = new List<(float Time, int Lane, EnemyKind Kind)>();

        for (var tick = 0; tick < TicksPerSecond * 30 && spawns.Count < count; tick++)
        {
            board.BeginFrame();
            board.Tick();
            var events = board.Events;
            for (var index = 0; index < events.Length; index++)
            {
                if (events[index].Kind == SiegeEventKind.EnemySpawned)
                {
                    spawns.Add((board.Time, events[index].Column, (EnemyKind)events[index].Detail));
                }
            }
        }

        Assert.Equal(new byte[] { 2, 1, 3 }, expected.Take(count).Select(entry => entry.Lane).ToArray());
        Assert.Equal(count, spawns.Count);
        for (var index = 0; index < count; index++)
        {
            Assert.Equal(expected[index].Lane, spawns[index].Lane);
            Assert.Equal((EnemyKind)expected[index].EnemyKind, spawns[index].Kind);
            var due = SiegeRules.PreludeSeconds + expected[index].Time;
            Assert.InRange(spawns[index].Time - due, -SiegeRules.StepSeconds, SiegeRules.StepSeconds * 3f);
        }
    }

    [Fact]
    public void EveryLevelParsesAndOnlySendsIntroducedEnemies()
    {
        var buffer = new WaveEntry[256];
        for (var level = 1; level <= SiegeLevels.Count; level++)
        {
            var source = SiegeLevels.Source(level);
            Assert.Matches(LevelPattern, source);
            var waves = SiegeLevels.WaveCount(level);
            Assert.InRange(waves, 3, 6);
            var bosses = 0;
            var waveSources = source.Split('|');
            for (var wave = 0; wave < waves; wave++)
            {
                var random = GameRandom.FromSeed((ulong)level);
                var count = SiegeLevels.BuildWave(level, wave, ref random, buffer);
                var tokens = waveSources[wave][(waveSources[wave].IndexOf(':') + 1)..].Split(' ')
                    .Count(token => token != ".");
                Assert.Equal(tokens, count);
                for (var index = 0; index < count; index++)
                {
                    var kind = (EnemyKind)buffer[index].EnemyKind;
                    Assert.True(SiegeLevels.IntroLevel(kind) <= level, $"level {level} sends {kind} too early");
                    Assert.InRange(buffer[index].Lane, 0, SiegeRules.Columns - 1);
                    if (index > 0)
                    {
                        Assert.True(buffer[index].Time >= buffer[index - 1].Time);
                    }

                    if (kind == EnemyKind.Boss)
                    {
                        bosses++;
                    }
                }
            }

            Assert.Equal(SiegeLevels.IsBossLevel(level), bosses > 0);
        }

        Assert.True(SiegeLevels.IsBossLevel(10));
        Assert.True(SiegeLevels.IsBossLevel(20));
        Assert.True(SiegeLevels.IsBossLevel(30));
        Assert.False(SiegeLevels.IsBossLevel(11));
    }

    [Fact]
    public void EveryDefenderAndEnemyIsIntroducedInTheFirstTwelveLevels()
    {
        var buffer = new WaveEntry[256];
        for (var kind = DefenderKind.Sprout; kind <= DefenderKind.Bombcap; kind++)
        {
            var level = SiegeLevels.UnlockLevel(kind);
            Assert.InRange(level, 1, 12);
            Assert.Equal(kind, SiegeLevels.DefenderIntroducedAt(level));
            Assert.False(Campaign(Math.Max(1, level - 1)).Unlocked(kind) && level > 1);
            Assert.True(Campaign(level).Unlocked(kind));
        }

        for (var kind = EnemyKind.Walker; kind <= EnemyKind.Boss; kind++)
        {
            var level = SiegeLevels.IntroLevel(kind);
            Assert.InRange(level, 1, 12);
            Assert.True(SiegeLevels.IntroducesEnemy(level, kind));
            var sent = false;
            for (var wave = 0; wave < SiegeLevels.WaveCount(level); wave++)
            {
                var random = GameRandom.FromSeed(3);
                var count = SiegeLevels.BuildWave(level, wave, ref random, buffer);
                for (var index = 0; index < count; index++)
                {
                    sent |= buffer[index].EnemyKind == (byte)kind;
                }
            }

            Assert.True(sent, $"{kind} is introduced on level {level} but never sent there");
        }
    }

    [Fact]
    public void EndlessWavesSpendABudgetThatGrowsEveryWave()
    {
        var budget = SiegeLevels.EndlessBudget;
        for (var wave = 1; wave < 40; wave++)
        {
            Assert.True(budget.BudgetFor(wave + 1) > budget.BudgetFor(wave));
        }

        var board = Endless(5);
        var previous = 0;
        for (var index = 0; index < 20; index++)
        {
            board.BeginWave(index);
            Assert.Equal(budget.BudgetFor(index + 1), board.WaveSpent);
            Assert.True(board.WaveSpent > previous);
            Assert.True(board.WaveSize >= board.WaveSpent / 4);
            previous = board.WaveSpent;
        }

        Assert.True(SiegeRules.EndlessHealthScale(10) > SiegeRules.EndlessHealthScale(1));
        Assert.Equal(0, BossesIn(Endless(5), 9));
        Assert.Equal(1, BossesIn(Endless(5), 10));
        Assert.Equal(1, BossesIn(Endless(5), 20));
    }

    [Fact]
    public void EndlessCountsTheWavesItSurvives()
    {
        var board = Endless(11);
        board.Grant(2000f);
        for (var column = 0; column < SiegeRules.Columns; column++)
        {
            for (var row = 3; row < SiegeRules.Rows; row++)
            {
                board.Place(DefenderKind.Sprout, column, row);
            }
        }

        var cleared = 0;
        for (var tick = 0; tick < TicksPerSecond * 120 && board.WavesSurvived < 2; tick++)
        {
            board.BeginFrame();
            board.Tick();
            cleared += board.Events.ToArray().Count(item => item.Kind == SiegeEventKind.WaveCleared);
        }

        Assert.Equal(2, board.WavesSurvived);
        Assert.Equal(2, cleared);
        Assert.False(board.Over);
    }

    [Fact]
    public void StarsFollowTheGardenHealthLeft()
    {
        Assert.Equal(3, SiegeRules.Stars(5, 5));
        Assert.Equal(2, SiegeRules.Stars(4, 5));
        Assert.Equal(2, SiegeRules.Stars(3, 5));
        Assert.Equal(1, SiegeRules.Stars(2, 5));
        Assert.Equal(1, SiegeRules.Stars(1, 5));
        Assert.Equal(0, SiegeRules.Stars(0, 5));
        Assert.Equal(2, SiegeRules.Stars(5, 10));
        Assert.Equal(1, SiegeRules.Stars(4, 10));
    }

    [Fact]
    public void MandragorasThatReachTheGardenCostHealthAndZeroLosesTheLevel()
    {
        var hurt = Campaign(3);
        hurt.Spawn(EnemyKind.Runner, 0, 7.5f);
        hurt.Spawn(EnemyKind.Runner, 4, 7.5f);
        var events = Record(hurt, 2f);

        Assert.Equal(SiegeRules.GardenHealth - 2, hurt.Health);
        Assert.Equal(2, events.Count(item => item.Kind == SiegeEventKind.GardenHit));
        Assert.False(hurt.Over);

        var lost = Campaign(3);
        for (var column = 0; column < SiegeRules.Columns; column++)
        {
            lost.Spawn(EnemyKind.Runner, column, 7.5f);
        }

        Run(lost, 2f);

        Assert.Equal(0, lost.Health);
        Assert.Equal(SiegePhase.Lost, lost.Phase);
        Assert.Equal(0, lost.Stars);
        Assert.Equal(PlantResult.Closed, lost.Plant(DefenderKind.Sprout, 0, 0));
    }

    [Fact]
    public void ClearingTheFinalWaveWinsTheLevelWithStars()
    {
        var board = Campaign(1);
        for (var column = 0; column < SiegeRules.Columns; column++)
        {
            for (var row = 4; row < SiegeRules.Rows; row++)
            {
                board.Place(DefenderKind.Sprout, column, row);
            }
        }

        var wins = 0;
        for (var tick = 0; tick < TicksPerSecond * 300 && !board.Over; tick++)
        {
            board.BeginFrame();
            board.Tick();
            wins += board.Events.ToArray().Count(item => item.Kind == SiegeEventKind.LevelWon);
        }

        Assert.Equal(SiegePhase.Won, board.Phase);
        Assert.Equal(1, wins);
        Assert.Equal(3, board.Stars);
        Assert.Equal(12, board.Defeated);
        Assert.Equal(board.WaveCount - 1, board.Wave);
    }

    [Fact]
    public void SunbloomsDropMotesThatTurnIntoSunlight()
    {
        var board = Campaign(2);
        board.Place(DefenderKind.Sunbloom, 0, 7);

        var events = Record(board, SiegeRules.SunbloomFirstSeconds + SiegeRules.MoteHopSeconds + 0.1f);

        Assert.Contains(events, item => item.Kind == SiegeEventKind.SunProduced && item.Column == 0);
        var mote = board.MoteAt(new Vector2(0.5f, SiegeRules.Rows - 0.3f), 0.6f);
        Assert.True(mote >= 0);
        Assert.False(board.Mote(mote).Sky);
        var before = board.SunAmount;

        Assert.Equal((int)SiegeRules.SunValue, board.Collect(mote));
        Assert.Equal(before + SiegeRules.SunValue, board.SunAmount, 3);
        Assert.Equal((int)SiegeRules.SunValue, board.SunGathered);
        Assert.False(board.Mote(mote).Alive);
        Assert.Equal(0, board.Collect(mote));
    }

    [Fact]
    public void SkyMotesFallAndFadeWhenLeftAlone()
    {
        var board = Campaign(1);

        var events = Record(board, 25f);

        Assert.Contains(events, item => item.Kind == SiegeEventKind.SkyMote);
        Assert.Contains(events, item => item.Kind == SiegeEventKind.MoteExpired);
        Assert.Equal(0, board.SunGathered);
    }

    private static SiegeBoard Campaign(int level, ulong seed = 7)
    {
        var board = new SiegeBoard();
        board.StartCampaign(GameRandom.FromSeed(seed), level);
        return board;
    }

    private static SiegeBoard Endless(ulong seed)
    {
        var board = new SiegeBoard();
        board.StartEndless(GameRandom.FromSeed(seed));
        return board;
    }

    private static void Run(SiegeBoard board, float seconds)
    {
        var ticks = (int)MathF.Round(seconds * TicksPerSecond);
        for (var tick = 0; tick < ticks; tick++)
        {
            board.BeginFrame();
            board.Tick();
        }
    }

    private static List<SiegeEvent> Record(SiegeBoard board, float seconds)
    {
        var events = new List<SiegeEvent>();
        var ticks = (int)MathF.Round(seconds * TicksPerSecond);
        for (var tick = 0; tick < ticks; tick++)
        {
            board.BeginFrame();
            board.Tick();
            events.AddRange(board.Events.ToArray());
        }

        return events;
    }

    private static int BossesIn(SiegeBoard board, int wave)
    {
        board.BeginWave(wave - 1);
        var bosses = 0;
        for (var tick = 0; tick < TicksPerSecond * 30; tick++)
        {
            board.BeginFrame();
            board.Tick();
            bosses += board.Events.ToArray().Count(item =>
                item.Kind == SiegeEventKind.EnemySpawned && item.Detail == (byte)EnemyKind.Boss);
            if (board.Phase != SiegePhase.Spawning)
            {
                break;
            }
        }

        return bosses;
    }

    private static string Play(ulong seed)
    {
        var board = Campaign(9, seed);
        board.Grant(400f);
        for (var column = 0; column < SiegeRules.Columns; column++)
        {
            board.Place(DefenderKind.Sunbloom, column, SiegeRules.Rows - 1);
        }

        var trace = new StringBuilder();
        for (var tick = 0; tick < TicksPerSecond * 120 && !board.Over; tick++)
        {
            board.BeginFrame();
            board.Tick();
            for (var index = 0; index < SiegeBoard.MoteCapacity; index++)
            {
                if (board.Mote(index).Alive && board.Mote(index).Landed)
                {
                    board.Collect(index);
                }
            }

            PlantNext(board);
            if (tick % 30 != 0)
            {
                continue;
            }

            var positions = 0f;
            for (var index = 0; index < SiegeBoard.EnemyCapacity; index++)
            {
                if (board.Enemy(index).Alive)
                {
                    positions += board.Enemy(index).Y * (index + 1) + board.Enemy(index).Column;
                }
            }

            trace.Append(CultureInfo.InvariantCulture,
                $"{board.Time:F2}|{board.Health}|{board.Defeated}|{board.Sunlight}|{board.AliveEnemies}|{positions:F3};");
        }

        return trace.ToString();
    }

    private static void PlantNext(SiegeBoard board)
    {
        if (!board.Ready(DefenderKind.Sprout) || !board.Affordable(DefenderKind.Sprout))
        {
            return;
        }

        for (var row = SiegeRules.Rows - 2; row >= 3; row--)
        {
            for (var column = 0; column < SiegeRules.Columns; column++)
            {
                if (board.Plant(DefenderKind.Sprout, column, row) == PlantResult.Planted)
                {
                    return;
                }
            }
        }
    }
}
