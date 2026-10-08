using Aetherphone.Apps.Casino.Cabinets;
using Xunit;

namespace Aetherphone.Tests;

public sealed class WheelPointerTests
{
    [Fact]
    public void AKickFlicksThePointerWithTheRimAndTheSpringBringsItHome()
    {
        var pointer = new WheelPointer();
        pointer.Kick(10f);
        pointer.Step(0.05f);
        Assert.True(pointer.Deflection > 0f);

        pointer.Step(3f);
        Assert.True(pointer.Resting);
        Assert.Equal(0f, pointer.Deflection);
    }

    [Fact]
    public void ASlowingRimKicksSofterThanAFastOne()
    {
        Assert.True(WheelPointer.KickFor(2f) < WheelPointer.KickFor(8f));
        Assert.Equal(WheelPointer.KickFor(-6f), WheelPointer.KickFor(6f));
        Assert.Equal(0f, WheelPointer.KickFor(0f));
        Assert.Equal(WheelPointer.MaxKick, WheelPointer.KickFor(1_000f));
    }

    [Fact]
    public void AHammeredPointerNeverPassesItsStop()
    {
        var pointer = new WheelPointer();
        for (var frame = 0; frame < 240; frame++)
        {
            pointer.Kick(40f);
            pointer.Step(1f / 60f);
            Assert.InRange(pointer.Deflection, -WheelPointer.MaxDeflection, WheelPointer.MaxDeflection);
        }
    }

    [Fact]
    public void TheSpringIsTheSameAtAnyFrameRate()
    {
        var fine = new WheelPointer();
        var coarse = new WheelPointer();
        fine.Kick(5f);
        coarse.Kick(5f);
        for (var frame = 0; frame < 6; frame++)
        {
            fine.Step(1f / 60f);
        }

        coarse.Step(0.1f);
        Assert.Equal(fine.Deflection, coarse.Deflection, 3);
    }

    [Fact]
    public void ResetStopsThePointerDead()
    {
        var pointer = new WheelPointer();
        pointer.Kick(20f);
        pointer.Step(0.02f);
        pointer.Reset();
        Assert.True(pointer.Resting);
    }
}
