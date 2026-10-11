using System.Numerics;
using Aetherphone.Apps.Casino.Machines;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CascadePopTests
{
    private const float Frame = 1f / 60f;
    private const float Cell = 56f;
    private static readonly Vector4 Tint = new(0.6f, 0.9f, 1f, 1f);

    [Fact]
    public void AWholeBoardClearingStaysUnderTheHardCap()
    {
        var pop = new CascadePop();
        var random = GameRandom.FromSeed(11);
        for (var burst = 0; burst < CrystalCascadeRules.CellCount * 4; burst++)
        {
            pop.Burst(new Vector2(burst % 6 * Cell, burst / 6 % 5 * Cell), Extent, Tint, ref random);
            Assert.True(pop.SparkCount <= CascadePop.SparkCapacity);
            Assert.True(pop.FlashCount <= CascadePop.CellCapacity);
        }

        Assert.Equal(CascadePop.SparkCapacity, pop.SparkCount);
        Assert.Equal(CascadePop.CellCapacity, pop.FlashCount);
        Assert.True(CascadePop.SparksPerCell <= 6);
    }

    [Fact]
    public void ThePopIsOverInUnderEightTenthsOfASecond()
    {
        var pop = new CascadePop();
        var random = GameRandom.FromSeed(5);
        for (var cell = 0; cell < CrystalCascadeRules.CellCount; cell++)
        {
            pop.Burst(new Vector2(cell * Cell, 0f), Extent, Tint, ref random);
        }

        var seconds = 0f;
        while (pop.Active && seconds < 2f)
        {
            pop.Update(Frame);
            seconds += Frame;
        }

        Assert.False(pop.Active);
        Assert.True(seconds < 0.8f, seconds.ToString());
    }

    [Fact]
    public void EverySparkStaysOnItsOwnCell()
    {
        var pop = new CascadePop();
        var random = GameRandom.FromSeed(19);
        var origin = new Vector2(Cell * 2.5f, Cell * 1.5f);
        for (var burst = 0; burst < 8; burst++)
        {
            pop.Burst(origin, Extent, Tint, ref random);
        }

        Assert.True(CascadePop.MaxTravel(Extent) + CascadePop.MaxSize(Extent) * 1.6f < Cell * 0.5f);
        while (pop.Active)
        {
            for (var index = 0; index < pop.SparkCount; index++)
            {
                var spark = pop.SparkAt(index);
                var travel = Vector2.Distance(CascadePop.PositionOf(spark), origin);
                Assert.True(travel <= CascadePop.MaxTravel(Extent) + 0.001f);
            }

            pop.Update(Frame);
        }
    }

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = new CascadePop();
        var second = new CascadePop();
        var firstRandom = GameRandom.FromSeed(42);
        var secondRandom = GameRandom.FromSeed(42);
        first.Burst(Vector2.Zero, Extent, Tint, ref firstRandom);
        second.Burst(Vector2.Zero, Extent, Tint, ref secondRandom);
        first.Update(0.1f);
        second.Update(0.1f);
        Assert.Equal(first.SparkCount, second.SparkCount);
        for (var index = 0; index < first.SparkCount; index++)
        {
            Assert.Equal(CascadePop.PositionOf(first.SparkAt(index)), CascadePop.PositionOf(second.SparkAt(index)));
        }
    }

    private static float Extent => Cell * MachineReels.SymbolShare;
}
