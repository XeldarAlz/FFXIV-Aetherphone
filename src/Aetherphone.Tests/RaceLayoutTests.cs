using System.Numerics;
using Aetherphone.Apps.Casino.Race;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RaceLayoutTests
{
    private static readonly Rect LandscapeSafe = new(new Vector2(12f, 64f), new Vector2(832f, 378f));
    private static readonly Rect PortraitSafe = new(new Vector2(12f, 104f), new Vector2(378f, 668f));
    private static readonly Rect PortraitDeck = new(new Vector2(0f, 680f), new Vector2(390f, 844f));

    private static bool Inside(Rect inner, Rect outer) =>
        inner.Min.X >= outer.Min.X - 0.01f && inner.Min.Y >= outer.Min.Y - 0.01f
        && inner.Max.X <= outer.Max.X + 0.01f && inner.Max.Y <= outer.Max.Y + 0.01f;

    private static bool Apart(Rect first, Rect second) =>
        first.Max.X <= second.Min.X + 0.01f || second.Max.X <= first.Min.X + 0.01f
        || first.Max.Y <= second.Min.Y + 0.01f || second.Max.Y <= first.Min.Y + 0.01f;

    [Fact]
    public void LandscapeBettingPutsTheBoardLeftAndTheRailRight()
    {
        var layout = RaceLayout.Compute(LandscapeSafe, default, true, false, true, RaceCabinet.DeckHeight, 1f);
        Assert.True(layout.Landscape);
        Assert.True(layout.HasHeader);
        Assert.True(layout.HasDeck);
        Assert.True(Inside(layout.Main, LandscapeSafe));
        Assert.True(Inside(layout.Side, LandscapeSafe));
        Assert.True(Inside(layout.Deck, LandscapeSafe));
        Assert.True(Apart(layout.Main, layout.Side));
        Assert.True(Apart(layout.Main, layout.Deck));
        Assert.True(Apart(layout.Side, layout.Deck));
        Assert.True(layout.Main.Width >= LandscapeSafe.Width * 0.5f - RaceLayout.Gap);
        Assert.InRange(layout.Side.Width, RaceLayout.SideMin, RaceLayout.SideMax);
        Assert.Equal(RaceCabinet.DeckHeight, layout.Deck.Height, 2);
    }

    [Fact]
    public void LandscapeRacingGivesTheTrackTheWholeWidth()
    {
        var layout = RaceLayout.Compute(LandscapeSafe, default, true, true, false, RaceCabinet.DeckHeight, 1f);
        Assert.True(layout.Racing);
        Assert.Equal(LandscapeSafe.Width, layout.Main.Width, 2);
        Assert.Equal(LandscapeSafe.Width, layout.Header.Width, 2);
        Assert.True(Apart(layout.Main, layout.Ticker));
        Assert.True(Apart(layout.Header, layout.Main));
        Assert.True(Inside(layout.Ticker, LandscapeSafe));
        Assert.False(layout.HasDeck);
    }

    [Fact]
    public void PortraitBettingStacksBoardOverTicketsAndUsesTheStageDeck()
    {
        var layout = RaceLayout.Compute(PortraitSafe, PortraitDeck, false, false, true, RaceCabinet.DeckHeight, 1f);
        Assert.False(layout.Landscape);
        Assert.False(layout.HasHeader);
        Assert.Equal(PortraitDeck, layout.Deck);
        Assert.True(Inside(layout.Main, PortraitSafe));
        Assert.True(Inside(layout.Side, PortraitSafe));
        Assert.True(Apart(layout.Main, layout.Side));
        Assert.True(layout.Main.Max.Y <= layout.Side.Min.Y);
    }

    [Fact]
    public void PortraitRacingKeepsATwoRowTicker()
    {
        var layout = RaceLayout.Compute(PortraitSafe, PortraitDeck, false, true, false, 0f, 1f);
        Assert.Equal(RaceLayout.TickerHeightStacked, layout.Ticker.Height, 2);
        Assert.True(Apart(layout.Main, layout.Ticker));
        Assert.Equal(PortraitSafe.Min.Y, layout.Main.Min.Y, 2);
    }

    [Fact]
    public void ATinySafeAreaNeverInvertsARect()
    {
        var tiny = new Rect(new Vector2(0f, 0f), new Vector2(40f, 30f));
        for (var mode = 0; mode < 4; mode++)
        {
            var layout = RaceLayout.Compute(tiny, tiny, mode % 2 == 0, mode >= 2, true, RaceCabinet.DeckHeight, 2f);
            Assert.True(layout.Main.Width >= 0f && layout.Main.Height >= 0f);
            Assert.True(layout.Side.Width >= 0f && layout.Side.Height >= 0f);
            Assert.True(layout.Ticker.Width >= 0f && layout.Ticker.Height >= 0f);
        }
    }

    [Fact]
    public void TheBuilderPicksOneRunnerForSinglesAndTwoForPairs()
    {
        var builder = new RaceTicketBuilder();
        builder.Follow(4);
        Assert.False(builder.Ready);
        builder.Tap(3);
        Assert.True(builder.Ready);
        Assert.Equal(RaceRules.NoRunner, builder.RunnerB);
        builder.Tap(5);
        Assert.Equal(5, builder.First);
        builder.SetKind(RaceRules.KindForecast);
        Assert.True(builder.WantsSecond);
        Assert.False(builder.Ready);
        builder.Tap(2);
        Assert.True(builder.Ready);
        Assert.Equal(2, builder.RunnerB);
        Assert.Equal(0, builder.PickOf(5));
        Assert.Equal(1, builder.PickOf(2));
        builder.Tap(5);
        Assert.Equal(2, builder.First);
        Assert.Equal(RaceRules.NoRunner, builder.Second);
        builder.SetKind(RaceRules.KindPlace);
        Assert.True(builder.Ready);
        builder.Follow(5);
        Assert.False(builder.Ready);
        Assert.Equal(RaceRules.KindPlace, builder.Kind);
    }

    [Fact]
    public void LettingItRideFloorsThePayoutToTheLadderAndTheCeiling()
    {
        Assert.Equal(10_000, RaceTicketBuilder.RideAmount(13_500, 1_000_000, 1_000_000));
        Assert.Equal(5_000, RaceTicketBuilder.RideAmount(13_500, 7_500, 1_000_000));
        Assert.Equal(2_500, RaceTicketBuilder.RideAmount(13_500, 1_000_000, 3_000));
        Assert.Equal(0, RaceTicketBuilder.RideAmount(50, 1_000_000, 1_000_000));
        Assert.True(CasinoLadder.IsRung(RaceTicketBuilder.RideAmount(987_654, 10_000_000, 10_000_000)));
    }
}
