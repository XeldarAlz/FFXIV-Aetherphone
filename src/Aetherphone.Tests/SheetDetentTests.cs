using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SheetDetentTests
{
    private const float Tolerance = 1e-4f;
    private const float ScreenHeight = 700f;
    private const float FittedHeight = 260f;
    private const float Still = 0f;
    private const float FlungDown = -1000f;
    private const float FlungUp = 1000f;

    private static readonly SheetDetents Standard = SheetDetents.Standard(ScreenHeight);
    private static readonly SheetDetents Fitted = SheetDetents.Fitted(FittedHeight);

    [Fact]
    public void StandardDetentsAreHalfAndNinetyTwoPercentOfTheScreen()
    {
        Assert.Equal(350f, Standard.Medium, Tolerance);
        Assert.Equal(644f, Standard.Large, Tolerance);
        Assert.True(Standard.Resizable);
        Assert.False(Fitted.Resizable);
    }

    [Fact]
    public void RestingAtMediumStaysAtMedium()
    {
        Assert.Equal(Standard.Medium, SheetMetrics.Snap(Standard.Medium, Still, in Standard, 1f), Tolerance);
    }

    [Fact]
    public void SmallDragAboveMediumReturnsToMedium()
    {
        Assert.Equal(Standard.Medium, SheetMetrics.Snap(400f, Still, in Standard, 1f), Tolerance);
    }

    [Fact]
    public void ReleasePastTheMidpointSnapsToLarge()
    {
        var midpoint = (Standard.Medium + Standard.Large) * 0.5f;
        Assert.Equal(Standard.Large, SheetMetrics.Snap(midpoint + 1f, Still, in Standard, 1f), Tolerance);
        Assert.Equal(Standard.Medium, SheetMetrics.Snap(midpoint - 1f, Still, in Standard, 1f), Tolerance);
    }

    [Fact]
    public void DraggedBelowTheDismissThresholdDismisses()
    {
        var threshold = Standard.Medium * (1f - SheetMetrics.DismissFraction);
        Assert.Equal(0f, SheetMetrics.Snap(threshold - 1f, Still, in Standard, 1f), Tolerance);
        Assert.Equal(Standard.Medium, SheetMetrics.Snap(threshold + 1f, Still, in Standard, 1f), Tolerance);
    }

    [Fact]
    public void FlingDownFromLargeLandsOnMedium()
    {
        Assert.Equal(Standard.Medium, SheetMetrics.Snap(600f, FlungDown, in Standard, 1f), Tolerance);
    }

    [Fact]
    public void FlingDownFromMediumDismisses()
    {
        Assert.Equal(0f, SheetMetrics.Snap(Standard.Medium, FlungDown, in Standard, 1f), Tolerance);
    }

    [Fact]
    public void FlingUpGoesLarge()
    {
        Assert.Equal(Standard.Large, SheetMetrics.Snap(360f, FlungUp, in Standard, 1f), Tolerance);
    }

    [Fact]
    public void FittedSheetNeverGrowsAndReturnsToItsHeight()
    {
        Assert.Equal(FittedHeight, SheetMetrics.Snap(FittedHeight + 10f, Still, in Fitted, 1f), Tolerance);
        Assert.Equal(FittedHeight, SheetMetrics.Snap(FittedHeight, FlungUp, in Fitted, 1f), Tolerance);
    }

    [Fact]
    public void FittedSheetFlungDownDismisses()
    {
        Assert.Equal(0f, SheetMetrics.Snap(FittedHeight, FlungDown, in Fitted, 1f), Tolerance);
    }

    [Fact]
    public void FlingThresholdScalesWithTheUi()
    {
        Assert.Equal(0f, SheetMetrics.Snap(Standard.Medium, FlungDown, in Standard, 1f), Tolerance);
        Assert.Equal(Standard.Medium, SheetMetrics.Snap(Standard.Medium, FlungDown, in Standard, 2f), Tolerance);
    }

    [Fact]
    public void VeilIsDeeperInsideApps()
    {
        Assert.Equal(0.45f, SheetMetrics.VeilFor(true), Tolerance);
        Assert.Equal(0.35f, SheetMetrics.VeilFor(false), Tolerance);
        Assert.True(SheetMetrics.VeilFor(true) > SheetMetrics.VeilFor(false));
    }

    [Fact]
    public void GrabberMatchesTheContract()
    {
        Assert.Equal(36f, SheetMetrics.GrabberWidth, Tolerance);
        Assert.Equal(5f, SheetMetrics.GrabberHeight, Tolerance);
        Assert.Equal(8f, SheetMetrics.GrabberTop, Tolerance);
        Assert.Equal(0.22f, SheetMetrics.PresentSmoothTime, Tolerance);
    }
}
