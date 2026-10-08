using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoHostedBlackjackTests
{
    [Fact]
    public void TheHostAndCoDealersHoldTheDealerControlsOnlyAtSeatBankedTables()
    {
        var practice = new CasinoBlackjackRoomStateDto(Practice: true, Currency: CasinoCurrencies.Practice,
            DealerMode: CasinoDealerModes.Host, DealerUserId: "host", CoDealers: new[] { "co" });

        Assert.True(BlackjackHosting.DealerPowers(practice, "host", "host"));
        Assert.True(BlackjackHosting.DealerPowers(practice, "co", "host"));
        Assert.False(BlackjackHosting.DealerPowers(practice, "player", "host"));
        Assert.False(BlackjackHosting.DealerPowers(practice, string.Empty, "host"));

        var houseMode = practice with { DealerMode = CasinoDealerModes.House, DealerUserId = string.Empty };
        Assert.True(BlackjackHosting.DealerPowers(houseMode, "host", "host"));

        var chips = new CasinoBlackjackRoomStateDto(CoDealers: new[] { "co" });
        Assert.False(BlackjackHosting.DealerPowers(chips, "co", "host"));
    }

    [Fact]
    public void AHostDealtTableWaitsForTheDealTap()
    {
        var waiting = new CasinoBlackjackRoomStateDto(Currency: CasinoCurrencies.Gil, DealerMode: CasinoDealerModes.Host,
            Phase: BlackjackPhases.Betting, DeadlineUnixMs: 0);

        Assert.True(BlackjackHosting.HostDeals(waiting));
        Assert.True(BlackjackHosting.AwaitsDeal(waiting));
        Assert.False(BlackjackHosting.AwaitsDeal(waiting with { DeadlineUnixMs = 1 }));
        Assert.False(BlackjackHosting.AwaitsDeal(waiting with { Paused = true }));
        Assert.False(BlackjackHosting.AwaitsDeal(waiting with { Phase = BlackjackPhases.PlayerTurns }));
        Assert.False(BlackjackHosting.AwaitsDeal(waiting with { DealerMode = CasinoDealerModes.House }));
    }

    [Fact]
    public void PracticeRebuysOpenOnlyWhenTheStackCannotCoverTheMinimum()
    {
        var board = new CasinoBlackjackRoomStateDto(Practice: true, Currency: CasinoCurrencies.Practice,
            PracticeRebuy: true, MinBet: 500, Phase: BlackjackPhases.Betting, PracticeStack: 100_000);

        Assert.True(BlackjackHosting.CanRebuy(board, 400));
        Assert.False(BlackjackHosting.CanRebuy(board, 500));
        Assert.False(BlackjackHosting.CanRebuy(board with { PracticeRebuy = false }, 0));
        Assert.False(BlackjackHosting.CanRebuy(board with { Phase = BlackjackPhases.PlayerTurns }, 0));
        Assert.False(BlackjackHosting.CanRebuy(board with { Currency = CasinoCurrencies.Gil, Practice = false }, 0));
    }

    [Fact]
    public void NaturalsPayTheHouseRulesTheSnapshotCarries()
    {
        var standard = new CasinoBlackjackRoomStateDto();
        var evenMoney = new CasinoBlackjackRoomStateDto(Rules: new CasinoBlackjackRuleSheetDto(
            BlackjackPays: CasinoRuleSheet.PaysEven));

        Assert.Equal(1_500, BlackjackHosting.NaturalPayout(standard, 1_000));
        Assert.Equal(1_000, BlackjackHosting.NaturalPayout(evenMoney, 1_000));
    }

    [Fact]
    public void ASmallHostedTableOffersOnlyItsOwnSeats()
    {
        var fourSeats = new CasinoBlackjackRoomStateDto(Seats: new CasinoBlackjackSeatDto[4]);

        Assert.Equal(4, BlackjackTable.SeatLimit(fourSeats));
        Assert.Equal(BlackjackRules.SeatCount, BlackjackTable.SeatLimit(new CasinoBlackjackRoomStateDto()));
    }
}
