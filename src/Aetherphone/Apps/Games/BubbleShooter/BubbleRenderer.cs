using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.BubbleShooter;

internal sealed class BubbleRenderer
{
    private const int TraceCapacity = 96;
    private const float FieldRadius = 0.03f;
    private const float GlyphAlpha = 0.30f;
    private const float GuideDotRadius = 1.5f;
    private const float NextPadX = 10f;
    private const float NextGap = 6f;
    private const float NextBubbleRadius = 8f;
    private static readonly TextStyle NextStyle = TextStyles.FootnoteEmphasized;

    private static readonly Vector4[] BubbleColors =
    {
        new(0.95f, 0.45f, 0.50f, 1f), new(0.40f, 0.70f, 0.98f, 1f), new(0.46f, 0.86f, 0.62f, 1f),
        new(0.96f, 0.86f, 0.36f, 1f), new(0.72f, 0.50f, 0.96f, 1f), new(0.99f, 0.60f, 0.24f, 1f),
    };

    private static readonly Vector4 FieldFill = new(0.03f, 0.02f, 0.02f, 0.42f);
    private static readonly Vector4 FieldRim = new(1f, 0.95f, 0.90f, 0.12f);
    private static readonly Vector4 DangerColor = new(0.97f, 0.32f, 0.34f, 1f);
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.12f);
    private static readonly Vector4 ChargeColor = new(1f, 0.92f, 0.55f, 0.85f);
    private static readonly Vector4 BombBody = new(0.14f, 0.14f, 0.18f, 1f);
    private static readonly Vector4 BombRing = new(1f, 0.60f, 0.26f, 1f);
    private static readonly Vector4 BombCore = new(1f, 0.86f, 0.52f, 1f);
    private static readonly Vector4 BombGlow = new(1f, 0.62f, 0.28f, 1f);
    private static readonly Vector4 RailFill = new(0f, 0f, 0f, 0.32f);
    private static readonly Vector4 RailLine = new(1f, 1f, 1f, 0.10f);
    private static readonly Vector4 RowFill = new(0.98f, 0.72f, 0.34f, 0.90f);
    private static readonly Vector4 RowImminent = new(0.97f, 0.34f, 0.34f, 1f);
    private static readonly Vector4 RowStroke = new(1f, 0.55f, 0.50f, 1f);
    private readonly Vector2[] tracePoints = new Vector2[TraceCapacity];

    public static Vector4 ColorOf(int color) => BubbleColors[color % BubbleColors.Length];

    public static Rect FieldRect(in Camera2D camera) =>
        new(camera.ToScreen(Vector2.Zero), camera.ToScreen(new Vector2(BubbleBoard.FieldWidth, BubbleBoard.FieldHeight)));

    public void Draw(ImDrawListPtr drawList, BubbleBoard board, in Camera2D camera, float scale, Vector2 aimDirection,
        PhoneTheme theme)
    {
        var field = FieldRect(in camera);
        var radius = camera.Px(BubbleBoard.Radius);
        var descent = new Vector2(0f, camera.Px(board.DescentOffset));
        DrawField(drawList, field, camera.Px(FieldRadius), scale);
        drawList.PushClipRect(field.Min, field.Max, true);
        DrawDangerLine(drawList, board, in camera, field, scale);
        DrawPack(drawList, board, in camera, radius, descent);
        DrawFallers(drawList, board, in camera, radius);
        if (!board.Flying && !board.GameOver && aimDirection != Vector2.Zero)
        {
            DrawGuide(drawList, board, in camera, radius, aimDirection, scale);
        }

        if (board.Flying)
        {
            DrawBubble(drawList, camera.ToScreen(board.FlyPosition), radius, board.FlyColor, board.FlyKind);
        }

        DrawLauncher(drawList, board, in camera, radius, scale);
        DrawRowMeter(drawList, board, in camera, field, scale, theme);
        drawList.PopClipRect();
    }

    public static float NextCapsuleWidth(float scale)
    {
        var text = Typography.Measure(Loc.T(L.Games.Next), NextStyle).X / scale;
        return NextPadX * 2f + text + NextGap + NextBubbleRadius * 2f;
    }

    public static void DrawNext(ImDrawListPtr drawList, Rect rect, BubbleBoard board, float scale, PhoneTheme theme)
    {
        StageHud.Capsule(drawList, rect, scale);
        var text = Loc.T(L.Games.Next);
        var left = rect.Min.X + NextPadX * scale;
        Typography.Draw(drawList, new Vector2(left, rect.Center.Y - Typography.LineHeight(NextStyle) * 0.5f), text,
            StageInks.Muted, NextStyle);
        var bubbleCenter = new Vector2(left + Typography.Measure(text, NextStyle).X + (NextGap + NextBubbleRadius) * scale,
            rect.Center.Y);
        DrawBubble(drawList, bubbleCenter, NextBubbleRadius * scale, board.NextColor, board.NextKind);
    }

    private static void DrawField(ImDrawListPtr drawList, Rect field, float radius, float scale)
    {
        Squircle.Fill(drawList, field.Min, field.Max, radius, ImGui.GetColorU32(FieldFill));
        Squircle.Stroke(drawList, field.Min, field.Max, radius, ImGui.GetColorU32(FieldRim), MathF.Max(1f, 1.5f * scale));
    }

    private static void DrawPack(ImDrawListPtr drawList, BubbleBoard board, in Camera2D camera, float radius,
        Vector2 descent)
    {
        for (var row = 0; row < board.RowLimit; row++)
        {
            for (var column = 0; column < BubbleBoard.Columns; column++)
            {
                var color = board.ColorAt(column, row);
                if (color < 0)
                {
                    continue;
                }

                var center = camera.ToScreen(board.CellCenter(column, row)) - descent;
                DrawBubble(drawList, center, radius, color, BubbleKind.Normal);
            }
        }
    }

    private static void DrawFallers(ImDrawListPtr drawList, BubbleBoard board, in Camera2D camera, float radius)
    {
        for (var index = 0; index < board.FallerCount; index++)
        {
            ref readonly var faller = ref board.Faller(index);
            var center = camera.ToScreen(faller.Position);
            var fade = MathF.Max(0f, 1f - (faller.Position.Y - BubbleBoard.FieldHeight * 0.75f) * 1.4f);
            DrawBubble(drawList, center, radius, faller.Color, BubbleKind.Normal, MathF.Min(1f, fade), faller.Rotation);
        }
    }

    private static void DrawDangerLine(ImDrawListPtr drawList, BubbleBoard board, in Camera2D camera, Rect field,
        float scale)
    {
        var dangerY = camera.ToScreen(new Vector2(0f, board.DangerY)).Y;
        var pulse = 0.5f + 0.5f * MathF.Sin((float)ImGui.GetTime() * (3f + board.DangerLevel * 5f));
        var intensity = 0.22f + board.DangerLevel * (0.35f + pulse * 0.35f);
        drawList.AddLine(new Vector2(field.Min.X, dangerY), new Vector2(field.Max.X, dangerY),
            ImGui.GetColorU32(DangerColor with { W = intensity }), (1.4f + board.DangerLevel * 1.6f) * scale);
        if (board.DangerLevel <= 0.01f)
        {
            return;
        }

        var glowTop = ImGui.GetColorU32(DangerColor with { W = 0f });
        var glowBottom = ImGui.GetColorU32(DangerColor with { W = 0.16f * board.DangerLevel * (0.6f + pulse * 0.4f) });
        drawList.AddRectFilledMultiColor(new Vector2(field.Min.X, dangerY - 42f * scale),
            new Vector2(field.Max.X, dangerY), glowTop, glowTop, glowBottom, glowBottom);
    }

    private void DrawGuide(ImDrawListPtr drawList, BubbleBoard board, in Camera2D camera, float radius,
        Vector2 direction, float scale)
    {
        var count = board.TracePath(direction, tracePoints, out var landingCell);
        if (count == 0)
        {
            return;
        }

        var tint = board.CurrentKind == BubbleKind.Normal ? ColorOf(board.CurrentColor) : White;
        for (var index = 0; index < count; index++)
        {
            var fade = 1f - index / (float)count * 0.55f;
            drawList.AddCircleFilled(camera.ToScreen(tracePoints[index]), GuideDotRadius * scale,
                ImGui.GetColorU32(tint with { W = 0.42f * fade }), 8);
        }

        if (landingCell < 0)
        {
            return;
        }

        var column = landingCell % BubbleBoard.Columns;
        var row = landingCell / BubbleBoard.Columns;
        var ghost = camera.ToScreen(board.CellCenter(column, row));
        drawList.AddCircle(ghost, radius * 0.92f, ImGui.GetColorU32(tint with { W = 0.55f }), 0, 1.6f * scale);
        drawList.AddCircleFilled(ghost, radius * 0.78f, ImGui.GetColorU32(tint with { W = 0.14f }));
    }

    private static void DrawLauncher(ImDrawListPtr drawList, BubbleBoard board, in Camera2D camera, float radius,
        float scale)
    {
        var launcher = camera.ToScreen(BubbleBoard.LauncherPosition);
        var launcherRadius = radius * 1.1f;
        ProgressRing.Glow(launcher, launcherRadius * 1.2f, LauncherGlow(board), 0.5f);
        DrawBubble(drawList, launcher, launcherRadius, board.CurrentColor, board.CurrentKind);
        DrawChargeRing(drawList, board, launcher, launcherRadius, scale);
        var nextCenter = camera.ToScreen(new Vector2(BubbleBoard.Radius * 1.5f, BubbleBoard.LauncherPosition.Y));
        DrawBubble(drawList, nextCenter, radius * 0.72f, board.NextColor, board.NextKind);
        if (board.NextKind == BubbleKind.Normal)
        {
            return;
        }

        var pulse = 0.5f + 0.5f * MathF.Sin((float)ImGui.GetTime() * 6f);
        drawList.AddCircle(nextCenter, radius * (0.92f + pulse * 0.12f),
            ImGui.GetColorU32(White with { W = 0.30f + pulse * 0.35f }), 0, 1.6f * scale);
    }

    private static Vector4 LauncherGlow(BubbleBoard board)
    {
        return board.CurrentKind switch
        {
            BubbleKind.Bomb => BombGlow,
            BubbleKind.Rainbow => White,
            _ => ColorOf(board.CurrentColor),
        };
    }

    private static void DrawChargeRing(ImDrawListPtr drawList, BubbleBoard board, Vector2 center, float radius,
        float scale)
    {
        if (board.Charge <= 0.01f)
        {
            return;
        }

        var ringRadius = radius + 4f * scale;
        var start = -MathF.PI * 0.5f;
        drawList.PathArcTo(center, ringRadius, start, start + MathF.Tau * board.Charge, 40);
        drawList.PathStroke(ImGui.GetColorU32(ChargeColor), ImDrawFlags.None, 2.4f * scale);
    }

    private static void DrawRowMeter(ImDrawListPtr drawList, BubbleBoard board, in Camera2D camera, Rect field,
        float scale, PhoneTheme theme)
    {
        var railBottom = camera.ToScreen(new Vector2(0f, BubbleBoard.CeilingY)).Y;
        drawList.AddRectFilled(field.Min, new Vector2(field.Max.X, railBottom), ImGui.GetColorU32(RailFill));
        drawList.AddLine(new Vector2(field.Min.X, railBottom), new Vector2(field.Max.X, railBottom),
            ImGui.GetColorU32(RailLine), 1f * scale);
        var inset = 8f * scale;
        var height = MathF.Max(3f * scale, (railBottom - field.Min.Y) * 0.42f);
        var center = (field.Min.Y + railBottom) * 0.5f;
        var trackMin = new Vector2(field.Min.X + inset, center - height * 0.5f);
        var trackMax = new Vector2(field.Max.X - inset, center + height * 0.5f);
        var used = board.RowInterval - board.ShotsUntilRow;
        var progress = board.RowInterval <= 0 ? 0f : Math.Clamp(used / (float)board.RowInterval, 0f, 1f);
        Squircle.Fill(drawList, trackMin, trackMax, height * 0.5f, ImGui.GetColorU32(StageInks.Muted with { W = 0.22f }));
        if (progress <= 0f)
        {
            return;
        }

        var imminent = board.ShotsUntilRow <= 1;
        var pulse = 0.5f + 0.5f * MathF.Sin((float)ImGui.GetTime() * 8f);
        var fillColor = imminent ? RowImminent with { W = 0.70f + pulse * 0.30f } : RowFill;
        var fillMax = new Vector2(trackMin.X + (trackMax.X - trackMin.X) * progress, trackMax.Y);
        Squircle.Fill(drawList, trackMin, fillMax, height * 0.5f, ImGui.GetColorU32(fillColor));
        if (!imminent)
        {
            return;
        }

        Squircle.Stroke(drawList, trackMin, trackMax, height * 0.5f,
            ImGui.GetColorU32(RowStroke with { W = 0.35f + pulse * 0.45f }), 1.2f * scale);
    }

    private static void DrawBubble(ImDrawListPtr drawList, Vector2 center, float radius, int color, BubbleKind kind,
        float alpha = 1f, float rotation = 0f)
    {
        if (alpha <= 0.01f)
        {
            return;
        }

        switch (kind)
        {
            case BubbleKind.Bomb:
                DrawBomb(drawList, center, radius, alpha);
                return;
            case BubbleKind.Rainbow:
                DrawRainbow(drawList, center, radius, alpha);
                return;
        }

        var fill = ColorOf(color);
        var cos = MathF.Cos(rotation);
        var sin = MathF.Sin(rotation);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(fill with { W = alpha }));
        drawList.AddCircleFilled(Spin(center, -radius * 0.3f, -radius * 0.3f, cos, sin), radius * 0.32f,
            ImGui.GetColorU32(White with { W = 0.4f * alpha }));
        drawList.AddCircle(center, radius, ImGui.GetColorU32(Shadow with { W = Shadow.W * alpha }), 0, 1.2f);
        DrawGlyph(drawList, center, radius, color, alpha, cos, sin);
    }

    private static void DrawGlyph(ImDrawListPtr drawList, Vector2 center, float radius, int color, float alpha,
        float cos, float sin)
    {
        var glyphColor = ImGui.GetColorU32(GamePalette.InkOn(ColorOf(color)) with { W = GlyphAlpha * alpha });
        var size = radius * 0.34f;
        switch (color % BubbleColors.Length)
        {
            case 0:
                drawList.AddCircleFilled(center, size * 0.82f, glyphColor);
                return;
            case 1:
                drawList.AddTriangleFilled(Spin(center, 0f, -size, cos, sin),
                    Spin(center, size * 0.92f, size * 0.72f, cos, sin),
                    Spin(center, -size * 0.92f, size * 0.72f, cos, sin), glyphColor);
                return;
            case 2:
            {
                var half = size * 0.82f;
                drawList.AddQuadFilled(Spin(center, -half, -half, cos, sin), Spin(center, half, -half, cos, sin),
                    Spin(center, half, half, cos, sin), Spin(center, -half, half, cos, sin), glyphColor);
                return;
            }
            case 3:
                drawList.AddQuadFilled(Spin(center, 0f, -size, cos, sin), Spin(center, size, 0f, cos, sin),
                    Spin(center, 0f, size, cos, sin), Spin(center, -size, 0f, cos, sin), glyphColor);
                return;
            case 4:
            {
                var thickness = MathF.Max(1f, size * 0.68f);
                drawList.AddLine(Spin(center, -size, 0f, cos, sin), Spin(center, size, 0f, cos, sin), glyphColor,
                    thickness);
                drawList.AddLine(Spin(center, 0f, -size, cos, sin), Spin(center, 0f, size, cos, sin), glyphColor,
                    thickness);
                return;
            }
            default:
                DrawHexagon(drawList, center, size, glyphColor, cos, sin);
                return;
        }
    }

    private static void DrawHexagon(ImDrawListPtr drawList, Vector2 center, float size, uint color, float cos, float sin)
    {
        for (var corner = 0; corner < 6; corner++)
        {
            var angle = corner * MathF.Tau / 6f - MathF.PI * 0.5f;
            drawList.PathLineTo(Spin(center, MathF.Cos(angle) * size, MathF.Sin(angle) * size, cos, sin));
        }

        drawList.PathFillConvex(color);
    }

    private static Vector2 Spin(Vector2 center, float x, float y, float cos, float sin) =>
        new(center.X + x * cos - y * sin, center.Y + x * sin + y * cos);

    private static void DrawBomb(ImDrawListPtr drawList, Vector2 center, float radius, float alpha)
    {
        var pulse = 0.5f + 0.5f * MathF.Sin((float)ImGui.GetTime() * 7f);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(BombBody with { W = alpha }));
        drawList.AddCircleFilled(center - new Vector2(radius * 0.3f, radius * 0.3f), radius * 0.28f,
            ImGui.GetColorU32(White with { W = 0.22f * alpha }));
        drawList.AddCircle(center, radius * 0.62f,
            ImGui.GetColorU32(BombRing with { Y = 0.60f + pulse * 0.25f, W = (0.7f + pulse * 0.3f) * alpha }), 0,
            MathF.Max(1.4f, radius * 0.14f));
        drawList.AddCircleFilled(center, radius * 0.20f,
            ImGui.GetColorU32(BombCore with { W = (0.75f + pulse * 0.25f) * alpha }));
    }

    private static void DrawRainbow(ImDrawListPtr drawList, Vector2 center, float radius, float alpha)
    {
        var rotation = (float)ImGui.GetTime() * 0.9f;
        var slice = MathF.Tau / BubbleColors.Length;
        for (var index = 0; index < BubbleColors.Length; index++)
        {
            var start = rotation + index * slice;
            drawList.PathLineTo(center);
            drawList.PathArcTo(center, radius, start, start + slice, 10);
            drawList.PathFillConvex(ImGui.GetColorU32(BubbleColors[index] with { W = alpha }));
        }

        drawList.AddCircleFilled(center - new Vector2(radius * 0.3f, radius * 0.3f), radius * 0.30f,
            ImGui.GetColorU32(White with { W = 0.45f * alpha }));
        drawList.AddCircleFilled(center, radius * 0.26f, ImGui.GetColorU32(White with { W = 0.90f * alpha }));
        drawList.AddCircle(center, radius, ImGui.GetColorU32(White with { W = 0.35f * alpha }), 0, 1.4f);
    }
}
