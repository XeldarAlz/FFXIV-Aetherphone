using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.BubbleShooter;

internal sealed class BubbleRenderer
{
    public const float ArenaRounding = 14f;
    private const int TraceCapacity = 96;
    private const float GlyphAlpha = 0.30f;
    private const float GuideDotSpacing = 3f;

    private static readonly Vector4[] BubbleColors =
    {
        new(0.95f, 0.45f, 0.50f, 1f), new(0.40f, 0.70f, 0.98f, 1f), new(0.46f, 0.86f, 0.62f, 1f),
        new(0.96f, 0.86f, 0.36f, 1f), new(0.72f, 0.50f, 0.96f, 1f), new(0.99f, 0.60f, 0.24f, 1f),
    };

    private readonly Vector2[] tracePoints = new Vector2[TraceCapacity];

    public static Vector4 ColorOf(int color) => BubbleColors[color % BubbleColors.Length];

    public void Draw(BubbleBoard board, Rect field, float scale, Vector2 aimDirection, PhoneTheme theme)
    {
        var drawList = ImGui.GetWindowDrawList();
        var factor = field.Width;
        var radius = board.Radius * factor;
        var descent = new Vector2(0f, board.DescentOffset * factor);
        drawList.PushClipRect(field.Min, field.Max, true);
        DrawDangerLine(drawList, board, field, factor, scale);
        DrawPack(drawList, board, field, factor, radius, descent);
        DrawFallers(drawList, board, field, factor, radius);
        if (!board.Flying && !board.GameOver && aimDirection != Vector2.Zero)
        {
            DrawGuide(drawList, board, field, factor, radius, aimDirection, scale);
        }

        if (board.Flying)
        {
            DrawBubble(drawList, field.Min + board.FlyPosition * factor, radius, board.FlyColor, board.FlyKind);
        }

        DrawLauncher(drawList, board, field, factor, radius, scale);
        drawList.PopClipRect();
        DrawRowMeter(drawList, board, field, factor, scale, theme);
    }

    private void DrawPack(ImDrawListPtr drawList, BubbleBoard board, Rect field, float factor, float radius,
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

                var center = field.Min + board.CellCenter(column, row) * factor - descent;
                DrawBubble(drawList, center, radius, color, BubbleKind.Normal);
            }
        }
    }

    private void DrawFallers(ImDrawListPtr drawList, BubbleBoard board, Rect field, float factor, float radius)
    {
        for (var index = 0; index < board.FallerCount; index++)
        {
            ref readonly var faller = ref board.Faller(index);
            var center = field.Min + faller.Position * factor;
            var fade = MathF.Max(0f, 1f - (faller.Position.Y - board.FieldHeight * 0.75f) * 1.4f);
            DrawBubble(drawList, center, radius, faller.Color, BubbleKind.Normal, MathF.Min(1f, fade));
        }
    }

    private void DrawDangerLine(ImDrawListPtr drawList, BubbleBoard board, Rect field, float factor, float scale)
    {
        var dangerY = field.Min.Y + board.DangerY * factor;
        var pulse = 0.5f + 0.5f * MathF.Sin((float)ImGui.GetTime() * (3f + board.DangerLevel * 5f));
        var intensity = 0.22f + board.DangerLevel * (0.35f + pulse * 0.35f);
        var lineColor = ImGui.GetColorU32(new Vector4(0.97f, 0.32f, 0.34f, intensity));
        drawList.AddLine(new Vector2(field.Min.X, dangerY), new Vector2(field.Max.X, dangerY), lineColor,
            (1.4f + board.DangerLevel * 1.6f) * scale);
        if (board.DangerLevel <= 0.01f)
        {
            return;
        }

        var glowTop = ImGui.GetColorU32(new Vector4(0.97f, 0.28f, 0.30f, 0f));
        var glowBottom = ImGui.GetColorU32(new Vector4(0.97f, 0.28f, 0.30f, 0.16f * board.DangerLevel * (0.6f + pulse * 0.4f)));
        drawList.AddRectFilledMultiColor(new Vector2(field.Min.X, dangerY - 42f * scale),
            new Vector2(field.Max.X, dangerY), glowTop, glowTop, glowBottom, glowBottom);
    }

    private void DrawGuide(ImDrawListPtr drawList, BubbleBoard board, Rect field, float factor, float radius,
        Vector2 direction, float scale)
    {
        var count = board.TracePath(direction, tracePoints, out var landingCell);
        if (count == 0)
        {
            return;
        }

        var tint = board.CurrentKind == BubbleKind.Normal ? ColorOf(board.CurrentColor) : new Vector4(1f, 1f, 1f, 1f);
        for (var index = 0; index < count; index++)
        {
            var fade = 1f - index / (float)count * 0.55f;
            drawList.AddCircleFilled(field.Min + tracePoints[index] * factor, GuideDotSpacing * 0.5f * scale,
                ImGui.GetColorU32(tint with { W = 0.42f * fade }));
        }

        if (landingCell < 0)
        {
            return;
        }

        var column = landingCell % BubbleBoard.Columns;
        var row = landingCell / BubbleBoard.Columns;
        var ghost = field.Min + board.CellCenter(column, row) * factor;
        drawList.AddCircle(ghost, radius * 0.92f, ImGui.GetColorU32(tint with { W = 0.55f }), 0, 1.6f * scale);
        drawList.AddCircleFilled(ghost, radius * 0.78f, ImGui.GetColorU32(tint with { W = 0.14f }));
    }

    private void DrawLauncher(ImDrawListPtr drawList, BubbleBoard board, Rect field, float factor, float radius,
        float scale)
    {
        var launcher = field.Min + board.LauncherPosition * factor;
        var launcherRadius = radius * 1.1f;
        ProgressRing.Glow(launcher, launcherRadius * 1.2f, LauncherGlow(board), 0.5f);
        DrawBubble(drawList, launcher, launcherRadius, board.CurrentColor, board.CurrentKind);
        DrawChargeRing(drawList, board, launcher, launcherRadius, scale);
        var nextCenter = new Vector2(field.Min.X + radius * 1.5f, launcher.Y);
        DrawBubble(drawList, nextCenter, radius * 0.72f, board.NextColor, board.NextKind);
        if (board.NextKind == BubbleKind.Normal)
        {
            return;
        }

        var pulse = 0.5f + 0.5f * MathF.Sin((float)ImGui.GetTime() * 6f);
        drawList.AddCircle(nextCenter, radius * (0.92f + pulse * 0.12f),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.30f + pulse * 0.35f)), 0, 1.6f * scale);
    }

    private static Vector4 LauncherGlow(BubbleBoard board)
    {
        return board.CurrentKind switch
        {
            BubbleKind.Bomb => new Vector4(1f, 0.62f, 0.28f, 1f),
            BubbleKind.Rainbow => new Vector4(1f, 1f, 1f, 1f),
            _ => ColorOf(board.CurrentColor),
        };
    }

    private void DrawChargeRing(ImDrawListPtr drawList, BubbleBoard board, Vector2 center, float radius, float scale)
    {
        if (board.Charge <= 0.01f)
        {
            return;
        }

        var ringRadius = radius + 4f * scale;
        var start = -MathF.PI * 0.5f;
        drawList.PathArcTo(center, ringRadius, start, start + MathF.Tau * board.Charge, 40);
        drawList.PathStroke(ImGui.GetColorU32(new Vector4(1f, 0.92f, 0.55f, 0.85f)), ImDrawFlags.None, 2.4f * scale);
    }

    private void DrawRowMeter(ImDrawListPtr drawList, BubbleBoard board, Rect field, float factor, float scale,
        PhoneTheme theme)
    {
        var railBottom = field.Min.Y + board.CeilingY * factor;
        drawList.AddRectFilled(field.Min, new Vector2(field.Max.X, railBottom),
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.32f)), ArenaRounding * scale, ImDrawFlags.RoundCornersTop);
        drawList.AddLine(new Vector2(field.Min.X, railBottom), new Vector2(field.Max.X, railBottom),
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.10f)), 1f * scale);
        var inset = 8f * scale;
        var height = MathF.Max(3f * scale, (railBottom - field.Min.Y) * 0.42f);
        var center = (field.Min.Y + railBottom) * 0.5f;
        var trackMin = new Vector2(field.Min.X + inset, center - height * 0.5f);
        var trackMax = new Vector2(field.Max.X - inset, center + height * 0.5f);
        var used = board.RowInterval - board.ShotsUntilRow;
        var progress = board.RowInterval <= 0 ? 0f : Math.Clamp(used / (float)board.RowInterval, 0f, 1f);
        Squircle.Fill(drawList, trackMin, trackMax, height * 0.5f,
            ImGui.GetColorU32(theme.TextMuted with { W = 0.22f }));
        if (progress <= 0f)
        {
            return;
        }

        var imminent = board.ShotsUntilRow <= 1;
        var pulse = 0.5f + 0.5f * MathF.Sin((float)ImGui.GetTime() * 8f);
        var fillColor = imminent
            ? new Vector4(0.97f, 0.34f, 0.34f, 0.70f + pulse * 0.30f)
            : new Vector4(0.98f, 0.72f, 0.34f, 0.90f);
        var fillMax = new Vector2(trackMin.X + (trackMax.X - trackMin.X) * progress, trackMax.Y);
        Squircle.Fill(drawList, trackMin, fillMax, height * 0.5f, ImGui.GetColorU32(fillColor));
        if (!imminent)
        {
            return;
        }

        Squircle.Stroke(drawList, trackMin, trackMax, height * 0.5f,
            ImGui.GetColorU32(new Vector4(1f, 0.55f, 0.50f, 0.35f + pulse * 0.45f)), 1.2f * scale);
    }

    private void DrawBubble(ImDrawListPtr drawList, Vector2 center, float radius, int color, BubbleKind kind,
        float alpha = 1f)
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
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(fill with { W = alpha }));
        drawList.AddCircleFilled(center - new Vector2(radius * 0.3f, radius * 0.3f), radius * 0.32f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.4f * alpha)));
        drawList.AddCircle(center, radius, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.12f * alpha)), 0, 1.2f);
        DrawGlyph(drawList, center, radius, color, alpha);
    }

    private void DrawGlyph(ImDrawListPtr drawList, Vector2 center, float radius, int color, float alpha)
    {
        var glyphColor = ImGui.GetColorU32(GamePalette.InkOn(ColorOf(color)) with { W = GlyphAlpha * alpha });
        var size = radius * 0.34f;
        switch (color % BubbleColors.Length)
        {
            case 0:
                drawList.AddCircleFilled(center, size * 0.82f, glyphColor);
                return;
            case 1:
                drawList.AddTriangleFilled(new Vector2(center.X, center.Y - size),
                    new Vector2(center.X + size * 0.92f, center.Y + size * 0.72f),
                    new Vector2(center.X - size * 0.92f, center.Y + size * 0.72f), glyphColor);
                return;
            case 2:
                drawList.AddRectFilled(center - new Vector2(size * 0.82f, size * 0.82f),
                    center + new Vector2(size * 0.82f, size * 0.82f), glyphColor, size * 0.2f);
                return;
            case 3:
                drawList.AddQuadFilled(new Vector2(center.X, center.Y - size), new Vector2(center.X + size, center.Y),
                    new Vector2(center.X, center.Y + size), new Vector2(center.X - size, center.Y), glyphColor);
                return;
            case 4:
                drawList.AddRectFilled(new Vector2(center.X - size, center.Y - size * 0.34f),
                    new Vector2(center.X + size, center.Y + size * 0.34f), glyphColor, size * 0.16f);
                drawList.AddRectFilled(new Vector2(center.X - size * 0.34f, center.Y - size),
                    new Vector2(center.X + size * 0.34f, center.Y + size), glyphColor, size * 0.16f);
                return;
            default:
                DrawHexagon(drawList, center, size, glyphColor);
                return;
        }
    }

    private void DrawHexagon(ImDrawListPtr drawList, Vector2 center, float size, uint color)
    {
        for (var corner = 0; corner < 6; corner++)
        {
            var angle = corner * MathF.Tau / 6f - MathF.PI * 0.5f;
            drawList.PathLineTo(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * size);
        }

        drawList.PathFillConvex(color);
    }

    private void DrawBomb(ImDrawListPtr drawList, Vector2 center, float radius, float alpha)
    {
        var pulse = 0.5f + 0.5f * MathF.Sin((float)ImGui.GetTime() * 7f);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(new Vector4(0.14f, 0.14f, 0.18f, alpha)));
        drawList.AddCircleFilled(center - new Vector2(radius * 0.3f, radius * 0.3f), radius * 0.28f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.22f * alpha)));
        drawList.AddCircle(center, radius * 0.62f,
            ImGui.GetColorU32(new Vector4(1f, 0.60f + pulse * 0.25f, 0.26f, (0.7f + pulse * 0.3f) * alpha)), 0,
            MathF.Max(1.4f, radius * 0.14f));
        drawList.AddCircleFilled(center, radius * 0.20f,
            ImGui.GetColorU32(new Vector4(1f, 0.86f, 0.52f, (0.75f + pulse * 0.25f) * alpha)));
    }

    private void DrawRainbow(ImDrawListPtr drawList, Vector2 center, float radius, float alpha)
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
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.45f * alpha)));
        drawList.AddCircleFilled(center, radius * 0.26f, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.90f * alpha)));
        drawList.AddCircle(center, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.35f * alpha)), 0, 1.4f);
    }
}
