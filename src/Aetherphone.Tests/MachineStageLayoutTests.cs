using System.Numerics;
using Aetherphone.Apps.Casino.Machines;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MachineStageLayoutTests
{
    [Theory]
    [InlineData(0.75f, 852f)]
    [InlineData(1f, 852f)]
    [InlineData(1.5f, 852f)]
    [InlineData(1f, 640f)]
    [InlineData(1.5f, 560f)]
    public void TheGlassWindowAndStripStackInsideSafe(float scale, float designHeight)
    {
        var screen = new Rect(Vector2.Zero, new Vector2(393f * scale, designHeight * scale));
        var content = new Rect(new Vector2(16f * scale, 48f * scale),
            new Vector2(377f * scale, (designHeight - 30f) * scale));
        var stage = CasinoStageLayout.Compute(screen, content, false, false, BetDeckLayout.DeckHeightFor(true, false),
            scale);
        var layout = MachineStageLayout.Compute(stage.Full, stage.Safe, scale);
        BetDeckLayoutTests.AssertDisjoint(new[] { layout.Glass, layout.Window, layout.Strip });
        Assert.True(layout.Glass.Max.Y <= layout.Window.Min.Y);
        Assert.True(layout.Window.Max.Y <= layout.Strip.Min.Y);
        Assert.True(layout.Window.Height > layout.Glass.Height, designHeight.ToString());
        Assert.True(layout.Chassis.Min.Y >= stage.Safe.Min.Y - 0.01f);
        Assert.True(layout.Chassis.Max.Y <= stage.Deck.Min.Y);
        Assert.True(layout.Chassis.Min.X >= stage.Full.Min.X && layout.Chassis.Max.X <= stage.Full.Max.X);
    }
}
