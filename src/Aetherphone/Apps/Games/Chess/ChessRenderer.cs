using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

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

    public Rect SquareRect(int square)
    {
        var min = SquareMin(square);
        return new Rect(min, min + new Vector2(CellSize, CellSize));
    }

    public ChessLayout Translate(Vector2 offset) => new(Origin + offset, CellSize);

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
    public readonly float Entrance;

    public ChessRenderState(int selected, int hovered, ulong targets, ulong captures, int lastFrom, int lastTo,
        int checkSquare, int movingFrom, int movingTo, float movingPhase, float entrance)
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
        Entrance = entrance;
    }

    public static ChessRenderState Idle => new(-1, -1, 0UL, 0UL, -1, -1, -1, -1, -1, 1f, 1f);
}

internal sealed class ChessRenderer
{
    private const float PieceHeightFraction = 0.66f;
    private const float MovingLift = 0.10f;
    private const float BaseShadowAlpha = 0.22f;
    private const float BaseShadowDrop = 0.40f;
    private const float BaseShadowWidth = 0.62f;
    private const float BaseShadowHeight = 0.18f;
    private const float CoordinateInset = 0.09f;
    private const float CapsuleIconHeight = 14f;
    private const float CapsuleIconStep = 5.5f;
    private const float CapsulePadX = 10f;
    private const float CapsuleGroupGap = 8f;
    private const float CapsuleLeadGap = 4f;
    private const float CapsuleEmptyDot = 2f;
    private const float CapsuleEmptyWidth = 6f;
    private const float CapsuleDividerInset = 0.28f;
    private const int CapsuleMaxShown = 4;
    private const float UndoPadX = 12f;
    private const float UndoIconSize = 12f;
    private const float UndoIconGap = 6f;
    private const float PromotionSlot = 56f;
    private const float PromotionPiece = 36f;
    private const float PromotionTitleGap = 14f;
    private const float PromotionVeil = 0.5f;
    private static readonly Vector4 LightSquare = new(0.85f, 0.87f, 0.90f, 1f);
    private static readonly Vector4 DarkSquare = new(0.42f, 0.52f, 0.62f, 1f);
    private static readonly Vector4 WhiteBody = new(0.97f, 0.97f, 0.98f, 1f);
    private static readonly Vector4 BlackBody = new(0.13f, 0.14f, 0.18f, 1f);
    private static readonly Vector4 WhiteRim = new(0.10f, 0.11f, 0.14f, 1f);
    private static readonly Vector4 BlackRim = new(0.88f, 0.89f, 0.92f, 1f);
    private static readonly Vector4 CheckGlow = new(0.94f, 0.30f, 0.32f, 1f);
    private static readonly Vector4 HoverWash = new(1f, 1f, 1f, 0.10f);
    private static readonly Vector4 QuietTarget = new(0.08f, 0.10f, 0.13f, 0.34f);
    private static readonly Vector4 CaptureTarget = new(0.08f, 0.10f, 0.13f, 0.42f);
    private static readonly string[] FileLetters = { "a", "b", "c", "d", "e", "f", "g", "h" };
    private static readonly TextStyle CapsuleStyle = TextStyles.FootnoteEmphasized;

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

    public void Draw(ImDrawListPtr drawList, ChessBoard board, in ChessLayout layout, in ChessRenderState state,
        Vector4 accent, float scale)
    {
        DrawSquares(drawList, layout, state, accent, scale);
        DrawTargets(drawList, layout, state);
        DrawPieces(drawList, board, layout, state, scale);
    }

    public static float CapturedCapsuleWidth(int leftCount, int rightCount, string leadLabel, int lead, float scale)
    {
        var width = CapsulePadX * 2f + CapsuleGroupGap * 2f + 1f + GroupWidth(leftCount) + GroupWidth(rightCount);
        if (lead != 0 && leadLabel.Length > 0)
        {
            width += CapsuleLeadGap + Typography.Measure(leadLabel, CapsuleStyle).X / scale;
        }

        return width;
    }

    public void DrawCapturedCapsule(ImDrawListPtr drawList, Rect rect, ReadOnlySpan<byte> leftPieces, int leftCount,
        ReadOnlySpan<byte> rightPieces, int rightCount, string leadLabel, int lead, PhoneTheme theme, float scale)
    {
        StageHud.Capsule(drawList, rect, scale);
        var centerY = rect.Center.Y;
        var penX = rect.Min.X + CapsulePadX * scale;
        penX = DrawGroup(drawList, penX, centerY, leftPieces, leftCount, theme, scale);
        if (lead > 0)
        {
            penX = DrawLead(drawList, penX, centerY, leadLabel, theme, scale);
        }

        penX += CapsuleGroupGap * scale;
        var inset = rect.Height * CapsuleDividerInset;
        drawList.AddLine(new Vector2(penX, rect.Min.Y + inset), new Vector2(penX, rect.Max.Y - inset),
            ImGui.GetColorU32(StageInks.Muted with { W = 0.35f }), 1f * scale);
        penX += (1f + CapsuleGroupGap) * scale;
        penX = DrawGroup(drawList, penX, centerY, rightPieces, rightCount, theme, scale);
        if (lead < 0)
        {
            DrawLead(drawList, penX, centerY, leadLabel, theme, scale);
        }
    }

    public static float UndoWidth(string label, float scale) =>
        (UndoPadX * 2f + UndoIconSize + UndoIconGap) * scale + Typography.Measure(label, CapsuleStyle).X;

    public static bool DrawUndoCapsule(ImDrawListPtr drawList, Rect rect, string label, Vector4 accent,
        PhoneTheme theme, bool enabled, float scale)
    {
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        StageHud.Capsule(drawList, rect, scale, enabled ? 0.92f : 0.55f);
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, rect.Height * 0.5f, ImGui.GetColorU32(accent with { W = 0.16f }));
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var dim = StageInks.Muted with { W = 0.45f };
        var iconInk = !enabled ? dim : hovered ? StageInks.Strong : accent;
        var iconSize = UndoIconSize * scale;
        var left = rect.Min.X + UndoPadX * scale;
        ProgressRing.CenterIcon(drawList, new Vector2(left + iconSize * 0.5f, rect.Center.Y), FontAwesomeIcon.Undo,
            iconInk, iconSize);
        Typography.Draw(drawList,
            new Vector2(left + iconSize + UndoIconGap * scale, rect.Center.Y - Typography.LineHeight(CapsuleStyle) * 0.5f),
            label, enabled ? StageInks.Strong : dim, CapsuleStyle);
        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public static ChessPieceType DrawPromotionPicker(Rect body, PhoneTheme theme, Vector4 accent, bool black,
        float progress, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var alpha = MathF.Min(1f, progress * 1.6f);
        Material.Veil(drawList, body.Min, body.Max, PromotionVeil * alpha);
        var grow = 0.88f + 0.12f * Easing.EaseOutBack(progress);
        var slot = PromotionSlot * scale * grow;
        var width = MathF.Min(body.Width * 0.9f, slot * 4f + CapsulePadX * 2f * scale);
        var center = body.Center;
        var half = new Vector2(width * 0.5f, slot * 0.5f);
        var rect = new Rect(center - half, center + half);
        Elevation.Floating(drawList, rect.Min, rect.Max, half.Y, scale, alpha);
        StageHud.Capsule(drawList, rect, scale, alpha);
        Squircle.Stroke(drawList, rect.Min, rect.Max, half.Y, ImGui.GetColorU32(accent with { W = 0.24f * alpha }),
            1f * scale);
        var titleHeight = Typography.LineHeight(TextStyles.Headline);
        Typography.DrawCentered(drawList, new Vector2(center.X, rect.Min.Y - PromotionTitleGap * scale - titleHeight * 0.5f),
            Loc.T(L.Games.Promote), StageInks.Strong with { W = alpha }, TextStyles.Headline);
        Span<ChessPieceType> choices = stackalloc ChessPieceType[4];
        choices[0] = ChessPieceType.Queen;
        choices[1] = ChessPieceType.Rook;
        choices[2] = ChessPieceType.Bishop;
        choices[3] = ChessPieceType.Knight;
        var slotWidth = (width - CapsulePadX * 2f * scale) / choices.Length;
        var result = ChessPieceType.None;
        for (var index = 0; index < choices.Length; index++)
        {
            var slotCenter = new Vector2(rect.Min.X + CapsulePadX * scale + (index + 0.5f) * slotWidth, center.Y);
            var slotHalf = new Vector2(slotWidth * 0.46f, slot * 0.44f);
            var hovered = UiInteract.Hover(slotCenter - slotHalf, slotCenter + slotHalf);
            if (hovered)
            {
                Squircle.Fill(drawList, slotCenter - slotHalf, slotCenter + slotHalf, slotHalf.Y,
                    ImGui.GetColorU32(accent with { W = 0.26f }));
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            DrawPiece(drawList, slotCenter, ChessPiece.Make(choices[index], black), PromotionPiece * scale * grow, scale,
                alpha);
            if (UiInteract.Click(slotCenter - slotHalf, slotCenter + slotHalf, hovered))
            {
                result = choices[index];
            }
        }

        return result;
    }

    public static void DrawPiece(ImDrawListPtr drawList, Vector2 center, byte piece, float targetHeight, float scale,
        float alpha)
    {
        var glyph = IconGlyph.Of(IconFor(ChessPiece.Type(piece)));
        using (Plugin.Fonts.PushIcon(targetHeight, glyph))
        {
            DrawPieceGlyph(drawList, glyph, center, piece, targetHeight, scale, alpha);
        }
    }

    private static void DrawRaisedPiece(ImDrawListPtr drawList, Vector2 center, byte piece, float targetHeight,
        float scale, float alpha)
    {
        var shadowCenter = center + new Vector2(0f, targetHeight * BaseShadowDrop);
        var shadowHalf = new Vector2(targetHeight * BaseShadowWidth * 0.5f, targetHeight * BaseShadowHeight * 0.5f);
        Squircle.Fill(drawList, shadowCenter - shadowHalf, shadowCenter + shadowHalf, shadowHalf.Y,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, BaseShadowAlpha * alpha)));
        DrawPiece(drawList, center, piece, targetHeight, scale, alpha);
    }

    private static void DrawSquares(ImDrawListPtr drawList, in ChessLayout layout, in ChessRenderState state,
        Vector4 accent, float scale)
    {
        var cellSize = layout.CellSize;
        var coordinateScale = MathF.Max(0.42f, MathF.Min(0.62f, cellSize / (56f * scale)));
        var size = new Vector2(cellSize, cellSize);
        var lastMove = ImGui.GetColorU32(accent with { W = 0.34f });
        var hover = ImGui.GetColorU32(HoverWash);
        var selectedFill = ImGui.GetColorU32(accent with { W = 0.45f });
        var selectedRim = ImGui.GetColorU32(GamePalette.Lighten(accent, 0.4f));
        var check = ImGui.GetColorU32(CheckGlow with { W = 0.30f + 0.22f * Pulse.Wave(Pulse.Calm) });
        for (var square = 0; square < ChessBoard.SquareCount; square++)
        {
            var column = ChessBoard.ColumnOf(square);
            var row = ChessBoard.RowOf(square);
            var light = (column + row) % 2 == 0;
            var min = layout.SquareMin(square);
            var max = min + size;
            StageCell.Draw(drawList, new Rect(min, max), light ? LightSquare : DarkSquare, CellDepth.Flat, 0f, scale);
            if (square == state.LastFrom || square == state.LastTo)
            {
                drawList.AddRectFilled(min, max, lastMove);
            }

            if (square == state.CheckSquare)
            {
                drawList.AddRectFilled(min, max, check);
            }

            if (square == state.Hovered && square != state.Selected)
            {
                drawList.AddRectFilled(min, max, hover);
            }

            if (square == state.Selected)
            {
                drawList.AddRectFilled(min, max, selectedFill);
                drawList.AddRect(min, max, selectedRim, 0f, ImDrawFlags.RoundCornersAll, 2f * scale);
            }

            DrawCoordinate(drawList, min, max, column, row, light, coordinateScale, cellSize);
        }
    }

    private static void DrawCoordinate(ImDrawListPtr drawList, Vector2 min, Vector2 max, int column, int row,
        bool light, float coordinateScale, float cellSize)
    {
        var ink = light ? DarkSquare : LightSquare;
        var inset = cellSize * CoordinateInset;
        if (column == 0)
        {
            Typography.Draw(drawList, new Vector2(min.X + inset, min.Y + inset * 0.6f),
                GameNumber.Label(ChessBoard.Size - row), ink, coordinateScale, FontWeight.SemiBold);
        }

        if (row != ChessBoard.Size - 1)
        {
            return;
        }

        var letter = FileLetters[column];
        var letterSize = Typography.Measure(letter, coordinateScale, FontWeight.SemiBold);
        Typography.Draw(drawList, new Vector2(max.X - inset - letterSize.X, max.Y - inset * 0.6f - letterSize.Y),
            letter, ink, coordinateScale, FontWeight.SemiBold);
    }

    private static void DrawTargets(ImDrawListPtr drawList, in ChessLayout layout, in ChessRenderState state)
    {
        if (state.Targets == 0)
        {
            return;
        }

        var cellSize = layout.CellSize;
        var quiet = ImGui.GetColorU32(QuietTarget);
        var capture = ImGui.GetColorU32(CaptureTarget);
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

    private static void DrawPieces(ImDrawListPtr drawList, ChessBoard board, in ChessLayout layout,
        in ChessRenderState state, float scale)
    {
        var pieceHeight = layout.CellSize * PieceHeightFraction;
        for (var square = 0; square < ChessBoard.SquareCount; square++)
        {
            var piece = board.PieceAt(square);
            if (piece == 0 || (square == state.MovingTo && state.MovingPhase < 1f))
            {
                continue;
            }

            var pop = state.Entrance < 1f
                ? GameJuice.PopIn(GameJuice.Stagger(state.Entrance, square, ChessBoard.SquareCount))
                : 1f;
            if (pop <= 0.02f)
            {
                continue;
            }

            DrawRaisedPiece(drawList, layout.SquareCenter(square), piece, pieceHeight * pop, scale, 1f);
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
        DrawRaisedPiece(drawList, center, moving, pieceHeight * (1f + MovingLift * (1f - eased)), scale, 1f);
    }

    private static float GroupWidth(int count)
    {
        var shown = Math.Min(count, CapsuleMaxShown);
        return shown == 0 ? CapsuleEmptyWidth : (shown - 1) * CapsuleIconStep + CapsuleIconHeight;
    }

    private static float DrawGroup(ImDrawListPtr drawList, float penX, float centerY, ReadOnlySpan<byte> pieces,
        int count, PhoneTheme theme, float scale)
    {
        var shown = Math.Min(count, CapsuleMaxShown);
        if (shown == 0)
        {
            drawList.AddCircleFilled(new Vector2(penX + CapsuleEmptyWidth * scale * 0.5f, centerY), CapsuleEmptyDot * scale,
                ImGui.GetColorU32(StageInks.Muted with { W = 0.4f }), 10);
            return penX + CapsuleEmptyWidth * scale;
        }

        var height = CapsuleIconHeight * scale;
        var step = CapsuleIconStep * scale;
        var first = count - shown;
        for (var index = 0; index < shown; index++)
        {
            DrawPiece(drawList, new Vector2(penX + height * 0.5f + index * step, centerY), pieces[first + index], height,
                scale, 0.9f);
        }

        return penX + (shown - 1) * step + height;
    }

    private static float DrawLead(ImDrawListPtr drawList, float penX, float centerY, string leadLabel, PhoneTheme theme,
        float scale)
    {
        if (leadLabel.Length == 0)
        {
            return penX;
        }

        penX += CapsuleLeadGap * scale;
        Typography.Draw(drawList, new Vector2(penX, centerY - Typography.LineHeight(CapsuleStyle) * 0.5f), leadLabel,
            StageInks.Strong, CapsuleStyle);
        return penX + Typography.Measure(leadLabel, CapsuleStyle).X;
    }

    private static void DrawPieceGlyph(ImDrawListPtr drawList, string glyph, Vector2 center, byte piece,
        float targetHeight, float scale, float alpha)
    {
        var font = ImGui.GetFont();
        var baseSize = ImGui.GetFontSize();
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
}
