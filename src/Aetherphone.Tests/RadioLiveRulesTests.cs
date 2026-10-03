using Aetherphone.Apps.Music;
using Aetherphone.Apps.Music.Radio.Live;
using Aetherphone.Core.Radio;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RadioLiveRulesTests
{
    [Fact]
    public void SuspensionLocksTheComposerBeforeAnythingElse()
    {
        Assert.Equal(RadioComposerLock.Suspended,
            RadioLiveRules.Evaluate(true, false, RadioRoomStatus.Unavailable, true, false));
        Assert.Equal(RadioComposerLock.SignedOut,
            RadioLiveRules.Evaluate(false, false, RadioRoomStatus.Attached, false, false));
    }

    [Fact]
    public void RoomStatusDecidesBetweenConnectingAndUnavailable()
    {
        Assert.Equal(RadioComposerLock.Connecting,
            RadioLiveRules.Evaluate(false, true, RadioRoomStatus.Attaching, false, false));
        Assert.Equal(RadioComposerLock.Connecting,
            RadioLiveRules.Evaluate(false, true, RadioRoomStatus.Idle, false, false));
        Assert.Equal(RadioComposerLock.Unavailable,
            RadioLiveRules.Evaluate(false, true, RadioRoomStatus.Unavailable, false, false));
        Assert.Equal(RadioComposerLock.None,
            RadioLiveRules.Evaluate(false, true, RadioRoomStatus.Attached, false, false));
    }

    [Fact]
    public void MutedListenersAreLockedButModeratorsAreNot()
    {
        Assert.Equal(RadioComposerLock.Muted,
            RadioLiveRules.Evaluate(false, true, RadioRoomStatus.Attached, true, false));
        Assert.Equal(RadioComposerLock.None,
            RadioLiveRules.Evaluate(false, true, RadioRoomStatus.Attached, true, true));
    }

    [Fact]
    public void RemainingMuteTimeRoundsUpIntoTheRightUnit()
    {
        Assert.Equal(new RadioDuration(RadioDurationUnit.Seconds, 1, 0), RadioLiveRules.Remaining(1_001, 1_000));
        Assert.Equal(new RadioDuration(RadioDurationUnit.Seconds, 59, 0), RadioLiveRules.Remaining(59_000, 0));
        Assert.Equal(new RadioDuration(RadioDurationUnit.Minutes, 1, 0), RadioLiveRules.Remaining(60_000, 0));
        Assert.Equal(new RadioDuration(RadioDurationUnit.Minutes, 10, 0), RadioLiveRules.Remaining(9 * 60_000 + 1, 0));
        Assert.Equal(new RadioDuration(RadioDurationUnit.HoursMinutes, 1, 0), RadioLiveRules.Remaining(60 * 60_000, 0));
        Assert.Equal(new RadioDuration(RadioDurationUnit.HoursMinutes, 23, 59),
            RadioLiveRules.Remaining(24 * 60 * 60_000 - 60_000, 0));
        Assert.Equal(new RadioDuration(RadioDurationUnit.Seconds, 0, 0), RadioLiveRules.Remaining(0, 5_000));
    }

    [Fact]
    public void CounterAppearsNearTheLimit()
    {
        Assert.False(RadioLiveRules.ShowsCounter(259, 300));
        Assert.True(RadioLiveRules.ShowsCounter(260, 300));
        Assert.True(RadioLiveRules.ShowsCounter(300, 300));
    }

    [Theory]
    [InlineData("https://youtu.be/abc", true)]
    [InlineData("  http://example.com/song", true)]
    [InlineData("music.youtube.com/watch?v=1 please", true)]
    [InlineData("Answers - Susan Calloway", false)]
    [InlineData("", false)]
    public void LinksAreRecognised(string text, bool expected)
    {
        Assert.Equal(expected, RadioLiveRules.IsLink(text));
    }

    [Fact]
    public void AcceptedRequestsComeFirstThenOldestFirst()
    {
        var entries = new[]
        {
            Request(1, RadioRequestState.Pending, 300),
            Request(2, RadioRequestState.Accepted, 500),
            Request(3, RadioRequestState.Pending, 100),
            Request(4, RadioRequestState.Accepted, 200),
        };
        var order = new int[entries.Length];

        var filled = RadioLiveRules.OrderRequests(entries, entries.Length, order);

        Assert.Equal(4, filled);
        Assert.Equal(new long[] { 4, 2, 3, 1 },
            new[] { entries[order[0]].RequestId, entries[order[1]].RequestId, entries[order[2]].RequestId,
                entries[order[3]].RequestId });
    }

    [Fact]
    public void OrderingNeverWritesPastTheOrderBuffer()
    {
        var entries = new[] { Request(1, RadioRequestState.Pending, 1), Request(2, RadioRequestState.Pending, 2) };
        var order = new int[1];

        Assert.Equal(1, RadioLiveRules.OrderRequests(entries, entries.Length, order));
    }

    [Fact]
    public void ReactionsRiseWithoutOvershootAndFadeOut()
    {
        var previous = -1f;
        for (var age = 0L; age <= 2500L; age += 50L)
        {
            var pose = RadioLiveRules.Pose(age, 2500L, 1);
            Assert.InRange(pose.Rise, 0f, 1f);
            Assert.True(pose.Rise >= previous);
            Assert.InRange(pose.Alpha, 0f, 1f);
            previous = pose.Rise;
        }

        Assert.Equal(0f, RadioLiveRules.Pose(0L, 2500L, 0).Alpha);
        Assert.Equal(1f, RadioLiveRules.Pose(1000L, 2500L, 0).Alpha);
        Assert.Equal(0f, RadioLiveRules.Pose(2500L, 2500L, 0).Alpha, 3);
    }

    [Fact]
    public void ReactionLanesStayInRangeAndAreStable()
    {
        for (var tick = 0L; tick < 200L; tick++)
        {
            var lane = RadioLiveRules.ReactionLane(tick * 97L, (int)(tick % 8));
            Assert.InRange(lane, 0, RadioLiveRules.ReactionLanes - 1);
            Assert.Equal(lane, RadioLiveRules.ReactionLane(tick * 97L, (int)(tick % 8)));
        }
    }

    private static RadioRequestEntry Request(long id, RadioRequestState state, long createdAt)
    {
        return new RadioRequestEntry(id, "user" + id, "Name", "handle", "@handle", null, "Song " + id, state,
            createdAt, false);
    }
}

public sealed class RadioChatFollowTests
{
    private const float Tolerance = 4f;

    [Fact]
    public void FirstFrameJumpsToTheNewestMessage()
    {
        var follow = new RadioChatFollow();

        Assert.True(follow.Update(0f, 0f, Tolerance, 300f, 10, 0));
        Assert.True(follow.Pinned);
        Assert.Equal(10, follow.SeenTailId);
    }

    [Fact]
    public void PinnedTranscriptFollowsNewMessages()
    {
        var follow = new RadioChatFollow();
        follow.Update(0f, 0f, Tolerance, 300f, 10, 0);

        Assert.False(follow.Update(500f, 500f, Tolerance, 300f, 10, 0));
        Assert.True(follow.Update(500f, 500f, Tolerance, 300f, 11, 1));
        Assert.Equal(0, follow.Unseen);
    }

    [Fact]
    public void ScrollingUpStopsFollowingAndCountsUnseenMessages()
    {
        var follow = new RadioChatFollow();
        follow.Update(0f, 0f, Tolerance, 300f, 10, 0);
        follow.Update(500f, 500f, Tolerance, 300f, 10, 0);

        Assert.False(follow.Update(200f, 500f, Tolerance, 300f, 12, 2));
        Assert.False(follow.Pinned);
        Assert.Equal(2, follow.Unseen);
        Assert.Equal(10, follow.SeenTailId);
    }

    [Fact]
    public void JumpRequestRepinsAndClearsTheBadge()
    {
        var follow = new RadioChatFollow();
        follow.Update(0f, 0f, Tolerance, 300f, 10, 0);
        follow.Update(200f, 500f, Tolerance, 300f, 12, 2);

        follow.RequestJump();

        Assert.True(follow.Update(200f, 600f, Tolerance, 300f, 12, 2));
        Assert.True(follow.Pinned);
        Assert.Equal(0, follow.Unseen);
        Assert.Equal(12, follow.SeenTailId);
    }

    [Fact]
    public void ComposerGrowthKeepsAPinnedTranscriptAtTheBottom()
    {
        var follow = new RadioChatFollow();
        follow.Update(0f, 0f, Tolerance, 300f, 10, 0);
        follow.Update(500f, 500f, Tolerance, 300f, 10, 0);

        Assert.True(follow.Update(500f, 500f, Tolerance, 280f, 10, 0));
    }

    [Fact]
    public void ResetStartsFollowingAgain()
    {
        var follow = new RadioChatFollow();
        follow.Update(0f, 0f, Tolerance, 300f, 10, 0);
        follow.Update(200f, 500f, Tolerance, 300f, 12, 2);

        follow.Reset();

        Assert.True(follow.Pinned);
        Assert.Equal(0, follow.Unseen);
        Assert.True(follow.Update(0f, 0f, Tolerance, 300f, 3, 0));
    }
}
