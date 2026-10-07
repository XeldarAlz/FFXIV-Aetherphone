using System.Numerics;
using Aetherphone.Apps.Games;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Hub;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class GamesHomeLayoutTests
{
    [Theory]
    [InlineData(0f, 0.1f, 3, 0)]
    [InlineData(0f, 0.19f, 3, 1)]
    [InlineData(0f, 1.0f, 3, 1)]
    [InlineData(0f, 1.25f, 3, 2)]
    [InlineData(1f, 0.85f, 3, 1)]
    [InlineData(1f, 0.8f, 3, 0)]
    [InlineData(2f, 2.4f, 3, 2)]
    [InlineData(0f, -0.4f, 3, 0)]
    [InlineData(0f, 0.6f, 1, 0)]
    public void AReleaseAdvancesOnlyPastTheFlingFraction(float pressPosition, float position, int count,
        int expected)
    {
        Assert.Equal(expected, PageSnap.Target(pressPosition, position, count));
    }

    [Theory]
    [InlineData(328f, 1f, 255.84f)]
    [InlineData(200f, 1f, 236f)]
    [InlineData(420f, 1f, 300f)]
    [InlineData(492f, 1.5f, 383.76f)]
    public void TheHeroCardHeightFollowsTheWidthWithinItsClamp(float width, float scale, float expected)
    {
        Assert.Equal(expected, HeroCarousel.CardHeight(width, scale), 2);
    }

    [Fact]
    public void TheHeroBlockReservesTheDotRowBelowTheCard()
    {
        Assert.Equal(HeroCarousel.CardHeight(328f, 1f) + 24f, HeroCarousel.BlockHeight(328f, 1f), 3);
        Assert.Equal(340f, HeroCarousel.Stride(328f, 1f));
    }

    [Theory]
    [InlineData(0, 3, 1)]
    [InlineData(2, 3, 0)]
    [InlineData(0, 1, 0)]
    public void AutoAdvanceWrapsToTheFirstPage(int page, int count, int expected)
    {
        Assert.Equal(expected, HeroCarousel.Next(page, count));
    }

    [Theory]
    [InlineData(20000, 3, 2)]
    [InlineData(20001, 3, 0)]
    [InlineData(5, 0, -1)]
    [InlineData(-4, 3, 2)]
    public void TheSpotlightRotatesThroughTheCandidatesByDay(int today, int candidates, int expected)
    {
        Assert.Equal(expected, HeroCarousel.SpotlightSlot(today, candidates));
    }

    [Fact]
    public void TheDailyPageAlwaysLeadsAndHiddenPagesDropOut()
    {
        var carousel = new HeroCarousel();
        carousel.SetPages(true, true);

        Assert.Equal(3, carousel.Count);
        Assert.Equal(HeroPage.Daily, carousel.PageAt(0));
        Assert.Equal(HeroPage.Spotlight, carousel.PageAt(1));
        Assert.Equal(HeroPage.Together, carousel.PageAt(2));

        carousel.SetPages(false, true);

        Assert.Equal(2, carousel.Count);
        Assert.Equal(HeroPage.Together, carousel.PageAt(1));

        carousel.SetPages(false, false);

        Assert.Equal(1, carousel.Count);
        Assert.Equal(0, carousel.Page);
    }

    [Theory]
    [InlineData(0f, 40f, 316f, 1000f, 0f)]
    [InlineData(0f, 60f, 316f, 1000f, 316f)]
    [InlineData(316f, 260f, 316f, 1000f, 316f)]
    [InlineData(316f, 250f, 316f, 1000f, 0f)]
    [InlineData(316f, 300f, 316f, 1000f, 316f)]
    [InlineData(632f, 900f, 316f, 920f, 920f)]
    [InlineData(0f, 30f, 316f, 0f, 0f)]
    public void ARailSettlesOnTheNearestColumn(float pressOffset, float offset, float stride, float maxOffset,
        float expected)
    {
        Assert.Equal(expected, SnapRail.SnapOffset(pressOffset, offset, stride, maxOffset), 3);
    }

    [Fact]
    public void RailContentSpansBothBleedsAndEveryGap()
    {
        Assert.Equal(16f * 2f + 3f * 300f + 2f * 16f, SnapRail.ContentWidth(3, 300f, 16f, 16f));
        Assert.Equal(0f, SnapRail.ContentWidth(0, 300f, 16f, 16f));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(13, 5)]
    public void ShelvesStackThreeGamesPerColumn(int entries, int expected)
    {
        Assert.Equal(expected, ShelfColumns.ColumnCount(entries));
    }

    [Fact]
    public void AShelfColumnLeavesTheNextOnePeeking()
    {
        Assert.Equal(300f, ShelfColumns.ColumnWidth(328f, 1f));
        Assert.Equal(204f, ShelfColumns.Height(1f));
        Assert.Equal(306f, ShelfColumns.Height(1.5f));
    }

    [Theory]
    [InlineData(328f, 276f)]
    [InlineData(492f, 413f)]
    public void EditorialCardsTakeMostOfTheWidth(float width, float expected)
    {
        Assert.Equal(expected, PosterCard.EditorialWidth(width));
    }

    [Fact]
    public void PostersKeepADarkGroundUnderWhiteType()
    {
        Assert.Equal(Backdrop.Nebula, PosterCard.PosterBackdrop(Backdrop.Paper));
        Assert.Equal(Backdrop.Nebula, PosterCard.PosterBackdrop(Backdrop.Meadow));
        Assert.Equal(Backdrop.Neon, PosterCard.PosterBackdrop(Backdrop.Neon));
        Assert.Equal(Backdrop.Sky, PosterCard.PosterBackdrop(Backdrop.Sky));
    }

    [Theory]
    [InlineData("Stack the blocks as high as you can.", "Stack the blocks", "fallback", "as high as you can.")]
    [InlineData("Tap to flap through the gaps and dodge every pipe", "Tap to flap through", "the gaps",
        "the gaps and dodge every pipe")]
    [InlineData("go go go go go", "go go", "go go", "go go go")]
    [InlineData("Stack the blocks", "Other", "fallback", "fallback")]
    [InlineData("Stack", "", "fallback", "fallback")]
    public void TheSecondHookLineCarriesTheRestOfTheSentence(string text, string firstLine, string fallback,
        string expected)
    {
        Assert.Equal(expected, ClampedLines.Remainder(text, firstLine, fallback));
    }

    [Fact]
    public void TheGroundMatchesTheAppGradientAtBothEnds()
    {
        var palette = AppPalettes.Games;
        var ground = new HubGround(palette, new Rect(new Vector2(0f, 100f), new Vector2(360f, 900f)));
        var top = ground.At(100f);
        var bottom = ground.At(900f);

        AssertBlend(palette.BackdropTop, palette.BloomTop, top);
        AssertBlend(palette.BackdropBottom, palette.BloomBottom, bottom);
        Assert.Equal(1f, top.W);
    }

    [Theory]
    [InlineData("tetris", true)]
    [InlineData("minigolf", true)]
    [InlineData("doom", false)]
    [InlineData("trivia", false)]
    [InlineData("moogleclicker", false)]
    public void OnlyAllowlistedGamesRunTheLivePreview(string gameId, bool expected)
    {
        Assert.Equal(expected, LivePreview.Allows(gameId));
    }

    [Theory]
    [InlineData(0f, 316f, 1f, 316f)]
    [InlineData(330f, 316f, 1f, 632f)]
    [InlineData(330f, 316f, -1f, 0f)]
    [InlineData(100f, 0f, 1f, 340f)]
    public void ArrowPagingStepsOneColumn(float offset, float stride, float direction, float expected)
    {
        Assert.Equal(expected, TileRail.PageStep(offset, 300f, stride, direction), 3);
    }

    private static void AssertBlend(Vector4 body, Vector4 bloom, Vector4 actual)
    {
        var alpha = Math.Clamp(bloom.W, 0f, 1f);
        Assert.Equal(body.X + (bloom.X - body.X) * alpha, actual.X, 4);
        Assert.Equal(body.Y + (bloom.Y - body.Y) * alpha, actual.Y, 4);
        Assert.Equal(body.Z + (bloom.Z - body.Z) * alpha, actual.Z, 4);
    }
}
