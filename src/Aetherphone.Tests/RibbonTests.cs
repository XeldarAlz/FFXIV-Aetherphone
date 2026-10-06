using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RibbonTests
{
    [Fact]
    public void ClaimingANewOwnerDropsTheOldTrail()
    {
        var ribbon = new Ribbon();
        ribbon.Claim(7);
        ribbon.Push(Vector2.Zero);
        ribbon.Push(Vector2.One);
        Assert.Equal(2, ribbon.Count);

        ribbon.Claim(7);
        Assert.Equal(2, ribbon.Count);

        ribbon.Claim(8);
        Assert.Equal(0, ribbon.Count);
        Assert.Equal(8, ribbon.Owner);
    }

    [Fact]
    public void ReleaseForgetsTheOwner()
    {
        var ribbon = new Ribbon();
        ribbon.Claim(3);
        ribbon.Push(Vector2.One);
        ribbon.Release();

        Assert.Equal(-1, ribbon.Owner);
        Assert.Equal(0, ribbon.Count);
    }

    [Fact]
    public void TheRingBufferKeepsTheNewestTwentyFourPoints()
    {
        var ribbon = new Ribbon();
        for (var index = 0; index < Ribbon.Capacity + 5; index++)
        {
            ribbon.Push(new Vector2(index, 0f));
        }

        Assert.Equal(Ribbon.Capacity, ribbon.Count);
        Assert.Equal(new Vector2(Ribbon.Capacity + 4, 0f), ribbon.Point(0));
        Assert.Equal(new Vector2(5f, 0f), ribbon.Point(Ribbon.Capacity - 1));
    }
}
