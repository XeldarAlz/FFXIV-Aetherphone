using System.Numerics;
using Aetherphone.Apps.Casino.Tables;
using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Windows.Components;
using Xunit;

namespace Aetherphone.Tests;

public sealed class HoldemTableLayoutTests
{
    private const float Epsilon = 0.01f;
    private const float StackPlateWidth = 60f;
    private const int CleanSeatLimit = 7;
    private const float TagHalfHeight = 9f;

    private static readonly float[] Scales = { 0.75f, 1f, 1.5f };

    private static readonly Vector2[] SafeSizes =
    {
        new(272f, 452f),
        new(272f, 398f),
        new(272f, 376f),
        new(312f, 470f),
    };

    public static TheoryData<float, float, float, int, bool> Tables()
    {
        var data = new TheoryData<float, float, float, int, bool>();
        for (var scaleIndex = 0; scaleIndex < Scales.Length; scaleIndex++)
        {
            for (var sizeIndex = 0; sizeIndex < SafeSizes.Length; sizeIndex++)
            {
                for (var seats = HoldemRules.MinSeats; seats <= CleanSeatLimit; seats++)
                {
                    var size = SafeSizes[sizeIndex];
                    data.Add(Scales[scaleIndex], size.X, size.Y, seats, true);
                    data.Add(Scales[scaleIndex], size.X, size.Y, seats, false);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public void PodsAndBetsNeverTouchAndStayOnTheFelt(float scale, float width, float height, int seats,
        bool seated)
    {
        var layout = Compute(scale, width, height, seats, 2, seated);
        var felt = layout.Felt;
        for (var seat = 0; seat < seats; seat++)
        {
            Assert.True(Inside(felt, layout.PodRect(seat)), Describe("pod", seat, layout.PodRect(seat), felt));
            Assert.True(Inside(felt, layout.BetRect(seat)), Describe("bet", seat, layout.BetRect(seat), felt));
            for (var other = 0; other < seats; other++)
            {
                if (other == seat)
                {
                    continue;
                }

                Assert.False(Overlaps(layout.PodRect(seat), layout.PodRect(other)), Pair("pods", seat, other));
                Assert.False(Overlaps(layout.BetRect(seat), layout.PodRect(other)), Pair("bet on pod", seat, other));
                Assert.False(Overlaps(layout.BetRect(seat), layout.BetRect(other)), Pair("bets", seat, other));
            }
        }
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public void TheBoardPotAndStateLineStayClearOfEverySeat(float scale, float width, float height, int seats,
        bool seated)
    {
        var layout = Compute(scale, width, height, seats, 0, seated);
        var board = layout.BoardRect();
        var pot = layout.PotRect();
        var state = layout.StateBand;
        Assert.True(Inside(layout.Felt, board));
        Assert.True(pot.Max.Y <= board.Min.Y + Epsilon);
        Assert.True(board.Max.Y <= state.Min.Y + Epsilon);
        Assert.True(layout.BoardCardWidth >= HoldemTableLayout.BoardCardMin * scale - Epsilon);
        for (var seat = 0; seat < seats; seat++)
        {
            Assert.False(Overlaps(board, layout.PodRect(seat)), Pair("board", seat, seat));
            Assert.False(Overlaps(pot, layout.PodRect(seat)), Pair("pot", seat, seat));
            Assert.False(Overlaps(state, layout.PodRect(seat)), Pair("state", seat, seat));
            Assert.False(Overlaps(board, layout.BetRect(seat)), Pair("board bet", seat, seat));
            Assert.False(Overlaps(pot, layout.BetRect(seat)), Pair("pot bet", seat, seat));
            Assert.False(Overlaps(state, layout.BetRect(seat)), Pair("state bet", seat, seat));
        }
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public void TheHeroCardsCapsuleAndBetStackWithoutTouchingTheRing(float scale, float width, float height,
        int seats, bool seated)
    {
        if (!seated)
        {
            return;
        }

        var hero = 1;
        var layout = Compute(scale, width, height, seats, hero, seated);
        var cards = layout.HeroCardsRect();
        var capsule = layout.HeroCapsule;
        Assert.True(layout.HeroCardPixels >= HoldemTableLayout.HeroCardMin * scale - Epsilon);
        Assert.True(Inside(layout.Felt, cards));
        Assert.True(Inside(layout.Felt, capsule));
        Assert.True(cards.Max.Y <= capsule.Min.Y + Epsilon);
        Assert.True(layout.BetRect(hero).Max.Y <= cards.Min.Y + Epsilon);
        Assert.False(Overlaps(cards, layout.StateBand));
        Assert.False(Overlaps(layout.BetRect(hero), layout.StateBand));
        for (var seat = 0; seat < seats; seat++)
        {
            if (seat == hero)
            {
                continue;
            }

            Assert.False(Overlaps(cards, layout.PodRect(seat)), Pair("hero cards", seat, hero));
            Assert.False(Overlaps(capsule, layout.PodRect(seat)), Pair("hero capsule", seat, hero));
            Assert.False(Overlaps(cards, layout.BetRect(seat)), Pair("hero cards bet", seat, hero));
            Assert.False(Overlaps(capsule, layout.BetRect(seat)), Pair("hero capsule bet", seat, hero));
        }
    }

    [Theory]
    [MemberData(nameof(Tables))]
    public void PlatesFitAStackAndSpotsFitTheirPods(float scale, float width, float height, int seats, bool seated)
    {
        var layout = Compute(scale, width, height, seats, 0, seated);
        Assert.True(layout.PodWidth >= StackPlateWidth * scale - Epsilon);
        Assert.True(layout.SpotRadius * 2f >= 44f * scale - Epsilon);
        for (var seat = 0; seat < seats; seat++)
        {
            if (layout.IsHero(seat))
            {
                continue;
            }

            var center = layout.SeatCenter(seat);
            var pod = layout.PodRect(seat);
            Assert.True(center.X - layout.SpotRadius >= pod.Min.X - Epsilon);
            Assert.True(center.X + layout.SpotRadius <= pod.Max.X + Epsilon);
            Assert.True(center.Y - layout.SpotRadius >= pod.Min.Y - Epsilon);
            Assert.True(layout.ShownCardPixels * (1f + HoldemTableLayout.ShownCardStep) <= layout.PodWidth);
            var button = layout.DealerButton(seat);
            Assert.True(button.X + HoldemTableLayout.DealerButtonRadius * scale <= pod.Max.X + Epsilon);
            Assert.True(button.Y + HoldemTableLayout.DealerButtonRadius * scale <= layout.CapsuleRect(seat).Min.Y);
            var cards = layout.SeatCardsAnchor(seat);
            var cardWidth = HoldemTableLayout.SeatCardWidth * scale;
            var cardHalfHeight = PlayingCards.HeightFor(cardWidth) * 0.5f;
            Assert.True(cards.X - cardWidth * 0.8f >= pod.Min.X - Epsilon);
            Assert.True(cards.Y + cardHalfHeight <= layout.CapsuleRect(seat).Min.Y + Epsilon);
            Assert.True(cards.Y - cardHalfHeight >= layout.TagCenter(seat).Y + TagHalfHeight * scale);
            var shownHalf = PlayingCards.HeightFor(layout.ShownCardPixels) * 0.5f;
            Assert.True(center.Y + shownHalf <= layout.CapsuleRect(seat).Min.Y + Epsilon);
        }
    }

    [Fact]
    public void TheBottomSeatIsAlwaysTheLowestPod()
    {
        var safe = new Rect(new Vector2(0f, 0f), new Vector2(272f, 452f));
        var layout = new HoldemTableLayout();
        for (var seats = HoldemRules.MinSeats; seats <= HoldemRules.MaxSeats; seats++)
        {
            for (var hero = 0; hero < seats; hero++)
            {
                layout.Compute(safe, seats, hero, true, 1f);
                var bottom = layout.SeatCenter(hero);
                for (var seat = 0; seat < seats; seat++)
                {
                    Assert.True(layout.SeatCenter(seat).Y <= bottom.Y + Epsilon);
                    Assert.InRange(layout.SeatCenter(seat).X, safe.Min.X, safe.Max.X);
                }

                Assert.True(layout.Shelf.Max.Y <= safe.Max.Y + Epsilon);
                Assert.True(layout.HeroCardsCenter.Y < bottom.Y);
            }
        }
    }

    [Fact]
    public void SeatsRunClockwiseFromTheHero()
    {
        var layout = Compute(1f, 272f, 452f, 6, 0, true);
        Assert.Equal(HoldemPodSide.Bottom, layout.SideOf(0));
        Assert.Equal(HoldemPodSide.LowerLeft, layout.SideOf(1));
        Assert.Equal(HoldemPodSide.Top, layout.SideOf(2));
        Assert.Equal(HoldemPodSide.Top, layout.SideOf(4));
        Assert.Equal(HoldemPodSide.LowerRight, layout.SideOf(5));
        Assert.True(layout.SeatCenter(2).X < layout.SeatCenter(3).X);
        Assert.True(layout.SeatCenter(3).X < layout.SeatCenter(4).X);
    }

    [Fact]
    public void TheBoardStaysInsideTheFeltAtNineSeats()
    {
        var layout = Compute(1f, 272f, 452f, 9, 0, true);
        var board = layout.BoardRect();
        Assert.True(board.Min.X >= layout.Felt.Min.X);
        Assert.True(board.Max.X <= layout.Felt.Max.X);
        for (var seat = 0; seat < 9; seat++)
        {
            Assert.False(Overlaps(board, layout.PodRect(seat)), Pair("board", seat, seat));
        }
    }

    private static HoldemTableLayout Compute(float scale, float width, float height, int seats, int bottom,
        bool seated)
    {
        var origin = new Vector2(30f, 140f);
        var safe = new Rect(origin, origin + new Vector2(width, height) * scale);
        var layout = new HoldemTableLayout();
        layout.Compute(safe, seats, bottom % seats, seated, scale);
        return layout;
    }

    private static bool Inside(in Rect outer, in Rect inner)
    {
        return inner.Min.X >= outer.Min.X - Epsilon && inner.Max.X <= outer.Max.X + Epsilon
            && inner.Min.Y >= outer.Min.Y - Epsilon && inner.Max.Y <= outer.Max.Y + Epsilon;
    }

    private static bool Overlaps(in Rect first, in Rect second)
    {
        return first.Min.X < second.Max.X - Epsilon && second.Min.X < first.Max.X - Epsilon
            && first.Min.Y < second.Max.Y - Epsilon && second.Min.Y < first.Max.Y - Epsilon;
    }

    private static string Pair(string what, int first, int second) => what + " " + first + "/" + second;

    private static string Describe(string what, int seat, in Rect rect, in Rect felt) =>
        what + " " + seat + " " + rect.Min + "-" + rect.Max + " in " + felt.Min + "-" + felt.Max;
}
