using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.CapMan;

internal enum GhostState : byte
{
    House,
    Leaving,
    Normal,
    Frightened,
    Eyes,
}

internal enum FruitKind : byte
{
    Cherry,
    Strawberry,
    Orange,
    Apple,
    Melon,
}

internal struct Ghost
{
    public Vector2 Position;
    public Vector2 Direction;
    public GhostState State;
    public int Personality;
    public float ReleaseTimer;
    public Vector2 DecidedAt;
}

internal sealed class CapManBoard
{
    public const int Columns = 15;
    public const int Rows = 18;
    public const int GhostCount = 4;
    public const int StartLives = 3;
    public const float ReadySeconds = 1.6f;
    public const float DeathSeconds = 1.4f;
    public const float MaxSubstepSeconds = 0.016f;
    public const char Wall = '#';
    public const char Dot = '.';
    public const char Pellet = 'o';
    public const char Door = '-';
    public const char Floor = ' ';
    public const int DotPoints = 10;
    public const int PelletPoints = 50;
    public const int LevelClearBonus = 500;
    public const int FruitKindCount = 5;
    public const int FirstFruitCollectables = 70;
    public const int FruitRepeatCollectables = 100;
    public const float FruitSeconds = 9f;
    public const int FirstFruitPoints = 100;
    public const int LaterFruitPoints = 300;
    public const int ChaserPersonality = 0;
    public const int AmbusherPersonality = 1;
    public const int WandererPersonality = 2;
    public const int ShyPersonality = 3;
    public static readonly string[] Layout =
    {
        "###############",
        "#......#......#",
        "#o##.#.#.#.##o#",
        "#.............#",
        "#.##.##.##.##.#",
        "#.............#",
        "####.##.##.####",
        "   ..#...#..   ",
        "###..##-##..###",
        "###..#GGG#..###",
        "###..#####..###",
        "#......#......#",
        "#.##.##.##.##.#",
        "#......P......#",
        "#.#.###.###.#.#",
        "#o...#...#...o#",
        "#....#.#.#....#",
        "###############",
    };

    public static readonly Vector2 Up = new(0f, -1f);
    public static readonly Vector2 Down = new(0f, 1f);
    public static readonly Vector2 Left = new(-1f, 0f);
    public static readonly Vector2 Right = new(1f, 0f);
    private static readonly Vector2[] DirectionOrder = { Up, Left, Down, Right };
    private static readonly Vector2 HouseCentre = new(7f, 9f);
    private static readonly Vector2 DoorExit = new(7f, 7f);
    private static readonly Vector2 ShyCorner = new(1f, 16f);
    private const char PenMarker = 'G';
    private const char StartMarker = 'P';
    private const float TurnSlack = 0.28f;
    private const float PlayerSpeed = 6.2f;
    private const float EyesSpeed = 10f;
    private const float FrightenedSpeed = 3f;
    private const float HouseSpeed = 2.5f;
    private const float LeavingSpeed = 4f;
    private const float NormalSpeedCap = PlayerSpeed * 0.98f;
    private const float NormalSpeedBase = 4.6f;
    private const float NormalSpeedPerLevel = 0.2f;
    private const float FrightBase = 7f;
    private const float FrightPerLevel = 0.5f;
    private const float FrightMinimum = 2f;
    private const float CollisionRadius = 0.7f;
    private const float ReleaseStagger = 3f;
    private const float EyesRelease = 1.5f;
    private const float HouseArrival = 0.6f;
    private const float AmbushLead = 4f;
    private const float ShyDistance = 8f;
    private const int MaxGhostChain = 4;
    private readonly char[] tiles = new char[Columns * Rows];
    private readonly Ghost[] ghosts = new Ghost[GhostCount];
    private readonly Vector2[] ghostEatPositions = new Vector2[GhostCount];
    private readonly int[] ghostEatPoints = new int[GhostCount];
    private readonly Vector2[] options = new Vector2[DirectionOrder.Length];
    private GameRandom random;
    private Vector2 playerStart;
    private Vector2 queuedDirection;
    private float frightTimer;
    private float fruitTimer;
    private int fruitsSpawned;
    private int ghostChain;
    private float readyTimer;
    private float deathTimer;
    public int Score { get; private set; }
    public int Level { get; private set; }
    public int Lives { get; private set; }
    public int DotsLeft { get; private set; }
    public int Collected { get; private set; }
    public int GhostsEaten { get; private set; }
    public int FruitEaten { get; private set; }
    public bool GameOver { get; private set; }
    public Vector2 PlayerPosition { get; private set; }
    public Vector2 PlayerDirection { get; private set; }
    public float FrightRemaining => frightTimer;
    public bool Ready => readyTimer > 0f;
    public bool Dying => deathTimer > 0f;
    public float DeathProgress => deathTimer > 0f ? 1f - deathTimer / DeathSeconds : 0f;
    public bool Frozen => Ready || Dying;
    public bool FruitActive => fruitTimer > 0f;
    public float FruitRemaining => fruitTimer;
    public FruitKind Fruit { get; private set; }
    public Vector2 FruitPosition => playerStart;
    public int DotsEatenThisFrame { get; private set; }
    public Vector2 LastDotPosition { get; private set; }
    public bool PelletEatenThisFrame { get; private set; }
    public int GhostsEatenThisFrame { get; private set; }
    public bool PlayerDiedThisFrame { get; private set; }
    public bool LevelClearedThisFrame { get; private set; }
    public bool ReadyStartedThisFrame { get; private set; }
    public bool FruitSpawnedThisFrame { get; private set; }
    public bool FruitEatenThisFrame { get; private set; }
    public int LastFruitPoints { get; private set; }
    public Ghost GetGhost(int index) => ghosts[index];
    public Vector2 GhostEatPosition(int index) => ghostEatPositions[index];
    public int GhostEatPoints(int index) => ghostEatPoints[index];
    public char Tile(int x, int y) => tiles[y * Columns + x];

    public static float NormalGhostSpeed(int level) => MathF.Min(NormalSpeedCap, NormalSpeedBase + level * NormalSpeedPerLevel);

    public static float FrightDuration(int level) => MathF.Max(FrightMinimum, FrightBase - level * FrightPerLevel);

    public static int ChainPoints(int chain) => 100 * (1 << chain);

    public static bool FruitSpawnsAt(int collected) =>
        collected >= FirstFruitCollectables && (collected - FirstFruitCollectables) % FruitRepeatCollectables == 0;

    public static int FruitPoints(int fruitIndex) => fruitIndex == 0 ? FirstFruitPoints : LaterFruitPoints;

    public void StartGame(GameRandom seededRandom)
    {
        random = seededRandom;
        Score = 0;
        Level = 0;
        Lives = StartLives;
        Collected = 0;
        GhostsEaten = 0;
        FruitEaten = 0;
        fruitsSpawned = 0;
        GameOver = false;
        ClearFrameEvents();
        StartLevel();
    }

    public void Turn(Vector2 direction)
    {
        queuedDirection = direction;
    }

    public void Tick(float deltaSeconds)
    {
        ClearFrameEvents();
        if (GameOver || deltaSeconds <= 0f)
        {
            return;
        }

        if (readyTimer > 0f)
        {
            readyTimer -= deltaSeconds;
            return;
        }

        if (deathTimer > 0f)
        {
            deathTimer -= deltaSeconds;
            if (deathTimer > 0f)
            {
                return;
            }

            deathTimer = 0f;
            if (Lives <= 0)
            {
                GameOver = true;
            }
            else
            {
                PlaceActors();
            }

            return;
        }

        TickFright(deltaSeconds);
        TickFruit(deltaSeconds);
        var substeps = new Substeps(deltaSeconds, MaxSubstepSeconds);
        for (var step = 0; step < substeps.Count; step++)
        {
            MovePlayer(substeps.Step);
            Consume();
            for (var ghostIndex = 0; ghostIndex < GhostCount; ghostIndex++)
            {
                MoveGhost(ref ghosts[ghostIndex], substeps.Step);
            }

            if (CheckCollisions())
            {
                return;
            }
        }

        if (DotsLeft == 0)
        {
            Score += LevelClearBonus;
            LevelClearedThisFrame = true;
            StartLevel();
        }
    }

    public bool Walkable(int x, int y, bool forGhost, bool useDoor = false)
    {
        if (y < 0 || y >= Rows)
        {
            return false;
        }

        var wrapped = ((x % Columns) + Columns) % Columns;
        var tile = tiles[y * Columns + wrapped];
        if (tile == Wall)
        {
            return false;
        }

        if (tile == Door)
        {
            return forGhost && useDoor;
        }

        return true;
    }

    private void ClearFrameEvents()
    {
        DotsEatenThisFrame = 0;
        PelletEatenThisFrame = false;
        GhostsEatenThisFrame = 0;
        PlayerDiedThisFrame = false;
        LevelClearedThisFrame = false;
        ReadyStartedThisFrame = false;
        FruitSpawnedThisFrame = false;
        FruitEatenThisFrame = false;
    }

    private void StartLevel()
    {
        Level++;
        DotsLeft = 0;
        for (var y = 0; y < Rows; y++)
        {
            var line = Layout[y];
            for (var x = 0; x < Columns; x++)
            {
                var tile = line[x];
                if (tile == PenMarker)
                {
                    tile = Floor;
                }
                else if (tile == StartMarker)
                {
                    playerStart = new Vector2(x, y);
                    tile = Dot;
                }

                if (tile == Dot || tile == Pellet)
                {
                    DotsLeft++;
                }

                tiles[y * Columns + x] = tile;
            }
        }

        frightTimer = 0f;
        ghostChain = 0;
        PlaceActors();
    }

    private void PlaceActors()
    {
        PlayerPosition = playerStart;
        PlayerDirection = Vector2.Zero;
        queuedDirection = Vector2.Zero;
        for (var index = 0; index < GhostCount; index++)
        {
            var penned = index > 0;
            ghosts[index] = new Ghost
            {
                Position = penned ? new Vector2(5f + index, HouseCentre.Y) : DoorExit,
                Direction = Left,
                State = penned ? GhostState.House : GhostState.Normal,
                Personality = index,
                ReleaseTimer = index * ReleaseStagger,
                DecidedAt = new Vector2(-1f, -1f),
            };
        }

        frightTimer = 0f;
        fruitTimer = 0f;
        ghostChain = 0;
        readyTimer = ReadySeconds;
        ReadyStartedThisFrame = true;
    }

    private static bool NearCentre(Vector2 position) =>
        MathF.Abs(position.X - MathF.Round(position.X)) < TurnSlack &&
        MathF.Abs(position.Y - MathF.Round(position.Y)) < TurnSlack;

    private static Vector2 Wrap(Vector2 position)
    {
        if (position.X < -0.5f)
        {
            position.X += Columns;
        }
        else if (position.X > Columns - 0.5f)
        {
            position.X -= Columns;
        }

        return position;
    }

    private void MovePlayer(float deltaSeconds)
    {
        var position = PlayerPosition;
        var tileX = (int)MathF.Round(position.X);
        var tileY = (int)MathF.Round(position.Y);
        if (queuedDirection != Vector2.Zero && NearCentre(position) &&
            Walkable(tileX + (int)queuedDirection.X, tileY + (int)queuedDirection.Y, false))
        {
            position = new Vector2(tileX, tileY);
            PlayerDirection = queuedDirection;
            queuedDirection = Vector2.Zero;
        }

        if (PlayerDirection == Vector2.Zero)
        {
            PlayerPosition = position;
            return;
        }

        if (!Walkable(tileX + (int)PlayerDirection.X, tileY + (int)PlayerDirection.Y, false) && NearCentre(position))
        {
            PlayerPosition = new Vector2(tileX, tileY);
            return;
        }

        PlayerPosition = Wrap(position + PlayerDirection * PlayerSpeed * deltaSeconds);
    }

    private void Consume()
    {
        var x = (int)MathF.Round(PlayerPosition.X);
        var y = (int)MathF.Round(PlayerPosition.Y);
        if (x < 0 || x >= Columns || y < 0 || y >= Rows)
        {
            return;
        }

        if (fruitTimer > 0f && x == (int)playerStart.X && y == (int)playerStart.Y)
        {
            EatFruit();
        }

        var index = y * Columns + x;
        var tile = tiles[index];
        if (tile == Dot)
        {
            tiles[index] = Floor;
            DotsLeft--;
            Score += DotPoints;
            DotsEatenThisFrame++;
            LastDotPosition = new Vector2(x, y);
            Collect();
            return;
        }

        if (tile != Pellet)
        {
            return;
        }

        tiles[index] = Floor;
        DotsLeft--;
        Score += PelletPoints;
        PelletEatenThisFrame = true;
        LastDotPosition = new Vector2(x, y);
        frightTimer = FrightDuration(Level);
        ghostChain = 0;
        for (var ghostIndex = 0; ghostIndex < GhostCount; ghostIndex++)
        {
            ref var ghost = ref ghosts[ghostIndex];
            if (ghost.State != GhostState.Normal)
            {
                continue;
            }

            ghost.State = GhostState.Frightened;
            ghost.Direction = -ghost.Direction;
        }

        Collect();
    }

    private void Collect()
    {
        Collected++;
        if (!FruitSpawnsAt(Collected))
        {
            return;
        }

        Fruit = (FruitKind)random.Next(FruitKindCount);
        fruitTimer = FruitSeconds;
        fruitsSpawned++;
        FruitSpawnedThisFrame = true;
    }

    private void EatFruit()
    {
        var points = FruitPoints(fruitsSpawned - 1);
        fruitTimer = 0f;
        Score += points;
        FruitEaten++;
        LastFruitPoints = points;
        FruitEatenThisFrame = true;
    }

    private void TickFruit(float deltaSeconds)
    {
        if (fruitTimer <= 0f)
        {
            return;
        }

        fruitTimer = MathF.Max(0f, fruitTimer - deltaSeconds);
    }

    private void MoveGhost(ref Ghost ghost, float deltaSeconds)
    {
        if (ghost.State == GhostState.House)
        {
            ghost.ReleaseTimer -= deltaSeconds;
            if (ghost.ReleaseTimer <= 0f)
            {
                ghost.State = GhostState.Leaving;
            }
        }

        var tile = new Vector2(MathF.Round(ghost.Position.X), MathF.Round(ghost.Position.Y));
        var reached = ghost.Direction == Vector2.Zero || Vector2.Dot(ghost.Position - tile, ghost.Direction) >= 0f;
        if (reached && (tile != ghost.DecidedAt || ghost.Direction == Vector2.Zero))
        {
            ghost.DecidedAt = tile;
            if (ghost.Direction.X != 0f)
            {
                ghost.Position.Y = tile.Y;
            }
            else if (ghost.Direction.Y != 0f)
            {
                ghost.Position.X = tile.X;
            }
            else
            {
                ghost.Position = tile;
            }

            if (ghost.State == GhostState.Leaving && tile.Y <= DoorExit.Y)
            {
                ghost.State = frightTimer > 0f ? GhostState.Frightened : GhostState.Normal;
            }
            else if (ghost.State == GhostState.Eyes && Vector2.Distance(tile, HouseCentre) < HouseArrival)
            {
                ghost.State = GhostState.House;
                ghost.ReleaseTimer = EyesRelease;
            }

            var steered = ChooseDirection(ref ghost, tile);
            if (steered != ghost.Direction)
            {
                ghost.Position = tile;
            }

            ghost.Direction = steered;
        }

        ghost.Position = Wrap(ghost.Position + ghost.Direction * GhostSpeed(in ghost) * deltaSeconds);
    }

    private float GhostSpeed(in Ghost ghost)
    {
        switch (ghost.State)
        {
            case GhostState.Frightened:
                return FrightenedSpeed;
            case GhostState.House:
                return HouseSpeed;
            case GhostState.Leaving:
                return LeavingSpeed;
            case GhostState.Eyes:
                return EyesSpeed;
            default:
                return NormalGhostSpeed(Level);
        }
    }

    private Vector2 ChooseDirection(ref Ghost ghost, Vector2 tile)
    {
        var useDoor = ghost.State is GhostState.Leaving or GhostState.Eyes;
        var target = TargetFor(in ghost);
        var optionCount = 0;
        var best = Vector2.Zero;
        var bestDistance = float.MaxValue;
        for (var index = 0; index < DirectionOrder.Length; index++)
        {
            var candidate = DirectionOrder[index];
            if (ghost.Direction != Vector2.Zero && candidate == -ghost.Direction)
            {
                continue;
            }

            var next = tile + candidate;
            if (!Walkable((int)next.X, (int)next.Y, true, useDoor))
            {
                continue;
            }

            options[optionCount++] = candidate;
            var distance = Vector2.DistanceSquared(next, target);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        if (optionCount == 0)
        {
            var reverse = -ghost.Direction;
            var back = tile + reverse;
            return Walkable((int)back.X, (int)back.Y, true, useDoor) ? reverse : Vector2.Zero;
        }

        if (ghost.State == GhostState.Frightened ||
            (ghost.Personality == WandererPersonality && ghost.State == GhostState.Normal))
        {
            return options[random.Next(optionCount)];
        }

        return best;
    }

    private Vector2 TargetFor(in Ghost ghost)
    {
        switch (ghost.State)
        {
            case GhostState.Eyes:
            case GhostState.House:
                return HouseCentre;
            case GhostState.Leaving:
                return DoorExit;
        }

        switch (ghost.Personality)
        {
            case AmbusherPersonality:
                return PlayerPosition + PlayerDirection * AmbushLead;
            case ShyPersonality:
                return Vector2.Distance(ghost.Position, PlayerPosition) > ShyDistance ? PlayerPosition : ShyCorner;
            default:
                return PlayerPosition;
        }
    }

    private bool CheckCollisions()
    {
        for (var index = 0; index < GhostCount; index++)
        {
            ref var ghost = ref ghosts[index];
            if (ghost.State is GhostState.Eyes or GhostState.House)
            {
                continue;
            }

            if (Vector2.Distance(ghost.Position, PlayerPosition) > CollisionRadius)
            {
                continue;
            }

            if (ghost.State == GhostState.Frightened)
            {
                ghost.State = GhostState.Eyes;
                ghostChain = Math.Min(ghostChain + 1, MaxGhostChain);
                var points = ChainPoints(ghostChain);
                Score += points;
                GhostsEaten++;
                ghostEatPositions[GhostsEatenThisFrame] = ghost.Position;
                ghostEatPoints[GhostsEatenThisFrame] = points;
                GhostsEatenThisFrame++;
                continue;
            }

            Lives--;
            deathTimer = DeathSeconds;
            PlayerDiedThisFrame = true;
            return true;
        }

        return false;
    }

    private void TickFright(float deltaSeconds)
    {
        if (frightTimer <= 0f)
        {
            return;
        }

        frightTimer -= deltaSeconds;
        if (frightTimer > 0f)
        {
            return;
        }

        frightTimer = 0f;
        ghostChain = 0;
        for (var index = 0; index < GhostCount; index++)
        {
            if (ghosts[index].State == GhostState.Frightened)
            {
                ghosts[index].State = GhostState.Normal;
            }
        }
    }
}
