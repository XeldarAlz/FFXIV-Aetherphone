using Aetherphone.Apps.Casino.Cabinets;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class WheelStatusLineTests
{
    private const string WidestCount = "120";

    private static readonly LocString[] Labels =
    {
        L.Casino.WheelPlaceBets,
        L.Casino.WheelBetsClosed,
        L.Casino.WheelSpinning,
        L.Casino.WheelNextSpin,
        L.Casino.WheelReconnecting,
    };

    [Fact]
    public void AnOpenWindowSaysPlaceYourBetsWithOneClock()
    {
        var status = WheelStatus.Of(false, CasinoRoomPhases.Open, WheelStage.Betting, 12_400);
        Assert.Equal(WheelStatusKind.Open, status.Kind);
        Assert.Equal(L.Casino.WheelPlaceBets.Key, status.Label.Key);
        Assert.True(status.HasClock);
        Assert.Equal(13, status.Seconds);
        Assert.False(status.Urgent);
    }

    [Fact]
    public void TheLastSecondsOfTheWindowTurnUrgent()
    {
        var status = WheelStatus.Of(false, CasinoRoomPhases.Open, WheelStage.Betting, 4_000);
        Assert.True(status.Urgent);
        Assert.Equal(4, status.Seconds);
    }

    [Fact]
    public void ALockedWheelHasNoClockBecauseTheSpinIsTheClock()
    {
        var closed = WheelStatus.Of(false, CasinoRoomPhases.Locked, WheelStage.Locking, 4_000);
        Assert.Equal(WheelStatusKind.Closed, closed.Kind);
        Assert.False(closed.HasClock);

        var spinning = WheelStatus.Of(false, CasinoRoomPhases.Locked, WheelStage.Spinning, 2_000);
        Assert.Equal(WheelStatusKind.Spinning, spinning.Kind);
        Assert.False(spinning.HasClock);
    }

    [Fact]
    public void ALandedWheelCountsDownToTheNextSpin()
    {
        var beforeResult = WheelStatus.Of(false, CasinoRoomPhases.Locked, WheelStage.Settling, 800);
        Assert.Equal(WheelStatusKind.Next, beforeResult.Kind);
        Assert.Equal(1 + CasinoRoomCadence.WheelResultSeconds, beforeResult.Seconds);

        var inResult = WheelStatus.Of(false, CasinoRoomPhases.Result, WheelStage.Settling, 7_200);
        Assert.Equal(8, inResult.Seconds);
        Assert.False(inResult.Urgent);
    }

    [Fact]
    public void AnUnreachableRoomSaysReconnectingWithoutAClock()
    {
        var status = WheelStatus.Of(true, CasinoRoomPhases.Open, WheelStage.Betting, 12_000);
        Assert.Equal(WheelStatusKind.Reconnecting, status.Kind);
        Assert.False(status.HasClock);
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void TheStatusLineIsOneCompactRowUnderTheChromeBand(float scale)
    {
        var stage = WheelTextMeasure.Stage(scale, WheelCabinet.DeckHeight);
        var row = WheelStatusLayout.RowOf(stage.Ribbon, scale);
        Assert.True(row.Min.Y >= stage.Band.Max.Y - 0.01f);
        Assert.True(row.Height <= 40f * scale + 0.01f);
        Assert.True(WheelTextMeasure.Inside(row, stage.Ribbon));
        Assert.True(row.Min.X >= stage.Safe.Min.X - 0.01f && row.Max.X <= stage.Safe.Max.X + 0.01f);
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void EveryPhaseLabelFitsWholeInEveryLanguageBesideTheClockAndTheCrowd(float scale)
    {
        var stage = WheelTextMeasure.Stage(scale, WheelCabinet.DeckHeight);
        var row = WheelStatusLayout.RowOf(stage.Ribbon, scale);
        var labelWidth = 0f;
        for (var index = 0; index < Labels.Length; index++)
        {
            labelWidth = MathF.Max(labelWidth, WheelTextMeasure.Widest(Labels[index], WheelStatusLine.LabelStyle, scale));
        }

        var clockWidth = WheelTextMeasure.Width(WheelStatusLine.ClockReference, WheelStatusLine.ClockStyle, scale);
        var countWidth = WheelTextMeasure.Width(WidestCount, WheelStatusLine.CountStyle, scale);
        var layout = WheelStatusLayout.Compute(row, labelWidth, clockWidth, countWidth, scale);

        Assert.True(layout.Label.Width >= labelWidth - 0.01f);
        Assert.True(layout.HasClock);
        Assert.True(layout.HasCrowd);
        Assert.True(WheelTextMeasure.Inside(layout.Phase, row));
        Assert.True(WheelTextMeasure.Inside(layout.Crowd, row));
        Assert.True(WheelTextMeasure.Inside(layout.Label, layout.Phase));
        Assert.True(WheelTextMeasure.Inside(layout.Clock, layout.Phase));
        Assert.True(WheelTextMeasure.Inside(layout.Count, layout.Crowd));
        Assert.True(layout.Clock.Min.X >= layout.Label.Max.X);
        Assert.True(layout.Dot.X < layout.Label.Min.X);
        Assert.True(WheelTextMeasure.Disjoint(layout.Phase, layout.Crowd));
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void TheStatusNeverTouchesTheChipsCapsuleAtItsWidest(float scale)
    {
        var stage = WheelTextMeasure.Stage(scale, WheelCabinet.DeckHeight);
        var row = WheelStatusLayout.RowOf(stage.Ribbon, scale);
        var layout = WheelStatusLayout.Compute(row, row.Width, 40f * scale, 30f * scale, scale);
        var capsule = WheelTextMeasure.Capsule(stage, scale);
        Assert.True(WheelTextMeasure.Disjoint(layout.Phase, capsule));
        Assert.True(WheelTextMeasure.Disjoint(layout.Crowd, capsule));
        Assert.False(stage.ChromeContains(layout.Phase.Center));
        Assert.False(stage.ChromeContains(layout.Crowd.Center));
    }

    [Fact]
    public void WithoutAClockOrACrowdThePillHugsItsLabel()
    {
        var stage = WheelTextMeasure.Stage(1f, WheelCabinet.DeckHeight);
        var row = WheelStatusLayout.RowOf(stage.Ribbon, 1f);
        var layout = WheelStatusLayout.Compute(row, 90f, 0f, 0f, 1f);
        Assert.False(layout.HasClock);
        Assert.False(layout.HasCrowd);
        var expected = WheelStatusLayout.PadX * 2f + WheelStatusLayout.DotRadius * 2f + WheelStatusLayout.DotGap + 90f;
        Assert.Equal(expected, layout.Phase.Width, 3);
        Assert.Equal(row.Min.X, layout.Phase.Min.X);
    }

    [Fact]
    public void ALabelTooWideForTheRowIsClampedInsideThePill()
    {
        var stage = WheelTextMeasure.Stage(1f, WheelCabinet.DeckHeight);
        var row = WheelStatusLayout.RowOf(stage.Ribbon, 1f);
        var layout = WheelStatusLayout.Compute(row, row.Width * 2f, 40f, 30f, 1f);
        Assert.True(layout.Label.Width < row.Width);
        Assert.True(WheelTextMeasure.Disjoint(layout.Phase, layout.Crowd));
        Assert.True(layout.Phase.Max.X <= layout.Crowd.Min.X - WheelStatusLayout.PillGap + 0.01f);
    }
}
