namespace Aetherphone.Apps.Games.Claim;

internal struct ClaimSpark
{
    public int Cell;
    public ClaimMove Heading;
    public float Travel;
    public bool Clockwise;
}

internal sealed partial class ClaimBoard
{
    public const int MaxSparks = 5;
    public const int BossHistory = 14;
    public const float BossTouchRadius = 1.6f;
    public const int SparkTouchCells = 1;
    private const float BossBaseSpeed = 15f;
    private const float BossSpeedPerLevel = 2.5f;
    private const float BossMaxSpeed = 38f;
    private const float BossMaxLength = 34f;
    private const float BossMinLength = 9f;
    private const float BossSteerRate = 3.2f;
    private const float BossTurnJitter = 14f;
    private const float BossMaxTurn = 4.5f;
    private const float BossSurgeRate = 2.2f;
    private const float BossSample = 0.5f;
    private const float BossHistoryInterval = 0.035f;
    private const float SparkBaseSpeed = 30f;
    private const float SparkSpeedPerLevel = 2f;
    private const float SparkMaxSpeed = 52f;
    private const float SparkChasePerLevel = 0.08f;
    private const float SparkMaxChase = 0.6f;
    private const float SparkStraightChance = 0.65f;
    private const int SparkSpacing = 24;

    private readonly ClaimSpark[] sparks = new ClaimSpark[MaxSparks];
    private readonly Vector2[] headHistory = new Vector2[BossHistory];
    private readonly Vector2[] tailHistory = new Vector2[BossHistory];
    private Vector2 bossHead;
    private Vector2 bossTail;
    private float angleHead;
    private float angleTail;
    private float turnHead;
    private float turnTail;
    private float bossPhase;
    private float historyTimer;
    private int historyCursor;
    private int historyCount;
    private int sparkCount;
    private bool bossFrozen;

    public Vector2 BossHead => bossHead;

    public Vector2 BossTail => bossTail;

    public float BossPhase => bossPhase;

    public int BossHistoryCount => historyCount;

    public int SparkCount => sparkCount;

    public float BossSpeed => MathF.Min(BossMaxSpeed, BossBaseSpeed + BossSpeedPerLevel * (Level - 1));

    public float SparkSpeed => MathF.Min(SparkMaxSpeed, SparkBaseSpeed + SparkSpeedPerLevel * (Level - 1));

    public static int SparkCountFor(int level) => Math.Min(MaxSparks, (level + 2) / 2);

    public Vector2 SparkPosition(int index) => CellCenter(sparks[index].Cell);

    public ClaimMove SparkHeading(int index) => sparks[index].Heading;

    public void BossHistoryAt(int age, out Vector2 head, out Vector2 tail)
    {
        var slot = (historyCursor - 1 - age + BossHistory * 2) % BossHistory;
        head = headHistory[slot];
        tail = tailHistory[slot];
    }

    internal void PlaceBoss(Vector2 head, Vector2 tail, bool frozen)
    {
        bossHead = head;
        bossTail = tail;
        bossFrozen = frozen;
        historyCount = 0;
    }

    internal void RemoveSparks()
    {
        sparkCount = 0;
    }

    internal void PlaceSpark(int column, int row, ClaimMove heading)
    {
        sparks[0] = new ClaimSpark { Cell = Index(column, row), Heading = heading, Clockwise = true };
        sparkCount = Math.Max(sparkCount, 1);
    }

    private void SpawnBoss()
    {
        var center = new Vector2(Width * 0.5f, Height * 0.42f);
        angleHead = random.NextFloat() * MathF.Tau;
        angleTail = angleHead + MathF.PI + random.Range(-0.6f, 0.6f);
        bossHead = center + new Vector2(MathF.Cos(angleHead), MathF.Sin(angleHead)) * (BossMinLength * 0.8f);
        bossTail = center + new Vector2(MathF.Cos(angleTail), MathF.Sin(angleTail)) * (BossMinLength * 0.8f);
        turnHead = 0f;
        turnTail = 0f;
        bossPhase = random.NextFloat() * MathF.Tau;
        historyCursor = 0;
        historyCount = 0;
        historyTimer = 0f;
        bossFrozen = false;
    }

    private void MoveBoss(float deltaSeconds)
    {
        bossPhase += BossSurgeRate * deltaSeconds;
        if (!bossFrozen)
        {
            var surge = 0.75f + 0.5f * (0.5f + 0.5f * MathF.Sin(bossPhase));
            var speed = BossSpeed * surge;
            var length = Vector2.Distance(bossHead, bossTail);
            if (length > BossMaxLength)
            {
                angleHead = SteerToward(angleHead, bossTail - bossHead, deltaSeconds);
                angleTail = SteerToward(angleTail, bossHead - bossTail, deltaSeconds);
            }
            else if (length < BossMinLength)
            {
                angleHead = SteerToward(angleHead, bossHead - bossTail, deltaSeconds);
                angleTail = SteerToward(angleTail, bossTail - bossHead, deltaSeconds);
            }

            MoveEndpoint(ref bossHead, ref angleHead, ref turnHead, speed, deltaSeconds);
            MoveEndpoint(ref bossTail, ref angleTail, ref turnTail, speed, deltaSeconds);
        }

        historyTimer -= deltaSeconds;
        if (historyTimer > 0f && historyCount > 0)
        {
            return;
        }

        historyTimer = BossHistoryInterval;
        headHistory[historyCursor] = bossHead;
        tailHistory[historyCursor] = bossTail;
        historyCursor = (historyCursor + 1) % BossHistory;
        historyCount = Math.Min(BossHistory, historyCount + 1);
    }

    private static float SteerToward(float angle, Vector2 direction, float deltaSeconds)
    {
        var wanted = MathF.Atan2(direction.Y, direction.X);
        var difference = MathF.IEEERemainder(wanted - angle, MathF.Tau);
        var step = BossSteerRate * deltaSeconds;
        return angle + Math.Clamp(difference, -step, step);
    }

    private void MoveEndpoint(ref Vector2 position, ref float angle, ref float turn, float speed, float deltaSeconds)
    {
        turn = Math.Clamp(turn + random.Range(-1f, 1f) * BossTurnJitter * deltaSeconds, -BossMaxTurn, BossMaxTurn);
        angle += turn * deltaSeconds;
        var velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed * deltaSeconds;
        var next = position + velocity;
        if (!BossPassable(next.X, position.Y))
        {
            angle = MathF.PI - angle;
            next.X = position.X;
        }

        if (!BossPassable(next.X, next.Y))
        {
            angle = -angle;
            next.Y = position.Y;
        }

        if (BossPassable(next.X, next.Y))
        {
            position = next;
        }
    }

    private bool BossPassable(float worldX, float worldY)
    {
        if (worldX < 0f || worldY < 0f)
        {
            return false;
        }

        var column = (int)worldX;
        var row = (int)worldY;
        return column < Width && row < Height && cells[Index(column, row)] != ClaimCell.Claimed;
    }

    private bool BossTouchesLine(out Vector2 contact)
    {
        var span = bossTail - bossHead;
        var length = span.Length();
        var samples = Math.Max(1, (int)MathF.Ceiling(length / BossSample));
        for (var sample = 0; sample <= samples; sample++)
        {
            var point = bossHead + span * (sample / (float)samples);
            var column = (int)point.X;
            var row = (int)point.Y;
            if ((uint)column >= Width || (uint)row >= Height)
            {
                continue;
            }

            if (cells[Index(column, row)] == ClaimCell.Trail)
            {
                contact = point;
                return true;
            }
        }

        var player = PlayerPosition;
        var closest = ClosestOnSegment(bossHead, bossTail, player);
        if (Vector2.DistanceSquared(closest, player) <= BossTouchRadius * BossTouchRadius)
        {
            contact = player;
            return true;
        }

        contact = default;
        return false;
    }

    private static Vector2 ClosestOnSegment(Vector2 start, Vector2 end, Vector2 point)
    {
        var span = end - start;
        var lengthSquared = span.LengthSquared();
        if (lengthSquared <= 1e-6f)
        {
            return start;
        }

        var along = Math.Clamp(Vector2.Dot(point - start, span) / lengthSquared, 0f, 1f);
        return start + span * along;
    }

    private int BossAnchorCell()
    {
        var first = CellOf(bossHead);
        if (first >= 0 && cells[first] == ClaimCell.Open)
        {
            return first;
        }

        var second = CellOf(bossTail);
        if (second >= 0 && cells[second] == ClaimCell.Open)
        {
            return second;
        }

        return LargestOpenRegion();
    }

    private void SettleBoss(int anchor)
    {
        if (anchor < 0)
        {
            return;
        }

        var first = CellOf(bossHead);
        if (first < 0 || cells[first] != ClaimCell.Open)
        {
            bossHead = CellCenter(anchor);
        }

        var second = CellOf(bossTail);
        if (second >= 0 && cells[second] == ClaimCell.Open)
        {
            return;
        }

        bossTail = bossHead;
    }

    private static int CellOf(Vector2 point)
    {
        if (point.X < 0f || point.Y < 0f)
        {
            return -1;
        }

        var column = (int)point.X;
        var row = (int)point.Y;
        return column < Width && row < Height ? Index(column, row) : -1;
    }

    private void SpawnSparks()
    {
        sparkCount = SparkCountFor(Level);
        for (var index = 0; index < sparkCount; index++)
        {
            var side = index % 2 == 0 ? -1 : 1;
            var column = Math.Clamp(Width / 2 + side * (index / 2) * SparkSpacing, 1, Width - 2);
            sparks[index] = new ClaimSpark
            {
                Cell = Index(column, 0),
                Heading = side < 0 ? ClaimMove.Left : ClaimMove.Right,
                Travel = 0f,
                Clockwise = side > 0,
            };
        }
    }

    private void ScatterSparks()
    {
        var far = FarthestWalkable(playerCell);
        for (var index = 0; index < sparkCount; index++)
        {
            ref var spark = ref sparks[index];
            spark.Cell = far;
            spark.Travel = -index * 0.6f;
            spark.Clockwise = index % 2 == 0;
            spark.Heading = FirstWalkableHeading(far, spark.Clockwise ? ClaimMove.Right : ClaimMove.Left);
        }
    }

    private void SettleSparks()
    {
        for (var index = 0; index < sparkCount; index++)
        {
            ref var spark = ref sparks[index];
            if (IsWalkable(spark.Cell))
            {
                continue;
            }

            spark.Cell = NearestWalkable(spark.Cell);
            spark.Heading = FirstWalkableHeading(spark.Cell, spark.Heading);
        }
    }

    private ClaimMove FirstWalkableHeading(int cell, ClaimMove preferred)
    {
        var heading = preferred == ClaimMove.None ? ClaimMove.Up : preferred;
        for (var turn = 0; turn < 4; turn++)
        {
            if (IsWalkable(Neighbour(cell, heading)))
            {
                return heading;
            }

            heading = TurnRight(heading);
        }

        return preferred;
    }

    private void MoveSparks(float deltaSeconds)
    {
        var speed = SparkSpeed;
        for (var index = 0; index < sparkCount; index++)
        {
            ref var spark = ref sparks[index];
            spark.Travel += speed * deltaSeconds;
            while (spark.Travel >= 1f)
            {
                spark.Travel -= 1f;
                StepSpark(ref spark);
            }
        }
    }

    private void StepSpark(ref ClaimSpark spark)
    {
        Span<ClaimMove> options = stackalloc ClaimMove[3];
        var straight = spark.Heading;
        var preferred = spark.Clockwise ? TurnRight(straight) : TurnLeft(straight);
        var other = Opposite(preferred);
        var count = 0;
        if (IsWalkable(Neighbour(spark.Cell, straight)))
        {
            options[count++] = straight;
        }

        if (IsWalkable(Neighbour(spark.Cell, preferred)))
        {
            options[count++] = preferred;
        }

        if (IsWalkable(Neighbour(spark.Cell, other)))
        {
            options[count++] = other;
        }

        ClaimMove chosen;
        if (count == 0)
        {
            var back = Opposite(straight);
            if (!IsWalkable(Neighbour(spark.Cell, back)))
            {
                return;
            }

            chosen = back;
        }
        else if (count == 1)
        {
            chosen = options[0];
        }
        else
        {
            chosen = ChooseSparkMove(spark.Cell, options[..count], straight);
        }

        spark.Heading = chosen;
        spark.Cell = Neighbour(spark.Cell, chosen);
    }

    private ClaimMove ChooseSparkMove(int cell, ReadOnlySpan<ClaimMove> options, ClaimMove straight)
    {
        var chase = MathF.Min(SparkMaxChase, SparkChasePerLevel * Level);
        if (random.Chance(chase))
        {
            var best = options[0];
            var bestDistance = int.MaxValue;
            var playerColumn = ColumnOf(playerCell);
            var playerRow = RowOf(playerCell);
            for (var index = 0; index < options.Length; index++)
            {
                var next = Neighbour(cell, options[index]);
                var distance = Math.Abs(ColumnOf(next) - playerColumn) + Math.Abs(RowOf(next) - playerRow);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                best = options[index];
            }

            return best;
        }

        if (options[0] == straight && random.Chance(SparkStraightChance))
        {
            return straight;
        }

        return options[random.Next(options.Length)];
    }

    private bool SparkTouchesPlayer()
    {
        var playerColumn = ColumnOf(playerCell);
        var playerRow = RowOf(playerCell);
        for (var index = 0; index < sparkCount; index++)
        {
            var cell = sparks[index].Cell;
            if (Math.Abs(ColumnOf(cell) - playerColumn) <= SparkTouchCells &&
                Math.Abs(RowOf(cell) - playerRow) <= SparkTouchCells)
            {
                return true;
            }
        }

        return false;
    }
}
