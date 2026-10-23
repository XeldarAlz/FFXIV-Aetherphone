using System.Text.Json;
using Aetherphone.Apps.Prism;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Coins;
using Aetherphone.Core.Game;
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
            + "\"previousWeekId\":\"w_0\",\"issue\":43,\"entryCount\":214,\"resultsRevision\":2}";
        var week = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.PrismWeekDto);
        Assert.NotNull(week);
        Assert.Equal("entering", week.Phase);
        Assert.Equal(40, week.ResultsAtUnix);
        Assert.Equal(2, week.BallotsCast);
        Assert.Equal("w_0", week.PreviousWeekId);
        Assert.Equal(43, week.Issue);
        Assert.Equal(214, week.EntryCount);
        Assert.Equal(2, week.ResultsRevision);
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
    [InlineData("prism.gil.cn")]
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
            + "\"heatLabel\":\"B\",\"revision\":2}", AethernetJsonContext.Default.PrismResultsDto);
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
        Assert.Equal(2, results.Revision);
    }

    [Fact]
    public void ServerRevisionsAreMillisecondsAndStillParse()
    {
        var week = JsonSerializer.Deserialize("{\"id\":\"2026-W43\",\"phase\":\"results\",\"previousWeekId\":\"2026-W43\","
            + "\"resultsRevision\":1792440123456,\"finalistCount\":6,\"prizes\":{\"heat\":[30,20,10],"
            + "\"final\":[2000,1500,1000],\"minHeatSize\":4}}", AethernetJsonContext.Default.PrismWeekDto);
        Assert.NotNull(week);
        Assert.Equal(1792440123456L, week.ResultsRevision);
        Assert.Equal(6, week.FinalistCount);
        Assert.NotNull(week.Prizes);
        Assert.Equal(new long[] { 2000, 1500, 1000 }, week.Prizes.Final);
        Assert.Equal(4, week.Prizes.MinHeatSize);

        var results = JsonSerializer.Deserialize("{\"weekId\":\"2026-W43\",\"revision\":1792440123456,\"finalistCount\":6}",
            AethernetJsonContext.Default.PrismResultsDto);
        Assert.NotNull(results);
        Assert.Equal(1792440123456L, results.Revision);
        Assert.Null(results.Prizes);
    }

    [Fact]
    public void PlacingsCarryBadgesFramesAndGilAheadOfTheCoinArrays()
    {
        var week = JsonSerializer.Deserialize("{\"id\":\"w_1\",\"prizes\":{\"heat\":[30],\"final\":[2000,1500],"
            + "\"gilNote\":\"Paid within a week\",\"placings\":[{\"stage\":\"final\",\"rank\":2,\"coins\":1500},"
            + "{\"stage\":\"final\",\"rank\":1,\"coins\":2000,\"gil\":500000,\"frameDays\":7,"
            + "\"badge\":{\"id\":\"prism-gold\",\"name\":\"Cover star\",\"icon\":\"f005\"},"
            + "\"frame\":{\"id\":\"prism-frame\",\"name\":\"Spectrum\",\"assetUrl\":\"https://x/f.png\"}}]}}",
            AethernetJsonContext.Default.PrismWeekDto);
        Assert.NotNull(week?.Prizes);
        Assert.Equal("Paid within a week", week.Prizes.GilNote);

        var final = new List<PrismPrizeRow>();
        PrismRewards.Fill(final, week.Prizes, PrismPhases.StageFinal);
        Assert.Equal(2, final.Count);
        Assert.Equal(1, final[0].Rank);
        Assert.Equal(500000, final[0].Gil);
        Assert.Equal(7, final[0].FrameDays);
        Assert.Equal("prism-gold", final[0].Badge?.Id);
        Assert.Equal("Spectrum", final[0].Frame?.BaseName);
        Assert.Null(final[1].Badge);

        var heat = new List<PrismPrizeRow>();
        PrismRewards.Fill(heat, week.Prizes, PrismPhases.StageHeat);
        Assert.Single(heat);
        Assert.Equal(30, heat[0].Coins);
    }

    [Fact]
    public void RewardsOnlyListTheKindsTheWeekPaysOut()
    {
        var kinds = new PrismRewardKind[PrismRewards.KindCount];
        var coinsOnly = JsonSerializer.Deserialize("{\"id\":\"w_1\",\"prizes\":{\"heat\":[30,20,10],"
            + "\"final\":[2000,1500,1000],\"placings\":[{\"stage\":\"final\",\"rank\":1,\"coins\":2000,\"gil\":0,"
            + "\"badge\":null,\"frame\":null}]}}", AethernetJsonContext.Default.PrismWeekDto);
        Assert.Equal(1, PrismRewards.Offered(coinsOnly?.Prizes, null, kinds));
        Assert.Equal(PrismRewardKind.Coin, kinds[0]);

        var everything = JsonSerializer.Deserialize("{\"id\":\"w_2\",\"prizes\":{\"placings\":[{\"stage\":\"final\","
            + "\"rank\":1,\"gil\":500000,\"badge\":{\"id\":\"prism-gold\",\"name\":\"Cover star\",\"icon\":\"f005\"},"
            + "\"frame\":{\"id\":\"prism-frame\",\"name\":\"Spectrum\",\"assetUrl\":\"https://x/f.png\"}}]}}",
            AethernetJsonContext.Default.PrismWeekDto);
        Assert.Equal(3, PrismRewards.Offered(everything?.Prizes, null, kinds));
        Assert.Equal(new[] { PrismRewardKind.Badge, PrismRewardKind.Frame, PrismRewardKind.Gil }, kinds[..3]);

        Assert.Equal(0, PrismRewards.Offered(null, null, kinds));
        Assert.Equal(1, PrismRewards.Offered(null, new PrismJudgeRewardDto(5, 4, 4), kinds));
        Assert.Equal(PrismRewardKind.Coin, kinds[0]);
    }

    [Fact]
    public void NoPrizesMeansNoPrizeRows()
    {
        var rows = new List<PrismPrizeRow> { new(1, 5) };
        PrismRewards.Fill(rows, null, PrismPhases.StageFinal);
        Assert.Empty(rows);
    }

    [Fact]
    public void AnEntryCarriesTheBodyItWasMadeOn()
    {
        var made = JsonSerializer.Serialize(new PrismEntryRequest("k", 810, 1080, Race: 6, Gender: 1),
            AethernetJsonContext.Default.PrismEntryRequest);
        Assert.Contains("\"race\":6", made);
        Assert.Contains("\"gender\":1", made);

        var male = JsonSerializer.Serialize(new PrismEntryRequest("k", 810, 1080, Race: 1, Gender: 0),
            AethernetJsonContext.Default.PrismEntryRequest);
        Assert.Contains("\"gender\":0", male);

        var unknown = JsonSerializer.Serialize(new PrismEntryRequest("k", 810, 1080),
            AethernetJsonContext.Default.PrismEntryRequest);
        Assert.DoesNotContain("race", unknown);
        Assert.DoesNotContain("gender", unknown);
    }

    [Fact]
    public void AnOutfitRidesAlongOnlyWhenShared()
    {
        var bare = JsonSerializer.Serialize(new PrismEntryRequest("k", 810, 1080),
            AethernetJsonContext.Default.PrismEntryRequest);
        Assert.DoesNotContain("outfit", bare);

        var shared = JsonSerializer.Serialize(new PrismEntryRequest("k", 810, 1080,
            Outfit: new[] { new GlamGearDto(2, 45123, 1, 101) }), AethernetJsonContext.Default.PrismEntryRequest);
        Assert.Contains("\"outfit\":[{\"slot\":2,\"itemId\":45123,\"stain0\":1,\"stain1\":101}]", shared);

        var cleared = JsonSerializer.Serialize(new PrismOutfitRequest(null),
            AethernetJsonContext.Default.PrismOutfitRequest);
        Assert.Equal("{\"outfit\":null}", cleared);
        Assert.Equal("/prism/weeks/w%2F1/entry/outfit", PrismClient.OutfitPath("w/1"));
    }

    [Fact]
    public void PlacingsAndEntriesCarryTheSharedOutfit()
    {
        var results = JsonSerializer.Deserialize("{\"weekId\":\"w_1\",\"finalPodium\":[{\"rank\":1,\"glamId\":\"g_9\","
            + "\"outfit\":[{\"slot\":0,\"itemId\":40001},{\"slot\":3,\"itemId\":45123,\"stain0\":12}]}]}",
            AethernetJsonContext.Default.PrismResultsDto);
        var winner = results?.FinalPodium?[0];
        Assert.Equal("g_9", winner?.GlamId);
        var outfit = winner?.Outfit;
        Assert.Equal(2, outfit?.Length);
        Assert.Equal(45123u, outfit![1].ItemId);
        Assert.Equal(12, outfit[1].Stain0);

        var entry = JsonSerializer.Deserialize("{\"id\":\"e_1\"}", AethernetJsonContext.Default.PrismEntryDto);
        Assert.Null(entry?.Outfit);
    }

    [Fact]
    public void TrackRoutesAddTheTrackOnlyWhenThereIsOne()
    {
        Assert.Equal("/prism/weeks/w_1/entry?track=modded", PrismClient.EntryPath("w_1", "modded"));
        Assert.Equal("/prism/weeks/w_1/ballots?track=vanilla", PrismClient.BallotsPath("w_1", "vanilla"));
        Assert.Equal("/prism/weeks/w_1/results?lang=ja&track=modded", PrismClient.ResultsPath("w_1", "ja", "modded"));
        Assert.Equal("/prism/weeks/w_1/ballots", PrismClient.BallotsPath("w_1", string.Empty));
        Assert.DoesNotContain("track", JsonSerializer.Serialize(new PrismEntryRequest("k", 810, 1080),
            AethernetJsonContext.Default.PrismEntryRequest));
        Assert.Contains("\"track\":\"modded\"", JsonSerializer.Serialize(new PrismEntryRequest("k", 810, 1080, "modded"),
            AethernetJsonContext.Default.PrismEntryRequest));
    }

    [Fact]
    public void TheWeekReadsAsTheChosenTrack()
    {
        var week = JsonSerializer.Deserialize("{\"id\":\"w_1\",\"phase\":\"entering\",\"entryCount\":120,"
            + "\"tracks\":[{\"id\":\"vanilla\",\"entryCount\":120},"
            + "{\"id\":\"modded\",\"entryCount\":84,\"ballotsCast\":3,\"myEntry\":{\"id\":\"e_2\",\"track\":\"modded\"}}]}",
            AethernetJsonContext.Default.PrismWeekDto)!;
        Assert.Equal("modded", PrismTracks.Choose(week, string.Empty));
        Assert.Equal("vanilla", PrismTracks.Choose(week, "vanilla"));
        Assert.Equal("modded", PrismTracks.Choose(week, "gone"));

        var modded = PrismTracks.Project(week, "modded");
        Assert.Equal(84, modded.EntryCount);
        Assert.Equal(3, modded.BallotsCast);
        Assert.Equal("e_2", modded.MyEntry?.Id);
        Assert.Null(PrismTracks.Project(week, "vanilla").MyEntry);

        var entered = PrismTracks.WithEntry(week, "vanilla", new PrismEntryDto(Id: "e_1"));
        Assert.Equal("e_1", PrismTracks.Project(entered, "vanilla").MyEntry?.Id);
        Assert.Equal("e_2", PrismTracks.Project(entered, "modded").MyEntry?.Id);
        Assert.Null(PrismTracks.Project(PrismTracks.WithEntry(entered, "modded", null), "modded").MyEntry);
    }

    [Fact]
    public void AWeekWithoutTracksStaysAsItIs()
    {
        var week = new PrismWeekDto(Id: "w_1", EntryCount: 9);
        Assert.Equal(string.Empty, PrismTracks.Choose(week, "modded"));
        Assert.Same(week, PrismTracks.Project(week, string.Empty));
        Assert.Equal("e_1", PrismTracks.WithEntry(week, string.Empty, new PrismEntryDto(Id: "e_1")).MyEntry?.Id);
    }

    [Fact]
    public void ModdedIsTheDefaultTrack()
    {
        var week = new PrismWeekDto(Id: "w_2", Tracks: new[] { new PrismTrackDto("vanilla"), new PrismTrackDto("modded") });
        Assert.Equal("modded", PrismTracks.Choose(week, string.Empty));
    }

    [Fact]
    public void AReplacedRoundWithTheSameWeekIdDropsTheHeldResults()
    {
        var held = new PrismResultsDto(WeekId: "2026-W43", Revision: 1000);
        var replaced = new PrismWeekDto(Id: "2026-W43", PreviousWeekId: "2026-W43", ResultsRevision: 2000);
        var same = replaced with { ResultsRevision = 1000 };
        var older = new PrismWeekDto(Id: "2026-W43", PreviousWeekId: "2026-W42");

        Assert.False(PrismStore.StillCurrent(held, replaced));
        Assert.True(PrismStore.StillCurrent(held, same));
        Assert.False(PrismStore.StillCurrent(held, older));
        Assert.True(PrismStore.StillCurrent(new PrismResultsDto(WeekId: "2026-W42"), older));
    }
}
