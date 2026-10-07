using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Gloop;

internal enum BotSkill : byte
{
    Easy,
    Hard,
}

internal sealed class GloopBot
{
    public const int PlacementCount = 22;
    private const float Unreachable = float.MinValue;
    private const float DeathPenalty = 1_000_000f;
    private const float NextWeight = 0.85f;
    private const float ChainWeight = 40f;
    private const float LinkWeight = 14f;
    private const float HeightWeight = 0.9f;
    private const float SpawnDangerWeight = 400f;
    private const int SpawnDangerHeight = 8;
    private const int EasyChoices = 5;
    private const float EasyBlunder = 0.15f;
    private static readonly float[] EasyWeights = { 1f, 0.6f, 0.35f, 0.2f, 0.1f };
    private static readonly int[] PlacementColumns = BuildPlacements(true);
    private static readonly int[] PlacementOrientations = BuildPlacements(false);

    private readonly byte[] baseGrid = new byte[GloopRules.Cells];
    private readonly byte[] firstGrid = new byte[GloopRules.Cells];
    private readonly byte[] secondGrid = new byte[GloopRules.Cells];
    private readonly bool[] pop = new bool[GloopRules.Cells];
    private readonly bool[] seen = new bool[GloopRules.Cells];
    private readonly int[] group = new int[GloopRules.Cells];
    private readonly byte[] fall = new byte[GloopRules.Cells];
    private readonly float[] values = new float[PlacementCount];
    private readonly int[] ranked = new int[PlacementCount];
    private GameRandom random;
    private int plannedPiece = -1;
    private float thinkTimer;
    private float actionTimer;

    public BotSkill Skill { get; private set; }

    public int TargetColumn { get; private set; }

    public int TargetOrientation { get; private set; }

    public float ThinkSeconds => Skill == BotSkill.Hard ? 0.18f : 0.55f;

    public float ActionSeconds => Skill == BotSkill.Hard ? 0.07f : 0.2f;

    public void Reset(GameRandom seeded, BotSkill skill)
    {
        random = seeded;
        Skill = skill;
        plannedPiece = -1;
        thinkTimer = 0f;
        actionTimer = 0f;
    }

    public void Drive(GloopBoard board, float deltaSeconds)
    {
        if (!board.Falling)
        {
            board.SoftDrop(false);
            return;
        }

        if (plannedPiece != board.Pieces)
        {
            Plan(board);
            plannedPiece = board.Pieces;
            thinkTimer = ThinkSeconds;
            actionTimer = 0f;
            board.SoftDrop(false);
        }

        if (deltaSeconds <= 0f)
        {
            return;
        }

        if (thinkTimer > 0f)
        {
            thinkTimer -= deltaSeconds;
            return;
        }

        actionTimer -= deltaSeconds;
        if (actionTimer > 0f)
        {
            return;
        }

        actionTimer = ActionSeconds;
        if (board.PieceOrientation != TargetOrientation)
        {
            var turns = TargetOrientation - board.PieceOrientation + GloopRules.Orientations;
            if (!board.Rotate(turns % GloopRules.Orientations == 3 ? -1 : 1))
            {
                TargetOrientation = board.PieceOrientation;
            }

            return;
        }

        if (board.PieceColumn != TargetColumn)
        {
            if (!board.Shift(Math.Sign(TargetColumn - board.PieceColumn)))
            {
                TargetColumn = board.PieceColumn;
            }

            return;
        }

        board.SoftDrop(true);
    }

    public void Plan(GloopBoard board)
    {
        board.Grid.CopyTo(baseGrid);
        var pivot = board.PivotColor;
        var satellite = board.SatelliteColor;
        var nextPivot = board.NextPivot(0);
        var nextSatellite = board.NextSatellite(0);
        for (var placement = 0; placement < PlacementCount; placement++)
        {
            values[placement] = Evaluate(placement, pivot, satellite, nextPivot, nextSatellite);
        }

        var choice = Skill == BotSkill.Hard ? Best() : Weighted();
        TargetColumn = PlacementColumns[choice];
        TargetOrientation = PlacementOrientations[choice];
    }

    public static int ColumnOf(int placement) => PlacementColumns[placement];

    public static int OrientationOf(int placement) => PlacementOrientations[placement];

    public float ValueOf(int placement) => values[placement];

    private float Evaluate(int placement, byte pivot, byte satellite, byte nextPivot, byte nextSatellite)
    {
        var column = PlacementColumns[placement];
        var orientation = PlacementOrientations[placement];
        if (!Reachable(baseGrid, column, orientation))
        {
            return Unreachable;
        }

        baseGrid.CopyTo(firstGrid, 0);
        GloopRules.Place(firstGrid, column, orientation, pivot, satellite);
        var firstScore = GloopRules.Resolve(firstGrid, pop, group, seen, fall, out var firstChains);
        if (GloopRules.SpawnBlocked(firstGrid))
        {
            return firstScore - DeathPenalty;
        }

        var bestNext = float.MinValue;
        for (var next = 0; next < PlacementCount; next++)
        {
            var nextColumn = PlacementColumns[next];
            var nextOrientation = PlacementOrientations[next];
            if (!Reachable(firstGrid, nextColumn, nextOrientation))
            {
                continue;
            }

            firstGrid.CopyTo(secondGrid, 0);
            GloopRules.Place(secondGrid, nextColumn, nextOrientation, nextPivot, nextSatellite);
            var secondScore = GloopRules.Resolve(secondGrid, pop, group, seen, fall, out var secondChains);
            var value = (secondScore + secondChains * ChainWeight) * NextWeight + Shape(secondGrid);
            bestNext = MathF.Max(bestNext, value);
        }

        if (bestNext == float.MinValue)
        {
            bestNext = Shape(firstGrid) - DeathPenalty * 0.5f;
        }

        return firstScore + firstChains * ChainWeight + bestNext;
    }

    private static float Shape(ReadOnlySpan<byte> grid)
    {
        if (GloopRules.SpawnBlocked(grid))
        {
            return -DeathPenalty;
        }

        var value = 0f;
        for (var cell = GloopRules.Index(0, GloopRules.FirstVisibleRow); cell < GloopRules.Cells; cell++)
        {
            var color = grid[cell];
            if (!GloopRules.IsColor(color))
            {
                continue;
            }

            var column = GloopRules.ColumnOf(cell);
            if (column + 1 < GloopRules.Columns && grid[cell + 1] == color)
            {
                value += LinkWeight;
            }

            if (cell + GloopRules.Columns < GloopRules.Cells && grid[cell + GloopRules.Columns] == color)
            {
                value += LinkWeight;
            }
        }

        for (var column = 0; column < GloopRules.Columns; column++)
        {
            var height = GloopRules.Height(grid, column);
            value -= height * height * HeightWeight;
            if (column == GloopRules.SpawnColumn && height > SpawnDangerHeight)
            {
                value -= (height - SpawnDangerHeight) * SpawnDangerWeight;
            }
        }

        return value;
    }

    private static bool Reachable(ReadOnlySpan<byte> grid, int column, int orientation)
    {
        var satelliteColumn = column + GloopRules.SatelliteColumn(orientation);
        var from = Math.Min(GloopRules.SpawnColumn, Math.Min(column, satelliteColumn));
        var to = Math.Max(GloopRules.SpawnColumn, Math.Max(column, satelliteColumn));
        for (var sweep = from; sweep <= to; sweep++)
        {
            if (grid[GloopRules.Index(sweep, GloopRules.SpawnRow)] != GloopRules.Empty)
            {
                return false;
            }
        }

        return true;
    }

    private int Best()
    {
        var best = 0;
        for (var placement = 1; placement < PlacementCount; placement++)
        {
            if (values[placement] > values[best])
            {
                best = placement;
            }
        }

        return best;
    }

    private int Weighted()
    {
        var reachable = 0;
        for (var placement = 0; placement < PlacementCount; placement++)
        {
            if (values[placement] == Unreachable)
            {
                continue;
            }

            var slot = reachable++;
            while (slot > 0 && values[ranked[slot - 1]] < values[placement])
            {
                ranked[slot] = ranked[slot - 1];
                slot--;
            }

            ranked[slot] = placement;
        }

        if (reachable == 0)
        {
            return Best();
        }

        if (random.Chance(EasyBlunder))
        {
            return ranked[random.Next(reachable)];
        }

        var choices = Math.Min(EasyChoices, reachable);
        var total = 0f;
        for (var choice = 0; choice < choices; choice++)
        {
            total += EasyWeights[choice];
        }

        var roll = random.NextFloat() * total;
        for (var choice = 0; choice < choices; choice++)
        {
            roll -= EasyWeights[choice];
            if (roll <= 0f)
            {
                return ranked[choice];
            }
        }

        return ranked[0];
    }

    private static int[] BuildPlacements(bool columns)
    {
        var result = new int[PlacementCount];
        var cursor = 0;
        for (var orientation = 0; orientation < GloopRules.Orientations; orientation++)
        {
            var satelliteStep = GloopRules.SatelliteColumn(orientation);
            for (var column = 0; column < GloopRules.Columns; column++)
            {
                var satelliteColumn = column + satelliteStep;
                if (satelliteColumn < 0 || satelliteColumn >= GloopRules.Columns)
                {
                    continue;
                }

                result[cursor++] = columns ? column : orientation;
            }
        }

        return result;
    }
}
