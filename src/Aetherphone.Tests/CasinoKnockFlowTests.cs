using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoKnockFlowTests
{
    [Fact]
    public void ARefusedAttachSaysWhereTheKnockerStands()
    {
        Assert.Equal(KnockStage.None, CasinoKnocks.FromDecline(CasinoReasons.InviteOnly));
        Assert.Equal(KnockStage.None, CasinoKnocks.FromDecline(CasinoReasons.NotMember));
        Assert.Equal(KnockStage.Waiting, CasinoKnocks.FromDecline(CasinoReasons.KnockPending));
        Assert.Equal(KnockStage.Denied, CasinoKnocks.FromDecline(CasinoReasons.Denied));
        Assert.Equal(KnockStage.Refused, CasinoKnocks.FromDecline(CasinoReasons.Kicked));
        Assert.Equal(KnockStage.Refused, CasinoKnocks.FromDecline(CasinoReasons.BannedFromTable));
        Assert.Equal(KnockStage.None, CasinoKnocks.FromDecline(CasinoReasons.Ended));
    }

    [Fact]
    public void ANotFoundRoomReadAsksTheCardWhyBeforeEndingTheRoom()
    {
        Assert.Equal(CasinoReasons.InviteOnly, CasinoRoomsStore.DoorReason(
            new CasinoTableRowDto(TableId: "t1", Admitted: false, Reason: CasinoReasons.InviteOnly), false));
        Assert.Equal(CasinoReasons.KnockPending, CasinoRoomsStore.DoorReason(
            new CasinoTableRowDto(TableId: "t1", Admitted: false, Reason: CasinoReasons.KnockPending), false));
        Assert.Equal(CasinoReasons.BannedFromTable, CasinoRoomsStore.DoorReason(
            new CasinoTableRowDto(TableId: "t1", Admitted: false, Reason: CasinoReasons.BannedFromTable), false));
        Assert.Equal(CasinoReasons.NoSpectators, CasinoRoomsStore.DoorReason(
            new CasinoTableRowDto(TableId: "t1", Admitted: false, Reason: CasinoReasons.NoSpectators), false));
        Assert.Equal(CasinoReasons.Ended, CasinoRoomsStore.DoorReason(
            new CasinoTableRowDto(TableId: "t1", Admitted: false), false));
        Assert.Equal(string.Empty, CasinoRoomsStore.DoorReason(
            new CasinoTableRowDto(TableId: "t1", Admitted: true), false));
        Assert.Equal(CasinoReasons.Ended, CasinoRoomsStore.DoorReason(null, true));
        Assert.Equal(string.Empty, CasinoRoomsStore.DoorReason(null, false));

        var asking = TableDoorCard.ContentFor(CasinoRoomsStore.DoorReason(
                new CasinoTableRowDto(TableId: "t1", Reason: CasinoReasons.InviteOnly), false),
            CasinoKnockStatus.Idle, L.Casino.BlackjackClosedTitle, L.Casino.BlackjackClosedHint);
        Assert.Equal(TableDoorAction.Knock, asking.Pressed);
    }

    [Fact]
    public void OnlyAnAnswerIsWorthANotificationAndOnlyWhileTheCardIsOffScreen()
    {
        Assert.True(CasinoKnockNotifier.AnswerWorthTelling(KnockStage.Admitted));
        Assert.True(CasinoKnockNotifier.AnswerWorthTelling(KnockStage.Denied));
        Assert.False(CasinoKnockNotifier.AnswerWorthTelling(KnockStage.Waiting));
        Assert.False(CasinoKnockNotifier.AnswerWorthTelling(KnockStage.Refused));
        Assert.False(CasinoKnockNotifier.AnswerWorthTelling(KnockStage.None));

        Assert.False(CasinoTablesStore.Attended(0, 10_000));
        Assert.True(CasinoTablesStore.Attended(9_000, 10_000));
        Assert.False(CasinoTablesStore.Attended(5_000, 10_000));
    }

    [Fact]
    public void AKnockerIsHeardOncePerKnock()
    {
        var first = new CasinoTableKnockDto("u1", "Mira", 1_750_000_000_000);
        var again = new CasinoTableKnockDto("u1", "Mira", 1_750_000_090_000);
        Assert.Equal(CasinoKnockNotifier.KnockKey(first), CasinoKnockNotifier.KnockKey(first with { DisplayName = "M" }));
        Assert.NotEqual(CasinoKnockNotifier.KnockKey(first), CasinoKnockNotifier.KnockKey(again));
    }

    [Fact]
    public void AKnockWaitsWalksStraightInOrIsTurnedAway()
    {
        Assert.Equal(KnockStage.Waiting,
            CasinoKnocks.FromKnock(KnockStage.None, true, CasinoReasons.KnockPending));
        Assert.Equal(KnockStage.Admitted, CasinoKnocks.FromKnock(KnockStage.None, true, string.Empty));
        Assert.Equal(KnockStage.Denied, CasinoKnocks.FromKnock(KnockStage.Denied, false, CasinoReasons.Denied));
        Assert.Equal(KnockStage.Waiting, CasinoKnocks.FromKnock(KnockStage.Waiting, false, CasinoReasons.Cooldown));
        Assert.Equal(KnockStage.Denied, CasinoKnocks.FromKnock(KnockStage.Denied, false, CasinoReasons.Cooldown));
        Assert.Equal(KnockStage.Refused,
            CasinoKnocks.FromKnock(KnockStage.None, false, CasinoReasons.BannedFromTable));
    }

    [Fact]
    public void TheCardAnswersAWaitingKnock()
    {
        Assert.Equal(KnockStage.Admitted, CasinoKnocks.FromCard(KnockStage.Waiting, true, string.Empty));
        Assert.Equal(KnockStage.Waiting,
            CasinoKnocks.FromCard(KnockStage.Waiting, false, CasinoReasons.KnockPending));
        Assert.Equal(KnockStage.Denied, CasinoKnocks.FromCard(KnockStage.Waiting, false, CasinoReasons.Denied));
        Assert.Equal(KnockStage.Refused,
            CasinoKnocks.FromCard(KnockStage.Waiting, false, CasinoReasons.BannedFromTable));
    }

    [Fact]
    public void AWaitingKnockThatVanishesWithoutASeatWasTurnedAway()
    {
        Assert.Equal(KnockStage.Denied, CasinoKnocks.FromCard(KnockStage.Waiting, false, CasinoReasons.InviteOnly));
        Assert.Equal(KnockStage.Denied, CasinoKnocks.FromCard(KnockStage.Waiting, false, CasinoReasons.NotMember));
        Assert.Equal(KnockStage.None, CasinoKnocks.FromCard(KnockStage.None, false, CasinoReasons.InviteOnly));
    }

    [Fact]
    public void OnlyDoorAnswersOpenTheTableToFollowTheKnock()
    {
        Assert.True(CasinoKnocks.AtTheDoor(CasinoReasons.InviteOnly));
        Assert.True(CasinoKnocks.AtTheDoor(CasinoReasons.KnockPending));
        Assert.True(CasinoKnocks.AtTheDoor(CasinoReasons.Denied));
        Assert.False(CasinoKnocks.AtTheDoor(CasinoReasons.BannedFromTable));
        Assert.False(CasinoKnocks.AtTheDoor(CasinoReasons.CodeUnknown));
    }

    [Fact]
    public void TheDoorCardFollowsTheKnockFromAskingToAnAnswer()
    {
        var closedTitle = L.Casino.BlackjackClosedTitle;
        var closedHint = L.Casino.BlackjackClosedHint;

        var asking = TableDoorCard.ContentFor(CasinoReasons.InviteOnly, CasinoKnockStatus.Idle, closedTitle,
            closedHint);
        Assert.Equal(L.Casino.BlackjackDoorTitle.Key, asking.Title.Key);
        Assert.Equal(TableDoorAction.Knock, asking.Pressed);

        var waiting = TableDoorCard.ContentFor(CasinoReasons.InviteOnly,
            new CasinoKnockStatus("t1", KnockStage.Waiting, CasinoReasons.KnockPending), closedTitle, closedHint);
        Assert.Equal(L.Tables.KnockWaitingTitle.Key, waiting.Title.Key);
        Assert.Equal(L.Casino.ReasonKnockPending.Key, waiting.Hint.Key);
        Assert.Equal(TableDoorAction.Leave, waiting.Pressed);

        var denied = TableDoorCard.ContentFor(CasinoReasons.KnockPending,
            new CasinoKnockStatus("t1", KnockStage.Denied, CasinoReasons.Denied), closedTitle, closedHint);
        Assert.Equal(L.Tables.KnockDeniedTitle.Key, denied.Title.Key);
        Assert.Equal(L.Casino.ReasonDenied.Key, denied.Hint.Key);
        Assert.Equal(L.Tables.KnockAgain.Key, denied.Action.Key);
        Assert.Equal(TableDoorAction.Knock, denied.Pressed);

        var removed = TableDoorCard.ContentFor(CasinoReasons.Kicked, CasinoKnockStatus.Idle, closedTitle,
            closedHint);
        Assert.Equal(L.Tables.RemovedTitle.Key, removed.Title.Key);
        Assert.Equal(L.Casino.ReasonKicked.Key, removed.Hint.Key);
        Assert.Equal(TableDoorAction.Leave, removed.Pressed);

        var ended = TableDoorCard.ContentFor(CasinoReasons.Ended, CasinoKnockStatus.Idle, closedTitle, closedHint);
        Assert.Equal(closedTitle.Key, ended.Title.Key);
        Assert.Equal(TableDoorAction.Leave, ended.Pressed);
    }
}
