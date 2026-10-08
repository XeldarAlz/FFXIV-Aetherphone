using System.Text.Json;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class SlotsRulesTests
{
    [Fact]
    public void GoldenBirdMirrorsTheBackendMachine()
    {
        using var document = SlotsVectorFiles.Load("slots-bird.json");
        var rules = document.RootElement.GetProperty("rules");
        var lines = rules.GetProperty("lines");
        AssertTable(lines.GetProperty("linePays"), GoldenBirdRules.LinePays);
        Assert.Equal(Longs(lines.GetProperty("scatterPays")), GoldenBirdRules.ScatterPays);
        Assert.Equal(Ints(lines.GetProperty("expanderWeights")), GoldenBirdRules.ExpanderWeights);
        Assert.Equal(GoldenBirdRules.ExpanderWeightTotal, GoldenBirdRules.ExpanderWeights.Sum());
        Assert.Equal(StripLengths(lines.GetProperty("strips")), GoldenBirdRules.StripLengths);
        Assert.Equal(StripLengths(lines.GetProperty("freeStrips")), GoldenBirdRules.FreeStripLengths);
        Assert.Equal(GoldenBirdRules.Wild, lines.GetProperty("wild").GetInt32());
        Assert.Equal(GoldenBirdRules.Scatter, lines.GetProperty("scatter").GetInt32());
        Assert.Equal(GoldenBirdRules.RetriggerSpins, lines.GetProperty("retriggerSpins").GetInt32());
        Assert.Equal(GoldenBirdRules.FreeSpinCap, lines.GetProperty("freeSpinCap").GetInt32());
        AssertPaylines(lines.GetProperty("paylines"));
        AssertInfo(rules, SlotsMachines.Bird);
    }

    [Fact]
    public void CrystalCascadeMirrorsTheBackendMachine()
    {
        using var document = SlotsVectorFiles.Load("slots-cascade.json");
        var rules = document.RootElement.GetProperty("rules");
        var cluster = rules.GetProperty("cluster");
        AssertTable(cluster.GetProperty("clusterPays"), CrystalCascadeRules.ClusterPays);
        Assert.Equal(Longs(cluster.GetProperty("scatterPays")), CrystalCascadeRules.ScatterPays);
        Assert.Equal(Ints(cluster.GetProperty("bandMinimums")), CrystalCascadeRules.BandMinimums);
        Assert.Equal(Ints(cluster.GetProperty("tumbleLadder")), CrystalCascadeRules.TumbleLadder);
        Assert.Equal(Ints(cluster.GetProperty("orbValues")), CrystalCascadeRules.OrbValues);
        Assert.Equal(Ints(cluster.GetProperty("orbWeights")), CrystalCascadeRules.OrbWeights);
        Assert.Equal(Ints(cluster.GetProperty("baseWeights")), CrystalCascadeRules.BaseWeights);
        Assert.Equal(Ints(cluster.GetProperty("anteWeights")), CrystalCascadeRules.AnteWeights);
        Assert.Equal(Ints(cluster.GetProperty("freeWeights")), CrystalCascadeRules.FreeWeights);
        Assert.Equal(Ints(cluster.GetProperty("buyWeights")), CrystalCascadeRules.BuyWeights);
        Assert.Equal(CrystalCascadeRules.BuyCostMultiple, cluster.GetProperty("buyCostMultiple").GetInt64());
        Assert.Equal(CrystalCascadeRules.FreeSpinCap, cluster.GetProperty("freeSpinCap").GetInt32());
        Assert.Equal(CrystalCascadeRules.RetriggerSpins, cluster.GetProperty("retriggerSpins").GetInt32());
        Assert.Equal(CrystalCascadeRules.TriggerScatters, cluster.GetProperty("triggerScatters").GetInt32());
        AssertInfo(rules, SlotsMachines.Cascade);
    }

    [Fact]
    public void MoogleMoneyMirrorsTheBackendMachine()
    {
        using var document = SlotsVectorFiles.Load("slots-moogle.json");
        var rules = document.RootElement.GetProperty("rules");
        var hold = rules.GetProperty("hold");
        var lines = hold.GetProperty("lines");
        AssertTable(lines.GetProperty("linePays"), MoogleMoneyRules.LinePays);
        Assert.Equal(StripLengths(lines.GetProperty("strips")), MoogleMoneyRules.StripLengths);
        Assert.Equal(StripLengths(lines.GetProperty("freeStrips")), MoogleMoneyRules.FreeStripLengths);
        Assert.Equal(Longs(hold.GetProperty("coinValues")), MoogleMoneyRules.CoinValues);
        Assert.Equal(Ints(hold.GetProperty("coinKinds")), MoogleMoneyRules.CoinKinds);
        Assert.Equal(Ints(hold.GetProperty("coinWeights")), MoogleMoneyRules.CoinWeights);
        Assert.Equal(MoogleMoneyRules.CoinWeightTotal, MoogleMoneyRules.CoinWeights.Sum());
        Assert.Equal(Ints(hold.GetProperty("giantWeights")), MoogleMoneyRules.GiantWeights);
        Assert.Equal(MoogleMoneyRules.CoinChance, hold.GetProperty("coinChance").GetInt32());
        Assert.Equal(MoogleMoneyRules.RespinCoinChance, hold.GetProperty("respinCoinChance").GetInt32());
        Assert.Equal(MoogleMoneyRules.TriggerCoins, hold.GetProperty("triggerCoins").GetInt32());
        Assert.Equal(MoogleMoneyRules.Respins, hold.GetProperty("respins").GetInt32());
        Assert.Equal(MoogleMoneyRules.GrandUnits, hold.GetProperty("grandUnits").GetInt64());
        Assert.Equal(MoogleMoneyRules.FreeGames, hold.GetProperty("freeGames").GetInt32());
        Assert.Equal(MoogleMoneyRules.MiniResetUnits, hold.GetProperty("mini").GetProperty("reset").GetInt64());
        Assert.Equal(MoogleMoneyRules.MiniCeilingUnits, hold.GetProperty("mini").GetProperty("ceiling").GetInt64());
        Assert.Equal(MoogleMoneyRules.MiniStepUnits, hold.GetProperty("mini").GetProperty("step").GetInt64());
        Assert.Equal(MoogleMoneyRules.MinorResetUnits, hold.GetProperty("minor").GetProperty("reset").GetInt64());
        Assert.Equal(MoogleMoneyRules.MinorCeilingUnits, hold.GetProperty("minor").GetProperty("ceiling").GetInt64());
        Assert.Equal(MoogleMoneyRules.MinorStepUnits, hold.GetProperty("minor").GetProperty("step").GetInt64());
        AssertPaylines(lines.GetProperty("paylines"));
        AssertInfo(rules, SlotsMachines.Moogle);
    }

    [Fact]
    public void EveryVectorWinPaysTheMirroredTableAtItsBet()
    {
        var checkedWins = 0;
        foreach (var (name, round) in SlotsVectorFiles.Rounds())
        {
            var bet = round.GetProperty("bet").GetInt64();
            foreach (var step in round.GetProperty("steps").EnumerateArray())
            {
                foreach (var win in step.GetProperty("wins").EnumerateArray())
                {
                    var units = UnitsFor(name, win.GetProperty("line").GetInt32(), win.GetProperty("symbol").GetInt32(),
                        win.GetProperty("count").GetInt32());
                    Assert.Equal(SlotsRules.ChipsFor(bet, units), win.GetProperty("pay").GetInt64());
                    checkedWins++;
                }
            }
        }

        Assert.True(checkedWins > 100);
    }

    [Fact]
    public void ModesCostWhatTheWireDocSays()
    {
        Assert.Equal(1_000, SlotsRules.CostOf(SlotsRules.BaseMode, 1_000));
        Assert.Equal(1_250, SlotsRules.CostOf(SlotsRules.AnteMode, 1_000));
        Assert.Equal(312, SlotsRules.CostOf(SlotsRules.AnteMode, 250));
        Assert.Equal(100_000, SlotsRules.CostOf(SlotsRules.BuyMode, 1_000));
        Assert.True(SlotsRules.Offers(SlotsRules.CascadeId, SlotsRules.AnteMode));
        Assert.True(SlotsRules.Offers(SlotsRules.CascadeId, SlotsRules.BuyMode));
        Assert.False(SlotsRules.Offers(SlotsRules.BirdId, SlotsRules.AnteMode));
        Assert.False(SlotsRules.Offers(SlotsRules.MoogleId, SlotsRules.BuyMode));
        Assert.False(SlotsRules.Offers("slots.unknown", SlotsRules.BaseMode));
    }

    [Fact]
    public void BetsAreLadderRungsFromTheMinimum()
    {
        Assert.True(SlotsRules.IsBet(100));
        Assert.True(SlotsRules.IsBet(2_500));
        Assert.False(SlotsRules.IsBet(50));
        Assert.False(SlotsRules.IsBet(1_234));
        Assert.Equal(262, SlotsRules.ChipsFor(250, 10_500));
        Assert.Equal(50_000_000_000L * 5_000, SlotsRules.ChipsFor(50_000_000_000L, 50_000_000));
    }

    [Fact]
    public void OnlyGoldenBirdWinsUnderTwentyBetsCanBeGambled()
    {
        Assert.True(SlotsRules.GambleEligible(SlotsRules.BirdId, 1_000, 1_050));
        Assert.True(SlotsRules.GambleEligible(SlotsRules.BirdId, 1_000, 19_999));
        Assert.False(SlotsRules.GambleEligible(SlotsRules.BirdId, 1_000, 20_000));
        Assert.False(SlotsRules.GambleEligible(SlotsRules.BirdId, 1_000, 0));
        Assert.False(SlotsRules.GambleEligible(SlotsRules.CascadeId, 1_000, 1_050));
    }

    private static long UnitsFor(string name, int line, int symbol, int count)
    {
        switch (name)
        {
            case "slots-cascade.json":
                return line == SlotsRules.LineCluster
                    ? CrystalCascadeRules.ClusterPays[symbol][CrystalCascadeRules.BandOf(count)]
                    : CrystalCascadeRules.ScatterPay(count);
            case "slots-moogle.json":
                return line >= 0 ? MoogleMoneyRules.LinePays[symbol][count - 3] : MoogleMoneyRules.GrandUnits;
            default:
                if (line == SlotsRules.LineScatter)
                {
                    return GoldenBirdRules.ScatterPay(count);
                }

                return line == SlotsRules.LineExpander
                    ? GoldenBirdRules.ExpanderPay(symbol, count)
                    : GoldenBirdRules.LinePays[symbol][count - 3];
        }
    }

    private static void AssertInfo(JsonElement rules, SlotsMachineInfo info)
    {
        Assert.Equal(info.Id, rules.GetProperty("id").GetString());
        Assert.Equal(info.Reels, rules.GetProperty("reels").GetInt32());
        Assert.Equal(info.Rows, rules.GetProperty("rows").GetInt32());
        Assert.Equal(info.VolatilityBars, rules.GetProperty("volatilityBars").GetInt32());
        Assert.Equal(info.MaxWinMultiple, rules.GetProperty("maxWinMultiple").GetInt32());
        Assert.Equal(info.ReturnBasisPoints, rules.GetProperty("returnBasisPoints").GetInt32());
        Assert.Equal(info.BonusOneIn, rules.GetProperty("bonusOneIn").GetInt32());
        Assert.Equal(info.OffersAnte, rules.GetProperty("offersAnte").GetBoolean());
        Assert.Equal(info.OffersBuy, rules.GetProperty("offersBuy").GetBoolean());
        Assert.Equal(info.CellCount, rules.GetProperty("cellCount").GetInt32());
    }

    private static void AssertPaylines(JsonElement paylines)
    {
        var index = 0;
        foreach (var line in paylines.EnumerateArray())
        {
            Assert.Equal(Ints(line), SlotsRules.Paylines[index]);
            index++;
        }

        Assert.Equal(SlotsRules.PaylineCount, index);
    }

    private static void AssertTable(JsonElement table, long[][] mirror)
    {
        var index = 0;
        foreach (var row in table.EnumerateArray())
        {
            Assert.Equal(Longs(row), mirror[index]);
            index++;
        }

        Assert.Equal(mirror.Length, index);
    }

    private static int[] StripLengths(JsonElement strips) =>
        strips.EnumerateArray().Select(strip => strip.GetArrayLength()).ToArray();

    private static int[] Ints(JsonElement array) => array.EnumerateArray().Select(value => value.GetInt32()).ToArray();

    private static long[] Longs(JsonElement array) =>
        array.EnumerateArray().Select(value => value.GetInt64()).ToArray();
}
