using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HostedTablePauseTests
{
    [Fact]
    public void APausedTableHoldsEverySeatedDeckOnThePauseCaption()
    {
        Assert.Equal(BlackjackDeck.Paused, Pick(paused: true, canAct: true));
        Assert.Equal(BlackjackDeck.Paused, Pick(paused: true, insurance: true));
        Assert.Equal(BlackjackDeck.Paused, Pick(paused: true, canBet: true));
        Assert.Equal(BlackjackDeck.Act, Pick(canBet: true, committed: true, canAct: true));
    }

    [Fact]
    public void APauseStillLetsABrokeSeatRebuyAndANewcomerSit()
    {
        Assert.Equal(BlackjackDeck.Rebuy, Pick(paused: true, canRebuy: true));
        Assert.Equal(BlackjackDeck.Broke, Pick(paused: true, broke: true));
        Assert.Equal(BlackjackDeck.Sit, Pick(onBoard: false, holdsSeat: false, paused: true));
        Assert.Equal(BlackjackDeck.Dealer, Pick(dealing: true, onBoard: false, holdsSeat: false, paused: true));
    }

    [Fact]
    public void TheDoorHoldsDealWhileTheTableIsPaused()
    {
        Assert.False(TableDoor.ControlEnabled(DoorControl.Deal, false, true));
        Assert.True(TableDoor.ControlEnabled(DoorControl.Deal, false, false));
        Assert.True(TableDoor.ControlEnabled(DoorControl.Pause, false, true));
        Assert.False(TableDoor.ControlEnabled(DoorControl.Pause, true, false));
        Assert.True(TableDoor.ControlEnabled(DoorControl.Rename, true, true));
    }

    [Fact]
    public void AHoldemTablePausedBetweenHandsSaysSo()
    {
        var waiting = new CasinoHoldemRoomStateDto(Phase: HoldemPhases.Waiting, Paused: true);

        Assert.True(HoldemTable.PausedBetweenHands(waiting));
        Assert.True(HoldemTable.PausedBetweenHands(waiting with { Phase = HoldemPhases.Intermission }));
        Assert.False(HoldemTable.PausedBetweenHands(waiting with { Phase = HoldemPhases.Flop }));
        Assert.False(HoldemTable.PausedBetweenHands(waiting with { Paused = false }));
    }

    [Fact]
    public void APausedTableRefusalReadsAsTheHostsPauseNotTheWheel()
    {
        Assert.Contains(CasinoReasons.TablePaused, CasinoReasons.All);
        Assert.True(CasinoReasons.TryMessage(CasinoReasons.TablePaused, out var message));
        Assert.Equal("casino.tables.reasonTablePaused", message.Key);
    }

    private static BlackjackDeck Pick(bool dealing = false, bool onBoard = true, bool holdsSeat = true,
        bool canRebuy = false, bool broke = false, bool paused = false, bool insurance = false,
        bool canBet = false, bool committed = false, bool canAct = false) =>
        BlackjackDeckFlow.Pick(BlackjackDealerDeckTests.Facts(dealing, true, onBoard, holdsSeat, canRebuy, broke,
            paused, insurance, canBet, committed, canAct));
}
