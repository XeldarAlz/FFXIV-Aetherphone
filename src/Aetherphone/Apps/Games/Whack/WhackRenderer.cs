using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Whack;

internal sealed class WhackRenderer
{
    private static readonly Vector4 Lawn = new(0.30f, 0.54f, 0.34f, 1f);
    private static readonly Vector4 Hole = new(0.16f, 0.12f, 0.10f, 1f);
    private static readonly Vector4 MoleBody = new(0.58f, 0.42f, 0.30f, 1f);
    private static readonly Vector4 MoleBelly = new(0.84f, 0.70f, 0.54f, 1f);
    private static readonly Vector4 BombBody = new(0.18f, 0.19f, 0.24f, 1f);

    public static Rect HoleArea(GameGrid grid, int hole)
    {
        return grid.Cell(hole % WhackBoard.Columns, hole / WhackBoard.Columns);
    }

    public void Draw(WhackBoard board, GameGrid grid, PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var lawnMin = grid.Origin - new Vector2(8f * scale, 8f * scale);
        var lawnMax = grid.Origin + new Vector2(grid.Width, grid.Height) + new Vector2(8f * scale, 8f * scale);
        Squircle.Fill(drawList, lawnMin, lawnMax, 18f * scale, ImGui.GetColorU32(GamePalette.Darken(Lawn, 0.3f)));
        Squircle.Fill(drawList, grid.Origin, new Vector2(grid.Origin.X + grid.Width, grid.Origin.Y + grid.Height),
            12f * scale, ImGui.GetColorU32(Lawn));
        for (var hole = 0; hole < WhackBoard.HoleCount; hole++)
        {
            DrawHole(drawList, board, grid, hole, scale);
        }
    }

    private void DrawHole(ImDrawListPtr drawList, WhackBoard board, GameGrid grid, int hole, float scale)
    {
        var pitch = grid.Pitch;
        var cellCenter = grid.CellCenter(hole % WhackBoard.Columns, hole / WhackBoard.Columns);
        var openingCenter = new Vector2(cellCenter.X, cellCenter.Y + pitch * 0.16f);
        var holeWidth = pitch * 0.62f;
        var holeHeight = pitch * 0.30f;
        DrawSquashed(drawList, openingCenter, holeWidth, holeHeight, ImGui.GetColorU32(Hole));
        DrawSquashed(drawList, new Vector2(openingCenter.X, openingCenter.Y - holeHeight * 0.18f), holeWidth * 0.84f,
            holeHeight * 0.7f, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.4f)));
        var height = board.HeightAt(hole);
        if (height > 0.02f && board.KindAt(hole) != Occupant.None)
        {
            var moleCenter = new Vector2(openingCenter.X, openingCenter.Y - height * pitch * 0.46f);
            var radius = pitch * 0.26f;
            if (board.KindAt(hole) == Occupant.Bomb)
            {
                DrawBomb(drawList, moleCenter, radius, scale);
            }
            else
            {
                DrawMole(drawList, moleCenter, radius, board.WhackedAt(hole), scale);
            }
        }

        DrawSquashed(drawList, new Vector2(openingCenter.X, openingCenter.Y + holeHeight * 0.16f), holeWidth * 1.02f,
            holeHeight * 0.66f, ImGui.GetColorU32(Lawn));
    }

    private void DrawMole(ImDrawListPtr drawList, Vector2 center, float radius, bool whacked, float scale)
    {
        drawList.AddCircleFilled(center + new Vector2(-radius * 0.6f, -radius * 0.5f), radius * 0.32f,
            ImGui.GetColorU32(MoleBody), 16);
        drawList.AddCircleFilled(center + new Vector2(radius * 0.6f, -radius * 0.5f), radius * 0.32f,
            ImGui.GetColorU32(MoleBody), 16);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(MoleBody), 28);
        DrawSquashed(drawList, center + new Vector2(0f, radius * 0.34f), radius * 1.3f, radius * 0.9f,
            ImGui.GetColorU32(MoleBelly));
        var leftEye = center + new Vector2(-radius * 0.36f, -radius * 0.18f);
        var rightEye = center + new Vector2(radius * 0.36f, -radius * 0.18f);
        if (whacked)
        {
            DrawCross(drawList, leftEye, radius * 0.16f, scale);
            DrawCross(drawList, rightEye, radius * 0.16f, scale);
        }
        else
        {
            var white = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f));
            var pupil = ImGui.GetColorU32(new Vector4(0.1f, 0.1f, 0.12f, 1f));
            drawList.AddCircleFilled(leftEye, radius * 0.2f, white, 14);
            drawList.AddCircleFilled(rightEye, radius * 0.2f, white, 14);
            drawList.AddCircleFilled(leftEye, radius * 0.1f, pupil, 10);
            drawList.AddCircleFilled(rightEye, radius * 0.1f, pupil, 10);
        }

        drawList.AddCircleFilled(center + new Vector2(0f, radius * 0.12f), radius * 0.16f,
            ImGui.GetColorU32(new Vector4(0.95f, 0.55f, 0.6f, 1f)), 12);
    }

    private void DrawBomb(ImDrawListPtr drawList, Vector2 center, float radius, float scale)
    {
        drawList.AddCircleFilled(center, radius * 0.92f, ImGui.GetColorU32(BombBody), 28);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.3f, radius * 0.3f), radius * 0.26f,
            ImGui.GetColorU32(new Vector4(0.5f, 0.52f, 0.58f, 0.7f)), 16);
        var fuseStart = center + new Vector2(radius * 0.4f, -radius * 0.8f);
        var fuseEnd = center + new Vector2(radius * 0.8f, -radius * 1.3f);
        drawList.AddLine(fuseStart, fuseEnd, ImGui.GetColorU32(new Vector4(0.7f, 0.6f, 0.4f, 1f)), 2.4f * scale);
        var spark = 0.6f + 0.4f * Styling.Pulse(Styling.PulseFast);
        drawList.AddCircleFilled(fuseEnd, radius * 0.18f * spark, ImGui.GetColorU32(new Vector4(1f, 0.7f, 0.25f, 1f)),
            12);
    }

    private void DrawCross(ImDrawListPtr drawList, Vector2 center, float size, float scale)
    {
        var color = ImGui.GetColorU32(new Vector4(0.1f, 0.1f, 0.12f, 1f));
        var thickness = 2f * scale;
        drawList.AddLine(center - new Vector2(size, size), center + new Vector2(size, size), color, thickness);
        drawList.AddLine(center - new Vector2(size, -size), center + new Vector2(size, -size), color, thickness);
    }

    private void DrawSquashed(ImDrawListPtr drawList, Vector2 center, float width, float height, uint color)
    {
        var min = new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f);
        var max = new Vector2(center.X + width * 0.5f, center.Y + height * 0.5f);
        Squircle.Fill(drawList, min, max, height * 0.5f, color);
    }
}
