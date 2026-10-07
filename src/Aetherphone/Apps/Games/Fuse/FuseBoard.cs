using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Fuse;

internal sealed class FuseBoard
{
    public const int Columns = 13;
    public const int Rows = 11;
    public const int CellCount = Columns * Rows;
    public const int MaxMoogles = 4;
    public const int Player = 0;
    public const int MaxBombsEach = 8;
    public const int BombCapacity = MaxMoogles * MaxBombsEach;
    public const int MaxRange = 8;
    public const int MaxSpeedLevel = 4;
    public const int StartBombs = 1;
    public const int StartRange = 2;
    public const int MaxBlastCells = 1 + 4 * MaxRange;
    public const int WinsNeeded = 2;
    public const int MaxRounds = 5;
    public const int NoMoogle = -1;
    public const byte ArmUp = 1;
    public const byte ArmRight = 2;
    public const byte ArmDown = 4;
    public const byte ArmLeft = 8;
    public const byte ArmCore = 16;
    public const float FuseSeconds = 2f;
    public const float FireSeconds = 0.6f;
    public const float SlideSpeed = 8f;
    public const float BaseSpeed = 3.4f;
    public const float SpeedStep = 0.55f;
    public const float RoundSeconds = 120f;
    public const float SuddenDeathSeconds = 30f;
    public const float SuddenDeathStart = RoundSeconds - SuddenDeathSeconds;
    public const float FallInterval = 0.25f;
    public const float FallWarning = 0.6f;
    public const float ReadySeconds = 1.5f;
    public const float EndSeconds = 2.4f;
    public const float SettleSeconds = 0.5f;
    public const float GhostSeconds = 1.4f;
    public const float TickSeconds = 1f / 120f;
    public const float CrateChance = 0.72f;
    public const float DropChance = 0.34f;
    private const float MaxCatchUpSeconds = 0.1f;
    private const float AlignEpsilon = 0.001f;
    private const float CornerAssist = 0.22f;
    private const float KickReach = 0.12f;
    private const float StrideRate = 7.5f;
    private const float BasePulseHertz = 1.6f;
    private const float PulseRampHertz = 6.4f;
    private const int StartZone = 2;
    private static readonly FuseDirection[] DirectionOrder =
    {
        FuseDirection.Up, FuseDirection.Right, FuseDirection.Down, FuseDirection.Left,
    };

    private static readonly int[] CornerColumns = { 0, Columns - 1, 0, Columns - 1 };
    private static readonly int[] CornerRows = { Rows - 1, 0, 0, Rows - 1 };
    private static readonly FuseDirection[] CornerFacing =
    {
        FuseDirection.Up, FuseDirection.Down, FuseDirection.Down, FuseDirection.Up,
    };

    private static readonly int[] SpiralOrder = BuildSpiral();

    private readonly FuseTile[] tiles = new FuseTile[CellCount];
    private readonly PowerUp[] hidden = new PowerUp[CellCount];
    private readonly PowerUp[] items = new PowerUp[CellCount];
    private readonly PowerUp[] pending = new PowerUp[CellCount];
    private readonly float[] fire = new float[CellCount];
    private readonly byte[] fireArms = new byte[CellCount];
    private readonly int[] fireOwner = new int[CellCount];
    private readonly int[] bombAt = new int[CellCount];
    private readonly Bomb[] bombs = new Bomb[BombCapacity];
    private readonly bool[] queued = new bool[BombCapacity];
    private readonly int[] detonations = new int[BombCapacity];
    private readonly int[] detonationChains = new int[BombCapacity];
    private readonly Moogle[] moogles = new Moogle[MaxMoogles];
    private readonly MoogleInput[] inputs = new MoogleInput[MaxMoogles];
    private readonly Blast[] blasts = new Blast[BombCapacity];
    private readonly Knockout[] knockouts = new Knockout[MaxMoogles];
    private readonly Pickup[] pickups = new Pickup[MaxMoogles * 2];
    private readonly FuseBot bot = new();
    private GameRandom random;
    private FixedStepClock clock = new(TickSeconds, MaxCatchUpSeconds);
    private FuseSkill skill;
    private FuseDirection playerDirection;
    private float phaseTimer;
    private float settle;
    private int fallCursor;
    private int fallsLanded;
    private int detonationCount;
    private int fightTicks;
    private bool playerBomb;
    private bool autopilot;
    private bool sandbox;

    public FuseBoard()
    {
        Array.Fill(bombAt, -1);
        Array.Fill(fireOwner, NoMoogle);
    }

    public FuseEventList BrokenCrates { get; } = new(64);

    public FuseEventList RevealedItems { get; } = new(32);

    public FuseEventList BurnedItems { get; } = new(32);

    public FuseEventList LandedBlocks { get; } = new(16);

    public FuseEventList PlacedBombs { get; } = new(16);

    public FuseEventList KickedBombs { get; } = new(16);

    public FuseEventList BombTicks { get; } = new(32);

    public FuseBot Bot => bot;

    public FusePhase Phase { get; private set; }

    public FuseVerdict Verdict { get; private set; }

    public int Round { get; private set; }

    public float Elapsed { get; private set; }

    public float TimeLeft => MathF.Max(0f, RoundSeconds - Elapsed);

    public bool SuddenDeath { get; private set; }

    public int LastWinner { get; private set; } = NoMoogle;

    public int PlayerKnockouts { get; private set; }

    public int PlayerCrates { get; private set; }

    public int PlayerPowerUps { get; private set; }

    public int BlastCount { get; private set; }

    public int KnockoutCount { get; private set; }

    public int PickupCount { get; private set; }

    public bool RoundStartedThisFrame { get; private set; }

    public bool FightStartedThisFrame { get; private set; }

    public bool RoundEndedThisFrame { get; private set; }

    public bool MatchEndedThisFrame { get; private set; }

    public bool SuddenDeathStartedThisFrame { get; private set; }

    public bool PlayerAlive => moogles[Player].Alive;

    public bool MatchPoint
    {
        get
        {
            for (var index = 0; index < MaxMoogles; index++)
            {
                if (moogles[index].Wins == WinsNeeded - 1)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public int AliveCount
    {
        get
        {
            var count = 0;
            for (var index = 0; index < MaxMoogles; index++)
            {
                if (moogles[index].Alive)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public static ReadOnlySpan<int> Spiral => SpiralOrder;

    public static ReadOnlySpan<FuseDirection> Directions => DirectionOrder;

    public FuseTile Tile(int cell) => tiles[cell];

    public FuseTile Tile(int column, int row) => tiles[CellIndex(column, row)];

    public PowerUp ItemAt(int cell) => items[cell];

    public PowerUp HiddenAt(int cell) => hidden[cell];

    public float FireAt(int cell) => fire[cell];

    public byte FireArmsAt(int cell) => fireArms[cell];

    public int BombIndexAt(int cell) => bombAt[cell];

    public ref readonly Bomb BombAt(int slot) => ref bombs[slot];

    public ref readonly Moogle MoogleAt(int index) => ref moogles[index];

    public Blast BlastAt(int index) => blasts[index];

    public Knockout KnockoutAt(int index) => knockouts[index];

    public Pickup PickupAt(int index) => pickups[index];

    public float SpeedOf(int index) => BaseSpeed + SpeedStep * moogles[index].SpeedLevel;

    public FuseSkill SkillOf(int index) => index == Player ? FuseSkill.Hard : skill;

    public static int CellIndex(int column, int row) => row * Columns + column;

    public static int ColumnOf(int cell) => cell % Columns;

    public static int RowOf(int cell) => cell / Columns;

    public static bool InArena(int column, int row) => column >= 0 && column < Columns && row >= 0 && row < Rows;

    public static Vector2 CellCenter(int cell) => new(ColumnOf(cell) + 0.5f, RowOf(cell) + 0.5f);

    public static int CellAt(Vector2 position)
    {
        var column = Math.Clamp((int)MathF.Floor(position.X), 0, Columns - 1);
        var row = Math.Clamp((int)MathF.Floor(position.Y), 0, Rows - 1);
        return CellIndex(column, row);
    }

    public static int StepX(FuseDirection direction) => direction switch
    {
        FuseDirection.Right => 1,
        FuseDirection.Left => -1,
        _ => 0,
    };

    public static int StepY(FuseDirection direction) => direction switch
    {
        FuseDirection.Down => 1,
        FuseDirection.Up => -1,
        _ => 0,
    };

    public static Vector2 Vector(FuseDirection direction) => new(StepX(direction), StepY(direction));

    public static FuseDirection Opposite(FuseDirection direction) => direction switch
    {
        FuseDirection.Up => FuseDirection.Down,
        FuseDirection.Down => FuseDirection.Up,
        FuseDirection.Left => FuseDirection.Right,
        FuseDirection.Right => FuseDirection.Left,
        _ => FuseDirection.None,
    };

    public static byte ArmFlag(FuseDirection direction) => direction switch
    {
        FuseDirection.Up => ArmUp,
        FuseDirection.Right => ArmRight,
        FuseDirection.Down => ArmDown,
        FuseDirection.Left => ArmLeft,
        _ => 0,
    };

    public static int Neighbour(int cell, FuseDirection direction)
    {
        if (cell < 0)
        {
            return -1;
        }

        var column = ColumnOf(cell) + StepX(direction);
        var row = RowOf(cell) + StepY(direction);
        return InArena(column, row) ? CellIndex(column, row) : -1;
    }

    public static bool InStartZone(int column, int row)
    {
        var fromSide = Math.Min(column, Columns - 1 - column);
        var fromEdge = Math.Min(row, Rows - 1 - row);
        return (fromSide == 0 && fromEdge <= StartZone) || (fromEdge == 0 && fromSide <= StartZone);
    }

    public static FuseVerdict Resolve(ReadOnlySpan<int> wins, int roundsPlayed)
    {
        if (wins[Player] >= WinsNeeded)
        {
            return FuseVerdict.Won;
        }

        var bestBot = 0;
        for (var index = 1; index < wins.Length; index++)
        {
            if (wins[index] >= WinsNeeded)
            {
                return FuseVerdict.Lost;
            }

            bestBot = Math.Max(bestBot, wins[index]);
        }

        if (roundsPlayed < MaxRounds)
        {
            return FuseVerdict.Ongoing;
        }

        if (wins[Player] == bestBot)
        {
            return FuseVerdict.Drawn;
        }

        return wins[Player] > bestBot ? FuseVerdict.Won : FuseVerdict.Lost;
    }

    public bool Walkable(int cell) => cell >= 0 && tiles[cell] == FuseTile.Floor && bombAt[cell] < 0;

    public bool MoogleIn(int cell)
    {
        for (var index = 0; index < MaxMoogles; index++)
        {
            if (moogles[index].Alive && CellAt(moogles[index].Position) == cell)
            {
                return true;
            }
        }

        return false;
    }

    public int ArmLength(int origin, FuseDirection direction, int range, int extraBombCell = -1)
    {
        var cell = origin;
        var length = 0;
        for (var step = 1; step <= range; step++)
        {
            cell = Neighbour(cell, direction);
            if (cell < 0)
            {
                break;
            }

            var tile = tiles[cell];
            if (tile is FuseTile.Pillar or FuseTile.Block)
            {
                break;
            }

            length++;
            if (tile == FuseTile.Crate || bombAt[cell] >= 0 || cell == extraBombCell)
            {
                break;
            }
        }

        return length;
    }

    public int BlastCells(int origin, int range, Span<int> output, int extraBombCell = -1)
    {
        var count = 0;
        output[count++] = origin;
        for (var directionIndex = 0; directionIndex < DirectionOrder.Length; directionIndex++)
        {
            var direction = DirectionOrder[directionIndex];
            var length = ArmLength(origin, direction, range, extraBombCell);
            var cell = origin;
            for (var step = 0; step < length; step++)
            {
                cell = Neighbour(cell, direction);
                output[count++] = cell;
            }
        }

        return count;
    }

    public int UpcomingFalls(Span<int> cells, Span<float> secondsLeft)
    {
        var count = 0;
        var cursor = fallCursor;
        while (count < cells.Length && cursor < SpiralOrder.Length)
        {
            var cell = SpiralOrder[cursor++];
            if (tiles[cell] == FuseTile.Pillar)
            {
                continue;
            }

            cells[count] = cell;
            secondsLeft[count] = SuddenDeathStart + (fallsLanded + count + 1) * FallInterval - Elapsed;
            count++;
        }

        return count;
    }

    public void Reset(GameRandom seededRandom, FuseSkill botSkill, bool autopilotPlayer = false)
    {
        random = seededRandom;
        skill = botSkill;
        autopilot = autopilotPlayer;
        sandbox = false;
        PlayerKnockouts = 0;
        PlayerCrates = 0;
        PlayerPowerUps = 0;
        Verdict = FuseVerdict.Ongoing;
        LastWinner = NoMoogle;
        for (var index = 0; index < MaxMoogles; index++)
        {
            moogles[index].Wins = 0;
        }

        clock.Reset();
        StartRound(1);
    }

    public void SetPlayerInput(FuseDirection direction, bool placeBomb)
    {
        playerDirection = direction;
        playerBomb |= placeBomb;
    }

    public void Step(float deltaSeconds)
    {
        ClearFrameEvents();
        if (deltaSeconds <= 0f || Phase == FusePhase.MatchOver)
        {
            return;
        }

        var ticks = clock.Advance(deltaSeconds);
        for (var tick = 0; tick < ticks && Phase != FusePhase.MatchOver; tick++)
        {
            Tick();
        }

        if (Phase != FusePhase.Fighting)
        {
            playerBomb = false;
        }
    }

    public void Tick()
    {
        AdvanceGhosts();
        switch (Phase)
        {
            case FusePhase.Ready:
                phaseTimer -= TickSeconds;
                if (phaseTimer > 0f)
                {
                    return;
                }

                Phase = FusePhase.Fighting;
                FightStartedThisFrame = true;
                return;
            case FusePhase.Fighting:
                Fight();
                return;
            case FusePhase.Ended:
                UpdateFires();
                phaseTimer -= TickSeconds;
                if (phaseTimer > 0f)
                {
                    return;
                }

                if (Verdict != FuseVerdict.Ongoing)
                {
                    Phase = FusePhase.MatchOver;
                    MatchEndedThisFrame = true;
                    return;
                }

                StartRound(Round + 1);
                return;
            default:
                return;
        }
    }

    public void ClearArena()
    {
        sandbox = true;
        Phase = FusePhase.Fighting;
        Elapsed = 0f;
        fightTicks = 0;
        settle = -1f;
        fallCursor = 0;
        fallsLanded = 0;
        SuddenDeath = false;
        LayPillars();
        ClearBombs();
        for (var index = 0; index < MaxMoogles; index++)
        {
            ref var moogle = ref moogles[index];
            moogle.Alive = false;
            moogle.Bot = false;
            moogle.Knockout = 0f;
            moogle.BombsOut = 0;
        }
    }

    public void SetTile(int column, int row, FuseTile tile)
    {
        tiles[CellIndex(column, row)] = tile;
    }

    public void SetHidden(int column, int row, PowerUp kind)
    {
        hidden[CellIndex(column, row)] = kind;
    }

    public void SetItem(int column, int row, PowerUp kind)
    {
        items[CellIndex(column, row)] = kind;
    }

    public void PlaceMoogle(int index, Vector2 position, bool driven)
    {
        ref var moogle = ref moogles[index];
        moogle.Position = position;
        moogle.Facing = FuseDirection.Down;
        moogle.Moving = false;
        moogle.Alive = true;
        moogle.Bot = driven;
        moogle.Bombs = StartBombs;
        moogle.Range = StartRange;
        moogle.SpeedLevel = 0;
        moogle.Kick = false;
        moogle.BombsOut = 0;
        moogle.Knockout = 0f;
        moogle.Stride = 0f;
    }

    public void PlaceMoogle(int index, int column, int row, bool driven) =>
        PlaceMoogle(index, CellCenter(CellIndex(column, row)), driven);

    public void Equip(int index, int bombCapacity, int range, int speedLevel, bool kick)
    {
        ref var moogle = ref moogles[index];
        moogle.Bombs = Math.Clamp(bombCapacity, 1, MaxBombsEach);
        moogle.Range = Math.Clamp(range, 1, MaxRange);
        moogle.SpeedLevel = Math.Clamp(speedLevel, 0, MaxSpeedLevel);
        moogle.Kick = kick;
    }

    public int PlaceBomb(int column, int row, int owner, int range, float fuseSeconds)
    {
        var cell = CellIndex(column, row);
        if (bombAt[cell] >= 0)
        {
            return bombAt[cell];
        }

        var slot = FreeBombSlot();
        if (slot < 0)
        {
            return -1;
        }

        bombs[slot] = new Bomb
        {
            Position = CellCenter(cell),
            Cell = cell,
            Fuse = fuseSeconds,
            Owner = owner,
            Range = range,
            Alive = true,
        };
        bombAt[cell] = slot;
        if (owner >= 0 && owner < MaxMoogles)
        {
            moogles[owner].BombsOut++;
        }

        return slot;
    }

    public MoogleInput DecideFor(int index)
    {
        bot.Prepare(this);
        return bot.Decide(this, index, SkillOf(index), ref random, TickSeconds);
    }

    private static int[] BuildSpiral()
    {
        var order = new int[CellCount];
        var count = 0;
        var top = 0;
        var bottom = Rows - 1;
        var left = 0;
        var right = Columns - 1;
        while (top <= bottom && left <= right)
        {
            for (var column = left; column <= right; column++)
            {
                order[count++] = CellIndex(column, top);
            }

            for (var row = top + 1; row <= bottom; row++)
            {
                order[count++] = CellIndex(right, row);
            }

            if (top < bottom)
            {
                for (var column = right - 1; column >= left; column--)
                {
                    order[count++] = CellIndex(column, bottom);
                }
            }

            if (left < right)
            {
                for (var row = bottom - 1; row > top; row--)
                {
                    order[count++] = CellIndex(left, row);
                }
            }

            top++;
            bottom--;
            left++;
            right--;
        }

        return order;
    }

    private void ClearFrameEvents()
    {
        BlastCount = 0;
        KnockoutCount = 0;
        PickupCount = 0;
        BrokenCrates.Clear();
        RevealedItems.Clear();
        BurnedItems.Clear();
        LandedBlocks.Clear();
        PlacedBombs.Clear();
        KickedBombs.Clear();
        BombTicks.Clear();
        RoundStartedThisFrame = false;
        FightStartedThisFrame = false;
        RoundEndedThisFrame = false;
        MatchEndedThisFrame = false;
        SuddenDeathStartedThisFrame = false;
    }

    private void StartRound(int round)
    {
        Round = round;
        Elapsed = 0f;
        fightTicks = 0;
        settle = -1f;
        fallCursor = 0;
        fallsLanded = 0;
        SuddenDeath = false;
        LastWinner = NoMoogle;
        playerBomb = false;
        Phase = round == 1 ? FusePhase.Fighting : FusePhase.Ready;
        phaseTimer = ReadySeconds;
        RoundStartedThisFrame = true;
        GenerateArena();
        ClearBombs();
        bot.Reset();
        for (var index = 0; index < MaxMoogles; index++)
        {
            PlaceMoogle(index, CornerColumns[index], CornerRows[index], index != Player || autopilot);
            moogles[index].Facing = CornerFacing[index];
        }
    }

    private void LayPillars()
    {
        for (var cell = 0; cell < CellCount; cell++)
        {
            var pillar = ColumnOf(cell) % 2 == 1 && RowOf(cell) % 2 == 1;
            tiles[cell] = pillar ? FuseTile.Pillar : FuseTile.Floor;
            hidden[cell] = PowerUp.None;
            items[cell] = PowerUp.None;
            pending[cell] = PowerUp.None;
            fire[cell] = 0f;
            fireArms[cell] = 0;
            fireOwner[cell] = NoMoogle;
        }
    }

    private void GenerateArena()
    {
        LayPillars();
        for (var cell = 0; cell < CellCount; cell++)
        {
            if (tiles[cell] != FuseTile.Floor || InStartZone(ColumnOf(cell), RowOf(cell)) ||
                !random.Chance(CrateChance))
            {
                continue;
            }

            tiles[cell] = FuseTile.Crate;
            if (random.Chance(DropChance))
            {
                hidden[cell] = RollPowerUp();
            }
        }
    }

    private PowerUp RollPowerUp()
    {
        var roll = random.Next(100);
        if (roll < 30)
        {
            return PowerUp.ExtraBomb;
        }

        if (roll < 60)
        {
            return PowerUp.Range;
        }

        return roll < 82 ? PowerUp.Speed : PowerUp.Kick;
    }

    private void ClearBombs()
    {
        for (var slot = 0; slot < BombCapacity; slot++)
        {
            bombs[slot].Alive = false;
            queued[slot] = false;
        }

        Array.Fill(bombAt, -1);
        detonationCount = 0;
    }

    private int FreeBombSlot()
    {
        for (var slot = 0; slot < BombCapacity; slot++)
        {
            if (!bombs[slot].Alive)
            {
                return slot;
            }
        }

        return -1;
    }

    private void AdvanceGhosts()
    {
        for (var index = 0; index < MaxMoogles; index++)
        {
            ref var moogle = ref moogles[index];
            if (moogle.Alive || moogle.Knockout <= 0f || moogle.Knockout >= 1f)
            {
                continue;
            }

            moogle.Knockout = MathF.Min(1f, moogle.Knockout + TickSeconds / GhostSeconds);
        }
    }

    private void Fight()
    {
        fightTicks++;
        Elapsed = fightTicks * TickSeconds;
        UpdateSuddenDeath();
        PrepareInputs();
        for (var index = 0; index < MaxMoogles; index++)
        {
            if (!moogles[index].Alive)
            {
                continue;
            }

            var input = inputs[index];
            if (input.Bomb)
            {
                TryPlaceBomb(index);
            }

            Move(index, input.Direction, SpeedOf(index) * TickSeconds);
            Collect(index);
        }

        UpdateBombs();
        UpdateFires();
        CheckFire();
        if (!sandbox)
        {
            CheckRoundEnd();
        }
    }

    private void PrepareInputs()
    {
        var anyBot = false;
        for (var index = 0; index < MaxMoogles; index++)
        {
            anyBot |= moogles[index].Alive && moogles[index].Bot;
        }

        if (anyBot)
        {
            bot.Prepare(this);
        }

        for (var index = 0; index < MaxMoogles; index++)
        {
            ref readonly var moogle = ref moogles[index];
            if (!moogle.Alive)
            {
                inputs[index] = default;
                continue;
            }

            if (moogle.Bot)
            {
                inputs[index] = bot.Decide(this, index, SkillOf(index), ref random, TickSeconds);
                continue;
            }

            if (index == Player)
            {
                inputs[index] = new MoogleInput(playerDirection, playerBomb);
                playerBomb = false;
                continue;
            }

            inputs[index] = default;
        }
    }

    private void UpdateSuddenDeath()
    {
        if (Elapsed < SuddenDeathStart)
        {
            return;
        }

        if (!SuddenDeath)
        {
            SuddenDeath = true;
            SuddenDeathStartedThisFrame = true;
        }

        while (fallCursor < SpiralOrder.Length)
        {
            var cell = SpiralOrder[fallCursor];
            if (tiles[cell] == FuseTile.Pillar)
            {
                fallCursor++;
                continue;
            }

            if (Elapsed < SuddenDeathStart + (fallsLanded + 1) * FallInterval)
            {
                return;
            }

            LandBlock(cell);
            fallsLanded++;
            fallCursor++;
        }
    }

    private void LandBlock(int cell)
    {
        tiles[cell] = FuseTile.Block;
        items[cell] = PowerUp.None;
        hidden[cell] = PowerUp.None;
        pending[cell] = PowerUp.None;
        fire[cell] = 0f;
        fireArms[cell] = 0;
        var slot = bombAt[cell];
        if (slot >= 0)
        {
            RemoveBomb(slot);
        }

        for (var index = 0; index < MaxMoogles; index++)
        {
            if (moogles[index].Alive && CellAt(moogles[index].Position) == cell)
            {
                KnockOut(index, NoMoogle, true);
            }
        }

        LandedBlocks.Add(cell, NoMoogle);
    }

    private void RemoveBomb(int slot)
    {
        ref var bomb = ref bombs[slot];
        bomb.Alive = false;
        bomb.Sliding = FuseDirection.None;
        bombAt[bomb.Cell] = -1;
        if (bomb.Owner >= 0 && bomb.Owner < MaxMoogles)
        {
            moogles[bomb.Owner].BombsOut = Math.Max(0, moogles[bomb.Owner].BombsOut - 1);
        }
    }

    private void TryPlaceBomb(int index)
    {
        ref var moogle = ref moogles[index];
        if (moogle.BombsOut >= moogle.Bombs)
        {
            return;
        }

        var cell = CellAt(moogle.Position);
        if (tiles[cell] != FuseTile.Floor || bombAt[cell] >= 0)
        {
            return;
        }

        var slot = FreeBombSlot();
        if (slot < 0)
        {
            return;
        }

        bombs[slot] = new Bomb
        {
            Position = CellCenter(cell),
            Cell = cell,
            Fuse = FuseSeconds,
            Owner = index,
            Range = moogle.Range,
            Alive = true,
        };
        bombAt[cell] = slot;
        moogle.BombsOut++;
        PlacedBombs.Add(cell, index);
    }

    private void Move(int index, FuseDirection direction, float distance)
    {
        ref var moogle = ref moogles[index];
        moogle.Moving = false;
        if (direction == FuseDirection.None || distance <= 0f)
        {
            return;
        }

        moogle.Facing = direction;
        var cell = CellAt(moogle.Position);
        var center = CellCenter(cell);
        var horizontal = direction is FuseDirection.Left or FuseDirection.Right;
        var forwardSign = direction is FuseDirection.Right or FuseDirection.Down ? 1f : -1f;
        var offset = moogle.Position - center;
        var along = horizontal ? offset.X : offset.Y;
        var across = horizontal ? offset.Y : offset.X;
        var forward = Neighbour(cell, direction);
        var open = Walkable(forward);
        var remaining = distance;
        if (MathF.Abs(across) > AlignEpsilon)
        {
            if (!open)
            {
                AssistCorner(ref moogle, cell, direction, horizontal, along, across, remaining, center);
                return;
            }

            var slide = MathF.Min(remaining, MathF.Abs(across));
            across -= MathF.Sign(across) * slide;
            remaining -= slide;
            moogle.Moving = true;
        }
        else
        {
            across = 0f;
        }

        if (remaining > 0f)
        {
            if (open)
            {
                along += forwardSign * remaining;
                moogle.Moving = true;
            }
            else
            {
                var toCenter = -forwardSign * along;
                if (toCenter > 0f)
                {
                    along += forwardSign * MathF.Min(remaining, toCenter);
                    moogle.Moving = true;
                }

                TryKick(index, forward, direction, toCenter);
            }
        }

        moogle.Position = Compose(center, horizontal, along, across);
        if (moogle.Moving)
        {
            moogle.Stride += distance * StrideRate;
        }
    }

    private void AssistCorner(ref Moogle moogle, int cell, FuseDirection direction, bool horizontal, float along,
        float across, float remaining, Vector2 center)
    {
        var side = across > 0f ? 1f : -1f;
        var sideDirection = horizontal
            ? (side > 0f ? FuseDirection.Down : FuseDirection.Up)
            : (side > 0f ? FuseDirection.Right : FuseDirection.Left);
        var sideCell = Neighbour(cell, sideDirection);
        var diagonal = Neighbour(sideCell, direction);
        if (MathF.Abs(across) < CornerAssist || !Walkable(sideCell) || !Walkable(diagonal))
        {
            return;
        }

        var slide = MathF.Min(remaining, 1f - MathF.Abs(across));
        moogle.Position = Compose(center, horizontal, along, across + side * slide);
        moogle.Moving = true;
        moogle.Stride += remaining * StrideRate;
    }

    private static Vector2 Compose(Vector2 center, bool horizontal, float along, float across) =>
        horizontal ? new Vector2(center.X + along, center.Y + across) : new Vector2(center.X + across, center.Y + along);

    private void TryKick(int index, int forward, FuseDirection direction, float toCenter)
    {
        if (!moogles[index].Kick || moogles[index].Bot || forward < 0 || toCenter > KickReach)
        {
            return;
        }

        var slot = bombAt[forward];
        if (slot < 0 || bombs[slot].Sliding != FuseDirection.None)
        {
            return;
        }

        bombs[slot].Sliding = direction;
        KickedBombs.Add(forward, index);
    }

    private void Collect(int index)
    {
        ref var moogle = ref moogles[index];
        var cell = CellAt(moogle.Position);
        var kind = items[cell];
        if (kind == PowerUp.None)
        {
            return;
        }

        items[cell] = PowerUp.None;
        switch (kind)
        {
            case PowerUp.ExtraBomb:
                moogle.Bombs = Math.Min(MaxBombsEach, moogle.Bombs + 1);
                break;
            case PowerUp.Range:
                moogle.Range = Math.Min(MaxRange, moogle.Range + 1);
                break;
            case PowerUp.Speed:
                moogle.SpeedLevel = Math.Min(MaxSpeedLevel, moogle.SpeedLevel + 1);
                break;
            case PowerUp.Kick:
                moogle.Kick = true;
                break;
            default:
                break;
        }

        if (index == Player)
        {
            PlayerPowerUps++;
        }

        if (PickupCount < pickups.Length)
        {
            pickups[PickupCount++] = new Pickup(index, kind, CellCenter(cell));
        }
    }

    private void UpdateBombs()
    {
        for (var slot = 0; slot < BombCapacity; slot++)
        {
            ref var bomb = ref bombs[slot];
            if (!bomb.Alive)
            {
                continue;
            }

            bomb.Age += TickSeconds;
            bomb.Fuse -= TickSeconds;
            var progress = 1f - Math.Clamp(bomb.Fuse / FuseSeconds, 0f, 1f);
            var previous = (int)bomb.Pulse;
            bomb.Pulse += (BasePulseHertz + PulseRampHertz * progress * progress) * TickSeconds;
            if ((int)bomb.Pulse != previous)
            {
                BombTicks.Add(bomb.Cell, bomb.Owner);
            }

            if (bomb.Sliding != FuseDirection.None)
            {
                Slide(slot);
            }

            if (bomb.Fuse <= 0f || fire[bomb.Cell] > 0f)
            {
                Enqueue(slot, 0);
            }
        }

        for (var index = 0; index < detonationCount; index++)
        {
            Detonate(detonations[index], detonationChains[index]);
        }

        for (var index = 0; index < detonationCount; index++)
        {
            queued[detonations[index]] = false;
        }

        detonationCount = 0;
    }

    private void Slide(int slot)
    {
        ref var bomb = ref bombs[slot];
        var direction = bomb.Sliding;
        var step = Vector(direction);
        var center = CellCenter(bomb.Cell);
        var along = Vector2.Dot(bomb.Position - center, step);
        var move = SlideSpeed * TickSeconds;
        if (along <= 0f && along + move > 0f && !BombCanEnter(Neighbour(bomb.Cell, direction)))
        {
            bomb.Position = center;
            bomb.Sliding = FuseDirection.None;
            return;
        }

        var position = bomb.Position + step * move;
        var cell = CellAt(position);
        if (cell == bomb.Cell)
        {
            bomb.Position = position;
            return;
        }

        if (bombAt[cell] >= 0 || tiles[cell] != FuseTile.Floor)
        {
            bomb.Position = center;
            bomb.Sliding = FuseDirection.None;
            return;
        }

        bomb.Position = position;
        bombAt[bomb.Cell] = -1;
        bomb.Cell = cell;
        bombAt[cell] = slot;
    }

    private bool BombCanEnter(int cell) => Walkable(cell) && !MoogleIn(cell);

    private void Enqueue(int slot, int chain)
    {
        if (queued[slot] || detonationCount >= BombCapacity)
        {
            return;
        }

        queued[slot] = true;
        detonations[detonationCount] = slot;
        detonationChains[detonationCount] = chain;
        detonationCount++;
    }

    private void Detonate(int slot, int chain)
    {
        ref var bomb = ref bombs[slot];
        if (!bomb.Alive)
        {
            return;
        }

        var cell = bomb.Cell;
        var owner = bomb.Owner;
        var range = bomb.Range;
        RemoveBomb(slot);
        Ignite(cell, ArmCore, owner);
        if (items[cell] != PowerUp.None)
        {
            items[cell] = PowerUp.None;
            BurnedItems.Add(cell, owner);
        }

        var up = Burn(cell, FuseDirection.Up, range, owner, chain);
        var right = Burn(cell, FuseDirection.Right, range, owner, chain);
        var down = Burn(cell, FuseDirection.Down, range, owner, chain);
        var left = Burn(cell, FuseDirection.Left, range, owner, chain);
        if (BlastCount < blasts.Length)
        {
            blasts[BlastCount++] = new Blast(cell, chain, owner, up, right, down, left);
        }
    }

    private int Burn(int origin, FuseDirection direction, int range, int owner, int chain)
    {
        var length = ArmLength(origin, direction, range);
        if (length == 0)
        {
            return 0;
        }

        fireArms[origin] |= ArmFlag(direction);
        var back = ArmFlag(Opposite(direction));
        var onward = ArmFlag(direction);
        var cell = origin;
        for (var step = 1; step <= length; step++)
        {
            cell = Neighbour(cell, direction);
            Ignite(cell, step < length ? (byte)(back | onward) : back, owner);
            Scorch(cell, owner, chain);
        }

        return length;
    }

    private void Ignite(int cell, byte arms, int owner)
    {
        fire[cell] = FireSeconds;
        fireArms[cell] |= arms;
        fireOwner[cell] = owner;
    }

    private void Scorch(int cell, int owner, int chain)
    {
        if (tiles[cell] == FuseTile.Crate)
        {
            tiles[cell] = FuseTile.Floor;
            pending[cell] = hidden[cell];
            hidden[cell] = PowerUp.None;
            BrokenCrates.Add(cell, owner);
            if (owner == Player)
            {
                PlayerCrates++;
            }
        }

        if (items[cell] != PowerUp.None)
        {
            items[cell] = PowerUp.None;
            BurnedItems.Add(cell, owner);
        }

        var other = bombAt[cell];
        if (other >= 0)
        {
            Enqueue(other, chain + 1);
        }
    }

    private void UpdateFires()
    {
        for (var cell = 0; cell < CellCount; cell++)
        {
            if (fire[cell] <= 0f)
            {
                continue;
            }

            fire[cell] -= TickSeconds;
            if (fire[cell] > 0f)
            {
                continue;
            }

            fire[cell] = 0f;
            fireArms[cell] = 0;
            fireOwner[cell] = NoMoogle;
            if (pending[cell] == PowerUp.None)
            {
                continue;
            }

            items[cell] = pending[cell];
            pending[cell] = PowerUp.None;
            RevealedItems.Add(cell, NoMoogle);
        }
    }

    private void CheckFire()
    {
        for (var index = 0; index < MaxMoogles; index++)
        {
            if (!moogles[index].Alive)
            {
                continue;
            }

            var cell = CellAt(moogles[index].Position);
            if (fire[cell] > 0f)
            {
                KnockOut(index, fireOwner[cell], false);
            }
        }
    }

    private void KnockOut(int index, int killer, bool crushed)
    {
        ref var moogle = ref moogles[index];
        moogle.Alive = false;
        moogle.Moving = false;
        moogle.Knockout = 0.0001f;
        if (killer == Player && index != Player)
        {
            PlayerKnockouts++;
        }

        if (KnockoutCount < knockouts.Length)
        {
            knockouts[KnockoutCount++] = new Knockout(index, killer, moogle.Position, crushed);
        }
    }

    private void CheckRoundEnd()
    {
        if (AliveCount > 1 && Elapsed < RoundSeconds)
        {
            settle = -1f;
            return;
        }

        if (settle < 0f)
        {
            settle = SettleSeconds;
            return;
        }

        settle -= TickSeconds;
        if (settle > 0f)
        {
            return;
        }

        EndRound();
    }

    private void EndRound()
    {
        LastWinner = NoMoogle;
        if (AliveCount == 1)
        {
            for (var index = 0; index < MaxMoogles; index++)
            {
                if (!moogles[index].Alive)
                {
                    continue;
                }

                moogles[index].Wins++;
                LastWinner = index;
            }
        }

        Phase = FusePhase.Ended;
        phaseTimer = EndSeconds;
        RoundEndedThisFrame = true;
        Span<int> wins = stackalloc int[MaxMoogles];
        for (var index = 0; index < MaxMoogles; index++)
        {
            wins[index] = moogles[index].Wins;
        }

        Verdict = Resolve(wins, Round);
    }
}
