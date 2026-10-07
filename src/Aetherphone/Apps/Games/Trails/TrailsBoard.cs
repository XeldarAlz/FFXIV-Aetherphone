using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Trails;

internal enum Heading : byte
{
    Up,
    Right,
    Down,
    Left,
}

internal enum BotSkill : byte
{
    Easy,
    Hard,
}

internal enum RoundPhase : byte
{
    Ready,
    Racing,
    Ended,
    MatchOver,
}

internal enum RoundResult : byte
{
    None,
    Won,
    Lost,
    Drawn,
}

internal enum MatchVerdict : byte
{
    Ongoing,
    Won,
    Lost,
    Drawn,
}

internal struct Rider
{
    public int Column;
    public int Row;
    public int PreviousColumn;
    public int PreviousRow;
    public Heading Heading;
    public bool Alive;
    public bool Bot;
    public float Derezz;
    public int PathCount;
}

internal readonly struct Crash
{
    public readonly int Rider;
    public readonly Vector2 Position;
    public readonly bool Takedown;

    public Crash(int rider, Vector2 position, bool takedown)
    {
        Rider = rider;
        Position = position;
        Takedown = takedown;
    }
}

internal sealed class TrailsBoard
{
    public const int Columns = 28;
    public const int Rows = 40;
    public const int CellCount = Columns * Rows;
    public const int MaxRiders = 4;
    public const int Player = 0;
    public const int WinsNeeded = 3;
    public const int MaxRounds = 9;
    public const int PathCapacity = 1024;
    public const byte Wall = byte.MaxValue;
    public const float ReadySeconds = 1.3f;
    public const float EndSeconds = 1.9f;
    public const float DerezzSeconds = 0.7f;
    public const float SpeedRamp = 0.16f;
    public const float MaxRamp = 4f;
    private const int QueueDepth = 2;
    private const int StartJitter = 3;
    private static readonly int[] StepX = { 0, 1, 0, -1 };
    private static readonly int[] StepY = { -1, 0, 1, 0 };

    private readonly byte[] owners = new byte[CellCount];
    private readonly Rider[] riders = new Rider[MaxRiders];
    private readonly Vector2[] paths = new Vector2[MaxRiders * PathCapacity];
    private readonly Heading[] queue = new Heading[QueueDepth];
    private readonly Crash[] crashes = new Crash[MaxRiders];
    private readonly int[] nextCells = new int[MaxRiders];
    private readonly bool[] crashing = new bool[MaxRiders];
    private readonly TrailsBot bot = new();
    private GameRandom random;
    private BotSkill skill;
    private float baseSpeed;
    private float tickTimer;
    private float phaseTimer;
    private float roundSeconds;
    private int queued;
    private bool playerIsBot;

    public int RiderCount { get; private set; }

    public RoundPhase Phase { get; private set; }

    public int Round { get; private set; }

    public int PlayerWins { get; private set; }

    public int PlayerLosses { get; private set; }

    public RoundResult LastResult { get; private set; }

    public MatchVerdict Verdict { get; private set; }

    public float Speed { get; private set; }

    public float SpeedFraction => Math.Clamp((Speed - baseSpeed) / MaxRamp, 0f, 1f);

    public int Takedowns { get; private set; }

    public float LongestRide { get; private set; }

    public int CrashCount { get; private set; }

    public bool RoundStartedThisFrame { get; private set; }

    public bool RacingStartedThisFrame { get; private set; }

    public bool RoundEndedThisFrame { get; private set; }

    public bool MatchEndedThisFrame { get; private set; }

    public bool TurnedThisFrame { get; private set; }

    public bool GrindLeftThisFrame { get; private set; }

    public bool GrindRightThisFrame { get; private set; }

    public bool PlayerAlive => riders[Player].Alive;

    public bool MatchPoint => PlayerWins == WinsNeeded - 1 || PlayerLosses == WinsNeeded - 1;

    public float Alpha => Phase == RoundPhase.Racing ? Math.Clamp(tickTimer * Speed, 0f, 1f) : 1f;

    public ref readonly Rider RiderAt(int index) => ref riders[index];

    public Crash CrashAt(int index) => crashes[index];

    public Vector2 PathPoint(int rider, int index) => paths[rider * PathCapacity + index];

    public byte OwnerAt(int column, int row) => InField(column, row) ? owners[CellIndex(column, row)] : Wall;

    public bool IsFree(int column, int row) => InField(column, row) && owners[CellIndex(column, row)] == 0;

    public static int CellIndex(int column, int row) => row * Columns + column;

    public static bool InField(int column, int row) => column >= 0 && column < Columns && row >= 0 && row < Rows;

    public static Vector2 CellCenter(int column, int row) => new(column + 0.5f, row + 0.5f);

    public static int DeltaX(Heading heading) => StepX[(int)heading];

    public static int DeltaY(Heading heading) => StepY[(int)heading];

    public static Heading TurnLeft(Heading heading) => (Heading)(((int)heading + 3) % 4);

    public static Heading TurnRight(Heading heading) => (Heading)(((int)heading + 1) % 4);

    public static bool Opposite(Heading first, Heading second) => ((int)first + 2) % 4 == (int)second;

    public static MatchVerdict Resolve(int wins, int losses, int roundsPlayed)
    {
        if (wins >= WinsNeeded)
        {
            return MatchVerdict.Won;
        }

        if (losses >= WinsNeeded)
        {
            return MatchVerdict.Lost;
        }

        if (roundsPlayed < MaxRounds)
        {
            return MatchVerdict.Ongoing;
        }

        if (wins == losses)
        {
            return MatchVerdict.Drawn;
        }

        return wins > losses ? MatchVerdict.Won : MatchVerdict.Lost;
    }

    public Vector2 HeadPosition(int index)
    {
        ref readonly var rider = ref riders[index];
        var alpha = rider.Alive ? Alpha : 1f;
        return new Vector2(rider.PreviousColumn + (rider.Column - rider.PreviousColumn) * alpha + 0.5f,
            rider.PreviousRow + (rider.Row - rider.PreviousRow) * alpha + 0.5f);
    }

    public void Reset(GameRandom seededRandom, int bots, BotSkill botSkill, float speed, bool autopilot = false)
    {
        random = seededRandom;
        RiderCount = Math.Clamp(bots + 1, 2, MaxRiders);
        skill = botSkill;
        baseSpeed = speed;
        playerIsBot = autopilot;
        Takedowns = 0;
        LongestRide = 0f;
        PlayerWins = 0;
        PlayerLosses = 0;
        Verdict = MatchVerdict.Ongoing;
        StartRound(1);
    }

    public void QueueTurn(int direction)
    {
        if (direction == 0)
        {
            return;
        }

        var last = queued > 0 ? queue[queued - 1] : riders[Player].Heading;
        QueueHeading(direction < 0 ? TurnLeft(last) : TurnRight(last));
    }

    public void QueueHeading(Heading heading)
    {
        if (!riders[Player].Alive || Phase == RoundPhase.Ended || Phase == RoundPhase.MatchOver)
        {
            return;
        }

        var last = queued > 0 ? queue[queued - 1] : riders[Player].Heading;
        if (heading == last || Opposite(heading, last) || queued == QueueDepth)
        {
            return;
        }

        queue[queued++] = heading;
    }

    public void ClearArena()
    {
        Array.Clear(owners);
        queued = 0;
        Phase = RoundPhase.Racing;
        LastResult = RoundResult.None;
        for (var index = 0; index < MaxRiders; index++)
        {
            riders[index].Alive = false;
            riders[index].Derezz = 0f;
            riders[index].PathCount = 0;
        }
    }

    public void Block(int column, int row)
    {
        if (InField(column, row))
        {
            owners[CellIndex(column, row)] = Wall;
        }
    }

    public void PlaceRider(int index, int column, int row, Heading heading, bool bot)
    {
        ref var rider = ref riders[index];
        rider.Column = column;
        rider.Row = row;
        rider.PreviousColumn = column;
        rider.PreviousRow = row;
        rider.Heading = heading;
        rider.Alive = true;
        rider.Bot = bot;
        rider.Derezz = 0f;
        rider.PathCount = 0;
        owners[CellIndex(column, row)] = (byte)(index + 1);
        AddPathPoint(index, CellCenter(column, row));
    }

    public Heading ChooseDirection(int index) => bot.Choose(this, index, skill, ref random);

    public void Step(float deltaSeconds)
    {
        ClearFrameEvents();
        if (deltaSeconds <= 0f)
        {
            return;
        }

        AdvanceDerezz(deltaSeconds);
        switch (Phase)
        {
            case RoundPhase.Ready:
                phaseTimer -= deltaSeconds;
                if (phaseTimer <= 0f)
                {
                    Phase = RoundPhase.Racing;
                    RacingStartedThisFrame = true;
                }

                return;
            case RoundPhase.Racing:
                Race(deltaSeconds);
                return;
            case RoundPhase.Ended:
                phaseTimer -= deltaSeconds;
                if (phaseTimer > 0f)
                {
                    return;
                }

                if (Verdict != MatchVerdict.Ongoing)
                {
                    Phase = RoundPhase.MatchOver;
                    MatchEndedThisFrame = true;
                    return;
                }

                StartRound(Round + 1);
                return;
            default:
                return;
        }
    }

    public void Tick()
    {
        if (queued > 0 && riders[Player].Alive)
        {
            var heading = queue[0];
            for (var index = 1; index < queued; index++)
            {
                queue[index - 1] = queue[index];
            }

            queued--;
            if (heading != riders[Player].Heading)
            {
                Turn(Player, heading);
                TurnedThisFrame = true;
            }
        }

        for (var index = 0; index < RiderCount; index++)
        {
            ref readonly var rider = ref riders[index];
            if (!rider.Alive || !rider.Bot)
            {
                continue;
            }

            var choice = ChooseDirection(index);
            if (choice != rider.Heading)
            {
                Turn(index, choice);
            }
        }

        for (var index = 0; index < RiderCount; index++)
        {
            ref readonly var rider = ref riders[index];
            crashing[index] = false;
            nextCells[index] = -1;
            if (!rider.Alive)
            {
                continue;
            }

            var column = rider.Column + DeltaX(rider.Heading);
            var row = rider.Row + DeltaY(rider.Heading);
            if (!IsFree(column, row))
            {
                crashing[index] = true;
                continue;
            }

            nextCells[index] = CellIndex(column, row);
        }

        for (var first = 0; first < RiderCount; first++)
        {
            for (var second = first + 1; second < RiderCount; second++)
            {
                if (nextCells[first] < 0 || nextCells[first] != nextCells[second])
                {
                    continue;
                }

                crashing[first] = true;
                crashing[second] = true;
            }
        }

        var crashed = false;
        for (var index = 0; index < RiderCount; index++)
        {
            ref var rider = ref riders[index];
            if (!rider.Alive)
            {
                continue;
            }

            rider.PreviousColumn = rider.Column;
            rider.PreviousRow = rider.Row;
            if (crashing[index])
            {
                Wreck(index);
                crashed = true;
                continue;
            }

            rider.Column = nextCells[index] % Columns;
            rider.Row = nextCells[index] / Columns;
            owners[nextCells[index]] = (byte)(index + 1);
        }

        if (riders[Player].Alive)
        {
            ref readonly var player = ref riders[Player];
            var left = TurnLeft(player.Heading);
            var right = TurnRight(player.Heading);
            GrindLeftThisFrame |= !IsFree(player.Column + DeltaX(left), player.Row + DeltaY(left));
            GrindRightThisFrame |= !IsFree(player.Column + DeltaX(right), player.Row + DeltaY(right));
        }

        if (crashed)
        {
            CheckRoundEnd();
        }
    }

    private void ClearFrameEvents()
    {
        CrashCount = 0;
        RoundStartedThisFrame = false;
        RacingStartedThisFrame = false;
        RoundEndedThisFrame = false;
        MatchEndedThisFrame = false;
        TurnedThisFrame = false;
        GrindLeftThisFrame = false;
        GrindRightThisFrame = false;
    }

    private void StartRound(int round)
    {
        Round = round;
        Array.Clear(owners);
        queued = 0;
        tickTimer = 0f;
        roundSeconds = 0f;
        Speed = baseSpeed;
        LastResult = RoundResult.None;
        RoundStartedThisFrame = true;
        Phase = round == 1 ? RoundPhase.Racing : RoundPhase.Ready;
        phaseTimer = ReadySeconds;
        for (var index = 0; index < MaxRiders; index++)
        {
            riders[index].Alive = false;
            riders[index].Derezz = 0f;
            riders[index].PathCount = 0;
        }

        PlaceRider(Player, Columns / 2 + Spread(), Rows - 4, Heading.Up, playerIsBot);
        var bots = RiderCount - 1;
        if (bots == 1)
        {
            PlaceRider(1, Columns / 2 - 2 - Spread(), 3, Heading.Down, true);
            return;
        }

        if (bots == 2)
        {
            PlaceRider(1, 3, Rows / 2 + Spread(), Heading.Right, true);
            PlaceRider(2, Columns - 4, Rows / 2 - 1 - Spread(), Heading.Left, true);
            return;
        }

        PlaceRider(1, Columns / 2 - 2 - Spread(), 3, Heading.Down, true);
        PlaceRider(2, 3, Rows / 2 + Spread(), Heading.Right, true);
        PlaceRider(3, Columns - 4, Rows / 2 - 1 - Spread(), Heading.Left, true);
    }

    private int Spread() => random.Next(StartJitter + 1);

    private void Race(float deltaSeconds)
    {
        roundSeconds += deltaSeconds;
        Speed = baseSpeed + MathF.Min(MaxRamp, roundSeconds * SpeedRamp);
        tickTimer += deltaSeconds;
        var tickSeconds = 1f / Speed;
        while (tickTimer >= tickSeconds && Phase == RoundPhase.Racing)
        {
            tickTimer -= tickSeconds;
            Tick();
        }
    }

    private void Turn(int index, Heading heading)
    {
        ref var rider = ref riders[index];
        rider.Heading = heading;
        AddPathPoint(index, CellCenter(rider.Column, rider.Row));
    }

    private void AddPathPoint(int index, Vector2 point)
    {
        ref var rider = ref riders[index];
        if (rider.PathCount >= PathCapacity)
        {
            return;
        }

        paths[index * PathCapacity + rider.PathCount] = point;
        rider.PathCount++;
    }

    private void Wreck(int index)
    {
        ref var rider = ref riders[index];
        rider.Alive = false;
        rider.Derezz = 0.0001f;
        var targetColumn = rider.Column + DeltaX(rider.Heading);
        var targetRow = rider.Row + DeltaY(rider.Heading);
        var takedown = index != Player && OwnerAt(targetColumn, targetRow) == Player + 1;
        if (takedown)
        {
            Takedowns++;
        }

        var position = CellCenter(rider.Column, rider.Row) +
                       new Vector2(DeltaX(rider.Heading), DeltaY(rider.Heading)) * 0.5f;
        AddPathPoint(index, CellCenter(rider.Column, rider.Row));
        if (CrashCount < MaxRiders)
        {
            crashes[CrashCount++] = new Crash(index, position, takedown);
        }

        if (index != Player)
        {
            return;
        }

        LongestRide = MathF.Max(LongestRide, roundSeconds);
        queued = 0;
    }

    private void CheckRoundEnd()
    {
        var botsAlive = 0;
        for (var index = 1; index < RiderCount; index++)
        {
            if (riders[index].Alive)
            {
                botsAlive++;
            }
        }

        if (!riders[Player].Alive)
        {
            EndRound(botsAlive > 0 ? RoundResult.Lost : RoundResult.Drawn);
            return;
        }

        if (botsAlive == 0)
        {
            EndRound(RoundResult.Won);
        }
    }

    private void EndRound(RoundResult result)
    {
        LastResult = result;
        if (result == RoundResult.Won)
        {
            PlayerWins++;
            LongestRide = MathF.Max(LongestRide, roundSeconds);
        }
        else if (result == RoundResult.Lost)
        {
            PlayerLosses++;
        }

        Phase = RoundPhase.Ended;
        phaseTimer = EndSeconds;
        RoundEndedThisFrame = true;
        Verdict = Resolve(PlayerWins, PlayerLosses, Round);
    }

    private void AdvanceDerezz(float deltaSeconds)
    {
        for (var index = 0; index < RiderCount; index++)
        {
            ref var rider = ref riders[index];
            if (rider.Derezz <= 0f || rider.Derezz >= 1f)
            {
                continue;
            }

            rider.Derezz = MathF.Min(1f, rider.Derezz + deltaSeconds / DerezzSeconds);
            if (rider.Derezz < 1f)
            {
                continue;
            }

            var mark = (byte)(index + 1);
            for (var cell = 0; cell < CellCount; cell++)
            {
                if (owners[cell] == mark)
                {
                    owners[cell] = 0;
                }
            }
        }
    }
}
