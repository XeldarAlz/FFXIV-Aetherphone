using Aetherphone.Core;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Telephony.Contracts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoRoomSessionTests
{
    private const string Room = "wheel-floor";
    private const long Now = 1_754_784_000_000;
    private const string WheelBlob = "{\"roundIndex\":7,\"segment\":-1,\"staked\":120}";

    [Fact]
    public void EnteringARoomAsksTheOneSocketToAttach()
    {
        var session = Session(out var sent);

        session.Enter(Room);

        Assert.Single(sent);
        Assert.Equal(SignalType.CasinoAttach, sent[0].Type);
        Assert.NotNull(sent[0].Casino);
        Assert.Equal(Room, sent[0].Casino!.RoomId);
        Assert.Equal(Room, session.RoomId);
        Assert.True(session.AwaitingSnapshot);
        Assert.False(session.Attached);
    }

    [Fact]
    public void ReEnteringTheRoomAlreadyInPlayDoesNotAttachTwice()
    {
        var session = Session(out var sent);

        session.Enter(Room);
        session.Enter(Room);

        Assert.Single(sent);
    }

    [Fact]
    public void LeavingDetachesAndDropsTheHeldRoom()
    {
        var session = Session(out var sent);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5), Now);

        session.Leave();

        Assert.Equal(2, sent.Count);
        Assert.Equal(SignalType.CasinoDetach, sent[1].Type);
        Assert.Equal(Room, sent[1].Casino!.RoomId);
        Assert.Equal(string.Empty, session.RoomId);
        Assert.Null(session.State);
    }

    [Fact]
    public void AReactionIsSentOnlyWhileAttachedAndOnlyFromTheSet()
    {
        var session = Session(out var sent);
        Assert.False(session.React(1));

        session.Enter(Room);
        Assert.False(session.React(1));
        session.Receive(Attached(epoch: 1, seq: 5), Now);

        Assert.False(session.React(-1));
        Assert.False(session.React(CasinoRoomSession.ReactionKinds));
        Assert.True(session.React(3));
        var reacted = sent[^1];
        Assert.Equal(SignalType.CasinoReact, reacted.Type);
        Assert.Equal(Room, reacted.Casino!.RoomId);
        Assert.Equal(3, reacted.Reaction);
    }

    [Fact]
    public void AReceivedReactionShowsForItsSenderUntilItAgesAndTheRoomForgetsItOnLeave()
    {
        var session = Session(out _);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5), Now);

        session.Receive(new CasinoSignal(SignalType.CasinoReaction, null, new CasinoPayload { RoomId = Room },
            "mira", 2), Now);
        session.Receive(new CasinoSignal(SignalType.CasinoReaction, null, new CasinoPayload { RoomId = "elsewhere" },
            "ciri", 1), Now);
        session.AbsorbReaction("rhan", CasinoRoomSession.ReactionKinds, 0);

        var nowTick = Environment.TickCount64;
        Assert.True(session.TryReactionOf("mira", nowTick, 2_600, out var reaction, out _));
        Assert.Equal(2, reaction);
        Assert.False(session.TryReactionOf("ciri", nowTick, 2_600, out _, out _));
        Assert.False(session.TryReactionOf("rhan", nowTick, 2_600, out _, out _));
        Assert.False(session.TryReactionOf("mira", nowTick + 10_000, 2_600, out _, out _));

        session.AbsorbReaction("mira", 4, nowTick);
        Assert.True(session.TryReactionOf("mira", nowTick, 2_600, out reaction, out _));
        Assert.Equal(4, reaction);

        session.Leave();
        Assert.False(session.TryReactionOf("mira", nowTick, 2_600, out _, out _));
    }

    [Fact]
    public void AttachingBaselinesTheEpochAndSeq()
    {
        var session = Session(out _);
        session.Enter(Room);

        session.Receive(Attached(epoch: 3, seq: 41), Now);

        var state = session.State;
        Assert.NotNull(state);
        Assert.Equal(3, state.Epoch);
        Assert.Equal(41, state.Seq);
        Assert.True(session.Attached);
        Assert.False(session.AwaitingSnapshot);
    }

    [Fact]
    public void TheGameBlobIsParsedOnceIntoTheKindTheRoomNamed()
    {
        var session = Session(out _);
        session.Enter(Room);

        session.Receive(Attached(epoch: 1, seq: 5), Now);

        var state = session.State;
        Assert.NotNull(state);
        Assert.NotNull(state.Wheel);
        Assert.Null(state.Bingo);
        Assert.Equal(7, state.Wheel.RoundIndex);
        Assert.Equal(-1, state.Wheel.Segment);
        Assert.Equal(120, state.Wheel.Staked);
    }

    [Fact]
    public void AnUnknownKindOrAnUnreadableBlobKeepsTheEnvelopeAndNothingElse()
    {
        var mystery = CasinoRoomSession.Build(Room, 1, 5, new CasinoRoomSnapshotDto(
            RoomId: Room,
            GameKind: "casino.mystery",
            GameState: WheelBlob,
            Occupancy: 4));
        Assert.Null(mystery.Wheel);
        Assert.Null(mystery.Bingo);
        Assert.Equal(4, mystery.Snapshot.Occupancy);

        var broken = CasinoRoomSession.Build(Room, 1, 5, new CasinoRoomSnapshotDto(
            RoomId: Room,
            GameKind: CasinoWire.WheelKind,
            GameState: "{not json",
            Occupancy: 4));
        Assert.Null(broken.Wheel);
        Assert.Equal(4, broken.Snapshot.Occupancy);

        var empty = CasinoRoomSession.Build(Room, 1, 5, new CasinoRoomSnapshotDto(
            RoomId: Room,
            GameKind: CasinoWire.BingoKind));
        Assert.Null(empty.Bingo);
    }

    [Fact]
    public void FramesForAnotherRoomNeverTouchTheRoomInPlay()
    {
        var session = Session(out _);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5), Now);

        session.Receive(new CasinoSignal(SignalType.CasinoEvent, null, new CasinoPayload
        {
            RoomId = "bingo-hall",
            Epoch = 1,
            Seq = 6,
            Event = new CasinoRoomEventDto(Occupancy: 99),
        }), Now);

        Assert.Equal(4, session.State!.Snapshot.Occupancy);
    }

    [Fact]
    public void TheNextSeqApplies()
    {
        var session = Session(out var sent);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5), Now);

        session.Receive(Event(epoch: 1, seq: 6, new CasinoRoomEventDto(
            State: 1,
            Phase: CasinoRoomPhases.Locked,
            PhaseEndsAtUnixMs: Now + 8_000,
            RoundIndex: 8,
            GameState: "{\"roundIndex\":8,\"segment\":31}",
            Occupancy: 7)), Now);

        var state = session.State;
        Assert.Equal(6, state!.Seq);
        Assert.Equal(7, state.Snapshot.Occupancy);
        Assert.Equal(8, state.Snapshot.RoundIndex);
        Assert.Equal(CasinoRoomPhases.Locked, state.Snapshot.Phase);
        Assert.Equal(Now + 8_000, state.Snapshot.PhaseEndsAtUnixMs);
        Assert.Equal(31, state.Wheel!.Segment);
        Assert.Single(sent);
    }

    [Fact]
    public void AnEventReplacesTheRoomWithoutLosingItsIdentity()
    {
        var session = Session(out _);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5), Now);

        session.Receive(Event(epoch: 1, seq: 6, new CasinoRoomEventDto(Occupancy: 9)), Now);

        var state = session.State;
        Assert.Equal(Room, state!.RoomId);
        Assert.Equal(Room, state.Snapshot.RoomId);
        Assert.Equal(CasinoWire.WheelKind, state.Snapshot.GameKind);
        Assert.Equal(9, state.Snapshot.Occupancy);
        Assert.Equal(0, state.Snapshot.RoundIndex);
        Assert.Null(state.Wheel);
    }

    [Fact]
    public void ADuplicateOrOlderSeqIsDropped()
    {
        var session = Session(out var sent);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5), Now);

        session.Receive(Event(epoch: 1, seq: 5, new CasinoRoomEventDto(Occupancy: 77)), Now);
        session.Receive(Event(epoch: 1, seq: 2, new CasinoRoomEventDto(Occupancy: 88)), Now);

        Assert.Equal(4, session.State!.Snapshot.Occupancy);
        Assert.Equal(5, session.State!.Seq);
        Assert.Single(sent);
    }

    [Fact]
    public void AFrameFromAnOlderEpochIsIgnoredWithoutAskingForAnything()
    {
        var session = Session(out var sent);
        session.Enter(Room);
        session.Receive(Attached(epoch: 4, seq: 5), Now);

        session.Receive(Event(epoch: 3, seq: 6, new CasinoRoomEventDto(Occupancy: 77)), Now);

        Assert.Equal(4, session.State!.Snapshot.Occupancy);
        Assert.Equal(4, session.State!.Epoch);
        Assert.Single(sent);
        Assert.False(session.AwaitingSnapshot);
    }

    [Fact]
    public void AFrameFromANewerEpochIsNeverApplied()
    {
        var session = Session(out var sent);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5), Now);

        session.Receive(Event(epoch: 2, seq: 1006, new CasinoRoomEventDto(Occupancy: 77)), Now);

        Assert.Equal(4, session.State!.Snapshot.Occupancy);
        Assert.Equal(1, session.State!.Epoch);
        Assert.True(session.AwaitingSnapshot);
        Assert.Equal(2, sent.Count);
        Assert.Equal(SignalType.CasinoResync, sent[1].Type);
    }

    [Fact]
    public void AGapAsksForExactlyOneResyncInsideTheRateLimit()
    {
        var session = Session(out var sent);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5), Now);

        session.Receive(Event(epoch: 1, seq: 9, new CasinoRoomEventDto(Occupancy: 6)), Now);
        session.Receive(Event(epoch: 1, seq: 10, new CasinoRoomEventDto(Occupancy: 7)), Now + 500);
        session.Receive(Event(epoch: 1, seq: 11, new CasinoRoomEventDto(Occupancy: 8)), Now + 1_999);

        Assert.Equal(2, sent.Count);
        Assert.Equal(SignalType.CasinoResync, sent[1].Type);
        Assert.Equal(Room, sent[1].Casino!.RoomId);
        Assert.Equal(4, session.State!.Snapshot.Occupancy);
        Assert.True(session.AwaitingSnapshot);
    }

    [Fact]
    public void AGapThatOutlivesTheRateLimitAsksAgain()
    {
        var session = Session(out var sent);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5), Now);

        session.Receive(Event(epoch: 1, seq: 9, new CasinoRoomEventDto(Occupancy: 6)), Now);
        session.Receive(Event(epoch: 1, seq: 10, new CasinoRoomEventDto(Occupancy: 7)), Now + 2_000);

        Assert.Equal(3, sent.Count);
        Assert.Equal(SignalType.CasinoResync, sent[1].Type);
        Assert.Equal(SignalType.CasinoResync, sent[2].Type);
    }

    [Fact]
    public void TheSnapshotSupersedesEveryFrameThatArrivedDuringTheGap()
    {
        var session = Session(out _);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5), Now);
        session.Receive(Event(epoch: 1, seq: 9, new CasinoRoomEventDto(Occupancy: 6)), Now);
        session.Receive(Event(epoch: 1, seq: 10, new CasinoRoomEventDto(Occupancy: 7)), Now);

        session.Receive(Snapshot(epoch: 1, seq: 12, occupancy: 31), Now);
        session.Receive(Event(epoch: 1, seq: 11, new CasinoRoomEventDto(Occupancy: 99)), Now);
        session.Receive(Event(epoch: 1, seq: 13, new CasinoRoomEventDto(Occupancy: 32)), Now);

        var state = session.State;
        Assert.Equal(13, state!.Seq);
        Assert.Equal(32, state.Snapshot.Occupancy);
        Assert.False(session.AwaitingSnapshot);
    }

    [Fact]
    public void ASnapshotFromANewerEpochLandsEvenWhenItsSeqRewinds()
    {
        var held = Held(epoch: 1, seq: 900);

        Assert.True(CasinoRoomSession.AcceptsSnapshot(held, epoch: 2, seq: 1));
        Assert.False(CasinoRoomSession.AcceptsSnapshot(held, epoch: 0, seq: 5_000));
        Assert.True(CasinoRoomSession.AcceptsSnapshot(held, epoch: 1, seq: 900));
        Assert.False(CasinoRoomSession.AcceptsSnapshot(held, epoch: 1, seq: 899));
        Assert.True(CasinoRoomSession.AcceptsSnapshot(null, epoch: 0, seq: 0));
    }

    [Fact]
    public void TheApplicationMatrixDecidesEveryFrameTheSameWay()
    {
        var held = Held(epoch: 2, seq: 10);

        Assert.Equal(CasinoRoomApply.Resync, CasinoRoomSession.Decide(null, 2, 11));
        Assert.Equal(CasinoRoomApply.Ignore, CasinoRoomSession.Decide(held, 1, 11));
        Assert.Equal(CasinoRoomApply.Resync, CasinoRoomSession.Decide(held, 3, 11));
        Assert.Equal(CasinoRoomApply.Ignore, CasinoRoomSession.Decide(held, 2, 10));
        Assert.Equal(CasinoRoomApply.Ignore, CasinoRoomSession.Decide(held, 2, 9));
        Assert.Equal(CasinoRoomApply.Apply, CasinoRoomSession.Decide(held, 2, 11));
        Assert.Equal(CasinoRoomApply.Resync, CasinoRoomSession.Decide(held, 2, 12));
    }

    [Fact]
    public void ADeclineClosesTheRoomAndStopsTheAttachLoop()
    {
        var session = Session(out var sent);
        session.Enter(Room);

        session.Receive(new CasinoSignal(SignalType.CasinoDeclined, "private", new CasinoPayload { RoomId = Room }),
            Now);

        Assert.Equal("private", session.ClosedReason);
        Assert.Equal(string.Empty, session.RoomId);
        Assert.Null(session.State);

        session.OnRealtimeConnected(true);
        session.RetryAttach(Environment.TickCount64 + 600_000);

        Assert.Single(sent);
    }

    [Theory]
    [InlineData(SignalType.CasinoEnded, "restarting")]
    [InlineData(SignalType.CasinoDeclined, "draining")]
    public void ARestartKeepsTheRoomAndReattachesOnTheNextConnection(string type, string reason)
    {
        var session = Session(out var sent);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5), Now);

        session.Receive(new CasinoSignal(type, reason, new CasinoPayload { RoomId = Room }), Now);

        Assert.Equal(string.Empty, session.ClosedReason);
        Assert.Equal(Room, session.RoomId);
        Assert.NotNull(session.State);
        Assert.False(session.Attached);
        Assert.True(session.AwaitingSnapshot);
        Assert.True(session.Restarting);
        Assert.True(session.Unreachable(Environment.TickCount64));

        session.OnRealtimeConnected(false);
        session.OnRealtimeConnected(true);

        Assert.Equal(2, sent.Count);
        Assert.Equal(SignalType.CasinoAttach, sent[1].Type);
        Assert.Equal(Room, sent[1].Casino!.RoomId);

        session.Receive(Attached(epoch: 2, seq: 1), Now);

        Assert.True(session.Attached);
        Assert.False(session.Restarting);
        Assert.Equal(2, session.State!.Epoch);
    }

    [Fact]
    public void ARestartWithoutADroppedSocketStillAsksAgainOnItsOwn()
    {
        var session = Session(out var sent);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5), Now);
        session.Receive(new CasinoSignal(SignalType.CasinoEnded, "restarting", new CasinoPayload { RoomId = Room }),
            Now);

        session.RetryAttach(Environment.TickCount64);
        Assert.Single(sent);

        session.RetryAttach(Environment.TickCount64 + 5_000);
        Assert.Equal(2, sent.Count);
        Assert.Equal(SignalType.CasinoAttach, sent[1].Type);

        session.RetryAttach(Environment.TickCount64 + 5_500);
        Assert.Equal(2, sent.Count);
    }

    [Fact]
    public void AnAttachCooldownIsRetriedAndNeverEndsAnAttachedRoom()
    {
        var session = Session(out var sent);
        session.Enter(Room);

        session.Receive(new CasinoSignal(SignalType.CasinoDeclined, "cooldown", new CasinoPayload { RoomId = Room }),
            Now);

        Assert.Equal(Room, session.RoomId);
        Assert.Equal(string.Empty, session.ClosedReason);
        Assert.False(session.Restarting);
        session.RetryAttach(Environment.TickCount64 + 5_000);
        Assert.Equal(2, sent.Count);

        session.Receive(Attached(epoch: 1, seq: 5), Now);
        session.Receive(new CasinoSignal(SignalType.CasinoDeclined, "cooldown", new CasinoPayload { RoomId = Room }),
            Now);

        Assert.True(session.Attached);
        Assert.False(session.AwaitingSnapshot);
    }

    [Fact]
    public void APollThatLandsLiftsTheRestartVeil()
    {
        var session = Session(out _);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5), Now);
        session.Receive(new CasinoSignal(SignalType.CasinoEnded, "restarting", new CasinoPayload { RoomId = Room }),
            Now);

        session.AbsorbHttpState(Room, Polled(epoch: 2, seq: 30, occupancy: 3), Now);

        Assert.False(session.Restarting);
        Assert.False(session.Unreachable(Environment.TickCount64));
        Assert.Equal(2, session.State!.Epoch);
    }

    [Fact]
    public void ReattachIsDueOnlyOnceArmedAndOnlyWhileDetached()
    {
        Assert.False(CasinoRoomSession.ReattachDue(0, false, 10_000));
        Assert.False(CasinoRoomSession.ReattachDue(5_000, true, 10_000));
        Assert.False(CasinoRoomSession.ReattachDue(15_000, false, 10_000));
        Assert.True(CasinoRoomSession.ReattachDue(5_000, false, 10_000));
    }

    [Theory]
    [InlineData("restarting", true)]
    [InlineData("draining", true)]
    [InlineData("cooldown", true)]
    [InlineData("ended", false)]
    [InlineData("private", false)]
    [InlineData("kicked", false)]
    public void OnlyAPassingServerConditionKeepsTheRoom(string reason, bool resumes)
    {
        Assert.Equal(resumes, CasinoRoomSession.Resumes(reason));
    }

    [Fact]
    public void AReconnectReattachesTheRoomStillInPlay()
    {
        var session = Session(out var sent);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5), Now);

        session.OnRealtimeConnected(false);
        Assert.False(session.Attached);
        Assert.NotNull(session.State);

        session.OnRealtimeConnected(true);

        Assert.Equal(2, sent.Count);
        Assert.Equal(SignalType.CasinoAttach, sent[1].Type);
        Assert.Equal(Room, sent[1].Casino!.RoomId);
    }

    [Fact]
    public void AReconnectAfterLeavingDoesNotReattachTheRoomThePlayerDropped()
    {
        var session = Session(out var sent);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5), Now);
        session.Leave();

        session.OnRealtimeConnected(false);
        session.OnRealtimeConnected(true);

        Assert.Equal(2, sent.Count);
        Assert.Equal(SignalType.CasinoAttach, sent[0].Type);
        Assert.Equal(SignalType.CasinoDetach, sent[1].Type);
        Assert.Equal(string.Empty, session.RoomId);
        Assert.False(session.AwaitingSnapshot);
    }

    [Fact]
    public void ThePollingPathFillsTheRoomUnderTheSameVersionRules()
    {
        var session = Session(out _);
        session.Enter(Room);

        session.AbsorbHttpState(Room, Polled(epoch: 2, seq: 30, occupancy: 12), Now);

        Assert.Equal(30, session.State!.Seq);
        Assert.Equal(12, session.State!.Snapshot.Occupancy);
        Assert.False(session.AwaitingSnapshot);

        session.AbsorbHttpState(Room, Polled(epoch: 1, seq: 900, occupancy: 99), Now);

        Assert.Equal(12, session.State!.Snapshot.Occupancy);
    }

    [Fact]
    public void APolledSnapshotForAnotherRoomIsDropped()
    {
        var session = Session(out _);
        session.Enter(Room);

        session.AbsorbHttpState("bingo-hall", Polled(epoch: 2, seq: 30, occupancy: 12), Now);
        Assert.Null(session.State);

        session.AbsorbHttpState(Room, Polled(epoch: 2, seq: 30, occupancy: 12) with { RoomId = "bingo-hall" }, Now);
        Assert.Null(session.State);
    }

    [Fact]
    public void APolledFourOhFourClosesTheRoomToo()
    {
        var session = Session(out _);
        session.Enter(Room);

        session.CloseFromHttp("bingo-hall", CasinoReasons.Ended);
        Assert.Equal(Room, session.RoomId);

        session.CloseFromHttp(Room, CasinoReasons.Ended);

        Assert.Equal(CasinoReasons.Ended, session.ClosedReason);
        Assert.Equal(string.Empty, session.RoomId);
    }

    [Fact]
    public void ThePollCarriesTheHandTheSocketCouldNotDeliver()
    {
        var session = Session(out _);
        session.Enter(Room);
        session.AbsorbHttpState(Room, Polled(epoch: 2, seq: 30, occupancy: 12), Now);

        session.AbsorbHttpPrivate(Room, 2, 31, You("hand-7", 0, new[] { 40, 41 }));

        var personal = session.Private;
        Assert.NotNull(personal);
        Assert.Equal(2, personal.Epoch);
        Assert.Equal(31, personal.Seq);
        Assert.NotNull(personal.Blackjack);
        Assert.Equal(0, personal.Blackjack.SeatIndex);
        Assert.Equal(new[] { 40, 41 }, personal.Blackjack.Hands![0].Cards);
    }

    [Fact]
    public void APolledHandIsVersionedLikeEveryOtherFrame()
    {
        var session = Session(out _);
        session.Enter(Room);
        session.AbsorbHttpPrivate(Room, 2, 31, You("hand-7", 0, new[] { 40, 41 }));

        session.AbsorbHttpPrivate(Room, 2, 30, You("hand-6", 0, new[] { 2, 3 }));
        Assert.Equal(31, session.Private!.Seq);

        session.AbsorbHttpPrivate(Room, 1, 900, You("hand-6", 0, new[] { 2, 3 }));
        Assert.Equal(31, session.Private!.Seq);

        session.AbsorbHttpPrivate("bingo-hall", 3, 1, You("hand-8", 0, new[] { 4, 5 }));
        Assert.Equal(31, session.Private!.Seq);

        session.AbsorbHttpPrivate(Room, 3, 1, You("hand-8", 0, new[] { 4, 5 }));
        Assert.Equal(3, session.Private!.Epoch);
        Assert.Equal("hand-8", session.Private!.Blackjack!.HandId);
    }

    private static CasinoBlackjackYouDto You(string handId, int seatIndex, int[] cards)
    {
        return new CasinoBlackjackYouDto(HandId: handId, SeatIndex: seatIndex,
            Hands: new[] { new CasinoBlackjackHandDto(Cards: cards) });
    }

    [Fact]
    public void ARoomOnThePollAloneIsNotAnUnreachableRoom()
    {
        Assert.False(CasinoRoomSession.Unreachable(true, 0, 10_000_000));
        Assert.False(CasinoRoomSession.Unreachable(false, 0, 10_000_000));
        Assert.False(CasinoRoomSession.Unreachable(false, 1_000, 1_000));
        Assert.False(CasinoRoomSession.Unreachable(false, 1_000, 12_999));
        Assert.True(CasinoRoomSession.Unreachable(false, 1_000, 13_000));
    }

    [Fact]
    public void EveryLandedReadPutsTheRoomBackInTouch()
    {
        var session = Session(out _);
        session.Enter(Room);
        Assert.False(session.Unreachable(Environment.TickCount64));

        session.AbsorbHttpState(Room, Polled(epoch: 2, seq: 30, occupancy: 12), Now);
        Assert.False(session.Unreachable(Environment.TickCount64));

        session.Receive(Attached(epoch: 3, seq: 1), Now);
        Assert.True(session.Attached);
        Assert.False(session.Unreachable(Environment.TickCount64 + 600_000));

        session.Leave();
        Assert.False(session.Unreachable(Environment.TickCount64 + 600_000));
    }

    [Fact]
    public void TheFirstServerStampAnchorsTheSkewAndTheRestCrawl()
    {
        var session = Session(out _);
        session.Enter(Room);

        session.Receive(Attached(epoch: 1, seq: 5, serverNowUnixMs: Now + 400), Now);
        Assert.Equal(400, session.SkewMilliseconds);

        session.Receive(Event(epoch: 1, seq: 6, new CasinoRoomEventDto(Occupancy: 5), Now + 800), Now);
        Assert.Equal(500, session.SkewMilliseconds);
    }

    [Fact]
    public void TheDirectoryClockAnchorsTheSkewWithoutEnteringARoom()
    {
        var session = Session(out _);

        session.AbsorbClock(Now + 250, Now);

        Assert.Equal(250, session.SkewMilliseconds);
        Assert.Equal(Now + 250, session.ServerNowUnixMs(Now));
    }

    [Fact]
    public void AClockThatJumpsSnapsInsteadOfCrawling()
    {
        Assert.Equal(100, CasinoRoomSession.SmoothedSkew(0, 400));
        Assert.Equal(-100, CasinoRoomSession.SmoothedSkew(0, -400));
        Assert.Equal(1_249, CasinoRoomSession.SmoothedSkew(0, 4_999));
        Assert.Equal(5_000, CasinoRoomSession.SmoothedSkew(0, 5_000));
        Assert.Equal(90_000, CasinoRoomSession.SmoothedSkew(120, 90_000));
        Assert.Equal(-90_000, CasinoRoomSession.SmoothedSkew(120, -90_000));
    }

    [Fact]
    public void CountdownsRenderFromTheServerClockAndNeverGoNegative()
    {
        var session = Session(out _);
        session.Enter(Room);
        session.Receive(Attached(epoch: 1, seq: 5, serverNowUnixMs: Now + 3_000), Now);

        Assert.Equal(Now + 3_000, session.ServerNowUnixMs(Now));
        Assert.Equal(17_000, session.RemainingMilliseconds(Now + 20_000, Now));
        Assert.Equal(0, session.RemainingMilliseconds(Now + 1_000, Now));
        Assert.Equal(0, session.RemainingMilliseconds(0, Now));
    }

    [Fact]
    public void ASocketThatIsNotUpNeverSwallowsTheAttach()
    {
        var signals = new RealtimeSignalBus();
        var sent = new List<CallControl>();
        signals.BindSender(sent.Add);
        var session = new CasinoRoomSession(signals);

        session.Enter(Room);

        Assert.Empty(sent);
        Assert.Equal(Room, session.RoomId);
    }

    private static CasinoRoomSession Session(out List<CallControl> sent)
    {
        var captured = new List<CallControl>();
        var signals = new RealtimeSignalBus();
        signals.SetActive(true);
        signals.BindSender(captured.Add);
        sent = captured;
        return new CasinoRoomSession(signals);
    }

    private static CasinoRoomState Held(int epoch, long seq)
    {
        return new CasinoRoomState(Room, epoch, seq, new CasinoRoomSnapshotDto(RoomId: Room), null, null, null);
    }

    private static CasinoRoomSnapshotDto Board(int occupancy)
    {
        return new CasinoRoomSnapshotDto(
            RoomId: Room,
            GameKind: CasinoWire.WheelKind,
            Phase: CasinoRoomPhases.Result,
            RoundIndex: 7,
            PhaseEndsAtUnixMs: Now + 20_000,
            GameState: WheelBlob,
            Occupancy: occupancy);
    }

    private static CasinoRoomSnapshotDto Polled(int epoch, long seq, int occupancy)
    {
        return Board(occupancy) with
        {
            Attached = false,
            Epoch = epoch,
            Seq = seq,
            ServerNowUnixMs = Now,
        };
    }

    private static CasinoSignal Attached(int epoch, long seq, long serverNowUnixMs = 0)
    {
        return new CasinoSignal(SignalType.CasinoAttached, null, new CasinoPayload
        {
            RoomId = Room,
            Epoch = epoch,
            Seq = seq,
            ServerNowUnixMs = serverNowUnixMs,
            Snapshot = Board(4) with { Attached = true, Epoch = epoch, Seq = seq },
        });
    }

    private static CasinoSignal Snapshot(int epoch, long seq, int occupancy)
    {
        return new CasinoSignal(SignalType.CasinoSnapshot, null, new CasinoPayload
        {
            RoomId = Room,
            Epoch = epoch,
            Seq = seq,
            Snapshot = Board(occupancy) with { Epoch = epoch, Seq = seq },
        });
    }

    private static CasinoSignal Event(int epoch, long seq, CasinoRoomEventDto change, long serverNowUnixMs = 0)
    {
        return new CasinoSignal(SignalType.CasinoEvent, null, new CasinoPayload
        {
            RoomId = Room,
            Epoch = epoch,
            Seq = seq,
            EventKind = "wheel.lock",
            ServerNowUnixMs = serverNowUnixMs,
            Event = change,
        });
    }
}
