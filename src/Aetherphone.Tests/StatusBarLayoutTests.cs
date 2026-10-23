using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class StatusBarLayoutTests
{
    private const float SidePadding = 16f;
    private const float ClockWidth = 35f;
    private const float ClusterWidth = 70.4f;
    private const float MinRightPadding = 8f;
    private const float IndicatorSlot = 17f;

    public static TheoryData<float, float> ScreensAndScales => new()
    {
        { 308.5f, 1f },
        { 328f, 1f },
        { 308.5f, 1.35f },
        { 328f, 2.5f },
    };

    [Theory]
    [MemberData(nameof(ScreensAndScales))]
    public void ClockStartsAtTheContentInsetTitlesUse(float screenUnits, float scale)
    {
        var screenLeft = 40f * scale;
        var inset = SidePadding * scale;
        var earRight = screenLeft + (screenUnits * 0.5f - 59f) * scale;

        var left = StatusBarLayout.ClockLeft(screenLeft, inset, earRight, ClockWidth * scale);

        Assert.Equal(screenLeft + inset, left, 3);
    }

    [Fact]
    public void ClockStaysPutWhenIndicatorsShrinkTheIsland()
    {
        var withoutIndicators = StatusBarLayout.ClockLeft(0f, SidePadding, 95f, ClockWidth);
        var withTwoIndicators = StatusBarLayout.ClockLeft(0f, SidePadding, 95f + IndicatorSlot * 2f, ClockWidth);

        Assert.Equal(withoutIndicators, withTwoIndicators);
    }

    [Fact]
    public void ClockGivesWayToAnEarTooNarrowForTheInset()
    {
        var left = StatusBarLayout.ClockLeft(0f, SidePadding, 45f, ClockWidth);

        Assert.Equal(10f, left, 3);
    }

    [Theory]
    [MemberData(nameof(ScreensAndScales))]
    public void ClusterEndsAtTheSameInsetOnTheRight(float screenUnits, float scale)
    {
        var screenRight = (40f + screenUnits) * scale;
        var inset = SidePadding * scale;
        var minClusterLeft = screenRight - (screenUnits * 0.5f - 59f) * scale;

        var right = StatusBarLayout.ClusterRight(screenRight, inset, minClusterLeft, ClusterWidth * scale,
            MinRightPadding * scale);

        Assert.Equal(screenRight - inset, right, 3);
    }

    [Fact]
    public void ClusterSqueezesTowardTheEdgeWhenTheEarIsTooNarrow()
    {
        var right = StatusBarLayout.ClusterRight(300f, SidePadding, 240f, ClusterWidth, MinRightPadding);

        Assert.Equal(292f, right, 3);
    }
}
