using Aetherphone.Apps.Casino.Cabinets;
using Aetherphone.Core.Aethernet.Contracts;
using Xunit;

namespace Aetherphone.Tests;

public sealed class WheelPodiumTests
{
    [Fact]
    public void PodiumsPrintEachSpotsReturnFromTheRules()
    {
        Assert.Equal(960, WheelCabinet.ReturnTenths(null, 0));
        Assert.Equal(960, WheelCabinet.ReturnTenths(null, 1));
        Assert.Equal(960, WheelCabinet.ReturnTenths(null, 2));
        Assert.Equal(960, WheelCabinet.ReturnTenths(null, 3));
        Assert.Equal(960, WheelCabinet.ReturnTenths(null, 4));
    }

    [Fact]
    public void ARoomThatShipsItsReturnWins()
    {
        var board = new CasinoWheelRoomStateDto(Spots: new[] { new CasinoWheelSpotDto(Spot: 4, ReturnBasisPoints: 9600) });
        Assert.Equal(960, WheelCabinet.ReturnTenths(board, 4));
        Assert.Equal(960, WheelCabinet.ReturnTenths(board, 0));
    }
}
