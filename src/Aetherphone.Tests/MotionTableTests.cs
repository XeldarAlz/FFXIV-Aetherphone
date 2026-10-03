using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MotionTableTests
{
    private const float Tolerance = 1e-6f;

    public static TheoryData<string, float, float> StandardTable() => new()
    {
        { nameof(Motion.PressIn), Motion.PressIn, 0.07f },
        { nameof(Motion.Release), Motion.Release, 0.16f },
        { nameof(Motion.HoverLift), Motion.HoverLift, 0.12f },
        { nameof(Motion.PageSettle), Motion.PageSettle, 0.18f },
        { nameof(Motion.Sheet), Motion.Sheet, 0.22f },
        { nameof(Motion.Island), Motion.Island, 0.20f },
        { nameof(Motion.SwitcherReveal), Motion.SwitcherReveal, 0.19f },
        { nameof(Motion.TabBar), Motion.TabBar, 0.16f },
        { nameof(Motion.Appear), Motion.Appear, 0.14f },
        { nameof(Motion.PressScaleControl), Motion.PressScaleControl, 0.93f },
        { nameof(Motion.PressScaleCard), Motion.PressScaleCard, 0.98f },
        { nameof(Motion.HoverLiftIcon), Motion.HoverLiftIcon, 0.05f },
        { nameof(Motion.HoverLiftCard), Motion.HoverLiftCard, 0.01f },
    };

    [Theory]
    [MemberData(nameof(StandardTable))]
    public void EveryMotionValueMatchesTheStandard(string name, float actual, float expected)
    {
        Assert.True(MathF.Abs(actual - expected) <= Tolerance, $"{name} drifted from {expected} to {actual}");
    }

    [Fact]
    public void TheTableCoversEveryMotionConstant()
    {
        var fields = typeof(Motion).GetFields(System.Reflection.BindingFlags.Public |
                                              System.Reflection.BindingFlags.Static);
        Assert.Equal(fields.Length, StandardTable().Count);
    }

    [Fact]
    public void PressInIsFasterThanRelease()
    {
        Assert.True(Motion.PressIn < Motion.Release);
    }

    [Fact]
    public void CardsMoveLessThanControls()
    {
        Assert.True(Motion.PressScaleCard > Motion.PressScaleControl);
        Assert.True(Motion.HoverLiftCard < Motion.HoverLiftIcon);
    }

    [Fact]
    public void SharedPrimitivesReadTheTable()
    {
        Assert.Equal(Motion.PressIn, PressFx.PressSmoothTime, Tolerance);
        Assert.Equal(Motion.Release, PressFx.ReleaseSmoothTime, Tolerance);
        Assert.Equal(Motion.PressScaleControl, PressFx.DefaultPressedScale, Tolerance);
        Assert.Equal(Motion.PressScaleControl, PressFx.ControlPressedScale, Tolerance);
        Assert.Equal(Motion.PressScaleControl, PressFx.IconPressedScale, Tolerance);
        Assert.Equal(Motion.PressScaleCard, PressFx.CardPressedScale, Tolerance);
        Assert.Equal(Motion.HoverLift, HoverFx.DefaultSmoothTime, Tolerance);
        Assert.Equal(Motion.Sheet, SheetMetrics.PresentSmoothTime, Tolerance);
    }
}
