using System.Text.Json;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoLadderTests
{
    [Fact]
    public void TheLadderIsTheStandardTwentySevenRungs()
    {
        Assert.Equal(27, CasinoLadder.Rungs.Length);
        Assert.Equal(100, CasinoLadder.Lowest);
        Assert.Equal(50_000_000_000, CasinoLadder.Highest);
        for (var index = 1; index < CasinoLadder.Rungs.Length; index++)
        {
            Assert.True(CasinoLadder.Rungs[index] > CasinoLadder.Rungs[index - 1]);
        }
    }

    [Fact]
    public void LevelCapsHitEveryAnchorAndInterpolateGeometrically()
    {
        Assert.Equal(10_000, CasinoLadder.LevelCap(1));
        Assert.Equal(50_000, CasinoLadder.LevelCap(10));
        Assert.Equal(250_000, CasinoLadder.LevelCap(20));
        Assert.Equal(1_000_000, CasinoLadder.LevelCap(30));
        Assert.Equal(25_000_000, CasinoLadder.LevelCap(50));
        Assert.Equal(50_000_000_000, CasinoLadder.LevelCap(100));
        Assert.Equal(50_000_000_000, CasinoLadder.LevelCap(140));
        Assert.Equal(10_000, CasinoLadder.LevelCap(0));
        var expected23 = (long)Math.Floor(250_000d * Math.Pow(4d, 0.3d));
        Assert.Equal(expected23, CasinoLadder.LevelCap(23));
        for (var level = 2; level <= 100; level++)
        {
            Assert.True(CasinoLadder.LevelCap(level) >= CasinoLadder.LevelCap(level - 1));
        }
    }

    [Fact]
    public void MaxBetIsTheLargerCapFlooredToARung()
    {
        Assert.Equal(10_000, CasinoLadder.MaxBet(1, 150_000));
        Assert.Equal(250_000, CasinoLadder.MaxBet(1, 5_000_000));
        Assert.Equal(250_000, CasinoLadder.MaxBet(23, 1_000_000));
        Assert.Equal(CeilingReason.Balance, CasinoLadder.ReasonFor(1, 5_000_000, CasinoLadder.DefaultAnchors,
            CasinoLadder.DefaultMaxWinPerBet));
        Assert.Equal(CeilingReason.Level, CasinoLadder.ReasonFor(1, 150_000, CasinoLadder.DefaultAnchors,
            CasinoLadder.DefaultMaxWinPerBet));
    }

    [Fact]
    public void ATenthOfTheMaxWinPerBetHoldsTheCeiling()
    {
        var anchors = CasinoLadder.DefaultAnchors;
        var maxWin = CasinoLadder.DefaultMaxWinPerBet;

        Assert.Equal(5_000_000, CasinoLadder.MaxBet(60, 0, anchors, maxWin));
        Assert.Equal(CeilingReason.MaxWin, CasinoLadder.ReasonFor(60, 0, anchors, maxWin));
        Assert.Equal(5_000_000, CasinoLadder.MaxBet(1, 5_000_000_000, anchors, maxWin));
        Assert.Equal(CeilingReason.MaxWin, CasinoLadder.ReasonFor(1, 5_000_000_000, anchors, maxWin));
        Assert.Equal(5_000_000, CasinoLadder.MaxBet(40, 0, anchors, maxWin));
        Assert.Equal(CeilingReason.Level, CasinoLadder.ReasonFor(40, 0, anchors, maxWin));
        Assert.Equal(1_000_000, CasinoLadder.MaxBet(30, 0, anchors, maxWin));
        Assert.Equal(CeilingReason.Level, CasinoLadder.ReasonFor(30, 0, anchors, maxWin));
        Assert.Equal(2_500_000, CasinoLadder.MaxBet(1, 500_000_000, anchors, 30_000_000));
        Assert.Equal(100_000_000, CasinoLadder.MaxBet(60, 0, anchors, 0));
    }

    [Fact]
    public void ATenfoldWinAtTheCeilingNeverPassesTheMaxWin()
    {
        var anchors = CasinoLadder.DefaultAnchors;
        var maxWin = CasinoLadder.DefaultMaxWinPerBet;
        for (var level = 1; level <= 100; level += 9)
        {
            Assert.True(CasinoLadder.MaxBet(level, 900_000_000_000, anchors, maxWin) * CasinoLadder.MaxWinBetMultiple
                <= maxWin);
        }
    }

    [Fact]
    public void TheServerMaxWinReasonReadsBack()
    {
        Assert.Equal(CeilingReason.MaxWin, CasinoLadder.ReasonOf("max_win"));
        Assert.Equal(CeilingReason.Balance, CasinoLadder.ReasonOf("balance"));
        Assert.Equal(CeilingReason.Level, CasinoLadder.ReasonOf("level"));
        Assert.Equal(CeilingReason.Level, CasinoLadder.ReasonOf(string.Empty));
    }

    [Fact]
    public void BadAnchorsFallBackToTheDefaults()
    {
        Assert.Equal(CasinoLadder.LevelCap(30), CasinoLadder.LevelCap(30, new long[] { 1, 2 }));
    }

    [Fact]
    public void RungStepsFloorCeilAndWalk()
    {
        Assert.Equal(100, CasinoLadder.FloorToRung(50));
        Assert.Equal(2_500, CasinoLadder.FloorToRung(4_999));
        Assert.Equal(5_000, CasinoLadder.CeilToRung(2_501));
        Assert.Equal(25_000, CasinoLadder.StepUp(10_000));
        Assert.Equal(5_000, CasinoLadder.StepDown(10_000));
        Assert.True(CasinoLadder.IsRung(2_500_000));
        Assert.False(CasinoLadder.IsRung(2_000));
    }

    [Fact]
    public void ComposerMovesSnapToTheLadderInsideTheBounds()
    {
        Assert.Equal(1_000, CasinoLadder.Clamp(1_700, 100, 10_000, 1_000_000));
        Assert.Equal(100, CasinoLadder.Clamp(0, 100, 10_000, 1_000_000));
        Assert.Equal(10_000, CasinoLadder.Clamp(900_000, 100, 10_000, 1_000_000));
        Assert.Equal(2_500, CasinoLadder.Clamp(9_000, 100, 10_000, 3_000));
        Assert.Equal(0, CasinoLadder.Clamp(500, 250, 10_000, 200));
        Assert.Equal(2_000, CasinoLadder.Clamp(2_400, 2_000, 10_000, 1_000_000));
        Assert.Equal(1_000, CasinoLadder.Half(2_500, 100, 10_000, 1_000_000));
        Assert.Equal(25_000, CasinoLadder.Double(10_000, 100, 50_000, 1_000_000));
        Assert.Equal(50_000, CasinoLadder.Double(25_000, 100, 50_000, 1_000_000));
        Assert.Equal(10_000, CasinoLadder.Top(100, 10_000, 1_000_000));
        Assert.Equal(25_000, CasinoLadder.Top(100, 1_000_000, 30_000));
    }

    [Fact]
    public void TheServerCeilingWinsWhenTheFloorSendsOne()
    {
        var state = new CasinoStateDto(Sitting: new CasinoSittingDto(Stack: 150_000),
            Progress: new CasinoProgressDto(Level: 12),
            Ceiling: new CasinoCeilingDto(MaxBet: 25_000, LevelCap: 69_000, BalanceCap: 7_500, Reason: "level"));
        var ceiling = CasinoLadder.CeilingFor(state);
        Assert.True(ceiling.FromServer);
        Assert.Equal(25_000, ceiling.MaxBet);
        Assert.Equal(12, ceiling.Level);
        Assert.Equal(CeilingReason.Level, ceiling.Reason);
    }

    [Fact]
    public void AnOldServerFallsBackToTheLocalFormula()
    {
        var state = new CasinoStateDto(Sitting: new CasinoSittingDto(Stack: 5_000_000));
        var ceiling = CasinoLadder.CeilingFor(state);
        Assert.False(ceiling.FromServer);
        Assert.Equal(1, ceiling.Level);
        Assert.Equal(250_000, ceiling.MaxBet);
        Assert.Equal(CeilingReason.Balance, ceiling.Reason);
        Assert.Equal(10_000, CasinoLadder.CeilingFor(null).MaxBet);
    }

    [Fact]
    public void TheStateWireCarriesProgressAndCeiling()
    {
        const string json = "{\"progress\":{\"level\":23,\"xp\":70000},\"ceiling\":{\"maxBet\":250000,\"levelCap\":378929," +
                            "\"balanceCap\":50000,\"balance\":1000000,\"reason\":\"level\",\"nextLevelCap\":400000}," +
                            "\"levelCapAnchors\":[10000,50000]}";
        var state = JsonSerializer.Deserialize(json, AethernetJsonContext.Default.CasinoStateDto);
        Assert.NotNull(state);
        Assert.Equal(23, state!.Progress!.Level);
        Assert.Equal(250_000, state.Ceiling!.MaxBet);
        Assert.Equal("level", state.Ceiling.Reason);
        Assert.Equal(2, state.LevelCapAnchors!.Length);
    }

    [Fact]
    public void AutoPlayResetsOrRaisesAlongTheLadder()
    {
        var plan = new AutoBetPlan { Rounds = 0, OnLoss = AutoAdjust.Increase, OnLossPercent = 100 };
        plan.Start(1_000);
        Assert.Equal(AutoStop.None, plan.Settle(1_000, 0, false, 100, 100_000, 1_000_000));
        Assert.Equal(2_500, plan.Next);
        Assert.Equal(AutoStop.None, plan.Settle(2_500, 0, false, 100, 100_000, 1_000_000));
        Assert.Equal(5_000, plan.Next);
        Assert.Equal(AutoStop.None, plan.Settle(5_000, 20_000, false, 100, 100_000, 1_000_000));
        Assert.Equal(1_000, plan.Next);
        Assert.True(plan.Running);
    }

    [Fact]
    public void AutoPlayStopsOnCountProfitLossBonusAndChips()
    {
        var plan = new AutoBetPlan { Rounds = 2 };
        plan.Start(1_000);
        plan.Settle(1_000, 0, false, 100, 10_000, 100_000);
        Assert.Equal(AutoStop.Count, plan.Settle(1_000, 0, false, 100, 10_000, 100_000));
        Assert.False(plan.Running);

        plan = new AutoBetPlan { Rounds = 0, StopOnProfit = 5_000 };
        plan.Start(1_000);
        Assert.Equal(AutoStop.Profit, plan.Settle(1_000, 7_000, false, 100, 10_000, 100_000));

        plan = new AutoBetPlan { Rounds = 0, StopOnLoss = 1_500 };
        plan.Start(1_000);
        plan.Settle(1_000, 0, false, 100, 10_000, 100_000);
        Assert.Equal(AutoStop.Loss, plan.Settle(1_000, 0, false, 100, 10_000, 100_000));

        plan = new AutoBetPlan { Rounds = 0 };
        plan.Start(1_000);
        Assert.Equal(AutoStop.Bonus, plan.Settle(1_000, 0, true, 100, 10_000, 100_000));

        plan = new AutoBetPlan { Rounds = 0 };
        plan.Start(1_000);
        Assert.Equal(AutoStop.Chips, plan.Settle(1_000, 0, false, 250, 10_000, 200));
    }

    [Fact]
    public void TheRealityCheckComesEveryHundredRoundsOrHalfHour()
    {
        var check = new RealityCheck();
        check.Tick(0);
        for (var round = 0; round < RealityCheck.RoundsPerCheck - 1; round++)
        {
            check.Record(100, 0, round * 1000L);
        }

        Assert.False(check.Due);
        check.Record(100, 250, 100_000);
        Assert.True(check.Due);
        Assert.Equal(-99 * 100 + 150, check.Net);
        check.Acknowledge(100_000);
        Assert.False(check.Due);
        check.Tick(100_000 + RealityCheck.MillisecondsPerCheck);
        Assert.False(check.Due);
        check.Record(100, 0, 100_000 + RealityCheck.MillisecondsPerCheck);
        Assert.True(check.Due);
    }
}
