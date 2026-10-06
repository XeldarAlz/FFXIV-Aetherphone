using Aetherphone.Apps.Games.Framework;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HudModelTests
{
    private static ComboMeter Combo(int hits)
    {
        var meter = ComboMeter.Create();
        for (var hit = 0; hit < hits; hit++)
        {
            meter.Hit();
        }

        return meter;
    }

    [Fact]
    public void SlotsAppearInTheFixedOrderRegardlessOfFillOrder()
    {
        var hud = new HudModel();
        hud.Best(100);
        hud.Combo(Combo(3));
        hud.Lives(2, 3);
        hud.Timer(10f, 30f, false);

        var visible = hud.Visible(HudStyle.Standard).ToArray();

        Assert.Equal(new[] { HudSlot.Timer, HudSlot.Lives, HudSlot.Combo, HudSlot.Best }, visible);
    }

    [Fact]
    public void BestIsDroppedFirstWhenFiveSlotsCompete()
    {
        var hud = new HudModel();
        hud.Timer(10f, 30f, false);
        hud.Lives(2, 3);
        hud.Level(4);
        hud.Combo(Combo(2));
        hud.Best(100);

        var visible = hud.Visible(HudStyle.Standard).ToArray();

        Assert.Equal(new[] { HudSlot.Timer, HudSlot.Lives, HudSlot.Level, HudSlot.Combo }, visible);
    }

    [Fact]
    public void LevelIsDroppedAfterBestWhenSixSlotsCompete()
    {
        var hud = new HudModel();
        hud.Timer(10f, 30f, false);
        hud.Lives(2, 3);
        hud.Level(4);
        hud.Combo(Combo(2));
        hud.Best(100);
        hud.Custom(80f);

        var visible = hud.Visible(HudStyle.Standard).ToArray();

        Assert.Equal(new[] { HudSlot.Timer, HudSlot.Lives, HudSlot.Combo, HudSlot.Custom }, visible);
    }

    [Fact]
    public void CompactShowsOnlyTheFirstSecondary()
    {
        var hud = new HudModel();
        hud.Lives(2, 3);
        hud.Level(4);
        hud.Best(100);

        var visible = hud.Visible(HudStyle.Compact).ToArray();

        Assert.Equal(new[] { HudSlot.Lives }, visible);
    }

    [Fact]
    public void AComboBelowTwoHitsAndAZeroBestStayHidden()
    {
        var hud = new HudModel();
        hud.Combo(Combo(1));
        hud.Best(0);

        Assert.Empty(hud.Visible(HudStyle.Standard).ToArray());
        hud.Combo(Combo(2));
        Assert.Equal(new[] { HudSlot.Combo }, hud.Visible(HudStyle.Standard).ToArray());
    }

    [Fact]
    public void ClearForgetsEverySlotButKeepsTheCustomRect()
    {
        var hud = new HudModel();
        hud.Score(12);
        hud.Timer(1f, 2f, true);
        hud.Custom(50f);
        hud.PlaceSlot(HudSlot.Custom, new Core.Rect(new System.Numerics.Vector2(1f, 2f), new System.Numerics.Vector2(3f, 4f)));
        hud.Clear();

        Assert.False(hud.HasScore);
        Assert.False(hud.HasTimer);
        Assert.False(hud.HasCustom);
        Assert.Empty(hud.Visible(HudStyle.Standard).ToArray());
        Assert.Equal(2f, hud.CustomRect(0).Width);
    }

    [Fact]
    public void TimerAndLivesClampTheirInputs()
    {
        var hud = new HudModel();
        hud.Timer(-3f, 10f, false);
        hud.Lives(-1, 0);

        Assert.Equal(0f, hud.TimerLeft);
        Assert.Equal(0, hud.LivesLeft);
        Assert.Equal(1, hud.LivesMax);
    }

    [Fact]
    public void TwoCustomSlotsKeepTheirOrderAndWidths()
    {
        var hud = new HudModel();
        hud.Custom(40f);
        hud.Custom(60f);
        hud.Custom(80f);

        Assert.Equal(2, hud.CustomCount);
        Assert.Equal(40f, hud.CustomWidth(0));
        Assert.Equal(60f, hud.CustomWidth(1));
        Assert.Equal(new[] { HudSlot.Custom, HudSlot.SecondCustom }, hud.Visible(HudStyle.Standard).ToArray());
    }

    [Fact]
    public void CustomPlacedIsTrueOnlyForTheFrameAfterTheKitLaidItOut()
    {
        var hud = new HudModel();
        hud.Clear();
        hud.Custom(40f);
        Assert.False(hud.CustomPlaced(0));

        hud.PlaceSlot(HudSlot.Custom, new Core.Rect(System.Numerics.Vector2.Zero, new System.Numerics.Vector2(40f, 28f)));
        hud.Clear();
        hud.Custom(40f);
        Assert.True(hud.CustomPlaced(0));
        Assert.False(hud.CustomPlaced(1));

        hud.Clear();
        hud.Clear();
        Assert.False(hud.CustomPlaced(0));
    }

    [Fact]
    public void SlotRectsRememberWhereEachCapsuleWasDrawn()
    {
        var hud = new HudModel();
        var rect = new Core.Rect(new System.Numerics.Vector2(10f, 20f), new System.Numerics.Vector2(70f, 48f));
        hud.PlaceSlot(HudSlot.Timer, rect);

        Assert.Equal(rect, hud.SlotRect(HudSlot.Timer));
        Assert.Equal(0f, hud.SlotRect(HudSlot.Lives).Width);
    }

    [Fact]
    public void ClockIsAnElapsedTimerWithoutATrack()
    {
        var hud = new HudModel();
        hud.Clock(65.9f);

        Assert.True(hud.HasTimer);
        Assert.True(hud.TimerElapsed);
        Assert.Equal(0f, hud.TimerTotal);
        Assert.False(hud.TimerUrgent);
        Assert.Equal(new[] { HudSlot.Timer }, hud.Visible(HudStyle.Standard).ToArray());
    }

    [Fact]
    public void ScoreCanCarryItsOwnLabel()
    {
        var hud = new HudModel();
        var label = new Core.Localization.LocString("t.level", "Level");
        hud.Score(4, label);
        Assert.True(hud.HasScore);
        Assert.Equal("t.level", hud.ScoreLabel!.Value.Key);

        hud.Score(5);
        Assert.Null(hud.ScoreLabel);
    }
}
