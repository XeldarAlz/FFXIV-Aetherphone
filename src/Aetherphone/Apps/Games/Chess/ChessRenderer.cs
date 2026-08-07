using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Games.Chess;

internal readonly struct ChessLayout
{
    public readonly Vector2 Origin;
    public readonly float CellSize;

    public ChessLayout(Vector2 origin, float cellSize)
    {
        Origin = origin;
        CellSize = cellSize;
    }

    public float BoardSize => CellSize * ChessBoard.Size;

    public Vector2 Center => Origin + new Vector2(BoardSize * 0.5f, BoardSize * 0.5f);

    public Rect Bounds => new(Origin, Origin + new Vector2(BoardSize, BoardSize));

    public Vector2 SquareMin(int square) =>
        new(Origin.X + ChessBoard.ColumnOf(square) * CellSize, Origin.Y + ChessBoard.RowOf(square) * CellSize);

    public Vector2 SquareCenter(int square) =>
        new(Origin.X + (ChessBoard.ColumnOf(square) + 0.5f) * CellSize,
            Origin.Y + (ChessBoard.RowOf(square) + 0.5f) * CellSize);

    public int HitTest(Vector2 point)
    {
        var column = (int)MathF.Floor((point.X - Origin.X) / CellSize);
        var row = (int)MathF.Floor((point.Y - Origin.Y) / CellSize);
        if (column < 0 || column >= ChessBoard.Size || row < 0 || row >= ChessBoard.Size)
        {
            return -1;
        }

        return row * ChessBoard.Size + column;
    }
}

internal readonly struct ChessRenderState
{
    public readonly int Selected;
    public readonly int Hovered;
    public readonly ulong Targets;
    public readonly ulong Captures;
    public readonly int LastFrom;
    public readonly int LastTo;
    public readonly int CheckSquare;
    public readonly int MovingFrom;
    public readonly int MovingTo;
    public readonly float MovingPhase;

    public ChessRenderState(int selected, int hovered, ulong targets, ulong captures, int lastFrom, int lastTo,
        int checkSquare, int movingFrom, int movingTo, float movingPhase)
    {
        Selected = selected;
        Hovered = hovered;
        Targets = targets;
        Captures = captures;
        LastFrom = lastFrom;
        LastTo = lastTo;
        CheckSquare = checkSquare;
        MovingFrom = movingFrom;
        MovingTo = movingTo;
        MovingPhase = movingPhase;
    }
}

internal sealed class ChessRenderer
{
    private static readonly Vector4 LightSquare = new(0.85f, 0.87f, 0.90f, 1f);
    private static readonly Vector4 DarkSquare = new(0.42f, 0.52f, 0.62f, 1f);
    private static readonly Vector4 WhiteBody = new(0.97f, 0.97f, 0.98f, 1f);
    private static readonly Vector4 BlackBody = new(0.13f, 0.14f, 0.18f, 1f);
    private static readonly Vector4 WhiteRim = new(0.10f, 0.11f, 0.14f, 1f);
    private static readonly Vector4 BlackRim = new(0.88f, 0.89f, 0.92f, 1f);
    private static readonly Vector4 CheckGlow = new(0.94f, 0.30f, 0.32f, 1f);

    private static readonly Vector2[] RimOffsets =
    {
        new(1f, 0f), new(-1f, 0f), new(0f, 1f), new(0f, -1f),
    };

    public static ChessLayout Layout(Rect area)
    {
        var span = MathF.Min(area.Width, area.Height);
        var cellSize = span / ChessBoard.Size;
        var origin = new Vector2(area.Center.X - span * 0.5f, area.Center.Y - span * 0.5f);
        return new ChessLayout(origin, cellSize);
    }

    public void Draw(ChessBoard board, in ChessLayout layout, in ChessRenderState state, Vector4 accent, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var bounds = layout.Bounds;
        GameScene.Arena(drawList, bounds, Metrics.Radius.Md * scale, scale, accent);
        DrawSquares(drawList, layout, state, accent, scale);
        DrawTargets(drawList, board, layout, state, scale);
        DrawPieces(drawList, board, layout, state, scale);
    }

    private void DrawSquares(ImDrawListPtr drawList, in ChessLayout layout, in ChessRenderState state, Vector4 accent,
        float scale)
    {
        var cellSize = layout.CellSize;
        var coordinateScale = MathF.Max(0.42f, MathF.Min(0.62f, cellSize / (56f * scale)));
        for (var square = 0; square < ChessBoard.SquareCount; square++)
        {
            var column = ChessBoard.ColumnOf(square);
            var row = ChessBoard.RowOf(square);
            var light = (column + row) % 2 == 0;
            var min = layout.SquareMin(square);
            var max = min + new Vector2(cellSize, cellSize);
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(light ? LightSquare : DarkSquare));
            if (square == state.LastFrom || square == state.LastTo)
            {
                drawList.AddRectFilled(min, max, ImGui.GetColorU32(accent with { W = 0.34f }));
            }

            if (square == state.CheckSquare)
            {
                drawList.AddRectFilled(min, max,
                    ImGui.GetColorU32(CheckGlow with { W = 0.30f + 0.22f * Pulse.Wave(Pulse.Calm) }));
            }

            if (square == state.Hovered && square != state.Selected)
            {
                drawList.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)));
            }

            if (square == state.Selected)
            {
                drawList.AddRectFilled(min, max, ImGui.GetColorU32(accent with { W = 0.45f }));
                drawList.AddRect(min, max, ImGui.GetColorU32(GamePalette.Lighten(accent, 0.4f)), 0f,
                    ImDrawFlags.RoundCornersAll, 2f * scale);
            }

            DrawCoordinate(drawList, min, max, column, row, light, coordinateScale, cellSize);
        }
    }

    private static void DrawCoordinate(ImDrawListPtr drawList, Vector2 min, Vector2 max, int column, int row,
        bool light, float coordinateScale, float cellSize)
    {
        var ink = light ? DarkSquare : LightSquare;
        var inset = cellSize * 0.09f;
        if (column == 0)
        {
            Typography.Draw(drawList, new Vector2(min.X + inset, min.Y + inset * 0.6f),
                GameNumber.Label(ChessBoard.Size - row), ink, coordinateScale, FontWeight.SemiBold);
        }

        if (row != ChessBoard.Size - 1)
        {
            return;
        }

        var letter = ((char)('a' + column)).ToString();
        var letterSize = Typography.Measure(letter, coordinateScale, FontWeight.SemiBold);
        Typography.Draw(drawList, new Vector2(max.X - inset - letterSize.X, max.Y - inset * 0.6f - letterSize.Y),
            letter, ink, coordinateScale, FontWeight.SemiBold);
    }

    private void DrawTargets(ImDrawListPtr drawList, ChessBoard board, in ChessLayout layout,
        in ChessRenderState state, float scale)
    {
        if (state.Targets == 0)
        {
            return;
        }

        var cellSize = layout.CellSize;
        var quiet = ImGui.GetColorU32(new Vector4(0.08f, 0.10f, 0.13f, 0.34f));
        var capture = ImGui.GetColorU32(new Vector4(0.08f, 0.10f, 0.13f, 0.42f));
        for (var square = 0; square < ChessBoard.SquareCount; square++)
        {
            if ((state.Targets & (1UL << square)) == 0)
            {
                continue;
            }

            var center = layout.SquareCenter(square);
            if ((state.Captures & (1UL << square)) != 0)
            {
                drawList.AddCircle(center, cellSize * 0.42f, capture, 0, cellSize * 0.10f);
                continue;
            }

            drawList.AddCircleFilled(center, cellSize * 0.16f, quiet, 24);
        }
    }

    private void DrawPieces(ImDrawListPtr drawList, ChessBoard board, in ChessLayout layout,
        in ChessRenderState state, float scale)
    {
        var pieceHeight = layout.CellSize * 0.66f;
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var font = ImGui.GetFont();
            var baseSize = ImGui.GetFontSize();
            for (var square = 0; square < ChessBoard.SquareCount; square++)
            {
                var piece = board.PieceAt(square);
                if (piece == 0 || (square == state.MovingTo && state.MovingPhase < 1f))
                {
                    continue;
                }

                DrawPiece(drawList, font, baseSize, layout.SquareCenter(square), piece, pieceHeight, scale, 1f);
            }

            if (state.MovingTo < 0 || state.MovingPhase >= 1f)
            {
                return;
            }

            var moving = board.PieceAt(state.MovingTo);
            if (moving == 0)
            {
                return;
            }

            var eased = Easing.EaseOutCubic(state.MovingPhase);
            var from = layout.SquareCenter(state.MovingFrom);
            var to = layout.SquareCenter(state.MovingTo);
            var center = Vector2.Lerp(from, to, eased);
            DrawPiece(drawList, font, baseSize, center, moving, pieceHeight * (1f + 0.10f * (1f - eased)), scale, 1f);
        }
    }

    public void DrawCaptured(ImDrawListPtr drawList, Rect row, ReadOnlySpan<byte> pieces, int count, int advantage,
        PhoneTheme theme, float scale)
    {
        var glyphHeight = row.Height * 0.78f;
        var step = glyphHeight * 0.62f;
        var penX = row.Min.X;
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var font = ImGui.GetFont();
            var baseSize = ImGui.GetFontSize();
            for (var index = 0; index < count; index++)
            {
                DrawPiece(drawList, font, baseSize, new Vector2(penX + step * 0.5f, row.Center.Y), pieces[index],
                    glyphHeight, scale, 0.75f);
                penX += step;
            }
        }

        if (advantage <= 0)
        {
            return;
        }

        Typography.Draw(drawList, new Vector2(penX + 4f * scale, row.Center.Y - 7f * scale),
            string.Concat("+", GameNumber.Label(advantage)), theme.TextMuted, TextStyles.Caption1);
    }

    private static void DrawPiece(ImDrawListPtr drawList, ImFontPtr font, float baseSize, Vector2 center, byte piece,
        float targetHeight, float scale, float alpha)
    {
        var glyph = IconFor(ChessPiece.Type(piece)).ToIconString();
        var measured = ImGui.CalcTextSize(glyph);
        if (measured.Y <= 0f)
        {
            return;
        }

        var glyphScale = targetHeight / measured.Y;
        var size = measured * glyphScale;
        var position = new Vector2(center.X - size.X * 0.5f, center.Y - size.Y * 0.5f);
        var fontSize = baseSize * glyphScale;
        var black = ChessPiece.IsBlack(piece);
        drawList.AddText(font, fontSize, position + new Vector2(0f, 2f * scale),
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.30f * alpha)), glyph);
        var rim = ImGui.GetColorU32((black ? BlackRim : WhiteRim) with { W = alpha });
        var rimOffset = MathF.Max(1f, targetHeight * 0.035f);
        for (var index = 0; index < RimOffsets.Length; index++)
        {
            drawList.AddText(font, fontSize, position + RimOffsets[index] * rimOffset, rim, glyph);
        }

        drawList.AddText(font, fontSize, position, ImGui.GetColorU32((black ? BlackBody : WhiteBody) with { W = alpha }),
            glyph);
    }

    private static FontAwesomeIcon IconFor(ChessPieceType type)
    {
        return type switch
        {
            ChessPieceType.Pawn => FontAwesomeIcon.ChessPawn,
            ChessPieceType.Knight => FontAwesomeIcon.ChessKnight,
            ChessPieceType.Bishop => FontAwesomeIcon.ChessBishop,
            ChessPieceType.Rook => FontAwesomeIcon.ChessRook,
            ChessPieceType.Queen => FontAwesomeIcon.ChessQueen,
            _ => FontAwesomeIcon.ChessKing,
        };
    }

    public static ChessPieceType DrawPromotionPicker(Rect body, PhoneTheme theme, Vector4 accent, bool black,
        float progress, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var alpha = MathF.Min(1f, progress * 1.6f);
        Material.Veil(drawList, body.Min, body.Max, 0.58f * alpha);
        var cardWidth = MathF.Min(body.Width * 0.86f, 268f * scale);
        var cardHeight = 118f * scale;
        var grow = 0.88f + 0.12f * Easing.EaseOutBack(progress);
        var half = new Vector2(cardWidth, cardHeight) * 0.5f * grow;
        var center = body.Center;
        var min = center - half;
        var max = center + half;
        var radius = Metrics.Radius.Lg * scale;
        Elevation.Floating(drawList, min, max, radius, scale, alpha);
        Material.Frosted(drawList, min, max, radius, scale, alpha);
        Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(accent with { W = 0.24f * alpha }), 1f * scale);
        Typography.DrawCentered(new Vector2(center.X, min.Y + 20f * scale), Loc.T(L.Games.Promote),
            theme.TextStrong with { W = alpha }, TextStyles.Headline);
        Span<ChessPieceType> choices = stackalloc ChessPieceType[4];
        choices[0] = ChessPieceType.Queen;
        choices[1] = ChessPieceType.Rook;
        choices[2] = ChessPieceType.Bishop;
        choices[3] = ChessPieceType.Knight;
        var slotWidth = cardWidth * grow / 4f;
        var result = ChessPieceType.None;
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var font = ImGui.GetFont();
            var baseSize = ImGui.GetFontSize();
            for (var index = 0; index < choices.Length; index++)
            {
                var slotCenter = new Vector2(min.X + (index + 0.5f) * slotWidth, center.Y + 14f * scale);
                var slotHalf = new Vector2(slotWidth * 0.42f, 30f * scale);
                var hovered = UiInteract.Hover(slotCenter - slotHalf, slotCenter + slotHalf);
                if (hovered)
                {
                    Squircle.Fill(drawList, slotCenter - slotHalf, slotCenter + slotHalf, Metrics.Radius.Sm * scale,
                        ImGui.GetColorU32(accent with { W = 0.26f }));
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }

                DrawPiece(drawList, font, baseSize, slotCenter, ChessPiece.Make(choices[index], black), 40f * scale,
                    scale, alpha);
                if (UiInteract.Click(slotCenter - slotHalf, slotCenter + slotHalf, hovered))
                {
                    result = choices[index];
                }
            }
        }

        return result;
    }
}
