using System.Numerics;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Twenty48;

internal readonly struct TileAnim
{
    public readonly float Slide;

    public readonly bool Sliding;

    public readonly float Resolve;

    public readonly int SpawnIndex;

    public readonly float Spawn;

    public TileAnim(float slide, bool sliding, float resolve, int spawnIndex, float spawn)
    {
        Slide = slide;
        Sliding = sliding;
        Resolve = resolve;
        SpawnIndex = spawnIndex;
        Spawn = spawn;
    }
}

internal sealed class Twenty48Renderer
{
    private static readonly Vector4[] TileColors =
    {
        new(0.93f, 0.89f, 0.85f, 1f),
        new(0.93f, 0.87f, 0.78f, 1f),
        new(0.95f, 0.69f, 0.47f, 1f),
        new(0.96f, 0.58f, 0.39f, 1f),
        new(0.96f, 0.49f, 0.37f, 1f),
        new(0.96f, 0.37f, 0.23f, 1f),
        new(0.93f, 0.81f, 0.45f, 1f),
        new(0.93f, 0.80f, 0.38f, 1f),
        new(0.93f, 0.78f, 0.31f, 1f),
        new(0.95f, 0.76f, 0.22f, 1f),
        new(0.40f, 0.70f, 0.95f, 1f),
        new(0.36f, 0.55f, 0.95f, 1f),
    };

    public static Vector4 ColorFor(int value)
    {
        var rank = 0;
        var scan = value;
        while (scan > 2)
        {
            scan >>= 1;
            rank++;
        }

        if (rank >= TileColors.Length)
        {
            rank = TileColors.Length - 1;
        }

        return TileColors[rank];
    }

    public void Draw(Twenty48Board board, GameGrid grid, in TileAnim anim, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var rounding = 7f * scale;

        var boardPad = grid.Gap;
        var boardMin = grid.Origin - new Vector2(boardPad, boardPad);
        var boardMax = grid.Origin + new Vector2(grid.Width, grid.Height) + new Vector2(boardPad, boardPad);
        Squircle.Fill(drawList, boardMin, boardMax, rounding + boardPad, ImGui.GetColorU32(GamePalette.Board));

        for (var row = 0; row < Twenty48Board.Size; row++)
        {
            for (var column = 0; column < Twenty48Board.Size; column++)
            {
                var cell = grid.Cell(column, row);
                Squircle.Fill(drawList, cell.Min, cell.Max, rounding, ImGui.GetColorU32(GamePalette.CellSunken));
            }
        }

        for (var index = 0; index < Twenty48Board.CellCount; index++)
        {
            var value = board.Value(index);
            if (value == 0)
            {
                continue;
            }

            DrawTile(drawList, board, grid, anim, index, value, rounding, scale);
        }
    }

    private void DrawTile(ImDrawListPtr drawList, Twenty48Board board, GameGrid grid, in TileAnim anim, int index, int value, float rounding, float scale)
    {
        var column = index % Twenty48Board.Size;
        var row = index / Twenty48Board.Size;
        var center = grid.CellCenter(column, row);

        var source = board.SlideFrom(index);
        if (anim.Sliding && source >= 0)
        {
            var fromCenter = grid.CellCenter(source % Twenty48Board.Size, source / Twenty48Board.Size);
            center = Vector2.Lerp(fromCenter, center, Easing.EaseOutCubic(anim.Slide));
        }

        var tileScale = 1f;
        if (index == anim.SpawnIndex)
        {
            if (anim.Sliding)
            {
                return;
            }

            tileScale = Easing.EaseOutBack(anim.Spawn);
            if (tileScale <= 0.01f)
            {
                return;
            }
        }
        else if (board.Merged(index) && !anim.Sliding && anim.Resolve < 1f)
        {
            tileScale = 1f + 0.16f * MathF.Sin(anim.Resolve * MathF.PI);
        }

        var halfPitch = (grid.Pitch - grid.Gap) * 0.5f * tileScale;
        var min = new Vector2(center.X - halfPitch, center.Y - halfPitch);
        var max = new Vector2(center.X + halfPitch, center.Y + halfPitch);

        var color = ColorFor(value);
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(color));
        Squircle.Fill(drawList, min, new Vector2(max.X, min.Y + halfPitch * 0.7f), rounding, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.07f)));

        var label = GameNumber.Label(value);
        var textScale = value >= 1000 ? 1.05f : value >= 100 ? 1.3f : 1.55f;
        Typography.DrawCentered(center, label, GamePalette.InkOn(color), textScale * tileScale, FontWeight.Bold);
    }
}
