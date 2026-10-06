using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Breakout;

internal static class BreakoutRenderer
{
    public static readonly Vector4 BallColor = new(0.99f, 0.99f, 1f, 1f);
    public static readonly Vector4 ArmourColor = new(0.60f, 0.64f, 0.72f, 1f);
    public static readonly Vector4 ExplosiveColor = new(1f, 0.50f, 0.26f, 1f);
    public static readonly Vector4 MultiBallColor = new(0.46f, 0.86f, 0.66f, 1f);
    public static readonly Vector4 WideColor = new(0.96f, 0.74f, 0.34f, 1f);
    private const float FieldRadius = 0.03f;
    private const float PowerUpBob = 0.15f;
    private const float ArmourRivet = 0.22f;
    private static readonly Vector4[] BrickColors =
    {
        new(0.95f, 0.45f, 0.50f, 1f), new(0.96f, 0.62f, 0.32f, 1f), new(0.92f, 0.82f, 0.36f, 1f),
        new(0.46f, 0.86f, 0.62f, 1f), new(0.40f, 0.70f, 0.98f, 1f), new(0.72f, 0.50f, 0.96f, 1f),
    };

    private static readonly Vector4 FieldFill = new(0.02f, 0.02f, 0.05f, 0.45f);
    private static readonly Vector4 FieldRim = new(1f, 1f, 1f, 0.14f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Ink = new(0.08f, 0.08f, 0.10f, 1f);
    private static readonly Vector4 Crack = new(0.12f, 0.12f, 0.15f, 0.8f);
    private static readonly Vector4 Core = new(1f, 0.90f, 0.55f, 1f);

    public static Vector4 BrickColorOf(int color) => BrickColors[color % BrickColors.Length];

    public static Vector4 ColorFor(BrickKind kind, int color) => kind switch
    {
        BrickKind.Armoured => ArmourColor,
        BrickKind.Explosive => ExplosiveColor,
        _ => BrickColorOf(color),
    };

    public static Rect FieldRect(in Camera2D camera) =>
        new(camera.ToScreen(Vector2.Zero), camera.ToScreen(new Vector2(BreakoutBoard.FieldWidth, BreakoutBoard.FieldHeight)));

    public static void DrawField(ImDrawListPtr drawList, in Camera2D camera, Vector4 accent, float scale)
    {
        var field = FieldRect(in camera);
        var radius = camera.Px(FieldRadius);
        Squircle.Fill(drawList, field.Min, field.Max, radius, ImGui.GetColorU32(FieldFill));
        Squircle.Stroke(drawList, field.Min, field.Max, radius, ImGui.GetColorU32(FieldRim), MathF.Max(1f, 1.5f * scale));
        var glowTop = ImGui.GetColorU32(accent with { W = 0.16f });
        var glowBottom = ImGui.GetColorU32(accent with { W = 0f });
        drawList.AddRectFilledMultiColor(field.Min, new Vector2(field.Max.X, field.Min.Y + camera.Px(0.08f)), glowTop,
            glowTop, glowBottom, glowBottom);
    }

    public static void DrawArena(ImDrawListPtr drawList, BreakoutBoard board, in Camera2D camera, Vector4 accent,
        float scale)
    {
        DrawBricks(drawList, board, in camera, scale);
        DrawPowerUps(drawList, board, in camera, scale);
        DrawPaddle(drawList, board, in camera, accent, scale);
    }

    private static void DrawBricks(ImDrawListPtr drawList, BreakoutBoard board, in Camera2D camera, float scale)
    {
        var halfWidth = camera.Px(BreakoutBoard.BrickWidth * 0.5f);
        var halfHeight = camera.Px(BreakoutBoard.BrickHeight * 0.5f);
        var rounding = halfHeight * 0.5f;
        for (var row = 0; row < board.Rows; row++)
        {
            for (var column = 0; column < BreakoutBoard.Columns; column++)
            {
                if (!board.BrickAlive(column, row))
                {
                    continue;
                }

                var center = camera.ToScreen(BreakoutBoard.BrickCenter(column, row));
                var min = new Vector2(center.X - halfWidth, center.Y - halfHeight);
                var max = new Vector2(center.X + halfWidth, center.Y + halfHeight);
                var kind = board.BrickKindAt(column, row);
                var color = ColorFor(kind, board.BrickColor(column, row));
                Squircle.FillVerticalGradient(drawList, min, max, rounding,
                    ImGui.GetColorU32(GamePalette.Lighten(color, 0.18f)),
                    ImGui.GetColorU32(GamePalette.Darken(color, 0.22f)));
                Material.Sheen(drawList, min, max, rounding, ImGui.GetColorU32(White with { W = 0.35f }), 1f * scale,
                    1f * scale);
                Squircle.Stroke(drawList, min, max, rounding,
                    ImGui.GetColorU32(GamePalette.Darken(color, 0.4f) with { W = 0.5f }), 1f * scale);
                switch (kind)
                {
                    case BrickKind.Armoured:
                        DrawArmour(drawList, center, halfWidth, halfHeight, board.BrickHits(column, row) > 0, scale);
                        break;
                    case BrickKind.Explosive:
                        DrawExplosiveCore(drawList, center, halfHeight);
                        break;
                }
            }
        }
    }

    private static void DrawArmour(ImDrawListPtr drawList, Vector2 center, float halfWidth, float halfHeight,
        bool dented, float scale)
    {
        var rivet = ImGui.GetColorU32(Ink with { W = 0.55f });
        var rivetRadius = halfHeight * ArmourRivet;
        var inset = halfWidth * 0.18f;
        drawList.AddCircleFilled(new Vector2(center.X - halfWidth + inset, center.Y), rivetRadius, rivet, 10);
        drawList.AddCircleFilled(new Vector2(center.X + halfWidth - inset, center.Y), rivetRadius, rivet, 10);
        if (!dented)
        {
            return;
        }

        var crack = ImGui.GetColorU32(Crack);
        var thickness = MathF.Max(1f, 1.2f * scale);
        var first = center + new Vector2(-halfWidth * 0.35f, -halfHeight * 0.8f);
        var second = center + new Vector2(-halfWidth * 0.05f, halfHeight * 0.1f);
        var third = center + new Vector2(halfWidth * 0.3f, halfHeight * 0.85f);
        drawList.AddLine(first, second, crack, thickness);
        drawList.AddLine(second, third, crack, thickness);
        drawList.AddLine(second, second + new Vector2(halfWidth * 0.25f, -halfHeight * 0.4f), crack, thickness);
    }

    private static void DrawExplosiveCore(ImDrawListPtr drawList, Vector2 center, float halfHeight)
    {
        var pulse = 0.6f + 0.4f * Pulse.Wave(Pulse.Fast);
        var radius = halfHeight * (0.45f + 0.15f * pulse);
        ProgressRing.Glow(center, radius * 2.2f, Core, 0.5f * pulse);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Core with { W = 0.75f + 0.25f * pulse }), 14);
        drawList.AddCircleFilled(center, radius * 0.45f, ImGui.GetColorU32(White), 10);
    }

    private static void DrawPowerUps(ImDrawListPtr drawList, BreakoutBoard board, in Camera2D camera, float scale)
    {
        var radius = camera.Px(BreakoutBoard.PowerUpRadius);
        for (var index = 0; index < board.PowerUpCount; index++)
        {
            var power = board.GetPowerUp(index);
            var color = power.Kind == PowerUpKind.MultiBall ? MultiBallColor : WideColor;
            var bob = MathF.Sin((float)ImGui.GetTime() * 6f + index) * radius * PowerUpBob;
            var center = camera.ToScreen(power.Position) + new Vector2(0f, bob);
            ProgressRing.Glow(center, radius, color, 0.7f);
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(color), 20);
            drawList.AddCircleFilled(center - new Vector2(radius * 0.3f, radius * 0.3f), radius * 0.3f,
                ImGui.GetColorU32(White with { W = 0.5f }), 12);
            if (power.Kind == PowerUpKind.MultiBall)
            {
                DrawMultiBallMark(drawList, center, radius);
            }
            else
            {
                DrawWideMark(drawList, center, radius, scale);
            }
        }
    }

    private static void DrawMultiBallMark(ImDrawListPtr drawList, Vector2 center, float radius)
    {
        var ink = ImGui.GetColorU32(Ink);
        var dot = radius * 0.18f;
        var spread = radius * 0.38f;
        drawList.AddCircleFilled(center + new Vector2(0f, -spread * 0.6f), dot, ink, 10);
        drawList.AddCircleFilled(center + new Vector2(-spread * 0.6f, spread * 0.45f), dot, ink, 10);
        drawList.AddCircleFilled(center + new Vector2(spread * 0.6f, spread * 0.45f), dot, ink, 10);
    }

    private static void DrawWideMark(ImDrawListPtr drawList, Vector2 center, float radius, float scale)
    {
        var ink = ImGui.GetColorU32(Ink);
        var half = radius * 0.55f;
        var thickness = MathF.Max(1.5f, radius * 0.22f);
        drawList.AddLine(center - new Vector2(half, 0f), center + new Vector2(half, 0f), ink, thickness);
        var tip = radius * 0.22f;
        drawList.AddTriangleFilled(center - new Vector2(half + tip, 0f), center - new Vector2(half - tip * 0.2f, -tip),
            center - new Vector2(half - tip * 0.2f, tip), ink);
        drawList.AddTriangleFilled(center + new Vector2(half + tip, 0f), center + new Vector2(half - tip * 0.2f, -tip),
            center + new Vector2(half - tip * 0.2f, tip), ink);
    }

    private static void DrawPaddle(ImDrawListPtr drawList, BreakoutBoard board, in Camera2D camera, Vector4 accent,
        float scale)
    {
        var paddleCenter = camera.ToScreen(new Vector2(board.PaddleX, BreakoutBoard.PaddleY));
        var paddleHalf = new Vector2(camera.Px(board.PaddleHalfWidth), camera.Px(BreakoutBoard.PaddleHeight * 0.5f));
        var paddleMin = paddleCenter - paddleHalf;
        var paddleMax = paddleCenter + paddleHalf;
        Elevation.Card(drawList, paddleMin, paddleMax, paddleHalf.Y, scale, 0.7f);
        Squircle.FillVerticalGradient(drawList, paddleMin, paddleMax, paddleHalf.Y,
            ImGui.GetColorU32(GamePalette.Lighten(accent, 0.22f)), ImGui.GetColorU32(GamePalette.Darken(accent, 0.18f)));
        Material.Sheen(drawList, paddleMin, paddleMax, paddleHalf.Y, ImGui.GetColorU32(White with { W = 0.4f }),
            1f * scale, 1f * scale);
        ProgressRing.Glow(paddleCenter, paddleHalf.X * 0.5f, accent, 0.25f);
    }

    public static void DrawBalls(ImDrawListPtr drawList, BreakoutBoard board, in Camera2D camera, Vector4 accent)
    {
        var radius = camera.Px(BreakoutBoard.BallRadius);
        var glow = GamePalette.Lighten(accent, 0.4f);
        for (var index = 0; index < board.BallCount; index++)
        {
            var center = camera.ToScreen(board.GetBall(index).Position);
            ProgressRing.Glow(center, radius, glow, 0.7f);
            drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(BallColor), 20);
            drawList.AddCircleFilled(center - new Vector2(radius * 0.3f, radius * 0.3f), radius * 0.32f,
                ImGui.GetColorU32(White with { W = 0.85f }), 12);
        }
    }
}
