using System.Numerics;
using Aetherphone.Apps.Casino.Cabinets;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Xunit;

namespace Aetherphone.Tests;

public sealed class WheelBoardLayoutTests
{
    private const float TallerDeck = 132f;

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void TheBoardStacksHistoryWheelCaptionAndCardsInsideSafe(float scale)
    {
        var stage = WheelTextMeasure.Stage(scale, WheelCabinet.DeckHeight);
        var board = WheelBoardLayout.Compute(stage.Safe, scale);
        var ordered = new[] { board.History, board.Wheel, board.Caption, board.Cards };
        for (var index = 0; index < ordered.Length; index++)
        {
            Assert.True(WheelTextMeasure.Inside(ordered[index], stage.Safe));
            if (index > 0)
            {
                Assert.True(ordered[index].Min.Y >= ordered[index - 1].Max.Y - 0.01f);
            }
        }

        Assert.Equal(stage.Safe.Max.Y, board.Cards.Max.Y);
        Assert.Equal(WheelPodiumLayout.Height(scale), board.Cards.Height, 3);
        Assert.True(board.Caption.Height >= WheelBoardLayout.CaptionHeight * scale - 0.01f);
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void TheWheelStaysAsWideAsSafeEvenOnATallerDeck(float scale)
    {
        var stage = WheelTextMeasure.Stage(scale, TallerDeck);
        var board = WheelBoardLayout.Compute(stage.Safe, scale);
        Assert.True(board.Wheel.Height >= board.Wheel.Width);
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void FiveCardsSplitTheRowEvenlyWithoutTouching(float scale)
    {
        var board = WheelBoardLayout.Compute(WheelTextMeasure.Stage(scale, WheelCabinet.DeckHeight).Safe, scale);
        Assert.Equal(board.Cards.Min.X, board.Card(0).Min.X, 3);
        Assert.Equal(board.Cards.Max.X, board.Card(WheelRules.SpotCount - 1).Max.X, 3);
        for (var spot = 1; spot < WheelRules.SpotCount; spot++)
        {
            var previous = board.Card(spot - 1);
            var card = board.Card(spot);
            Assert.Equal(previous.Width, card.Width, 3);
            Assert.Equal(WheelBoardLayout.CardGap * scale, card.Min.X - previous.Max.X, 3);
        }
    }

    [Fact]
    public void ACrampedSafeNeverInvertsARegion()
    {
        var safe = new Rect(new Vector2(12f, 100f), new Vector2(200f, 180f));
        var board = WheelBoardLayout.Compute(safe, 1f);
        Assert.True(board.Wheel.Height >= 0f);
        Assert.True(board.Caption.Height >= 0f);
        Assert.True(board.Cards.Min.Y >= board.History.Max.Y);
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void TheSpotHintAndTheReturnShareTheCaptionInEveryLanguage(float scale)
    {
        var board = WheelBoardLayout.Compute(WheelTextMeasure.Stage(scale, WheelCabinet.DeckHeight).Safe, scale);
        var hints = WheelTextMeasure.Translations(L.Casino.WheelPickSpot);
        var returns = WheelTextMeasure.Translations(L.Strip.PaysBackShort);
        for (var language = 0; language < hints.Length; language++)
        {
            var returnWidth = WheelTextMeasure.Width(string.Format(returns[language], "92-96"),
                WheelSpotCaption.ReturnStyle, scale);
            var hintWidth = WheelTextMeasure.Width(hints[language], WheelSpotCaption.HintStyle, scale);
            Assert.True(hintWidth <= WheelSpotCaption.HintRoom(board.Caption.Width, returnWidth, scale), hints[language]);
        }
    }
}
