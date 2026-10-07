using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.World;

namespace Aetherphone.Apps.Games.Siege;

internal sealed class SiegeBoard
{
    public const int EnemyCapacity = 96;
    public const int SeedCapacity = 48;
    public const int MoteCapacity = 24;
    public const int EventCapacity = 256;
    private const int WaveCapacity = 256;
    private const int DueCapacity = 32;
    private const float SquashDecay = 6f;
    private const float RecoilDecay = 5f;
    private const float FlashDecay = 6f;
    private const float MoteHopHeight = 0.45f;
    private const float MoteHopSpread = 0.32f;
    private const float MoteHopDrop = 0.3f;
    private const float SkyMoteEdge = 0.6f;
    private const float SkyMoteLandTop = 1.2f;
    private const float SkyMoteLandBottom = SiegeRules.Rows - 1.2f;
    private const float SkyMoteStart = SiegeRules.SpawnY - 0.3f;
    private const float GardenSurfaceMargin = 0.3f;

    private readonly LaneGrid grid = new(SiegeRules.Columns, SiegeRules.Rows);
    private readonly SiegeDefender[] defenders = new SiegeDefender[SiegeRules.CellCount];
    private readonly SiegeEnemy[] enemies = new SiegeEnemy[EnemyCapacity];
    private readonly SiegeSeed[] seeds = new SiegeSeed[SeedCapacity];
    private readonly SiegeMote[] motes = new SiegeMote[MoteCapacity];
    private readonly SiegeEvent[] events = new SiegeEvent[EventCapacity];
    private readonly float[] cooldowns = new float[SiegeRules.DefenderKinds + 1];
    private readonly float[] columnTop = new float[SiegeRules.Columns];
    private readonly WaveSpawner spawner = new(SiegeRules.Columns, WaveCapacity);
    private readonly WaveEntry[] waveBuffer = new WaveEntry[WaveCapacity];
    private readonly WaveEntry[] due = new WaveEntry[DueCapacity];
    private GameRandom random;
    private FixedStepClock clock = new(SiegeRules.StepSeconds, SiegeRules.MaxCatchUpSeconds);
    private DripEconomy economy;
    private int eventCount;
    private int nextId;
    private int aliveCount;
    private int seenKinds;
    private float phaseTimer;
    private float skyTimer;
    private float healthScale = 1f;

    public SiegeBoard()
    {
        StartEndless(GameRandom.FromSeed(1));
    }

    public int Level { get; private set; }

    public bool Endless { get; private set; }

    public SiegePhase Phase { get; private set; }

    public int Wave { get; private set; }

    public int WaveCount { get; private set; }

    public int WavesSurvived { get; private set; }

    public int Health { get; private set; }

    public int Defeated { get; private set; }

    public int Planted { get; private set; }

    public int SunGathered { get; private set; }

    public int WaveSpent { get; private set; }

    public float Time { get; private set; }

    public Vector2 LastKill { get; private set; }

    public int AliveEnemies => aliveCount;

    public int Sunlight => economy.Whole;

    public float SunAmount => economy.Amount;

    public bool Over => Phase is SiegePhase.Won or SiegePhase.Lost;

    public bool Won => Phase == SiegePhase.Won;

    public int Stars => Won ? SiegeRules.Stars(Health, SiegeRules.GardenHealth) : 0;

    public float PhaseTimer => phaseTimer;

    public int WaveSize => spawner.Count;

    public int WaveSpawned => spawner.Spawned;

    public bool FinalWave => !Endless && Wave >= WaveCount - 1;

    public float WaveProgress => spawner.Count == 0 ? 1f : spawner.Spawned / (float)spawner.Count;

    public ReadOnlySpan<SiegeEvent> Events => events.AsSpan(0, eventCount);

    public static Vector2 CellCenter(int column, int row) => new(column + 0.5f, row + 0.5f);

    public static Vector2 MotePosition(in SiegeMote mote)
    {
        var progress = mote.Travel <= 0f ? 1f : Math.Clamp(mote.Age / mote.Travel, 0f, 1f);
        var position = Vector2.Lerp(mote.From, mote.To, mote.Sky ? progress : 1f - (1f - progress) * (1f - progress));
        if (!mote.Sky)
        {
            position.Y -= MathF.Sin(progress * MathF.PI) * MoteHopHeight;
        }

        return position;
    }

    public ref readonly SiegeDefender Defender(int cell) => ref defenders[cell];

    public ref readonly SiegeDefender DefenderAt(int column, int row) =>
        ref defenders[SiegeRules.CellIndex(column, row)];

    public ref readonly SiegeEnemy Enemy(int index) => ref enemies[index];

    public ref readonly SiegeSeed Seed(int index) => ref seeds[index];

    public ref readonly SiegeMote Mote(int index) => ref motes[index];

    public float Cooldown(DefenderKind kind) => cooldowns[(int)kind];

    public float CooldownFraction(DefenderKind kind)
    {
        var total = SiegeRules.Cooldown(kind);
        return total <= 0f ? 0f : Math.Clamp(cooldowns[(int)kind] / total, 0f, 1f);
    }

    public bool Ready(DefenderKind kind) => cooldowns[(int)kind] <= 0f;

    public bool Affordable(DefenderKind kind) => economy.CanAfford(SiegeRules.Cost(kind));

    public bool Unlocked(DefenderKind kind) =>
        kind != DefenderKind.None && (Endless || SiegeLevels.UnlockLevel(kind) <= Level);

    public void StartCampaign(GameRandom seeded, int level)
    {
        Reset(seeded);
        Level = Math.Clamp(level, 1, SiegeLevels.Count);
        Endless = false;
        WaveCount = SiegeLevels.WaveCount(Level);
        healthScale = SiegeRules.CampaignHealthScale(Level);
        economy = new DripEconomy(SiegeRules.CampaignSunlight, SiegeRules.BaseDrip, SiegeRules.SunCap);
    }

    public void StartEndless(GameRandom seeded)
    {
        Reset(seeded);
        Level = 0;
        Endless = true;
        WaveCount = 0;
        healthScale = 1f;
        economy = new DripEconomy(SiegeRules.EndlessSunlight, SiegeRules.BaseDrip, SiegeRules.SunCap);
    }

    public void BeginFrame()
    {
        eventCount = 0;
    }

    public int Step(float deltaSeconds)
    {
        var ticks = clock.Advance(deltaSeconds);
        for (var tick = 0; tick < ticks; tick++)
        {
            Tick();
        }

        return ticks;
    }

    public void Tick()
    {
        const float step = SiegeRules.StepSeconds;
        Time += step;
        if (Over)
        {
            DecayCosmetics(step);
            return;
        }

        economy.Advance(step);
        for (var kind = 1; kind < cooldowns.Length; kind++)
        {
            cooldowns[kind] = MathF.Max(0f, cooldowns[kind] - step);
        }

        AdvancePhase(step);
        AdvanceSky(step);
        RefreshColumnTops();
        AdvanceDefenders(step);
        AdvanceSeeds(step);
        AdvanceEnemies(step);
        AdvanceMotes(step);
        CheckOutcome();
    }

    public PlantResult CanPlant(DefenderKind kind, int column, int row)
    {
        if (Over)
        {
            return PlantResult.Closed;
        }

        if (!Unlocked(kind))
        {
            return PlantResult.Locked;
        }

        if (!grid.InBounds(column, row))
        {
            return PlantResult.OutOfBounds;
        }

        if (!grid.CanPlace(column, row))
        {
            return PlantResult.Occupied;
        }

        if (!Ready(kind))
        {
            return PlantResult.CoolingDown;
        }

        return Affordable(kind) ? PlantResult.Planted : PlantResult.Unaffordable;
    }

    public PlantResult Plant(DefenderKind kind, int column, int row)
    {
        var result = CanPlant(kind, column, row);
        if (result != PlantResult.Planted)
        {
            return result;
        }

        economy.TrySpend(SiegeRules.Cost(kind));
        cooldowns[(int)kind] = SiegeRules.Cooldown(kind);
        Place(kind, column, row);
        Planted++;
        Emit(SiegeEventKind.DefenderPlanted, CellCenter(column, row), column, row, (byte)kind, SiegeRules.Cost(kind));
        return PlantResult.Planted;
    }

    public bool Place(DefenderKind kind, int column, int row)
    {
        var cell = SiegeRules.CellIndex(column, row);
        if (!grid.Place(column, row, (byte)kind, cell))
        {
            return false;
        }

        var health = SiegeRules.Health(kind);
        defenders[cell] = new SiegeDefender
        {
            Kind = kind,
            Health = health,
            MaxHealth = health,
            Timer = InitialTimer(kind),
        };
        return true;
    }

    public bool Dig(int column, int row)
    {
        if (Over || grid.KindAt(column, row) == LaneGrid.EmptyKind)
        {
            return false;
        }

        var kind = defenders[SiegeRules.CellIndex(column, row)].Kind;
        ClearCell(column, row);
        Emit(SiegeEventKind.DefenderRemoved, CellCenter(column, row), column, row, (byte)kind, 0);
        return true;
    }

    public float Grant(float sunlight) => economy.Add(sunlight);

    public int MoteAt(Vector2 world, float radius)
    {
        var best = -1;
        var bestDistance = radius * radius;
        for (var index = 0; index < MoteCapacity; index++)
        {
            if (!motes[index].Alive)
            {
                continue;
            }

            var distance = Vector2.DistanceSquared(MotePosition(motes[index]), world);
            if (distance > bestDistance)
            {
                continue;
            }

            best = index;
            bestDistance = distance;
        }

        return best;
    }

    public int Collect(int index)
    {
        if (Over || (uint)index >= MoteCapacity || !motes[index].Alive)
        {
            return 0;
        }

        ref var mote = ref motes[index];
        mote.Alive = false;
        economy.Add(mote.Value);
        SunGathered += mote.Value;
        Emit(SiegeEventKind.MoteCollected, MotePosition(mote), 0, 0, mote.Sky ? (byte)1 : (byte)0, mote.Value);
        return mote.Value;
    }

    public int Spawn(EnemyKind kind, int column, float y)
    {
        if ((uint)column >= SiegeRules.Columns)
        {
            return -1;
        }

        var slot = FreeEnemy();
        if (slot < 0)
        {
            return -1;
        }

        var health = SiegeRules.Health(kind) * healthScale;
        enemies[slot] = new SiegeEnemy
        {
            Kind = kind,
            State = kind == EnemyKind.Digger ? EnemyState.Burrowing : EnemyState.Walking,
            Alive = true,
            HasPot = kind == EnemyKind.Armoured,
            Column = (byte)column,
            BiteRow = -1,
            Y = y,
            Health = health,
            MaxHealth = health,
            SummonTimer = SiegeRules.BossSummonSeconds,
            Id = nextId++,
        };
        aliveCount++;
        var bit = 1 << (int)kind;
        var first = (seenKinds & bit) == 0;
        seenKinds |= bit;
        Emit(SiegeEventKind.EnemySpawned, new Vector2(column + 0.5f, y), column, SiegeRules.RowAt(y), (byte)kind,
            first ? 1 : 0);
        return slot;
    }

    public void BeginWave(int index)
    {
        Wave = Math.Max(0, index);
        if (Endless)
        {
            healthScale = SiegeRules.EndlessHealthScale(Wave + 1);
            BuildEndlessWave(Wave + 1);
        }
        else
        {
            var count = SiegeLevels.BuildWave(Level, Wave, ref random, waveBuffer);
            spawner.Load(waveBuffer.AsSpan(0, count));
            WaveSpent = 0;
        }

        Phase = SiegePhase.Spawning;
        Emit(SiegeEventKind.WaveStarted, new Vector2(SiegeRules.Columns * 0.5f, 0f), 0, 0, FinalWave ? (byte)1 : (byte)0,
            Wave + 1);
    }

    private void Reset(GameRandom seeded)
    {
        random = seeded;
        grid.Clear();
        Array.Clear(defenders);
        Array.Clear(enemies);
        Array.Clear(seeds);
        Array.Clear(motes);
        Array.Clear(cooldowns);
        spawner.Clear();
        clock.Reset();
        eventCount = 0;
        nextId = 0;
        aliveCount = 0;
        seenKinds = 0;
        Phase = SiegePhase.Prelude;
        phaseTimer = SiegeRules.PreludeSeconds;
        skyTimer = SiegeRules.FirstSkyMoteSeconds;
        Wave = 0;
        WavesSurvived = 0;
        Health = SiegeRules.GardenHealth;
        Defeated = 0;
        Planted = 0;
        SunGathered = 0;
        WaveSpent = 0;
        Time = 0f;
        LastKill = new Vector2(SiegeRules.Columns * 0.5f, SiegeRules.Rows * 0.5f);
    }

    private void BuildEndlessWave(int wave)
    {
        var count = spawner.Endless(ref random, wave, SiegeLevels.EndlessBudget);
        WaveSpent = spawner.SpentBudget;
        if (wave % SiegeLevels.EndlessBossEvery != 0 || count >= WaveCapacity)
        {
            return;
        }

        spawner.Entries.CopyTo(waveBuffer);
        waveBuffer[count] = new WaveEntry(SiegeLevels.WaveLead + 1f, (byte)random.Next(SiegeRules.Columns),
            (byte)EnemyKind.Boss);
        spawner.Load(waveBuffer.AsSpan(0, count + 1));
    }

    private static float InitialTimer(DefenderKind kind) => kind switch
    {
        DefenderKind.Sprout or DefenderKind.Frostbud => SiegeRules.FirstShotSeconds,
        DefenderKind.Sunbloom => SiegeRules.SunbloomFirstSeconds,
        DefenderKind.Bombcap => SiegeRules.BombFuseSeconds,
        _ => 0f,
    };

    private void AdvancePhase(float step)
    {
        switch (Phase)
        {
            case SiegePhase.Prelude:
                phaseTimer -= step;
                if (phaseTimer <= 0f)
                {
                    BeginWave(0);
                }

                return;
            case SiegePhase.Spawning:
                var count = spawner.Advance(step, due);
                for (var index = 0; index < count; index++)
                {
                    Spawn((EnemyKind)due[index].EnemyKind, due[index].Lane, SiegeRules.SpawnY);
                }

                if (spawner.Finished)
                {
                    Phase = SiegePhase.Clearing;
                }

                return;
            case SiegePhase.Break:
                phaseTimer -= step;
                if (phaseTimer <= 0f)
                {
                    BeginWave(Wave + 1);
                }

                return;
            default:
                return;
        }
    }

    private void CheckOutcome()
    {
        if (Health <= 0)
        {
            Phase = SiegePhase.Lost;
            Emit(SiegeEventKind.LevelLost, new Vector2(SiegeRules.Columns * 0.5f, SiegeRules.GardenLine), 0,
                SiegeRules.Rows, 0, 0);
            return;
        }

        if (Phase != SiegePhase.Clearing || aliveCount > 0)
        {
            return;
        }

        if (FinalWave)
        {
            Phase = SiegePhase.Won;
            Emit(SiegeEventKind.LevelWon, LastKill, 0, 0, 0, Health);
            return;
        }

        if (Endless)
        {
            WavesSurvived = Wave + 1;
        }

        Emit(SiegeEventKind.WaveCleared, new Vector2(SiegeRules.Columns * 0.5f, 0f), 0, 0, 0, Wave + 1);
        Phase = SiegePhase.Break;
        phaseTimer = SiegeRules.BreakSeconds;
    }

    private void AdvanceSky(float step)
    {
        skyTimer -= step;
        if (skyTimer > 0f)
        {
            return;
        }

        skyTimer = random.Range(SiegeRules.SkyMoteMinSeconds, SiegeRules.SkyMoteMaxSeconds);
        var x = random.Range(SkyMoteEdge, SiegeRules.Columns - SkyMoteEdge);
        var landing = random.Range(SkyMoteLandTop, SkyMoteLandBottom);
        var from = new Vector2(x, SkyMoteStart);
        var to = new Vector2(x, landing);
        if (SpawnMote(from, to, (landing - SkyMoteStart) / SiegeRules.MoteFallSpeed, true))
        {
            Emit(SiegeEventKind.SkyMote, from, (int)x, 0, 1, 0);
        }
    }

    private void RefreshColumnTops()
    {
        for (var column = 0; column < SiegeRules.Columns; column++)
        {
            columnTop[column] = float.MaxValue;
        }

        for (var index = 0; index < EnemyCapacity; index++)
        {
            ref readonly var enemy = ref enemies[index];
            if (!enemy.Targetable || enemy.Y < SiegeRules.VisibleTop)
            {
                continue;
            }

            if (enemy.Y < columnTop[enemy.Column])
            {
                columnTop[enemy.Column] = enemy.Y;
            }
        }
    }

    private void AdvanceDefenders(float step)
    {
        for (var cell = 0; cell < SiegeRules.CellCount; cell++)
        {
            ref var defender = ref defenders[cell];
            if (!defender.Occupied)
            {
                continue;
            }

            defender.Age += step;
            defender.Recoil = MathF.Max(0f, defender.Recoil - step * RecoilDecay);
            defender.Flash = MathF.Max(0f, defender.Flash - step * FlashDecay);
            var column = SiegeRules.ColumnOf(cell);
            var row = SiegeRules.RowOf(cell);
            switch (defender.Kind)
            {
                case DefenderKind.Sprout:
                case DefenderKind.Frostbud:
                    defender.Timer -= step;
                    if (defender.Timer > 0f)
                    {
                        break;
                    }

                    if (columnTop[column] > row + SiegeRules.TargetLead)
                    {
                        defender.Timer = 0f;
                        break;
                    }

                    if (Fire(column, row, defender.Kind == DefenderKind.Frostbud))
                    {
                        defender.Timer = SiegeRules.FireInterval(defender.Kind);
                        defender.Recoil = 1f;
                    }

                    break;
                case DefenderKind.Sunbloom:
                    defender.Timer -= step;
                    if (defender.Timer > 0f)
                    {
                        break;
                    }

                    defender.Timer = SiegeRules.SunbloomInterval;
                    defender.Recoil = 1f;
                    ProduceSun(column, row);
                    break;
                case DefenderKind.Bombcap:
                    defender.Timer -= step;
                    if (defender.Timer <= 0f)
                    {
                        Explode(column, row);
                    }

                    break;
                default:
                    break;
            }
        }
    }

    private bool Fire(int column, int row, bool frost)
    {
        var slot = -1;
        for (var index = 0; index < SeedCapacity; index++)
        {
            if (!seeds[index].Alive)
            {
                slot = index;
                break;
            }
        }

        if (slot < 0)
        {
            return false;
        }

        seeds[slot] = new SiegeSeed
        {
            Column = (byte)column,
            Frost = frost,
            Alive = true,
            Y = row + 0.5f - SiegeRules.SeedLaunchOffset,
            Id = nextId++,
        };
        Emit(SiegeEventKind.SeedFired, new Vector2(column + 0.5f, seeds[slot].Y), column, row, frost ? (byte)1 : (byte)0,
            seeds[slot].Id);
        return true;
    }

    private void ProduceSun(int column, int row)
    {
        var center = CellCenter(column, row);
        var target = new Vector2(
            Math.Clamp(center.X + random.Range(-MoteHopSpread, MoteHopSpread), 0.3f, SiegeRules.Columns - 0.3f),
            MathF.Min(center.Y + MoteHopDrop, SiegeRules.Rows - 0.3f));
        if (SpawnMote(center, target, SiegeRules.MoteHopSeconds, false))
        {
            Emit(SiegeEventKind.SunProduced, center, column, row, 0, (int)SiegeRules.SunValue);
        }
    }

    private bool SpawnMote(Vector2 from, Vector2 to, float travel, bool sky)
    {
        for (var index = 0; index < MoteCapacity; index++)
        {
            if (motes[index].Alive)
            {
                continue;
            }

            motes[index] = new SiegeMote
            {
                From = from,
                To = to,
                Travel = MathF.Max(0.01f, travel),
                Value = (int)SiegeRules.SunValue,
                Alive = true,
                Sky = sky,
                Id = nextId++,
            };
            return true;
        }

        return false;
    }

    private void Explode(int column, int row)
    {
        Emit(SiegeEventKind.BombExploded, CellCenter(column, row), column, row, (byte)DefenderKind.Bombcap, 0);
        ClearCell(column, row);
        for (var index = 0; index < EnemyCapacity; index++)
        {
            ref var enemy = ref enemies[index];
            if (!enemy.Alive || !SiegeRules.InBlast(column, row, enemy.Column, enemy.Y))
            {
                continue;
            }

            Damage(ref enemy, SiegeRules.BombDamage, false);
        }
    }

    private void AdvanceSeeds(float step)
    {
        for (var index = 0; index < SeedCapacity; index++)
        {
            ref var seed = ref seeds[index];
            if (!seed.Alive)
            {
                continue;
            }

            seed.Y -= SiegeRules.SeedSpeed * step;
            var target = SeedTarget(seed.Column, seed.Y);
            if (target >= 0)
            {
                seed.Alive = false;
                ref var enemy = ref enemies[target];
                Emit(SiegeEventKind.EnemyHit, new Vector2(seed.Column + 0.5f, seed.Y), seed.Column, SiegeRules.RowAt(enemy.Y),
                    seed.Frost ? (byte)1 : (byte)0, seed.Id);
                Damage(ref enemy, SiegeRules.SeedDamage, seed.Frost);
                continue;
            }

            if (seed.Y < SiegeRules.SeedVanishY)
            {
                seed.Alive = false;
            }
        }
    }

    private int SeedTarget(int column, float y)
    {
        var best = -1;
        var bestY = float.MinValue;
        for (var index = 0; index < EnemyCapacity; index++)
        {
            ref readonly var enemy = ref enemies[index];
            if (!enemy.Targetable || enemy.Column != column ||
                MathF.Abs(enemy.Y - y) > SiegeRules.HitRadius(enemy.Kind) || enemy.Y <= bestY)
            {
                continue;
            }

            best = index;
            bestY = enemy.Y;
        }

        return best;
    }

    private void Damage(ref SiegeEnemy enemy, float amount, bool frost)
    {
        enemy.Health -= amount;
        enemy.Squash = 1f;
        if (frost)
        {
            enemy.SlowSeconds = SiegeRules.SlowSeconds;
        }

        var position = new Vector2(enemy.Column + 0.5f, enemy.Y);
        if (enemy.HasPot && enemy.Health <= enemy.MaxHealth * SiegeRules.PotShare)
        {
            enemy.HasPot = false;
            Emit(SiegeEventKind.PotLost, position, enemy.Column, SiegeRules.RowAt(enemy.Y), (byte)enemy.Kind, 0);
        }

        if (enemy.Health > 0f)
        {
            return;
        }

        enemy.Alive = false;
        aliveCount--;
        Defeated++;
        LastKill = position;
        Emit(SiegeEventKind.EnemyKilled, position, enemy.Column, SiegeRules.RowAt(enemy.Y), (byte)enemy.Kind,
            enemy.Id);
    }

    private void AdvanceEnemies(float step)
    {
        for (var index = 0; index < EnemyCapacity; index++)
        {
            ref var enemy = ref enemies[index];
            if (!enemy.Alive)
            {
                continue;
            }

            enemy.Age += step;
            enemy.Squash = MathF.Max(0f, enemy.Squash - step * SquashDecay);
            var slow = enemy.Slowed ? SiegeRules.SlowFactor : 1f;
            enemy.SlowSeconds = MathF.Max(0f, enemy.SlowSeconds - step);
            switch (enemy.State)
            {
                case EnemyState.Burrowing:
                    Burrow(ref enemy, step * slow);
                    break;
                case EnemyState.Surfacing:
                    enemy.StateTimer -= step;
                    if (enemy.StateTimer <= 0f)
                    {
                        enemy.State = enemy.BiteRow >= 0 ? EnemyState.Eating : EnemyState.Walking;
                    }

                    break;
                default:
                    if (enemy.Kind == EnemyKind.Flyer)
                    {
                        Advance(ref enemy, SiegeRules.Speed(enemy.Kind) * slow * step);
                    }
                    else
                    {
                        March(ref enemy, step * slow);
                    }

                    break;
            }

            if (enemy.Kind == EnemyKind.Boss)
            {
                Summon(ref enemy, step);
            }

            if (enemy.Alive && enemy.Y >= SiegeRules.GardenLine)
            {
                ReachGarden(ref enemy);
            }
        }
    }

    private static void Advance(ref SiegeEnemy enemy, float distance)
    {
        enemy.Y += distance;
        enemy.Stride += distance;
    }

    private void March(ref SiegeEnemy enemy, float scaledStep)
    {
        if (enemy.State == EnemyState.Eating)
        {
            if (enemy.BiteRow >= 0 && grid.KindAt(enemy.Column, enemy.BiteRow) != LaneGrid.EmptyKind)
            {
                Chew(ref enemy, scaledStep);
                return;
            }

            enemy.State = EnemyState.Walking;
            enemy.BiteRow = -1;
            enemy.BiteTimer = 0f;
        }

        var distance = SiegeRules.Speed(enemy.Kind) * scaledStep;
        var from = Math.Max(0, SiegeRules.RowAt(enemy.Y - SiegeRules.BackReach));
        var row = grid.NextOccupied(enemy.Column, from, 1);
        if (row >= 0)
        {
            var contact = SiegeRules.ContactY(enemy.Kind, row);
            if (enemy.Y + distance >= contact)
            {
                if (enemy.Y < contact)
                {
                    Advance(ref enemy, contact - enemy.Y);
                }

                enemy.State = EnemyState.Eating;
                enemy.BiteRow = (sbyte)row;
                enemy.BiteTimer = 0f;
                return;
            }
        }

        Advance(ref enemy, distance);
    }

    private void Chew(ref SiegeEnemy enemy, float scaledStep)
    {
        enemy.BiteTimer += scaledStep;
        var interval = SiegeRules.BiteInterval(enemy.Kind);
        if (enemy.BiteTimer < interval)
        {
            return;
        }

        enemy.BiteTimer -= interval;
        var column = enemy.Column;
        int row = enemy.BiteRow;
        ref var defender = ref defenders[SiegeRules.CellIndex(column, row)];
        defender.Health -= SiegeRules.BiteDamage(enemy.Kind);
        defender.Flash = 1f;
        if (defender.Health > 0f)
        {
            Emit(SiegeEventKind.DefenderBitten, CellCenter(column, row), column, row, (byte)defender.Kind,
                (int)enemy.Kind);
            return;
        }

        Emit(SiegeEventKind.DefenderLost, CellCenter(column, row), column, row, (byte)defender.Kind, (int)enemy.Kind);
        ClearCell(column, row);
        enemy.State = EnemyState.Walking;
        enemy.BiteRow = -1;
        enemy.BiteTimer = 0f;
    }

    private void Burrow(ref SiegeEnemy enemy, float scaledStep)
    {
        Advance(ref enemy, SiegeRules.DiggerBurrowSpeed * scaledStep);
        var from = Math.Max(0, SiegeRules.RowAt(enemy.Y - SiegeRules.DiggerSurfaceOffset));
        var target = grid.NextOccupied(enemy.Column, from, 1);
        if (target >= 0)
        {
            var surfaceAt = MathF.Min(target + SiegeRules.DiggerSurfaceOffset, SiegeRules.GardenLine - GardenSurfaceMargin);
            if (enemy.Y >= surfaceAt)
            {
                Surface(ref enemy, target);
            }

            return;
        }

        if (enemy.Y >= SiegeRules.DiggerLastSurface)
        {
            Surface(ref enemy, -1);
        }
    }

    private void Surface(ref SiegeEnemy enemy, int target)
    {
        enemy.State = EnemyState.Surfacing;
        enemy.StateTimer = SiegeRules.DiggerSurfaceSeconds;
        enemy.BiteRow = (sbyte)target;
        enemy.BiteTimer = 0f;
        Emit(SiegeEventKind.Surfaced, new Vector2(enemy.Column + 0.5f, enemy.Y), enemy.Column, target,
            (byte)enemy.Kind, enemy.Id);
    }

    private void Summon(ref SiegeEnemy enemy, float step)
    {
        enemy.SummonTimer -= step;
        if (enemy.SummonTimer > 0f || enemy.Y < 0f)
        {
            return;
        }

        enemy.SummonTimer = SiegeRules.BossSummonSeconds;
        var side = random.Next(2) == 0 ? -1 : 1;
        var column = enemy.Column + side;
        if ((uint)column >= SiegeRules.Columns)
        {
            column = enemy.Column - side;
        }

        if (Spawn(EnemyKind.Walker, column, enemy.Y) >= 0)
        {
            Emit(SiegeEventKind.Summoned, new Vector2(enemy.Column + 0.5f, enemy.Y), column, SiegeRules.RowAt(enemy.Y),
                (byte)enemy.Kind, enemy.Id);
        }
    }

    private void ReachGarden(ref SiegeEnemy enemy)
    {
        var damage = SiegeRules.GardenDamage(enemy.Kind);
        Health = Math.Max(0, Health - damage);
        enemy.Alive = false;
        aliveCount--;
        Emit(SiegeEventKind.GardenHit, new Vector2(enemy.Column + 0.5f, SiegeRules.GardenLine), enemy.Column,
            SiegeRules.Rows, (byte)enemy.Kind, damage);
    }

    private void AdvanceMotes(float step)
    {
        for (var index = 0; index < MoteCapacity; index++)
        {
            ref var mote = ref motes[index];
            if (!mote.Alive)
            {
                continue;
            }

            mote.Age += step;
            if (mote.Remaining > 0f)
            {
                continue;
            }

            mote.Alive = false;
            Emit(SiegeEventKind.MoteExpired, MotePosition(mote), 0, 0, mote.Sky ? (byte)1 : (byte)0, mote.Value);
        }
    }

    private void DecayCosmetics(float step)
    {
        for (var index = 0; index < EnemyCapacity; index++)
        {
            ref var enemy = ref enemies[index];
            enemy.Squash = MathF.Max(0f, enemy.Squash - step * SquashDecay);
        }

        for (var cell = 0; cell < SiegeRules.CellCount; cell++)
        {
            ref var defender = ref defenders[cell];
            defender.Recoil = MathF.Max(0f, defender.Recoil - step * RecoilDecay);
            defender.Flash = MathF.Max(0f, defender.Flash - step * FlashDecay);
        }
    }

    private void ClearCell(int column, int row)
    {
        grid.Remove(column, row);
        defenders[SiegeRules.CellIndex(column, row)] = default;
    }

    private int FreeEnemy()
    {
        for (var index = 0; index < EnemyCapacity; index++)
        {
            if (!enemies[index].Alive)
            {
                return index;
            }
        }

        return -1;
    }

    private void Emit(SiegeEventKind kind, Vector2 position, int column, int row, byte detail, int value)
    {
        if (eventCount >= EventCapacity)
        {
            return;
        }

        events[eventCount++] = new SiegeEvent(kind, position, column, row, detail, value);
    }
}
