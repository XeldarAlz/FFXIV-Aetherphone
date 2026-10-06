using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.World;
using Xunit;

namespace Aetherphone.Tests;

public sealed class WaveSpawnerTests
{
    private const int Lanes = 5;
    private const byte Walker = 0;
    private const byte Runner = 1;
    private const byte Armoured = 2;
    private const byte Boss = 3;

    private static readonly int[] Costs = { 1, 2, 4, 25 };
    private static readonly int[] Unlocks = { 1, 3, 5, 10 };

    [Fact]
    public void ATablePlaysBackInTimeOrderWithTiesInTableOrder()
    {
        var spawner = new WaveSpawner(Lanes);
        spawner.Load(new[]
        {
            new WaveEntry(4f, 0, Walker),
            new WaveEntry(1f, 2, Runner),
            new WaveEntry(2.5f, 1, Walker),
            new WaveEntry(1f, 3, Armoured),
            new WaveEntry(0f, 4, Walker),
        });

        var played = new List<WaveEntry>();
        Span<WaveEntry> due = stackalloc WaveEntry[8];
        for (var frame = 0; frame < 60; frame++)
        {
            var count = spawner.Advance(0.1f, due);
            for (var index = 0; index < count; index++)
            {
                played.Add(due[index]);
            }
        }

        Assert.Equal(new byte[] { 4, 2, 3, 1, 0 }, played.Select(entry => entry.Lane).ToArray());
        Assert.True(spawner.Finished);
        Assert.Equal(5, spawner.Spawned);
        Assert.Equal(4f, spawner.Duration);
    }

    [Fact]
    public void NothingSpawnsBeforeItsTime()
    {
        var spawner = new WaveSpawner(Lanes);
        spawner.Load(new[] { new WaveEntry(1f, 0, Walker), new WaveEntry(2f, 1, Walker) });
        Span<WaveEntry> due = stackalloc WaveEntry[4];

        Assert.Equal(0, spawner.Advance(0.75f, due));
        Assert.Equal(1, spawner.Advance(0.25f, due));
        Assert.Equal(0, spawner.Advance(0.5f, due));
        Assert.Equal(1, spawner.Remaining);
        Assert.Equal(1, spawner.Advance(0.5f, due));
        Assert.Equal(1, due[0].Lane);
        Assert.Equal(0, spawner.Advance(10f, due));
    }

    [Fact]
    public void SpawnsThatDoNotFitTheSpanWaitForTheNextCall()
    {
        var spawner = new WaveSpawner(Lanes);
        spawner.Load(new[]
        {
            new WaveEntry(0.1f, 0, Walker), new WaveEntry(0.2f, 1, Walker), new WaveEntry(0.3f, 2, Walker),
        });
        Span<WaveEntry> due = stackalloc WaveEntry[2];

        Assert.Equal(2, spawner.Advance(1f, due));
        Assert.Equal(1, spawner.Advance(0f, due));
        Assert.Equal(2, due[0].Lane);
    }

    [Fact]
    public void RewindReplaysTheSameTable()
    {
        var spawner = new WaveSpawner(Lanes);
        spawner.Load(new[] { new WaveEntry(0.5f, 3, Runner) });
        Span<WaveEntry> due = stackalloc WaveEntry[2];
        spawner.Advance(1f, due);

        spawner.Rewind();

        Assert.False(spawner.Finished);
        Assert.Equal(0f, spawner.Elapsed);
        Assert.Equal(1, spawner.Advance(1f, due));
    }

    [Fact]
    public void ATableLongerThanTheCapacityIsTrimmed()
    {
        var spawner = new WaveSpawner(Lanes, 2);

        spawner.Load(new[] { new WaveEntry(3f, 0, Walker), new WaveEntry(1f, 0, Walker), new WaveEntry(0f, 0, Walker) });

        Assert.Equal(2, spawner.Count);
        Assert.Equal(1f, spawner.Entries[0].Time);
    }

    [Fact]
    public void EndlessWavesAreSeeded()
    {
        var budget = Budget();
        var first = new WaveSpawner(Lanes);
        var second = new WaveSpawner(Lanes);
        var other = new WaveSpawner(Lanes);
        var firstRandom = GameRandom.FromSeed(55UL);
        var secondRandom = GameRandom.FromSeed(55UL);
        var otherRandom = GameRandom.FromSeed(56UL);

        first.Endless(ref firstRandom, 12, budget);
        second.Endless(ref secondRandom, 12, budget);
        other.Endless(ref otherRandom, 12, budget);

        Assert.Equal(first.Count, second.Count);
        for (var index = 0; index < first.Count; index++)
        {
            Assert.Equal(first.Entries[index].Time, second.Entries[index].Time);
            Assert.Equal(first.Entries[index].Lane, second.Entries[index].Lane);
            Assert.Equal(first.Entries[index].EnemyKind, second.Entries[index].EnemyKind);
        }

        Assert.False(Same(first, other), "a different seed should build a different wave");
    }

    [Fact]
    public void EndlessBudgetGrowsEveryWaveAndIsNeverOverspent()
    {
        var budget = Budget();
        var spawner = new WaveSpawner(Lanes);
        var random = GameRandom.FromSeed(9UL);
        var previousBudget = 0;
        var spentAtWaveOne = 0;
        var spentAtWaveTwenty = 0;

        for (var wave = 1; wave <= 20; wave++)
        {
            var available = budget.BudgetFor(wave);
            Assert.True(available > previousBudget);
            previousBudget = available;

            spawner.Endless(ref random, wave, budget);

            var spent = 0;
            for (var index = 0; index < spawner.Count; index++)
            {
                spent += budget.Cost(spawner.Entries[index].EnemyKind);
            }

            Assert.Equal(spent, spawner.SpentBudget);
            Assert.InRange(spent, available - 1, available);
            spentAtWaveOne = wave == 1 ? spent : spentAtWaveOne;
            spentAtWaveTwenty = spent;
        }

        Assert.True(spentAtWaveTwenty > spentAtWaveOne * 5);
    }

    [Fact]
    public void EndlessOnlyFieldsUnlockedKinds()
    {
        var budget = Budget();
        var spawner = new WaveSpawner(Lanes);
        var random = GameRandom.FromSeed(3UL);

        spawner.Endless(ref random, 4, budget);
        for (var index = 0; index < spawner.Count; index++)
        {
            Assert.True(spawner.Entries[index].EnemyKind is Walker or Runner);
        }

        var sawBoss = false;
        for (var attempt = 0; attempt < 10 && !sawBoss; attempt++)
        {
            spawner.Endless(ref random, 30, budget);
            for (var index = 0; index < spawner.Count; index++)
            {
                sawBoss |= spawner.Entries[index].EnemyKind == Boss;
            }
        }

        Assert.True(sawBoss);
    }

    [Fact]
    public void EndlessSpreadsTheBudgetAcrossEveryLaneInTimeOrder()
    {
        var budget = Budget();
        var spawner = new WaveSpawner(Lanes);
        var random = GameRandom.FromSeed(21UL);

        spawner.Endless(ref random, 15, budget);

        Span<int> perLane = stackalloc int[Lanes];
        var previousTime = 0f;
        for (var index = 0; index < spawner.Count; index++)
        {
            var entry = spawner.Entries[index];
            Assert.InRange(entry.Lane, (byte)0, (byte)(Lanes - 1));
            Assert.True(entry.Time >= previousTime);
            Assert.InRange(entry.Time, 0f, budget.WaveSeconds);
            previousTime = entry.Time;
            perLane[entry.Lane] += budget.Cost(entry.EnemyKind);
        }

        for (var lane = 0; lane < Lanes; lane++)
        {
            Assert.True(perLane[lane] > 0, $"lane {lane} should get a share of the wave");
        }
    }

    [Fact]
    public void EndlessStopsAtTheCapacity()
    {
        var budget = Budget();
        var spawner = new WaveSpawner(Lanes, 6);
        var random = GameRandom.FromSeed(1UL);

        Assert.Equal(6, spawner.Endless(ref random, 40, budget));
    }

    private static EnemyBudget Budget() => new(Costs, Unlocks, 6, 4, 30f);

    private static bool Same(WaveSpawner first, WaveSpawner second)
    {
        if (first.Count != second.Count)
        {
            return false;
        }

        for (var index = 0; index < first.Count; index++)
        {
            if (first.Entries[index].Lane != second.Entries[index].Lane
                || first.Entries[index].EnemyKind != second.Entries[index].EnemyKind)
            {
                return false;
            }
        }

        return true;
    }
}
