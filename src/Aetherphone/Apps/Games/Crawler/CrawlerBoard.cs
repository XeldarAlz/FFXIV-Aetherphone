using Aetherphone.Apps.Games.Framework;

namespace Aetherphone.Apps.Games.Crawler;

internal readonly struct CrawlerControls
{
    public readonly float MoveX;
    public readonly float MoveY;
    public readonly bool Fire;
    public readonly bool Steering;
    public readonly Vector2 Target;

    public CrawlerControls(float moveX, float moveY, bool fire, bool steering, Vector2 target)
    {
        MoveX = Math.Clamp(moveX, -1f, 1f);
        MoveY = Math.Clamp(moveY, -1f, 1f);
        Fire = fire;
        Steering = steering;
        Target = target;
    }
}

internal struct Segment
{
    public int Column;
    public int Row;
    public int PreviousColumn;
    public int PreviousRow;
    public int StepX;
    public int StepY;
    public int Ahead;
    public int Behind;
    public bool Alive;

    public readonly bool Head => Ahead < 0;
}

internal readonly struct SegmentHit
{
    public readonly Vector2 Position;
    public readonly bool Head;
    public readonly int Points;

    public SegmentHit(Vector2 position, bool head, int points)
    {
        Position = position;
        Head = head;
        Points = points;
    }
}

internal sealed class CrawlerBoard
{
    public const int Columns = 15;
    public const int Rows = 22;
    public const int CellCount = Columns * Rows;
    public const int ZoneRows = 5;
    public const int ZoneTop = Rows - ZoneRows;
    public const int MushroomHealth = 4;
    public const int MaxSegments = 32;
    public const int ChainLength = 10;
    public const int MaxExtraHeads = 4;
    public const int BodyPoints = 10;
    public const int HeadPoints = 100;
    public const int MushroomPoints = 1;
    public const int RegrowPoints = 5;
    public const int FleaPoints = 200;
    public const int FleaHits = 2;
    public const int FleaFirstWave = 2;
    public const int FleaThreshold = 5;
    public const int StartLives = 3;
    public const int MaxLives = 6;
    public const int ExtraLifeScore = 12000;
    public const float PlayerRadius = 0.38f;
    public const float PlayerSpeed = 11f;
    public const float SteerSpeed = 20f;
    public const float BulletSpeed = 46f;
    public const float SegmentReach = 0.62f;
    public const float SpiderReach = 0.8f;
    public const float FleaReach = 0.7f;
    public const float DeathSeconds = 1.4f;
    public const float WaveDelay = 1.2f;
    public const float FieldDensity = 0.11f;
    public const float SpiderFirstDelay = 3f;
    public const float FleaSpeed = 9f;
    public const float FleaSpeedHit = 18f;
    public const float FleaDropChance = 0.32f;
    private const float StepSeconds = 1f / 240f;
    private const float BulletLength = 0.6f;
    private const float SpiderSpeed = 2.4f;
    private const float SpiderClimb = 3.8f;
    private const float SpiderEatChance = 0.35f;
    private const float FleaCheckSeconds = 2.5f;
    private const int MaxChips = 8;
    private const int MaxHits = 16;

    private readonly byte[] mushrooms = new byte[CellCount];
    private readonly Segment[] segments = new Segment[MaxSegments];
    private readonly SegmentHit[] hits = new SegmentHit[MaxHits];
    private readonly Vector2[] chips = new Vector2[MaxChips];
    private readonly int[] regrown = new int[CellCount];
    private GameRandom random;
    private ComboMeter combo = ComboMeter.Create();
    private float tickTimer;
    private float deathTimer;
    private float waveTimer;
    private bool waveClearing;
    private bool zoneBreached;
    private int nextExtraLife;
    private float spiderTimer;
    private float spiderHopTimer;
    private Vector2 spiderVelocity;
    private int spiderCell = -1;
    private float fleaTimer;
    private int fleaHitsTaken;
    private int fleaRow;

    public int Score { get; private set; }

    public int Lives { get; private set; }

    public int Wave { get; private set; }

    public bool GameOver { get; private set; }

    public bool Dying => deathTimer > 0f;

    public float PlaySeconds { get; private set; }

    public Vector2 Player { get; private set; }

    public bool HasBullet { get; private set; }

    public Vector2 Bullet { get; private set; }

    public bool SpiderActive { get; private set; }

    public Vector2 Spider { get; private set; }

    public bool FleaActive { get; private set; }

    public Vector2 Flea { get; private set; }

    public bool FleaWounded => fleaHitsTaken > 0;

    public float TickSeconds { get; private set; }

    public float Alpha => TickSeconds <= 0f ? 1f : Math.Clamp(tickTimer / TickSeconds, 0f, 1f);

    public int ShotsFired { get; private set; }

    public int ShotsHit { get; private set; }

    public int SegmentsShot { get; private set; }

    public int SpidersShot { get; private set; }

    public int FleasShot { get; private set; }

    public int WavesCleared { get; private set; }

    public int HitCount { get; private set; }

    public int ChipCount { get; private set; }

    public int RegrownCount { get; private set; }

    public bool ShotFiredThisFrame { get; private set; }

    public bool SpiderArrivedThisFrame { get; private set; }

    public bool SpiderShotThisFrame { get; private set; }

    public int SpiderPoints { get; private set; }

    public Vector2 SpiderShotPosition { get; private set; }

    public bool FleaArrivedThisFrame { get; private set; }

    public bool FleaWoundedThisFrame { get; private set; }

    public bool FleaShotThisFrame { get; private set; }

    public Vector2 FleaShotPosition { get; private set; }

    public bool FleaDroppedThisFrame { get; private set; }

    public bool PlayerLostThisFrame { get; private set; }

    public bool RegrowThisFrame { get; private set; }

    public bool ExtraLifeThisFrame { get; private set; }

    public bool WaveClearedThisFrame { get; private set; }

    public bool WaveStartedThisFrame { get; private set; }

    public bool ZoneBreachedThisFrame { get; private set; }

    public bool ComboRaisedThisFrame { get; private set; }

    public ComboMeter Combo => combo;

    public int SegmentCapacity => MaxSegments;

    public ref readonly Segment SegmentAt(int index) => ref segments[index];

    public SegmentHit HitAt(int index) => hits[index];

    public Vector2 ChipAt(int index) => chips[index];

    public int RegrownCell(int index) => regrown[index];

    public static int CellIndex(int column, int row) => row * Columns + column;

    public static bool InField(int column, int row) => column >= 0 && column < Columns && row >= 0 && row < Rows;

    public static Vector2 CellCenter(int column, int row) => new(column + 0.5f, row + 0.5f);

    public static float TickFor(int wave) => MathF.Max(0.07f, 0.2f - (Math.Max(1, wave) - 1) * 0.013f);

    public static int ExtraHeadsFor(int wave) => Math.Min(Math.Max(0, wave - 1), MaxExtraHeads);

    public static int SpiderPointsFor(float distance) => distance < 2f ? 900 : distance < 3.5f ? 600 : 300;

    public int MushroomAt(int column, int row) => InField(column, row) ? mushrooms[CellIndex(column, row)] : 0;

    public int AliveSegments
    {
        get
        {
            var count = 0;
            for (var index = 0; index < MaxSegments; index++)
            {
                if (segments[index].Alive)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public int MushroomsInZone
    {
        get
        {
            var count = 0;
            for (var row = ZoneTop; row < Rows; row++)
            {
                for (var column = 0; column < Columns; column++)
                {
                    if (mushrooms[CellIndex(column, row)] > 0)
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }

    public Vector2 SegmentPosition(int index)
    {
        ref readonly var segment = ref segments[index];
        var alpha = Alpha;
        return new Vector2(segment.PreviousColumn + (segment.Column - segment.PreviousColumn) * alpha + 0.5f,
            segment.PreviousRow + (segment.Row - segment.PreviousRow) * alpha + 0.5f);
    }

    public void Reset(GameRandom seededRandom)
    {
        random = seededRandom;
        combo.Reset();
        Score = 0;
        Lives = StartLives;
        GameOver = false;
        PlaySeconds = 0f;
        ShotsFired = 0;
        ShotsHit = 0;
        SegmentsShot = 0;
        SpidersShot = 0;
        FleasShot = 0;
        WavesCleared = 0;
        nextExtraLife = ExtraLifeScore;
        deathTimer = 0f;
        ClearFrameEvents();
        SeedField();
        PlacePlayer();
        StartWave(1);
    }

    public void ClearField()
    {
        Array.Clear(mushrooms);
    }

    public void ClearCrawlers()
    {
        for (var index = 0; index < MaxSegments; index++)
        {
            segments[index].Alive = false;
        }
    }

    public void PlaceMushroom(int column, int row)
    {
        if (!InField(column, row))
        {
            return;
        }

        mushrooms[CellIndex(column, row)] = MushroomHealth;
    }

    public int SpawnChain(int column, int row, int length, int stepX)
    {
        var ahead = -1;
        var head = -1;
        for (var link = 0; link < length; link++)
        {
            var index = FreeSegment();
            if (index < 0)
            {
                break;
            }

            ref var segment = ref segments[index];
            segment.Column = column - stepX * link;
            segment.Row = row;
            segment.PreviousColumn = segment.Column;
            segment.PreviousRow = row;
            segment.StepX = stepX;
            segment.StepY = 1;
            segment.Ahead = ahead;
            segment.Behind = -1;
            segment.Alive = true;
            if (ahead >= 0)
            {
                segments[ahead].Behind = index;
            }
            else
            {
                head = index;
            }

            ahead = index;
        }

        return head;
    }

    public void LaunchFlea(int column)
    {
        FleaActive = true;
        fleaHitsTaken = 0;
        fleaRow = -1;
        Flea = new Vector2(column + 0.5f, -0.5f);
        FleaArrivedThisFrame = true;
    }

    public void Step(float deltaSeconds, in CrawlerControls controls)
    {
        ClearFrameEvents();
        if (GameOver || deltaSeconds <= 0f)
        {
            return;
        }

        var multiplierBefore = combo.Multiplier;
        PlaySeconds += deltaSeconds;
        combo.Update(deltaSeconds);
        if (deathTimer > 0f)
        {
            deathTimer -= deltaSeconds;
            if (deathTimer <= 0f)
            {
                Recover();
            }

            return;
        }

        var substeps = new Substeps(deltaSeconds, StepSeconds);
        for (var step = 0; step < substeps.Count; step++)
        {
            StepOnce(substeps.Step, controls);
            if (deathTimer > 0f)
            {
                break;
            }
        }

        AdvanceWave(deltaSeconds);
        ComboRaisedThisFrame = combo.Multiplier > multiplierBefore;
    }

    public void AdvanceCrawlers()
    {
        for (var index = 0; index < MaxSegments; index++)
        {
            ref var segment = ref segments[index];
            segment.PreviousColumn = segment.Column;
            segment.PreviousRow = segment.Row;
        }

        for (var index = 0; index < MaxSegments; index++)
        {
            ref var segment = ref segments[index];
            if (!segment.Alive)
            {
                continue;
            }

            if (segment.Head)
            {
                MoveHead(ref segment);
            }
            else
            {
                ref readonly var leader = ref segments[segment.Ahead];
                segment.Column = leader.PreviousColumn;
                segment.Row = leader.PreviousRow;
            }

            var movedX = segment.Column - segment.PreviousColumn;
            var movedY = segment.Row - segment.PreviousRow;
            if (movedX != 0)
            {
                segment.StepX = Math.Sign(movedX);
            }

            if (movedY != 0)
            {
                segment.StepY = Math.Sign(movedY);
            }

            if (segment.Row >= ZoneTop && !zoneBreached)
            {
                zoneBreached = true;
                ZoneBreachedThisFrame = true;
            }
        }
    }

    public void HitSegment(int index)
    {
        ref var segment = ref segments[index];
        if (!segment.Alive)
        {
            return;
        }

        var alpha = Alpha;
        var column = alpha < 0.5f ? segment.PreviousColumn : segment.Column;
        var row = alpha < 0.5f ? segment.PreviousRow : segment.Row;
        var position = SegmentPosition(index);
        var head = segment.Head;
        segment.Alive = false;
        if (segment.Ahead >= 0)
        {
            segments[segment.Ahead].Behind = -1;
        }

        if (segment.Behind >= 0)
        {
            segments[segment.Behind].Ahead = -1;
        }

        if (InField(column, row))
        {
            mushrooms[CellIndex(column, row)] = MushroomHealth;
        }

        SegmentsShot++;
        var points = (head ? HeadPoints : BodyPoints) * combo.Hit();
        AddPoints(points);
        if (HitCount < MaxHits)
        {
            hits[HitCount++] = new SegmentHit(position, head, points);
        }
    }

    private void ClearFrameEvents()
    {
        HitCount = 0;
        ChipCount = 0;
        RegrownCount = 0;
        ShotFiredThisFrame = false;
        SpiderArrivedThisFrame = false;
        SpiderShotThisFrame = false;
        FleaArrivedThisFrame = false;
        FleaWoundedThisFrame = false;
        FleaShotThisFrame = false;
        FleaDroppedThisFrame = false;
        PlayerLostThisFrame = false;
        RegrowThisFrame = false;
        ExtraLifeThisFrame = false;
        WaveClearedThisFrame = false;
        WaveStartedThisFrame = false;
        ZoneBreachedThisFrame = false;
        ComboRaisedThisFrame = false;
    }

    private void SeedField()
    {
        Array.Clear(mushrooms);
        for (var row = 1; row < ZoneTop; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                if (random.Chance(FieldDensity))
                {
                    mushrooms[CellIndex(column, row)] = MushroomHealth;
                }
            }
        }

        for (var column = 0; column < Columns; column++)
        {
            if (random.Chance(FieldDensity * 0.6f))
            {
                mushrooms[CellIndex(column, ZoneTop + random.Next(ZoneRows - 1))] = MushroomHealth;
            }
        }
    }

    private void PlacePlayer()
    {
        Player = new Vector2(Columns * 0.5f, Rows - 0.6f);
        HasBullet = false;
        var column = (int)Player.X;
        mushrooms[CellIndex(column, Rows - 1)] = 0;
    }

    private void StartWave(int wave)
    {
        Wave = wave;
        TickSeconds = TickFor(wave);
        tickTimer = 0f;
        waveClearing = false;
        zoneBreached = false;
        WaveStartedThisFrame = true;
        ClearCrawlers();
        var extraHeads = ExtraHeadsFor(wave);
        SpawnChain(Columns / 2, 0, ChainLength, random.Sign());
        for (var head = 0; head < extraHeads; head++)
        {
            SpawnChain(random.Next(Columns), 0, 1, random.Sign());
        }

        SpiderActive = false;
        spiderTimer = SpiderFirstDelay + random.Range(0f, 2f);
        FleaActive = false;
        fleaTimer = FleaCheckSeconds;
    }

    private int FreeSegment()
    {
        for (var index = 0; index < MaxSegments; index++)
        {
            if (!segments[index].Alive)
            {
                return index;
            }
        }

        return -1;
    }

    private void StepOnce(float step, in CrawlerControls controls)
    {
        MovePlayer(step, controls);
        if (controls.Fire)
        {
            Fire();
        }

        if (!waveClearing)
        {
            tickTimer += step;
            while (tickTimer >= TickSeconds)
            {
                tickTimer -= TickSeconds;
                AdvanceCrawlers();
            }
        }

        AdvanceSpider(step);
        AdvanceFlea(step);
        MoveBullet(step);
        CollidePlayer();
    }

    private void MovePlayer(float step, in CrawlerControls controls)
    {
        var velocity = new Vector2(controls.MoveX, controls.MoveY) * PlayerSpeed;
        if (controls.Steering)
        {
            var toTarget = ClampToZone(controls.Target) - Player;
            var distance = toTarget.Length();
            var reach = SteerSpeed * step;
            velocity = distance <= reach ? toTarget / step : toTarget / distance * SteerSpeed;
        }

        var position = Player;
        var movedX = ClampToZone(new Vector2(position.X + velocity.X * step, position.Y));
        if (!TouchesMushroom(movedX))
        {
            position = movedX;
        }

        var movedY = ClampToZone(new Vector2(position.X, position.Y + velocity.Y * step));
        if (!TouchesMushroom(movedY))
        {
            position = movedY;
        }

        Player = position;
    }

    private static Vector2 ClampToZone(Vector2 point) =>
        new(Math.Clamp(point.X, PlayerRadius, Columns - PlayerRadius),
            Math.Clamp(point.Y, ZoneTop + PlayerRadius, Rows - PlayerRadius));

    private bool TouchesMushroom(Vector2 center)
    {
        var minColumn = (int)MathF.Floor(center.X - PlayerRadius);
        var maxColumn = (int)MathF.Floor(center.X + PlayerRadius);
        var minRow = (int)MathF.Floor(center.Y - PlayerRadius);
        var maxRow = (int)MathF.Floor(center.Y + PlayerRadius);
        for (var row = minRow; row <= maxRow; row++)
        {
            for (var column = minColumn; column <= maxColumn; column++)
            {
                if (MushroomAt(column, row) == 0)
                {
                    continue;
                }

                var nearest = new Vector2(Math.Clamp(center.X, column, column + 1f), Math.Clamp(center.Y, row, row + 1f));
                if (Vector2.DistanceSquared(nearest, center) < PlayerRadius * PlayerRadius * 0.8f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void Fire()
    {
        if (HasBullet)
        {
            return;
        }

        HasBullet = true;
        Bullet = Player - new Vector2(0f, PlayerRadius + 0.1f);
        ShotsFired++;
        ShotFiredThisFrame = true;
    }

    private void MoveHead(ref Segment segment)
    {
        var nextColumn = segment.Column + segment.StepX;
        if (nextColumn >= 0 && nextColumn < Columns && MushroomAt(nextColumn, segment.Row) == 0)
        {
            segment.Column = nextColumn;
            return;
        }

        if (segment.Column < 0 || segment.Column >= Columns)
        {
            segment.Column += segment.StepX;
            return;
        }

        var nextRow = segment.Row + segment.StepY;
        if (nextRow >= Rows)
        {
            segment.StepY = -1;
            nextRow = segment.Row - 1;
        }
        else if (segment.StepY < 0 && nextRow < ZoneTop)
        {
            segment.StepY = 1;
            nextRow = segment.Row + 1;
        }

        segment.Row = nextRow;
        segment.StepX = -segment.StepX;
    }

    private void MoveBullet(float step)
    {
        if (!HasBullet)
        {
            return;
        }

        var bullet = Bullet - new Vector2(0f, BulletSpeed * step);
        Bullet = bullet;
        if (bullet.Y < -BulletLength)
        {
            HasBullet = false;
            return;
        }

        for (var index = 0; index < MaxSegments; index++)
        {
            if (!segments[index].Alive)
            {
                continue;
            }

            var position = SegmentPosition(index);
            if (MathF.Abs(position.X - bullet.X) > 0.5f || MathF.Abs(position.Y - bullet.Y) > 0.55f)
            {
                continue;
            }

            HasBullet = false;
            ShotsHit++;
            HitSegment(index);
            return;
        }

        if (SpiderActive && Vector2.Distance(Spider, bullet) < SpiderReach)
        {
            HasBullet = false;
            ShotsHit++;
            ShootSpider();
            return;
        }

        if (FleaActive && Vector2.Distance(Flea, bullet) < FleaReach)
        {
            HasBullet = false;
            ShotsHit++;
            ShootFlea();
            return;
        }

        var column = (int)MathF.Floor(bullet.X);
        var row = (int)MathF.Floor(bullet.Y);
        if (MushroomAt(column, row) == 0)
        {
            return;
        }

        HasBullet = false;
        DamageMushroom(column, row);
    }

    private void DamageMushroom(int column, int row)
    {
        var index = CellIndex(column, row);
        mushrooms[index]--;
        if (ChipCount < MaxChips)
        {
            chips[ChipCount++] = CellCenter(column, row);
        }

        if (mushrooms[index] == 0)
        {
            AddPoints(MushroomPoints);
        }
    }

    private void AdvanceSpider(float step)
    {
        if (!SpiderActive)
        {
            spiderTimer -= step;
            if (spiderTimer > 0f || waveClearing)
            {
                return;
            }

            LaunchSpider();
            return;
        }

        var position = Spider + spiderVelocity * step;
        var top = ZoneTop - 1.5f;
        var bottom = Rows - 0.5f;
        if (position.Y < top)
        {
            position.Y = top;
            spiderVelocity.Y = MathF.Abs(spiderVelocity.Y);
        }
        else if (position.Y > bottom)
        {
            position.Y = bottom;
            spiderVelocity.Y = -MathF.Abs(spiderVelocity.Y);
        }

        Spider = position;
        spiderHopTimer -= step;
        if (spiderHopTimer <= 0f)
        {
            spiderHopTimer = random.Range(0.35f, 0.9f);
            spiderVelocity.Y = random.Sign() * random.Range(0.7f, 1f) * SpiderClimb;
        }

        var column = (int)MathF.Floor(position.X);
        var row = (int)MathF.Floor(position.Y);
        var cell = InField(column, row) ? CellIndex(column, row) : -1;
        if (cell != spiderCell)
        {
            spiderCell = cell;
            if (cell >= 0 && mushrooms[cell] > 0 && random.Chance(SpiderEatChance))
            {
                mushrooms[cell] = 0;
            }
        }

        if (position.X < -1.5f || position.X > Columns + 1.5f)
        {
            SpiderActive = false;
            spiderTimer = random.Range(4f, 8f);
        }
    }

    private void LaunchSpider()
    {
        var fromLeft = random.Next(2) == 0;
        var speed = SpiderSpeed * (1f + Math.Min(Wave - 1, 8) * 0.08f);
        Spider = new Vector2(fromLeft ? -1f : Columns + 1f, random.Range(ZoneTop, Rows - 1f));
        spiderVelocity = new Vector2(fromLeft ? speed : -speed, random.Sign() * SpiderClimb);
        spiderHopTimer = random.Range(0.35f, 0.9f);
        spiderCell = -1;
        SpiderActive = true;
        SpiderArrivedThisFrame = true;
    }

    private void ShootSpider()
    {
        SpiderActive = false;
        SpidersShot++;
        SpiderPoints = SpiderPointsFor(Vector2.Distance(Spider, Player));
        AddPoints(SpiderPoints);
        SpiderShotPosition = Spider;
        SpiderShotThisFrame = true;
        spiderTimer = random.Range(4f, 8f);
    }

    private void AdvanceFlea(float step)
    {
        if (!FleaActive)
        {
            if (Wave < FleaFirstWave || waveClearing)
            {
                return;
            }

            fleaTimer -= step;
            if (fleaTimer > 0f)
            {
                return;
            }

            fleaTimer = FleaCheckSeconds;
            if (MushroomsInZone < FleaThreshold)
            {
                LaunchFlea(random.Next(Columns));
            }

            return;
        }

        var speed = fleaHitsTaken > 0 ? FleaSpeedHit : FleaSpeed;
        var position = Flea + new Vector2(0f, speed * step);
        Flea = position;
        var row = (int)MathF.Floor(position.Y);
        if (row != fleaRow)
        {
            fleaRow = row;
            var column = (int)MathF.Floor(position.X);
            if (row >= 1 && row < Rows - 1 && MushroomAt(column, row) == 0 && random.Chance(FleaDropChance))
            {
                mushrooms[CellIndex(column, row)] = MushroomHealth;
                FleaDroppedThisFrame = true;
            }
        }

        if (position.Y > Rows + 0.5f)
        {
            FleaActive = false;
        }
    }

    private void ShootFlea()
    {
        fleaHitsTaken++;
        if (fleaHitsTaken < FleaHits)
        {
            FleaWoundedThisFrame = true;
            return;
        }

        FleaActive = false;
        FleasShot++;
        AddPoints(FleaPoints);
        FleaShotPosition = Flea;
        FleaShotThisFrame = true;
    }

    private void CollidePlayer()
    {
        for (var index = 0; index < MaxSegments; index++)
        {
            if (!segments[index].Alive)
            {
                continue;
            }

            if (Vector2.Distance(SegmentPosition(index), Player) < SegmentReach)
            {
                LosePlayer();
                return;
            }
        }

        if (SpiderActive && Vector2.Distance(Spider, Player) < SpiderReach)
        {
            LosePlayer();
            return;
        }

        if (FleaActive && Vector2.Distance(Flea, Player) < FleaReach)
        {
            LosePlayer();
        }
    }

    private void LosePlayer()
    {
        Lives--;
        HasBullet = false;
        deathTimer = DeathSeconds;
        combo.Reset();
        PlayerLostThisFrame = true;
    }

    private void Recover()
    {
        deathTimer = 0f;
        RegrowThisFrame = true;
        for (var index = 0; index < CellCount; index++)
        {
            if (mushrooms[index] == 0 || mushrooms[index] == MushroomHealth)
            {
                continue;
            }

            mushrooms[index] = MushroomHealth;
            regrown[RegrownCount++] = index;
            AddPoints(RegrowPoints);
        }

        if (Lives <= 0)
        {
            GameOver = true;
            return;
        }

        PlacePlayer();
        StartWave(Wave);
    }

    private void AdvanceWave(float deltaSeconds)
    {
        if (!waveClearing)
        {
            if (AliveSegments > 0)
            {
                return;
            }

            waveClearing = true;
            waveTimer = WaveDelay;
            WavesCleared++;
            WaveClearedThisFrame = true;
            return;
        }

        waveTimer -= deltaSeconds;
        if (waveTimer > 0f)
        {
            return;
        }

        StartWave(Wave + 1);
    }

    private void AddPoints(int points)
    {
        Score += points;
        while (Score >= nextExtraLife)
        {
            nextExtraLife += ExtraLifeScore;
            if (Lives >= MaxLives)
            {
                continue;
            }

            Lives++;
            ExtraLifeThisFrame = true;
        }
    }
}
