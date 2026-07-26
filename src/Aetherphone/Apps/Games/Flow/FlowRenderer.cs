using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Flow;

internal sealed class FlowRenderer
{
    private static readonly Vector4[] Colors =
    {
        new(0.95f, 0.42f, 0.50f, 1f), new(0.40f, 0.68f, 0.98f, 1f), new(0.46f, 0.86f, 0.66f, 1f),
        new(0.95f, 0.74f, 0.34f, 1f), new(0.72f, 0.46f, 0.96f, 1f), new(0.36f, 0.82f, 0.82f, 1f),
        new(0.95f, 0.55f, 0.78f, 1f), new(0.62f, 0.66f, 0.74f, 1f), new(0.80f, 0.56f, 0.36f, 1f),
    };

    public static Vector4 ColorOf(int color) => Colors[color % Colors.Length];

    public void Draw(FlowBoard board, GameGrid grid, PhoneTheme theme, float scale, float emptyHighlight)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = grid.Pitch * 0.14f;
        var boardMin = grid.Origin - new Vector2(6f * scale, 6f * scale);
        var boardMax = grid.Origin + new Vector2(grid.Width, grid.Height) + new Vector2(6f * scale, 6f * scale);
        Squircle.Fill(drawList, boardMin, boardMax, 18f * scale, ImGui.GetColorU32(GamePalette.Board));
        var emptyGlow = emptyHighlight * (0.35f + 0.65f * Pulse.Wave(Pulse.Fast));
        for (var row = 0; row < board.Rows; row++)
        {
            for (var column = 0; column < board.Columns; column++)
            {
                var cell = grid.Cell(column, row);
                var index = row * board.Columns + column;
                var occupant = board.Owner(index);
                var fill = occupant >= 0
                    ? GamePalette.Darken(ColorOf(occupant), 0.5f) with { W = 0.55f }
                    : GamePalette.Cell;
                Squircle.Fill(drawList, cell.Min, cell.Max, rounding, ImGui.GetColorU32(fill));
                if (occupant >= 0 || emptyGlow <= 0f)
                {
                    continue;
                }

                Squircle.Stroke(drawList, cell.Min, cell.Max, rounding,
                    ImGui.GetColorU32(theme.Accent with { W = 0.9f * emptyGlow }), 2f * scale);
            }
        }

        var thickness = grid.Pitch * 0.36f;
        for (var color = 0; color < board.ColorCount; color++)
        {
            DrawPath(drawList, board, grid, color, thickness);
        }

        var pulse = 0.4f + 0.6f * Pulse.Wave(Pulse.Fast);
        for (var color = 0; color < board.ColorCount; color++)
        {
            DrawEndpoints(drawList, board, grid, color, thickness, pulse);
        }

        DrawActiveHead(drawList, board, grid, thickness, pulse);
    }

    public void DrawProgress(Rect row, PhoneTheme theme, Vector4 accent, float scale, int filled, int total, bool warn)
    {
        if (total <= 0)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var caption = Loc.Culture.TextInfo.ToUpper(Loc.T(L.Games.Filled));
        var count = $"{GameNumber.Label(filled)}/{GameNumber.Label(total)}";
        var captionSize = Typography.Measure(caption, TextStyles.Caption2);
        var countSize = Typography.Measure(count, TextStyles.Caption2);
        var textY = row.Center.Y - captionSize.Y * 0.5f;
        var countTint = warn ? GamePalette.Lighten(accent, 0.2f) : theme.TextMuted;
        Typography.Draw(drawList, new Vector2(row.Min.X, textY), caption, theme.TextMuted, TextStyles.Caption2);
        Typography.Draw(drawList, new Vector2(row.Max.X - countSize.X, textY), count, countTint, TextStyles.Caption2);
        var trackLeft = row.Min.X + captionSize.X + 10f * scale;
        var trackRight = row.Max.X - countSize.X - 10f * scale;
        var trackWidth = trackRight - trackLeft;
        if (trackWidth <= 0f)
        {
            return;
        }

        var height = 5f * scale;
        var radius = height * 0.5f;
        var min = new Vector2(trackLeft, row.Center.Y - radius);
        var max = new Vector2(trackRight, min.Y + height);
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(GamePalette.CellSunken));
        var pulse = 0.6f + 0.4f * Pulse.Wave(Pulse.Fast);
        var barColor = warn ? GamePalette.Lighten(accent, 0.3f) with { W = pulse } : accent;
        var fillWidth = MathF.Max(height, trackWidth * filled / total);
        Squircle.Fill(drawList, min, new Vector2(min.X + fillWidth, max.Y), radius, ImGui.GetColorU32(barColor));
    }

    private void DrawPath(ImDrawListPtr drawList, FlowBoard board, GameGrid grid, int color, float thickness)
    {
        var length = board.PathLength(color);
        if (length < 1)
        {
            return;
        }

        var packed = ImGui.GetColorU32(ColorOf(color));
        var joint = thickness * 0.5f;
        var previous = CellCenter(grid, board, board.PathCell(color, 0));
        drawList.AddCircleFilled(previous, joint, packed, 20);
        for (var index = 1; index < length; index++)
        {
            var current = CellCenter(grid, board, board.PathCell(color, index));
            drawList.AddLine(previous, current, packed, thickness);
            drawList.AddCircleFilled(current, joint, packed, 20);
            previous = current;
        }
    }

    private void DrawEndpoints(ImDrawListPtr drawList, FlowBoard board, GameGrid grid, int color, float thickness,
        float pulse)
    {
        var radius = grid.Pitch * 0.30f;
        var packed = ImGui.GetColorU32(ColorOf(color));
        var connected = board.IsConnected(color);
        DrawDot(drawList, CellCenter(grid, board, board.EndpointA(color)), radius, packed, connected, pulse);
        DrawDot(drawList, CellCenter(grid, board, board.EndpointB(color)), radius, packed, connected, pulse);
    }

    private void DrawDot(ImDrawListPtr drawList, Vector2 center, float radius, uint packed, bool connected, float pulse)
    {
        if (connected)
        {
            drawList.AddCircleFilled(center, radius * (1.2f + 0.12f * pulse), packed & 0x33FFFFFF, 24);
        }

        drawList.AddCircleFilled(center, radius, packed, 24);
        drawList.AddCircleFilled(center - new Vector2(radius * 0.3f, radius * 0.3f), radius * 0.34f,
            ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.5f)), 16);
    }

    private void DrawActiveHead(ImDrawListPtr drawList, FlowBoard board, GameGrid grid, float thickness, float pulse)
    {
        var active = board.ActiveColor;
        if (active < 0 || board.PathLength(active) == 0)
        {
            return;
        }

        var head = board.PathCell(active, board.PathLength(active) - 1);
        if (board.IsEndpoint(head))
        {
            return;
        }

        var center = CellCenter(grid, board, head);
        var color = ColorOf(active);
        ProgressRing.Glow(center, thickness * 0.9f, color, 0.6f + 0.4f * pulse);
        drawList.AddCircleFilled(center, thickness * 0.62f, ImGui.GetColorU32(GamePalette.Lighten(color, 0.3f)), 24);
    }

    private Vector2 CellCenter(GameGrid grid, FlowBoard board, int cell)
    {
        return grid.CellCenter(cell % board.Columns, cell / board.Columns);
    }
}
