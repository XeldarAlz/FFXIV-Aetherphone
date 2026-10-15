using Aetherphone.Core.Coins;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CoinGamesTests
{
    [Fact]
    public void TheFeaturedGameWalksTheListByDay()
    {
        Assert.Equal(CoinGames.All[0], CoinGames.FeaturedFor(0));
        Assert.Equal(CoinGames.All[1], CoinGames.FeaturedFor(1));
        Assert.Equal(CoinGames.All[0], CoinGames.FeaturedFor(CoinGames.All.Length));
    }

    [Fact]
    public void ANegativeDayWrapsFromTheEnd()
    {
        Assert.Equal(CoinGames.All[^1], CoinGames.FeaturedFor(-1));
    }
}
