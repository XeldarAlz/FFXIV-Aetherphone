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
    public void ASpacedPushMovesTheHeadUntilItTravelsFarEnough()
    {
        var ribbon = new Ribbon();
        ribbon.PushSpaced(Vector2.Zero, 1f);
        ribbon.PushSpaced(new Vector2(0.2f, 0f), 1f);
        Assert.Equal(2, ribbon.Count);

        ribbon.PushSpaced(new Vector2(0.5f, 0f), 1f);
        ribbon.PushSpaced(new Vector2(0.9f, 0f), 1f);
        Assert.Equal(2, ribbon.Count);
        Assert.Equal(new Vector2(0.9f, 0f), ribbon.Point(0));
        Assert.Equal(Vector2.Zero, ribbon.Point(1));

        ribbon.PushSpaced(new Vector2(1.1f, 0f), 1f);
        Assert.Equal(3, ribbon.Count);
        Assert.Equal(new Vector2(1.1f, 0f), ribbon.Point(0));
        Assert.Equal(1f, ribbon.Point(1).X, 4);
        Assert.Equal(Vector2.Zero, ribbon.Point(2));

        ribbon.PushSpaced(new Vector2(1.6f, 0f), 1f);
        Assert.Equal(3, ribbon.Count);
        Assert.Equal(new Vector2(1.6f, 0f), ribbon.Point(0));
    }

    [Fact]
    public void ASpacedTrailCoversTheSameDistanceAtAnyFrameRate()
    {
        const float speed = 10f;
        var spacing = Ribbon.Spacing(speed);
        var slow = new Ribbon();
        var fast = new Ribbon();
        for (var frame = 0; frame <= 120; frame++)
        {
            slow.PushSpaced(new Vector2(speed * frame / 60f, 0f), spacing);
        }

        for (var frame = 0; frame <= 288; frame++)
        {
            fast.PushSpaced(new Vector2(speed * frame / 144f, 0f), spacing);
        }

        var slowLength = slow.Point(0).X - slow.Point(slow.Count - 1).X;
        var fastLength = fast.Point(0).X - fast.Point(fast.Count - 1).X;
        Assert.Equal(Ribbon.Capacity, slow.Count);
        Assert.Equal(Ribbon.Capacity, fast.Count);
        Assert.InRange(fastLength, slowLength * 0.8f, slowLength * 1.25f);
        Assert.Equal(speed / 60f * (Ribbon.Capacity - 1), slowLength, 3);
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
