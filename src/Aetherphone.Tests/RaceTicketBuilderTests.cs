using Aetherphone.Apps.Casino.Race;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RaceTicketBuilderTests
{
    [Fact]
    public void TheBuilderPicksOneRunnerForSinglesAndTwoForPairs()
    {
        var builder = new RaceTicketBuilder();
        builder.Follow(4);
        Assert.False(builder.Ready);
        builder.Tap(3);
        Assert.True(builder.Ready);
        Assert.Equal(RaceRules.NoRunner, builder.RunnerB);
        builder.Tap(5);
        Assert.Equal(5, builder.First);
        builder.SetKind(RaceRules.KindForecast);
        Assert.True(builder.WantsSecond);
        Assert.False(builder.Ready);
        builder.Tap(2);
        Assert.True(builder.Ready);
        Assert.Equal(2, builder.RunnerB);
        Assert.Equal(0, builder.PickOf(5));
        Assert.Equal(1, builder.PickOf(2));
        builder.Tap(5);
        Assert.Equal(2, builder.First);
        Assert.Equal(RaceRules.NoRunner, builder.Second);
        builder.SetKind(RaceRules.KindPlace);
        Assert.True(builder.Ready);
        builder.Follow(5);
        Assert.False(builder.Ready);
        Assert.Equal(RaceRules.KindPlace, builder.Kind);
    }

    [Fact]
    public void LettingItRideFloorsThePayoutToTheLadderAndTheCeiling()
    {
        Assert.Equal(10_000, RaceTicketBuilder.RideAmount(13_500, 1_000_000, 1_000_000));
        Assert.Equal(5_000, RaceTicketBuilder.RideAmount(13_500, 7_500, 1_000_000));
        Assert.Equal(2_500, RaceTicketBuilder.RideAmount(13_500, 1_000_000, 3_000));
        Assert.Equal(0, RaceTicketBuilder.RideAmount(50, 1_000_000, 1_000_000));
        Assert.True(CasinoLadder.IsRung(RaceTicketBuilder.RideAmount(987_654, 10_000_000, 10_000_000)));
    }
}
