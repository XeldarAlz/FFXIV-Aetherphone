using Aetherphone.Core.GameChat;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PopoutPresenceTests
{
    private static readonly PresenceSettings Defaults = new(true, false, true);

    [Fact]
    public void CombatSuppressesWhenTheCombatGateIsOn()
    {
        var state = new PresenceState(true, false, false);

        Assert.True(PopoutPresenceGate.ShouldSuppress(state, Defaults));
    }

    [Fact]
    public void CombatLeavesPopoutsAloneWhenTheCombatGateIsOff()
    {
        var state = new PresenceState(true, false, false);

        Assert.False(PopoutPresenceGate.ShouldSuppress(state, new PresenceSettings(false, false, true)));
    }

    [Fact]
    public void DutyLeavesPopoutsAloneUntilTheDutyGateIsOn()
    {
        var state = new PresenceState(false, true, false);

        Assert.False(PopoutPresenceGate.ShouldSuppress(state, Defaults));
        Assert.True(PopoutPresenceGate.ShouldSuppress(state, new PresenceSettings(true, true, true)));
    }

    [Fact]
    public void ExemptFieldOperationsDoNotCountAsADuty()
    {
        var state = new PresenceState(false, true, true);

        Assert.False(PopoutPresenceGate.ShouldSuppress(state, new PresenceSettings(true, true, true)));
        Assert.True(PopoutPresenceGate.ShouldSuppress(state, new PresenceSettings(true, true, false)));
    }

    [Fact]
    public void CutscenesSuppressOnlyWhenTheCutsceneGateIsOn()
    {
        var state = new PresenceState(false, false, false, InCutscene: true);

        Assert.False(PopoutPresenceGate.ShouldSuppress(state, Defaults));
        Assert.True(PopoutPresenceGate.ShouldSuppress(state, new PresenceSettings(true, false, true, HideInCutscene: true)));
    }

    [Fact]
    public void AHiddenHudSuppressesOnlyWhenTheHudGateIsOn()
    {
        var state = new PresenceState(false, false, false, UiHidden: true);

        Assert.False(PopoutPresenceGate.ShouldSuppress(state, Defaults));
        Assert.True(PopoutPresenceGate.ShouldSuppress(state,
            new PresenceSettings(true, false, true, HideWhenUiHidden: true)));
    }

    [Fact]
    public void CombatInAnExemptFieldOperationStillSuppresses()
    {
        var state = new PresenceState(true, true, true);

        Assert.True(PopoutPresenceGate.ShouldSuppress(state, new PresenceSettings(true, true, true)));
    }

    [Fact]
    public void ASingleFrameFlickerNeverReachesTheWindows()
    {
        var debounce = new PresenceDebounce();

        Assert.False(debounce.Step(true, 0.016f, 0.35f));
        Assert.False(debounce.Step(false, 0.016f, 1f));
        Assert.False(debounce.Value);
    }

    [Fact]
    public void AConditionThatHoldsPastTheDelayFlipsOnce()
    {
        var debounce = new PresenceDebounce();

        Assert.False(debounce.Step(true, 0.2f, 0.35f));
        Assert.True(debounce.Step(true, 0.2f, 0.35f));
        Assert.True(debounce.Value);
        Assert.False(debounce.Step(true, 0.2f, 0.35f));
    }

    [Fact]
    public void AZeroDelayAppliesOnTheSameStep()
    {
        var debounce = new PresenceDebounce();

        Assert.True(debounce.Step(true, 0.016f, 0f));
        Assert.True(debounce.Value);
    }
}
