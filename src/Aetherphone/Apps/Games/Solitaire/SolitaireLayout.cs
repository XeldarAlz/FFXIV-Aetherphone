using System.Numerics;
using Aetherphone.Core;

namespace Aetherphone.Apps.Games.Solitaire;

internal enum SolitairePileKind
{
    None,
    Stock,
    Waste,
    Foundation,
    Tableau,
}

internal readonly struct SolitaireHit
{
    public readonly SolitairePileKind Kind;
    public readonly int Pile;
    public readonly int CardIndex;

    public SolitaireHit(SolitairePileKind kind, int pile, int cardIndex)
    {
        Kind = kind;
        Pile = pile;
        CardIndex = cardIndex;
    }

    public static readonly SolitaireHit None = new(SolitairePileKind.None, -1, -1);
}

internal readonly struct SolitaireLayout
{
    private readonly SolitaireBoard board;
    public readonly float OriginX;
    public readonly float ColumnPitch;
    public readonly float CardWidth;
    public readonly float CardHeight;
    public readonly float TopRowY;
    public readonly float TableauTop;
    public readonly float FanUp;
    public readonly float FanDown;

    private SolitaireLayout(SolitaireBoard board, float originX, float columnPitch, float cardWidth, float cardHeight,
        float topRowY, float tableauTop, float fanUp, float fanDown)
    {
        this.board = board;
        OriginX = originX;
        ColumnPitch = columnPitch;
        CardWidth = cardWidth;
        CardHeight = cardHeight;
        TopRowY = topRowY;
        TableauTop = tableauTop;
        FanUp = fanUp;
        FanDown = fanDown;
    }

    public static SolitaireLayout Compute(Rect area, SolitaireBoard board, float scale)
    {
        const int columns = SolitaireBoard.TableauPiles;
        var gap = 5f * scale;
        var sidePad = gap;
        var cardWidth = (area.Width - 2f * sidePad - gap * (columns - 1)) / columns;
        var cardHeight = cardWidth * 1.4f;
        var columnPitch = cardWidth + gap;
        var originX = area.Min.X + sidePad;
        var topRowY = area.Min.Y + 4f * scale;
        var tableauTop = topRowY + cardHeight + 14f * scale;
        var bottomLimit = area.Max.Y - 4f * scale;
        var fanUp = cardHeight * 0.30f;
        var fanDown = cardHeight * 0.15f;
        var maxOffset = 0f;
        for (var pile = 0; pile < SolitaireBoard.TableauPiles; pile++)
        {
            var count = board.TableauCount(pile);
            if (count <= 1)
            {
                continue;
            }

            var down = board.TableauFaceDownCount(pile);
            var lastDownBefore = down < count - 1 ? down : count - 1;
            var lastUpBefore = count - 1 - lastDownBefore;
            var offset = lastDownBefore * fanDown + lastUpBefore * fanUp;
            if (offset > maxOffset)
            {
                maxOffset = offset;
            }
        }

        var available = bottomLimit - tableauTop - cardHeight;
        if (maxOffset > available && maxOffset > 0f)
        {
            var shrink = available / maxOffset;
            fanUp *= shrink;
            fanDown *= shrink;
        }

        return new SolitaireLayout(board, originX, columnPitch, cardWidth, cardHeight, topRowY, tableauTop, fanUp,
            fanDown);
    }

    public Vector2 CardSize => new(CardWidth, CardHeight);

    public Rect TopSlot(int column)
    {
        var min = new Vector2(OriginX + column * ColumnPitch, TopRowY);
        return new Rect(min, min + CardSize);
    }

    public Rect StockRect => TopSlot(0);
    public Rect WasteRect => TopSlot(1);
    public Rect FoundationRect(int suit) => TopSlot(3 + suit);
    public float TableauColumnX(int pile) => OriginX + pile * ColumnPitch;

    public Vector2 TableauCardPosition(int pile, int index)
    {
        var down = board.TableauFaceDownCount(pile);
        var downBefore = index < down ? index : down;
        var upBefore = index - downBefore;
        var y = TableauTop + downBefore * FanDown + upBefore * FanUp;
        return new Vector2(TableauColumnX(pile), y);
    }

    public Rect TableauCardRect(int pile, int index)
    {
        var min = TableauCardPosition(pile, index);
        return new Rect(min, min + CardSize);
    }

    public Rect TableauBaseRect(int pile)
    {
        var min = new Vector2(TableauColumnX(pile), TableauTop);
        return new Rect(min, min + CardSize);
    }

    public SolitaireHit Hit(Vector2 point)
    {
        if (StockRect.Contains(point))
        {
            return new SolitaireHit(SolitairePileKind.Stock, 0, -1);
        }

        if (WasteRect.Contains(point))
        {
            return new SolitaireHit(SolitairePileKind.Waste, 0, board.WasteCount - 1);
        }

        for (var suit = 0; suit < SolitaireBoard.SuitCount; suit++)
        {
            if (FoundationRect(suit).Contains(point))
            {
                return new SolitaireHit(SolitairePileKind.Foundation, suit, -1);
            }
        }

        for (var pile = 0; pile < SolitaireBoard.TableauPiles; pile++)
        {
            var count = board.TableauCount(pile);
            if (count == 0)
            {
                if (TableauBaseRect(pile).Contains(point))
                {
                    return new SolitaireHit(SolitairePileKind.Tableau, pile, -1);
                }

                continue;
            }

            for (var index = count - 1; index >= 0; index--)
            {
                if (TableauCardRect(pile, index).Contains(point))
                {
                    return new SolitaireHit(SolitairePileKind.Tableau, pile, index);
                }
            }
        }

        return SolitaireHit.None;
    }
}
