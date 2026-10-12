using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core.Aethernet.Contracts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BlackjackHandLogTests
{
    [Fact]
    public void TheLogKeepsTheTenNewestHandsNewestFirst()
    {
        var log = new BlackjackHandLog();
        log.Follow("table-a");
        for (var handIndex = 1; handIndex <= 13; handIndex++)
        {
            Record(log, handIndex);
        }

        Assert.Equal(BlackjackHandLog.Capacity, log.Count);
        Assert.Equal(13, log.Newest(0).HandIndex);
        Assert.Equal(4, log.Newest(BlackjackHandLog.Capacity - 1).HandIndex);
    }

    [Fact]
    public void AHandSettledTwiceIsRecordedOnce()
    {
        var log = new BlackjackHandLog();
        log.Follow("table-a");
        Record(log, 7);
        Record(log, 7);

        Assert.Equal(1, log.Count);
    }

    [Fact]
    public void AnotherTableStartsAnEmptyLogAndTheSameTableKeepsIt()
    {
        var log = new BlackjackHandLog();
        log.Follow("table-a");
        Record(log, 1);
        log.Follow("table-a");
        Assert.Equal(1, log.Count);

        log.Follow("table-b");
        Assert.Equal(0, log.Count);
    }

    private static void Record(BlackjackHandLog log, long handIndex)
    {
        log.Record(handIndex, 1_700_000_000, 100, Array.Empty<CasinoBlackjackHandDto>(), Array.Empty<int>(), 18,
            default);
    }
}
