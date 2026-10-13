using Aetherphone.Core.Coins;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CoinDayTests
{
    private static DateTime At(int day, int hour, int minute, int second) =>
        new(2026, 10, day, hour, minute, second, DateTimeKind.Utc);

    [Fact]
    public void TheDayTurnsOverAtFifteenUtc()
    {
        var before = CoinDay.Index(At(7, 14, 59, 59));
        var after = CoinDay.Index(At(7, 15, 0, 0));

        Assert.Equal(before + 1, after);
    }

    [Fact]
    public void TheHoursBeforeTheResetBelongToThePreviousDay()
    {
        var start = CoinDay.Index(At(7, 15, 0, 0));

        Assert.Equal(start, CoinDay.Index(At(7, 23, 59, 59)));
        Assert.Equal(start, CoinDay.Index(At(8, 14, 59, 59)));
        Assert.Equal(start + 1, CoinDay.Index(At(8, 15, 0, 0)));
    }
}
