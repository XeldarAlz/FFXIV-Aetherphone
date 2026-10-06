using Aetherphone.Apps.Games.Framework;
using Xunit;

namespace Aetherphone.Tests;

public sealed class GameRandomTests
{
    [Fact]
    public void TheSameSeedReplaysTheSameSequence()
    {
        var first = GameRandom.FromSeed(0xC0FFEE);
        var second = GameRandom.FromSeed(0xC0FFEE);

        for (var step = 0; step < 256; step++)
        {
            Assert.Equal(first.NextUInt(), second.NextUInt());
            Assert.Equal(first.Next(100), second.Next(100));
            Assert.Equal(first.NextFloat(), second.NextFloat());
            Assert.Equal(first.Sign(), second.Sign());
        }
    }

    [Fact]
    public void DifferentSeedsDiverge()
    {
        var first = GameRandom.FromSeed(1);
        var second = GameRandom.FromSeed(2);
        var same = 0;
        for (var step = 0; step < 64; step++)
        {
            if (first.NextUInt() == second.NextUInt())
            {
                same++;
            }
        }

        Assert.True(same < 4);
    }

    [Fact]
    public void AZeroSeedStillProducesAStream()
    {
        var random = GameRandom.FromSeed(0);
        var seen = new HashSet<uint>();
        for (var step = 0; step < 32; step++)
        {
            seen.Add(random.NextUInt());
        }

        Assert.True(seen.Count > 16);
    }

    [Fact]
    public void NextStaysInsideTheRangeAndCoversIt()
    {
        var random = GameRandom.FromSeed(42);
        var counts = new int[6];
        for (var step = 0; step < 60_000; step++)
        {
            var value = random.Next(6);
            Assert.InRange(value, 0, 5);
            counts[value]++;
        }

        for (var face = 0; face < counts.Length; face++)
        {
            Assert.InRange(counts[face], 9_000, 11_000);
        }
    }

    [Fact]
    public void NextWithBoundsHonoursBothEnds()
    {
        var random = GameRandom.FromSeed(7);
        var sawMin = false;
        var sawMax = false;
        for (var step = 0; step < 10_000; step++)
        {
            var value = random.Next(-3, 3);
            Assert.InRange(value, -3, 2);
            sawMin |= value == -3;
            sawMax |= value == 2;
        }

        Assert.True(sawMin);
        Assert.True(sawMax);
        Assert.Equal(0, random.Next(0));
    }

    [Fact]
    public void NextFloatIsUniformOnTheUnitInterval()
    {
        var random = GameRandom.FromSeed(99);
        var sum = 0.0;
        for (var step = 0; step < 100_000; step++)
        {
            var value = random.NextFloat();
            Assert.InRange(value, 0f, 0.99999994f);
            sum += value;
        }

        Assert.InRange(sum / 100_000, 0.49, 0.51);
    }

    [Fact]
    public void ChanceMatchesItsProbability()
    {
        var random = GameRandom.FromSeed(3);
        var hits = 0;
        for (var step = 0; step < 50_000; step++)
        {
            if (random.Chance(0.25f))
            {
                hits++;
            }
        }

        Assert.InRange(hits, 11_500, 13_500);
    }

    [Fact]
    public void DailySeedIsStableForAGameAndDay()
    {
        Assert.Equal(GameSeed.Daily("whack", 20_000), GameSeed.Daily("whack", 20_000));
        Assert.NotEqual(GameSeed.Daily("whack", 20_000), GameSeed.Daily("whack", 20_001));
        Assert.NotEqual(GameSeed.Daily("whack", 20_000), GameSeed.Daily("snake", 20_000));
    }

    [Fact]
    public void GameStartExposesTheSeededRandom()
    {
        var start = new GameStart(0, 1234, false);
        var first = start.Random;
        var second = GameRandom.FromSeed(1234);

        Assert.Equal(second.NextUInt(), first.NextUInt());
    }
}
