using Aetherphone.Apps.Casino.Stage;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Casino;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Cabinets;

internal static class BingoCardArt
{
    public const int Columns = BingoRules.Columns;

    private const string FreeMark = "FREE";
    private const float DaubRadius = 0.40f;
    private const float PlatePad = 5f;
    private const float BallBand = 0.62f;
    private const float CageSpokes = 6f;

    private static readonly string[] Letters = { "B", "I", "N", "G", "O" };

    private static readonly Vector4 Paper = new(0.105f, 0.075f, 0.170f, 1f);
    private static readonly Vector4 PaperEdge = new(0.30f, 0.24f, 0.42f, 1f);
    private static readonly Vector4 BallInk = new(0.09f, 0.08f, 0.12f, 1f);
    private static readonly Vector4 Glass = new(0.55f, 0.75f, 1f, 1f);

    public static readonly Vector4[] ColumnTints =
    {
        new(0.310f, 0.588f, 0.855f, 1f),
        new(0.243f, 0.686f, 0.604f, 1f),
        new(0.898f, 0.690f, 0.298f, 1f),
        new(0.882f, 0.451f, 0.404f, 1f),
        new(0.612f, 0.460f, 0.840f, 1f),
    };

    private static readonly Vector4[] LetterInks =
    {
        WheelRingArt.InkOn(ColumnTints[0]), WheelRingArt.InkOn(ColumnTints[1]), WheelRingArt.InkOn(ColumnTints[2]),
        WheelRingArt.InkOn(ColumnTints[3]), WheelRingArt.InkOn(ColumnTints[4]),
    };

    public static Rect CellRect(Rect card, int cellIndex)
    {
        var cell = card.Width / Columns;
        var min = new Vector2(card.Min.X + BingoRules.ColumnOfCell(cellIndex) * cell,
            card.Min.Y + BingoRules.RowOfCell(cellIndex) * cell);
        return new Rect(min, min + new Vector2(cell, cell));
    }

    public static void DrawPlate(ImDrawListPtr drawList, Rect card, bool hero, float glow, float scale)
    {
        var pad = PlatePad * scale;
        var min = card.Min - new Vector2(pad, pad);
        var max = card.Max + new Vector2(pad, pad);
        var rounding = Metrics.Radius.Sm * scale;
        Elevation.Card(drawList, min, max, rounding, scale, hero ? 1f : 0.6f);
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(Paper));
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(PaperEdge with { W = 0.8f }),
            MathF.Max(1f, scale));
        if (glow > 0f)
        {
            Squircle.Stroke(drawList, min - new Vector2(scale, scale), max + new Vector2(scale, scale), rounding,
                ImGui.GetColorU32(CasinoColors.Money with { W = glow }), 2f * scale);
        }
    }

    public static void DrawCard(ImDrawListPtr drawList, Rect card, int[]? numbers, int visibleMask, int stampedMask,
        BingoRoundPlayback playback, int cardIndex, float scale)
    {
        for (var cellIndex = 0; cellIndex < BingoRules.Cells; cellIndex++)
        {
            DrawCell(drawList, CellRect(card, cellIndex), cellIndex, numbers, visibleMask, stampedMask, playback,
                cardIndex, scale);
        }
    }

    private static void DrawCell(ImDrawListPtr drawList, Rect rect, int cellIndex, int[]? numbers, int visibleMask,
        int stampedMask, BingoRoundPlayback playback, int cardIndex, float scale)
    {
        var center = rect.Center;
        var radius = rect.Width * DaubRadius;
        var tint = ColumnTints[BingoRules.ColumnOfCell(cellIndex)];
        if (cellIndex == BingoRules.FreeCell)
        {
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(CasinoColors.LightA with { W = 0.75f }), 24);
            Typography.DrawCentered(drawList, center, FreeMark, CasinoColors.InkTitle, ScaleForRadius(radius) * 0.72f,
                FontWeight.Bold);
            return;
        }

        var slot = BingoRules.SlotForCell(cellIndex);
        if (numbers is null || slot < 0 || slot >= numbers.Length || !BingoRules.IsBall(numbers[slot]))
        {
            return;
        }

        var bit = 1 << cellIndex;
        var marked = (stampedMask & bit) != 0;
        var pending = !marked && (visibleMask & bit) != 0;
        if (marked)
        {
            var pop = playback.PopOf(cardIndex, cellIndex);
            var progress = pop <= 0f ? 1f : Easing.EaseOutBack(1f - pop / BingoRoundPlayback.PopSeconds);
            var size = radius * MathF.Max(0.1f, progress);
            if (pop > 0f)
            {
                drawList.AddCircleFilled(center, size * 1.5f, ImGui.GetColorU32(tint with { W = 0.25f * pop }), 24);
            }

            drawList.AddCircleFilled(center, size, ImGui.GetColorU32(tint with { W = 0.9f }), 24);
        }
        else if (pending)
        {
            var breath = 0.35f + 0.55f * Pulse.Wave(Pulse.Fast);
            drawList.AddCircle(center, radius, ImGui.GetColorU32(tint with { W = breath }), 24, 1.8f * scale);
        }

        Typography.DrawCentered(drawList, center, GameNumber.Label(numbers[slot]),
            marked ? CasinoColors.InkTitle : CasinoColors.InkBody, ScaleForRadius(radius),
            marked ? FontWeight.Bold : FontWeight.Medium);
    }

    public static void DrawMini(ImDrawListPtr drawList, Rect card, int visibleMask, int stampedMask, float scale)
    {
        var cell = card.Width / Columns;
        var dot = cell * 0.30f;
        for (var cellIndex = 0; cellIndex < BingoRules.Cells; cellIndex++)
        {
            var center = CellRect(card, cellIndex).Center;
            var bit = 1 << cellIndex;
            var tint = ColumnTints[BingoRules.ColumnOfCell(cellIndex)];
            if ((stampedMask & bit) != 0)
            {
                drawList.AddCircleFilled(center, dot * 1.25f, ImGui.GetColorU32(tint with { W = 0.9f }), 12);
                continue;
            }

            var alpha = (visibleMask & bit) != 0 ? 0.6f : 0.16f;
            drawList.AddCircleFilled(center, dot, ImGui.GetColorU32(CasinoColors.InkMuted with { W = alpha }), 12);
        }
    }

    public static void DrawBall(ImDrawListPtr drawList, Vector2 center, float radius, int ball, float alpha)
    {
        var column = BingoRules.ColumnOfBall(ball);
        var tint = column >= 0 ? ColumnTints[column] : CasinoColors.InkMuted;
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(tint with { W = alpha }), 32);
        drawList.AddCircleFilled(center, radius * BallBand,
            ImGui.GetColorU32(CasinoColors.InkTitle with { W = alpha }), 32);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.35f, radius * 0.4f), radius * 0.18f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.45f * alpha)), 12);
        if (radius < 5f)
        {
            return;
        }

        Typography.DrawCentered(drawList, center, GameNumber.Label(ball), BallInk with { W = alpha },
            ScaleForRadius(radius * BallBand), FontWeight.Bold);
    }

    public static void DrawBoard(ImDrawListPtr drawList, Rect board, float cell, BingoRoundPlayback playback,
        float scale)
    {
        var rounding = Metrics.Radius.Sm * scale;
        Squircle.Fill(drawList, board.Min - new Vector2(3f * scale, 3f * scale),
            board.Max + new Vector2(3f * scale, 3f * scale), rounding, ImGui.GetColorU32(Paper with { W = 0.86f }));
        var dot = cell * 0.42f;
        for (var column = 0; column < BingoRules.Columns; column++)
        {
            var tint = ColumnTints[column];
            var rowCenterY = board.Min.Y + (column + 0.5f) * cell;
            var letterCenter = new Vector2(board.Min.X + cell * 0.5f, rowCenterY);
            drawList.AddCircleFilled(letterCenter, dot, ImGui.GetColorU32(tint with { W = 0.9f }), 16);
            Typography.DrawCentered(drawList, letterCenter, Letters[column], LetterInks[column],
                ScaleForRadius(dot), FontWeight.Bold);
            for (var offset = 0; offset < BingoRules.NumbersPerColumn; offset++)
            {
                var ball = BingoRules.ColumnFloorFor(column) + offset;
                var center = new Vector2(board.Min.X + (offset + 1.5f) * cell, rowCenterY);
                var lit = playback.IsLit(ball);
                if (lit)
                {
                    var latest = ball == playback.LatestBall;
                    if (latest)
                    {
                        var breath = 0.5f + 0.5f * Pulse.Wave(Pulse.Breath);
                        drawList.AddCircleFilled(center, dot * 1.45f,
                            ImGui.GetColorU32(CasinoColors.Money with { W = 0.20f + 0.25f * breath }), 16);
                    }

                    drawList.AddCircleFilled(center, dot, ImGui.GetColorU32(tint), 16);
                    Typography.DrawCentered(drawList, center, GameNumber.Label(ball), BallInk, ScaleForRadius(dot),
                        FontWeight.Bold);
                    continue;
                }

                Typography.DrawCentered(drawList, center, GameNumber.Label(ball), CasinoColors.InkMuted,
                    ScaleForRadius(dot), FontWeight.Medium);
            }
        }
    }

    public static Vector2 TumblerPoint(Rect rect, Vector2 world)
    {
        var radius = rect.Width * 0.5f;
        return rect.Center + world * (radius / BingoTumbler.DrumRadius);
    }

    public static void DrawTumbler(ImDrawListPtr drawList, Rect rect, BingoTumbler tumbler, float lit, float scale)
    {
        var center = rect.Center;
        var radius = rect.Width * 0.5f;
        drawList.AddCircleFilled(center, radius + 4f * scale, ImGui.GetColorU32(Paper with { W = 0.9f }), 48);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Glass with { W = 0.06f }), 48);
        var ballRadius = BingoTumbler.BallRadius * radius / BingoTumbler.DrumRadius;
        for (var index = 0; index < BingoTumbler.BallCount; index++)
        {
            if (index == tumbler.HiddenBall)
            {
                continue;
            }

            var at = TumblerPoint(rect, tumbler.BallPosition(index));
            var tint = ColumnTints[tumbler.TintOf(index)];
            drawList.AddCircleFilled(at, ballRadius, ImGui.GetColorU32(tint), 16);
            drawList.AddCircleFilled(at - new Vector2(ballRadius * 0.3f, ballRadius * 0.35f), ballRadius * 0.3f,
                ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.5f)), 8);
        }

        var spokeInk = ImGui.GetColorU32(CasinoColors.MoneyHighlight with { W = 0.22f + 0.2f * lit });
        for (var spoke = 0; spoke < CageSpokes; spoke++)
        {
            var angle = tumbler.CageAngle + spoke * MathF.PI / CageSpokes;
            var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            drawList.AddLine(center - direction, center + direction, spokeInk, MathF.Max(1f, scale));
        }

        drawList.AddCircle(center, radius, ImGui.GetColorU32(CasinoColors.Money with { W = 0.55f + 0.35f * lit }), 48,
            2.2f * scale);
        drawList.AddCircleFilled(center, 4f * scale, ImGui.GetColorU32(CasinoColors.Money), 12);
        var exit = TumblerPoint(rect, BingoTumbler.Exit);
        drawList.AddRectFilled(exit - new Vector2(ballRadius * 1.4f, ballRadius * 1.6f),
            exit + new Vector2(ballRadius * 1.4f, ballRadius * 0.2f), ImGui.GetColorU32(CasinoColors.Money), 2f * scale);
    }

    public static float ScaleForRadius(float radius)
    {
        var scale = radius / (13f * MathF.Max(0.5f, UiScale.Current));
        return Math.Clamp(scale, 0.5f, 3.00f);
    }
}
