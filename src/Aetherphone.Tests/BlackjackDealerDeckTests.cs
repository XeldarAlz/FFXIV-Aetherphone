using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BlackjackDealerDeckTests
{
    private const string Owner = "owner";
    private const string CoDealer = "co";
    private const string Player = "player";

    private static readonly CasinoBlackjackRoomStateDto HostDealt = new(Currency: CasinoCurrencies.Gil,
        DealerMode: CasinoDealerModes.Host, DealerUserId: Owner, CoDealers: new[] { CoDealer },
        Phase: BlackjackPhases.Betting);

    [Fact]
    public void TheOwnerOfAHostDealtTableGetsTheDealerDeckInsteadOfASeat()
    {
        var dealing = BlackjackHosting.DealsFromDeck(HostDealt, Owner, Owner, false);

        Assert.True(dealing);
        Assert.Equal(BlackjackDeck.Dealer, BlackjackDeckFlow.Pick(Facts(dealing: dealing, onBoard: false,
            holdsSeat: false)));
        Assert.True(BlackjackHosting.DealsFromDeck(HostDealt with { Currency = CasinoCurrencies.Practice },
            Owner, string.Empty, false));
    }

    [Fact]
    public void EveryoneElseKeepsTheSeatTheyWouldHaveHad()
    {
        Assert.False(BlackjackHosting.DealsFromDeck(HostDealt, CoDealer, Owner, false));
        Assert.False(BlackjackHosting.DealsFromDeck(HostDealt, Player, Owner, false));
        Assert.False(BlackjackHosting.DealsFromDeck(HostDealt, string.Empty, Owner, false));
        Assert.False(BlackjackHosting.DealsFromDeck(HostDealt with { DealerMode = CasinoDealerModes.House },
            Owner, Owner, false));
        Assert.False(BlackjackHosting.DealsFromDeck(HostDealt with { Currency = CasinoCurrencies.Chips },
            Owner, Owner, false));
        Assert.Equal(BlackjackDeck.Sit, BlackjackDeckFlow.Pick(Facts(onBoard: false, holdsSeat: false)));
    }

    [Fact]
    public void AnOwnerWhoIsAlreadySeatedPlaysTheirHandOut()
    {
        Assert.False(BlackjackHosting.DealsFromDeck(HostDealt, Owner, Owner, true));
        Assert.Equal(BlackjackDeck.Bet, BlackjackDeckFlow.Pick(Facts(canBet: true)));
    }

    [Fact]
    public void TheDealerDeckOffersDealOnlyOnceABetIsDown()
    {
        Assert.Equal(DealerDeck.WaitingBets, BlackjackHosting.DealerDeckOf(HostDealt));

        var betDown = HostDealt with
        {
            Seats = new[] { new CasinoBlackjackSeatDto(SeatIndex: 0, UserId: Player, Committed: 500) },
        };
        Assert.Equal(DealerDeck.Deal, BlackjackHosting.DealerDeckOf(betDown));
        Assert.Equal(DealerDeck.Paused, BlackjackHosting.DealerDeckOf(betDown with { Paused = true }));
        Assert.Equal(DealerDeck.HandLive,
            BlackjackHosting.DealerDeckOf(betDown with { Phase = BlackjackPhases.PlayerTurns }));
        Assert.Equal(DealerDeck.HandLive,
            BlackjackHosting.DealerDeckOf(betDown with { Phase = BlackjackPhases.DealerPlay }));
        Assert.Equal(DealerDeck.HandOver,
            BlackjackHosting.DealerDeckOf(betDown with { Phase = BlackjackPhases.Settlement }));
    }

    [Fact]
    public void ThePlayerDeckKeepsItsOrder()
    {
        Assert.Equal(BlackjackDeck.Sit, BlackjackDeckFlow.Pick(Facts(bought: false, canRebuy: true, canAct: true)));
        Assert.Equal(BlackjackDeck.Rebuy, BlackjackDeckFlow.Pick(Facts(canRebuy: true, broke: true,
            insurance: true)));
        Assert.Equal(BlackjackDeck.Broke, BlackjackDeckFlow.Pick(Facts(broke: true, insurance: true)));
        Assert.Equal(BlackjackDeck.Insurance, BlackjackDeckFlow.Pick(Facts(insurance: true, canBet: true)));
        Assert.Equal(BlackjackDeck.Act, BlackjackDeckFlow.Pick(Facts(canBet: true, committed: true, canAct: true)));
        Assert.Equal(BlackjackDeck.Wait, BlackjackDeckFlow.Pick(Facts(canBet: true, committed: true)));
        Assert.Equal(BlackjackDeck.Wait, BlackjackDeckFlow.Pick(Facts(onBoard: false, canAct: true)));
    }

    [Fact]
    public void AnOlderFlowThatStillTriesToSeatTheDealerIsToldWhy()
    {
        Assert.Contains(CasinoReasons.DealerCannotSit, CasinoReasons.All);
        Assert.True(CasinoReasons.TryMessage(CasinoReasons.DealerCannotSit, out var message));
        Assert.Equal("casino.tables.reasonDealerCannotSit", message.Key);
    }

    [Fact]
    public void TheOwnerOfAClockDealtGilTableHoldsTheBankInsteadOfASeat()
    {
        var clockDealt = HostDealt with { DealerMode = CasinoDealerModes.House };

        Assert.True(BlackjackHosting.BanksFromDeck(clockDealt, Owner, Owner, false));
        Assert.Equal(BlackjackDeck.Bank, BlackjackDeckFlow.Pick(Facts(banking: true, onBoard: false,
            holdsSeat: false)));
        Assert.Equal(BlackjackDeck.Dealer, BlackjackDeckFlow.Pick(Facts(dealing: true, banking: true,
            onBoard: false, holdsSeat: false)));
    }

    [Fact]
    public void OnlyTheGilTableOwnerWithoutASeatIsKeptOffTheFelt()
    {
        var clockDealt = HostDealt with { DealerMode = CasinoDealerModes.House };

        Assert.False(BlackjackHosting.BanksFromDeck(clockDealt, Owner, Owner, true));
        Assert.False(BlackjackHosting.BanksFromDeck(clockDealt, Player, Owner, false));
        Assert.False(BlackjackHosting.BanksFromDeck(clockDealt, CoDealer, Owner, false));
        Assert.False(BlackjackHosting.BanksFromDeck(clockDealt, string.Empty, string.Empty, false));
        Assert.False(BlackjackHosting.BanksFromDeck(clockDealt with { Currency = CasinoCurrencies.Practice },
            Owner, Owner, false));
        Assert.False(BlackjackHosting.BanksFromDeck(clockDealt with { Currency = CasinoCurrencies.Chips },
            Owner, Owner, false));
    }

    [Fact]
    public void AnOlderFlowThatStillTriesToSeatTheGilHostIsToldWhy()
    {
        Assert.Contains(CasinoReasons.HostCannotSit, CasinoReasons.All);
        Assert.True(CasinoReasons.TryMessage(CasinoReasons.HostCannotSit, out var message));
        Assert.Equal("casino.tables.reasonHostCannotSit", message.Key);
    }

    internal static BlackjackDeckFacts Facts(bool dealing = false, bool bought = true, bool onBoard = true,
        bool holdsSeat = true, bool canRebuy = false, bool broke = false, bool paused = false,
        bool insurance = false, bool canBet = false, bool committed = false, bool canAct = false,
        bool banking = false) =>
        new(dealing, banking, bought, onBoard, holdsSeat, canRebuy, broke, paused, insurance, canBet, committed,
            canAct);
}
