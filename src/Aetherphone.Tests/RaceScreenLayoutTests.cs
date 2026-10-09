using System.Numerics;
using Aetherphone.Apps.Casino.Race;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Xunit;

namespace Aetherphone.Tests;

public sealed class RaceScreenLayoutTests
{
    private const float Tolerance = 0.01f;

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

    private static CasinoStageLayout Stage(Rect full, float deckHeight, float scale) =>
        CasinoStageLayout.Compute(full, false, false, deckHeight, scale);

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
                    Assert.Equal(notice > 0f, layout.HasNotice);
                    AssertAllApart(layout.List, layout.Tickets, layout.Notice, layout.Deck, stage.Band);
                }
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
            var layout = RaceDeckLayout.Compute(deck, rides[index], scale);
            AssertAllApart(layout.Segment, layout.Field, layout.Half, layout.Double, layout.Max, layout.Ride,
                layout.Primary);
            Assert.True(Inside(layout.Segment, deck));
            Assert.True(Inside(layout.Field, deck));
            Assert.True(Inside(layout.Max, deck));
            Assert.True(Inside(layout.Primary, deck));
            Assert.True(layout.Primary.Width >= RaceDeckLayout.PrimaryMinWidth * scale - Tolerance);
            Assert.True(layout.Primary.Height >= 44f * scale);
            Assert.True(layout.Half.Height >= 36f * scale);
            Assert.Equal(rides[index] > 0f, layout.HasRide);
        }
    }

    [Theory]
    [MemberData(nameof(AllScales))]
    public void TheCountdownChipSitsInTheBandBetweenTheCapsuleAndTheInfoChip(float scale)
    {
        for (var phone = 0; phone < PortraitPhones.Length; phone++)
        {
            var stage = Stage(Phone(PortraitPhones[phone], scale), RaceCabinet.DeckHeight, scale);
            var capsuleWidth = RaceCountdownLayout.CapsuleWidth(48f * scale, stage.CapsuleMaxWidth, scale);
            var chip = RaceCountdownLayout.Compute(stage.CapsuleCenter, capsuleWidth, stage.InfoCenter,
                stage.ChipRadiusPixels, scale, out _);
            Assert.True(chip.Width >= RaceCountdownLayout.NarrowWidth * scale - Tolerance);
            Assert.True(Inside(chip, stage.Band));
            var capsule = new Rect(stage.CapsuleCenter - new Vector2(capsuleWidth * 0.5f, 17f * scale),
                stage.CapsuleCenter + new Vector2(capsuleWidth * 0.5f, 17f * scale));
            var info = new Rect(stage.InfoCenter - new Vector2(stage.ChipRadiusPixels, stage.ChipRadiusPixels),
                stage.InfoCenter + new Vector2(stage.ChipRadiusPixels, stage.ChipRadiusPixels));
            AssertAllApart(chip, capsule, info);
        }
    }

    [Fact]
    public void AWideBalanceShrinksTheChipThenHidesIt()
    {
        var stage = Stage(Phone(PortraitPhones[1], 1f), RaceCabinet.DeckHeight, 1f);
        var roomy = RaceCountdownLayout.Compute(stage.CapsuleCenter, RaceCountdownLayout.CapsuleWidth(8f,
            stage.CapsuleMaxWidth, 1f), stage.InfoCenter, stage.ChipRadiusPixels, 1f, out var roomyWide);
        Assert.True(roomyWide);
        Assert.Equal(RaceCountdownLayout.WideWidth, roomy.Width, 2);
        var narrowCapsule = RaceCountdownLayout.CapsuleWidth(64f, stage.CapsuleMaxWidth, 1f);
        var narrow = RaceCountdownLayout.Compute(stage.CapsuleCenter, narrowCapsule, stage.InfoCenter,
            stage.ChipRadiusPixels, 1f, out var wide);
        Assert.False(wide);
        Assert.Equal(RaceCountdownLayout.NarrowWidth, narrow.Width, 2);
        var hidden = RaceCountdownLayout.Compute(stage.CapsuleCenter, stage.CapsuleMaxWidth, stage.InfoCenter,
            stage.ChipRadiusPixels, 1f, out _);
        Assert.Equal(0f, hidden.Width);
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
            Assert.True(layout.Track.Height >= full.Height * 0.7f);
            Assert.True(RaceTrackView.VerticalBirdLength(layout.Track) >= 36f * scale);
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
        Assert.True(layout.Caption.Min.Y >= layout.Track.Max.Y);
        Assert.True(layout.Caption.Width >= full.Width * 0.4f);
        Assert.Equal(layout.Track.Min.Y, layout.Stand.Max.Y, 2);
        AssertAllApart(layout.Progress, layout.Track, layout.TopThree, layout.Caption, stage.Band);
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
