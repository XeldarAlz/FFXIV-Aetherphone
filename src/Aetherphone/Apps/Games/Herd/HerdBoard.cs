using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.World;
using Aetherphone.Core;

namespace Aetherphone.Apps.Games.Herd;

internal sealed class HerdBoard
{
    public const int CellsPerUnit = 8;
    public const float CellSize = 1f / CellsPerUnit;
    public const int Columns = HerdLevel.Columns * CellsPerUnit;
    public const int Rows = HerdLevel.Rows * CellsPerUnit;
    public const float WorldWidth = HerdLevel.Columns;
    public const float WorldHeight = HerdLevel.Rows;
    public const float TickSeconds = 1f / 12f;
    public const int TicksPerSecond = 12;
    public const int MaxMoogles = 64;
    public const int BodyCells = 7;
    public const int HalfWidthCells = 2;
    public const int StepUpCells = 3;
    public const int StepDownCells = 3;
    public const int FallCellsPerTick = 3;
    public const int SafeFallCells = 40;
    public const int FloatOpenCells = 8;
    public const int ClimbTicksPerCell = 2;
    public const int DigTicksPerRow = 2;
    public const int DigHalfWidth = 4;
    public const int BashTicksPerColumn = 2;
    public const int BashReach = 8;
    public const int BuildTicksPerBrick = 4;
    public const int BricksPerBridge = 12;
    public const int BrickLength = 6;
    public const int BlockerReach = 4;
    public const int BlockerHeight = 6;
    public const int ExitReach = 3;
    public const int ExitHeight = 8;
    public const int ExitTicks = 8;
    public const int SplatTicks = 8;
    public const int DrownTicks = 10;
    public const int FirstReleaseTicks = 8;
    public const int NukeBaseTicks = 10;
    public const int NukeStepTicks = 2;
    public const int OutOfWorldRows = Rows + 6;
    private const float MaxCatchUpSeconds = 0.25f;
    private const int EventCapacity = 192;

    private readonly TerrainMask terrain = new(Columns, Rows, CellSize);
    private readonly HerdMoogle[] moogles = new HerdMoogle[MaxMoogles];
    private readonly HerdEvent[] events = new HerdEvent[EventCapacity];
    private readonly bool[] water = new bool[HerdLevel.Columns * HerdLevel.Rows];
    private readonly int[] skillsLeft = new int[HerdLevel.SkillCount];
    private FixedStepClock clock = new(TickSeconds, MaxCatchUpSeconds);
    private GameRandom random;
    private int moogleCount;
    private int eventCount;
    private int releaseTimer;
    private int nukeOrder;
    private bool goalAnnounced;

    public TerrainMask Terrain => terrain;

    public HerdLevel? Level { get; private set; }

    public int Count { get; private set; }

    public int Required { get; private set; }

    public int Spawned { get; private set; }

    public int SavedCount { get; private set; }

    public int Lost { get; private set; }

    public int SkillsUsed { get; private set; }

    public int Ticks { get; private set; }

    public int TotalTicks { get; private set; }

    public bool Nuking { get; private set; }

    public bool Over { get; private set; }

    public bool TimeUp { get; private set; }

    public int DoorColumn { get; private set; }

    public int DoorRow { get; private set; }

    public int DoorDirection { get; private set; }

    public int ExitColumn { get; private set; }

    public int ExitRow { get; private set; }

    public int MoogleCount => moogleCount;

    public int EventCount => eventCount;

    public float Alpha => clock.Alpha;

    public int TicksLeft => Math.Max(0, TotalTicks - Ticks);

    public float SecondsLeft => TicksLeft * TickSeconds;

    public float TotalSeconds => TotalTicks * TickSeconds;

    public int Active
    {
        get
        {
            var active = 0;
            for (var index = 0; index < moogleCount; index++)
            {
                if (!moogles[index].Leaving)
                {
                    active++;
                }
            }

            return active;
        }
    }

    public bool GoalReached => SavedCount >= Required;

    public ref readonly HerdMoogle Moogle(int index) => ref moogles[index];

    public ref readonly HerdEvent Event(int index) => ref events[index];

    public int SkillsLeft(HerdSkill skill) => skillsLeft[(int)skill];

    public bool IsWater(int unitColumn, int unitRow) =>
        unitColumn >= 0 && unitColumn < HerdLevel.Columns && unitRow >= 0 && unitRow < HerdLevel.Rows &&
        water[unitRow * HerdLevel.Columns + unitColumn];

    public static int Stars(int saved, int required, int count)
    {
        if (saved < required || saved <= 0)
        {
            return 0;
        }

        if (saved >= count)
        {
            return 3;
        }

        return saved >= TwoStarTarget(required, count) ? 2 : 1;
    }

    public static int TwoStarTarget(int required, int count) => Math.Min(count, (required * 5 + 3) / 4);

    public static Vector2 CellPoint(float column, float row) => new(column * CellSize, row * CellSize);

    public Vector2 BodyCenter(int index, float alpha)
    {
        ref readonly var moogle = ref moogles[index];
        var column = moogle.PreviousColumn + (moogle.Column - moogle.PreviousColumn) * alpha;
        var row = moogle.PreviousRow + (moogle.Row - moogle.PreviousRow) * alpha;
        return CellPoint(column, row - BodyCells * 0.5f);
    }

    public Vector2 Feet(int index, float alpha)
    {
        ref readonly var moogle = ref moogles[index];
        var column = moogle.PreviousColumn + (moogle.Column - moogle.PreviousColumn) * alpha;
        var row = moogle.PreviousRow + (moogle.Row - moogle.PreviousRow) * alpha;
        return CellPoint(column, row);
    }

    public void Load(HerdLevel level, GameRandom seed)
    {
        Level = level;
        random = seed;
        terrain.Clear();
        Array.Clear(water);
        for (var row = 0; row < HerdLevel.Rows; row++)
        {
            for (var column = 0; column < HerdLevel.Columns; column++)
            {
                Stamp(level.At(column, row), column, row);
            }
        }

        Count = Math.Clamp(level.Count, 1, MaxMoogles);
        Required = Math.Clamp(level.Required, 1, Count);
        for (var skill = 0; skill < HerdLevel.SkillCount; skill++)
        {
            skillsLeft[skill] = level.SkillsFor((HerdSkill)skill);
        }

        DoorColumn = level.DoorX * CellsPerUnit + CellsPerUnit / 2;
        DoorRow = level.DoorY * CellsPerUnit + CellsPerUnit / 2;
        DoorDirection = level.DoorDirection;
        ExitColumn = level.ExitX * CellsPerUnit + CellsPerUnit / 2;
        ExitRow = (level.ExitY + 1) * CellsPerUnit;
        TotalTicks = level.Seconds * TicksPerSecond;
        moogleCount = 0;
        eventCount = 0;
        Spawned = 0;
        SavedCount = 0;
        Lost = 0;
        SkillsUsed = 0;
        Ticks = 0;
        Nuking = false;
        Over = false;
        TimeUp = false;
        releaseTimer = FirstReleaseTicks;
        nukeOrder = 0;
        goalAnnounced = false;
        clock.Reset();
    }

    public int Step(float deltaSeconds)
    {
        eventCount = 0;
        if (Over || Level is null)
        {
            return 0;
        }

        var ticks = clock.Advance(deltaSeconds);
        for (var tick = 0; tick < ticks && !Over; tick++)
        {
            Tick();
        }

        return ticks;
    }

    public void ClearEvents()
    {
        eventCount = 0;
    }

    public void Tick()
    {
        if (Over || Level is null)
        {
            return;
        }

        Ticks++;
        Release();
        for (var index = 0; index < moogleCount; index++)
        {
            ref var moogle = ref moogles[index];
            moogle.PreviousColumn = moogle.Column;
            moogle.PreviousRow = moogle.Row;
            if (moogle.Gone)
            {
                continue;
            }

            moogle.ActionTicks++;
            Advance(index);
        }

        if (!goalAnnounced && GoalReached)
        {
            goalAnnounced = true;
            Emit(HerdEventKind.GoalReached, -1, ExitColumn, ExitRow);
        }

        if (Ticks >= TotalTicks)
        {
            TimeUp = true;
            Over = true;
            return;
        }

        if ((Spawned >= Count || Nuking) && !AnyBusy())
        {
            Over = true;
        }
    }

    public bool CanAssign(int index, HerdSkill skill)
    {
        if (index < 0 || index >= moogleCount || skillsLeft[(int)skill] <= 0 || Over || Nuking)
        {
            return false;
        }

        ref readonly var moogle = ref moogles[index];
        if (moogle.Leaving)
        {
            return false;
        }

        return skill switch
        {
            HerdSkill.Climb => !moogle.Climber && moogle.Action != HerdAction.Blocking,
            HerdSkill.Float => !moogle.Floater && moogle.Action != HerdAction.Blocking,
            HerdSkill.Block => moogle.Action is HerdAction.Walking or HerdAction.Bashing or HerdAction.Building,
            HerdSkill.Dig => moogle.Action is HerdAction.Walking or HerdAction.Bashing or HerdAction.Building,
            HerdSkill.Bash => moogle.Action is HerdAction.Walking or HerdAction.Digging or HerdAction.Building,
            _ => moogle.Action is HerdAction.Walking or HerdAction.Digging or HerdAction.Bashing or
                HerdAction.Building,
        };
    }

    public bool Assign(int index, HerdSkill skill)
    {
        if (!CanAssign(index, skill))
        {
            return false;
        }

        ref var moogle = ref moogles[index];
        switch (skill)
        {
            case HerdSkill.Climb:
                moogle.Climber = true;
                break;
            case HerdSkill.Float:
                moogle.Floater = true;
                break;
            case HerdSkill.Block:
                Begin(ref moogle, HerdAction.Blocking);
                break;
            case HerdSkill.Dig:
                Begin(ref moogle, HerdAction.Digging);
                break;
            case HerdSkill.Bash:
                Begin(ref moogle, HerdAction.Bashing);
                break;
            default:
                Begin(ref moogle, HerdAction.Building);
                moogle.Bricks = BricksPerBridge;
                break;
        }

        skillsLeft[(int)skill]--;
        SkillsUsed++;
        Emit(HerdEventKind.Assigned, index, moogle.Column, moogle.Row);
        return true;
    }

    public int Pick(Vector2 world, float radius, HerdSkill skill, bool eligibleOnly)
    {
        var best = -1;
        var bestDistance = radius * radius;
        for (var index = 0; index < moogleCount; index++)
        {
            if (moogles[index].Leaving || (eligibleOnly && !CanAssign(index, skill)))
            {
                continue;
            }

            var distance = Vector2.DistanceSquared(BodyCenter(index, 1f), world);
            if (distance > bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            best = index;
        }

        return best;
    }

    public void Nuke()
    {
        if (Nuking || Over)
        {
            return;
        }

        Nuking = true;
        for (var index = 0; index < moogleCount; index++)
        {
            ref var moogle = ref moogles[index];
            if (moogle.Leaving)
            {
                continue;
            }

            Begin(ref moogle, HerdAction.Popping);
            moogle.Timer = (short)(NukeBaseTicks + nukeOrder * NukeStepTicks);
            nukeOrder++;
        }
    }

    public bool IsSolid(int column, int row) => terrain.IsSolid(column, row);

    private void Stamp(char cell, int unitColumn, int unitRow)
    {
        var left = unitColumn * CellsPerUnit;
        var top = unitRow * CellsPerUnit;
        var last = CellsPerUnit - 1;
        switch (cell)
        {
            case HerdLevel.Solid:
                FillCells(left, top, left + last, top + last);
                return;
            case HerdLevel.TopHalf:
                FillCells(left, top, left + last, top + CellsPerUnit / 2 - 1);
                return;
            case HerdLevel.BottomHalf:
                FillCells(left, top + CellsPerUnit / 2, left + last, top + last);
                return;
            case HerdLevel.Rising:
                for (var offset = 0; offset < CellsPerUnit; offset++)
                {
                    FillCells(left + offset, top + last - offset, left + offset, top + last);
                }

                return;
            case HerdLevel.Falling:
                for (var offset = 0; offset < CellsPerUnit; offset++)
                {
                    FillCells(left + offset, top + offset, left + offset, top + last);
                }

                return;
            case HerdLevel.Water:
                water[unitRow * HerdLevel.Columns + unitColumn] = true;
                return;
            default:
                return;
        }
    }

    private void FillCells(int firstColumn, int firstRow, int lastColumn, int lastRow)
    {
        terrain.FillRect(CellRect(firstColumn, firstRow, lastColumn, lastRow));
    }

    private int CarveCells(int firstColumn, int firstRow, int lastColumn, int lastRow)
    {
        return terrain.CarveRect(CellRect(firstColumn, firstRow, lastColumn, lastRow));
    }

    private static Rect CellRect(int firstColumn, int firstRow, int lastColumn, int lastRow)
    {
        var minColumn = Math.Min(firstColumn, lastColumn);
        var maxColumn = Math.Max(firstColumn, lastColumn);
        var minRow = Math.Min(firstRow, lastRow);
        var maxRow = Math.Max(firstRow, lastRow);
        return new Rect(CellPoint(minColumn, minRow), CellPoint(maxColumn + 1, maxRow + 1));
    }

    private void Release()
    {
        if (Nuking || Spawned >= Count || moogleCount >= MaxMoogles)
        {
            return;
        }

        releaseTimer--;
        if (releaseTimer > 0)
        {
            return;
        }

        releaseTimer = Math.Max(1, Level!.ReleaseTicks);
        ref var moogle = ref moogles[moogleCount];
        moogle = default;
        moogle.Column = (short)DoorColumn;
        moogle.Row = (short)DoorRow;
        moogle.PreviousColumn = moogle.Column;
        moogle.PreviousRow = moogle.Row;
        moogle.Direction = (sbyte)DoorDirection;
        moogle.Action = HerdAction.Falling;
        moogle.Variant = (byte)random.Next(256);
        Emit(HerdEventKind.Spawned, moogleCount, DoorColumn, DoorRow);
        moogleCount++;
        Spawned++;
    }

    private bool AnyBusy()
    {
        for (var index = 0; index < moogleCount; index++)
        {
            if (moogles[index].Busy)
            {
                return true;
            }
        }

        return false;
    }

    private static void Begin(ref HerdMoogle moogle, HerdAction action)
    {
        moogle.Action = action;
        moogle.Counter = 0;
        moogle.ActionTicks = 0;
    }

    private void Advance(int index)
    {
        ref var moogle = ref moogles[index];
        switch (moogle.Action)
        {
            case HerdAction.Falling:
            case HerdAction.Floating:
                Fall(index, ref moogle);
                break;
            case HerdAction.Walking:
                Walk(index, ref moogle);
                break;
            case HerdAction.Blocking:
                Block(ref moogle);
                break;
            case HerdAction.Digging:
                Dig(index, ref moogle);
                break;
            case HerdAction.Bashing:
                Bash(index, ref moogle);
                break;
            case HerdAction.Building:
                Build(index, ref moogle);
                break;
            case HerdAction.Climbing:
                Climb(ref moogle);
                break;
            case HerdAction.Exiting:
                Finish(ref moogle, HerdAction.Saved);
                return;
            case HerdAction.Splatting:
            case HerdAction.Drowning:
                Finish(ref moogle, HerdAction.Dead);
                return;
            case HerdAction.Popping:
                Pop(index, ref moogle);
                return;
            default:
                return;
        }

        CheckSurroundings(index, ref moogle);
    }

    private static void Finish(ref HerdMoogle moogle, HerdAction next)
    {
        moogle.Timer--;
        if (moogle.Timer > 0)
        {
            return;
        }

        moogle.Action = next;
    }

    private void Pop(int index, ref HerdMoogle moogle)
    {
        moogle.Timer--;
        if (moogle.Timer > 0)
        {
            return;
        }

        moogle.Action = HerdAction.Dead;
        Lost++;
        Emit(HerdEventKind.Popped, index, moogle.Column, moogle.Row);
    }

    private void CheckSurroundings(int index, ref HerdMoogle moogle)
    {
        if (moogle.Leaving)
        {
            return;
        }

        if (moogle.Row > OutOfWorldRows)
        {
            moogle.Action = HerdAction.Dead;
            Lost++;
            Emit(HerdEventKind.FellOut, index, moogle.Column, Rows);
            return;
        }

        var footColumn = moogle.Column / CellsPerUnit;
        var footRow = (moogle.Row - 1) / CellsPerUnit;
        if (moogle.Row > 0 && IsWater(footColumn, footRow))
        {
            moogle.Action = HerdAction.Drowning;
            moogle.Timer = DrownTicks;
            moogle.ActionTicks = 0;
            Lost++;
            Emit(HerdEventKind.Drowned, index, moogle.Column, moogle.Row);
            return;
        }

        if (moogle.Action is HerdAction.Digging or HerdAction.Climbing or HerdAction.Blocking || !InExit(moogle))
        {
            return;
        }

        moogle.Action = HerdAction.Exiting;
        moogle.Timer = ExitTicks;
        moogle.ActionTicks = 0;
        moogle.Column = (short)ExitColumn;
        SavedCount++;
        Emit(HerdEventKind.Saved, index, ExitColumn, ExitRow);
    }

    private bool InExit(in HerdMoogle moogle) =>
        Math.Abs(moogle.Column - ExitColumn) <= ExitReach && moogle.Row >= ExitRow - ExitHeight &&
        moogle.Row <= ExitRow + 1;

    private void Fall(int index, ref HerdMoogle moogle)
    {
        var speed = moogle.Action == HerdAction.Floating ? 1 : FallCellsPerTick;
        for (var step = 0; step < speed; step++)
        {
            if (Supported(moogle.Column, moogle.Row))
            {
                Land(index, ref moogle);
                return;
            }

            moogle.Row++;
            moogle.FallCells++;
            if (moogle.Floater && moogle.Action == HerdAction.Falling && moogle.FallCells >= FloatOpenCells)
            {
                moogle.Action = HerdAction.Floating;
                return;
            }

            if (moogle.Row > OutOfWorldRows)
            {
                return;
            }
        }
    }

    private void Land(int index, ref HerdMoogle moogle)
    {
        var fall = moogle.FallCells;
        moogle.FallCells = 0;
        if (fall > SafeFallCells && !moogle.Floater)
        {
            Begin(ref moogle, HerdAction.Splatting);
            moogle.Timer = SplatTicks;
            Lost++;
            Emit(HerdEventKind.Splat, index, moogle.Column, moogle.Row);
            return;
        }

        Begin(ref moogle, HerdAction.Walking);
        if (fall > CellsPerUnit)
        {
            Emit(HerdEventKind.Landed, index, moogle.Column, moogle.Row);
        }
    }

    private bool Supported(int column, int row) => terrain.IsSolid(column, row);

    private bool KeepFooting(ref HerdMoogle moogle)
    {
        if (Supported(moogle.Column, moogle.Row))
        {
            return true;
        }

        for (var drop = 1; drop <= StepDownCells; drop++)
        {
            if (Supported(moogle.Column, moogle.Row + drop))
            {
                moogle.Row = (short)(moogle.Row + drop);
                return true;
            }
        }

        Begin(ref moogle, HerdAction.Falling);
        moogle.FallCells = 0;
        return false;
    }

    private void Walk(int index, ref HerdMoogle moogle)
    {
        if (!KeepFooting(ref moogle))
        {
            return;
        }

        TurnAtBlockers(index, ref moogle);
        var next = moogle.Column + moogle.Direction;
        if (next < HalfWidthCells || next > Columns - HalfWidthCells)
        {
            moogle.Direction = (sbyte)-moogle.Direction;
            return;
        }

        var row = moogle.Row;
        if (terrain.IsSolid(next, row - 1))
        {
            for (var rise = 1; rise <= StepUpCells; rise++)
            {
                if (terrain.IsSolid(next, row - 1 - rise))
                {
                    continue;
                }

                moogle.Column = (short)next;
                moogle.Row = (short)(row - rise);
                return;
            }

            if (moogle.Climber)
            {
                Begin(ref moogle, HerdAction.Climbing);
                return;
            }

            moogle.Direction = (sbyte)-moogle.Direction;
            return;
        }

        moogle.Column = (short)next;
        KeepFooting(ref moogle);
    }

    private void TurnAtBlockers(int index, ref HerdMoogle moogle)
    {
        for (var other = 0; other < moogleCount; other++)
        {
            if (other == index || moogles[other].Action != HerdAction.Blocking)
            {
                continue;
            }

            ref readonly var blocker = ref moogles[other];
            var offset = blocker.Column - moogle.Column;
            if (offset == 0 || Math.Abs(offset) > BlockerReach || Math.Abs(blocker.Row - moogle.Row) > BlockerHeight)
            {
                continue;
            }

            if (Math.Sign(offset) == moogle.Direction)
            {
                moogle.Direction = (sbyte)-moogle.Direction;
                return;
            }
        }
    }

    private void Block(ref HerdMoogle moogle)
    {
        KeepFooting(ref moogle);
    }

    private void Dig(int index, ref HerdMoogle moogle)
    {
        moogle.Counter++;
        if (moogle.Counter < DigTicksPerRow)
        {
            return;
        }

        moogle.Counter = 0;
        var row = moogle.Row;
        CarveCells(moogle.Column - DigHalfWidth, row, moogle.Column + DigHalfWidth - 1, row);
        moogle.Row++;
        Emit(HerdEventKind.Dug, index, moogle.Column, row);
        for (var column = moogle.Column - DigHalfWidth; column < moogle.Column + DigHalfWidth; column++)
        {
            if (terrain.IsSolid(column, moogle.Row))
            {
                return;
            }
        }

        Begin(ref moogle, HerdAction.Falling);
        moogle.FallCells = 0;
    }

    private void Bash(int index, ref HerdMoogle moogle)
    {
        if (!KeepFooting(ref moogle))
        {
            return;
        }

        moogle.Counter++;
        if (moogle.Counter < BashTicksPerColumn)
        {
            return;
        }

        moogle.Counter = 0;
        var direction = moogle.Direction;
        var row = moogle.Row;
        if (!SolidAhead(moogle.Column, row, direction, 1, BashReach))
        {
            Begin(ref moogle, HerdAction.Walking);
            return;
        }

        var carved = CarveCells(moogle.Column + direction, row - BodyCells,
            moogle.Column + direction * (HalfWidthCells + 1), row - 1);
        if (carved > 0)
        {
            Emit(HerdEventKind.Bashed, index, moogle.Column + direction * (HalfWidthCells + 1), row - BodyCells / 2);
        }

        var next = moogle.Column + direction;
        if (next < HalfWidthCells || next > Columns - HalfWidthCells)
        {
            moogle.Direction = (sbyte)-direction;
            Begin(ref moogle, HerdAction.Walking);
            return;
        }

        moogle.Column = (short)next;
        KeepFooting(ref moogle);
    }

    private bool SolidAhead(int column, int row, int direction, int nearest, int farthest)
    {
        for (var reach = nearest; reach <= farthest; reach++)
        {
            var probe = column + direction * reach;
            for (var height = 1; height <= BodyCells; height++)
            {
                if (terrain.IsSolid(probe, row - height))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void Build(int index, ref HerdMoogle moogle)
    {
        if (!KeepFooting(ref moogle))
        {
            return;
        }

        moogle.Counter++;
        if (moogle.Counter < BuildTicksPerBrick)
        {
            return;
        }

        moogle.Counter = 0;
        var direction = moogle.Direction;
        var row = moogle.Row;
        var column = moogle.Column;
        if (Obstructed(column, row, direction) || column + direction * (BrickLength - 1) < 0 ||
            column + direction * (BrickLength - 1) >= Columns || row - 1 <= 0)
        {
            moogle.Direction = (sbyte)-direction;
            Begin(ref moogle, HerdAction.Walking);
            Emit(HerdEventKind.BridgeDone, index, column, row);
            return;
        }

        FillCells(column, row - 1, column + direction * (BrickLength - 1), row - 1);
        moogle.Column = (short)(column + direction * 2);
        moogle.Row = (short)(row - 1);
        moogle.Bricks--;
        Emit(HerdEventKind.Brick, index, column + direction * (BrickLength / 2), row - 1);
        if (moogle.Bricks > 0)
        {
            return;
        }

        Begin(ref moogle, HerdAction.Walking);
        Emit(HerdEventKind.BridgeDone, index, moogle.Column, moogle.Row);
    }

    private bool Obstructed(int column, int row, int direction)
    {
        for (var reach = 1; reach <= 2; reach++)
        {
            for (var height = 2; height <= BodyCells + 1; height++)
            {
                if (terrain.IsSolid(column + direction * reach, row - height))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void Climb(ref HerdMoogle moogle)
    {
        moogle.Counter++;
        if (moogle.Counter < ClimbTicksPerCell)
        {
            return;
        }

        moogle.Counter = 0;
        if (terrain.IsSolid(moogle.Column, moogle.Row - BodyCells - 1))
        {
            moogle.Direction = (sbyte)-moogle.Direction;
            Begin(ref moogle, HerdAction.Falling);
            moogle.FallCells = 0;
            return;
        }

        moogle.Row--;
        var wall = moogle.Column + moogle.Direction;
        if (terrain.IsSolid(wall, moogle.Row - 1))
        {
            return;
        }

        if (!terrain.IsSolid(wall, moogle.Row))
        {
            Begin(ref moogle, HerdAction.Falling);
            moogle.FallCells = 0;
            return;
        }

        moogle.Column = (short)wall;
        Begin(ref moogle, HerdAction.Walking);
    }

    private void Emit(HerdEventKind kind, int moogle, int column, int row)
    {
        if (eventCount >= EventCapacity)
        {
            return;
        }

        events[eventCount++] = new HerdEvent(kind, moogle, column, row);
    }
}
