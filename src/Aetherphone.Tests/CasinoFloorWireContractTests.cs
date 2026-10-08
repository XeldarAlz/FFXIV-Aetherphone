using System.Text.Json;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Clients;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoFloorWireContractTests
{
    [Fact]
    public void FloorRoutesMatchTheBackend()
    {
        Assert.Equal("/casino/missions", CasinoClient.MissionsPath);
        Assert.Equal("/casino/missions/spin-25/claim", CasinoClient.MissionClaimPath("spin-25"));
        Assert.Equal("/casino/challenges", CasinoClient.ChallengesPath);
        Assert.Equal("/casino/fame?board=profit&span=last&limit=50",
            CasinoClient.FameBoardPath(CasinoFameBoards.Profit, CasinoFameSpans.Last, CasinoFloorRules.FameDefaultLimit));
        Assert.Equal("/casino/feed?tab=high", CasinoClient.FeedTabPath(CasinoFeedTabs.High));
    }

    [Fact]
    public void FloorConstantsMirrorTheBackend()
    {
        Assert.Equal("casino-floor", CasinoFloorRules.FloorRoomId);
        Assert.Equal("casino.floor", CasinoFloorRules.FloorKind);
        Assert.Equal(20, CasinoFloorRules.TickerLength);
        Assert.Equal(50, CasinoFloorRules.FeedLength);
        Assert.Equal(1000, CasinoFloorRules.BigWinTenths);
        Assert.Equal(100_000, CasinoFloorRules.HighRollerStake);
        Assert.Equal(100, CasinoFloorRules.FameMaxLimit);
        Assert.Equal(new[] { "profit", "multiplier", "win", "poker" }, CasinoFameBoards.All);
    }

    [Fact]
    public void MissionsReadTheirWholeShape()
    {
        const string json = """
        { "dayIndex": 740123, "resetsAtUnix": 1791558000,
          "missions": [
            { "id": "spin-25", "slot": 0, "metric": "rounds", "gameKind": "casino.slots", "target": 25, "threshold": 0,
              "reward": 5000, "progress": 12, "complete": false, "claimed": false } ] }
        """;
        var missions = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoMissionsDto)!;
        Assert.Equal(740123, missions.DayIndex);
        var mission = Assert.Single(missions.Missions!);
        Assert.Equal("spin-25", mission.Id);
        Assert.Equal(CasinoMissionMetrics.Rounds, mission.Metric);
        Assert.Equal(25, mission.Target);
        Assert.Equal(5000, mission.Reward);
        Assert.Equal(12, mission.Progress);
    }

    [Fact]
    public void AMissionClaimCarriesTheGrantAndTheBankroll()
    {
        const string json = """
        { "granted": true, "reason": "", "missionId": "spin-25", "amount": 5000, "stack": 155000,
          "sitting": { "id": "s1", "stack": 155000 } }
        """;
        var claim = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoMissionClaimDto)!;
        Assert.True(claim.Granted);
        Assert.Equal(5000, claim.Amount);
        Assert.Equal("s1", claim.Sitting!.Id);
        var body = JsonSerializer.Serialize(new Core.Aethernet.Contracts.CasinoMissionClaimRequest("abc"),
            AethernetJsonContext.Default.CasinoMissionClaimRequest);
        Assert.Equal("{\"clientActionId\":\"abc\"}", body);
    }

    [Fact]
    public void ChallengesNameTheClaimantOrHideThem()
    {
        const string json = """
        { "serverNowUnix": 1791500000,
          "challenges": [ { "id": "c-1", "title": "First to 1,000x on Plinko", "kind": "first", "gameKind": "casino.plinko",
            "thresholdTenths": 10000, "minStake": 0, "startsAtUnix": 1791400000, "endsAtUnix": 1792000000, "state": 1,
            "reward": 1000000, "badgeId": "b-123",
            "leader": { "userId": "u-1", "displayName": "Mira", "handle": "mira", "avatarUrl": null, "badges": 0 },
            "leaderValue": 10840, "leaderAtUnix": 1791499000, "myValue": 0 },
            { "id": "c-2", "title": "Most 50x hits", "kind": "most", "state": 0, "leader": null } ] }
        """;
        var challenges = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoChallengesDto)!;
        Assert.Equal(1791500000, challenges.ServerNowUnix);
        Assert.Equal(2, challenges.Challenges!.Length);
        Assert.Equal("Mira", challenges.Challenges[0].Leader!.DisplayName);
        Assert.Equal(CasinoChallengeStates.Live, challenges.Challenges[0].State);
        Assert.Null(challenges.Challenges[1].Leader);
    }

    [Fact]
    public void FameBoardsReadEntriesMeAndTheChampion()
    {
        const string json = """
        { "board": "profit", "span": "week", "weekIndex": 105732, "startsAtUnix": 1791392400, "endsAtUnix": 1791997200,
          "entries": [ { "rank": 1, "player": { "userId": "u-1", "displayName": "Mira", "handle": "mira", "badges": 0 },
                         "value": 4200000, "gameKind": "", "stake": 0, "atUnix": 1791480000 },
                       { "rank": 2, "player": null, "value": 10, "gameKind": "", "stake": 0, "atUnix": 0 } ],
          "me": { "optedIn": true, "rank": 0, "value": 0 },
          "champion": { "rank": 1, "player": null, "value": 9100000, "gameKind": "", "stake": 0, "atUnix": 1791380000 } }
        """;
        var board = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoFameBoardDto)!;
        Assert.Equal(CasinoFameBoards.Profit, board.Board);
        Assert.Equal(2, board.Entries!.Length);
        Assert.Null(board.Entries[1].Player);
        Assert.True(board.Me!.OptedIn);
        Assert.Equal(9100000, board.Champion!.Value);
    }

    [Fact]
    public void TheFeedAndTheFloorRoomReadTheirShapes()
    {
        const string feed = """
        { "tab": "all", "serverNowUnixMs": 1791500000123,
          "items": [ { "roundId": "r1", "gameKind": "casino.slots", "player": null, "stake": 2500, "payout": 25000,
                       "multiplierTenths": 100, "atUnixMs": 1791499999000, "jackpot": 0 } ] }
        """;
        var parsed = JsonSerializer.Deserialize(feed, AethernetJsonContext.Default.CasinoFeedDto)!;
        Assert.Equal(25000, Assert.Single(parsed.Items!).Payout);

        const string state = """
        { "ticker": [ { "kind": "rain", "roundId": "", "gameKind": "", "player": null, "stake": 0, "payout": 0,
                        "multiplierTenths": 0, "amount": 1500, "recipients": 12, "roomIds": ["wheel-floor"],
                        "challengeId": "", "atUnixMs": 1 } ] }
        """;
        var floor = JsonSerializer.Deserialize(state, AethernetJsonContext.Default.CasinoFloorStateDto)!;
        var tick = Assert.Single(floor.Ticker!);
        Assert.Equal(CasinoTickKinds.Rain, tick.Kind);
        Assert.Equal(12, tick.Recipients);
        Assert.Equal("wheel-floor", Assert.Single(tick.RoomIds!));

        const string rain = """{ "rainId": "rain-9", "amount": 1500, "stack": 151500 }""";
        Assert.Equal(1500, JsonSerializer.Deserialize(rain, AethernetJsonContext.Default.CasinoRainPrivateDto)!.Amount);
    }
}
