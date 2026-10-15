using System.Numerics;
using Aetherphone.Apps.Casino.Venue;
using Aetherphone.Core;
using Xunit;

namespace Aetherphone.Tests;

public sealed class CasinoBroadcastLayoutTests
{
    private const float SafeWidth = 600f;
    private const float SafeHeight = 250f;
    private const float CardAspect = 1.4f;
    private const float TitleTwoHeight = 26f;
    private const float SubheadlineHeight = 18f;
    private const float HeadlineHeight = 20f;
    private const float TitleThreeHeight = 24f;

    public static TheoryData<float, int> Cases()
    {
        var data = new TheoryData<float, int>();
        float[] scales = { 0.75f, 1f, 1.25f, 1.5f };
        for (var scaleIndex = 0; scaleIndex < scales.Length; scaleIndex++)
        {
            for (var count = 1; count <= 9; count++)
            {
                data.Add(scales[scaleIndex], count);
            }
        }

        return data;
    }

    [Fact]
    public void TheRecordedTradeCardStacksItsButtonsWhenBothLabelsCannotSitWhole()
    {
        Assert.False(TradeRecordedCard.ButtonsStack(300f, 160f, 110f, 8f));
        Assert.True(TradeRecordedCard.ButtonsStack(300f, 200f, 110f, 8f));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void SeatsNeverTouchTheBoardOrPotAndStayOnTheStage(float scale, int count)
    {
        var safe = new Rect(Vector2.Zero, new Vector2(SafeWidth * scale, SafeHeight * scale));
        var cardWidth = MathF.Max(44f * scale, safe.Width * 0.075f);
        var cardHeight = cardWidth * CardAspect;
        var top = BroadcastLayout.TopRowY(safe, cardHeight, scale);
        var topBottom = BroadcastLayout.TopBottom(top, cardHeight, TitleTwoHeight * scale, scale);
        Assert.True(top - cardHeight * 0.5f >= safe.Min.Y);

        var seatCard = cardWidth * 0.8f;
        var seatCardHeight = seatCard * CardAspect;
        var above = BroadcastLayout.SeatAbove(seatCardHeight, SubheadlineHeight * scale, scale);
        var below = BroadcastLayout.SeatBelow(seatCardHeight, HeadlineHeight * scale, TitleThreeHeight * scale, scale);
        var nameWidth = BroadcastLayout.NameWidth(safe, count, seatCard, scale);
        var handWidth = seatCard * (1f + 0.58f);
        for (var index = 0; index < count; index++)
        {
            var seat = BroadcastLayout.Seat(safe, index, count, topBottom, above, below, scale);
            Assert.True(seat.Y - above >= topBottom - 0.01f);
            Assert.True(seat.Y + below <= safe.Max.Y + 0.01f);
            Assert.True(seat.X - MathF.Max(nameWidth, handWidth) * 0.5f >= safe.Min.X - 0.01f);
            Assert.True(seat.X + MathF.Max(nameWidth, handWidth) * 0.5f <= safe.Max.X + 0.01f);
            if (index == 0)
            {
                continue;
            }

            var previous = BroadcastLayout.Seat(safe, index - 1, count, topBottom, above, below, scale);
            Assert.True(seat.X - previous.X >= nameWidth - 0.01f);
            Assert.True(seat.X - previous.X >= handWidth - 0.01f);
        }
    }
}
