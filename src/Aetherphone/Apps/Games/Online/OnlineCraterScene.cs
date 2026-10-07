using Aetherphone.Apps.Games.Crater;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.World;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Games;

namespace Aetherphone.Apps.Games.Online;

internal sealed class OnlineCraterScene
{
    public const int NoMoogle = -1;
    private const int StyleCount = 3;
    private const int EventCapacity = 96;
    private const int MaxMoves = 64;
    private const float ReplayTailSeconds = 0.3f;
    private const float NeverBlasted = 1000f;
    private const float WalkEpsilon = 0.01f;
    private const float MilliradiansPerRadian = 1000f;
    private const float Permille = 1000f;

    private readonly TerrainMask terrain = new(CraterRules.Columns, CraterRules.Rows, CraterRules.MetresPerCell);
    private readonly CraterMoogle[] moogles = new CraterMoogle[CraterRules.MaxMoogles];
    private readonly float[] aims = new float[CraterRules.MaxMoogles];
    private readonly CraterProjectile[] projectiles = new CraterProjectile[CraterBoard.MaxProjectiles];
    private readonly CraterMark[] marks = new CraterMark[CraterBoard.MaxCraters];
    private readonly CraterEvent[] events = new CraterEvent[EventCapacity];
    private readonly ReplayTrack[] moves = new ReplayTrack[MaxMoves];
    private readonly ReplayTrack[] flights = new ReplayTrack[CraterBoard.MaxProjectiles];
    private int[] points = new int[1024];
    private FixedStepClock walkClock = new(CraterRules.TickSeconds, CraterRules.MaxCatchUpSeconds);
    private CraterRoomStateDto? shown;
    private CraterShotDto? replay;
    private int terrainSeed;
    private bool terrainReady;
    private int moogleCount;
    private int markCount;
    private int carvedCraters;
    private int carvedTunnelPoints;
    private int moveCount;
    private int flightCount;
    private int pointCount;
    private int beatCursor;
    private int drillFrame;
    private int riseFrame;
    private int eventHead;
    private int eventCount;
    private float replaySeconds;
    private float frameSeconds;
    private float waterFrom;
    private float waterTarget;
    private float walkOriginX;
    private bool muted;

    public TerrainMask Terrain => terrain;

    public TerrainStyle Style { get; private set; }

    public int TerrainVersion { get; private set; }

    public CraterRoomStateDto? Shown => shown;

    public ReadOnlySpan<CraterMoogle> Moogles => new(moogles, 0, moogleCount);

    public ReadOnlySpan<CraterProjectile> Projectiles => projectiles;

    public ReadOnlySpan<CraterMark> Craters => new(marks, 0, markCount);

    public bool Replaying => replay is not null;

    public int ActiveMoogle { get; private set; } = NoMoogle;

    public float Water { get; private set; } = CraterRules.WorldHeight - CraterRules.WaterDepth;

    public Vector2 LastBlast { get; private set; }

    public float SinceBlast { get; private set; } = NeverBlasted;

    public bool Walked => ActiveMoogle >= 0 && MathF.Abs(moogles[ActiveMoogle].Position.X - walkOriginX) > WalkEpsilon;

    public float WalkedX => ActiveMoogle >= 0 ? moogles[ActiveMoogle].Position.X : 0f;

    public ref readonly CraterMoogle Moogle(int index) => ref moogles[index];

    public float Aim(int index) => aims[index];

    public void Reset()
    {
        shown = null;
        replay = null;
        terrainReady = false;
        moogleCount = 0;
        markCount = 0;
        eventHead = 0;
        eventCount = 0;
        ActiveMoogle = NoMoogle;
        SinceBlast = NeverBlasted;
        Array.Clear(projectiles);
    }

    public void Sync(CraterRoomStateDto state)
    {
        if (ReferenceEquals(state, shown))
        {
            return;
        }

        var previous = shown;
        shown = state;
        if (!terrainReady || state.Seed != terrainSeed || previous is null)
        {
            replay = null;
            Rebuild(state);
            Snap(state);
            if (previous is not null)
            {
                AnnounceTurn(state);
            }

            return;
        }

        if (state.TurnCount == previous.TurnCount)
        {
            if (replay is null && state.ActionCount != previous.ActionCount)
            {
                Refresh(state);
            }

            return;
        }

        if (replay is not null)
        {
            Settle(previous);
        }

        var shot = state.Shot;
        if (shot is null || shot.Frames <= 1)
        {
            Snap(state);
            AnnounceTurn(state);
            return;
        }

        BeginReplay(state, previous, shot);
    }

    public void Advance(float seconds)
    {
        SinceBlast = MathF.Min(NeverBlasted, SinceBlast + seconds);
        if (replay is null || shown is null)
        {
            return;
        }

        replaySeconds += seconds;
        var frame = replaySeconds / frameSeconds;
        FireBeats(replay, frame);
        PoseMoogles(frame);
        PoseProjectiles(replay, frame);
        PoseWater(frame);
        if (replaySeconds < (replay.Frames - 1) * frameSeconds + ReplayTailSeconds)
        {
            return;
        }

        Settle(shown);
        AnnounceTurn(shown);
    }

    public bool TryTakeEvent(out CraterEvent entry)
    {
        if (eventCount == 0)
        {
            entry = default;
            return false;
        }

        entry = events[eventHead];
        eventHead = (eventHead + 1) % EventCapacity;
        eventCount--;
        return true;
    }

    public void BeginAiming()
    {
        walkClock.Reset();
        if (ActiveMoogle >= 0)
        {
            walkOriginX = moogles[ActiveMoogle].Position.X;
        }
    }

    public bool Walk(int direction, float deltaSeconds)
    {
        if (ActiveMoogle < 0 || direction == 0 || replay is not null)
        {
            walkClock.Reset();
            return false;
        }

        ref var moogle = ref moogles[ActiveMoogle];
        moogle.Facing = direction;
        var ticks = walkClock.Advance(deltaSeconds);
        var moved = false;
        for (var tick = 0; tick < ticks; tick++)
        {
            if (!CraterFooting.TryWalkStep(terrain, moogle.Position, direction, Water, out var next)
                || MathF.Abs(next.X - walkOriginX) > GameRoomWire.CraterMaxWalk)
            {
                break;
            }

            moogle.Position = next;
            moved = true;
        }

        return moved;
    }

    public void SetAim(float elevation, int facing)
    {
        if (ActiveMoogle < 0)
        {
            return;
        }

        aims[ActiveMoogle] = Math.Clamp(elevation, CraterRules.MinElevation, CraterRules.MaxElevation);
        if (facing != 0)
        {
            moogles[ActiveMoogle].Facing = Math.Sign(facing);
        }
    }

    private void Rebuild(CraterRoomStateDto state)
    {
        terrainSeed = state.Seed;
        terrainReady = true;
        var random = GameRandom.FromSeed((uint)state.Seed);
        Style = (TerrainStyle)random.Next(StyleCount);
        terrain.Generate(ref random, Style);
        carvedCraters = 0;
        carvedTunnelPoints = 0;
        markCount = 0;
        SinceBlast = NeverBlasted;
        TerrainVersion++;
    }

    private void Snap(CraterRoomStateDto state)
    {
        replay = null;
        CarveCraters(state.Craters, int.MaxValue);
        CarveTunnels(state.Tunnels, int.MaxValue);
        var source = state.Moogles ?? Array.Empty<CraterMoogleDto>();
        moogleCount = Math.Min(source.Length, moogles.Length);
        for (var index = 0; index < moogleCount; index++)
        {
            moogles[index] = Restored(source[index]);
            aims[index] = source[index].Aim / MilliradiansPerRadian;
        }

        Array.Clear(projectiles);
        Water = state.Water > 0 ? state.Water / GameRoomWire.CraterCentimetres : Water;
        ActiveMoogle = state.TurnMoogle >= 0 && state.TurnMoogle < moogleCount ? state.TurnMoogle : NoMoogle;
        BeginAiming();
    }

    private void Refresh(CraterRoomStateDto state)
    {
        var source = state.Moogles ?? Array.Empty<CraterMoogleDto>();
        var count = Math.Min(source.Length, moogleCount);
        for (var index = 0; index < count; index++)
        {
            if (index != ActiveMoogle)
            {
                moogles[index] = Restored(source[index]);
                aims[index] = source[index].Aim / MilliradiansPerRadian;
                continue;
            }

            ref var active = ref moogles[index];
            active.Health = source[index].Health;
            active.Alive = source[index].Alive;
            active.Sunk = source[index].Sunk;
            active.Shielded = source[index].Shielded;
        }
    }

    private void Settle(CraterRoomStateDto state)
    {
        if (replay is not null)
        {
            muted = replaySeconds < (replay.Frames - 1) * frameSeconds;
            FireBeats(replay, float.MaxValue);
            muted = false;
        }

        Snap(state);
    }

    private void AnnounceTurn(CraterRoomStateDto state)
    {
        if (state.EndKind.Length > 0)
        {
            Push(CraterEventKind.MatchOver, Vector2.Zero, Vector2.Zero, 0f, state.Round, NoMoogle,
                WinnerTeam(state));
            return;
        }

        if (state.TurnTeam < 0)
        {
            return;
        }

        if (string.Equals(state.LastKind, GameRoomWire.CraterTimeoutEvent, StringComparison.Ordinal))
        {
            Push(CraterEventKind.TimeUp, Vector2.Zero, Vector2.Zero, 0f, 0, NoMoogle, state.TurnTeam);
        }

        var position = ActiveMoogle >= 0 ? moogles[ActiveMoogle].Position : Vector2.Zero;
        Push(CraterEventKind.TurnStarted, position, Vector2.Zero, 0f, state.Round, ActiveMoogle, state.TurnTeam);
    }

    private void BeginReplay(CraterRoomStateDto state, CraterRoomStateDto previous, CraterShotDto shot)
    {
        var beats = shot.Beats ?? Array.Empty<int>();
        var crateredHere = CountBeats(beats, GameRoomWire.CraterBeatExploded);
        var dugHere = CountBeats(beats, GameRoomWire.CraterBeatDrillStarted) +
                      CountBeats(beats, GameRoomWire.CraterBeatTunnel);
        var craters = state.Craters ?? Array.Empty<int>();
        CarveCraters(craters, craters.Length / 3 - crateredHere);
        CarveTunnels(state.Tunnels, TunnelPointCount(state.Tunnels) - dugHere);
        var consecutive = previous.TurnCount == state.TurnCount - 1;
        var finals = state.Moogles ?? Array.Empty<CraterMoogleDto>();
        var before = previous.Moogles ?? Array.Empty<CraterMoogleDto>();
        moogleCount = Math.Min(finals.Length, moogles.Length);
        for (var index = 0; index < moogleCount; index++)
        {
            var source = consecutive && index < before.Length ? before[index] : finals[index];
            moogles[index] = Restored(source);
            aims[index] = source.Aim / MilliradiansPerRadian;
        }

        if (!consecutive)
        {
            Rewind(beats, finals);
        }

        frameSeconds = Math.Max(1, shot.Stride) / (float)GameRoomWire.CraterTicksPerSecond;
        Decode(shot);
        if (shot.Moogle >= 0 && shot.Moogle < moogleCount)
        {
            aims[shot.Moogle] = shot.Aim / MilliradiansPerRadian;
        }

        ActiveMoogle = shot.Moogle >= 0 && shot.Moogle < moogleCount ? shot.Moogle : NoMoogle;
        Array.Clear(projectiles);
        beatCursor = 0;
        drillFrame = FirstBeatFrame(beats, GameRoomWire.CraterBeatDrillStarted);
        riseFrame = Math.Min(FirstBeatFrame(beats, GameRoomWire.CraterBeatSuddenDeath),
            FirstBeatFrame(beats, GameRoomWire.CraterBeatWaterRising));
        waterFrom = shot.WaterFrom > 0 ? shot.WaterFrom / GameRoomWire.CraterCentimetres : Water;
        waterTarget = state.Water / GameRoomWire.CraterCentimetres;
        Water = waterFrom;
        replaySeconds = 0f;
        replay = shot;
        PoseMoogles(0f);
    }

    private void Rewind(int[] beats, CraterMoogleDto[] finals)
    {
        for (var index = 0; index < moogleCount; index++)
        {
            var first = FirstMove(index);
            if (first >= 0)
            {
                ref readonly var track = ref moves[first];
                moogles[index].Position = Point(track.Start);
            }
        }

        for (var offset = 0; offset + GameRoomWire.CraterBeatStride <= beats.Length;
             offset += GameRoomWire.CraterBeatStride)
        {
            var moogle = beats[offset + 5];
            if (moogle < 0 || moogle >= moogleCount)
            {
                continue;
            }

            ref var body = ref moogles[moogle];
            switch (beats[offset + 1])
            {
                case GameRoomWire.CraterBeatDamaged:
                    body.Health = Math.Min(CraterRules.MaxHealth, body.Health + beats[offset + 4]);
                    break;
                case GameRoomWire.CraterBeatDied:
                case GameRoomWire.CraterBeatDrowned:
                    body.Alive = true;
                    body.Sunk = false;
                    break;
                case GameRoomWire.CraterBeatShielded:
                    body.Shielded = true;
                    break;
                case GameRoomWire.CraterBeatShieldRaised:
                    body.Shielded = false;
                    break;
                default:
                    break;
            }
        }

        for (var index = 0; index < moogleCount; index++)
        {
            if (!finals[index].Alive && moogles[index].Alive && moogles[index].Health <= 0)
            {
                moogles[index].Health = 1;
            }
        }
    }

    private void Decode(CraterShotDto shot)
    {
        pointCount = 0;
        moveCount = 0;
        flightCount = 0;
        var moveSource = shot.Moves ?? Array.Empty<CraterMoveDto>();
        for (var index = 0; index < moveSource.Length && moveCount < moves.Length; index++)
        {
            var move = moveSource[index];
            if (move.Moogle < 0 || move.Moogle >= moogleCount)
            {
                continue;
            }

            var start = Append(move.Path);
            moves[moveCount++] = new ReplayTrack(move.Moogle, move.Frame, start, (pointCount - start) / 2, move.Walk);
        }

        var flightSource = shot.Flights ?? Array.Empty<CraterFlightDto>();
        for (var index = 0; index < flightSource.Length && flightCount < flights.Length; index++)
        {
            var flight = flightSource[index];
            var start = Append(flight.Path);
            flights[flightCount++] = new ReplayTrack(flight.Kind, flight.Frame, start, (pointCount - start) / 2, false);
        }
    }

    private int Append(int[]? path)
    {
        var start = pointCount;
        if (path is null || path.Length < 2)
        {
            return start;
        }

        var needed = pointCount + path.Length - path.Length % 2;
        if (needed > points.Length)
        {
            Array.Resize(ref points, Math.Max(needed, points.Length * 2));
        }

        var x = 0;
        var y = 0;
        for (var index = 0; index + 1 < path.Length; index += 2)
        {
            x = index == 0 ? path[0] : x + path[index];
            y = index == 0 ? path[1] : y + path[index + 1];
            points[pointCount++] = x;
            points[pointCount++] = y;
        }

        return start;
    }

    private void PoseMoogles(float frame)
    {
        for (var index = 0; index < moogleCount; index++)
        {
            moogles[index].Velocity = Vector2.Zero;
        }

        for (var index = 0; index < moveCount; index++)
        {
            ref readonly var track = ref moves[index];
            var local = frame - track.Frame;
            if (local < 0f || track.Count == 0)
            {
                continue;
            }

            ref var body = ref moogles[track.Subject];
            body.Position = Sample(track, local, out var velocity);
            var moving = local <= track.Count - 1;
            body.Grounded = track.Walk || !moving;
            body.Velocity = moving ? velocity : Vector2.Zero;
            if (moving && track.Walk && MathF.Abs(velocity.X) > 0.0001f)
            {
                body.Facing = velocity.X > 0f ? 1 : -1;
            }
        }

        if (replay is not null && ActiveMoogle >= 0 && frame >= replay.WalkFrames)
        {
            moogles[ActiveMoogle].Facing = replay.Facing < 0 ? -1 : 1;
        }
    }

    private void PoseProjectiles(CraterShotDto shot, float frame)
    {
        for (var index = 0; index < projectiles.Length; index++)
        {
            ref var projectile = ref projectiles[index];
            if (index >= flightCount)
            {
                projectile.Alive = false;
                continue;
            }

            ref readonly var track = ref flights[index];
            var local = frame - track.Frame;
            if (local < 0f || local > track.Count - 1 || track.Count == 0)
            {
                projectile.Alive = false;
                continue;
            }

            projectile.Position = Sample(track, local, out var velocity);
            projectile.Velocity = velocity;
            projectile.Kind = (ProjectileKind)Math.Clamp(track.Subject, 0, (int)ProjectileKind.Drill);
            projectile.Alive = true;
            projectile.Id = index;
            projectile.Fuse = shot.Fuse - local * frameSeconds;
            projectile.Drilling = projectile.Kind == ProjectileKind.Drill && frame >= drillFrame;
        }
    }

    private void PoseWater(float frame)
    {
        if (frame < riseFrame)
        {
            Water = waterFrom;
            return;
        }

        var risen = (frame - riseFrame) * frameSeconds * CraterRules.WaterRiseSpeed;
        Water = MathF.Max(waterTarget, waterFrom - risen);
    }

    private void FireBeats(CraterShotDto shot, float frame)
    {
        var beats = shot.Beats ?? Array.Empty<int>();
        while ((beatCursor + 1) * GameRoomWire.CraterBeatStride <= beats.Length)
        {
            var offset = beatCursor * GameRoomWire.CraterBeatStride;
            if (beats[offset] > frame)
            {
                return;
            }

            beatCursor++;
            Fire(beats[offset + 1], beats[offset + 2], beats[offset + 3], beats[offset + 4], beats[offset + 5]);
        }
    }

    private void Fire(int kind, int x, int y, int value, int moogle)
    {
        var position = new Vector2(x, y) / GameRoomWire.CraterCentimetres;
        var known = moogle >= 0 && moogle < moogleCount;
        var team = known ? moogles[moogle].Team : CraterBoard.NoTeam;
        switch (kind)
        {
            case GameRoomWire.CraterBeatExploded:
                CarveCraters(shown?.Craters, carvedCraters + 1);
                LastBlast = position;
                SinceBlast = 0f;
                Push(CraterEventKind.Exploded, position, Vector2.Zero, value / GameRoomWire.CraterCentimetres, 0,
                    NoMoogle, team);
                return;
            case GameRoomWire.CraterBeatDrillStarted:
            case GameRoomWire.CraterBeatTunnel:
                CarveTunnels(shown?.Tunnels, carvedTunnelPoints + 1);
                if (kind == GameRoomWire.CraterBeatDrillStarted)
                {
                    Push(CraterEventKind.DrillStarted, position, Vector2.Zero, 0f, 0, NoMoogle, team);
                }

                return;
            case GameRoomWire.CraterBeatLaunched:
                var heading = value / MilliradiansPerRadian;
                Push(CraterEventKind.Launched, position, new Vector2(MathF.Cos(heading), MathF.Sin(heading)), 0f, 0,
                    moogle, team);
                return;
            case GameRoomWire.CraterBeatDamaged:
                if (known)
                {
                    moogles[moogle].Health -= Math.Min(value, moogles[moogle].Health);
                }

                break;
            case GameRoomWire.CraterBeatDied:
                if (known)
                {
                    moogles[moogle].Alive = false;
                    moogles[moogle].Shielded = false;
                }

                break;
            case GameRoomWire.CraterBeatDrowned:
                if (known)
                {
                    moogles[moogle].Alive = false;
                    moogles[moogle].Health = 0;
                    moogles[moogle].Sunk = true;
                    moogles[moogle].Shielded = false;
                }

                break;
            case GameRoomWire.CraterBeatSplashed:
                if (known)
                {
                    moogles[moogle].Sunk = true;
                }

                break;
            case GameRoomWire.CraterBeatShielded:
                if (known)
                {
                    moogles[moogle].Shielded = false;
                }

                break;
            case GameRoomWire.CraterBeatShieldRaised:
                if (known)
                {
                    moogles[moogle].Shielded = true;
                }

                break;
            case GameRoomWire.CraterBeatTeleported:
                if (known)
                {
                    Push(CraterEventKind.Teleported, moogles[moogle].Position, position, 0f, 0, moogle, team);
                    moogles[moogle].Position = position;
                }

                return;
            default:
                break;
        }

        if (kind <= (int)CraterEventKind.MatchOver)
        {
            Push((CraterEventKind)kind, position, Vector2.Zero, 0f, value, moogle, team);
        }
    }

    private void CarveCraters(int[]? listed, int upTo)
    {
        if (listed is null)
        {
            return;
        }

        var limit = Math.Min(upTo, listed.Length / 3);
        for (; carvedCraters < limit; carvedCraters++)
        {
            var offset = carvedCraters * 3;
            var center = new Vector2(listed[offset], listed[offset + 1]) / GameRoomWire.CraterCentimetres;
            var radius = listed[offset + 2] / GameRoomWire.CraterCentimetres;
            terrain.Carve(center, radius);
            if (markCount < marks.Length)
            {
                marks[markCount++] = new CraterMark(center, radius);
            }
        }
    }

    private void CarveTunnels(int[]? listed, int upTo)
    {
        if (listed is null || carvedTunnelPoints >= upTo)
        {
            return;
        }

        var cursor = 0;
        var seen = 0;
        while (cursor < listed.Length && carvedTunnelPoints < upTo)
        {
            var count = listed[cursor];
            var first = cursor + 1;
            cursor = first + Math.Max(0, count) * 2;
            if (count <= 0 || cursor > listed.Length)
            {
                return;
            }

            for (var point = 0; point < count && carvedTunnelPoints < upTo; point++, seen++)
            {
                if (seen < carvedTunnelPoints)
                {
                    continue;
                }

                var offset = first + point * 2;
                var to = new Vector2(listed[offset], listed[offset + 1]) / GameRoomWire.CraterCentimetres;
                var from = point == 0
                    ? to
                    : new Vector2(listed[offset - 2], listed[offset - 1]) / GameRoomWire.CraterCentimetres;
                CraterFooting.CarveTunnel(terrain, from, to);
                carvedTunnelPoints++;
            }
        }
    }

    private void Push(CraterEventKind kind, Vector2 position, Vector2 target, float radius, int value, int moogle,
        int team)
    {
        if (muted)
        {
            return;
        }

        var entry = new CraterEvent(kind, position, target, radius, value, moogle, team, ProjectileKind.Shell);
        if (eventCount == EventCapacity)
        {
            eventHead = (eventHead + 1) % EventCapacity;
            eventCount--;
        }

        events[(eventHead + eventCount) % EventCapacity] = entry;
        eventCount++;
    }

    private Vector2 Sample(in ReplayTrack track, float local, out Vector2 velocity)
    {
        var last = track.Count - 1;
        if (local >= last)
        {
            velocity = Vector2.Zero;
            return Point(track.Start + last * 2);
        }

        var step = (int)local;
        var from = Point(track.Start + step * 2);
        var to = Point(track.Start + (step + 1) * 2);
        velocity = (to - from) / frameSeconds;
        return Vector2.Lerp(from, to, local - step);
    }

    private Vector2 Point(int offset) =>
        new Vector2(points[offset], points[offset + 1]) / GameRoomWire.CraterCentimetres;

    private int FirstMove(int moogle)
    {
        var first = -1;
        for (var index = 0; index < moveCount; index++)
        {
            if (moves[index].Subject == moogle && (first < 0 || moves[index].Frame < moves[first].Frame))
            {
                first = index;
            }
        }

        return first;
    }

    private static CraterMoogle Restored(CraterMoogleDto source) => new()
    {
        Position = new Vector2(source.X, source.Y) / GameRoomWire.CraterCentimetres,
        Team = source.Team,
        Health = source.Health,
        Facing = source.Facing < 0 ? -1 : 1,
        Alive = source.Alive,
        Grounded = true,
        Sunk = source.Sunk,
        Shielded = source.Shielded,
        ApexY = source.Y / GameRoomWire.CraterCentimetres,
    };

    private static int WinnerTeam(CraterRoomStateDto state)
    {
        var players = state.Players ?? Array.Empty<CraterPlayerDto>();
        return state.WinnerSeat >= 0 && state.WinnerSeat < players.Length
            ? players[state.WinnerSeat].Team
            : CraterBoard.NoTeam;
    }

    private static int CountBeats(int[] beats, int kind)
    {
        var count = 0;
        for (var offset = 1; offset < beats.Length; offset += GameRoomWire.CraterBeatStride)
        {
            count += beats[offset] == kind ? 1 : 0;
        }

        return count;
    }

    private static int FirstBeatFrame(int[] beats, int kind)
    {
        for (var offset = 0; offset + 1 < beats.Length; offset += GameRoomWire.CraterBeatStride)
        {
            if (beats[offset + 1] == kind)
            {
                return beats[offset];
            }
        }

        return int.MaxValue;
    }

    private static int TunnelPointCount(int[]? tunnels)
    {
        if (tunnels is null)
        {
            return 0;
        }

        var total = 0;
        var cursor = 0;
        while (cursor < tunnels.Length)
        {
            var count = Math.Max(0, tunnels[cursor]);
            total += count;
            cursor += 1 + count * 2;
        }

        return total;
    }

    private readonly struct ReplayTrack
    {
        public readonly int Subject;
        public readonly int Frame;
        public readonly int Start;
        public readonly int Count;
        public readonly bool Walk;

        public ReplayTrack(int subject, int frame, int start, int count, bool walk)
        {
            Subject = subject;
            Frame = frame;
            Start = start;
            Count = count;
            Walk = walk;
        }
    }
}
