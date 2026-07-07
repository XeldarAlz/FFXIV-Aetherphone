using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Reversi;

internal sealed class ReversiRenderer
{
    private static readonly Vector4 Felt = new(0.16f, 0.42f, 0.30f, 1f);
    private static readonly Vector4 DarkDisc = new(0.13f, 0.14f, 0.19f, 1f);
    private static readonly Vector4 LightDisc = new(0.94f, 0.95f, 0.97f, 1f);

    public void Draw(ReversiBoard board, GameGrid grid, float[] flipTimer, int[] flipFrom, float[] placeTimer,
        float flipDuration, float placeDuration, bool showHints, int hintPlayer, Vector4 accent, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var boardMin = grid.Origin - new Vector2(7f * scale, 7f * scale);
        var boardMax = grid.Origin + new Vector2(grid.Width, grid.Height) + new Vector2(7f * scale, 7f * scale);
        Squircle.Fill(drawList, boardMin, boardMax, 16f * scale, ImGui.GetColorU32(GamePalette.Darken(Felt, 0.4f)));
        Squircle.Fill(drawList, grid.Origin, new Vector2(grid.Origin.X + grid.Width, grid.Origin.Y + grid.Height),
            6f * scale, ImGui.GetColorU32(Felt));
        DrawGridLines(drawList, grid, scale);
        var radius = grid.Pitch * 0.40f;
        var hintPulse = 0.3f + 0.4f * Pulse.Wave(Pulse.Calm);
        for (var row = 0; row < ReversiBoard.Size; row++)
        {
            for (var column = 0; column < ReversiBoard.Size; column++)
            {
                var index = row * ReversiBoard.Size + column;
                var center = grid.CellCenter(column, row);
                DrawCell(drawList, board, index, center, radius, flipTimer[index], flipFrom[index], placeTimer[index],
                    flipDuration, placeDuration, showHints, hintPlayer, accent, hintPulse, scale);
            }
        }
    }

    private void DrawCell(ImDrawListPtr drawList, ReversiBoard board, int index, Vector2 center, float radius,
        float flip, int flipFrom, float place, float flipDuration, float placeDuration, bool showHints, int hintPlayer,
        Vector4 accent, float hintPulse, float scale)
    {
        if (flip > 0f)
        {
            if (flip > flipDuration)
            {
                DrawDisc(drawList, center, radius, 1f, 1f, flipFrom, scale);
                return;
            }

            var progress = 1f - flip / flipDuration;
            var squash = MathF.Abs(MathF.Cos(progress * MathF.PI));
            var shown = progress < 0.5f ? flipFrom : board.Cell(index);
            DrawDisc(drawList, center, radius, MathF.Max(0.06f, squash), 1f, shown, scale);
            return;
        }

        if (place > 0f)
        {
            var progress = 1f - place / placeDuration;
            DrawDisc(drawList, center, radius, 1f, Easing.EaseOutBack(progress), board.Cell(index), scale);
            return;
        }

        var color = board.Cell(index);
        if (color != 0)
        {
            DrawDisc(drawList, center, radius, 1f, 1f, color, scale);
            return;
        }

        if (showHints && board.IsLegal(index, hintPlayer))
        {
            drawList.AddCircleFilled(center, radius * 0.3f, ImGui.GetColorU32(accent with { W = hintPulse }), 20);
        }
    }

    private void DrawDisc(ImDrawListPtr drawList, Vector2 center, float radius, float squashY, float popScale,
        int colorIndex, float scale)
    {
        var effective = radius * popScale;
        var halfWidth = effective;
        var halfHeight = MathF.Max(0.5f, effective * squashY);
        var min = new Vector2(center.X - halfWidth, center.Y - halfHeight);
        var max = new Vector2(center.X + halfWidth, center.Y + halfHeight);
        var corner = MathF.Min(halfWidth, halfHeight);
        var body = colorIndex == ReversiBoard.Dark ? DarkDisc : LightDisc;
        Squircle.Fill(drawList, min + new Vector2(0f, 2f * scale), max + new Vector2(0f, 2f * scale), corner,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.28f)));
        Squircle.Fill(drawList, min, max, corner, ImGui.GetColorU32(body));
        Squircle.Fill(drawList, min, new Vector2(max.X, center.Y), corner,
            ImGui.GetColorU32(GamePalette.Lighten(body, 0.22f) with { W = 0.5f }));
        Squircle.Stroke(drawList, min, max, corner, ImGui.GetColorU32(GamePalette.Darken(body, 0.3f) with { W = 0.6f }),
            1f * scale);
    }

    private void DrawGridLines(ImDrawListPtr drawList, GameGrid grid, float scale)
    {
        var color = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.22f));
        for (var line = 0; line <= ReversiBoard.Size; line++)
        {
            var x = grid.Origin.X + line * grid.Pitch;
            drawList.AddLine(new Vector2(x, grid.Origin.Y), new Vector2(x, grid.Origin.Y + grid.Height), color,
                1f * scale);
            var y = grid.Origin.Y + line * grid.Pitch;
            drawList.AddLine(new Vector2(grid.Origin.X, y), new Vector2(grid.Origin.X + grid.Width, y), color,
                1f * scale);
        }
    }
}
