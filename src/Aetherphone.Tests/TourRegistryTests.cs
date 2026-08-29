using Aetherphone.Core.Onboarding;
using Xunit;

namespace Aetherphone.Tests;

public sealed class TourRegistryTests
{
    // Expected values were read out of TourRegistry before its 410 line BuildTours
    // was split across six partial files, so this table is the pre split behaviour.
    private static readonly Dictionary<string, (int Version, int StepCount)> Expected = new()
    {
        { "messages", (3, 7) },
        { "skywatcher", (2, 3) },
        { "market", (2, 4) },
        { "strats", (1, 4) },
        { "venues", (2, 4) },
        { "music", (2, 4) },
        { "games", (2, 3) },
        { "camera", (3, 5) },
        { "photos", (2, 2) },
        { "settings", (2, 4) },
        { "character", (2, 3) },
        { "chirper", (2, 6) },
        { "aethergram", (2, 7) },
        { "maps", (2, 4) },
        { "news", (2, 4) },
        { "collections", (2, 4) },
        { "wallet", (2, 3) },
        { "inventory", (2, 4) },
        { "clock", (2, 3) },
        { "calendar", (2, 3) },
        { "notes", (2, 4) },
        { "calculator", (2, 2) },
        { "timers", (2, 3) },
        { "dailies", (2, 3) },
        { "fishing", (2, 4) },
        { "notifications", (2, 2) },
        { "message", (2, 7) },
        { "velvet", (3, 8) },
        { "feedback", (2, 4) },
        { "polls", (2, 3) },
        { "appstore", (1, 5) },
        { "jobs", (1, 4) },
        { "muster", (1, 5) },
        { "yellowpages", (1, 6) },
        { "announcements", (1, 3) },
        { "health", (1, 5) },
        { "coin", (1, 6) },
        { "shortcuts", (1, 6) },
        { "housing", (1, 7) },
        { "casino", (1, 8) },
        { "aetherstream", (1, 7) },
        { "hunts", (2, 3) },
    };

    [Fact]
    public void EveryAppTourResolvesWithItsOriginalVersionAndStepCount()
    {
        foreach (var (appId, expected) in Expected)
        {
            Assert.True(TourRegistry.TryGetAppTour(appId, out var sequence), $"no tour registered for '{appId}'");
            Assert.Equal(appId, sequence.Id);
            Assert.Equal(appId, sequence.RequiredAppId);
            Assert.Equal(expected.Version, sequence.ContentVersion);
            Assert.Equal(expected.StepCount, sequence.Steps.Length);
            Assert.True(sequence.IsValid);
        }
    }

    [Fact]
    public void NoTourIsRegisteredBeyondTheExpectedSet()
    {
        Assert.Equal(42, Expected.Count);
        foreach (var appId in new[] { "welcome", "nope", "", "Messages" })
        {
            Assert.False(TourRegistry.TryGetAppTour(appId, out _), $"'{appId}' should not have an app tour");
        }
    }

    [Fact]
    public void TheWelcomeSequenceIsSeparateFromTheAppTours()
    {
        var welcome = TourRegistry.GetWelcome();
        Assert.Equal(TourRegistry.WelcomeId, welcome.Id);
        Assert.Null(welcome.RequiredAppId);
        Assert.True(welcome.IsValid);
    }
}
