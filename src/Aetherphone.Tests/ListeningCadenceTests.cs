using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Songs;
using Aetherphone.Core.Telephony.Contracts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ListeningCadenceTests
{
    private const long Start = 1_000_000;

    private static ListeningState Playing(string videoId, bool paused = false, string jamCode = "") =>
        new(true, videoId, paused, jamCode, 0);

    private static ListeningMark PublishedAt(string videoId, long sentAt, bool paused = false, string jamCode = "")
    {
        var mark = ListeningMark.Fresh;
        ListeningCadence.Sent(ref mark, Playing(videoId, paused, jamCode), sentAt);
        return mark;
    }

    [Fact]
    public void FirstSongPublishesImmediately()
    {
        var mark = ListeningMark.Fresh;

        Assert.Equal(ListeningAction.Publish, ListeningCadence.Decide(mark, Playing("a"), Start));
    }

    [Fact]
    public void NothingIsPublishedWhenSharingIsOff()
    {
        var mark = ListeningMark.Fresh;
        var state = new ListeningState(false, "a", false, string.Empty, 0);

        Assert.Equal(ListeningAction.None, ListeningCadence.Decide(mark, state, Start));
    }

    [Fact]
    public void TrackChangeWaitsForTheTenSecondWindow()
    {
        var mark = PublishedAt("a", Start);

        Assert.Equal(ListeningAction.None, ListeningCadence.Decide(mark, Playing("b"), Start + 3_000));
        Assert.Equal(ListeningAction.Publish,
            ListeningCadence.Decide(mark, Playing("b"), Start + ListeningCadence.MinimumIntervalMilliseconds));
    }

    [Fact]
    public void PauseAndResumeArePublished()
    {
        var mark = PublishedAt("a", Start);
        var later = Start + ListeningCadence.MinimumIntervalMilliseconds;

        Assert.Equal(ListeningAction.Publish, ListeningCadence.Decide(mark, Playing("a", true), later));
        var paused = PublishedAt("a", Start, true);
        Assert.Equal(ListeningAction.Publish, ListeningCadence.Decide(paused, Playing("a"), later));
    }

    [Fact]
    public void JamCodeChangeIsPublished()
    {
        var mark = PublishedAt("a", Start);
        var later = Start + ListeningCadence.MinimumIntervalMilliseconds;

        Assert.Equal(ListeningAction.Publish, ListeningCadence.Decide(mark, Playing("a", false, "JAM1"), later));
    }

    [Fact]
    public void HeartbeatFiresEveryMinuteWhilePlaying()
    {
        var mark = PublishedAt("a", Start);

        Assert.Equal(ListeningAction.None, ListeningCadence.Decide(mark, Playing("a"), Start + 59_000));
        Assert.Equal(ListeningAction.Publish,
            ListeningCadence.Decide(mark, Playing("a"), Start + ListeningCadence.HeartbeatIntervalMilliseconds));
    }

    [Fact]
    public void NoHeartbeatWhilePaused()
    {
        var mark = PublishedAt("a", Start, true);

        Assert.Equal(ListeningAction.None, ListeningCadence.Decide(mark, Playing("a", true), Start + 10 * 60_000));
    }

    [Fact]
    public void OptOutClearsImmediately()
    {
        var mark = PublishedAt("a", Start);
        var state = new ListeningState(false, "a", false, string.Empty, 0);

        Assert.Equal(ListeningAction.Clear, ListeningCadence.Decide(mark, state, Start + 1));
    }

    [Fact]
    public void StopClearsAfterTheGracePeriod()
    {
        var mark = PublishedAt("a", Start);
        var idleSince = Start + 1_000;
        var stopped = new ListeningState(true, string.Empty, false, string.Empty, idleSince);

        Assert.Equal(ListeningAction.None, ListeningCadence.Decide(mark, stopped, idleSince + 1_000));
        Assert.Equal(ListeningAction.Clear,
            ListeningCadence.Decide(mark, stopped, idleSince + ListeningCadence.StopGraceMilliseconds));
    }

    [Fact]
    public void StoppedWithNothingPublishedStaysQuiet()
    {
        var mark = ListeningMark.Fresh;
        var stopped = new ListeningState(true, string.Empty, false, string.Empty, Start);

        Assert.Equal(ListeningAction.None, ListeningCadence.Decide(mark, stopped, Start + 60_000));
    }

    [Fact]
    public void FailedPublishBacksOffThenRetries()
    {
        var mark = PublishedAt("a", Start);
        ListeningCadence.Completed(ref mark, ListeningAction.Publish, false, Start + 500);

        Assert.Equal(ListeningAction.None, ListeningCadence.Decide(mark, Playing("a"), Start + 5_000));
        Assert.Equal(ListeningAction.Publish, ListeningCadence.Decide(mark, Playing("a"), Start + 10_500));
    }

    [Fact]
    public void FailedClearKeepsTheEntryForRetry()
    {
        var mark = PublishedAt("a", Start);
        var state = new ListeningState(false, string.Empty, false, string.Empty, 0);
        ListeningCadence.Completed(ref mark, ListeningAction.Clear, false, Start);

        Assert.True(mark.Published);
        Assert.Equal(ListeningAction.Clear, ListeningCadence.Decide(mark, state, Start + 10_000));
        ListeningCadence.Completed(ref mark, ListeningAction.Clear, true, Start + 10_000);
        Assert.False(mark.Published);
        Assert.Equal(ListeningAction.None, ListeningCadence.Decide(mark, state, Start + 20_000));
    }

    [Fact]
    public void BackoffDoublesAndCaps()
    {
        Assert.Equal(10_000, ListeningCadence.Backoff(10_000, 1));
        Assert.Equal(20_000, ListeningCadence.Backoff(10_000, 2));
        Assert.Equal(160_000, ListeningCadence.Backoff(10_000, 5));
        Assert.Equal(ListeningCadence.MaximumBackoffMilliseconds, ListeningCadence.Backoff(10_000, 40));
        Assert.Equal(ListeningCadence.MaximumBackoffMilliseconds, ListeningCadence.Backoff(60_000, 4));
    }

    [Fact]
    public void RecentUpdateReadsAsListeningNow()
    {
        Assert.Equal(ListeningStatus.Now, ListeningCadence.Status(false, Start, Start + 90_000));
    }

    [Fact]
    public void PausedWinsOverRecency()
    {
        Assert.Equal(ListeningStatus.Paused, ListeningCadence.Status(true, Start, Start + 1_000));
    }

    [Fact]
    public void StaleUpdateReadsAsMinutesAgo()
    {
        Assert.Equal(ListeningStatus.Earlier, ListeningCadence.Status(false, Start, Start + 4 * 60_000));
        Assert.Equal(4, ListeningCadence.MinutesAgo(Start, Start + 4 * 60_000 + 30_000));
    }

    [Fact]
    public void MinutesAgoNeverReadsZero()
    {
        Assert.Equal(1, ListeningCadence.MinutesAgo(Start, Start + 5_000));
        Assert.Equal(1, ListeningCadence.MinutesAgo(Start, Start - 5_000));
    }

    [Fact]
    public void UnusableFriendsAreDropped()
    {
        var good = new ListeningFriendDto("u1", "Ana", "ana", null, new JamTrack("v1"));
        var friends = new[]
        {
            good,
            new ListeningFriendDto("u2", "Bo", "bo"),
            new ListeningFriendDto("u3", Track: new JamTrack(string.Empty)),
        };

        var usable = ListeningCadence.Usable(friends);

        Assert.Single(usable);
        Assert.Same(good, usable[0]);
        Assert.Empty(ListeningCadence.Usable(null));
    }
}
