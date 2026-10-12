using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Config;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoFameChoiceTests
{
    [Fact]
    public void TheWindowIsNeededForASignedInHiddenPlayerWhoHasNotAnswered()
    {
        var answered = Answered(out _);

        Assert.True(CasinoFameChoiceStore.ConsentNeeded(true, true, User("main", false), answered));
    }

    [Fact]
    public void TheWindowWaitsForTheFameFeatureAndASignedInPlayer()
    {
        var answered = Answered(out _);

        Assert.False(CasinoFameChoiceStore.ConsentNeeded(false, true, User("main", false), answered));
        Assert.False(CasinoFameChoiceStore.ConsentNeeded(true, false, User("main", false), answered));
        Assert.False(CasinoFameChoiceStore.ConsentNeeded(true, true, null, answered));
    }

    [Fact]
    public void APlayerAlreadyInTheHallOfFameIsNeverAsked()
    {
        var answered = Answered(out _);

        Assert.False(CasinoFameChoiceStore.ConsentNeeded(true, true, User("main", true), answered));
    }

    [Fact]
    public void TheWindowIsAskedOncePerAccount()
    {
        var answered = Answered(out var accounts);

        Assert.True(answered.Add("main"));

        Assert.Equal(new[] { "main" }, accounts);
        Assert.False(CasinoFameChoiceStore.ConsentNeeded(true, true, User("main", false), answered));
        Assert.True(CasinoFameChoiceStore.ConsentNeeded(true, true, User("alt", false), answered));
    }

    [Fact]
    public void TheGamesLeaderboardChoiceDoesNotAnswerTheHallOfFame()
    {
        var answered = Answered(out _);
        var user = User("main", false) with { ShowOnLeaderboards = true };

        Assert.True(CasinoFameChoiceStore.ConsentNeeded(true, true, user, answered));
    }

    [Fact]
    public void TheUserPayloadCarriesTheHallOfFameFlagAndDefaultsToHidden()
    {
        var shown = System.Text.Json.JsonSerializer.Deserialize("{\"id\":\"u1\",\"showInHallOfFame\":true}",
            AethernetJsonContext.Default.UserDto);
        Assert.NotNull(shown);
        Assert.True(shown.ShowInHallOfFame);
        Assert.False(shown.ShowOnLeaderboards);

        var legacy = System.Text.Json.JsonSerializer.Deserialize("{\"id\":\"u1\",\"showOnLeaderboards\":true}",
            AethernetJsonContext.Default.UserDto);
        Assert.NotNull(legacy);
        Assert.False(legacy.ShowInHallOfFame);
    }

    private static AnsweredAccounts Answered(out List<string> accounts)
    {
        var held = new List<string>();
        accounts = held;
        return new AnsweredAccounts(() => held, () => { });
    }

    private static UserDto User(string id, bool showInHallOfFame)
    {
        var user = System.Text.Json.JsonSerializer.Deserialize("{\"id\":\"" + id + "\"}",
            AethernetJsonContext.Default.UserDto);
        Assert.NotNull(user);
        return user with { ShowInHallOfFame = showInHallOfFame };
    }
}
