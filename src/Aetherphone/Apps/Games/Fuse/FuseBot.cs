using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Fuse;

internal sealed class FuseBot
{
    public const float Never = float.MaxValue;
    public const float FleeMargin = 0.05f;
    public const float HardBombMargin = 0.35f;
    public const float EasyBombMargin = 0.1f;
    public const float EasyReaction = 0.45f;
    public const float HardReplan = 0.2f;
    public const float EasyReplan = 0.7f;
    public const float FallHorizon = 2.5f;
    private const float RejectSeconds = 1.5f;
    private const float EasyBombChance = 0.7f;
    private const float EasyWildChance = 0.25f;
    private const float EasyNoise = 3f;
    private const int EasyWildReach = 3;
    private const int WanderReach = 5;
    private const int MaxFalls = 16;
    private const float HardItemValue = 9f;
    private const float EasyItemValue = 5f;
    private const float CrateValue = 2f;
    private const float HardEnemyValue = 6f;
    private const float EasyEnemyValue = 3f;
    private const float ItemPenalty = 3f;
    private const float TrapValue = 10f;
    private const int HardRoom = 3;
    private const int HardEscapeSteps = 4;
    private const int AnySteps = FuseBoard.CellCount;
    private const int RoomCapacity = 17;

    private struct Mind
    {
        public int Target;
        public int Rejected;
        public float RejectTimer;
        public float Replan;
        public float Hesitation;
        public bool Alarmed;
    }

    private readonly float[] danger = new float[FuseBoard.CellCount];
    private readonly float[] hypothetical = new float[FuseBoard.CellCount];
    private readonly float[] times = new float[FuseBoard.BombCapacity + 1];
    private readonly bool[] exploded = new bool[FuseBoard.BombCapacity + 1];
    private readonly int[] brokenAt = new int[FuseBoard.CellCount];
    private readonly int[] blast = new int[FuseBoard.MaxBlastCells];
    private readonly int[] reachQueue = new int[FuseBoard.CellCount];
    private readonly int[] reachSteps = new int[FuseBoard.CellCount];
    private readonly FuseDirection[] reachFirst = new FuseDirection[FuseBoard.CellCount];
    private readonly int[] reachSeen = new int[FuseBoard.CellCount];
    private readonly int[] fleeQueue = new int[FuseBoard.CellCount];
    private readonly int[] fleeSteps = new int[FuseBoard.CellCount];
    private readonly FuseDirection[] fleeFirst = new FuseDirection[FuseBoard.CellCount];
    private readonly int[] fleeSeen = new int[FuseBoard.CellCount];
    private readonly int[] fallCells = new int[MaxFalls];
    private readonly float[] fallTimes = new float[MaxFalls];
    private readonly Mind[] minds = new Mind[FuseBoard.MaxMoogles];
    private int crateStamp;
    private int reachStamp;
    private int fleeStamp;
    private int reachCount;

    public float Danger(int cell) => danger[cell];

    public bool IsSafe(FuseBoard board, int cell) => Safe(board, danger, cell);

    public void Reset()
    {
        for (var index = 0; index < minds.Length; index++)
        {
            minds[index] = new Mind { Target = -1, Rejected = -1 };
        }
    }

    public void Prepare(FuseBoard board)
    {
        ComputeDanger(board, danger, -1, 0);
    }

    public MoogleInput Decide(FuseBoard board, int index, FuseSkill skill, ref GameRandom random, float tickSeconds)
    {
        ref var mind = ref minds[index];
        ref readonly var moogle = ref board.MoogleAt(index);
        var cell = FuseBoard.CellAt(moogle.Position);
        mind.RejectTimer = MathF.Max(0f, mind.RejectTimer - tickSeconds);
        if (!Safe(board, danger, cell))
        {
            mind.Target = -1;
            if (skill == FuseSkill.Easy && !mind.Alarmed)
            {
                mind.Hesitation += tickSeconds;
                if (mind.Hesitation < EasyReaction)
                {
                    return default;
                }

                mind.Alarmed = true;
            }

            return new MoogleInput(Escape(board, index, danger, FleeMargin, out _), false);
        }

        mind.Hesitation = 0f;
        mind.Alarmed = false;
        mind.Replan -= tickSeconds;
        Reach(board, cell);
        if (mind.Replan <= 0f || mind.Target < 0 || reachSeen[mind.Target] != reachStamp)
        {
            mind.Target = ChooseTarget(board, index, skill, ref random, ref mind);
            mind.Replan = skill == FuseSkill.Hard ? HardReplan : EasyReplan + random.NextFloat() * EasyReplan;
        }

        if (mind.Target == cell)
        {
            if (ShouldBomb(board, index, skill, ref random, cell))
            {
                mind.Target = -1;
                mind.Replan = 0f;
                mind.Alarmed = true;
                return new MoogleInput(FuseDirection.None, true);
            }

            mind.Rejected = cell;
            mind.RejectTimer = RejectSeconds;
            mind.Target = Wander(cell, ref random);
        }

        if (mind.Target < 0 || mind.Target == cell)
        {
            return default;
        }

        return new MoogleInput(reachFirst[mind.Target], false);
    }

    public FuseDirection Escape(FuseBoard board, int index, float[] map, float margin, out bool found)
    {
        var direction = Flee(board, index, map, margin, true, 1, AnySteps, out found);
        return found ? direction : Flee(board, index, map, margin, false, 1, AnySteps, out _);
    }

    public bool CanEscape(FuseBoard board, int index, float[] map, float margin, int room = 1, int maxSteps = AnySteps)
    {
        Flee(board, index, map, margin, true, room, maxSteps, out var found);
        return found;
    }

    public void ComputeDanger(FuseBoard board, float[] output, int extraCell, int extraRange, bool extraOnly = false)
    {
        var extraSlot = FuseBoard.BombCapacity;
        for (var slot = 0; slot < FuseBoard.BombCapacity; slot++)
        {
            ref readonly var bomb = ref board.BombAt(slot);
            var counted = bomb.Alive && !extraOnly;
            times[slot] = counted ? MathF.Max(0f, bomb.Fuse) : Never;
            exploded[slot] = !counted;
        }

        times[extraSlot] = extraCell >= 0 ? FuseBoard.FuseSeconds : Never;
        exploded[extraSlot] = extraCell < 0;
        crateStamp++;
        Array.Fill(output, Never);
        while (true)
        {
            var next = -1;
            var nextTime = Never;
            for (var slot = 0; slot <= extraSlot; slot++)
            {
                if (exploded[slot] || times[slot] >= nextTime)
                {
                    continue;
                }

                next = slot;
                nextTime = times[slot];
            }

            if (next < 0)
            {
                break;
            }

            exploded[next] = true;
            var origin = next == extraSlot ? extraCell : board.BombAt(next).Cell;
            var range = next == extraSlot ? extraRange : board.BombAt(next).Range;
            output[origin] = MathF.Min(output[origin], nextTime);
            var directions = FuseBoard.Directions;
            for (var directionIndex = 0; directionIndex < directions.Length; directionIndex++)
            {
                Spread(board, output, origin, directions[directionIndex], range, nextTime, extraCell);
            }
        }

        var falls = board.UpcomingFalls(fallCells, fallTimes);
        for (var fall = 0; fall < falls; fall++)
        {
            if (fallTimes[fall] > FallHorizon)
            {
                break;
            }

            var target = fallCells[fall];
            output[target] = MathF.Min(output[target], MathF.Max(0f, fallTimes[fall]));
        }
    }

    private void Spread(FuseBoard board, float[] output, int origin, FuseDirection direction, int range, float time,
        int extraCell)
    {
        var cell = origin;
        for (var step = 1; step <= range; step++)
        {
            cell = FuseBoard.Neighbour(cell, direction);
            if (cell < 0)
            {
                return;
            }

            var tile = board.Tile(cell);
            if (tile is FuseTile.Pillar or FuseTile.Block)
            {
                return;
            }

            output[cell] = MathF.Min(output[cell], time);
            if (tile == FuseTile.Crate && brokenAt[cell] != crateStamp)
            {
                brokenAt[cell] = crateStamp;
                return;
            }

            var other = cell == extraCell ? FuseBoard.BombCapacity : board.BombIndexAt(cell);
            if (other < 0 || exploded[other])
            {
                continue;
            }

            times[other] = MathF.Min(times[other], time);
            return;
        }
    }

    private static bool Safe(FuseBoard board, float[] map, int cell) => map[cell] == Never && board.FireAt(cell) <= 0f;

    private static bool Passable(FuseBoard board, int cell) => board.Walkable(cell) && board.FireAt(cell) <= 0f;

    private FuseDirection Flee(FuseBoard board, int index, float[] map, float margin, bool strict, int room,
        int maxSteps, out bool found)
    {
        found = false;
        ref readonly var moogle = ref board.MoogleAt(index);
        var start = FuseBoard.CellAt(moogle.Position);
        var stepSeconds = 1f / board.SpeedOf(index);
        var startDelay = Vector2.Distance(moogle.Position, FuseBoard.CellCenter(start)) * stepSeconds;
        fleeStamp++;
        var head = 0;
        var tail = 0;
        fleeSeen[start] = fleeStamp;
        fleeSteps[start] = 0;
        fleeFirst[start] = FuseDirection.None;
        fleeQueue[tail++] = start;
        var fallback = FuseDirection.None;
        var fallbackTime = map[start];
        while (head < tail)
        {
            var cell = fleeQueue[head++];
            if (strict && fleeSteps[cell] > maxSteps)
            {
                break;
            }

            if (cell != start && Safe(board, map, cell) && (room <= 1 || Room(board, map, cell) >= room))
            {
                found = true;
                return fleeFirst[cell];
            }

            if (!strict && cell != start && map[cell] > fallbackTime)
            {
                fallbackTime = map[cell];
                fallback = fleeFirst[cell];
            }

            var directions = FuseBoard.Directions;
            for (var directionIndex = 0; directionIndex < directions.Length; directionIndex++)
            {
                var direction = directions[directionIndex];
                var next = FuseBoard.Neighbour(cell, direction);
                if (next < 0 || fleeSeen[next] == fleeStamp || !Passable(board, next))
                {
                    continue;
                }

                var arrival = startDelay + (fleeSteps[cell] + 1) * stepSeconds;
                if (strict && map[next] != Never && map[next] <= arrival + stepSeconds * 0.5f + margin)
                {
                    continue;
                }

                fleeSeen[next] = fleeStamp;
                fleeSteps[next] = fleeSteps[cell] + 1;
                fleeFirst[next] = cell == start ? direction : fleeFirst[cell];
                fleeQueue[tail++] = next;
            }
        }

        return fallback;
    }

    private void Reach(FuseBoard board, int start)
    {
        reachStamp++;
        var head = 0;
        var tail = 0;
        reachSeen[start] = reachStamp;
        reachSteps[start] = 0;
        reachFirst[start] = FuseDirection.None;
        reachQueue[tail++] = start;
        var directions = FuseBoard.Directions;
        while (head < tail)
        {
            var cell = reachQueue[head++];
            for (var directionIndex = 0; directionIndex < directions.Length; directionIndex++)
            {
                var direction = directions[directionIndex];
                var next = FuseBoard.Neighbour(cell, direction);
                if (next < 0 || reachSeen[next] == reachStamp || !Passable(board, next) || !Safe(board, danger, next))
                {
                    continue;
                }

                reachSeen[next] = reachStamp;
                reachSteps[next] = reachSteps[cell] + 1;
                reachFirst[next] = cell == start ? direction : reachFirst[cell];
                reachQueue[tail++] = next;
            }
        }

        reachCount = tail;
    }

    private int ChooseTarget(FuseBoard board, int index, FuseSkill skill, ref GameRandom random, ref Mind mind)
    {
        ref readonly var moogle = ref board.MoogleAt(index);
        var canBomb = moogle.BombsOut < moogle.Bombs;
        var hard = skill == FuseSkill.Hard;
        var best = -1;
        var bestScore = 0f;
        for (var entry = 0; entry < reachCount; entry++)
        {
            var cell = reachQueue[entry];
            if (cell == mind.Rejected && mind.RejectTimer > 0f)
            {
                continue;
            }

            var value = 0f;
            if (board.ItemAt(cell) != PowerUp.None)
            {
                value = hard ? HardItemValue : EasyItemValue;
            }

            if (canBomb)
            {
                var bombValue = BombValue(board, index, cell, hard, out var enemies);
                if (hard && enemies > 0)
                {
                    bombValue += TrapBonus(board, index, cell);
                }

                value = MathF.Max(value, bombValue);
            }

            if (value <= 0f)
            {
                continue;
            }

            var score = value - reachSteps[cell] + (hard ? 0f : random.NextFloat() * EasyNoise);
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            best = cell;
        }

        if (best >= 0)
        {
            return best;
        }

        return hard ? Hunt(board, index) : Wander(FuseBoard.CellAt(moogle.Position), ref random);
    }

    private float BombValue(FuseBoard board, int index, int cell, bool hard, out int enemies)
    {
        enemies = 0;
        ref readonly var moogle = ref board.MoogleAt(index);
        if (board.BombIndexAt(cell) >= 0)
        {
            return 0f;
        }

        var count = board.BlastCells(cell, moogle.Range, blast);
        var crates = 0;
        var items = 0;
        for (var entry = 0; entry < count; entry++)
        {
            var target = blast[entry];
            if (board.Tile(target) == FuseTile.Crate && danger[target] == Never)
            {
                crates++;
            }

            if (entry > 0 && board.ItemAt(target) != PowerUp.None)
            {
                items++;
            }
        }

        enemies = EnemiesIn(board, index, count);
        return crates * CrateValue + enemies * (hard ? HardEnemyValue : EasyEnemyValue) - items * ItemPenalty;
    }

    private int EnemiesIn(FuseBoard board, int index, int count)
    {
        var enemies = 0;
        for (var other = 0; other < FuseBoard.MaxMoogles; other++)
        {
            ref readonly var rival = ref board.MoogleAt(other);
            if (other == index || !rival.Alive)
            {
                continue;
            }

            var rivalCell = FuseBoard.CellAt(rival.Position);
            for (var entry = 0; entry < count; entry++)
            {
                if (blast[entry] != rivalCell)
                {
                    continue;
                }

                enemies++;
                break;
            }
        }

        return enemies;
    }

    private bool ShouldBomb(FuseBoard board, int index, FuseSkill skill, ref GameRandom random, int cell)
    {
        ref readonly var moogle = ref board.MoogleAt(index);
        if (moogle.BombsOut >= moogle.Bombs || board.BombIndexAt(cell) >= 0)
        {
            return false;
        }

        var hard = skill == FuseSkill.Hard;
        var value = BombValue(board, index, cell, hard, out _);
        ComputeDanger(board, hypothetical, cell, moogle.Range);
        var trapped = hard ? TrappedEnemies(board, index) : 0;
        if (value <= 0f && trapped == 0)
        {
            if (hard || !random.Chance(EasyWildChance) || !EnemyNear(board, index, cell))
            {
                return false;
            }
        }
        else if (!hard && !random.Chance(EasyBombChance))
        {
            return false;
        }

        if (!hard)
        {
            ComputeDanger(board, hypothetical, cell, moogle.Range, true);
        }

        return hard
            ? CanEscape(board, index, hypothetical, HardBombMargin, HardRoom, HardEscapeSteps)
            : CanEscape(board, index, hypothetical, EasyBombMargin);
    }

    private static int Room(FuseBoard board, float[] map, int goal)
    {
        Span<int> cells = stackalloc int[RoomCapacity];
        var count = 0;
        cells[count++] = goal;
        var directions = FuseBoard.Directions;
        for (var first = 0; first < directions.Length; first++)
        {
            var near = FuseBoard.Neighbour(goal, directions[first]);
            if (near < 0 || !Passable(board, near) || !Safe(board, map, near))
            {
                continue;
            }

            count = AddRoom(cells, count, near);
            for (var second = 0; second < directions.Length; second++)
            {
                var far = FuseBoard.Neighbour(near, directions[second]);
                if (far >= 0 && Passable(board, far) && Safe(board, map, far))
                {
                    count = AddRoom(cells, count, far);
                }
            }
        }

        return count;
    }

    private static int AddRoom(Span<int> cells, int count, int cell)
    {
        for (var index = 0; index < count; index++)
        {
            if (cells[index] == cell)
            {
                return count;
            }
        }

        if (count >= cells.Length)
        {
            return count;
        }

        cells[count] = cell;
        return count + 1;
    }

    private float TrapBonus(FuseBoard board, int index, int cell)
    {
        ComputeDanger(board, hypothetical, cell, board.MoogleAt(index).Range);
        return TrappedEnemies(board, index) * TrapValue;
    }

    private int TrappedEnemies(FuseBoard board, int index)
    {
        var trapped = 0;
        for (var other = 0; other < FuseBoard.MaxMoogles; other++)
        {
            ref readonly var rival = ref board.MoogleAt(other);
            if (other == index || !rival.Alive)
            {
                continue;
            }

            var rivalCell = FuseBoard.CellAt(rival.Position);
            if (hypothetical[rivalCell] == Never || danger[rivalCell] != Never)
            {
                continue;
            }

            if (!CanEscape(board, other, hypothetical, 0f))
            {
                trapped++;
            }
        }

        return trapped;
    }

    private static bool EnemyNear(FuseBoard board, int index, int cell)
    {
        var column = FuseBoard.ColumnOf(cell);
        var row = FuseBoard.RowOf(cell);
        for (var other = 0; other < FuseBoard.MaxMoogles; other++)
        {
            ref readonly var rival = ref board.MoogleAt(other);
            if (other == index || !rival.Alive)
            {
                continue;
            }

            var rivalCell = FuseBoard.CellAt(rival.Position);
            var distance = Math.Abs(FuseBoard.ColumnOf(rivalCell) - column) + Math.Abs(FuseBoard.RowOf(rivalCell) - row);
            if (distance <= EasyWildReach)
            {
                return true;
            }
        }

        return false;
    }

    private int Hunt(FuseBoard board, int index)
    {
        ref readonly var moogle = ref board.MoogleAt(index);
        var self = FuseBoard.CellAt(moogle.Position);
        var quarry = -1;
        var quarryDistance = int.MaxValue;
        for (var other = 0; other < FuseBoard.MaxMoogles; other++)
        {
            ref readonly var rival = ref board.MoogleAt(other);
            if (other == index || !rival.Alive)
            {
                continue;
            }

            var rivalCell = FuseBoard.CellAt(rival.Position);
            var distance = Manhattan(self, rivalCell);
            if (distance >= quarryDistance)
            {
                continue;
            }

            quarryDistance = distance;
            quarry = rivalCell;
        }

        if (quarry < 0)
        {
            return -1;
        }

        var best = -1;
        var bestDistance = int.MaxValue;
        for (var entry = 0; entry < reachCount; entry++)
        {
            var cell = reachQueue[entry];
            var distance = Manhattan(cell, quarry) * 4 + reachSteps[cell];
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            best = cell;
        }

        return best;
    }

    private int Wander(int start, ref GameRandom random)
    {
        var options = 0;
        for (var entry = 0; entry < reachCount; entry++)
        {
            var cell = reachQueue[entry];
            if (cell != start && reachSteps[cell] <= WanderReach)
            {
                options++;
            }
        }

        if (options == 0)
        {
            return -1;
        }

        var pick = random.Next(options);
        for (var entry = 0; entry < reachCount; entry++)
        {
            var cell = reachQueue[entry];
            if (cell == start || reachSteps[cell] > WanderReach)
            {
                continue;
            }

            if (pick == 0)
            {
                return cell;
            }

            pick--;
        }

        return -1;
    }

    private static int Manhattan(int first, int second) =>
        Math.Abs(FuseBoard.ColumnOf(first) - FuseBoard.ColumnOf(second)) +
        Math.Abs(FuseBoard.RowOf(first) - FuseBoard.RowOf(second));
}
