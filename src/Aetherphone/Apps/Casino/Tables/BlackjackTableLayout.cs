using Aetherphone.Core;
using Aetherphone.Core.Casino;
using Aetherphone.Windows.Components;

namespace Aetherphone.Apps.Casino.Tables;

internal static class BlackjackTableLayout
{
    public const float RailFraction = 0.50f;

    public const float HeroFanFraction = 0.70f;

    public const float RailArcLift = 14f;

    public const float StateLineGap = 8f;

    public const float SideInset = 10f;

    public const float DealerPuckDrop = 24f;

    public const float DealerPuckRadius = 16f;

    public const float DealerFanGap = 8f;

    public const float DealerCardWidth = 38f;

    public const float DealerTotalDrop = 12f;

    public const float SpeechGap = 10f;

    public const float RailCardWidth = 18f;

    public const float RailSplitCardWidth = 14f;

    public const float RailPuckRadius = 22f;

    public const float RailCardsLift = 64f;

    public const float RailTotalLift = 51f;

    public const float RailBadgeLift = 53f;

    public const float RailBetLift = 32f;

    public const float RailPlateGap = 4f;

    public const float RailPlateHeight = 36f;

    public const float HeroTotalDrop = 12f;

    public const float HeroChipsDrop = 42f;

    public const float HeroSlotWidthCap = 150f;

    public const float HeroSlotPad = 8f;

    public const float SideSpotRadius = 22f;

    public const float SideSpotSpacing = 64f;

    public const float MainSpotRadius = 22f;

    public const float CapsuleDrop = 28f;

    public const float CapsuleHeight = 50f;

    public const float CapsulePuckRadius = 17f;

    public const float ShoeInsetX = 30f;

    public const float ShoeInsetY = 26f;

    public static int RailSeatCount(int mySeat)
    {
        return BlackjackRules.IsSeat(mySeat) ? BlackjackRules.SeatCount - 1 : BlackjackRules.SeatCount;
    }

    public static int RailSlotOf(int seatIndex, int mySeat)
    {
        if (!BlackjackRules.IsSeat(mySeat))
        {
            return seatIndex;
        }

        var slot = (seatIndex - mySeat - 1) % BlackjackRules.SeatCount;
        return slot < 0 ? slot + BlackjackRules.SeatCount : slot;
    }

    public static float RailColumnWidth(in Rect felt, int railCount, float scale)
    {
        if (railCount <= 0)
        {
            return felt.Width;
        }

        return (felt.Width - SideInset * 2f * scale) / railCount;
    }

    public static Vector2 RailPuckCenter(in Rect felt, int slot, int railCount, float scale)
    {
        var columnWidth = RailColumnWidth(felt, railCount, scale);
        var x = felt.Min.X + SideInset * scale + columnWidth * (slot + 0.5f);
        var half = MathF.Max(1f, felt.Width * 0.5f);
        var spread = (x - felt.Center.X) / half;
        return new Vector2(x, RailPuckY(felt) - spread * spread * RailArcLift * scale);
    }

    public static float StateLineY(in Rect felt, float scale)
    {
        var fan = DealerFanCenter(felt, scale);
        var totalBottom = fan.Y + PlayingCards.HeightFor(DealerCardWidth * scale) * 0.5f
            + (DealerTotalDrop + 10f) * scale;
        return totalBottom + StateLineGap * scale + Typography.LineHeight(TextStyles.Title2) * 0.5f;
    }

    public static float RailPuckY(in Rect felt)
    {
        return felt.Min.Y + felt.Height * RailFraction;
    }

    public static Vector2 DealerPuckCenter(in Rect felt, float scale)
    {
        return new Vector2(felt.Center.X, felt.Min.Y + DealerPuckDrop * scale);
    }

    public static Vector2 DealerFanCenter(in Rect felt, float scale)
    {
        var puckBottom = felt.Min.Y + (DealerPuckDrop + DealerPuckRadius) * scale;
        var cardHalf = PlayingCards.HeightFor(DealerCardWidth * scale) * 0.5f;
        return new Vector2(felt.Center.X, puckBottom + DealerFanGap * scale + cardHalf);
    }

    public static Rect SpeechArea(in Rect felt, float scale)
    {
        var puck = DealerPuckCenter(felt, scale);
        var radius = DealerPuckRadius * scale;
        var right = puck.X - radius - SpeechGap * scale;
        var left = felt.Min.X + SideInset * 2f * scale;
        var top = puck.Y - radius;
        return new Rect(new Vector2(left, top), new Vector2(MathF.Max(left, right), puck.Y + radius));
    }

    public static Vector2 ShoeAnchor(in Rect felt, float scale)
    {
        return new Vector2(felt.Max.X - ShoeInsetX * scale, felt.Min.Y + ShoeInsetY * scale);
    }

    public static float HeroFanY(in Rect felt)
    {
        return felt.Min.Y + felt.Height * HeroFanFraction;
    }

    public static float HeroCardWidth(int handCount)
    {
        return handCount switch
        {
            <= 1 => 44f,
            2 => 36f,
            3 => 30f,
            _ => 24f,
        };
    }

    public static float HeroSlotWidth(in Rect felt, int handCount, float scale)
    {
        var count = handCount > 0 ? handCount : 1;
        var available = (felt.Width - SideInset * 2f * scale) / count;
        var cap = HeroSlotWidthCap * scale;
        return available < cap ? available : cap;
    }

    public static Vector2 HeroHandCenter(in Rect felt, int handCount, int handIndex, float scale)
    {
        var count = handCount > 0 ? handCount : 1;
        var slotWidth = HeroSlotWidth(felt, count, scale);
        var x = felt.Center.X + (handIndex - (count - 1) * 0.5f) * slotWidth;
        return new Vector2(x, HeroFanY(felt));
    }

    public static Vector2 HeroBetSpot(in Rect felt, float scale)
    {
        var fanBottom = HeroFanY(felt) + PlayingCards.HeightFor(HeroCardWidth(1) * scale) * 0.5f;
        return new Vector2(felt.Center.X, fanBottom + HeroChipsDrop * scale);
    }

    public static Vector2 SideSpot(in Rect felt, BlackjackSideBet bet, float scale)
    {
        var main = HeroBetSpot(felt, scale);
        var offset = SideSpotSpacing * scale;
        return new Vector2(bet == BlackjackSideBet.PerfectPairs ? main.X - offset : main.X + offset, main.Y);
    }

    public static Vector2 CapsuleCenter(in Rect felt, float scale)
    {
        return new Vector2(felt.Center.X, felt.Max.Y - CapsuleDrop * scale);
    }

    public static float FanStep(float cardWidth, int cardCount, float maxWidth)
    {
        if (cardCount <= 1)
        {
            return 0f;
        }

        var step = cardWidth * 0.45f;
        var width = cardWidth + step * (cardCount - 1);
        if (width <= maxWidth)
        {
            return step;
        }

        var squeezed = (maxWidth - cardWidth) / (cardCount - 1);
        return squeezed > 0f ? squeezed : 0f;
    }
}
