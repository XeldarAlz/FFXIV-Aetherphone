using Aetherphone.Apps.Casino.Strip;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoClubTests
{
    [Theory]
    [InlineData("rounds", "casino.slots", 25, 0, "Spin 25 times")]
    [InlineData("rounds", "casino.blackjack", 10, 0, "Play 10 hands of blackjack")]
    [InlineData("rounds", "casino.plinko", 10, 0, "Drop 10 Plinko balls")]
    [InlineData("rounds", "casino.race", 2, 0, "Bet on 2 races")]
    [InlineData("rounds", "", 5, 0, "Play 5 rounds of anything")]
    [InlineData("games", "", 3, 0, "Play 3 different games")]
    [InlineData("wins", "", 5, 0, "Win 5 rounds")]
    [InlineData("hits", "", 1, 100, "Hit 10x on any game")]
    [InlineData("hits", "", 3, 50, "Hit 5x or more, 3 times")]
    [InlineData("maxbet", "", 1, 0, "Bet your max once")]
    [InlineData("hosted", "", 1, 0, "Play a hand at a hosted table")]
    [InlineData("mystery", "", 1, 0, "Finish today's mission")]
    public void MissionsReadAsOneSentence(string metric, string gameKind, long target, long threshold, string expected)
    {
        var mission = new CasinoMissionDto("m", 0, metric, gameKind, target, threshold, 5000);
        Assert.Equal(expected, MissionText.Compose(mission));
    }

    [Fact]
    public void ClubPerksFollowTheTierTable()
    {
        Assert.Equal("Bonuses x1, weekly rebate from Silver", ClubSheet.PerkLine(CasinoClubTiers.Bronze));
        Assert.Equal("Bonuses x1.5, weekly rebate 7.5%", ClubSheet.PerkLine(CasinoClubTiers.Gold));
        Assert.Equal("Bonuses x2, weekly rebate 7.5%, daily reload", ClubSheet.PerkLine(CasinoClubTiers.Platinum));
        Assert.Equal(7, CasinoClubTiers.Floors.Length);
        Assert.Equal(25_000_000, CasinoClubTiers.Floors[CasinoClubTiers.Obsidian]);
    }

    [Fact]
    public void FameValuesReadAsChipsOrMultiples()
    {
        Assert.Equal("4.2M", FameText.Value(CasinoFameBoards.Profit, 4_200_000));
        Assert.Equal("108.4x", FameText.Value(CasinoFameBoards.Multiplier, 1084));
        Assert.Equal("Hidden", FameText.Name(new CasinoFameEntryDto(1, null, 10)));
        Assert.Equal("Mira", FameText.Name(new CasinoFameEntryDto(1, new CasinoPlayerRefDto("u", "Mira", "mira"), 10)));
    }

    [Fact]
    public void TickerLinesNameTheWinnerOrHideThem()
    {
        var win = new CasinoFloorTickDto(CasinoTickKinds.Win, "r", "casino.plinko",
            new CasinoPlayerRefDto("u", "Mira", "mira"), 100, 108_400, 10840);
        Assert.Equal("Mira hit 1084x on Plinko", WinsTicker.Line(win));
        var rain = new CasinoFloorTickDto(CasinoTickKinds.Rain, Amount: 1500, Recipients: 12);
        Assert.Equal("Chip rain: 1.5K chips each for 12 players", WinsTicker.Line(rain));
        var hidden = new CasinoFloorTickDto(CasinoTickKinds.Jackpot, Amount: 12_000_000);
        Assert.Equal("Hidden won the jackpot: 12M chips", WinsTicker.Line(hidden));
    }
}
