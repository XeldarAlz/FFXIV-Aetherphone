using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Coins;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CoinGameSessionTrackerTests
{
    [Fact]
    public void APaidSessionAlwaysReports()
    {
        var paid = new CoinAwardDto(true, 5, 100);

        Assert.True(CoinGameSessionTracker.Reports(paid, true));
        Assert.True(CoinGameSessionTracker.Reports(paid, false));
    }

    [Fact]
    public void ATooShortSessionReportsOnlyWhileThePlayerIsWatching()
    {
        var tooShort = new CoinAwardDto(false, 0, 0, CoinGameSessionTracker.TooShortReason);

        Assert.True(CoinGameSessionTracker.Reports(tooShort, true));
        Assert.False(CoinGameSessionTracker.Reports(tooShort, false));
    }

    [Fact]
    public void OtherRefusalsStaySilent()
    {
        var capped = new CoinAwardDto(false, 0, 0, "daily_cap");

        Assert.False(CoinGameSessionTracker.Reports(capped, true));
        Assert.False(CoinGameSessionTracker.Reports(capped, false));
    }
}
