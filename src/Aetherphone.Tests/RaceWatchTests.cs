using Aetherphone.Core.Game;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RaceWatchTests
{
    private const byte HyurRace = 1;
    private const byte Unpopulated = 0;
    private const int CustomizeLength = 26;

    [Fact]
    public void StartsUnknown_SoNothingIsReportedBeforeACharacterLoads()
    {
        var watch = new RaceWatch();

        Assert.Null(watch.Race);
        Assert.Null(watch.IsLalafell);
    }

    [Fact]
    public void Observe_RecordsTheRaceItRead()
    {
        var watch = new RaceWatch();

        watch.Observe(Customize(HyurRace));

        Assert.Equal(HyurRace, watch.Race);
    }

    [Fact]
    public void Observe_RecordsALalafell()
    {
        var watch = new RaceWatch();

        watch.Observe(Customize(RaceWatch.LalafellRaceId));

        Assert.Equal(RaceWatch.LalafellRaceId, watch.Race);
        Assert.True(watch.IsLalafell);
    }

    [Fact]
    public void Observe_RecordsANonLalafell()
    {
        var watch = new RaceWatch();

        watch.Observe(Customize(HyurRace));

        Assert.False(watch.IsLalafell);
    }

    [Fact]
    public void Observe_LeavesTheAnswerUnknownWhenThereIsNoCustomizeData()
    {
        var watch = new RaceWatch();

        watch.Observe([]);

        Assert.Null(watch.Race);
        Assert.Null(watch.IsLalafell);
    }

    [Fact]
    public void Observe_TreatsAnUnpopulatedRaceByteAsNoAnswer()
    {
        var watch = new RaceWatch();

        watch.Observe(Customize(Unpopulated));

        Assert.Null(watch.Race);
        Assert.Null(watch.IsLalafell);
    }

    [Fact]
    public void AnUnpopulatedReadDoesNotOverwriteAKnownAnswer()
    {
        var watch = new RaceWatch();
        watch.Observe(Customize(RaceWatch.LalafellRaceId));

        watch.Observe(Customize(Unpopulated));

        Assert.True(watch.IsLalafell);
    }

    [Fact]
    public void AFailedReadDoesNotEraseAnAnswerWeAlreadyHave()
    {
        var watch = new RaceWatch();
        watch.Observe(Customize(RaceWatch.LalafellRaceId));

        watch.Observe([]);

        Assert.True(watch.IsLalafell);
    }

    [Fact]
    public void Observe_TracksARaceChangeRatherThanLatchingTheFirstRead()
    {
        var watch = new RaceWatch();
        watch.Observe(Customize(HyurRace));

        watch.Observe(Customize(RaceWatch.LalafellRaceId));

        Assert.True(watch.IsLalafell);
    }

    [Fact]
    public void Forget_ReturnsToUnknownWhenTheCharacterGoesAway()
    {
        var watch = new RaceWatch();
        watch.Observe(Customize(RaceWatch.LalafellRaceId));

        watch.Forget();

        Assert.Null(watch.Race);
        Assert.Null(watch.IsLalafell);
    }

    [Fact]
    public void ObserveAfterForget_DoesNotKeepThePreviousCharactersAnswer()
    {
        var watch = new RaceWatch();
        watch.Observe(Customize(RaceWatch.LalafellRaceId));
        watch.Forget();

        watch.Observe(Customize(HyurRace));

        Assert.False(watch.IsLalafell);
    }

    private static byte[] Customize(byte race)
    {
        var customize = new byte[CustomizeLength];
        customize[RaceWatch.RaceIndex] = race;
        return customize;
    }
}
