using System.Numerics;
using Aetherphone.Apps.Casino.Plinko;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PlinkoStageLayoutTests
{
    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void TheSignBoardAndPlateNeverCollide(float scale)
    {
        var screen = new Rect(Vector2.Zero, new Vector2(393f * scale, 852f * scale));
        var content = new Rect(new Vector2(16f * scale, 48f * scale), new Vector2(377f * scale, 822f * scale));
        var stage = CasinoStageLayout.Compute(screen, content, false, false, BetDeckLayout.DeckHeightFor(true, false),
            scale);
        var layout = PlinkoStageLayout.Compute(stage.Safe, scale);
        BetDeckLayoutTests.AssertDisjoint(new[] { layout.Sign, layout.Board, layout.Plate });
        BetDeckLayoutTests.AssertDisjoint(new[] { layout.Rail, layout.Stats });
        Assert.True(layout.Board.Height > 0f);
        Assert.True(layout.Rail.Min.Y >= layout.Plate.Min.Y && layout.Stats.Max.Y <= layout.Plate.Max.Y + 0.01f);
        Assert.True(layout.Rail.Min.Y > layout.Sign.Max.Y);
        Assert.True(layout.Plate.Max.Y <= stage.Deck.Min.Y);
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    public void TheRowsAndRiskPickersFillTheKnobWithoutOverlapAndKeepTheirNaturalWidth(float scale)
    {
        var knob = new Rect(new Vector2(12f * scale, 400f * scale), new Vector2(348f * scale, 436f * scale));
        var gap = 8f * scale;
        var rowsNatural = 123f * scale;
        var riskNatural = 158f * scale;

        PlinkoStageLayout.SplitKnob(knob, rowsNatural, riskNatural, gap, out var rows, out var risk);

        Assert.True(rows.Width >= rowsNatural - 0.01f);
        Assert.True(risk.Width >= riskNatural - 0.01f);
        Assert.True(rows.Max.X + gap <= risk.Min.X + 0.01f);
        Assert.Equal(knob.Min.X, rows.Min.X, 2);
        Assert.Equal(knob.Max.X, risk.Max.X, 2);
        Assert.Equal(knob.Height, rows.Height, 2);
        Assert.Equal(knob.Height, risk.Height, 2);
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void TheResultRailKeepsItsCaptionAndChipsInsideTheRowNewestFirst(float scale)
    {
        var rowLeft = 20f * scale;
        var rowWidth = 320f * scale;
        var caption = PlinkoResultRail.CaptionRoom(rowWidth);
        var fit = PlinkoResultRail.Fit(rowWidth, caption, scale);

        Assert.True(fit >= 1);
        Assert.True(PlinkoResultRail.ChipLeft(rowLeft, caption, 0, scale) >= rowLeft + caption);
        var last = PlinkoResultRail.ChipLeft(rowLeft, caption, fit - 1, scale) + PlinkoResultRail.ChipWidth * scale;
        Assert.True(last <= rowLeft + rowWidth + 0.01f);
        for (var age = 1; age < PlinkoResultRail.Capacity; age++)
        {
            Assert.True(PlinkoResultRail.AlphaFor(age) <= PlinkoResultRail.AlphaFor(age - 1));
            Assert.True(PlinkoResultRail.AlphaFor(age) >= PlinkoResultRail.MinimumAlpha);
        }
    }

    [Theory]
    [InlineData(5_000_000L, 1000, 50_000_000L, 55_000_000L)]
    [InlineData(1_000L, 1000, 50_000_000L, 1_000_000L)]
    [InlineData(5_000_000L, 1000, 0L, 5_000_000_000L)]
    public void TheMaxWinNeverPromisesMoreThanTheServerPays(long stake, int multiple, long maxWinPerBet, long shown)
    {
        Assert.Equal(shown, PlinkoStageLayout.PaidAtMost(stake * multiple, stake, maxWinPerBet));
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1.5f)]
    public void PickersThatCannotFitShareTheKnobWithoutLeavingIt(float scale)
    {
        var knob = new Rect(Vector2.Zero, new Vector2(200f * scale, 36f * scale));

        PlinkoStageLayout.SplitKnob(knob, 160f * scale, 180f * scale, 8f * scale, out var rows, out var risk);

        Assert.True(rows.Max.X <= risk.Min.X);
        Assert.True(risk.Max.X <= knob.Max.X + 0.01f);
        Assert.True(rows.Width > 0f && risk.Width > 0f);
    }
}
