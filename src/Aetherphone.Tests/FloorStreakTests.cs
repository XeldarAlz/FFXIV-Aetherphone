using Aetherphone.Apps.Casino.Strip;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class FloorStreakTests
{
    private const float PhoneContent = 328f;
    private const int Days = StripHero.StreakDays;

    [Fact]
    public void TheStreakMarksDoneDaysTodayAndTheDaysAhead()
    {
        var ready = StreakView.Of(Streak(3, ready: true), Days);
        Assert.Equal(new StreakView(2, 2, true, 3), ready);
        Assert.Equal(StreakDayState.Done, ready.StateOf(0));
        Assert.Equal(StreakDayState.Done, ready.StateOf(1));
        Assert.Equal(StreakDayState.Today, ready.StateOf(2));
        Assert.Equal(StreakDayState.Future, ready.StateOf(3));
        Assert.Equal(StreakDayState.Future, ready.StateOf(6));
    }

    [Fact]
    public void ADayAlreadyClaimedStaysTodayButCannotBeClaimedAgain()
    {
        var claimed = StreakView.Of(Streak(3, ready: false), Days);
        Assert.Equal(new StreakView(3, 2, false, 3), claimed);
        Assert.Equal(StreakDayState.Done, claimed.StateOf(1));
        Assert.Equal(StreakDayState.Today, claimed.StateOf(2));
        Assert.Equal(StreakDayState.Future, claimed.StateOf(3));
        Assert.False(StreakView.Of(Streak(3, ready: true, eligible: false), Days).Claimable);
    }

    [Fact]
    public void AFreshStreakStartsOnDayOneWithNothingLit()
    {
        var fresh = StreakView.Of(Streak(0, ready: false), Days);
        Assert.Equal(1, fresh.DayNumber);
        for (var day = 0; day < Days; day++)
        {
            Assert.Equal(StreakDayState.Future, fresh.StateOf(day));
        }

        Assert.Equal(new StreakView(6, 6, true, 7), StreakView.Of(Streak(7, ready: true), Days));
        Assert.Equal(new StreakView(6, 6, true, 7), StreakView.Of(Streak(12, ready: true), Days));
        Assert.Equal(new StreakView(7, 6, false, 7), StreakView.Of(Streak(12, ready: false), Days));
    }

    [Fact]
    public void TheStreakRowSharesTheHeroWidthBetweenItsLabelAndSevenDots()
    {
        var row = PhoneContent - StripHero.Pad * 2f;
        Assert.Equal(80f, StripHero.StreakColumnWidth(80f, 60f, row));
        Assert.Equal(row * StripHero.StreakColumnShare, StripHero.StreakColumnWidth(400f, 60f, row), 3);
        var region = row - row * StripHero.StreakColumnShare - StripHero.StreakColumnGap;
        var dot = StripHero.StreakDotSize(region, StripHero.StreakDot, StripHero.StreakMinGap, Days);
        Assert.True(dot >= 20f);
        Assert.True(dot * Days + StripHero.StreakMinGap * (Days - 1) <= region + 0.01f);
        Assert.Equal(StripHero.StreakDot, StripHero.StreakDotSize(400f, StripHero.StreakDot, 2f, Days));
        Assert.Equal(181f / 6f, StripHero.StreakPitch(204f, 23f, Days), 3);
    }

    [Fact]
    public void TheStreakRowIsTallEnoughForTheLabelsAndTheGrownTodayCircle()
    {
        Assert.Equal(11f + 34f, StripHero.StreakHeight(34f, 23f, 11f));
        Assert.Equal(11f + 40f * StripHero.TodayScale, StripHero.StreakHeight(20f, 40f, 11f), 3);
    }

    private static CasinoBonusDto Streak(int day, bool ready, bool eligible = true) =>
        new(Kind: CasinoBonusKinds.Streak, Ready: ready, Amount: 5_000, StreakDay: day, Eligible: eligible);
}
