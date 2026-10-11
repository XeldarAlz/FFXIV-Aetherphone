using System.Numerics;
using Aetherphone.Apps.Casino.Race;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RaceDeckLayoutTests
{
    private const float Tolerance = 0.01f;

    private static readonly float[] Scales = { 0.75f, 1f, 1.25f, 1.5f };

    private static readonly Vector2[] LandscapePhones = { new(844f, 390f), new(740f, 360f), new(932f, 430f) };

    [Fact]
    public void TheDeckIsExactlyItsRows()
    {
        var rows = RaceDeckLayout.Pad * 2f + RaceDeckLayout.SegmentHeight + RaceDeckLayout.Gap
            + RaceDeckLayout.AmountHeight + RaceDeckLayout.Gap + RaceDeckLayout.CaptionHeight
            + RaceDeckLayout.CaptionGap + RaceDeckLayout.ActionHeight;
        Assert.Equal(RaceDeckLayout.Height, rows, 3);
        Assert.Equal(DeckActions.SecondaryHeight, RaceDeckLayout.SegmentHeight);
        Assert.Equal(DeckActions.SecondaryHeight, RaceDeckLayout.AmountHeight);
        Assert.Equal(DeckActions.PrimaryHeight, RaceDeckLayout.ActionHeight);
    }

    [Fact]
    public void PortraitQuickChipsHoldTheWidestMaxInEveryLanguage()
    {
        var widths = new[]
        {
            BetDeckLayoutTests.ScreenWidth(PhoneCaseKind.Art), BetDeckLayoutTests.ScreenWidth(PhoneCaseKind.Color),
        };
        for (var scaleIndex = 0; scaleIndex < Scales.Length; scaleIndex++)
        {
            var scale = Scales[scaleIndex];
            var quick = BetDeckLayoutTests.Quick(DeckLabelMeasure.Widest(L.Casino.BetMax, scale), scale);
            for (var widthIndex = 0; widthIndex < widths.Length; widthIndex++)
            {
                var layout = RaceDeckLayout.Compute(Deck(widths[widthIndex] * scale, scale), quick, 0f, 0f, scale);
                var amount = layout.Amount;
                Assert.True(amount.HasHalf && amount.HasDouble && amount.HasMax);
                AssertHolds(amount.Half, quick.Half, scale);
                AssertHolds(amount.Double, quick.Double, scale);
                AssertHolds(amount.Max, quick.Max, scale);
                Assert.True(amount.Field.Width >= DeckAmountRow.FieldMinWidth * scale - Tolerance);
            }
        }
    }

    [Fact]
    public void TheLandscapeRailKeepsMaxAndEveryChipItShowsWhole()
    {
        for (var scaleIndex = 0; scaleIndex < Scales.Length; scaleIndex++)
        {
            var scale = Scales[scaleIndex];
            var quick = BetDeckLayoutTests.Quick(DeckLabelMeasure.Widest(L.Casino.BetMax, scale), scale);
            for (var phone = 0; phone < LandscapePhones.Length; phone++)
            {
                var full = new Rect(Vector2.Zero, LandscapePhones[phone] * scale);
                var stage = CasinoStageLayout.Compute(full, false, false, 0f, scale);
                var open = RaceOpenLayout.Compute(stage.Safe, stage.Deck, true, 0, false, 0f, scale);
                var layout = RaceDeckLayout.Compute(open.Deck, quick, 0f, 0f, scale);
                var amount = layout.Amount;
                Assert.True(amount.HasMax);
                AssertHolds(amount.Max, quick.Max, scale);
                if (amount.HasHalf)
                {
                    AssertHolds(amount.Half, quick.Half, scale);
                }

                if (amount.HasDouble)
                {
                    AssertHolds(amount.Double, quick.Double, scale);
                }

                Assert.True(amount.Field.Width >= DeckAmountRow.FieldMinWidth * scale - Tolerance);
                RaceScreenLayoutTests.AssertAllApart(amount.Field, amount.Half, amount.Double, amount.Max);
            }
        }
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void TheCaptionSitsBetweenTheAmountRowAndTheButton(float scale)
    {
        var deck = Deck(BetDeckLayoutTests.ScreenWidth(PhoneCaseKind.Color) * scale, scale);
        var rides = new[] { 0f, DeckActions.PillWidth(90f * scale, scale) };
        for (var index = 0; index < rides.Length; index++)
        {
            var layout = RaceDeckLayout.Compute(deck, BetDeckLayoutTests.Quick("Max", scale), rides[index],
                RaceDeckLayout.ActionHeight * scale, scale);
            var amount = layout.Amount;
            Assert.False(layout.RideCompact);
            Assert.Equal(RaceDeckLayout.CaptionHeight * scale, layout.Caption.Height, 2);
            Assert.Equal(deck.Width - RaceDeckLayout.Pad * 2f * scale, layout.Caption.Width, 2);
            Assert.True(layout.Caption.Min.Y >= amount.Field.Max.Y + RaceDeckLayout.Gap * scale - Tolerance);
            Assert.Equal(layout.Primary.Min.Y - RaceDeckLayout.CaptionGap * scale, layout.Caption.Max.Y, 2);
            Assert.Equal(deck.Max.Y - RaceDeckLayout.Pad * scale, layout.Primary.Max.Y, 2);
            Assert.Equal(RaceDeckLayout.ActionHeight * scale, layout.Primary.Height, 2);
            Assert.True(layout.Primary.Width >= RaceDeckLayout.PrimaryMinWidth * scale - Tolerance);
            Assert.Equal(rides[index] > 0f, layout.HasRide);
            if (layout.HasRide)
            {
                Assert.Equal(rides[index], layout.Ride.Width, 2);
                Assert.Equal(layout.Ride.Height, layout.Primary.Height, 2);
            }

            RaceScreenLayoutTests.AssertAllApart(layout.Segment, amount.Field, amount.Half, amount.Double,
                amount.Max, layout.Caption, layout.Ride, layout.Primary);
        }
    }

    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void ALetItRideLabelTooLongForTheRowFoldsToItsIconAndAmount(float scale)
    {
        var deck = Deck(BetDeckLayoutTests.ScreenWidth(PhoneCaseKind.Art) * scale, scale);
        var widest = DeckLabelMeasure.Widest(L.Race.LetItRide, scale).Replace("{0}", "12.5K");
        var full = DeckActions.PillWidth(DeckLabelMeasure.Width(widest, scale), scale);
        var compact = DeckActions.IconPillWidth(DeckLabelMeasure.Width("12.5K", scale),
            RaceDeckLayout.ActionHeight * scale, scale);
        var layout = RaceDeckLayout.Compute(deck, BetDeckLayoutTests.Quick("Max", scale), full, compact, scale);
        Assert.True(layout.HasRide);
        Assert.True(layout.RideCompact);
        Assert.Equal(compact, layout.Ride.Width, 2);
        Assert.True(layout.Primary.Width >= RaceDeckLayout.PrimaryMinWidth * scale - Tolerance);
        RaceScreenLayoutTests.AssertAllApart(layout.Ride, layout.Primary, layout.Caption);
    }

    private static Rect Deck(float width, float scale) =>
        new(new Vector2(0f, 600f * scale), new Vector2(width, 600f * scale + RaceDeckLayout.Height * scale));

    private static void AssertHolds(Rect chip, float labelWidth, float scale)
    {
        Assert.True(chip.Width >= labelWidth + DeckActions.PillPadX * 2f * scale - Tolerance, chip.Width.ToString());
        Assert.Equal(RaceDeckLayout.AmountHeight * scale, chip.Height, 2);
    }
}
