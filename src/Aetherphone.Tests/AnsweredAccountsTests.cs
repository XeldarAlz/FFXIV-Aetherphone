using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Config;
using Aetherphone.Core.Games;
using Xunit;

namespace Aetherphone.Tests;

public sealed class AnsweredAccountsTests
{
    private sealed class FakeConsentConfiguration : IScoreUploadConfiguration
    {
        public List<PendingScoreUpload> PendingScoreUploads { get; } = new();
        public List<GameStatRecord> GameStats { get; } = new();
        public List<string> LeaderboardConsentAnswered { get; } = new();
        public List<string> HallOfFameConsentAnswered { get; } = new();
        public int Saves { get; private set; }

        public void Save()
        {
            Saves++;
        }
    }

    [Fact]
    public void AnAccountIsAnsweredOnceAndSavedOnce()
    {
        var accounts = new List<string>();
        var saves = 0;
        var answered = new AnsweredAccounts(() => accounts, () => saves++);

        Assert.False(answered.Contains("main"));
        Assert.True(answered.Add("main"));
        Assert.False(answered.Add("main"));
        Assert.True(answered.Add("alt"));

        Assert.Equal(new[] { "main", "alt" }, accounts);
        Assert.Equal(2, saves);
        Assert.True(answered.Contains("main"));
        Assert.True(answered.Contains("alt"));
        Assert.False(answered.Contains("third"));
    }

    [Fact]
    public void AnEmptyAccountIdIsNeverAnswered()
    {
        var accounts = new List<string>();
        var saves = 0;
        var answered = new AnsweredAccounts(() => accounts, () => saves++);

        Assert.False(answered.Add(string.Empty));
        Assert.False(answered.Contains(string.Empty));
        Assert.Empty(accounts);
        Assert.Equal(0, saves);
    }

    [Fact]
    public void AnsweredAccountsMatchCaseSensitively()
    {
        var accounts = new List<string> { "Main" };
        var answered = new AnsweredAccounts(() => accounts, () => { });

        Assert.True(answered.Contains("Main"));
        Assert.False(answered.Contains("main"));
    }

    [Fact]
    public void AReplacedListIsReadOnTheNextCall()
    {
        var accounts = new List<string> { "main" };
        var answered = new AnsweredAccounts(() => accounts, () => { });
        Assert.True(answered.Contains("main"));

        accounts = new List<string> { "alt" };

        Assert.False(answered.Contains("main"));
        Assert.True(answered.Contains("alt"));
        Assert.True(answered.Add("main"));
        Assert.Equal(new[] { "alt", "main" }, accounts);
    }

    [Fact]
    public void TheLeaderboardAndHallOfFameConsentsKeepSeparateLists()
    {
        var configuration = new FakeConsentConfiguration();
        var leaderboard = new LeaderboardConsent(configuration);
        var fame = new AnsweredAccounts(() => configuration.HallOfFameConsentAnswered, configuration.Save);

        Assert.True(leaderboard.Answer("main"));

        Assert.Equal(new[] { "main" }, configuration.LeaderboardConsentAnswered);
        Assert.Empty(configuration.HallOfFameConsentAnswered);
        Assert.False(fame.Contains("main"));
        Assert.True(CasinoFameChoiceStore.ConsentNeeded(true, true, User("main"), fame));

        Assert.True(fame.Add("alt"));

        Assert.Equal(new[] { "alt" }, configuration.HallOfFameConsentAnswered);
        Assert.False(leaderboard.HasAnswered("alt"));
        Assert.True(leaderboard.Needed(true, User("alt")));
        Assert.False(CasinoFameChoiceStore.ConsentNeeded(true, true, User("alt"), fame));
        Assert.Equal(2, configuration.Saves);
    }

    [Fact]
    public void BothConsentListsRoundTripThroughConfigurationSeparately()
    {
        const string json = "{\"LeaderboardConsentAnswered\":[\"main\"],\"HallOfFameConsentAnswered\":[\"alt\"]}";

        var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<Configuration>(json);

        Assert.NotNull(loaded);
        var leaderboard = new LeaderboardConsent(loaded);
        var fame = CasinoFameChoiceStore.AnsweredFor(loaded);
        Assert.True(leaderboard.HasAnswered("main"));
        Assert.False(leaderboard.HasAnswered("alt"));
        Assert.True(fame.Contains("alt"));
        Assert.False(fame.Contains("main"));
    }

    private static UserDto User(string id)
    {
        var user = System.Text.Json.JsonSerializer.Deserialize("{\"id\":\"" + id + "\"}",
            AethernetJsonContext.Default.UserDto);
        Assert.NotNull(user);
        return user;
    }
}
