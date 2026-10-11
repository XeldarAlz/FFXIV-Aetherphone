using Aetherphone.Core;
using Aetherphone.Core.Casino;

namespace Aetherphone.Apps.Casino.Cabinets;

internal readonly struct BingoHallLayout
{
    public const float TopShare = 0.22f;
    public const float TopMin = 76f;
    public const float TopMax = 118f;
    public const float Gap = 8f;
    public const float PodiumHeight = 70f;
    public const float BoardColumns = BingoRules.NumbersPerColumn + 1;
    public const float BoardMaxCell = 24f;
    public const float LabelHeight = 18f;
    public const float RailShare = 0.24f;
    public const float RailMin = 52f;
    public const float RailMax = 84f;
    public const float HeroShare = 0.72f;

    public readonly Rect Tumbler;
    public readonly Rect Caller;
    public readonly Rect Board;
    public readonly Rect Cards;
    public readonly Rect Hero;
    public readonly Rect Rail;
    public readonly Rect Podiums;
    public readonly float Cell;

    private BingoHallLayout(Rect tumbler, Rect caller, Rect board, Rect cards, Rect hero, Rect rail, Rect podiums,
        float cell)
    {
        Tumbler = tumbler;
        Caller = caller;
        Board = board;
        Cards = cards;
        Hero = hero;
        Rail = rail;
        Podiums = podiums;
        Cell = cell;
    }

    public bool HasRail => Rail.Width > 0f && Rail.Height > 0f;

    public static BingoHallLayout Compute(Rect safe, int cards, float scale)
    {
        var gap = Gap * scale;
        var width = safe.Width;
        var top = Math.Clamp(safe.Height * TopShare, TopMin * scale, TopMax * scale);
        var tumblerSide = MathF.Min(top, width * 0.4f);
        var tumbler = new Rect(safe.Min, new Vector2(safe.Min.X + tumblerSide, safe.Min.Y + tumblerSide));
        var caller = new Rect(new Vector2(tumbler.Max.X + gap, safe.Min.Y),
            new Vector2(safe.Max.X, safe.Min.Y + top));
        var cell = MathF.Min(width / BoardColumns, BoardMaxCell * scale);
        var boardWidth = cell * BoardColumns;
        var boardLeft = safe.Center.X - boardWidth * 0.5f;
        var boardTop = safe.Min.Y + top + gap;
        var board = new Rect(new Vector2(boardLeft, boardTop),
            new Vector2(boardLeft + boardWidth, boardTop + cell * BingoRules.Columns));
        var podiumTop = MathF.Max(board.Max.Y + gap, safe.Max.Y - PodiumHeight * scale);
        var podiums = new Rect(new Vector2(safe.Min.X, podiumTop), new Vector2(safe.Max.X, safe.Max.Y));
        var cardsArea = new Rect(new Vector2(safe.Min.X, board.Max.Y + gap),
            new Vector2(safe.Max.X, MathF.Max(board.Max.Y + gap, podiumTop - gap)));
        var label = LabelHeight * scale;
        var available = MathF.Max(0f, cardsArea.Height - label);
        if (cards <= 1)
        {
            var side = MathF.Min(available, width * HeroShare);
            var left = cardsArea.Center.X - side * 0.5f;
            var hero = new Rect(new Vector2(left, cardsArea.Min.Y + label),
                new Vector2(left + side, cardsArea.Min.Y + label + side));
            var none = new Rect(cardsArea.Max, cardsArea.Max);
            return new BingoHallLayout(tumbler, caller, board, cardsArea, hero, none, podiums, cell);
        }

        var railWidth = Math.Clamp(width * RailShare, RailMin * scale, RailMax * scale);
        var heroSide = MathF.Max(0f, MathF.Min(available, width - railWidth - gap));
        var groupWidth = heroSide + gap + railWidth;
        var groupLeft = cardsArea.Center.X - groupWidth * 0.5f;
        var heroRect = new Rect(new Vector2(groupLeft, cardsArea.Min.Y + label),
            new Vector2(groupLeft + heroSide, cardsArea.Min.Y + label + heroSide));
        var rail = new Rect(new Vector2(heroRect.Max.X + gap, cardsArea.Min.Y),
            new Vector2(heroRect.Max.X + gap + railWidth, cardsArea.Max.Y));
        return new BingoHallLayout(tumbler, caller, board, cardsArea, heroRect, rail, podiums, cell);
    }

    public static Vector2 BoardCellCenter(Rect board, float cell, int ball)
    {
        var column = BingoRules.ColumnOfBall(ball);
        if (column < 0)
        {
            return board.Center;
        }

        var offset = (ball - 1) % BingoRules.NumbersPerColumn;
        return new Vector2(board.Min.X + (offset + 1.5f) * cell, board.Min.Y + (column + 0.5f) * cell);
    }
}
