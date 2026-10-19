using System.Text.Json;
using Aetherphone.Apps.Prism;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PrismWireContractTests
{
    [Fact]
    public void RoutesMatchTheProposedEndpoints()
    {
        Assert.Equal("/prism/week?lang=en", PrismClient.WeekPath("en"));
        Assert.Equal("/prism/weeks/w_1/entry", PrismClient.EntryPath("w_1"));
        Assert.Equal("/prism/weeks/w_1/ballots", PrismClient.BallotsPath("w_1"));
        Assert.Equal("/prism/ballots/b%2F9", PrismClient.BallotPath("b/9"));
        Assert.Equal("/prism/weeks/w_1/results?lang=ja", PrismClient.ResultsPath("w_1", "ja"));
        Assert.Equal("prism", PrismClient.MediaScope);
        Assert.Equal("prism_look", PrismClient.ReportTarget);
    }

    [Fact]
    public void WeekDeserializesWithAnEntry()
    {
        const string json = "{\"id\":\"w_1\",\"title\":\"Ishgardian winter\",\"brief\":\"Furs\",\"phase\":\"entering\","
            + "\"entriesCloseAtUnix\":10,\"judgingCloseAtUnix\":20,\"finalCloseAtUnix\":30,\"resultsAtUnix\":40,"
            + "\"myEntry\":{\"id\":\"e_1\",\"imageUrl\":\"https://x/e.jpg\",\"width\":810,\"height\":1080,"
            + "\"status\":\"rejected\",\"rejectReason\":\"Not your character\"},\"ballotsCast\":2,"
            + "\"previousWeekId\":\"w_0\",\"issue\":43,\"entryCount\":214}";
        var week = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.PrismWeekDto);
        Assert.NotNull(week);
        Assert.Equal("entering", week.Phase);
        Assert.Equal(40, week.ResultsAtUnix);
        Assert.Equal(2, week.BallotsCast);
        Assert.Equal("w_0", week.PreviousWeekId);
        Assert.Equal(43, week.Issue);
        Assert.Equal(214, week.EntryCount);
        Assert.NotNull(week.MyEntry);
        Assert.Equal("rejected", week.MyEntry.Status);
        Assert.Equal("Not your character", week.MyEntry.RejectReason);
    }

    [Fact]
    public void SparseWeekFallsBackToDefaults()
    {
        var week = JsonSerializer.Deserialize("{\"id\":\"w_1\",\"phase\":\"judging\"}",
            AethernetJsonContext.Default.PrismWeekDto);
        Assert.NotNull(week);
        Assert.Null(week.MyEntry);
        Assert.Equal(string.Empty, week.Title);
        Assert.Equal(string.Empty, week.PreviousWeekId);
        Assert.Equal(0, week.BallotsCast);
        Assert.Equal(0, week.Issue);
        Assert.Equal(0, week.EntryCount);
        Assert.Null(week.JudgeReward);
        Assert.Equal(0, week.BallotCoins);
    }

    [Fact]
    public void WeekCarriesTheLiveJudgeRewardAndWhatTheBallotEarned()
    {
        const string json = "{\"id\":\"w_1\",\"phase\":\"judging\",\"ballotsCast\":1,"
            + "\"judgeReward\":{\"amount\":8,\"perWeek\":2,\"leftThisWeek\":1,\"minLooks\":3,\"minViewedMs\":600},"
            + "\"ballotCoins\":8}";
        var week = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.PrismWeekDto);

        Assert.NotNull(week);
        Assert.NotNull(week.JudgeReward);
        Assert.Equal(8, week.JudgeReward.Amount);
        Assert.Equal(2, week.JudgeReward.PerWeek);
        Assert.Equal(1, week.JudgeReward.LeftThisWeek);
        Assert.Equal(3, week.JudgeReward.MinLooks);
        Assert.Equal(600, week.JudgeReward.MinViewedMs);
        Assert.Equal(8, week.BallotCoins);
    }

    [Fact]
    public void AnUnlimitedRewardReadsAsEachHeat()
    {
        var week = JsonSerializer.Deserialize(
            "{\"id\":\"w_1\",\"judgeReward\":{\"amount\":5,\"perWeek\":0,\"leftThisWeek\":-1}}",
            AethernetJsonContext.Default.PrismWeekDto);

        Assert.NotNull(week);
        Assert.Equal(PrismRewardLine.Each, PrismRewards.LineFor(week.JudgeReward));
    }

    [Fact]
    public void TheRewardLineFollowsWhatTheServerSends()
    {
        Assert.Equal(PrismRewardLine.Hidden, PrismRewards.LineFor(null));
        Assert.Equal(PrismRewardLine.Hidden, PrismRewards.LineFor(new PrismJudgeRewardDto(0, 4, 4)));
        Assert.Equal(PrismRewardLine.Left, PrismRewards.LineFor(new PrismJudgeRewardDto(5, 4, 3)));
        Assert.Equal(PrismRewardLine.Spent, PrismRewards.LineFor(new PrismJudgeRewardDto(5, 4, 0)));
        Assert.Equal(PrismRewardLine.Each, PrismRewards.LineFor(new PrismJudgeRewardDto(5, 0, -1)));
    }

    [Theory]
    [InlineData("prism.judge")]
    [InlineData("prism.heat.first")]
    [InlineData("prism.heat.second")]
    [InlineData("prism.heat.third")]
    [InlineData("prism.final.first")]
    [InlineData("prism.final.second")]
    [InlineData("prism.final.third")]
    public void PrismPayoutsHaveTheirOwnCoinLabelAndHint(string ruleId)
    {
        Assert.NotEqual(L.Coin.RuleGeneric.Key, CoinRuleLabels.For(ruleId).Key);
        Assert.True(CoinRuleLabels.TryHint(ruleId, out _));
    }

    [Fact]
    public void RequestsSerializeTheProposedShape()
    {
        var entry = JsonSerializer.Serialize(new PrismEntryRequest("prism/a.jpg", 810, 1080),
            AethernetJsonContext.Default.PrismEntryRequest);
        Assert.Equal("{\"mediaKey\":\"prism/a.jpg\",\"width\":810,\"height\":1080}", entry);

        var ballot = JsonSerializer.Serialize(new PrismBallotRequest(new[] { new PrismStarDto("l_1", 4, 5200) }),
            AethernetJsonContext.Default.PrismBallotRequest);
        Assert.Equal("{\"stars\":[{\"lookId\":\"l_1\",\"stars\":4,\"viewedMs\":5200}]}", ballot);
    }

    [Fact]
    public void BallotAndResultsDeserialize()
    {
        var ballot = JsonSerializer.Deserialize("{\"id\":\"b_1\",\"stage\":\"final\",\"looks\":[{\"id\":\"l_1\","
            + "\"imageUrl\":\"https://x/l.jpg\",\"width\":810,\"height\":1080}],\"heatLabel\":\"B\"}",
            AethernetJsonContext.Default.PrismBallotDto);
        Assert.NotNull(ballot);
        Assert.Equal("final", ballot.Stage);
        Assert.Equal("B", ballot.HeatLabel);
        Assert.NotNull(ballot.Looks);
        Assert.Single(ballot.Looks);
        Assert.Equal("l_1", ballot.Looks[0].Id);

        var empty = JsonSerializer.Deserialize("{\"id\":\"\",\"stage\":\"heat\",\"looks\":[]}",
            AethernetJsonContext.Default.PrismBallotDto);
        Assert.NotNull(empty);
        Assert.NotNull(empty.Looks);
        Assert.Empty(empty.Looks);

        var results = JsonSerializer.Deserialize("{\"weekId\":\"w_0\",\"title\":\"Gold Saucer glam\","
            + "\"heatPodium\":[{\"rank\":1,\"imageUrl\":\"https://x/p.jpg\",\"displayName\":\"Ada Moon\","
            + "\"world\":\"Phoenix\",\"handle\":\"ada.moon\"}],\"finalPodium\":[],\"myHeatRank\":2,\"heatSize\":12,\"percentile\":15,"
            + "\"coinsAwarded\":150,\"issue\":42,\"entryCount\":214,\"myImageUrl\":\"https://x/me.jpg\","
            + "\"heatLabel\":\"B\"}", AethernetJsonContext.Default.PrismResultsDto);
        Assert.NotNull(results);
        Assert.NotNull(results.HeatPodium);
        Assert.Equal("Ada Moon", results.HeatPodium[0].DisplayName);
        Assert.Equal("ada.moon", results.HeatPodium[0].Handle);
        Assert.Equal(2, results.MyHeatRank);
        Assert.Equal(0, results.MyFinalRank);
        Assert.Equal(150, results.CoinsAwarded);
        Assert.Equal(42, results.Issue);
        Assert.Equal("https://x/me.jpg", results.MyImageUrl);
        Assert.Equal("B", results.HeatLabel);
    }
}
