using System.Numerics;
using Aetherphone.Apps.Casino;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class BlackjackDeckLayoutTests
{
    private const float PhoneWidth = 360f;
    private const float Epsilon = 0.01f;

    public static TheoryData<float> Scales => new() { 0.75f, 0.9f, 1f, 1.25f, 1.5f };

    private static Rect Deck(float scale)
    {
        var height = BlackjackDeckLayout.DeckHeight(BetComposer.DeckHeightFor(false, false)) * scale;
        return new Rect(new Vector2(0f, 600f * scale), new Vector2(PhoneWidth * scale, 600f * scale + height));
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void TheUpperRowIsATouchRowInsideTheDeckAboveThePrimaryRow(float scale)
    {
        var deck = Deck(scale);
        var row = DeckActions.Row(deck, scale);
        var upper = BlackjackDeckLayout.UpperRow(row, scale);

        Assert.True(upper.Height >= BlackjackDeckLayout.Touch * scale - Epsilon);
        Assert.True(upper.Min.Y >= deck.Min.Y + DeckActions.Pad * scale - Epsilon);
        Assert.True(upper.Max.Y <= row.Min.Y - DeckActions.Gap * scale + Epsilon);
        Assert.True(row.Height >= DeckActions.PrimaryHeight * scale - Epsilon);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void ShortChoicesShareOneRowAndKeepTheSecondaryContentSized(float scale)
    {
        var row = DeckActions.Row(Deck(scale), scale);
        var upper = BlackjackDeckLayout.UpperRow(row, scale);
        var stacked = BlackjackDeckLayout.Split(row, upper, 70f * scale, 90f * scale, scale, out var secondary,
            out var primary);

        Assert.False(stacked);
        Assert.True(secondary.Width >= 70f * scale + row.Height - Epsilon);
        Assert.True(primary.Width >= 90f * scale + row.Height - Epsilon);
        Assert.True(secondary.Max.X + DeckActions.Gap * scale <= primary.Min.X + Epsilon);
        Assert.True(secondary.Min.X >= row.Min.X - Epsilon && primary.Max.X <= row.Max.X + Epsilon);
        Assert.Equal(row.Height, primary.Height, 2);
        Assert.Equal(row.Height, secondary.Height, 2);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void LongChoicesStackSoNeitherLabelIsCut(float scale)
    {
        var row = DeckActions.Row(Deck(scale), scale);
        var upper = BlackjackDeckLayout.UpperRow(row, scale);
        var stacked = BlackjackDeckLayout.Split(row, upper, 170f * scale, 150f * scale, scale, out var secondary,
            out var primary);

        Assert.True(stacked);
        Assert.Equal(row, primary);
        Assert.Equal(upper, secondary);
        Assert.True(secondary.Max.Y <= primary.Min.Y);
        Assert.True(secondary.Width >= 170f * scale + secondary.Height - Epsilon);
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void ThreeSideActionsStayTouchSizedWithoutOverlap(float scale)
    {
        var upper = BlackjackDeckLayout.UpperRow(DeckActions.Row(Deck(scale), scale), scale);
        for (var index = 0; index < 3; index++)
        {
            var cell = DeckActions.Slice(upper, index, 3, scale);
            Assert.True(cell.Width >= BlackjackDeckLayout.Touch * scale);
            Assert.True(cell.Min.X >= upper.Min.X - Epsilon && cell.Max.X <= upper.Max.X + Epsilon);
            if (index > 0)
            {
                Assert.True(DeckActions.Slice(upper, index - 1, 3, scale).Max.X <= cell.Min.X + Epsilon);
            }
        }
    }
}
