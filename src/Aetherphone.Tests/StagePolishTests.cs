using System.Numerics;
using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class StagePolishTests
{
    private const float NightCeiling = 0.22f;
    private const float EdgeCeiling = 0.08f;
    private const float SmallScale = 0.75f;

    private static readonly Rect Table = new(new Vector2(12f, 120f), new Vector2(348f, 640f));
    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);

    [Fact]
    public void NightFeltLampCentreStaysUnderTheCeilingWithTheWeave()
    {
        var centre = Palette.Luminance(StageBackdrop.NightFeltCenter);
        var woven = Over(centre, White, StageBackdrop.NightWeaveAlpha);
        Assert.True(centre <= NightCeiling, centre.ToString());
        Assert.True(woven <= NightCeiling, woven.ToString());
    }

    [Fact]
    public void NightFeltEdgesFallToTheEdgeLevel()
    {
        var edge = Palette.Luminance(StageBackdrop.NightFeltEdge);
        Assert.True(edge <= EdgeCeiling, edge.ToString());
        Assert.True(Palette.Luminance(StageBackdrop.RailWood) <= NightCeiling);
    }

    [Fact]
    public void NightFeltIsADesaturatedEmerald()
    {
        var centre = StageBackdrop.NightFeltCenter;
        Assert.True(centre.Y > centre.X && centre.Y > centre.Z);
        Assert.True(centre.Y - MathF.Min(centre.X, centre.Z) < 0.12f);
    }

    [Fact]
    public void StripRoseHazeStaysUnderTheNightCeiling()
    {
        var level = HazeStack(StageBackdrop.NeonRose, StageBackdrop.RoseHazeAlpha);
        Assert.True(level <= NightCeiling, level.ToString());
    }

    [Fact]
    public void StripCyanHazeStaysUnderTheNightCeiling()
    {
        var level = HazeStack(StageBackdrop.NeonCyan, StageBackdrop.CyanHazeAlpha);
        Assert.True(level <= NightCeiling, level.ToString());
    }

    [Fact]
    public void ArenaConesStayUnderTheNightCeilingWhereAllFourCross()
    {
        var level = Palette.Luminance(StageBackdrop.ArenaTop);
        var layerAlpha = StageBackdrop.ConeAlpha / StageBackdrop.ConeLayers * StageBackdrop.ConeLayerGain;
        for (var cone = 0; cone < 4; cone++)
        {
            for (var layer = 0; layer < StageBackdrop.ConeLayers; layer++)
            {
                level = Over(level, StageBackdrop.Floodlight, layerAlpha);
            }
        }

        Assert.True(level <= NightCeiling, level.ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EveryHelperHoldsItsMinimumAtSmallScale(int roleIndex)
    {
        var role = (StageTextRole)roleIndex;
        var minimum = StageText.Minimum(role);
        var tiny = StageText.Plan(role, TextStyles.Caption2, SmallScale);
        var large = StageText.Plan(role, TextStyles.LargeTitle, SmallScale);
        Assert.True(tiny.Style.Scale >= minimum.Scale);
        Assert.Equal(TextStyles.LargeTitle.Scale, large.Style.Scale);
        Assert.True(tiny.MinimumCapsuleHeight >= StageText.MinimumCapsuleHeight * SmallScale);
        Assert.True(tiny.ShadowOffset >= 1f);
        Assert.True(tiny.CapsuleHeight(0f) >= tiny.MinimumCapsuleHeight);
    }

    [Fact]
    public void TheMinimumsFollowTheLegibilityRules()
    {
        Assert.True(StageText.Minimum(StageTextRole.State).Scale >= TextStyles.Title2.Scale);
        Assert.True(StageText.Minimum(StageTextRole.Amount).Scale >= TextStyles.Title3.Scale);
        Assert.True(StageText.Minimum(StageTextRole.Status).Scale >= TextStyles.Footnote.Scale);
        Assert.True(StageText.Minimum(StageTextRole.Label).Scale >= TextStyles.Footnote.Scale);
    }

    [Fact]
    public void StrongInkAndGoldReadOnTheBrightestFelt()
    {
        var felt = StageBackdrop.NightFeltCenter;
        Assert.True(StageContrast.Ratio(StageText.Strong, felt) >= StageContrast.Readable);
        Assert.True(StageContrast.Ratio(CasinoColors.Money, felt) >= StageContrast.Readable);
    }

    [Fact]
    public void MutedInkOnlyReadsOnTheCapsule()
    {
        var felt = StageBackdrop.NightFeltCenter;
        var capsule = StageContrast.Over(StageText.CapsuleFill, felt);
        Assert.True(StageContrast.Ratio(StageText.Muted, felt) < StageContrast.Readable);
        Assert.True(StageContrast.Ratio(StageText.Muted, capsule) >= StageContrast.Readable);
        Assert.True(StageContrast.Ratio(StageText.Strong, capsule) >= StageContrast.Readable);
    }

    [Fact]
    public void ContrastRatioMatchesTheReference()
    {
        Assert.Equal(21f, StageContrast.Ratio(White, Black), 2);
        Assert.Equal(1f, StageContrast.Ratio(White, White), 3);
    }

    [Theory]
    [InlineData(1, 1f)]
    [InlineData(5, 1f)]
    [InlineData(6, 1f)]
    [InlineData(7, 1f)]
    [InlineData(6, SmallScale)]
    public void FeltSeatsAreLargeAndStayOnTheTable(int seats, float scale)
    {
        var geometry = FeltTableGeometry.Compute(Table, seats, scale);
        Assert.Equal(seats, geometry.SeatCount);
        Assert.True(geometry.SeatRadius >= SeatSpot.MinimumRadius * scale);
        Assert.True(geometry.CircleRadius >= FeltTableGeometry.MinimumCircleRadius * scale);
        for (var seat = 0; seat < seats; seat++)
        {
            var center = geometry.Seat(seat);
            Assert.True(center.X - geometry.SeatRadius >= Table.Min.X - 0.01f);
            Assert.True(center.X + geometry.SeatRadius <= Table.Max.X + 0.01f);
            Assert.True(center.Y + geometry.SeatRadius <= Table.Max.Y + 0.01f);
            Assert.True(center.Y > geometry.DealerAnchor.Y);
            var circle = geometry.BettingCircle(seat);
            Assert.True(Vector2.Distance(circle, geometry.DealerAnchor) < Vector2.Distance(center, geometry.DealerAnchor));
        }
    }

    [Fact]
    public void TheMiddleSeatSitsBottomCentreUnderTheDealer()
    {
        var geometry = FeltTableGeometry.Compute(Table, 5, 1f);
        var hero = geometry.Seat(2);
        Assert.Equal(Table.Center.X, hero.X, 2);
        Assert.Equal(Table.Center.X, geometry.DealerAnchor.X, 2);
        Assert.True(geometry.DealerAnchor.Y < Table.Min.Y + Table.Height * 0.2f);
        Assert.Equal(Table.Max.Y - geometry.SeatRadius, hero.Y, 1);
    }

    [Fact]
    public void SixSeatsDoNotOverlapOnAPhoneWidthTable()
    {
        var geometry = FeltTableGeometry.Compute(Table, 6, 1f);
        for (var seat = 1; seat < 6; seat++)
        {
            var gap = Vector2.Distance(geometry.Seat(seat), geometry.Seat(seat - 1));
            Assert.True(gap >= geometry.SeatRadius * 2f * 0.9f, gap.ToString());
        }
    }

    [Fact]
    public void ThePrimaryActionIsFullWidthAndFiftySixTall()
    {
        var deck = new Rect(new Vector2(0f, 600f), new Vector2(360f, 760f));
        var row = DeckActions.Row(deck, 1f);
        var primary = DeckActions.Primary(row, row.Min.X);
        Assert.Equal(DeckActions.PrimaryHeight, primary.Height, 3);
        Assert.Equal(deck.Width - DeckActions.Pad * 2f, primary.Width, 3);
        Assert.Equal(deck.Max.Y - DeckActions.Pad, primary.Max.Y, 3);
    }

    [Fact]
    public void SecondaryPillsNeverSqueezeThePrimary()
    {
        var deck = new Rect(new Vector2(0f, 600f), new Vector2(300f, 760f));
        var row = DeckActions.Row(deck, 1f);
        var cursor = row.Min.X;
        for (var pill = 0; pill < DeckActions.MaxSecondary; pill++)
        {
            var rect = DeckActions.Secondary(row, cursor, DeckActions.PillMaxWidth, 1f);
            Assert.True(rect.Width >= 0f);
            cursor = DeckActions.Advance(rect, 1f);
        }

        var primary = DeckActions.Primary(row, cursor);
        Assert.True(primary.Width >= DeckActions.PrimaryMinWidth - 0.01f, primary.Width.ToString());
    }

    [Fact]
    public void PillsKeepATouchSizedWidth()
    {
        Assert.True(DeckActions.PillWidth(0f, 56f, SmallScale) >= DeckActions.PillMinWidth * SmallScale);
        Assert.True(DeckActions.PillWidth(900f, 56f, 1f) <= DeckActions.PillMaxWidth);
        Assert.True(DeckActions.PillMinWidth >= 44f);
    }

    [Fact]
    public void SeatSpotsNeverShrinkUnderTheTouchSize()
    {
        Assert.Equal(SeatSpot.MinimumRadius * SmallScale, SeatSpot.RadiusFor(4f, SmallScale));
        Assert.Equal(40f, SeatSpot.RadiusFor(40f, 1f));
        Assert.True(SeatSpot.MinimumRadius * 2f >= 56f);
    }

    private static float Over(float level, Vector4 tint, float alpha) =>
        level * (1f - alpha) + Palette.Luminance(tint) * alpha;

    private static float HazeStack(Vector4 tint, float alpha)
    {
        var level = MathF.Max(Palette.Luminance(StageBackdrop.StripTop), Palette.Luminance(StageBackdrop.StripWarmTop));
        level = Over(level, StageBackdrop.WindowLit, StageBackdrop.SkylineGlowAlpha);
        level = Over(level, tint, alpha);
        for (var blob = 0; blob < 2; blob++)
        {
            for (var layer = 1; layer <= 3; layer++)
            {
                level = Over(level, tint, alpha * StageBackdrop.HazeBlobShare / layer);
            }
        }

        return level;
    }
}
