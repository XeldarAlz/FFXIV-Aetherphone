using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Solitaire;

internal sealed class SolitaireRenderer
{
    private const int DealtCards = 28;
    private const float DealOverlap = 0.5f;
    private const float RecycleBadgeRadius = 8f;
    private static readonly Vector4 FoundationGhost = new(1f, 1f, 1f, 0.12f);
    private static readonly Vector4 StockRing = new(1f, 1f, 1f, 0.3f);
    private static readonly Vector4 StockRingSpent = new(1f, 1f, 1f, 0.12f);
    private static readonly TextStyle BadgeStyle = TextStyles.Caption2;

    private LabelSlot recyclesLabel;

    public void Draw(SolitaireBoard board, in SolitaireLayout layout, PhoneTheme theme, Vector4 accent, float scale,
        in SolitaireHit dragSource, in SolitaireHit dropTarget, float entrance, ReadOnlySpan<int> hiddenByFlight)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = PlayingCards.RoundingFor(layout.CardWidth);
        DrawStock(drawList, board, layout, rounding, accent, scale);
        DrawWaste(drawList, board, layout, rounding, scale, dragSource);
        DrawFoundations(drawList, board, layout, rounding, scale, dragSource, hiddenByFlight);
        DrawTableau(drawList, board, layout, rounding, scale, dragSource, entrance);
        if (dropTarget.Kind != SolitairePileKind.None)
        {
            DrawDropHighlight(drawList, layout, rounding, accent, scale, dropTarget);
        }
    }

    public static void DrawFlightCard(ImDrawListPtr drawList, in SolitaireLayout layout, int card, Vector2 center,
        float sizeFactor, float scale)
    {
        var half = layout.CardSize * 0.5f * sizeFactor;
        var rect = new Rect(center - half, center + half);
        var rounding = PlayingCards.RoundingFor(rect.Width);
        Elevation.Floating(drawList, rect.Min, rect.Max, rounding, scale, 0.6f);
        PlayingCards.DrawFace(drawList, rect, card, rounding, scale, false);
    }

    private void DrawStock(ImDrawListPtr drawList, SolitaireBoard board, in SolitaireLayout layout, float rounding,
        Vector4 accent, float scale)
    {
        var rect = layout.StockRect;
        if (board.StockCount == 0)
        {
            PlayingCards.DrawSlot(drawList, rect, rounding, scale);
            var radius = layout.CardWidth * 0.24f;
            drawList.AddCircle(rect.Center, radius, ImGui.GetColorU32(board.CanRecycle ? StockRing : StockRingSpent), 24,
                2f * scale);
        }
        else
        {
            PlayingCards.DrawBack(drawList, rect, rounding, scale, true);
        }

        if (!board.Vegas)
        {
            return;
        }

        var badgeRadius = RecycleBadgeRadius * scale;
        var badgeCenter = new Vector2(rect.Max.X, rect.Min.Y);
        var spent = board.RecyclesLeft == 0;
        drawList.AddCircleFilled(badgeCenter, badgeRadius, ImGui.GetColorU32(spent ? GamePalette.CellSunken : accent));
        Typography.DrawCentered(drawList, badgeCenter, recyclesLabel.Get(L.Stage.Times, board.RecyclesLeft),
            spent ? GamePalette.InkLight with { W = 0.5f } : GamePalette.InkOn(accent), BadgeStyle);
    }

    private void DrawWaste(ImDrawListPtr drawList, SolitaireBoard board, in SolitaireLayout layout, float rounding,
        float scale, in SolitaireHit dragSource)
    {
        var rect = layout.WasteRect;
        var fromTop = dragSource.Kind == SolitairePileKind.Waste ? 1 : 0;
        var card = board.WastePeek(fromTop);
        if (card < 0)
        {
            PlayingCards.DrawSlot(drawList, rect, rounding, scale);
            return;
        }

        PlayingCards.DrawFace(drawList, rect, card, rounding, scale, true);
    }

    private void DrawFoundations(ImDrawListPtr drawList, SolitaireBoard board, in SolitaireLayout layout,
        float rounding, float scale, in SolitaireHit dragSource, ReadOnlySpan<int> hiddenByFlight)
    {
        for (var suit = 0; suit < SolitaireBoard.SuitCount; suit++)
        {
            var rect = layout.FoundationRect(suit);
            var fromTop = dragSource.Kind == SolitairePileKind.Foundation && dragSource.Pile == suit ? 1 : 0;
            fromTop += hiddenByFlight[suit];
            var card = board.FoundationPeek(suit, fromTop);
            if (card < 0)
            {
                PlayingCards.DrawSlot(drawList, rect, rounding, scale);
                PlayingCards.DrawSuit(drawList, rect.Center, layout.CardWidth * 0.26f, suit, FoundationGhost);
                continue;
            }

            PlayingCards.DrawFace(drawList, rect, card, rounding, scale, true);
        }
    }

    private void DrawTableau(ImDrawListPtr drawList, SolitaireBoard board, in SolitaireLayout layout, float rounding,
        float scale, in SolitaireHit dragSource, float entrance)
    {
        var dealOrigin = layout.StockRect.Min;
        for (var pile = 0; pile < SolitaireBoard.TableauPiles; pile++)
        {
            var count = board.TableauCount(pile);
            if (count == 0)
            {
                PlayingCards.DrawSlot(drawList, layout.TableauBaseRect(pile), rounding, scale);
                continue;
            }

            var skipFrom = dragSource.Kind == SolitairePileKind.Tableau && dragSource.Pile == pile
                ? dragSource.CardIndex
                : count;
            var firstOrdinal = pile * (pile + 1) / 2;
            for (var index = 0; index < count; index++)
            {
                if (index >= skipFrom)
                {
                    break;
                }

                var rect = layout.TableauCardRect(pile, index);
                var dealProgress = Easing.EaseOutCubic(GameJuice.Stagger(entrance, firstOrdinal + index, DealtCards, DealOverlap));
                if (dealProgress <= 0f)
                {
                    continue;
                }

                if (dealProgress < 1f)
                {
                    var min = Vector2.Lerp(dealOrigin, rect.Min, dealProgress);
                    rect = new Rect(min, min + layout.CardSize);
                }

                if (board.IsTableauFaceUp(pile, index))
                {
                    PlayingCards.DrawFace(drawList, rect, board.TableauCardAt(pile, index), rounding, scale, true);
                }
                else
                {
                    PlayingCards.DrawBack(drawList, rect, rounding, scale, true);
                }
            }
        }
    }

    public void DrawFloating(in SolitaireLayout layout, ReadOnlySpan<int> cards, Vector2 topLeft, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = PlayingCards.RoundingFor(layout.CardWidth);
        for (var index = 0; index < cards.Length; index++)
        {
            var min = new Vector2(topLeft.X, topLeft.Y + index * layout.FanUp);
            var rect = new Rect(min, min + layout.CardSize);
            Elevation.Floating(drawList, rect.Min, rect.Max, rounding, scale, 0.6f);
            PlayingCards.DrawFace(drawList, rect, cards[index], rounding, scale, false);
        }
    }

    private static void DrawDropHighlight(ImDrawListPtr drawList, in SolitaireLayout layout, float rounding,
        Vector4 accent, float scale, in SolitaireHit target)
    {
        var rect = target.Kind switch
        {
            SolitairePileKind.Foundation => layout.FoundationRect(target.Pile),
            SolitairePileKind.Tableau => target.CardIndex >= 0
                ? layout.TableauCardRect(target.Pile, target.CardIndex)
                : layout.TableauBaseRect(target.Pile),
            _ => layout.TopSlot(0),
        };
        Squircle.Stroke(drawList, rect.Min - new Vector2(2f * scale, 2f * scale),
            rect.Max + new Vector2(2f * scale, 2f * scale), rounding + 2f * scale, ImGui.GetColorU32(accent),
            2.4f * scale);
    }
}
