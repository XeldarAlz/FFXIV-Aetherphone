using Aetherphone.Core.Wallet;
using Xunit;

namespace Aetherphone.Tests;

public sealed class WalletLogicTests
{
    private const uint Gil = 1;
    private const uint Poetics = 28;
    private const int Day = 740000;
    private const long Now = 1_790_000_000;

    [Fact]
    public void LevelIsNoneWithoutCap()
    {
        Assert.Equal(CapLevel.None, WalletMath.Level(500, 0));
    }

    [Theory]
    [InlineData(0, 2000, (int)CapLevel.Room)]
    [InlineData(1799, 2000, (int)CapLevel.Room)]
    [InlineData(1800, 2000, (int)CapLevel.Near)]
    [InlineData(1999, 2000, (int)CapLevel.Near)]
    [InlineData(2000, 2000, (int)CapLevel.Full)]
    [InlineData(2100, 2000, (int)CapLevel.Full)]
    public void LevelFollowsTheNinetyPercentLine(long amount, long cap, int expected)
    {
        Assert.Equal((CapLevel)expected, WalletMath.Level(amount, cap));
    }

    [Fact]
    public void FractionAndRemainingClamp()
    {
        Assert.Equal(1f, WalletMath.Fraction(5000, 4000));
        Assert.Equal(0f, WalletMath.Fraction(-5, 4000));
        Assert.Equal(0.5f, WalletMath.Fraction(2000, 4000));
        Assert.Equal(0, WalletMath.Remaining(5000, 4000));
        Assert.Equal(1500, WalletMath.Remaining(2500, 4000));
    }

    [Fact]
    public void FirstReadingIsABaselineNotAChange()
    {
        var history = new WalletHistory();
        var changed = WalletJournal.Apply(history, new[] { new WalletReading(Gil, 1000) }, Now, Day, 0);
        Assert.True(changed);
        Assert.Empty(history.Changes);
        Assert.Equal(1000, history.Last[Gil]);
        Assert.Single(history.Days);
    }

    [Fact]
    public void UnchangedReadingsDoNotDirtyTheHistory()
    {
        var history = new WalletHistory();
        WalletJournal.Apply(history, new[] { new WalletReading(Gil, 1000) }, Now, Day, 0);
        Assert.False(WalletJournal.Apply(history, new[] { new WalletReading(Gil, 1000) }, Now + 5, Day, 0));
    }

    [Fact]
    public void BurstsInOnePlaceCoalesce()
    {
        var history = new WalletHistory();
        WalletJournal.Apply(history, new[] { new WalletReading(Gil, 1000) }, Now, Day, 128);
        WalletJournal.Apply(history, new[] { new WalletReading(Gil, 1200) }, Now + 10, Day, 128);
        WalletJournal.Apply(history, new[] { new WalletReading(Gil, 1500) }, Now + 20, Day, 128);
        var change = Assert.Single(history.Changes);
        Assert.Equal(500, change.Delta);
        Assert.Equal(1500, change.Balance);
        Assert.Equal(Now + 20, change.Unix);
    }

    [Fact]
    public void SpendAfterGainStartsANewChange()
    {
        var history = new WalletHistory();
        WalletJournal.Apply(history, new[] { new WalletReading(Gil, 1000) }, Now, Day, 128);
        WalletJournal.Apply(history, new[] { new WalletReading(Gil, 1200) }, Now + 10, Day, 128);
        WalletJournal.Apply(history, new[] { new WalletReading(Gil, 900) }, Now + 20, Day, 128);
        Assert.Equal(2, history.Changes.Count);
        Assert.Equal(-300, history.Changes[1].Delta);
    }

    [Fact]
    public void InterleavedGainsAndSpendsKeepSeparateTotals()
    {
        var history = new WalletHistory();
        WalletJournal.Apply(history, new[] { new WalletReading(Gil, 1000) }, Now, Day, 128);
        WalletJournal.Apply(history, new[] { new WalletReading(Gil, 1200) }, Now + 10, Day, 128);
        WalletJournal.Apply(history, new[] { new WalletReading(Gil, 900) }, Now + 20, Day, 128);
        WalletJournal.Apply(history, new[] { new WalletReading(Gil, 1000) }, Now + 30, Day, 128);
        Assert.Equal(2, history.Changes.Count);
        Assert.Equal(-300, history.Changes[0].Delta);
        Assert.Equal(300, history.Changes[1].Delta);
        Assert.Equal(1000, history.Changes[1].Balance);
    }

    [Fact]
    public void ChangesOutsideTheWindowOrPlaceStaySeparate()
    {
        var history = new WalletHistory();
        WalletJournal.Apply(history, new[] { new WalletReading(Gil, 1000) }, Now, Day, 128);
        WalletJournal.Apply(history, new[] { new WalletReading(Gil, 1100) }, Now + 10, Day, 128);
        WalletJournal.Apply(history, new[] { new WalletReading(Gil, 1200) }, Now + 20, Day, 129);
        WalletJournal.Apply(history, new[] { new WalletReading(Gil, 1300) },
            Now + 20 + WalletJournal.CoalesceSeconds + 1, Day, 129);
        Assert.Equal(3, history.Changes.Count);
    }

    [Fact]
    public void NetSinceSumsOnlyTheItemAndWindow()
    {
        var history = new WalletHistory();
        WalletJournal.Apply(history, new[] { new WalletReading(Gil, 1000), new WalletReading(Poetics, 10) }, Now, Day,
            0);
        WalletJournal.Apply(history, new[] { new WalletReading(Gil, 1500), new WalletReading(Poetics, 60) }, Now + 10,
            Day, 0);
        WalletJournal.Apply(history, new[] { new WalletReading(Gil, 1200), new WalletReading(Poetics, 60) },
            Now + 1000, Day, 0);
        Assert.Equal(200, WalletJournal.NetSince(history, Gil, Now));
        Assert.Equal(-300, WalletJournal.NetSince(history, Gil, Now + 500));
        Assert.Equal(50, WalletJournal.NetSince(history, Poetics, Now));
    }

    [Fact]
    public void HistoryIsTrimmedToItsLimits()
    {
        var history = new WalletHistory();
        for (var index = 0; index <= WalletJournal.MaxChanges + 10; index++)
        {
            var amount = index % 2 == 0 ? 1000 : 500;
            WalletJournal.Apply(history, new[] { new WalletReading(Gil, amount) },
                Now + index * (WalletJournal.CoalesceSeconds + 1), Day + index, 0);
        }

        Assert.Equal(WalletJournal.MaxChanges, history.Changes.Count);
        Assert.Equal(WalletJournal.MaxDays, history.Days.Count);
    }

    [Fact]
    public void SeriesCarriesClosesForwardAndStartsAtTheFirstKnownDay()
    {
        var history = new WalletHistory();
        WalletJournal.Apply(history, new[] { new WalletReading(Gil, 100) }, Now, Day - 3, 0);
        WalletJournal.Apply(history, new[] { new WalletReading(Gil, 400) }, Now + 10, Day - 1, 0);
        Span<long> values = stackalloc long[30];
        var filled = WalletJournal.Series(history, Gil, Day, values);
        Assert.Equal(4, filled);
        Assert.Equal(new long[] { 100, 100, 400, 400 }, values[..filled].ToArray());
    }

    [Fact]
    public void SeriesUsesCloseFromBeforeTheWindow()
    {
        var history = new WalletHistory();
        WalletJournal.Apply(history, new[] { new WalletReading(Gil, 700) }, Now, Day - 40, 0);
        Span<long> values = stackalloc long[5];
        var filled = WalletJournal.Series(history, Gil, Day, values);
        Assert.Equal(5, filled);
        Assert.Equal(700, values[0]);
        Assert.Equal(700, values[4]);
    }

    [Fact]
    public void SeriesIsEmptyForUnknownItems()
    {
        var history = new WalletHistory();
        WalletJournal.Apply(history, new[] { new WalletReading(Gil, 700) }, Now, Day, 0);
        Span<long> values = stackalloc long[5];
        Assert.Equal(0, WalletJournal.Series(history, Poetics, Day, values));
    }
}
