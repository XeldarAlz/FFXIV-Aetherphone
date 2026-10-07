using Aetherphone.Apps.Games;
using Aetherphone.Core.Changelog;
using Xunit;

namespace Aetherphone.Tests;

public sealed class WhatsNewNoticeTests
{
    private const string PreviousGamesPin = "app.games.1100";

    [Fact]
    public void ThePinKeysAreNewForThisUpdate()
    {
        Assert.Equal("app.games.1200", NewFeaturePins.Games);
        Assert.Equal("games.whatsnew.1200", NewFeaturePins.GamesWhatsNew);
        Assert.NotEqual(PreviousGamesPin, NewFeaturePins.Games);
        Assert.NotEqual(NewFeaturePins.Games, NewFeaturePins.GamesWhatsNew);
        Assert.NotEqual(NewFeaturePins.Music, NewFeaturePins.GamesWhatsNew);
        Assert.NotEqual(NewFeaturePins.Nameplate, NewFeaturePins.GamesWhatsNew);
    }

    [Fact]
    public void APlayerWhoOpenedGamesBeforeSeesTheDotAndTheCardAgain()
    {
        var configuration = new Configuration();
        configuration.SeenFeaturePins.Add(PreviousGamesPin);

        Assert.True(configuration.HasUnseenFeaturePin(NewFeaturePins.Games));
        Assert.True(WhatsNewNotice.Shows(configuration, false));
    }

    [Fact]
    public void SeeingThePinHidesTheCardForGood()
    {
        var configuration = new Configuration();
        configuration.SeenFeaturePins.Add(NewFeaturePins.Games);

        Assert.True(WhatsNewNotice.Shows(configuration, false));

        configuration.SeenFeaturePins.Add(NewFeaturePins.GamesWhatsNew);

        Assert.False(WhatsNewNotice.Shows(configuration, false));
        Assert.False(WhatsNewNotice.Shows(configuration, true));
    }

    [Fact]
    public void TheCardWaitsUntilTheConsentCardIsAnswered()
    {
        var configuration = new Configuration();

        Assert.False(WhatsNewNotice.Shows(configuration, true));
        Assert.True(WhatsNewNotice.Shows(configuration, false));
    }

    [Fact]
    public void TheJoinCardStepsAsideWhileTheCardShows()
    {
        Assert.False(WhatsNewNotice.ShowsJoinCard(true, true));
        Assert.True(WhatsNewNotice.ShowsJoinCard(true, false));
        Assert.False(WhatsNewNotice.ShowsJoinCard(false, false));
    }
}
