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

    public static TileAnim Still => new(1f, false, 1f, -1, 1f);
}

internal static class Twenty48Renderer
{
    public const float GapFraction = 0.08f;
    private const float Rounding = 8f;
    private const float TrailWidthFraction = 0.55f;
    private const float TrailAlpha = 0.5f;
    private const float MergePulse = 0.16f;
    private const float SheenAlpha = 0.08f;
    private static readonly Vector4 Sheen = new(1f, 1f, 1f, SheenAlpha);
    private static readonly Vector4[] TileColors =
    {
        new(0.93f, 0.89f, 0.85f, 1f), new(0.93f, 0.87f, 0.78f, 1f), new(0.95f, 0.69f, 0.47f, 1f),
        new(0.96f, 0.58f, 0.39f, 1f), new(0.96f, 0.49f, 0.37f, 1f), new(0.96f, 0.37f, 0.23f, 1f),
        new(0.93f, 0.81f, 0.45f, 1f), new(0.93f, 0.80f, 0.38f, 1f), new(0.93f, 0.78f, 0.31f, 1f),
        new(0.95f, 0.76f, 0.22f, 1f), new(0.40f, 0.70f, 0.95f, 1f), new(0.36f, 0.55f, 0.95f, 1f),
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

    public static Vector2 TileCenter(in GameGrid grid, Twenty48Board board, in TileAnim anim, int index)
    {
        var center = grid.CellCenter(index % Twenty48Board.Size, index / Twenty48Board.Size);
        var source = board.SlideFrom(index);
        if (!anim.Sliding || source < 0)
        {
            return center;
        }

        var from = grid.CellCenter(source % Twenty48Board.Size, source / Twenty48Board.Size);
        return Vector2.Lerp(from, center, Easing.EaseOutCubic(anim.Slide));
    }

    public static void DrawBoard(ImDrawListPtr drawList, Twenty48Board board, in GameGrid grid, in TileAnim anim,
        Ribbon[]? trails, float trailFade, float scale, Vector4 accent, float entrance, StageInk ink)
    {
        BoardPlate.Draw(drawList, BoardPlate.Around(grid.Bounds, scale), BoardPlate.Radius * scale, scale, accent, ink);
        var rounding = Rounding * scale;
        for (var index = 0; index < Twenty48Board.CellCount; index++)
        {
            var pop = GameJuice.PopIn(GameJuice.Stagger(entrance, index, Twenty48Board.CellCount));
            if (pop <= 0.01f)
            {
                continue;
            }

            var cell = grid.Cell(index % Twenty48Board.Size, index / Twenty48Board.Size);
            var half = cell.Size * 0.5f * pop;
            StageCell.Draw(drawList, new Rect(cell.Center - half, cell.Center + half), GamePalette.CellSunken,
                CellDepth.Sunken, rounding * pop, scale);
        }

        if (trails is not null && trailFade > 0.01f)
        {
            DrawTrails(drawList, board, grid, trails, trailFade);
        }

        for (var index = 0; index < Twenty48Board.CellCount; index++)
        {
            var value = board.Value(index);
            if (value == 0)
            {
                continue;
            }

            DrawTile(drawList, board, grid, anim, index, value, rounding, scale, entrance);
        }
    }

    private static void DrawTrails(ImDrawListPtr drawList, Twenty48Board board, in GameGrid grid, Ribbon[] trails,
        float trailFade)
    {
        var width = (grid.Pitch - grid.Gap) * TrailWidthFraction;
        for (var index = 0; index < Twenty48Board.CellCount; index++)
        {
            if (board.SlideFrom(index) < 0 || trails[index].Count < 2)
            {
                continue;
            }

            var color = ColorFor(board.Value(index)) with { W = TrailAlpha * trailFade };
            trails[index].Draw(drawList, color, width, additive: true);
        }
    }

    private static void DrawTile(ImDrawListPtr drawList, Twenty48Board board, in GameGrid grid, in TileAnim anim,
        int index, int value, float rounding, float scale, float entrance)
    {
        var tileScale = GameJuice.PopIn(GameJuice.Stagger(entrance, index, Twenty48Board.CellCount));
        if (tileScale <= 0.01f)
        {
            return;
        }

        var center = TileCenter(grid, board, anim, index);
        var merging = board.Merged(index) && !anim.Sliding && anim.Resolve < 1f;
        if (index == anim.SpawnIndex)
        {
            if (anim.Sliding)
            {
                return;
            }

            tileScale *= Easing.EaseOutBack(anim.Spawn);
            if (tileScale <= 0.01f)
            {
                return;
            }
        }
        else if (merging)
        {
            tileScale *= 1f + MergePulse * MathF.Sin(anim.Resolve * MathF.PI);
        }

        var half = (grid.Pitch - grid.Gap) * 0.5f * tileScale;
        var rect = new Rect(new Vector2(center.X - half, center.Y - half), new Vector2(center.X + half, center.Y + half));
        var color = ColorFor(value);
        if (merging)
        {
            ProgressRing.Glow(center, half * 1.15f, GamePalette.Lighten(color, 0.3f),
                0.8f * MathF.Sin(anim.Resolve * MathF.PI));
        }

        var tileRounding = rounding * tileScale;
        StageCell.Draw(drawList, rect, color, CellDepth.Raised, tileRounding, scale);
        Squircle.Fill(drawList, rect.Min, new Vector2(rect.Max.X, rect.Min.Y + half * 0.7f), tileRounding,
            ImGui.GetColorU32(Sheen));
        var textScale = value >= 1000 ? 1.05f : value >= 100 ? 1.3f : 1.55f;
        Typography.DrawCentered(drawList, center, GameNumber.Label(value), GamePalette.InkOn(color),
            textScale * tileScale, FontWeight.Bold);
    }
}
