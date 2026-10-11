using System.Numerics;
using Aetherphone.Apps.Casino.Race;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RaceScreenLayoutTests
{
    private const float Tolerance = 0.01f;
    private const float WrapSlack = 0.8f;

    private static readonly float[] Scales = { 0.75f, 1f, 1.25f, 1.5f };

    private static readonly Vector2[] PortraitPhones = { new(390f, 844f), new(360f, 740f), new(430f, 932f) };

    private static readonly Vector2[] LandscapePhones = { new(844f, 390f), new(740f, 360f), new(932f, 430f) };

    public static TheoryData<float> AllScales()
    {
        var data = new TheoryData<float>();
        for (var index = 0; index < Scales.Length; index++)
        {
            data.Add(Scales[index]);
        }

        return data;
    }

    internal static Rect Phone(Vector2 size, float scale) => new(Vector2.Zero, size * scale);

    internal static bool Inside(Rect inner, Rect outer) =>
        inner.Min.X >= outer.Min.X - Tolerance && inner.Min.Y >= outer.Min.Y - Tolerance
        && inner.Max.X <= outer.Max.X + Tolerance && inner.Max.Y <= outer.Max.Y + Tolerance;

    internal static bool Apart(Rect first, Rect second) =>
        first.Width <= 0f || first.Height <= 0f || second.Width <= 0f || second.Height <= 0f
        || first.Max.X <= second.Min.X + Tolerance || second.Max.X <= first.Min.X + Tolerance
        || first.Max.Y <= second.Min.Y + Tolerance || second.Max.Y <= first.Min.Y + Tolerance;

    internal static void AssertAllApart(params Rect[] rects)
    {
        for (var outer = 0; outer < rects.Length; outer++)
        {
            for (var inner = outer + 1; inner < rects.Length; inner++)
            {
                Assert.True(Apart(rects[outer], rects[inner]), $"rect {outer} overlaps rect {inner}");
            }
        }
    }

    internal static CasinoStageLayout Stage(Rect full, float deckHeight, float scale) =>
        CasinoStageLayout.Compute(full, true, false, deckHeight, scale);

    [Theory]
    [MemberData(nameof(AllScales))]
    public void PortraitBettingNeverOverlapsAtAnyScale(float scale)
    {
        for (var phone = 0; phone < PortraitPhones.Length; phone++)
        {
            var stage = Stage(Phone(PortraitPhones[phone], scale), RaceCabinet.DeckHeight, scale);
            for (var tickets = 0; tickets <= RaceRules.MaxTickets; tickets++)
            {
                for (var mode = 0; mode < 4; mode++)
                {
                    var expanded = mode % 2 == 0;
                    var notice = mode >= 2 ? 56f * scale : 0f;
                    var layout = RaceOpenLayout.Compute(stage.Safe, stage.Deck, false, tickets, expanded, notice, scale);
                    Assert.False(layout.Landscape);
                    Assert.Equal(stage.Deck, layout.Deck);
                    Assert.True(Inside(layout.List, stage.Safe));
                    Assert.True(Inside(layout.Tickets, stage.Safe));
                    Assert.True(Inside(layout.Notice, stage.Safe));
                    Assert.True(layout.List.Height >= RaceOpenLayout.CardHeight * scale * 2f);
                    Assert.True(layout.List.Max.Y <= stage.Deck.Min.Y);
                    Assert.Equal(tickets > 0, layout.HasTickets);
                    Assert.Equal(expanded ? tickets : 0, layout.TicketRows);
                    Assert.True(layout.TicketRowHeight >= RaceOpenLayout.StripRowMin * scale - Tolerance);
                    Assert.True(layout.TicketRowHeight <= RaceOpenLayout.StripRow * scale + Tolerance);
                    Assert.Equal(notice > 0f, layout.HasNotice);
                    AssertAllApart(layout.List, layout.Tickets, layout.Notice, layout.Deck, stage.Band);
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllScales))]
    public void ATallerDeckShortensTheTicketStripBeforeTheField(float scale)
    {
        var extras = new[] { 0f, 24f, 48f };
        for (var phone = 0; phone < PortraitPhones.Length; phone++)
        {
            for (var extra = 0; extra < extras.Length; extra++)
            {
                var stage = Stage(Phone(PortraitPhones[phone], scale), RaceCabinet.DeckHeight + extras[extra], scale);
                var layout = RaceOpenLayout.Compute(stage.Safe, stage.Deck, false, RaceRules.MaxTickets, true,
                    56f * scale, scale);
                Assert.True(layout.List.Height >= RaceOpenLayout.MinimumListHeight * scale - Tolerance,
                    $"field {layout.List.Height / scale} with {extras[extra]} more deck on {PortraitPhones[phone]}");
                Assert.True(layout.HasTickets);
                Assert.True(layout.HasNotice);
                AssertAllApart(layout.List, layout.Tickets, layout.Notice, layout.Deck, stage.Band, stage.Ribbon);
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllScales))]
    public void LandscapeBettingKeepsTheFieldLeftOfTheRail(float scale)
    {
        for (var phone = 0; phone < LandscapePhones.Length; phone++)
        {
            var stage = Stage(Phone(LandscapePhones[phone], scale), 0f, scale);
            for (var tickets = 0; tickets <= RaceRules.MaxTickets; tickets++)
            {
                var layout = RaceOpenLayout.Compute(stage.Safe, stage.Deck, true, tickets, true, 48f * scale, scale);
                Assert.True(layout.Landscape);
                Assert.True(layout.HasRail);
                Assert.True(Inside(layout.List, stage.Safe));
                Assert.True(Inside(layout.Deck, layout.Rail));
                Assert.True(Inside(layout.Tickets, layout.Rail));
                Assert.Equal(RaceDeckLayout.Height * scale, layout.Deck.Height, 2);
                Assert.True(layout.List.Max.X <= layout.Rail.Min.X);
                AssertAllApart(layout.List, layout.Tickets, layout.Notice, layout.Deck);
            }
        }
    }

    [Theory]
    [MemberData(nameof(AllScales))]
    public void TheBetDeckLaysEveryControlOutApart(float scale)
    {
        var deck = new Rect(new Vector2(0f, 600f * scale), new Vector2(390f * scale, 600f * scale + RaceDeckLayout.Height * scale));
        var rides = new[] { 0f, 90f * scale, 400f * scale };
        for (var index = 0; index < rides.Length; index++)
        {
            var layout = RaceDeckLayout.Compute(deck, BetDeckLayoutTests.Quick("Max", scale), rides[index],
                RaceDeckLayout.ActionHeight * scale, scale);
            var amount = layout.Amount;
            AssertAllApart(layout.Segment, amount.Field, amount.Half, amount.Double, amount.Max, layout.Ride,
                layout.Primary);
            Assert.True(Inside(layout.Segment, deck));
            Assert.True(Inside(amount.Field, deck));
            Assert.True(Inside(amount.Max, deck));
            Assert.True(Inside(layout.Primary, deck));
            Assert.True(layout.Primary.Width >= RaceDeckLayout.PrimaryMinWidth * scale - Tolerance);
            Assert.True(layout.Primary.Height >= 44f * scale);
            Assert.True(amount.Half.Height >= 44f * scale - Tolerance);
            Assert.Equal(rides[index] > 0f, layout.HasRide);
        }
    }

    [Theory]
    [MemberData(nameof(AllScales))]
    public void TheLandscapeTrackFillsTheStageWithBigBirds(float scale)
    {
        for (var phone = 0; phone < LandscapePhones.Length; phone++)
        {
            var full = Phone(LandscapePhones[phone], scale);
            var stage = Stage(full, 0f, scale);
            var layout = RaceTrackLayout.Compute(full, stage.Band.Max.Y, scale);
            AssertRaceLayout(layout, full, stage, scale);
            Assert.False(layout.Stacked);
            Assert.True(layout.Caption.Width >= RaceTrackLayout.SideCaptionMin * scale - Tolerance);
            Assert.Equal(layout.TopThree.Min.Y, layout.Caption.Min.Y, 2);
            Assert.Equal(full.Width, layout.Track.Width, 2);
            Assert.True(layout.Track.Height >= full.Height * 0.5f);
            Assert.True(RaceTrackView.SideBirdHeight(layout.Track, scale) >= 36f * scale,
                $"bird {RaceTrackView.SideBirdHeight(layout.Track, scale) / scale} on {LandscapePhones[phone]}");
            RaceTrackView.SideBand(layout.Track, scale, out var farY, out var nearY);
            Assert.True(farY >= layout.Track.Min.Y);
            Assert.True(nearY <= layout.Track.Max.Y);
            var crest = (nearY - farY) * RaceTrackView.FarScale / RaceTrackView.LaneScaleSum
                * (RaceTrackView.CrestShare * RaceTrackView.BirdLaneFactor - RaceTrackView.GroundShare);
            Assert.True(farY - crest >= layout.Track.Min.Y - Tolerance);
        }
    }

    [Theory]
    [MemberData(nameof(AllScales))]
    public void ThePortraitTrackRunsTallWithBigBirds(float scale)
    {
        for (var phone = 0; phone < PortraitPhones.Length; phone++)
        {
            var full = Phone(PortraitPhones[phone], scale);
            var stage = Stage(full, 0f, scale);
            var layout = RaceTrackLayout.Compute(full, stage.Band.Max.Y, scale);
            AssertRaceLayout(layout, full, stage, scale);
            Assert.True(layout.Stacked);
            Assert.Equal(full.Width - RaceTrackLayout.Inset * scale * 2f, layout.Caption.Width, 2);
            Assert.True(layout.Caption.Max.Y <= layout.TopThree.Min.Y);
            Assert.Equal(full.Center.X, layout.TopThree.Center.X, 2);
            Assert.True(layout.Track.Height >= full.Height * 0.7f);
            Assert.True(RaceTrackView.VerticalBirdLength(layout.Track) >= 36f * scale);
        }
    }

    [Fact]
    public void EveryFixedRaceCaptionFitsTheNarrowestPortraitTrack()
    {
        var lines = new[]
        {
            L.Race.Gates, L.Race.Replay, L.Race.PhotoFinish, L.Race.LineOff1, L.Race.LineOff2, L.Race.LineStretch1,
            L.Race.LineStretch2,
        };
        var catalogs = RaceCopy.Catalogs;
        for (var index = 0; index < Scales.Length; index++)
        {
            var scale = Scales[index];
            var full = Phone(PortraitPhones[1], scale);
            var layout = RaceTrackLayout.Compute(full, Stage(full, 0f, scale).Band.Max.Y, scale);
            var room = layout.Caption.Width - layout.Caption.Height - 16f * scale;
            for (var language = 0; language < catalogs.Length; language++)
            {
                for (var line = 0; line < lines.Length; line++)
                {
                    var text = RaceCopy.Text(catalogs[language], lines[line]);
                    Assert.True(RaceCopy.Measure(text, TextStyles.SubheadlineEmphasized, scale, 1f) <= room,
                        $"'{text}' at full size x{scale}");
                    Assert.True(
                        RaceCopy.Measure(text, TextStyles.FootnoteEmphasized, scale, RaceCopy.MaxTextZoom)
                        <= room * RaceHud.CaptionLines * WrapSlack, $"'{text}' at the largest text size x{scale}");
                }
            }
        }
    }

    private static void AssertRaceLayout(in RaceTrackLayout layout, Rect full, in CasinoStageLayout stage, float scale)
    {
        Assert.True(Inside(layout.Progress, full));
        Assert.True(Inside(layout.Track, full));
        Assert.True(Inside(layout.TopThree, full));
        Assert.True(Inside(layout.Caption, full));
        Assert.True(layout.Progress.Height <= 24f * scale + Tolerance);
        Assert.True(layout.Progress.Min.Y >= stage.Band.Max.Y);
        Assert.True(layout.Progress.Max.Y <= stage.Ribbon.Max.Y + Tolerance);
        Assert.True(layout.Caption.Min.Y >= layout.Track.Max.Y);
        Assert.True(layout.Caption.Width >= full.Width * 0.4f);
        Assert.Equal(layout.Track.Min.Y, layout.Stand.Max.Y, 2);
        AssertAllApart(layout.Progress, layout.Track, layout.TopThree, layout.Caption, stage.Band);
    }

    [Theory]
    [MemberData(nameof(AllScales))]
    public void TheResultCardStacksFixedRowsWithoutOverlap(float scale)
    {
        for (var phone = 0; phone < PortraitPhones.Length; phone++)
        {
            var stage = Stage(Phone(PortraitPhones[phone], scale), 0f, scale);
            for (var paid = 0; paid <= RaceRules.MaxTickets; paid++)
            {
                for (var mode = 0; mode < 6; mode++)
                {
                    var dividends = mode % 3 == 0 ? 0 : RaceRules.ResultRows;
                    var expanded = mode % 3 == 2;
                    var lines = mode < 3 ? 1 : RaceResultLayout.HeadlineMaxLines;
                    var layout = RaceResultLayout.Compute(stage.Safe, lines, paid, dividends, expanded, scale);
                    Assert.True(Inside(layout.Card, stage.Safe));
                    Assert.Equal(paid > 0, layout.HasPaid);
                    Assert.Equal((RaceResultLayout.HeadlineHeight + (lines - 1) * RaceResultLayout.HeadlineLineHeight)
                        * scale, layout.Headline.Height, 2);
                    Assert.Equal(RaceResultLayout.ContentWidthFor(stage.Safe, scale), layout.ContentWidth, 2);
                    var content = new Rect(Vector2.Zero, new Vector2(layout.ContentWidth, layout.ContentHeight));
                    var rows = new Rect[5 + paid + (expanded ? dividends : 0)];
                    rows[0] = layout.Headline;
                    rows[1] = layout.Meta;
                    rows[2] = layout.Winner;
                    rows[3] = layout.Second;
                    rows[4] = layout.Third;
                    for (var row = 0; row < paid; row++)
                    {
                        rows[5 + row] = layout.PaidRow(row, scale);
                        Assert.Equal(RaceResultLayout.RowHeight * scale, rows[5 + row].Height, 2);
                    }

                    for (var row = 0; expanded && row < dividends; row++)
                    {
                        rows[5 + paid + row] = layout.DividendRow(row, scale);
                        Assert.Equal(RaceResultLayout.RowHeight * scale, rows[5 + paid + row].Height, 2);
                    }

                    for (var index = 0; index < rows.Length; index++)
                    {
                        Assert.True(Inside(rows[index], content));
                    }

                    AssertAllApart(rows);
                    AssertAllApart(layout.PaidHeader, layout.Toggle, layout.Headline, layout.Winner);
                    Assert.True(Inside(layout.Toggle, content));
                    Assert.True(layout.Viewport(scale).Height > 0f);
                    Assert.True(Inside(layout.Viewport(scale), layout.Card));
                }
            }
        }
    }

    [Fact]
    public void TheResultCardCentresAndCapsItsWidthInLandscape()
    {
        var stage = Stage(Phone(LandscapePhones[0], 1f), 0f, 1f);
        var layout = RaceResultLayout.Compute(stage.Safe, 1, 2, RaceRules.ResultRows, true, 1f);
        Assert.Equal(RaceResultLayout.MaxCardWidth, layout.Card.Width, 2);
        Assert.Equal(stage.Safe.Center.X, layout.Card.Center.X, 2);
        Assert.True(layout.Card.Height <= stage.Safe.Height + Tolerance);
        Assert.True(layout.ContentHeight > layout.Viewport(1f).Height);
    }

    [Fact]
    public void CommentaryFadesInAndOut()
    {
        Assert.Equal(0f, RaceHud.CaptionAlpha(0f, RaceCommentary.LineSeconds));
        Assert.Equal(1f, RaceHud.CaptionAlpha(RaceCommentary.LineSeconds * 0.5f, RaceCommentary.LineSeconds));
        Assert.InRange(RaceHud.CaptionAlpha(RaceCommentary.LineSeconds - 0.1f, RaceCommentary.LineSeconds), 0.01f,
            0.5f);
        Assert.Equal(0f, RaceHud.CaptionAlpha(RaceCommentary.LineSeconds, RaceCommentary.LineSeconds));
    }

    [Fact]
    public void RunnerCardsMeetTheMinimumTouchHeight()
    {
        Assert.True(RaceOpenLayout.CardHeight >= 56f);
        Assert.True(RaceOpenLayout.StripHeader >= 40f);
    }

    [Fact]
    public void TheBoardShowsTheOddsOfTheTicketBeingBuilt()
    {
        var runner = new Core.Aethernet.Contracts.CasinoRaceRunnerDto(OddsHundredths: 562, PlaceOddsHundredths: 215);
        Assert.Equal(562, RaceFieldList.OddsFor(runner, RaceRules.KindWin));
        Assert.Equal(215, RaceFieldList.OddsFor(runner, RaceRules.KindPlace));
        Assert.Equal(562, RaceFieldList.OddsFor(runner, RaceRules.KindForecast));
        Assert.Equal(562, RaceFieldList.OddsFor(runner, RaceRules.KindReverse));
    }

    [Fact]
    public void FormRatingsFoldOntoThreeStars()
    {
        Assert.Equal(1, RaceFieldList.Stars(RaceRules.MinRating));
        Assert.Equal(1, RaceFieldList.Stars(2));
        Assert.Equal(2, RaceFieldList.Stars(3));
        Assert.Equal(2, RaceFieldList.Stars(4));
        Assert.Equal(3, RaceFieldList.Stars(RaceRules.MaxRating));
        Assert.Equal(1, RaceFieldList.Stars(0));
    }
}
