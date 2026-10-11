using System.Numerics;
using Aetherphone.Apps.Casino.Cabinets;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BarkeepDeckLayoutTests
{
    private const float PhoneWidth = 360f;

    public static TheoryData<float> Scales => new() { 0.75f, 1f, 1.25f, 1.5f };

    private static Rect Deck(float scale) =>
        new(new Vector2(0f, 500f * scale), new Vector2(PhoneWidth * scale, (500f + BarkeepDeckLayout.Height) * scale));

    [Theory]
    [MemberData(nameof(Scales))]
    public void TheRowAndThePrimaryStackInsideTheDeckWithoutTouching(float scale)
    {
        var deck = Deck(scale);
        var layout = BarkeepDeckLayout.Compute(deck, scale);

        Assert.True(layout.Row.Min.Y >= deck.Min.Y);
        Assert.True(layout.Row.Max.Y + DeckActions.Gap * scale <= layout.Action.Min.Y + 0.01f);
        Assert.True(layout.Action.Max.Y <= deck.Max.Y + 0.01f);
        Assert.Equal(BarkeepDeckLayout.RowHeight * scale, layout.Row.Height, 2);
        Assert.Equal(DeckActions.PrimaryHeight * scale, layout.Action.Height, 2);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void APillGivesItsLabelTheRoomTheButtonNeeds(float scale)
    {
        var row = BarkeepDeckLayout.Compute(Deck(scale), scale).Row;
        var labelWidth = 190f * scale;
        var pill = BarkeepDeckLayout.Pill(row, labelWidth, scale);

        Assert.True(pill.Width - pill.Height >= labelWidth - 0.01f);
        Assert.True(pill.Min.X >= row.Min.X - 0.01f && pill.Max.X <= row.Max.X + 0.01f);
        Assert.True(BarkeepDeckLayout.Pill(row, 2f, scale).Width >= BarkeepDeckLayout.MinimumPill * scale - 0.01f);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void HudCellsSplitTheRowWithoutOverlap(float scale)
    {
        var row = BarkeepDeckLayout.Compute(Deck(scale), scale).Row;
        for (var index = 0; index < BarkeepDeckLayout.HudCells; index++)
        {
            var cell = BarkeepDeckLayout.Cell(row, index);
            Assert.True(cell.Min.X >= row.Min.X - 0.01f && cell.Max.X <= row.Max.X + 0.01f);
            if (index > 0)
            {
                Assert.True(cell.Min.X >= BarkeepDeckLayout.Cell(row, index - 1).Max.X - 0.01f);
            }
        }
    }

    [Fact]
    public void TheDeckHoldsTheComposerRowsToo()
    {
        Assert.True(BarkeepDeckLayout.Height >= Aetherphone.Apps.Casino.BetComposer.DeckHeightFor(true, true));
    }
}
