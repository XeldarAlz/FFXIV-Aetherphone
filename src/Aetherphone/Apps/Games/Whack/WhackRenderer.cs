using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Whack;

internal static class WhackRenderer
{
    public const float GapFraction = 0.12f;
    private const float CellRadius = 12f;
    private const int BladesPerCell = 3;
    private static readonly Vector4 Lawn = new(0.30f, 0.54f, 0.34f, 1f);
    private static readonly Vector4 Hole = new(0.16f, 0.12f, 0.10f, 1f);
    private static readonly Vector4 MoleBody = new(0.58f, 0.42f, 0.30f, 1f);
    private static readonly Vector4 MoleBelly = new(0.84f, 0.70f, 0.54f, 1f);
    private static readonly Vector4 GoldBody = new(0.94f, 0.74f, 0.26f, 1f);
    private static readonly Vector4 GoldBelly = new(1f, 0.92f, 0.62f, 1f);
    private static readonly Vector4 BombBody = new(0.18f, 0.19f, 0.24f, 1f);
    private static readonly Vector4 BombShine = new(0.5f, 0.52f, 0.58f, 0.7f);
    private static readonly Vector4 Fuse = new(0.7f, 0.6f, 0.4f, 1f);
    private static readonly Vector4 FuseSpark = new(1f, 0.7f, 0.25f, 1f);
    private static readonly Vector4 Nose = new(0.95f, 0.55f, 0.6f, 1f);
    private static readonly Vector4 Ink = new(0.1f, 0.1f, 0.12f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.4f);

    public static void DrawBoard(ImDrawListPtr drawList, WhackBoard board, in GameGrid grid, float entrance,
        float scale, Vector4 accent, StageInk ink)
    {
        BoardPlate.Draw(drawList, BoardPlate.Around(grid.Bounds, scale), BoardPlate.Radius * scale, scale, accent, ink);
        var grass = ImGui.GetColorU32(GamePalette.Darken(Lawn, 0.2f));
        for (var hole = 0; hole < WhackBoard.HoleCount; hole++)
        {
            var lift = StageCell.Lift(GameJuice.Stagger(entrance, hole, WhackBoard.HoleCount)) * scale;
            var cell = grid.Cell(hole % WhackBoard.Columns, hole / WhackBoard.Columns)
                .Translate(new Vector2(0f, -lift));
            StageCell.Draw(drawList, cell, Lawn, CellDepth.Raised, CellRadius * scale, scale);
            DrawGrass(drawList, cell, hole, grass, scale);
            DrawHole(drawList, board, hole, cell, grid.Pitch, scale);
        }
    }

    private static void DrawGrass(ImDrawListPtr drawList, Rect cell, int hole, uint color, float scale)
    {
        var seed = hole * 37;
        for (var blade = 0; blade < BladesPerCell; blade++)
        {
            var offsetX = (seed + blade * 53) % 100 / 100f;
            var offsetY = (seed + blade * 71) % 100 / 100f;
            var root = new Vector2(cell.Min.X + cell.Width * offsetX, cell.Min.Y + cell.Height * (0.05f + offsetY * 0.22f));
            var height = cell.Height * 0.05f;
            var lean = (offsetX - 0.5f) * height;
            drawList.AddLine(root, root + new Vector2(lean, -height), color, 1.4f * scale);
        }
    }

    private static void DrawHole(ImDrawListPtr drawList, WhackBoard board, int hole, Rect cell, float pitch,
        float scale)
    {
        var openingCenter = new Vector2(cell.Center.X, cell.Center.Y + pitch * 0.16f);
        var holeWidth = pitch * 0.62f;
        var holeHeight = pitch * 0.30f;
        DrawSquashed(drawList, openingCenter, holeWidth, holeHeight, ImGui.GetColorU32(Hole));
        DrawSquashed(drawList, new Vector2(openingCenter.X, openingCenter.Y - holeHeight * 0.18f), holeWidth * 0.84f,
            holeHeight * 0.7f, ImGui.GetColorU32(Shadow));
        var height = board.HeightAt(hole);
        var occupant = board.KindAt(hole);
        if (height > 0.02f && occupant != Occupant.None)
        {
            DrawSquashed(drawList, openingCenter, holeWidth * (0.5f + 0.4f * height), holeHeight * 0.5f,
                ImGui.GetColorU32(Shadow with { W = 0.22f * height }));
            var overshoot = 1f + 0.08f * MathF.Sin(MathF.Min(1f, height) * MathF.PI);
            var center = new Vector2(openingCenter.X, openingCenter.Y - height * pitch * 0.46f);
            var radius = pitch * 0.26f * overshoot;
            if (occupant == Occupant.Bomb)
            {
                if (!board.WhackedAt(hole))
                {
                    DrawBomb(drawList, center, radius, scale);
                }
            }
            else
            {
                DrawMole(drawList, center, radius, board.SquashAt(hole), board.Frenzy, scale);
            }
        }

        DrawSquashed(drawList, new Vector2(openingCenter.X, openingCenter.Y + holeHeight * 0.16f), holeWidth * 1.02f,
            holeHeight * 0.66f, ImGui.GetColorU32(Lawn));
    }

    private static void DrawMole(ImDrawListPtr drawList, Vector2 center, float radius, float squash, bool gold,
        float scale)
    {
        var body = gold ? GoldBody : MoleBody;
        var belly = gold ? GoldBelly : MoleBelly;
        if (gold)
        {
            ProgressRing.Glow(center, radius * 1.5f, GoldBody, 0.4f);
        }

        if (squash > 0f)
        {
            var width = radius * (2f + 0.7f * squash);
            var height = radius * (2f - 0.6f * squash);
            var squashedCenter = center + new Vector2(0f, radius * 0.3f * squash);
            DrawSquashed(drawList, squashedCenter, width, height, ImGui.GetColorU32(body));
            DrawSquashed(drawList, squashedCenter + new Vector2(0f, height * 0.18f), width * 0.6f, height * 0.5f,
                ImGui.GetColorU32(belly));
            var eyeSpan = width * 0.18f;
            var eyeLift = height * 0.08f;
            DrawCross(drawList, squashedCenter + new Vector2(-eyeSpan, -eyeLift), radius * 0.16f, scale);
            DrawCross(drawList, squashedCenter + new Vector2(eyeSpan, -eyeLift), radius * 0.16f, scale);
            drawList.AddCircleFilled(squashedCenter + new Vector2(0f, height * 0.06f), radius * 0.16f,
                ImGui.GetColorU32(Nose), 12);
            return;
        }

        var bodyColor = ImGui.GetColorU32(body);
        drawList.AddCircleFilled(center + new Vector2(-radius * 0.6f, -radius * 0.5f), radius * 0.32f, bodyColor, 16);
        drawList.AddCircleFilled(center + new Vector2(radius * 0.6f, -radius * 0.5f), radius * 0.32f, bodyColor, 16);
        drawList.AddCircleFilled(center, radius, bodyColor, 28);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.3f, radius * 0.42f), radius * 0.28f,
            ImGui.GetColorU32(GamePalette.Lighten(body, 0.18f)), 16);
        DrawSquashed(drawList, center + new Vector2(0f, radius * 0.34f), radius * 1.3f, radius * 0.9f,
            ImGui.GetColorU32(belly));
        var leftEye = center + new Vector2(-radius * 0.36f, -radius * 0.18f);
        var rightEye = center + new Vector2(radius * 0.36f, -radius * 0.18f);
        var white = ImGui.GetColorU32(White);
        var pupil = ImGui.GetColorU32(Ink);
        drawList.AddCircleFilled(leftEye, radius * 0.2f, white, 14);
        drawList.AddCircleFilled(rightEye, radius * 0.2f, white, 14);
        drawList.AddCircleFilled(leftEye, radius * 0.1f, pupil, 10);
        drawList.AddCircleFilled(rightEye, radius * 0.1f, pupil, 10);
        drawList.AddCircleFilled(center + new Vector2(0f, radius * 0.12f), radius * 0.16f, ImGui.GetColorU32(Nose), 12);
    }

    private static void DrawBomb(ImDrawListPtr drawList, Vector2 center, float radius, float scale)
    {
        drawList.AddCircleFilled(center, radius * 0.92f, ImGui.GetColorU32(BombBody), 28);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.3f, radius * 0.3f), radius * 0.26f,
            ImGui.GetColorU32(BombShine), 16);
        var fuseStart = center + new Vector2(radius * 0.4f, -radius * 0.8f);
        var fuseEnd = center + new Vector2(radius * 0.8f, -radius * 1.3f);
        drawList.AddLine(fuseStart, fuseEnd, ImGui.GetColorU32(Fuse), 2.4f * scale);
        var spark = 0.6f + 0.4f * Pulse.Wave(Pulse.Fast);
        drawList.AddCircleFilled(fuseEnd, radius * 0.18f * spark, ImGui.GetColorU32(FuseSpark), 12);
    }

    private static void DrawCross(ImDrawListPtr drawList, Vector2 center, float size, float scale)
    {
        var color = ImGui.GetColorU32(Ink);
        var thickness = 2f * scale;
        drawList.AddLine(center - new Vector2(size, size), center + new Vector2(size, size), color, thickness);
        drawList.AddLine(center - new Vector2(size, -size), center + new Vector2(size, -size), color, thickness);
    }

    private static void DrawSquashed(ImDrawListPtr drawList, Vector2 center, float width, float height, uint color)
    {
        var min = new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f);
        var max = new Vector2(center.X + width * 0.5f, center.Y + height * 0.5f);
        Squircle.Fill(drawList, min, max, height * 0.5f, color);
    }
}
