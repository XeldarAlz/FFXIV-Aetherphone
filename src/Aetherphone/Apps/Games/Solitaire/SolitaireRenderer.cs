using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Solitaire;

internal sealed class SolitaireRenderer
{
    private static readonly Vector4 Red = new(0.88f, 0.22f, 0.26f, 1f);
    private static readonly Vector4 Black = new(0.14f, 0.15f, 0.19f, 1f);
    private static readonly Vector4 Face = new(0.97f, 0.97f, 0.98f, 1f);
    private static readonly Vector4 BackFill = new(0.20f, 0.26f, 0.52f, 1f);
    private static readonly Vector4 BackInner = new(0.32f, 0.40f, 0.74f, 1f);

    public void Draw(SolitaireBoard board, in SolitaireLayout layout, PhoneTheme theme, Vector4 accent, float scale,
        in SolitaireHit dragSource, in SolitaireHit dropTarget)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = layout.CardWidth * 0.12f;
        DrawStock(drawList, board, layout, rounding, scale);
        DrawWaste(drawList, board, layout, rounding, scale, dragSource);
        DrawFoundations(drawList, board, layout, rounding, scale, dragSource);
        DrawTableau(drawList, board, layout, rounding, scale, dragSource);
        if (dropTarget.Kind != SolitairePileKind.None)
        {
            DrawDropHighlight(drawList, layout, rounding, accent, scale, dropTarget);
        }
    }

    private void DrawStock(ImDrawListPtr drawList, SolitaireBoard board, in SolitaireLayout layout, float rounding,
        float scale)
    {
        var rect = layout.StockRect;
        if (board.StockCount == 0)
        {
            DrawEmptySlot(drawList, rect, rounding, scale);
            var center = rect.Center;
            var radius = layout.CardWidth * 0.24f;
            drawList.AddCircle(center, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.3f)), 24, 2f * scale);
            return;
        }

        DrawCardBack(drawList, rect, rounding, scale);
    }

    private void DrawWaste(ImDrawListPtr drawList, SolitaireBoard board, in SolitaireLayout layout, float rounding,
        float scale, in SolitaireHit dragSource)
    {
        var rect = layout.WasteRect;
        var fromTop = dragSource.Kind == SolitairePileKind.Waste ? 1 : 0;
        var card = board.WastePeek(fromTop);
        if (card < 0)
        {
            DrawEmptySlot(drawList, rect, rounding, scale);
            return;
        }

        DrawCardFace(drawList, rect, card, rounding, scale, false);
    }

    private void DrawFoundations(ImDrawListPtr drawList, SolitaireBoard board, in SolitaireLayout layout,
        float rounding, float scale, in SolitaireHit dragSource)
    {
        for (var suit = 0; suit < SolitaireBoard.SuitCount; suit++)
        {
            var rect = layout.FoundationRect(suit);
            var fromTop = dragSource.Kind == SolitairePileKind.Foundation && dragSource.Pile == suit ? 1 : 0;
            var card = board.FoundationPeek(suit, fromTop);
            if (card < 0)
            {
                DrawEmptySlot(drawList, rect, rounding, scale);
                DrawSuit(drawList, rect.Center, layout.CardWidth * 0.26f, suit, new Vector4(1f, 1f, 1f, 0.12f));
                continue;
            }

            DrawCardFace(drawList, rect, card, rounding, scale, false);
        }
    }

    private void DrawTableau(ImDrawListPtr drawList, SolitaireBoard board, in SolitaireLayout layout, float rounding,
        float scale, in SolitaireHit dragSource)
    {
        for (var pile = 0; pile < SolitaireBoard.TableauPiles; pile++)
        {
            var count = board.TableauCount(pile);
            if (count == 0)
            {
                DrawEmptySlot(drawList, layout.TableauBaseRect(pile), rounding, scale);
                continue;
            }

            var skipFrom = dragSource.Kind == SolitairePileKind.Tableau && dragSource.Pile == pile
                ? dragSource.CardIndex
                : count;
            for (var index = 0; index < count; index++)
            {
                if (index >= skipFrom)
                {
                    break;
                }

                var rect = layout.TableauCardRect(pile, index);
                if (board.IsTableauFaceUp(pile, index))
                {
                    DrawCardFace(drawList, rect, board.TableauCardAt(pile, index), rounding, scale, false);
                }
                else
                {
                    DrawCardBack(drawList, rect, rounding, scale);
                }
            }
        }
    }

    public void DrawFloating(in SolitaireLayout layout, ReadOnlySpan<int> cards, Vector2 topLeft, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = layout.CardWidth * 0.12f;
        for (var index = 0; index < cards.Length; index++)
        {
            var min = new Vector2(topLeft.X, topLeft.Y + index * layout.FanUp);
            var rect = new Rect(min, min + layout.CardSize);
            Elevation.Floating(drawList, rect.Min, rect.Max, rounding, scale, 0.6f);
            DrawCardFace(drawList, rect, cards[index], rounding, scale, true);
        }
    }

    private void DrawDropHighlight(ImDrawListPtr drawList, in SolitaireLayout layout, float rounding, Vector4 accent,
        float scale, in SolitaireHit target)
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

    private void DrawCardFace(ImDrawListPtr drawList, in Rect rect, int card, float rounding, float scale,
        bool floating)
    {
        if (!floating)
        {
            Squircle.Fill(drawList, rect.Min + new Vector2(0f, 1.5f * scale), rect.Max + new Vector2(0f, 1.5f * scale),
                rounding, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.22f)));
        }

        Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(Face));
        Squircle.Stroke(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.12f)),
            1f * scale);
        var suit = SolitaireBoard.Suit(card);
        var color = SolitaireBoard.IsRed(card) ? Red : Black;
        var labelScale = MathF.Max(0.5f, MathF.Min(0.9f, rect.Width / (40f * scale)));
        var label = RankLabel(SolitaireBoard.Rank(card));
        var corner = new Vector2(rect.Min.X + rect.Width * 0.18f, rect.Min.Y + rect.Height * 0.16f);
        Typography.DrawCentered(corner, label, color, labelScale, FontWeight.Bold);
        DrawSuit(drawList, new Vector2(corner.X, corner.Y + rect.Height * 0.2f), rect.Width * 0.1f, suit, color);
        DrawSuit(drawList, new Vector2(rect.Center.X, rect.Center.Y + rect.Height * 0.08f), rect.Width * 0.24f, suit,
            color);
    }

    private void DrawCardBack(ImDrawListPtr drawList, in Rect rect, float rounding, float scale)
    {
        Squircle.Fill(drawList, rect.Min + new Vector2(0f, 1.5f * scale), rect.Max + new Vector2(0f, 1.5f * scale),
            rounding, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.22f)));
        Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(BackFill));
        var inset = rect.Width * 0.12f;
        var innerMin = rect.Min + new Vector2(inset, inset);
        var innerMax = rect.Max - new Vector2(inset, inset);
        Squircle.Stroke(drawList, innerMin, innerMax, rounding * 0.6f, ImGui.GetColorU32(BackInner), 1.5f * scale);
        drawList.AddCircleFilled(rect.Center, rect.Width * 0.12f, ImGui.GetColorU32(BackInner), 20);
    }

    private void DrawEmptySlot(ImDrawListPtr drawList, in Rect rect, float rounding, float scale)
    {
        Squircle.Fill(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.04f)));
        Squircle.Stroke(drawList, rect.Min, rect.Max, rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.16f)),
            1.4f * scale);
    }

    private void DrawSuit(ImDrawListPtr drawList, Vector2 center, float radius, int suit, Vector4 color)
    {
        var packed = ImGui.GetColorU32(color);
        switch (suit)
        {
            case 2:
                DrawDiamond(drawList, center, radius, packed);
                break;
            case 1:
                DrawHeart(drawList, center, radius, packed);
                break;
            case 0:
                DrawSpade(drawList, center, radius, packed);
                break;
            default:
                DrawClub(drawList, center, radius, packed);
                break;
        }
    }

    private void DrawDiamond(ImDrawListPtr drawList, Vector2 center, float radius, uint packed)
    {
        Span<Vector2> points = stackalloc Vector2[4]
        {
            new(center.X, center.Y - radius), new(center.X + radius * 0.72f, center.Y),
            new(center.X, center.Y + radius), new(center.X - radius * 0.72f, center.Y),
        };
        FillConvex(drawList, packed, points);
    }

    private void DrawHeart(ImDrawListPtr drawList, Vector2 center, float radius, uint packed)
    {
        var lobe = radius * 0.5f;
        drawList.AddCircleFilled(new Vector2(center.X - lobe, center.Y - radius * 0.2f), lobe, packed, 20);
        drawList.AddCircleFilled(new Vector2(center.X + lobe, center.Y - radius * 0.2f), lobe, packed, 20);
        Span<Vector2> triangle = stackalloc Vector2[3]
        {
            new(center.X - radius * 0.98f, center.Y - radius * 0.08f),
            new(center.X + radius * 0.98f, center.Y - radius * 0.08f), new(center.X, center.Y + radius),
        };
        FillConvex(drawList, packed, triangle);
    }

    private void DrawSpade(ImDrawListPtr drawList, Vector2 center, float radius, uint packed)
    {
        var lobe = radius * 0.5f;
        drawList.AddCircleFilled(new Vector2(center.X - lobe, center.Y + radius * 0.2f), lobe, packed, 20);
        drawList.AddCircleFilled(new Vector2(center.X + lobe, center.Y + radius * 0.2f), lobe, packed, 20);
        Span<Vector2> triangle = stackalloc Vector2[3]
        {
            new(center.X - radius * 0.98f, center.Y + radius * 0.08f),
            new(center.X + radius * 0.98f, center.Y + radius * 0.08f), new(center.X, center.Y - radius),
        };
        FillConvex(drawList, packed, triangle);
        DrawStem(drawList, center, radius, packed);
    }

    private void DrawClub(ImDrawListPtr drawList, Vector2 center, float radius, uint packed)
    {
        var lobe = radius * 0.46f;
        drawList.AddCircleFilled(new Vector2(center.X, center.Y - radius * 0.42f), lobe, packed, 20);
        drawList.AddCircleFilled(new Vector2(center.X - radius * 0.52f, center.Y + radius * 0.18f), lobe, packed, 20);
        drawList.AddCircleFilled(new Vector2(center.X + radius * 0.52f, center.Y + radius * 0.18f), lobe, packed, 20);
        DrawStem(drawList, center, radius, packed);
    }

    private void DrawStem(ImDrawListPtr drawList, Vector2 center, float radius, uint packed)
    {
        Span<Vector2> stem = stackalloc Vector2[4]
        {
            new(center.X - radius * 0.12f, center.Y + radius * 0.18f),
            new(center.X + radius * 0.12f, center.Y + radius * 0.18f),
            new(center.X + radius * 0.26f, center.Y + radius * 0.98f),
            new(center.X - radius * 0.26f, center.Y + radius * 0.98f),
        };
        FillConvex(drawList, packed, stem);
    }

    private static void FillConvex(ImDrawListPtr drawList, uint color, ReadOnlySpan<Vector2> points)
    {
        drawList.PathClear();
        for (var index = 0; index < points.Length; index++)
        {
            drawList.PathLineTo(points[index]);
        }

        drawList.PathFillConvex(color);
    }

    private static string RankLabel(int rank)
    {
        return rank switch
        {
            0 => "A",
            9 => "10",
            10 => "J",
            11 => "Q",
            12 => "K",
            _ => GameNumber.Label(rank + 1),
        };
    }
}
