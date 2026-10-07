using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Trails;

internal static class TrailsRenderer
{
    public const float ScoreboardPad = 10f;
    public const float ScoreboardGap = 10f;
    private const float BotDotFraction = 0.7f;
    private const float PipRun = TrailsBoard.WinsNeeded * (PipSize + PipGap);
    private const float TrailWidth = 0.3f;
    private const float HeadRadius = 0.34f;
    private const float CycleLength = 0.8f;
    private const float GridAlpha = 0.07f;
    private const float FrameAlpha = 0.5f;
    private const int GridStep = 4;
    private const float DotSize = 9f;
    private const float PipSize = 6f;
    private const float PipGap = 3f;
    private const float HintAlpha = 0.5f;
    private static readonly Vector4 LossColor = new(0.95f, 0.36f, 0.36f, 1f);
    private static readonly Vector4[] BotColors =
    {
        new(0.36f, 0.86f, 1f, 1f), new(1f, 0.68f, 0.28f, 1f), new(0.64f, 1f, 0.38f, 1f),
    };

    public static Vector4 RiderColor(int index, Vector4 accent) =>
        index == TrailsBoard.Player ? accent : BotColors[(index - 1) % BotColors.Length];

    public static Rect FieldRect(in Camera2D camera) =>
        new(camera.ToScreen(Vector2.Zero), camera.ToScreen(new Vector2(TrailsBoard.Columns, TrailsBoard.Rows)));

    public static float ScoreboardWidth(int bots) =>
        ScoreboardPad * 2f + DotSize * (2f - BotDotFraction + bots * BotDotFraction) + PipGap * 2f + PipRun * 2f +
        ScoreboardGap;

    public static void DrawField(ImDrawListPtr drawList, Rect field, in Camera2D camera, Vector4 accent, float scale)
    {
        var grid = ImGui.GetColorU32(accent with { W = GridAlpha });
        for (var column = GridStep; column < TrailsBoard.Columns; column += GridStep)
        {
            var x = camera.ToScreen(new Vector2(column, 0f)).X;
            drawList.AddLine(new Vector2(x, field.Min.Y), new Vector2(x, field.Max.Y), grid, 1f);
        }

        for (var row = GridStep; row < TrailsBoard.Rows; row += GridStep)
        {
            var y = camera.ToScreen(new Vector2(0f, row)).Y;
            drawList.AddLine(new Vector2(field.Min.X, y), new Vector2(field.Max.X, y), grid, 1f);
        }

        var width = MathF.Max(1f, 1.2f * scale);
        var color = accent with { W = FrameAlpha };
        NeonStroke.Line(drawList, field.Min, new Vector2(field.Max.X, field.Min.Y), color, width);
        NeonStroke.Line(drawList, new Vector2(field.Max.X, field.Min.Y), field.Max, color, width);
        NeonStroke.Line(drawList, field.Max, new Vector2(field.Min.X, field.Max.Y), color, width);
        NeonStroke.Line(drawList, new Vector2(field.Min.X, field.Max.Y), field.Min, color, width);
    }

    public static void DrawTrails(ImDrawListPtr drawList, TrailsBoard board, in Camera2D camera, Vector4 accent,
        float scale, float time)
    {
        var width = MathF.Max(1.5f * scale, camera.Px(TrailWidth));
        Span<Vector2> points = stackalloc Vector2[TrailsBoard.PathCapacity + 1];
        for (var index = 0; index < board.RiderCount; index++)
        {
            ref readonly var rider = ref board.RiderAt(index);
            if (rider.PathCount == 0 || rider.Derezz >= 1f)
            {
                continue;
            }

            var count = 0;
            for (var point = 0; point < rider.PathCount; point++)
            {
                points[count++] = camera.ToScreen(board.PathPoint(index, point));
            }

            if (rider.Alive)
            {
                points[count++] = camera.ToScreen(board.HeadPosition(index));
            }

            var color = RiderColor(index, accent);
            if (rider.Derezz > 0f)
            {
                var flicker = 0.6f + 0.4f * MathF.Sin(time * 60f + index);
                color = color with { W = (1f - rider.Derezz) * flicker };
            }

            NeonStroke.Path(drawList, points[..count], false, color, width);
        }

        for (var index = 0; index < board.RiderCount; index++)
        {
            ref readonly var rider = ref board.RiderAt(index);
            if (!rider.Alive)
            {
                continue;
            }

            DrawCycle(drawList, board, index, in camera, RiderColor(index, accent), width, time);
        }
    }

    public static void DrawHints(ImDrawListPtr drawList, Rect field, Vector4 accent, float scale, float alpha)
    {
        if (alpha <= 0.01f)
        {
            return;
        }

        var size = 14f * scale;
        var y = field.Max.Y - size * 2.4f;
        var color = accent with { W = HintAlpha * alpha };
        var width = MathF.Max(1f, 2f * scale);
        var left = new Vector2(field.Min.X + size * 2f, y);
        var right = new Vector2(field.Max.X - size * 2f, y);
        NeonStroke.Line(drawList, left + new Vector2(size * 0.5f, -size), left - new Vector2(size * 0.5f, 0f), color, width);
        NeonStroke.Line(drawList, left - new Vector2(size * 0.5f, 0f), left + new Vector2(size * 0.5f, size), color, width);
        NeonStroke.Line(drawList, right - new Vector2(size * 0.5f, size), right + new Vector2(size * 0.5f, 0f), color,
            width);
        NeonStroke.Line(drawList, right + new Vector2(size * 0.5f, 0f), right - new Vector2(size * 0.5f, -size), color,
            width);
    }

    public static void DrawScoreboard(ImDrawListPtr drawList, Rect rect, TrailsBoard board, Vector4 accent, float scale)
    {
        StageHud.Capsule(drawList, rect, scale);
        var centerY = rect.Center.Y;
        var dotRadius = DotSize * 0.5f * scale;
        var x = rect.Min.X + ScoreboardPad * scale;
        NeonStroke.Dot(drawList, new Vector2(x + dotRadius, centerY), dotRadius * 0.7f, accent);
        x += DotSize * scale + PipGap * scale;
        x = DrawPips(drawList, x, centerY, board.PlayerWins, accent, scale);
        x += ScoreboardGap * scale;
        for (var index = 1; index < board.RiderCount; index++)
        {
            NeonStroke.Dot(drawList, new Vector2(x + dotRadius, centerY), dotRadius * 0.55f, RiderColor(index, accent));
            x += DotSize * BotDotFraction * scale;
        }

        x += DotSize * (1f - BotDotFraction) * scale + PipGap * scale;
        DrawPips(drawList, x, centerY, board.PlayerLosses, LossColor, scale);
    }

    private static float DrawPips(ImDrawListPtr drawList, float x, float centerY, int filled, Vector4 color, float scale)
    {
        for (var pip = 0; pip < TrailsBoard.WinsNeeded; pip++)
        {
            var center = new Vector2(x + PipSize * 0.5f * scale, centerY);
            drawList.AddCircleFilled(center, PipSize * 0.5f * scale,
                ImGui.GetColorU32(pip < filled ? color : color with { W = 0.22f }), 10);
            x += (PipSize + PipGap) * scale;
        }

        return x;
    }

    private static void DrawCycle(ImDrawListPtr drawList, TrailsBoard board, int index, in Camera2D camera,
        Vector4 color, float width, float time)
    {
        ref readonly var rider = ref board.RiderAt(index);
        var head = board.HeadPosition(index);
        var back = new Vector2(TrailsBoard.DeltaX(rider.Heading), TrailsBoard.DeltaY(rider.Heading)) * -CycleLength;
        var screenHead = camera.ToScreen(head);
        NeonStroke.Line(drawList, camera.ToScreen(head + back), screenHead, color, width * 1.6f);
        var pulse = 0.85f + 0.15f * MathF.Sin(time * 9f + index);
        NeonStroke.Dot(drawList, screenHead, camera.Px(HeadRadius) * pulse, color);
        if (index != TrailsBoard.Player)
        {
            return;
        }

        drawList.AddCircle(screenHead, camera.Px(HeadRadius * 2.4f) * pulse,
            ImGui.GetColorU32(color with { W = 0.35f }), 20, MathF.Max(1f, width * 0.5f));
    }
}
