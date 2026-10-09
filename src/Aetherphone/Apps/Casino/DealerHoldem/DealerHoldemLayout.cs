using Aetherphone.Core;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.DealerHoldem;

internal readonly struct DealerHoldemLayout
{
    public const float CardAspect = 1.4f;
    public const float SectionGap = 8f;
    public const float PlateRow = 30f;
    public const float StateRow = 38f;
    public const float CircleLabelRow = 22f;
    public const float PuckRadius = 16f;
    public const float PuckTuck = 1.25f;
    public const float CardGap = 6f;
    public const float HeroOverlap = 0.12f;
    public const float HeroAngle = 0.06f;
    public const float MinimumCardWidth = 26f;
    public const float MaximumDealerCard = 66f;
    public const float MaximumBoardCard = 64f;
    public const float MaximumHeroCard = 104f;
    public const float MinimumCircleRadius = 22f;
    public const float MaximumCircleRadius = 38f;
    public const float CircleGap = 12f;
    public const float TableGap = 10f;
    public const float MinimumTableWidth = 72f;
    public const float DealerWeight = 0.22f;
    public const float BoardWeight = 0.22f;
    public const float CircleWeight = 0.18f;
    public const float TableBudget = 0.2f;

    public readonly Rect Safe;
    public readonly Rect Dealer;
    public readonly Vector2 Puck;
    public readonly float PuckRadiusPixels;
    public readonly float DealerCardWidth;
    public readonly float DealerCardsY;
    public readonly Rect BlindTable;
    public readonly Rect TripsTable;
    public readonly float DealerNameY;
    public readonly float BoardCardWidth;
    public readonly float BoardLeft;
    public readonly float BoardY;
    public readonly float StateY;
    public readonly float CircleRadius;
    public readonly float CircleLeft;
    public readonly float CircleStep;
    public readonly float CircleY;
    public readonly float CircleLabelY;
    public readonly float HeroCardWidth;
    public readonly float HeroY;
    public readonly float HeroNameY;
    public readonly Vector2 Shoe;
    public readonly float Scale;

    private DealerHoldemLayout(Rect safe, Rect dealer, Vector2 puck, float puckRadius, float dealerCardWidth,
        float dealerCardsY, Rect blindTable, Rect tripsTable, float dealerNameY, float boardCardWidth, float boardLeft,
        float boardY, float stateY, float circleRadius, float circleLeft, float circleStep, float circleY,
        float circleLabelY, float heroCardWidth, float heroY, float heroNameY, Vector2 shoe, float scale)
    {
        Safe = safe;
        Dealer = dealer;
        Puck = puck;
        PuckRadiusPixels = puckRadius;
        DealerCardWidth = dealerCardWidth;
        DealerCardsY = dealerCardsY;
        BlindTable = blindTable;
        TripsTable = tripsTable;
        DealerNameY = dealerNameY;
        BoardCardWidth = boardCardWidth;
        BoardLeft = boardLeft;
        BoardY = boardY;
        StateY = stateY;
        CircleRadius = circleRadius;
        CircleLeft = circleLeft;
        CircleStep = circleStep;
        CircleY = circleY;
        CircleLabelY = circleLabelY;
        HeroCardWidth = heroCardWidth;
        HeroY = heroY;
        HeroNameY = heroNameY;
        Shoe = shoe;
        Scale = scale;
    }

    public bool HasTables => BlindTable.Width > 0f && TripsTable.Width > 0f;

    public float DealerCardHeight => DealerCardWidth * CardAspect;

    public float BoardCardHeight => BoardCardWidth * CardAspect;

    public float HeroCardHeight => HeroCardWidth * CardAspect;

    public static DealerHoldemLayout Compute(Rect safe, float scale, float tableHeight = 0f)
    {
        var width = MathF.Max(1f, safe.Width);
        var gap = SectionGap * scale;
        var puckRadius = PuckRadius * scale;
        var puckBlock = puckRadius * (1f + PuckTuck);
        var fixedHeight = PlateRow * scale * 2f + StateRow * scale + CircleLabelRow * scale + puckBlock + gap * 6f;
        var flexible = MathF.Max(0f, safe.Height - fixedHeight);
        var dealerCard = DealerCardFor(flexible, width, scale);
        var tableExtra = MathF.Max(0f, tableHeight - puckBlock - dealerCard * CardAspect);
        var printTables = tableExtra <= flexible * TableBudget;
        if (!printTables)
        {
            tableExtra = 0f;
            tableHeight = 0f;
        }

        var sized = MathF.Max(0f, flexible - tableExtra);
        dealerCard = DealerCardFor(sized, width, scale);
        var cardGap = CardGap * scale;
        var minimumCard = MinimumCardWidth * scale;
        var boardCard = Math.Clamp(sized * BoardWeight / CardAspect, minimumCard,
            MathF.Min(MaximumBoardCard * scale, (width - cardGap * 4f) / DealerHoldemRules.BoardCards));
        var circleRadius = Math.Clamp(sized * CircleWeight * 0.5f, MinimumCircleRadius * scale,
            MathF.Min(MaximumCircleRadius * scale,
                (width - CircleGap * scale * (DealerHoldemRules.SpotCount - 1)) / (DealerHoldemRules.SpotCount * 2f)));
        var dealerZone = MathF.Max(dealerCard * CardAspect, tableHeight - puckBlock);
        var heroBudget = MathF.Max(0f, flexible - dealerZone - boardCard * CardAspect - circleRadius * 2f);
        var heroCard = Math.Clamp(heroBudget / CardAspect, minimumCard,
            MathF.Min(MaximumHeroCard * scale, width * 0.32f));

        var top = safe.Min.Y;
        var puck = new Vector2(safe.Center.X, top + puckRadius);
        var dealerCardsY = puck.Y + puckRadius * PuckTuck + dealerZone * 0.5f;
        var dealerBottom = top + puckBlock + dealerZone;
        var dealer = new Rect(new Vector2(safe.Min.X, top), new Vector2(safe.Max.X, dealerBottom));
        var cardsHalf = dealerCard + cardGap * 0.5f;
        var tableWidth = MathF.Max(0f, width * 0.5f - cardsHalf - TableGap * scale);
        var blindTable = default(Rect);
        var tripsTable = default(Rect);
        if (printTables && tableWidth >= MinimumTableWidth * scale)
        {
            blindTable = new Rect(new Vector2(safe.Min.X, top), new Vector2(safe.Min.X + tableWidth, dealerBottom));
            tripsTable = new Rect(new Vector2(safe.Max.X - tableWidth, top), new Vector2(safe.Max.X, dealerBottom));
        }

        var dealerNameY = dealerBottom + gap + PlateRow * scale * 0.5f;
        var boardTop = dealerBottom + gap + PlateRow * scale + gap;
        var boardY = boardTop + boardCard * CardAspect * 0.5f;
        var boardWidth = boardCard * DealerHoldemRules.BoardCards + cardGap * (DealerHoldemRules.BoardCards - 1);
        var boardLeft = safe.Center.X - boardWidth * 0.5f;
        var stateY = boardTop + boardCard * CardAspect + gap + StateRow * scale * 0.5f;

        var heroNameY = safe.Max.Y - PlateRow * scale * 0.5f;
        var heroBottom = safe.Max.Y - PlateRow * scale - gap;
        var heroY = heroBottom - heroCard * CardAspect * 0.5f;
        var circlesTop = stateY + StateRow * scale * 0.5f + gap;
        var circlesBottom = heroBottom - heroCard * CardAspect - gap;
        var circleBlock = circleRadius * 2f + CircleLabelRow * scale;
        var circleSpare = MathF.Max(0f, circlesBottom - circlesTop - circleBlock);
        var circleY = circlesTop + circleSpare * 0.5f + circleRadius;
        var circleLabelY = circleY + circleRadius + CircleLabelRow * scale * 0.5f;
        var circleStep = circleRadius * 2f + MathF.Max(CircleGap * scale,
            MathF.Min(circleRadius * 1.2f, (width - circleRadius * 2f * DealerHoldemRules.SpotCount)
                / (DealerHoldemRules.SpotCount - 1)));
        var circleLeft = safe.Center.X - circleStep * (DealerHoldemRules.SpotCount - 1) * 0.5f;
        var shoe = new Vector2(safe.Max.X - dealerCard * 0.5f, top + dealerCard * CardAspect * 0.5f);
        return new DealerHoldemLayout(safe, dealer, puck, puckRadius, dealerCard, dealerCardsY, blindTable,
            tripsTable, dealerNameY, boardCard, boardLeft, boardY, stateY, circleRadius, circleLeft, circleStep,
            circleY, circleLabelY, heroCard, heroY, heroNameY, shoe, scale);
    }

    private static float DealerCardFor(float flexible, float width, float scale) =>
        Math.Clamp(flexible * DealerWeight / CardAspect, MinimumCardWidth * scale,
            MathF.Min(MaximumDealerCard * scale, (width - CardGap * scale) * 0.25f));

    public Vector2 DealerCard(int index)
    {
        var offset = (DealerCardWidth + CardGap * Scale) * 0.5f;
        return new Vector2(Safe.Center.X + (index == 0 ? -offset : offset), DealerCardsY);
    }

    public Vector2 BoardCard(int index) =>
        new(BoardLeft + BoardCardWidth * 0.5f + index * (BoardCardWidth + CardGap * Scale), BoardY);

    public Vector2 HeroCard(int index)
    {
        var offset = HeroCardWidth * (0.5f - HeroOverlap * 0.5f);
        return new Vector2(Safe.Center.X + (index == 0 ? -offset : offset), HeroY);
    }

    public static float HeroCardAngle(int index) => index == 0 ? -HeroAngle : HeroAngle;

    public Vector2 Circle(DealerHoldemSpot spot) => new(CircleLeft + (int)spot * CircleStep, CircleY);

    public Vector2 CircleLabel(DealerHoldemSpot spot) => new(CircleLeft + (int)spot * CircleStep, CircleLabelY);

    public Rect CircleRect(DealerHoldemSpot spot)
    {
        var center = Circle(spot);
        var half = new Vector2(CircleRadius, CircleRadius);
        return new Rect(center - half, center + half);
    }

    public Rect CircleBlock(DealerHoldemSpot spot)
    {
        var center = Circle(spot);
        var halfWidth = MathF.Max(CircleRadius, CircleStep * 0.5f - CircleGap * Scale * 0.25f);
        return new Rect(new Vector2(center.X - halfWidth, center.Y - CircleRadius),
            new Vector2(center.X + halfWidth, CircleLabelY + CircleLabelRow * Scale * 0.5f));
    }

    public float CircleLabelWidth => MathF.Max(CircleRadius * 2f, CircleStep - CircleGap * Scale * 0.5f);

    public Rect DealerCards
    {
        get
        {
            var half = new Vector2(DealerCardWidth + CardGap * Scale * 0.5f, DealerCardHeight * 0.5f);
            var center = new Vector2(Safe.Center.X, DealerCardsY);
            return new Rect(center - half, center + half);
        }
    }

    public Rect Board => new(new Vector2(BoardLeft, BoardY - BoardCardHeight * 0.5f),
        new Vector2(Safe.Center.X * 2f - BoardLeft, BoardY + BoardCardHeight * 0.5f));

    public Rect Hero
    {
        get
        {
            var halfWidth = HeroCardWidth * (1f - HeroOverlap * 0.5f);
            return new Rect(new Vector2(Safe.Center.X - halfWidth, HeroY - HeroCardHeight * 0.5f),
                new Vector2(Safe.Center.X + halfWidth, HeroY + HeroCardHeight * 0.5f));
        }
    }

    public Rect StateBand => new(new Vector2(Safe.Min.X, StateY - StateRow * Scale * 0.5f),
        new Vector2(Safe.Max.X, StateY + StateRow * Scale * 0.5f));

    public Rect DealerNameBand => new(new Vector2(Safe.Min.X, DealerNameY - PlateRow * Scale * 0.5f),
        new Vector2(Safe.Max.X, DealerNameY + PlateRow * Scale * 0.5f));

    public Rect HeroNameBand => new(new Vector2(Safe.Min.X, HeroNameY - PlateRow * Scale * 0.5f),
        new Vector2(Safe.Max.X, HeroNameY + PlateRow * Scale * 0.5f));

    public Rect Circles => new(new Vector2(CircleLeft - CircleRadius, CircleY - CircleRadius),
        new Vector2(CircleLeft + CircleStep * (DealerHoldemRules.SpotCount - 1) + CircleRadius,
            CircleLabelY + CircleLabelRow * Scale * 0.5f));
}
