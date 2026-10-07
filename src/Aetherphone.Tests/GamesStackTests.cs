using Aetherphone.Apps.Games;
using Aetherphone.Core.Apps;
using Xunit;

namespace Aetherphone.Tests;

public sealed class GamesStackTests
{
    private static ViewRouter<GamesRoute> Stack(params GamesRoute[] pushed)
    {
        var router = new ViewRouter<GamesRoute>(GamesRoute.Root);
        for (var index = 0; index < pushed.Length; index++)
        {
            router.Push(pushed[index], false);
        }

        return router;
    }

    [Fact]
    public void TheHubAloneHoldsNoGame()
    {
        Assert.False(GamesStack.HoldsGame(Stack()));
        Assert.False(GamesStack.HoldsGame(Stack(GamesRoute.Bests)));
    }

    [Fact]
    public void AGameOnTopOrUnderItsLeaderboardStaysOpen()
    {
        Assert.True(GamesStack.HoldsGame(Stack(GamesRoute.Playing)));
        Assert.True(GamesStack.HoldsGame(Stack(GamesRoute.Playing, GamesRoute.LeaderboardOf("snake", "snake"))));
    }

    [Fact]
    public void LeavingAGameOpenedFromBestsOrALeaderboardReleasesIt()
    {
        var fromBests = Stack(GamesRoute.Bests, GamesRoute.Playing);
        var fromBoard = Stack(GamesRoute.LeaderboardOf("crater", "crater"), GamesRoute.Playing);

        fromBests.Pop(false);
        fromBoard.Pop(false);

        Assert.False(GamesStack.HoldsGame(fromBests));
        Assert.False(GamesStack.HoldsGame(fromBoard));
    }
}
