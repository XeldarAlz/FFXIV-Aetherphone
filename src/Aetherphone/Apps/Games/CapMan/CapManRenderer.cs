using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.CapMan;

internal sealed class CapManRenderer
{
    public static readonly Vector4 PlayerColor = new(1f, 0.85f, 0.30f, 1f);
    public static readonly Vector4 DotColor = new(1f, 0.95f, 0.85f, 1f);
    public static readonly Vector4 FrightColor = new(0.35f, 0.45f, 0.98f, 1f);
    public static readonly Vector4 EyeWhite = new(1f, 1f, 1f, 1f);
    public static readonly Vector2 Half = new(0.5f, 0.5f);
    private static readonly Vector4 FrightFlash = new(0.95f, 0.95f, 1f, 1f);
    private static readonly Vector4 Pupil = new(0.1f, 0.12f, 0.3f, 1f);
    private static readonly Vector4 Cherry = new(0.95f, 0.22f, 0.28f, 1f);
    private static readonly Vector4 Strawberry = new(0.98f, 0.32f, 0.42f, 1f);
    private static readonly Vector4 Orange = new(1f, 0.60f, 0.22f, 1f);
    private static readonly Vector4 Apple = new(0.60f, 0.86f, 0.36f, 1f);
    private static readonly Vector4 Melon = new(0.30f, 0.72f, 0.42f, 1f);
    private static readonly Vector4 MelonStripe = new(0.14f, 0.45f, 0.26f, 1f);
    private static readonly Vector4 Stem = new(0.50f, 0.72f, 0.32f, 1f);
    private static readonly Vector4 Seed = new(1f, 0.96f, 0.72f, 1f);
    private static readonly Vector4[] GhostColors =
    {
        new(0.98f, 0.35f, 0.35f, 1f), new(0.98f, 0.55f, 0.85f, 1f), new(0.40f, 0.90f, 0.95f, 1f), new(1f, 0.70f, 0.35f, 1f),
    };
    private const float FrightWarningSeconds = 2f;
    private const float MouthRate = 9f;
    private const float IdleMouth = 0.15f;
    private const float PelletPulseRate = 4f;
    private const float WallPulseRate = 10f;
    private const float FruitBobRate = 5f;

    public static Vector4 GhostColor(int personality) => GhostColors[personality % GhostColors.Length];

    public static Rect BoardRect(in Camera2D camera, out float cell)
    {
        cell = camera.Px(1f);
        return new Rect(camera.ToScreen(Vector2.Zero), camera.ToScreen(new Vector2(CapManBoard.Columns, CapManBoard.Rows)));
    }

    public static Vector2 ToScreen(Rect board, float cell, Vector2 tile) => board.Min + (tile + Half) * cell;

    public void Draw(ImDrawListPtr drawList, CapManBoard board, in Camera2D camera, Vector4 accent, float scale)
    {
        var boardRect = BoardRect(in camera, out var cell);
        drawList.PushClipRect(boardRect.Min, boardRect.Max, true);
        var time = (float)ImGui.GetTime();
        DrawTiles(drawList, board, boardRect, cell, accent, scale, time);
        DrawFruit(drawList, board, boardRect, cell, scale, time);
        DrawGhosts(drawList, board, boardRect, cell, time);
        DrawPlayer(drawList, board, boardRect, cell, time);
        drawList.PopClipRect();
    }

    private static void DrawTiles(ImDrawListPtr drawList, CapManBoard board, Rect boardRect, float cell, Vector4 accent,
        float scale, float time)
    {
        var fright = board.FrightRemaining > 0f ? 0.5f + 0.5f * MathF.Sin(time * WallPulseRate) : 0f;
        var wallBase = GamePalette.Darken(accent, 0.4f);
        var wallLit = GamePalette.Lighten(accent, 0.2f);
        var wallFill = ImGui.GetColorU32(Vector4.Lerp(wallBase, wallLit, fright * 0.55f) with { W = 0.92f });
        var wallEdge = ImGui.GetColorU32(Vector4.Lerp(GamePalette.Lighten(accent, 0.25f), FrightFlash, fright * 0.6f) with
        {
            W = 0.55f + 0.4f * fright,
        });
        var door = ImGui.GetColorU32(accent with { W = 0.5f });
        var dot = ImGui.GetColorU32(DotColor);
        var inset = MathF.Max(0.5f, cell * 0.06f);
        var rounding = cell * 0.22f;
        var dotRadius = MathF.Max(1.2f, cell * 0.09f);
        var pulse = 0.7f + MathF.Abs(MathF.Sin(time * PelletPulseRate)) * 0.5f;
        var edgeThickness = MathF.Max(1f, scale * (1f + fright));
        for (var row = 0; row < CapManBoard.Rows; row++)
        {
            for (var column = 0; column < CapManBoard.Columns; column++)
            {
                var tile = board.Tile(column, row);
                var min = boardRect.Min + new Vector2(column * cell, row * cell);
                var center = min + new Vector2(cell * 0.5f, cell * 0.5f);
                switch (tile)
                {
                    case CapManBoard.Wall:
                        drawList.AddRectFilled(min + new Vector2(inset, inset), min + new Vector2(cell - inset, cell - inset),
                            wallFill, rounding);
                        drawList.AddRect(min + new Vector2(inset, inset), min + new Vector2(cell - inset, cell - inset),
                            wallEdge, rounding, ImDrawFlags.None, edgeThickness);
                        break;
                    case CapManBoard.Door:
                        drawList.AddRectFilled(new Vector2(min.X + inset, min.Y + cell * 0.42f),
                            new Vector2(min.X + cell - inset, min.Y + cell * 0.58f), door);
                        break;
                    case CapManBoard.Dot:
                        drawList.AddCircleFilled(center, dotRadius, dot, 8);
                        break;
                    case CapManBoard.Pellet:
                        ProgressRing.Glow(center, cell * 0.4f, PlayerColor, 0.5f * pulse);
                        drawList.AddCircleFilled(center, cell * 0.24f * pulse, ImGui.GetColorU32(PlayerColor), 14);
                        break;
                }
            }
        }
    }

    private static void DrawFruit(ImDrawListPtr drawList, CapManBoard board, Rect boardRect, float cell, float scale,
        float time)
    {
        if (!board.FruitActive)
        {
            return;
        }

        var center = ToScreen(boardRect, cell, board.FruitPosition);
        center.Y += MathF.Sin(time * FruitBobRate) * cell * 0.04f;
        var radius = cell * 0.30f;
        var ringRadius = cell * 0.46f;
        var thickness = MathF.Max(1f, 1.5f * scale);
        ProgressRing.Track(drawList, center, ringRadius, thickness, EyeWhite with { W = 0.2f });
        ProgressRing.Fill(drawList, center, ringRadius, thickness, board.FruitRemaining / CapManBoard.FruitSeconds, PlayerColor);
        switch (board.Fruit)
        {
            case FruitKind.Cherry:
                DrawCherry(drawList, center, radius, scale);
                return;
            case FruitKind.Strawberry:
                DrawStrawberry(drawList, center, radius);
                return;
            case FruitKind.Orange:
                DrawOrange(drawList, center, radius);
                return;
            case FruitKind.Apple:
                DrawApple(drawList, center, radius, scale);
                return;
            default:
                DrawMelon(drawList, center, radius, scale);
                return;
        }
    }

    private static void DrawCherry(ImDrawListPtr drawList, Vector2 center, float radius, float scale)
    {
        var fill = ImGui.GetColorU32(Cherry);
        var stem = ImGui.GetColorU32(Stem);
        var left = center + new Vector2(-radius * 0.45f, radius * 0.35f);
        var right = center + new Vector2(radius * 0.5f, radius * 0.45f);
        var top = center + new Vector2(radius * 0.1f, -radius * 0.9f);
        drawList.AddLine(left, top, stem, MathF.Max(1f, 1.5f * scale));
        drawList.AddLine(right, top, stem, MathF.Max(1f, 1.5f * scale));
        ProgressRing.Glow(center, radius * 1.4f, Cherry, 0.5f);
        drawList.AddCircleFilled(left, radius * 0.5f, fill, 14);
        drawList.AddCircleFilled(right, radius * 0.5f, fill, 14);
        drawList.AddCircleFilled(left - new Vector2(radius * 0.15f, radius * 0.15f), radius * 0.14f,
            ImGui.GetColorU32(EyeWhite with { W = 0.6f }), 8);
    }

    private static void DrawStrawberry(ImDrawListPtr drawList, Vector2 center, float radius)
    {
        var fill = ImGui.GetColorU32(Strawberry);
        ProgressRing.Glow(center, radius * 1.4f, Strawberry, 0.5f);
        drawList.AddTriangleFilled(center + new Vector2(-radius * 0.9f, -radius * 0.3f),
            center + new Vector2(radius * 0.9f, -radius * 0.3f), center + new Vector2(0f, radius), fill);
        drawList.AddCircleFilled(center + new Vector2(0f, -radius * 0.3f), radius * 0.9f, fill, 16);
        var seed = ImGui.GetColorU32(Seed);
        drawList.AddCircleFilled(center + new Vector2(-radius * 0.35f, -radius * 0.2f), radius * 0.1f, seed, 6);
        drawList.AddCircleFilled(center + new Vector2(radius * 0.3f, -radius * 0.35f), radius * 0.1f, seed, 6);
        drawList.AddCircleFilled(center + new Vector2(0f, radius * 0.25f), radius * 0.1f, seed, 6);
        var leaf = ImGui.GetColorU32(Stem);
        drawList.AddTriangleFilled(center + new Vector2(-radius * 0.5f, -radius * 0.9f),
            center + new Vector2(radius * 0.5f, -radius * 0.9f), center + new Vector2(0f, -radius * 0.4f), leaf);
    }

    private static void DrawOrange(ImDrawListPtr drawList, Vector2 center, float radius)
    {
        ProgressRing.Glow(center, radius * 1.4f, Orange, 0.5f);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Orange), 20);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.3f, radius * 0.3f), radius * 0.28f,
            ImGui.GetColorU32(EyeWhite with { W = 0.5f }), 10);
        var leaf = ImGui.GetColorU32(Stem);
        drawList.AddTriangleFilled(center + new Vector2(0f, -radius * 0.9f), center + new Vector2(radius * 0.7f, -radius * 1.25f),
            center + new Vector2(radius * 0.25f, -radius * 0.55f), leaf);
    }

    private static void DrawApple(ImDrawListPtr drawList, Vector2 center, float radius, float scale)
    {
        ProgressRing.Glow(center, radius * 1.4f, Apple, 0.5f);
        var fill = ImGui.GetColorU32(Apple);
        drawList.AddCircleFilled(center + new Vector2(-radius * 0.3f, 0f), radius * 0.75f, fill, 16);
        drawList.AddCircleFilled(center + new Vector2(radius * 0.3f, 0f), radius * 0.75f, fill, 16);
        drawList.AddLine(center + new Vector2(0f, -radius * 0.6f), center + new Vector2(radius * 0.15f, -radius * 1.2f),
            ImGui.GetColorU32(Stem), MathF.Max(1f, 1.5f * scale));
        drawList.AddCircleFilled(center - new Vector2(radius * 0.45f, radius * 0.35f), radius * 0.2f,
            ImGui.GetColorU32(EyeWhite with { W = 0.5f }), 8);
    }

    private static void DrawMelon(ImDrawListPtr drawList, Vector2 center, float radius, float scale)
    {
        ProgressRing.Glow(center, radius * 1.4f, Melon, 0.5f);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Melon), 20);
        var stripe = ImGui.GetColorU32(MelonStripe);
        var thickness = MathF.Max(1f, 1.2f * scale);
        drawList.AddLine(center + new Vector2(0f, -radius), center + new Vector2(0f, radius), stripe, thickness);
        drawList.AddLine(center + new Vector2(-radius * 0.6f, -radius * 0.8f), center + new Vector2(-radius * 0.6f, radius * 0.8f),
            stripe, thickness);
        drawList.AddLine(center + new Vector2(radius * 0.6f, -radius * 0.8f), center + new Vector2(radius * 0.6f, radius * 0.8f),
            stripe, thickness);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.35f, radius * 0.4f), radius * 0.18f,
            ImGui.GetColorU32(EyeWhite with { W = 0.45f }), 8);
    }

    private static void DrawPlayer(ImDrawListPtr drawList, CapManBoard board, Rect boardRect, float cell, float time)
    {
        var center = ToScreen(boardRect, cell, board.PlayerPosition);
        var radius = cell * 0.42f;
        var direction = board.PlayerDirection;
        var moving = direction != Vector2.Zero && !board.Frozen;
        var mouth = moving ? MathF.Abs(MathF.Sin(time * MouthRate)) * 0.5f : IdleMouth;
        if (board.Dying)
        {
            var progress = board.DeathProgress;
            radius *= 1f - progress;
            mouth = IdleMouth + progress * (MathF.PI - IdleMouth);
            if (radius <= 0.5f)
            {
                return;
            }
        }

        var angle = direction == Vector2.Zero ? 0f : MathF.Atan2(direction.Y, direction.X);
        var color = ImGui.GetColorU32(PlayerColor);
        ProgressRing.Glow(center, radius * 1.3f, PlayerColor, 0.45f);
        drawList.PathClear();
        drawList.PathLineTo(center);
        drawList.PathArcTo(center, radius, angle + mouth, angle + MathF.PI * 2f - mouth, 24);
        drawList.PathFillConvex(color);
    }

    private static void DrawGhosts(ImDrawListPtr drawList, CapManBoard board, Rect boardRect, float cell, float time)
    {
        var warning = board.FrightRemaining > 0f && board.FrightRemaining < FrightWarningSeconds &&
            MathF.Sin(time * 14f) > 0f;
        for (var index = 0; index < CapManBoard.GhostCount; index++)
        {
            var ghost = board.GetGhost(index);
            var center = ToScreen(boardRect, cell, ghost.Position);
            var radius = cell * 0.42f;
            if (ghost.State == GhostState.Eyes)
            {
                ProgressRing.Glow(center, radius * 0.9f, EyeWhite, 0.25f);
                DrawEyes(drawList, center, radius, ghost.Direction, EyeWhite);
                continue;
            }

            var body = ghost.State == GhostState.Frightened ? (warning ? FrightFlash : FrightColor) : GhostColor(ghost.Personality);
            DrawGhostBody(drawList, center, radius, body);
            if (ghost.State == GhostState.Frightened)
            {
                DrawFrightFace(drawList, center, radius, warning ? FrightColor : FrightFlash);
                continue;
            }

            DrawEyes(drawList, center, radius, ghost.Direction, EyeWhite);
        }
    }

    private static void DrawGhostBody(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color)
    {
        var fill = ImGui.GetColorU32(color);
        var domeCenter = new Vector2(center.X, center.Y - radius * 0.15f);
        var bottom = center.Y + radius;
        ProgressRing.Glow(center, radius * 1.25f, color, 0.35f);
        drawList.AddCircleFilled(domeCenter, radius, fill, 20);
        drawList.AddRectFilled(new Vector2(center.X - radius, domeCenter.Y), new Vector2(center.X + radius, bottom - radius * 0.3f),
            fill);
        var bump = radius / 3f;
        for (var bumpIndex = 0; bumpIndex < 3; bumpIndex++)
        {
            var bumpCenter = new Vector2(center.X - radius + bump + bumpIndex * bump * 2f, bottom - bump);
            drawList.AddCircleFilled(bumpCenter, bump, fill, 10);
        }
    }

    private static void DrawEyes(ImDrawListPtr drawList, Vector2 center, float radius, Vector2 direction, Vector4 color)
    {
        var eyeRadius = radius * 0.26f;
        var eyeY = center.Y - radius * 0.27f;
        var white = ImGui.GetColorU32(color);
        var pupil = ImGui.GetColorU32(Pupil);
        var look = direction * eyeRadius * 0.45f;
        for (var side = -1; side <= 1; side += 2)
        {
            var eye = new Vector2(center.X + side * radius * 0.38f, eyeY);
            drawList.AddCircleFilled(eye, eyeRadius, white, 12);
            drawList.AddCircleFilled(eye + look, eyeRadius * 0.5f, pupil, 8);
        }
    }

    private static void DrawFrightFace(ImDrawListPtr drawList, Vector2 center, float radius, Vector4 color)
    {
        var ink = ImGui.GetColorU32(color);
        var eyeRadius = radius * 0.14f;
        var eyeY = center.Y - radius * 0.27f;
        drawList.AddCircleFilled(new Vector2(center.X - radius * 0.36f, eyeY), eyeRadius, ink, 8);
        drawList.AddCircleFilled(new Vector2(center.X + radius * 0.36f, eyeY), eyeRadius, ink, 8);
        var mouthY = center.Y + radius * 0.35f;
        var thickness = MathF.Max(1f, radius * 0.12f);
        for (var segment = 0; segment < 4; segment++)
        {
            var from = new Vector2(center.X - radius * 0.6f + segment * radius * 0.3f, mouthY + (segment % 2 == 0 ? radius * 0.12f : -radius * 0.12f));
            var to = new Vector2(from.X + radius * 0.3f, mouthY + (segment % 2 == 0 ? -radius * 0.12f : radius * 0.12f));
            drawList.AddLine(from, to, ink, thickness);
        }
    }
}
