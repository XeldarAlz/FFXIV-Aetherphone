using Aetherphone.Core.Jam;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Video;
using Xunit;

namespace Aetherphone.Tests;

public sealed class JamSessionRulesTests
{
    private const float Tick = 1f / 60f;

    private static JamRouteDecision Guest(PlaybackIntentKind kind, int permissions, bool localHold = false,
        bool roomPaused = false, bool ownsEntry = false)
    {
        return JamIntentRouting.ForGuest(kind, new JamGuestContext(permissions, localHold, roomPaused, ownsEntry));
    }

    private static JamRouteDecision Host(PlaybackIntentKind kind, bool songActive = true, int queued = 0,
        bool repeatOne = false)
    {
        return JamIntentRouting.ForHost(kind, new JamHostContext(songActive, queued, repeatOne));
    }

    [Theory]
    [InlineData((byte)PlaybackIntentKind.Next, (byte)JamRoute.ControlNext)]
    [InlineData((byte)PlaybackIntentKind.Previous, (byte)JamRoute.ControlPrevious)]
    [InlineData((byte)PlaybackIntentKind.Seek, (byte)JamRoute.ControlSeek)]
    [InlineData((byte)PlaybackIntentKind.MoveQueued, (byte)JamRoute.QueueMove)]
    public void GuestWithControlTurnsTransportIntoRequests(byte kind, byte expected)
    {
        var decision = Guest((PlaybackIntentKind)kind, JamPermission.ControlPlayback);

        Assert.Equal((JamRoute)expected, decision.Route);
        Assert.True(decision.Handled);
        Assert.Equal(JamRefusal.None, decision.Refusal);
    }

    [Theory]
    [InlineData((byte)PlaybackIntentKind.Next)]
    [InlineData((byte)PlaybackIntentKind.Previous)]
    [InlineData((byte)PlaybackIntentKind.Seek)]
    [InlineData((byte)PlaybackIntentKind.TogglePlayPause)]
    [InlineData((byte)PlaybackIntentKind.JumpTo)]
    public void GuestWithoutControlIsRefusedButHandled(byte kind)
    {
        var decision = Guest((PlaybackIntentKind)kind, JamPermission.AddToQueue);

        Assert.Equal(JamRoute.Refuse, decision.Route);
        Assert.Equal(JamRefusal.HostControlsPlayback, decision.Refusal);
        Assert.True(decision.Handled);
    }

    [Fact]
    public void GuestPlayPauseFollowsTheRoomNotTheLocalPlayer()
    {
        Assert.Equal(JamRoute.ControlPlay,
            Guest(PlaybackIntentKind.TogglePlayPause, JamPermission.All, roomPaused: true).Route);
        Assert.Equal(JamRoute.ControlPause,
            Guest(PlaybackIntentKind.TogglePlayPause, JamPermission.All, roomPaused: false).Route);
    }

    [Fact]
    public void GuestJumpToIsHostOnlyEvenWithControl()
    {
        var decision = Guest(PlaybackIntentKind.JumpTo, JamPermission.All);

        Assert.Equal(JamRoute.Refuse, decision.Route);
        Assert.Equal(JamRefusal.HostControlsPlayback, decision.Refusal);
    }

    [Theory]
    [InlineData((byte)PlaybackIntentKind.PlayNext, (byte)JamRoute.QueueNext)]
    [InlineData((byte)PlaybackIntentKind.PlayLast, (byte)JamRoute.QueueEnd)]
    [InlineData((byte)PlaybackIntentKind.PlaySongs, (byte)JamRoute.QueueEnd)]
    public void GuestWithAddPermissionQueuesOnTheServer(byte kind, byte expected)
    {
        Assert.Equal((JamRoute)expected, Guest((PlaybackIntentKind)kind, JamPermission.AddToQueue).Route);
    }

    [Theory]
    [InlineData((byte)PlaybackIntentKind.PlayNext)]
    [InlineData((byte)PlaybackIntentKind.PlayLast)]
    [InlineData((byte)PlaybackIntentKind.PlaySongs)]
    [InlineData((byte)PlaybackIntentKind.MoveQueued)]
    public void GuestWithoutQueuePermissionIsRefused(byte kind)
    {
        var decision = Guest((PlaybackIntentKind)kind, JamPermission.None);

        Assert.Equal(JamRoute.Refuse, decision.Route);
        Assert.Equal(JamRefusal.HostControlsQueue, decision.Refusal);
    }

    [Fact]
    public void GuestRemovesOnlyTheirOwnEntries()
    {
        Assert.Equal(JamRoute.QueueRemove,
            Guest(PlaybackIntentKind.RemoveQueued, JamPermission.None, ownsEntry: true).Route);

        var refused = Guest(PlaybackIntentKind.RemoveQueued, JamPermission.All, ownsEntry: false);
        Assert.Equal(JamRoute.Refuse, refused.Route);
        Assert.Equal(JamRefusal.NotYourEntry, refused.Refusal);
    }

    [Fact]
    public void GuestStopHoldsLocallyAndPlayReleasesItWithoutPermission()
    {
        Assert.Equal(JamRoute.HoldLocally, Guest(PlaybackIntentKind.Stop, JamPermission.None).Route);
        Assert.Equal(JamRoute.ReleaseHold,
            Guest(PlaybackIntentKind.TogglePlayPause, JamPermission.None, localHold: true).Route);
    }

    [Fact]
    public void GuestSwallowsTheLocalTrackEnd()
    {
        var decision = Guest(PlaybackIntentKind.TrackEnded, JamPermission.All);

        Assert.Equal(JamRoute.Swallow, decision.Route);
        Assert.True(decision.Handled);
    }

    [Fact]
    public void HostTransportStaysLocalAndPublishes()
    {
        Assert.Equal(JamRoute.LocalThenPublish, Host(PlaybackIntentKind.TogglePlayPause).Route);
        Assert.Equal(JamRoute.LocalThenPublish, Host(PlaybackIntentKind.Seek).Route);
        Assert.Equal(JamRoute.LocalThenPublish, Host(PlaybackIntentKind.Previous).Route);
        Assert.False(Host(PlaybackIntentKind.Seek).Handled);
    }

    [Fact]
    public void HostNextAndTrackEndDrawFromTheServerQueue()
    {
        Assert.Equal(JamRoute.Advance, Host(PlaybackIntentKind.Next, queued: 2).Route);
        Assert.Equal(JamRoute.LocalThenPublish, Host(PlaybackIntentKind.Next, queued: 0).Route);
        Assert.Equal(JamRoute.Advance, Host(PlaybackIntentKind.TrackEnded, queued: 1).Route);
        Assert.Equal(JamRoute.LocalThenPublish, Host(PlaybackIntentKind.TrackEnded, queued: 0).Route);
        Assert.Equal(JamRoute.LocalThenPublish, Host(PlaybackIntentKind.TrackEnded, queued: 3, repeatOne: true).Route);
    }

    [Fact]
    public void HostQueueIntentsGoToTheServer()
    {
        Assert.Equal(JamRoute.QueueNext, Host(PlaybackIntentKind.PlayNext).Route);
        Assert.Equal(JamRoute.QueueEnd, Host(PlaybackIntentKind.PlayLast).Route);
        Assert.Equal(JamRoute.QueueRemove, Host(PlaybackIntentKind.RemoveQueued).Route);
        Assert.Equal(JamRoute.QueueMove, Host(PlaybackIntentKind.MoveQueued).Route);
        Assert.Equal(JamRoute.AdvanceTo, Host(PlaybackIntentKind.JumpTo).Route);
        Assert.Equal(JamRoute.PlayNow, Host(PlaybackIntentKind.PlaySongs).Route);
        Assert.Equal(JamRoute.PauseForEveryone, Host(PlaybackIntentKind.Stop).Route);
    }

    [Fact]
    public void HostQueueingWhileIdleStartsPlaybackLocally()
    {
        Assert.Equal(JamRoute.Local, Host(PlaybackIntentKind.PlayNext, songActive: false).Route);
        Assert.Equal(JamRoute.Local, Host(PlaybackIntentKind.PlayLast, songActive: false).Route);
    }

    [Fact]
    public void DriftInsideTheDeadZoneHolds()
    {
        var controller = new JamDriftController();

        var decision = controller.Step(100.15, 100.0, 300, false, Tick);

        Assert.False(decision.RateChanged);
        Assert.False(decision.Seek);
        Assert.Equal(1f, controller.AppliedRate);
    }

    [Fact]
    public void BehindTheHostSpeedsUpWithinTheAudioBand()
    {
        var controller = new JamDriftController();

        var decision = controller.Step(99.0, 100.0, 300, false, Tick);

        Assert.True(decision.RateChanged);
        Assert.False(decision.Seek);
        Assert.InRange(decision.Rate, 1f + JamDriftController.MinimumRateDeviation - 0.0001f,
            1f + JamDriftController.MaximumRateDeviation + 0.0001f);
    }

    [Fact]
    public void AheadOfTheHostSlowsDownAndSmallDriftUsesTheFloor()
    {
        var controller = new JamDriftController();

        var decision = controller.Step(100.3, 100.0, 300, false, Tick);

        Assert.True(decision.RateChanged);
        Assert.Equal(1f - JamDriftController.MinimumRateDeviation, decision.Rate, 3);
    }

    [Fact]
    public void LargeDriftSeeksAndThenSettles()
    {
        var controller = new JamDriftController();
        controller.Step(99.0, 100.0, 300, false, Tick);

        var decision = controller.Step(90.0, 100.0, 300, false, Tick);

        Assert.True(decision.Seek);
        Assert.Equal(100.0, decision.SeekTarget, 3);
        Assert.True(decision.RateChanged);
        Assert.Equal(1f, decision.Rate);

        var settling = controller.Step(97.0, 100.0, 300, false, Tick);
        Assert.Equal(JamDriftDecision.Hold, settling);
    }

    [Fact]
    public void CorrectionKeepsGoingUntilTheReleaseBand()
    {
        var controller = new JamDriftController();
        controller.Step(100.5, 100.0, 300, false, Tick);
        Assert.True(controller.Correcting);

        var stillCorrecting = controller.Step(100.1, 100.0, 300, false, Tick);
        Assert.True(controller.Correcting);
        Assert.NotEqual(1f, controller.AppliedRate);
        Assert.False(stillCorrecting.Seek);

        var released = controller.Step(100.03, 100.0, 300, false, Tick);
        Assert.True(released.RateChanged);
        Assert.Equal(1f, released.Rate);
        Assert.False(controller.Correcting);
    }

    [Fact]
    public void HoldingAndTheTrackEndNeverNudge()
    {
        var controller = new JamDriftController();

        Assert.Equal(JamDriftDecision.Hold, controller.Step(90.0, 100.0, 300, true, Tick));

        controller.Step(99.0, 100.0, 300, false, Tick);
        var nearEnd = controller.Step(298.0, 299.5, 300, false, Tick);
        Assert.True(nearEnd.RateChanged);
        Assert.Equal(1f, nearEnd.Rate);
        Assert.False(nearEnd.Seek);
    }

    [Fact]
    public void ReleaseRestoresNormalSpeedOnce()
    {
        var controller = new JamDriftController();
        controller.Step(99.0, 100.0, 300, false, Tick);

        Assert.True(controller.Release());
        Assert.False(controller.Release());
        Assert.Equal(1f, controller.AppliedRate);
    }

    [Theory]
    [InlineData(3, 4, false, true)]
    [InlineData(4, 4, false, false)]
    [InlineData(5, 4, false, false)]
    [InlineData(9, 0, true, true)]
    public void QueueVersionsOnlyMoveForwardOutsideSnapshots(int current, int incoming, bool snapshot,
        bool expected)
    {
        Assert.Equal(expected, JamQueueVersion.ShouldApply(current, incoming, snapshot));
    }

    [Theory]
    [InlineData("abc234", "ABC234", "ABC 234")]
    [InlineData("ABC-234", "ABC234", "ABC 234")]
    [InlineData(" abc 234 ", "ABC234", "ABC 234")]
    public void CodesNormalizeAndFormatThreeByThree(string input, string normalized, string display)
    {
        var code = PartyCode.Normalize(input);

        Assert.Equal(normalized, code);
        Assert.Equal(display, PartyCode.Display(code));
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABC23")]
    [InlineData("ABC2345")]
    public void MalformedCodesNormalizeToEmpty(string input)
    {
        Assert.Equal(string.Empty, PartyCode.Normalize(input));
    }

    [Fact]
    public void PacerNeverExceedsTheServerWindow()
    {
        var pacer = new JamOperationPacer();
        for (var index = 0; index < JamOperationPacer.MaxPerWindow; index++)
        {
            Assert.True(pacer.TryAcquire(1_000 + index));
        }

        Assert.False(pacer.TryAcquire(1_100));
        Assert.False(pacer.TryAcquire(1_000 + JamOperationPacer.WindowMilliseconds - 1));
        Assert.True(pacer.TryAcquire(1_000 + JamOperationPacer.WindowMilliseconds));
        Assert.False(pacer.TryAcquire(1_000 + JamOperationPacer.WindowMilliseconds));
    }

    [Fact]
    public void InvitesAreSpacedPastTheServerCooldownAndDeduplicated()
    {
        var invites = new JamInviteQueue();
        invites.Enqueue("first");
        invites.Enqueue("second");
        invites.Enqueue("first");
        Assert.Equal(2, invites.Count);

        Assert.True(invites.TryDequeue(5_000, out var firstSent));
        Assert.Equal("first", firstSent);
        Assert.False(invites.TryDequeue(5_000 + 2_000, out _));
        Assert.True(invites.TryDequeue(5_000 + JamInviteQueue.SpacingMilliseconds, out var secondSent));
        Assert.Equal("second", secondSent);
        Assert.False(invites.TryDequeue(60_000, out _));
    }

    [Fact]
    public void RepeatedInvitesToTheSameJamNotifyOnce()
    {
        var gate = new JamInviteGate();
        Assert.True(gate.Admit("ABC234", 1_000));
        Assert.False(gate.Admit("ABC234", 1_500));
        Assert.True(gate.Admit("XYZ789", 1_600));
        Assert.True(gate.Admit("ABC234", 1_700));
        Assert.False(gate.Admit("ABC234", 1_700 + JamInviteGate.RepeatMilliseconds - 1));
        Assert.True(gate.Admit("ABC234", 1_700 + JamInviteGate.RepeatMilliseconds));
    }

    [Fact]
    public void PacerSlidingWindowFitsEveryFixedServerWindow()
    {
        var pacer = new JamOperationPacer();
        var sent = new List<long>();
        for (long now = 0; now < 30_000; now += 50)
        {
            if (pacer.TryAcquire(now))
            {
                sent.Add(now);
            }
        }

        for (long windowStart = 0; windowStart < 30_000; windowStart += 50)
        {
            var inWindow = 0;
            for (var index = 0; index < sent.Count; index++)
            {
                if (sent[index] >= windowStart && sent[index] < windowStart + 4_000)
                {
                    inWindow++;
                }
            }

            Assert.True(inWindow <= 8, $"window at {windowStart} carried {inWindow}");
        }
    }

    [Fact]
    public void ReactionsKeepTheNewestFirstAndIgnoreUnknownKinds()
    {
        var reactions = new JamReactions();
        reactions.Add(new JamReaction(1, "a", "A", 10));
        reactions.Add(new JamReaction(JamReactions.MaxKinds, "b", "B", 11));
        reactions.Add(new JamReaction(2, "c", "C", 12));

        Assert.Equal(2, reactions.Count);
        Assert.Equal("c", reactions.NewestAt(0).UserId);
        Assert.Equal("a", reactions.NewestAt(1).UserId);

        for (var index = 0; index < JamReactions.Capacity + 3; index++)
        {
            reactions.Add(new JamReaction(0, "x", "X", 100 + index));
        }

        Assert.Equal(JamReactions.Capacity, reactions.Count);
        Assert.Equal(100 + JamReactions.Capacity + 2, reactions.NewestAt(0).AtTicks);
    }

    [Fact]
    public void InviteGroupKeysRoundTripTheCode()
    {
        Assert.True(JamInviteNotification.TryParseCode(JamInviteNotification.GroupKey("ABC234"), out var code));
        Assert.Equal("ABC234", code);
        Assert.False(JamInviteNotification.TryParseCode(JamSession.InviteGroupPrefix, out _));
        Assert.False(JamInviteNotification.TryParseCode("music", out _));
        Assert.False(JamInviteNotification.TryParseCode(null, out _));
    }

    [Fact]
    public void TitlesAreTrimmedAndCapped()
    {
        Assert.Equal("Chill", JamWire.NormalizeTitle("  Chill  "));
        Assert.Equal(string.Empty, JamWire.NormalizeTitle(null));
        Assert.Equal(JamWire.MaxTitleLength, JamWire.NormalizeTitle(new string('a', 100)).Length);
    }
}
