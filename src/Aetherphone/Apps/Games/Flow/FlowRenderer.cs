using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core.Theme;
using Aetherphone.Windows;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Flow;

internal sealed class FlowRenderer
{
    private static readonly Vector4[] Colors =
    {
        new(0.95f, 0.42f, 0.50f, 1f),
        new(0.40f, 0.68f, 0.98f, 1f),
        new(0.46f, 0.86f, 0.66f, 1f),
        new(0.95f, 0.74f, 0.34f, 1f),
        new(0.72f, 0.46f, 0.96f, 1f),
        new(0.36f, 0.82f, 0.82f, 1f),
        new(0.95f, 0.55f, 0.78f, 1f),
        new(0.62f, 0.66f, 0.74f, 1f),
    };

    public static Vector4 ColorOf(int color) => Colors[color % Colors.Length];

    public void Draw(FlowBoard board, GameGrid grid, PhoneTheme theme, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = grid.Pitch * 0.14f;

        var boardMin = grid.Origin - new Vector2(6f * scale, 6f * scale);
        var boardMax = grid.Origin + new Vector2(grid.Width, grid.Height) + new Vector2(6f * scale, 6f * scale);
        Squircle.Fill(drawList, boardMin, boardMax, 18f * scale, ImGui.GetColorU32(GamePalette.Board));

        for (var row = 0; row < board.Size; row++)
        {
            for (var column = 0; column < board.Size; column++)
            {
                var cell = grid.Cell(column, row);
                var index = row * board.Size + column;
                var occupant = board.Owner(index);
                var fill = occupant >= 0 ? GamePalette.Darken(ColorOf(occupant), 0.5f) with { W = 0.55f } : GamePalette.Cell;
                Squircle.Fill(drawList, cell.Min, cell.Max, rounding, ImGui.GetColorU32(fill));
            }
        }

        var thickness = grid.Pitch * 0.36f;
        for (var color = 0; color < board.ColorCount; color++)
        {
            DrawPath(drawList, board, grid, color, thickness);
        }

        var pulse = 0.4f + 0.6f * Styling.Pulse(Styling.PulseFast);
        for (var color = 0; color < board.ColorCount; color++)
        {
            DrawEndpoints(drawList, board, grid, color, thickness, pulse);
        }

        DrawActiveHead(drawList, board, grid, thickness, pulse);
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

    private void DrawEndpoints(ImDrawListPtr drawList, FlowBoard board, GameGrid grid, int color, float thickness, float pulse)
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
        drawList.AddCircleFilled(center - new Vector2(radius * 0.3f, radius * 0.3f), radius * 0.34f, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.5f)), 16);
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
        return grid.CellCenter(cell % board.Size, cell / board.Size);
    }
}
