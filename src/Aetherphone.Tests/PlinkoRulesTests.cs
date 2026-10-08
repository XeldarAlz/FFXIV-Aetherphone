using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PlinkoRulesTests
{
    [Theory]
    [InlineData(8, PlinkoRules.Low, new[] { 56, 21, 11, 10, 5, 10, 11, 21, 56 })]
    [InlineData(8, PlinkoRules.Medium, new[] { 130, 30, 13, 7, 4, 7, 13, 30, 130 })]
    [InlineData(8, PlinkoRules.High, new[] { 290, 40, 15, 3, 2, 3, 15, 40, 290 })]
    [InlineData(12, PlinkoRules.Low, new[] { 100, 30, 16, 14, 11, 10, 5, 10, 11, 14, 16, 30, 100 })]
    [InlineData(12, PlinkoRules.Medium, new[] { 330, 110, 40, 20, 11, 6, 3, 6, 11, 20, 40, 110, 330 })]
    [InlineData(12, PlinkoRules.High, new[] { 1700, 240, 81, 20, 7, 2, 2, 2, 7, 20, 81, 240, 1700 })]
    [InlineData(16, PlinkoRules.Low, new[] { 160, 90, 20, 14, 14, 12, 11, 10, 5, 10, 11, 12, 14, 14, 20, 90, 160 })]
    [InlineData(16, PlinkoRules.Medium,
        new[] { 1100, 410, 100, 50, 30, 15, 10, 5, 3, 5, 10, 15, 30, 50, 100, 410, 1100 })]
    [InlineData(16, PlinkoRules.High,
        new[] { 10000, 1300, 260, 90, 40, 20, 2, 2, 2, 2, 2, 20, 40, 90, 260, 1300, 10000 })]
    public void TheStripsMirrorTheBackendEngine(int rows, int risk, int[] expected)
    {
        Assert.Equal(expected, PlinkoRules.Strip(rows, risk).ToArray());
        Assert.Equal(rows + 1, PlinkoRules.SlotCount(rows));
    }

    [Theory]
    [InlineData(8, PlinkoRules.Low, 9898)]
    [InlineData(8, PlinkoRules.Medium, 9891)]
    [InlineData(8, PlinkoRules.High, 9906)]
    [InlineData(12, PlinkoRules.Low, 9898)]
    [InlineData(12, PlinkoRules.Medium, 9899)]
    [InlineData(12, PlinkoRules.High, 9912)]
    [InlineData(16, PlinkoRules.Low, 9900)]
    [InlineData(16, PlinkoRules.Medium, 9899)]
    [InlineData(16, PlinkoRules.High, 9898)]
    public void EveryBoardReturnsItsPublishedClosedForm(int rows, int risk, int basisPoints)
    {
        Assert.Equal(basisPoints, PlinkoRules.ReturnBasisPoints(rows, risk));
        Assert.InRange(PlinkoRules.ReturnBasisPoints(rows, risk), PlinkoRules.MinReturnBasisPoints,
            PlinkoRules.MaxReturnBasisPoints);
        Assert.InRange(PlinkoRules.ReturnTenths(rows, risk), 989, 992);
    }

    [Fact]
    public void EveryStripIsMirrored()
    {
        for (var rowsIndex = 0; rowsIndex < PlinkoRules.RowCounts.Length; rowsIndex++)
        {
            var rows = PlinkoRules.RowCounts[rowsIndex];
            for (var risk = 0; risk < PlinkoRules.RiskCount; risk++)
            {
                var strip = PlinkoRules.Strip(rows, risk);
                for (var slot = 0; slot < strip.Length; slot++)
                {
                    Assert.Equal(strip[slot], strip[strip.Length - 1 - slot]);
                }

                Assert.Equal(strip[0], PlinkoRules.EdgeTenths(rows, risk));
            }
        }
    }

    [Fact]
    public void TheConstantsMatchTheWireDoc()
    {
        Assert.Equal(100, PlinkoRules.MinBet);
        Assert.Equal(10, PlinkoRules.MaxInFlight);
        Assert.Equal(PlinkoRules.Medium, PlinkoRules.DefaultRisk);
        Assert.Equal(new[] { 8, 12, 16 }, PlinkoRules.RowCounts);
        Assert.Equal(PlinkoRules.TopTenths, PlinkoRules.EdgeTenths(16, PlinkoRules.High));
        Assert.Equal("plinko", PlinkoRules.Feature);
        Assert.Equal("peg", PlinkoRules.PegPurpose);
        Assert.Equal(128, PlinkoRules.EdgeOneIn(8));
        Assert.Equal(2048, PlinkoRules.EdgeOneIn(12));
        Assert.Equal(32768, PlinkoRules.EdgeOneIn(16));
    }

    [Fact]
    public void InvalidBoardsHaveNoStrip()
    {
        Assert.True(PlinkoRules.Strip(10, PlinkoRules.Low).IsEmpty);
        Assert.True(PlinkoRules.Strip(8, 3).IsEmpty);
        Assert.False(PlinkoRules.IsValidBoard(16, -1));
        Assert.Equal(0, PlinkoRules.MultiplierTenths(8, PlinkoRules.Low, 9));
        Assert.Equal(-1, PlinkoRules.RowsIndex(9));
    }

    [Fact]
    public void ThePayoutIsExactOnEveryLadderRung()
    {
        for (var rung = 0; rung < CasinoLadder.Rungs.Length; rung++)
        {
            var stake = CasinoLadder.Rungs[rung];
            for (var rowsIndex = 0; rowsIndex < PlinkoRules.RowCounts.Length; rowsIndex++)
            {
                var rows = PlinkoRules.RowCounts[rowsIndex];
                for (var risk = 0; risk < PlinkoRules.RiskCount; risk++)
                {
                    var strip = PlinkoRules.Strip(rows, risk);
                    for (var slot = 0; slot < strip.Length; slot++)
                    {
                        Assert.Equal(0, stake * strip[slot] % PlinkoRules.TenthsPerMultiple);
                        Assert.Equal(stake * strip[slot] / 10, PlinkoRules.Payout(stake, strip[slot]));
                    }
                }
            }
        }
    }

    [Fact]
    public void APathIsRowsBitsThatSumToTheSlot()
    {
        Assert.True(PlinkoRules.IsPath(new[] { 1, 0, 1, 1, 0, 0, 0, 1 }, 8, 4));
        Assert.False(PlinkoRules.IsPath(new[] { 1, 0, 1, 1, 0, 0, 0, 1 }, 8, 5));
        Assert.False(PlinkoRules.IsPath(new[] { 1, 0, 2, 1, 0, 0, 0, 0 }, 8, 4));
        Assert.False(PlinkoRules.IsPath(new[] { 1, 0, 1 }, 8, 2));
        Assert.Equal(3, PlinkoRules.SlotOf(new[] { 1, 0, 1, 1 }));
    }
}
