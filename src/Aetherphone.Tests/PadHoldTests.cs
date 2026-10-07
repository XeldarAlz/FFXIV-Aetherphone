using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PadHoldTests
{
    private const float DeadZone = 20f;
    private static readonly Vector2 Center = new(100f, 100f);

    [Fact]
    public void APressOnThePadHoldsTheDirectionUnderThePointer()
    {
        var pad = new HeldPadState();

        var pressed = pad.Update(Center + new Vector2(0f, -50f), true, true, Center, DeadZone);

        Assert.Equal(PadDirection.Up, pressed);
        Assert.Equal(PadDirection.Up, pad.Held);
        Assert.True(pad.Engaged);
        Assert.Equal(PadDirection.None, pad.Update(Center + new Vector2(2f, -48f), true, false, Center, DeadZone));
        Assert.Equal(PadDirection.Up, pad.Held);
    }

    [Fact]
    public void SlidingToANeighbourSwitchesTheHeldDirectionAndReportsOnePress()
    {
        var pad = new HeldPadState();
        pad.Update(Center + new Vector2(-50f, 0f), true, true, Center, DeadZone);

        Assert.Equal(PadDirection.Down, pad.Update(Center + new Vector2(-10f, 50f), true, false, Center, DeadZone));
        Assert.Equal(PadDirection.Down, pad.Held);
        Assert.Equal(PadDirection.Right, pad.Update(Center + new Vector2(60f, 5f), true, false, Center, DeadZone));
        Assert.Equal(PadDirection.Right, pad.Held);
    }

    [Fact]
    public void TheCentreHoldsNothingUntilThePointerLeavesTheDeadZone()
    {
        var pad = new HeldPadState();

        Assert.Equal(PadDirection.None, pad.Update(Center + new Vector2(5f, 5f), true, true, Center, DeadZone));
        Assert.True(pad.Engaged);
        Assert.Equal(PadDirection.None, pad.Held);
        Assert.Equal(PadDirection.Left, pad.Update(Center + new Vector2(-30f, 0f), true, false, Center, DeadZone));
        Assert.Equal(PadDirection.None, pad.Update(Center, true, false, Center, DeadZone));
        Assert.Equal(PadDirection.None, pad.Held);
        Assert.Equal(PadDirection.Left, pad.Update(Center + new Vector2(-30f, 0f), true, false, Center, DeadZone));
    }

    [Fact]
    public void SlidingOffThePadKeepsSteeringByTheSectorUntilRelease()
    {
        var pad = new HeldPadState();
        pad.Update(Center + new Vector2(0f, 50f), true, true, Center, DeadZone);

        Assert.Equal(PadDirection.None, pad.Update(Center + new Vector2(10f, 400f), true, false, Center, DeadZone));
        Assert.Equal(PadDirection.Down, pad.Held);
        Assert.Equal(PadDirection.Right, pad.Update(Center + new Vector2(500f, 30f), true, false, Center, DeadZone));
        Assert.True(pad.Engaged);
    }

    [Fact]
    public void ReleasingDropsTheHoldAndOnlyAPressOnThePadReengages()
    {
        var pad = new HeldPadState();
        pad.Update(Center + new Vector2(50f, 0f), true, true, Center, DeadZone);

        Assert.Equal(PadDirection.None, pad.Update(Center + new Vector2(50f, 0f), false, false, Center, DeadZone));
        Assert.False(pad.Engaged);
        Assert.Equal(PadDirection.None, pad.Held);
        Assert.Equal(PadDirection.None, pad.Update(Center + new Vector2(50f, 0f), true, false, Center, DeadZone));
        Assert.Equal(PadDirection.None, pad.Held);
        Assert.Equal(PadDirection.Right, pad.Update(Center + new Vector2(50f, 0f), true, true, Center, DeadZone));
        Assert.Equal(PadDirection.Right, pad.Held);

        pad.Release();
        Assert.False(pad.Engaged);
        Assert.Equal(PadDirection.None, pad.Held);
    }

    [Fact]
    public void TheSectorFavoursTheLongerAxis()
    {
        Assert.Equal(PadDirection.Right, HeldPadState.Sector(new Vector2(30f, 29f), DeadZone));
        Assert.Equal(PadDirection.Up, HeldPadState.Sector(new Vector2(29f, -30f), DeadZone));
        Assert.Equal(PadDirection.Down, HeldPadState.Sector(new Vector2(30f, 30f), DeadZone));
        Assert.Equal(PadDirection.None, HeldPadState.Sector(new Vector2(12f, 12f), DeadZone));
    }

    [Fact]
    public void AHoldLatchPressesInsideAndReleasesAnywhere()
    {
        var latch = new HoldLatch();

        Assert.Equal(HoldEdge.None, latch.Update(false, true));
        Assert.False(latch.Held);
        Assert.Equal(HoldEdge.Pressed, latch.Update(true, true));
        Assert.True(latch.Held);
        Assert.Equal(HoldEdge.None, latch.Update(false, true));
        Assert.Equal(HoldEdge.None, latch.Update(true, true));
        Assert.Equal(HoldEdge.Released, latch.Update(false, false));
        Assert.False(latch.Held);
        Assert.Equal(HoldEdge.None, latch.Update(false, false));
        Assert.Equal(HoldEdge.Pressed, latch.Update(true, true));
        latch.Release();
        Assert.False(latch.Held);
        Assert.Equal(HoldEdge.None, latch.Update(false, false));
    }
}
