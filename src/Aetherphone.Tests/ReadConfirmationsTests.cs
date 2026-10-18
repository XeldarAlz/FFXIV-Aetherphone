using Aetherphone.Core.Message;
using Aetherphone.Core.Net;
using Xunit;

namespace Aetherphone.Tests;

public sealed class ReadConfirmationsTests
{
    private const string Thread = "thread-a";
    private const string OtherThread = "thread-b";
    private const int Account = 1;
    private static readonly DateTime Start = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ALocalReadWaitsForTheSettleDelay()
    {
        var queue = new ReadConfirmations();
        queue.Request(Thread, Start, ReadConfirmations.SettleDelay, Account);

        Assert.False(queue.TryTakeDue(Start, out _, out _, out _));
        Assert.True(queue.TryTakeDue(Start + ReadConfirmations.SettleDelay, out var key, out _, out _));
        Assert.Equal(Thread, key);
    }

    [Fact]
    public void APushedReadIsDueAtOnce()
    {
        var queue = new ReadConfirmations();
        queue.Request(Thread, Start, TimeSpan.Zero, Account);

        Assert.True(queue.TryTakeDue(Start, out _, out _, out _));
    }

    [Fact]
    public void ALaterRequestNeverPushesAnEarlierDueTimeBack()
    {
        var queue = new ReadConfirmations();
        queue.Request(Thread, Start, TimeSpan.Zero, Account);
        queue.Request(Thread, Start, ReadConfirmations.SettleDelay, Account);

        Assert.True(queue.TryTakeDue(Start, out _, out _, out _));
    }

    [Fact]
    public void ATakenRequestNamesTheAccountItWasMadeUnder()
    {
        var queue = new ReadConfirmations();
        queue.Request(Thread, Start, TimeSpan.Zero, 7);

        Assert.True(queue.TryTakeDue(Start, out _, out _, out var account));
        Assert.Equal(7, account);
    }

    [Fact]
    public void ARequestUnderANewAccountReplacesTheStaleOne()
    {
        var queue = new ReadConfirmations();
        queue.Request(Thread, Start, TimeSpan.Zero, Account);
        queue.Request(Thread, Start, ReadConfirmations.SettleDelay, Account + 1);

        Assert.False(queue.TryTakeDue(Start, out _, out _, out _));
        Assert.True(queue.TryTakeDue(Start + ReadConfirmations.SettleDelay, out _, out _, out var account));
        Assert.Equal(Account + 1, account);
    }

    [Fact]
    public void OnlyOneConfirmationFliesAtATime()
    {
        var queue = new ReadConfirmations();
        queue.Request(Thread, Start, TimeSpan.Zero, Account);
        queue.Request(OtherThread, Start, TimeSpan.Zero, Account);

        Assert.True(queue.TryTakeDue(Start, out var first, out var generation, out _));
        Assert.False(queue.TryTakeDue(Start, out _, out _, out _));

        queue.Complete(first, generation, ReadMarkOutcome.Confirmed, Start);
        Assert.True(queue.TryTakeDue(Start, out var second, out _, out _));
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void AFailedConfirmationRetriesWithBackoff()
    {
        var queue = new ReadConfirmations();
        queue.Request(Thread, Start, TimeSpan.Zero, Account);
        Assert.True(queue.TryTakeDue(Start, out var key, out var generation, out _));

        queue.Complete(key, generation, ReadMarkOutcome.Failed, Start);
        Assert.True(queue.IsPending(Thread));
        Assert.False(queue.TryTakeDue(Start + TimeSpan.FromSeconds(1), out _, out _, out _));
        Assert.True(queue.TryTakeDue(Start + ReadConfirmations.FirstRetryDelay, out key, out generation, out _));

        var secondFailure = Start + ReadConfirmations.FirstRetryDelay;
        queue.Complete(key, generation, ReadMarkOutcome.Failed, secondFailure);
        Assert.False(queue.TryTakeDue(secondFailure + TimeSpan.FromSeconds(3), out _, out _, out _));
        Assert.True(queue.TryTakeDue(secondFailure + TimeSpan.FromSeconds(4), out _, out _, out _));
    }

    [Fact]
    public void RetryDelayDoublesUpToTheCap()
    {
        Assert.Equal(TimeSpan.FromSeconds(2), ReadConfirmations.RetryDelay(1));
        Assert.Equal(TimeSpan.FromSeconds(4), ReadConfirmations.RetryDelay(2));
        Assert.Equal(TimeSpan.FromSeconds(8), ReadConfirmations.RetryDelay(3));
        Assert.Equal(ReadConfirmations.MaxRetryDelay, ReadConfirmations.RetryDelay(12));
    }

    [Theory]
    [InlineData((byte)ReadMarkOutcome.Confirmed)]
    [InlineData((byte)ReadMarkOutcome.Rejected)]
    public void ASettledOutcomeDropsTheRequest(byte outcome)
    {
        var queue = new ReadConfirmations();
        queue.Request(Thread, Start, TimeSpan.Zero, Account);
        Assert.True(queue.TryTakeDue(Start, out var key, out var generation, out _));

        queue.Complete(key, generation, (ReadMarkOutcome)outcome, Start);

        Assert.Equal(0, queue.PendingCount);
    }

    [Fact]
    public void AMissingRouteStopsTheQueueForTheSession()
    {
        var queue = new ReadConfirmations();
        queue.Request(Thread, Start, TimeSpan.Zero, Account);
        queue.Request(OtherThread, Start, TimeSpan.Zero, Account);
        Assert.True(queue.TryTakeDue(Start, out var key, out var generation, out _));

        queue.Complete(key, generation, ReadMarkOutcome.Unsupported, Start);
        queue.Request(Thread, Start, TimeSpan.Zero, Account);

        Assert.True(queue.Unsupported);
        Assert.Equal(0, queue.PendingCount);
        Assert.False(queue.TryTakeDue(Start, out _, out _, out _));
    }

    [Fact]
    public void ANewerRequestOutlivesTheOlderFlight()
    {
        var queue = new ReadConfirmations();
        queue.Request(Thread, Start, TimeSpan.Zero, Account);
        Assert.True(queue.TryTakeDue(Start, out var key, out var generation, out _));

        queue.Request(Thread, Start, TimeSpan.Zero, Account);
        queue.Complete(key, generation, ReadMarkOutcome.Confirmed, Start);

        Assert.True(queue.IsPending(Thread));
        Assert.True(queue.TryTakeDue(Start, out _, out _, out _));
    }

    [Fact]
    public void APageFetchSatisfiesTheRequestItStartedAfter()
    {
        var queue = new ReadConfirmations();
        queue.Request(Thread, Start, ReadConfirmations.SettleDelay, Account);

        queue.Satisfy(Thread, queue.GenerationOf(Thread));

        Assert.False(queue.IsPending(Thread));
    }

    [Fact]
    public void APageFetchLeavesALaterRequestInPlace()
    {
        var queue = new ReadConfirmations();
        queue.Request(Thread, Start, ReadConfirmations.SettleDelay, Account);
        var seen = queue.GenerationOf(Thread);

        queue.Request(Thread, Start, ReadConfirmations.SettleDelay, Account);
        queue.Satisfy(Thread, seen);

        Assert.True(queue.IsPending(Thread));
    }

    [Fact]
    public void APageFetchWithNothingQueuedChangesNothing()
    {
        var queue = new ReadConfirmations();
        var seen = queue.GenerationOf(Thread);
        queue.Request(Thread, Start, ReadConfirmations.SettleDelay, Account);

        queue.Satisfy(Thread, seen);

        Assert.True(queue.IsPending(Thread));
    }

    [Fact]
    public void APageFetchLeavesAFlightToItsOwnOutcome()
    {
        var queue = new ReadConfirmations();
        queue.Request(Thread, Start, TimeSpan.Zero, Account);
        Assert.True(queue.TryTakeDue(Start, out var key, out var generation, out _));

        queue.Satisfy(Thread, generation);
        Assert.True(queue.IsPending(Thread));

        queue.Complete(key, generation, ReadMarkOutcome.Confirmed, Start);
        Assert.False(queue.IsPending(Thread));
    }

    [Fact]
    public void ResetClearsTheQueueAndTheMissingRouteFlag()
    {
        var queue = new ReadConfirmations();
        queue.Request(Thread, Start, TimeSpan.Zero, Account);
        Assert.True(queue.TryTakeDue(Start, out var key, out var generation, out _));
        queue.Complete(key, generation, ReadMarkOutcome.Unsupported, Start);

        queue.Reset();
        queue.Request(Thread, Start, TimeSpan.Zero, Account);

        Assert.False(queue.Unsupported);
        Assert.True(queue.TryTakeDue(Start, out _, out _, out _));
    }

    [Fact]
    public void ClassifySortsResponsesIntoOutcomes()
    {
        Assert.Equal(ReadMarkOutcome.Confirmed, ReadConfirmations.Classify(true, AepFailure.None, true));
        Assert.Equal(ReadMarkOutcome.Unsupported, ReadConfirmations.Classify(false, AepFailure.FromStatus(404, null), true));
        Assert.Equal(ReadMarkOutcome.Rejected, ReadConfirmations.Classify(false, AepFailure.FromStatus(404, null), false));
        Assert.Equal(ReadMarkOutcome.Rejected, ReadConfirmations.Classify(false, AepFailure.FromStatus(403, null), true));
        Assert.Equal(ReadMarkOutcome.Failed, ReadConfirmations.Classify(false, AepFailure.FromStatus(429, null), true));
        Assert.Equal(ReadMarkOutcome.Failed, ReadConfirmations.Classify(false, AepFailure.FromStatus(503, null), true));
        Assert.Equal(ReadMarkOutcome.Failed,
            ReadConfirmations.Classify(false, AepFailure.Transport(AepFailureKind.Offline), true));
    }
}
