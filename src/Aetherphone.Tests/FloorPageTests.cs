using Aetherphone.Apps.Casino;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Casino.Strip;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Xunit;

namespace Aetherphone.Tests;

public sealed class FloorPageTests
{
    private const float PhoneContent = 328f;

    [Fact]
    public void CardsAndSlotsTakeThreeColumnsAndQuickBetsTakeFour()
    {
        Assert.Equal(3, StripCatalog.ColumnsOf(StripShelf.Cards));
        Assert.Equal(3, StripCatalog.ColumnsOf(StripShelf.Machines));
        Assert.Equal(4, StripCatalog.ColumnsOf(StripShelf.QuickBets));
        Assert.Equal(104f, StripGrid.CellWidth(PhoneContent, StripShelves.CardColumns, StripShelves.GridGap), 3);
        Assert.Equal(76f, StripGrid.CellWidth(PhoneContent, StripShelves.QuickColumns, StripShelves.GridGap), 3);
    }

    [Fact]
    public void EveryShelfPicksItsPosterLayoutAndSubline()
    {
        Assert.True(StripCatalog.LayoutOf(StripShelf.Cards).CapsuleBand);
        Assert.False(StripCatalog.LayoutOf(StripShelf.Machines).CapsuleBand);
        Assert.True(StripCatalog.LayoutOf(StripShelf.QuickBets).CapsuleBand);
        Assert.True(StripCatalog.LayoutOf(StripShelf.QuickBets).Centered);
        Assert.Equal(L.FloorPage.CardsSubline.Key, StripCatalog.SublineOf(StripShelf.Cards).Key);
        Assert.Equal(L.FloorPage.SlotsSubline.Key, StripCatalog.SublineOf(StripShelf.Machines).Key);
        Assert.Equal(L.FloorPage.QuickSubline.Key, StripCatalog.SublineOf(StripShelf.QuickBets).Key);
    }

    [Fact]
    public void DealerHoldemWearsAShortSignSoItReadsInAThirdOfThePhone()
    {
        var cards = StripCatalog.EntriesOf(StripShelf.Cards);
        Assert.Equal("21", StripCatalog.SignOf(cards[0]).ToString());
        Assert.Equal("DEALER", StripCatalog.SignOf(cards[2]).ToString());
        Assert.True(CasinoSigns.Measure(StripCatalog.SignOf(cards[2]), 1f)
                    < CasinoSigns.Measure(CasinoSign.DealerHoldem, 1f));
    }

    [Fact]
    public void TheNeonSignBuzzesButNeverGoesDark()
    {
        Assert.Equal(1f, NeonFlicker.Level(0f, 6.5f), 3);
        Assert.Equal(0.55f, NeonFlicker.Level(0.46f * 6.5f, 6.5f), 3);
        Assert.Equal(1f, NeonFlicker.Level(6.5f, 6.5f), 3);
        Assert.Equal(1f, NeonFlicker.Level(3f, 0f));
        for (var step = 0; step < 200; step++)
        {
            Assert.InRange(NeonFlicker.Level(step * 0.07f, 5f), 0.55f, 1f);
        }
    }

    [Fact]
    public void FloorRoomBarsCountDownTheWindowOfTheRoundPhase()
    {
        Assert.Equal(0.5f, LiveBoard.RingFraction(12_500, 25), 3);
        Assert.Equal(0f, LiveBoard.RingFraction(-5, 25));
        Assert.Equal(1f, LiveBoard.RingFraction(90_000, 25));
        Assert.Equal(CasinoRoomCadence.BingoOpenSeconds,
            LiveBoard.WindowSeconds(CasinoGames.Bingo, CasinoRoomPhases.Open));
        Assert.Equal(CasinoRoomCadence.RaceLockedSeconds,
            LiveBoard.WindowSeconds(CasinoGames.Race, CasinoRoomPhases.Locked));
        Assert.Equal(CasinoRoomCadence.WheelResultSeconds,
            LiveBoard.WindowSeconds(CasinoGames.Wheel, CasinoRoomPhases.Result));
    }

    [Fact]
    public void FloorRoomCardsGrowWithTheirTextButNeverShrinkBelowTheDesign()
    {
        Assert.Equal(FloorRoomCard.MinHeight, FloorRoomCard.Height(20f, 17f, 1f));
        var tall = FloorRoomCard.Height(44f, 36f, 1f);
        Assert.True(tall > FloorRoomCard.MinHeight);
        Assert.Equal(tall * 1.5f, FloorRoomCard.Height(66f, 54f, 1.5f), 3);
        Assert.Equal(84f, FloorRoomCard.TextWidth(104f, 1f));
    }

    [Fact]
    public void EveryPhaseLineGetsAllItsLinesSoTheTimeNeverDrops()
    {
        Assert.Equal(17f, FloorRoomCard.LineBlock(0, 17f));
        Assert.Equal(17f, FloorRoomCard.LineBlock(1, 17f));
        Assert.Equal(51f, FloorRoomCard.LineBlock(3, 17f));
        Assert.Equal(68f, FloorRoomCard.LineBlock(4, 17f));
        Assert.True(FloorRoomCard.Height(20f, FloorRoomCard.LineBlock(4, 17f), 1f)
                    > FloorRoomCard.Height(20f, FloorRoomCard.LineBlock(2, 17f), 1f));
    }

    [Fact]
    public void ShortcutTilesGrowForATwoLineLabel()
    {
        Assert.Equal(FloorShortcuts.TileHeight, FloorShortcuts.Height(18f, 0f, 1f));
        Assert.Equal(6f + 36f + 7f + 6f + 60f, FloorShortcuts.Height(60f, 0f, 1f));
        Assert.Equal(72f, FloorShortcuts.LabelWidth(76f, 1f));
        Assert.Equal(1f, FloorShortcuts.LabelWidth(2f, 1f));
    }

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void ShortcutBadgesSitInTheirOwnBandAboveTheIcon(float scale)
    {
        var badge = FloorShortcuts.BadgeHeight(13f * scale, scale);
        var band = FloorShortcuts.BadgeBand(badge, scale);
        Assert.Equal(0f, FloorShortcuts.BadgeBand(0f, scale));
        Assert.True(FloorShortcuts.BlockTop(band, scale) >= FloorShortcuts.BadgeInset * scale + badge);
        Assert.Equal(FloorShortcuts.Pad * scale, FloorShortcuts.BlockTop(0f, scale), 3);
        var label = 34f * scale;
        Assert.True(FloorShortcuts.Height(label, band, scale) >= band + (FloorShortcuts.Icon + FloorShortcuts.IconGap
            + FloorShortcuts.Pad) * scale + label - 0.01f);
    }

    [Fact]
    public void TheJackpotSnapsToItsFirstLoadedValueThenRolls()
    {
        var roll = default(RollingAmount);
        roll.Snap(0);
        JackpotMarquee.Follow(ref roll, 48_210, 0.016f);
        Assert.Equal(48_210, roll.Display);
        JackpotMarquee.Follow(ref roll, 58_210, 0.016f);
        Assert.Equal(58_210, roll.Target);
        Assert.InRange(roll.Display, 48_211, 58_209);
        var fresh = default(RollingAmount);
        JackpotMarquee.Follow(ref fresh, 1_000, 0.016f);
        Assert.Equal(1_000, fresh.Display);
    }

    [Fact]
    public void SectionSublinesSitBesideTheTitleOnlyWhenBothFit()
    {
        Assert.True(FloorHeader.SublineInline(100f, 150f, PhoneContent, FloorHeader.TrailingGap));
        Assert.False(FloorHeader.SublineInline(200f, 150f, PhoneContent, FloorHeader.TrailingGap));
    }

    [Fact]
    public void TheJackpotMarqueeKeepsItsCapsuleHeightUnlessTheAmountNeedsMore()
    {
        Assert.Equal(JackpotMarquee.Height, JackpotMarquee.HeightFor(24f, 1f));
        Assert.Equal(40f + JackpotMarquee.PadY * 2f, JackpotMarquee.HeightFor(40f, 1f));
    }

    [Fact]
    public void TheCasinoTourPointsAtTheShortcutsInsteadOfTheRecordsRow()
    {
        Assert.True(TourRegistry.TryGetAppTour("casino", out var tour));
        Assert.Equal(5, tour.ContentVersion);
        var anchors = new List<string?>();
        for (var index = 0; index < tour.Steps.Length; index++)
        {
            anchors.Add(tour.Steps[index].AnchorKey);
        }

        Assert.Contains("casino.shelves", anchors);
        Assert.Contains("casino.tab.live", anchors);
        Assert.Equal("casino.shortcuts", anchors[^1]);
        Assert.DoesNotContain("casino.records", anchors);
    }
}
