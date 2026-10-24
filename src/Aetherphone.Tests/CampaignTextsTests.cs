using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Campaigns;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CampaignTextsTests
{
    private static readonly string[] ContractKeys =
    {
        "tabTonight", "tabDjs", "tabExperiences", "tabGallery", "tabPerks", "doorsOpenIn", "liveNow", "betweenSets",
        "djsSub", "noDjs",
        "endedTitle", "endedBody", "keepNight", "keepNightHintLive", "keepNightHintBefore", "liveDj", "noStream",
        "streamHint", "watchTwitch", "shareTitle", "shareBody", "websiteNote", "postCta", "galleryCta", "pinned",
        "announcements",
        "announcementsFrom", "announcementsBody", "syncshellsTitle", "syncshellsBody", "about", "more",
        "aboutFooter", "experiencesSub", "nothingScheduled", "whoIsOn", "remindMe", "dcTravelNote", "gallerySub",
        "galleryCta2", "perksSub", "frameName", "frameBody", "badgeName", "badgeBody", "coinsName", "coinsBody",
        "howToEarn", "earnTitle", "earnBody", "equipCta", "earned", "notYet",
    };

    [Fact]
    public void EveryContractKeyHasAnEnglishDefault()
    {
        var texts = new CampaignTexts();
        for (var index = 0; index < ContractKeys.Length; index++)
        {
            Assert.False(string.IsNullOrWhiteSpace(texts.T(ContractKeys[index])), ContractKeys[index]);
        }
    }

    [Fact]
    public void WithoutServerTextTheDefaultIsUsed()
    {
        var texts = new CampaignTexts();
        texts.Sync(Array.Empty<CampaignTextDto>());

        Assert.Equal("Tonight", texts.T(CampaignTextKeys.TabTonight));
        Assert.Equal("Doors open in", texts.T(CampaignTextKeys.DoorsOpenIn));
    }

    [Fact]
    public void AServerValueOverridesTheDefault()
    {
        var texts = new CampaignTexts();
        texts.Sync(new[] { new CampaignTextDto(CampaignTextKeys.TabTonight, "This evening") });

        Assert.Equal("This evening", texts.T(CampaignTextKeys.TabTonight));
        Assert.Equal("Experiences", texts.T(CampaignTextKeys.TabExperiences));
    }

    [Fact]
    public void AnEmptyServerValueFallsBackToTheDefault()
    {
        var texts = new CampaignTexts();
        texts.Sync(new[]
        {
            new CampaignTextDto(CampaignTextKeys.TabTonight, string.Empty),
            new CampaignTextDto(CampaignTextKeys.TabPerks, "   "),
        });

        Assert.Equal("Tonight", texts.T(CampaignTextKeys.TabTonight));
        Assert.Equal("Perks", texts.T(CampaignTextKeys.TabPerks));
    }

    [Fact]
    public void ANewSyncReplacesTheOldOverrides()
    {
        var texts = new CampaignTexts();
        texts.Sync(new[] { new CampaignTextDto(CampaignTextKeys.More, "Read more") });
        var version = texts.Version;
        texts.Sync(Array.Empty<CampaignTextDto>());

        Assert.Equal("More", texts.T(CampaignTextKeys.More));
        Assert.True(texts.Version > version);
    }

    [Fact]
    public void DefaultPlaceholdersAreFilled()
    {
        var texts = new CampaignTexts();

        Assert.Equal("12 from PNX", texts.T(CampaignTextKeys.AnnouncementsFrom, new CampaignTextArgs(N: "12")));
        Assert.Equal("Post once with #LoveIslandEclipse",
            texts.T(CampaignTextKeys.EarnTitle, new CampaignTextArgs(Hashtag: "LoveIslandEclipse")));
        Assert.Equal(
            "On Aethergram or Chirper, any time between Sat 8:00 and Sun 13:00 (your time). They arrive in your inventory within a few minutes.",
            texts.T(CampaignTextKeys.EarnBody, new CampaignTextArgs(Start: "Sat 8:00", End: "Sun 13:00")));
    }

    [Fact]
    public void ServerPlaceholdersAreFilledByName()
    {
        var texts = new CampaignTexts();
        texts.Sync(new[]
        {
            new CampaignTextDto(CampaignTextKeys.EarnBody, "From {start} until {end}, tag #{hashtag}"),
            new CampaignTextDto(CampaignTextKeys.CoinsBody, "{n} coins for {name}"),
        });

        Assert.Equal("From A until B, tag #Eclipse",
            texts.T(CampaignTextKeys.EarnBody, new CampaignTextArgs(Hashtag: "Eclipse", Start: "A", End: "B")));
        Assert.Equal("500 coins for Masha",
            texts.T(CampaignTextKeys.CoinsBody, new CampaignTextArgs(N: "500", Name: "Masha")));
    }

    [Fact]
    public void AMissingArgumentLeavesItsPlaceholderAlone()
    {
        Assert.Equal("Hi {name}", CampaignTexts.Fill("Hi {name}", new CampaignTextArgs(N: "3")));
        Assert.Equal("No braces", CampaignTexts.Fill("No braces", new CampaignTextArgs(N: "3")));
    }
}
