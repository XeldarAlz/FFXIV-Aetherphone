using Aetherphone.Apps.Games.Framework;
using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Games.Siege;

internal sealed class SiegeRenderer
{
    public const float WorldLeft = -0.14f;
    public const float WorldTop = -0.85f;
    public const float WorldRight = SiegeRules.Columns + 0.14f;
    public const float WorldBottom = SiegeRules.Rows + 0.6f;
    public const float FenceTop = SiegeRules.Rows + 0.08f;
    public const float FenceBottom = SiegeRules.Rows + 0.44f;
    public const float FeetOffset = 0.3f;
    public const float MoteRadius = 0.16f;
    private const float CellGap = 0.06f;
    private const float CellRadius = 0.14f;
    private const int BladesPerCell = 3;
    private const int FlowersPerColumn = 3;
    private const float MoteBlinkSeconds = 2f;
    private static readonly Vector4 LawnLight = new(0.44f, 0.72f, 0.36f, 1f);
    private static readonly Vector4 LawnDark = new(0.37f, 0.63f, 0.31f, 1f);
    private static readonly Vector4 Blade = new(0.28f, 0.52f, 0.24f, 1f);
    private static readonly Vector4 Hedge = new(0.20f, 0.44f, 0.22f, 1f);
    private static readonly Vector4 HedgeTop = new(0.28f, 0.56f, 0.28f, 1f);
    private static readonly Vector4 Danger = new(0.95f, 0.30f, 0.30f, 1f);
    private static readonly Vector4 Valid = new(0.70f, 1f, 0.62f, 1f);
    private static readonly Vector4 Blast = new(1f, 0.58f, 0.22f, 1f);
    private static readonly Vector4[] Flowers =
    {
        new(1f, 0.62f, 0.76f, 1f), new(1f, 0.96f, 0.92f, 1f), new(1f, 0.86f, 0.36f, 1f),
    };

    private readonly int[] order = new int[SiegeBoard.EnemyCapacity];

    public static Rect World => new(new Vector2(WorldLeft, WorldTop), new Vector2(WorldRight, WorldBottom));

    public static Vector2 Feet(in SiegeEnemy enemy) => new(enemy.Column + 0.5f, enemy.Y + FeetOffset);

    public void DrawGarden(ImDrawListPtr drawList, in Camera2D camera, float entrance, float scale, Vector4 accent,
        StageInk ink, ReadOnlySpan<float> fenceFlash)
    {
        var gridMin = camera.ToScreen(Vector2.Zero);
        var gridMax = camera.ToScreen(new Vector2(SiegeRules.Columns, FenceBottom));
        BoardPlate.Draw(drawList, BoardPlate.Around(new Rect(gridMin, gridMax), scale), BoardPlate.Radius * scale, scale,
            accent, ink);
        var unit = camera.Px(1f);
        var gap = new Vector2(unit * CellGap * 0.5f);
        var blade = ImGui.GetColorU32(Blade);
        for (var cell = 0; cell < SiegeRules.CellCount; cell++)
        {
            var column = SiegeRules.ColumnOf(cell);
            var row = SiegeRules.RowOf(cell);
            var lift = StageCell.Lift(GameJuice.Stagger(entrance, cell, SiegeRules.CellCount)) * scale;
            var min = camera.ToScreen(new Vector2(column, row)) + gap - new Vector2(0f, lift);
            var max = camera.ToScreen(new Vector2(column + 1, row + 1)) - gap - new Vector2(0f, lift);
            var fill = (column + row) % 2 == 0 ? LawnLight : LawnDark;
            StageCell.Draw(drawList, new Rect(min, max), fill, CellDepth.Raised, unit * CellRadius, scale);
            DrawBlades(drawList, min, max, cell, blade, scale);
        }

        DrawFence(drawList, camera, unit, fenceFlash);
    }

    public static void DrawPlacement(ImDrawListPtr drawList, in Camera2D camera, int column, int row, bool valid,
        DefenderKind ghost, float time, float scale)
    {
        if (column < 0 || row < 0)
        {
            return;
        }

        var unit = camera.Px(1f);
        if (ghost == DefenderKind.Bombcap && valid)
        {
            var blastMin = camera.ToScreen(new Vector2(Math.Max(0, column - 1), Math.Max(0, row - 1)));
            var blastMax = camera.ToScreen(new Vector2(Math.Min(SiegeRules.Columns, column + 2), Math.Min(SiegeRules.Rows, row + 2)));
            Squircle.Fill(drawList, blastMin, blastMax, unit * CellRadius, ImGui.GetColorU32(Blast with { W = 0.16f }));
            Squircle.Stroke(drawList, blastMin, blastMax, unit * CellRadius, ImGui.GetColorU32(Blast with { W = 0.55f }),
                1.5f * scale);
        }

        var min = camera.ToScreen(new Vector2(column, row));
        var max = camera.ToScreen(new Vector2(column + 1, row + 1));
        var tint = valid ? Valid : Danger;
        var pulse = 0.75f + 0.25f * MathF.Sin(time * 7f);
        Squircle.Fill(drawList, min, max, unit * CellRadius, ImGui.GetColorU32(tint with { W = 0.22f * pulse }));
        Squircle.Stroke(drawList, min, max, unit * CellRadius, ImGui.GetColorU32(tint with { W = 0.8f }), 2f * scale);
        if (!valid || ghost == DefenderKind.None)
        {
            return;
        }

        SiegeArt.Card(drawList, ghost, (min + max) * 0.5f, unit, time, 0.5f);
    }

    public static void DrawDefenders(ImDrawListPtr drawList, in Camera2D camera, SiegeBoard board, float time,
        float alpha)
    {
        var unit = camera.Px(1f);
        for (var cell = 0; cell < SiegeRules.CellCount; cell++)
        {
            ref readonly var defender = ref board.Defender(cell);
            if (!defender.Occupied)
            {
                continue;
            }

            var center = camera.ToScreen(SiegeBoard.CellCenter(SiegeRules.ColumnOf(cell), SiegeRules.RowOf(cell)));
            SiegeArt.Defender(drawList, defender, center, unit, time, alpha);
            if (defender.Kind == DefenderKind.Thornwall || defender.HealthFraction >= 1f)
            {
                continue;
            }

            SiegeArt.HealthBar(drawList, center - new Vector2(0f, unit * 0.44f), unit * 0.5f, unit * 0.06f,
                defender.HealthFraction, alpha);
        }
    }

    public void DrawEnemies(ImDrawListPtr drawList, in Camera2D camera, SiegeBoard board, float time, float alpha)
    {
        var count = 0;
        for (var index = 0; index < SiegeBoard.EnemyCapacity; index++)
        {
            if (board.Enemy(index).Alive)
            {
                order[count++] = index;
            }
        }

        for (var index = 1; index < count; index++)
        {
            var current = order[index];
            var key = SortKey(board.Enemy(current));
            var slot = index - 1;
            while (slot >= 0 && SortKey(board.Enemy(order[slot])) > key)
            {
                order[slot + 1] = order[slot];
                slot--;
            }

            order[slot + 1] = current;
        }

        var unit = camera.Px(1f);
        for (var index = 0; index < count; index++)
        {
            ref readonly var enemy = ref board.Enemy(order[index]);
            var feet = camera.ToScreen(Feet(enemy));
            if (enemy.State == EnemyState.Burrowing)
            {
                SiegeArt.Mound(drawList, feet, unit, enemy.Stride, time + enemy.Id, alpha);
                continue;
            }

            var fade = alpha * Math.Clamp((enemy.Y - SiegeRules.SpawnY) * 4f + 0.25f, 0f, 1f);
            SiegeArt.Mandragora(drawList, enemy, feet, unit, time, fade);
            if (enemy.HealthFraction >= 1f)
            {
                continue;
            }

            var size = SiegeArt.Size(enemy.Kind);
            var lift = enemy.Kind == EnemyKind.Flyer ? 0.3f : 0f;
            var barCenter = feet - new Vector2(0f, unit * (0.62f * size + 0.16f + lift));
            SiegeArt.HealthBar(drawList, barCenter, unit * 0.42f * MathF.Max(1f, size * 0.8f), unit * 0.055f,
                enemy.HealthFraction, fade);
        }
    }

    public static void DrawSeeds(ImDrawListPtr drawList, in Camera2D camera, SiegeBoard board, float alpha)
    {
        var unit = camera.Px(1f);
        for (var index = 0; index < SiegeBoard.SeedCapacity; index++)
        {
            ref readonly var seed = ref board.Seed(index);
            if (!seed.Alive)
            {
                continue;
            }

            SiegeArt.Seed(drawList, camera.ToScreen(new Vector2(seed.Column + 0.5f, seed.Y)), unit, seed.Frost, alpha);
        }
    }

    public static void DrawMotes(ImDrawListPtr drawList, in Camera2D camera, SiegeBoard board, float time, float alpha)
    {
        var radius = camera.Px(MoteRadius);
        for (var index = 0; index < SiegeBoard.MoteCapacity; index++)
        {
            ref readonly var mote = ref board.Mote(index);
            if (!mote.Alive)
            {
                continue;
            }

            var remaining = mote.Remaining;
            var blink = remaining < MoteBlinkSeconds ? 0.35f + 0.65f * MathF.Abs(MathF.Sin(remaining * 9f)) : 1f;
            var position = camera.ToScreen(SiegeBoard.MotePosition(mote));
            SiegeArt.Mote(drawList, position, radius, time + mote.Id * 0.7f, alpha * blink);
        }
    }

    private static float SortKey(in SiegeEnemy enemy) =>
        enemy.Kind == EnemyKind.Flyer ? enemy.Y + SiegeRules.Rows * 4f : enemy.Y;

    private static void DrawBlades(ImDrawListPtr drawList, Vector2 min, Vector2 max, int cell, uint color, float scale)
    {
        var width = max.X - min.X;
        var height = max.Y - min.Y;
        var seed = cell * 37 + 11;
        for (var blade = 0; blade < BladesPerCell; blade++)
        {
            var offsetX = (seed + blade * 53) % 100 / 100f;
            var offsetY = (seed + blade * 71) % 100 / 100f;
            var root = new Vector2(min.X + width * (0.12f + offsetX * 0.76f), min.Y + height * (0.2f + offsetY * 0.6f));
            var length = height * 0.07f;
            var lean = (offsetX - 0.5f) * length;
            drawList.AddLine(root, root + new Vector2(lean, -length), color, 1.3f * scale);
        }
    }

    private static void DrawFence(ImDrawListPtr drawList, in Camera2D camera, float unit, ReadOnlySpan<float> fenceFlash)
    {
        var min = camera.ToScreen(new Vector2(0.04f, FenceTop));
        var max = camera.ToScreen(new Vector2(SiegeRules.Columns - 0.04f, FenceBottom));
        var radius = (max.Y - min.Y) * 0.5f;
        Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(Hedge));
        Squircle.Fill(drawList, min, new Vector2(max.X, (min.Y + max.Y) * 0.5f), radius * 0.6f, ImGui.GetColorU32(HedgeTop));
        for (var column = 0; column < SiegeRules.Columns; column++)
        {
            var flash = column < fenceFlash.Length ? fenceFlash[column] : 0f;
            if (flash > 0f)
            {
                var flashMin = camera.ToScreen(new Vector2(column + 0.04f, FenceTop - 0.05f));
                var flashMax = camera.ToScreen(new Vector2(column + 0.96f, FenceBottom + 0.03f));
                Squircle.Fill(drawList, flashMin, flashMax, radius, ImGui.GetColorU32(Danger with { W = 0.65f * flash }));
            }

            for (var flower = 0; flower < FlowersPerColumn; flower++)
            {
                var x = column + (flower + 0.5f) / FlowersPerColumn;
                var bounce = flash * 0.08f * MathF.Sin(flower * 2.1f + flash * 12f);
                var center = camera.ToScreen(new Vector2(x, FenceTop + 0.12f + (flower % 2) * 0.1f + bounce));
                var petal = ImGui.GetColorU32(Flowers[(column + flower) % Flowers.Length]);
                var petalRadius = unit * 0.045f;
                for (var leaf = 0; leaf < 5; leaf++)
                {
                    var angle = leaf * MathF.Tau / 5f + column;
                    drawList.AddCircleFilled(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * petalRadius * 1.2f,
                        petalRadius, petal, 8);
                }

                drawList.AddCircleFilled(center, petalRadius * 0.8f, ImGui.GetColorU32(SiegeArt.Sun), 8);
            }
        }
    }
}
