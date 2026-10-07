using System.Numerics;
using System.Text.Json;
using Aetherphone.Apps.Games;
using Aetherphone.Apps.Games.Crater;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.World;
using Aetherphone.Apps.Games.Online;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Aethernet.Contracts;
using Aetherphone.Core.Games;
using Xunit;

namespace Aetherphone.Tests;

public sealed class OnlineCraterWireTests
{
    private const int Seed = 424242;
    private const float FrameSeconds = 4f / 120f;

    private const string ServerState =
        "{\"roundIndex\":1,\"hostUserId\":\"host\",\"players\":["
        + "{\"userId\":\"host\",\"displayName\":\"Ada\",\"seat\":0,\"away\":false,\"wins\":0,\"team\":0,\"missed\":0},"
        + "{\"userId\":\"guest\",\"displayName\":\"Bex\",\"seat\":1,\"away\":true,\"wins\":2,\"team\":1,\"missed\":1}],"
        + "\"seed\":424242,\"teamCount\":2,\"moogles\":["
        + "{\"team\":0,\"x\":420,\"y\":905,\"health\":100,\"facing\":1,\"aim\":600,\"alive\":true,\"sunk\":false,"
        + "\"shielded\":true}],"
        + "\"craters\":[1600,900,120],\"tunnels\":[2,500,800,520,830],\"ammo\":[-1,-1,2,2,1,2],"
        + "\"nextMembers\":[1,0],\"turnTeam\":1,\"turnMoogle\":0,\"wind\":-3,\"water\":1690,\"round\":2,"
        + "\"playedMask\":1,\"turnCount\":3,\"shot\":{\"team\":0,\"moogle\":0,\"weapon\":1,\"facing\":-1,\"aim\":700,"
        + "\"power\":650,\"fuse\":4,\"stride\":4,\"frames\":40,\"walkFrames\":6,\"waterFrom\":1690,"
        + "\"flights\":[{\"kind\":1,\"frame\":6,\"path\":[450,880,20,-10]}],"
        + "\"moves\":[{\"moogle\":0,\"frame\":0,\"walk\":true,\"path\":[400,905,5,0]}],"
        + "\"beats\":[6,1,450,880,-500,0]},\"actionCount\":5,\"turnSeconds\":45,\"lastSeat\":0,"
        + "\"lastKind\":\"crater.shot\",\"endKind\":\"\",\"winnerSeat\":-1}";

    [Fact]
    public void TheKindActionAndEndingNamesMatchTheServer()
    {
        Assert.Equal("games.crater", GameRoomWire.CraterKind);
        Assert.Equal("start", GameRoomWire.ActionStart);
        Assert.Equal("shoot", GameRoomWire.ActionShoot);
        Assert.Equal("resign", GameRoomWire.ActionResign);
        Assert.Equal("crater.timeout", GameRoomWire.CraterTimeoutEvent);
        Assert.Equal("knockout", GameRoomWire.CraterEndKnockout);
        Assert.Equal("draw", GameRoomWire.CraterEndDraw);
        Assert.Equal("resign", GameRoomWire.CraterEndResign);
        Assert.Equal("desertion", GameRoomWire.CraterEndDesertion);
        Assert.Equal("timeout", GameRoomWire.CraterEndTimeout);
        Assert.Equal(4, OnlineGameArt.MaxPlayers(GameRoomWire.CraterKind));
        Assert.Equal("crater", OnlineGameArt.AccentId(GameRoomWire.CraterKind));
        Assert.Equal("online.crater", GamesLibrary.OnlineEntryId(GameRoomWire.CraterKind));
        Assert.Contains(GameRoomWire.CraterKind, OnlineGameArt.Kinds);
    }

    [Fact]
    public void BeatNumbersAreTheOfflineEventKinds()
    {
        Assert.Equal(6, GameRoomWire.CraterBeatStride);
        Assert.Equal((int)CraterEventKind.Launched, GameRoomWire.CraterBeatLaunched);
        Assert.Equal((int)CraterEventKind.Bounced, GameRoomWire.CraterBeatBounced);
        Assert.Equal((int)CraterEventKind.Exploded, GameRoomWire.CraterBeatExploded);
        Assert.Equal((int)CraterEventKind.Damaged, GameRoomWire.CraterBeatDamaged);
        Assert.Equal((int)CraterEventKind.Shielded, GameRoomWire.CraterBeatShielded);
        Assert.Equal((int)CraterEventKind.Died, GameRoomWire.CraterBeatDied);
        Assert.Equal((int)CraterEventKind.Drowned, GameRoomWire.CraterBeatDrowned);
        Assert.Equal((int)CraterEventKind.Splashed, GameRoomWire.CraterBeatSplashed);
        Assert.Equal((int)CraterEventKind.ClusterSplit, GameRoomWire.CraterBeatClusterSplit);
        Assert.Equal((int)CraterEventKind.DrillStarted, GameRoomWire.CraterBeatDrillStarted);
        Assert.Equal((int)CraterEventKind.Landed, GameRoomWire.CraterBeatLanded);
        Assert.Equal((int)CraterEventKind.FallHurt, GameRoomWire.CraterBeatFallHurt);
        Assert.Equal((int)CraterEventKind.Teleported, GameRoomWire.CraterBeatTeleported);
        Assert.Equal((int)CraterEventKind.ShieldRaised, GameRoomWire.CraterBeatShieldRaised);
        Assert.Equal((int)CraterEventKind.SuddenDeath, GameRoomWire.CraterBeatSuddenDeath);
        Assert.Equal((int)CraterEventKind.WaterRising, GameRoomWire.CraterBeatWaterRising);
        Assert.True(GameRoomWire.CraterBeatTunnel > (int)CraterEventKind.MatchOver);
    }

    [Fact]
    public void TheShotRequestCarriesTheWholeTurn()
    {
        var request = new GameRoomActionRequest("shoot", 7, -1, -1, "turn1", -1, -1, 0.5f, 0.75f, 12.5f, 3.25f, -1,
            -1, 1, 3, 9.5f);
        var json = JsonSerializer.Serialize(request, AethernetJsonContext.Default.GameRoomActionRequest);
        Assert.Equal(
            "{\"action\":\"shoot\",\"actionCount\":7,\"card\":-1,\"color\":-1,\"clientActionId\":\"turn1\","
            + "\"from\":-1,\"to\":-1,\"angle\":0.5,\"power\":0.75,\"placeX\":12.5,\"placeY\":3.25,\"column\":-1,"
            + "\"facing\":-1,\"weapon\":1,\"fuse\":3,\"walkX\":9.5}",
            json);
    }

    [Fact]
    public void TheRoomStateParsesFromTheServerShape()
    {
        var state = JsonSerializer.Deserialize(ServerState, AethernetJsonContext.Default.CraterRoomStateDto);

        Assert.NotNull(state);
        Assert.Equal(424242, state.Seed);
        Assert.Equal(2, state.TeamCount);
        Assert.Equal(1, state.Players![1].Team);
        Assert.True(state.Players[1].Away);
        Assert.Equal(1, state.Players[1].Missed);
        var moogle = state.Moogles![0];
        Assert.Equal(420, moogle.X);
        Assert.Equal(905, moogle.Y);
        Assert.Equal(600, moogle.Aim);
        Assert.True(moogle.Shielded);
        Assert.Equal(new[] { 1600, 900, 120 }, state.Craters);
        Assert.Equal(new[] { 2, 500, 800, 520, 830 }, state.Tunnels);
        Assert.Equal(1, state.Ammo![4]);
        Assert.Equal(new[] { 1, 0 }, state.NextMembers);
        Assert.Equal(1, state.TurnTeam);
        Assert.Equal(-3, state.Wind);
        Assert.Equal(1690, state.Water);
        Assert.Equal(2, state.Round);
        Assert.Equal(3, state.TurnCount);
        var shot = state.Shot!;
        Assert.Equal(-1, shot.Facing);
        Assert.Equal(650, shot.Power);
        Assert.Equal(6, shot.WalkFrames);
        Assert.Equal(1, shot.Flights![0].Kind);
        Assert.Equal(new[] { 450, 880, 20, -10 }, shot.Flights[0].Path);
        Assert.True(shot.Moves![0].Walk);
        Assert.Equal(new[] { 6, 1, 450, 880, -500, 0 }, shot.Beats);
        Assert.Equal(45, state.TurnSeconds);
        Assert.Equal("crater.shot", state.LastKind);

        var snapshot = new GameRoomSnapshotDto("room", GameRoomWire.CraterKind, GameState: ServerState);
        var built = GameRoomSession.Build("room", 1, 4, snapshot);
        Assert.NotNull(built.Crater);
        Assert.Null(built.Pool);
        Assert.Equal(2, built.Roster!.Players.Length);
        Assert.Equal("Bex", built.Roster.Players[1].DisplayName);
        Assert.Equal(5, built.Roster.ActionCount);
        Assert.Equal(-1, built.Roster.WinnerSeat);
    }

    [Theory]
    [InlineData(1000003, 0, 92921, 11967011131138485162UL)]
    [InlineData(2000006, 1, 64935, 805262199926799704UL)]
    [InlineData(3000009, 2, 79480, 6748406159480276939UL)]
    public void ASeedGeneratesTheSameGroundAsTheServer(int seed, int style, int solid, ulong fingerprint)
    {
        var random = GameRandom.FromSeed((uint)seed);
        Assert.Equal(style, random.Next(3));
        var terrain = new TerrainMask(CraterRules.Columns, CraterRules.Rows, CraterRules.MetresPerCell);
        terrain.Generate(ref random, (TerrainStyle)style);
        var hash = 14695981039346656037UL;
        var count = 0;
        for (var row = 0; row < CraterRules.Rows; row++)
        {
            for (var column = 0; column < CraterRules.Columns; column++)
            {
                var bit = terrain.IsSolid(column, row) ? 1UL : 0UL;
                count += (int)bit;
                hash = (hash ^ bit) * 1099511628211UL;
            }
        }

        Assert.Equal(solid, count);
        Assert.Equal(fingerprint, hash);
    }

    [Fact]
    public void TheFirstLookSnapsWithoutReplaying()
    {
        var scene = new OnlineCraterScene();
        var state = Field() with { TurnCount = 4, Shot = Shot(Field()) };

        scene.Sync(state);

        Assert.False(scene.Replaying);
        Assert.Equal(state.Moogles![0].X / 100f, scene.Moogle(0).Position.X, 3);
        Assert.Equal(state.Moogles[0].Y / 100f, scene.Moogle(0).Position.Y, 3);
        Assert.Equal(0, scene.ActiveMoogle);
        Assert.False(scene.TryTakeEvent(out _));
    }

    [Fact]
    public void ALiveTurnReplaysTheServerPathsAndBeats()
    {
        var scene = new OnlineCraterScene();
        var before = Field();
        scene.Sync(before);
        var crater = Spawn(1) + new Vector2(0f, 0.3f);
        var target = before.Moogles![1];
        var after = before with
        {
            TurnCount = 1,
            TurnTeam = 1,
            TurnMoogle = 1,
            Craters = new[] { Centimetres(crater.X), Centimetres(crater.Y), 120 },
            Moogles = new[] { before.Moogles[0], target with { X = target.X + 60, Health = 60 } },
            Shot = Shot(before),
        };

        scene.Sync(after);
        Assert.True(scene.Replaying);
        Assert.True(scene.Terrain.IsSolid(crater));

        scene.Advance(FrameSeconds * 4.5f);
        Assert.True(scene.Projectiles[0].Alive);
        Assert.Equal(ProjectileKind.Shell, scene.Projectiles[0].Kind);
        Assert.Equal(before.Moogles[0].X / 100f + 0.45f, scene.Projectiles[0].Position.X, 2);
        Assert.True(scene.TryTakeEvent(out var launched));
        Assert.Equal(CraterEventKind.Launched, launched.Kind);

        scene.Advance(FrameSeconds * 6f);
        Assert.False(scene.Terrain.IsSolid(crater));
        Assert.Equal(1, scene.Craters.Length);
        Assert.True(scene.TryTakeEvent(out var exploded));
        Assert.Equal(CraterEventKind.Exploded, exploded.Kind);
        Assert.Equal(1.2f, exploded.Radius, 3);
        Assert.True(scene.TryTakeEvent(out var damaged));
        Assert.Equal(CraterEventKind.Damaged, damaged.Kind);
        Assert.Equal(60, scene.Moogle(1).Health);
        Assert.False(scene.Moogle(1).Grounded);

        scene.Advance(2f);
        Assert.False(scene.Replaying);
        Assert.True(scene.TryTakeEvent(out var turn));
        Assert.Equal(CraterEventKind.TurnStarted, turn.Kind);
        Assert.Equal(1, turn.Team);
        Assert.Equal((target.X + 60) / 100f, scene.Moogle(1).Position.X, 3);
        Assert.Equal(1, scene.ActiveMoogle);
        Assert.False(scene.Projectiles[0].Alive);
    }

    [Fact]
    public void AWalkPreviewFollowsTheGroundWithinTheBudget()
    {
        var scene = new OnlineCraterScene();
        var state = Field();
        scene.Sync(state);
        var start = scene.Moogle(0).Position;

        Assert.True(scene.Walk(1, 0.5f));
        var walked = scene.Moogle(0).Position;
        Assert.True(walked.X > start.X);
        Assert.True(walked.X - start.X <= CraterRules.WalkSpeed * 0.5f + 0.01f);
        var bottom = walked.Y + CraterRules.MoogleRadius;
        Assert.Equal(bottom, CraterFooting.GroundTop(scene.Terrain, walked.X, bottom - CraterRules.StepUp), 3);
        Assert.True(scene.Walked);
        Assert.Equal(walked.X, scene.WalkedX);

        for (var second = 0; second < 20; second++)
        {
            scene.Walk(1, 0.1f);
        }

        Assert.True(scene.Moogle(0).Position.X - start.X <= GameRoomWire.CraterMaxWalk);
    }

    private static CraterRoomStateDto Field()
    {
        var players = new[]
        {
            new CraterPlayerDto("host", "Ada", 0, false, 0, 0, 0),
            new CraterPlayerDto("guest", "Bex", 1, false, 0, 1, 0),
        };
        var left = Spawn(0) - new Vector2(0f, CraterRules.MoogleRadius);
        var right = Spawn(2) - new Vector2(0f, CraterRules.MoogleRadius);
        var moogles = new[]
        {
            new CraterMoogleDto(0, Centimetres(left.X), Centimetres(left.Y), 100, 1, 600, true, false, false),
            new CraterMoogleDto(1, Centimetres(right.X), Centimetres(right.Y), 100, -1, 600, true, false, false),
        };
        return new CraterRoomStateDto(1, "host", players, Seed, 2, moogles, Array.Empty<int>(), Array.Empty<int>(),
            new[] { -1, -1, 2, 2, 2, 2, -1, -1, 2, 2, 2, 2 }, new[] { 1, 1 }, 0, 0, 0, 1690, 1, 0, 0, null, 3, 45,
            -1, "crater.start", string.Empty, -1);
    }

    private static CraterShotDto Shot(CraterRoomStateDto before)
    {
        var shooter = before.Moogles![0];
        var target = before.Moogles[1];
        var crater = Spawn(1) + new Vector2(0f, 0.3f);
        var flight = new List<int> { shooter.X, shooter.Y - 40 };
        for (var frame = 1; frame <= 10; frame++)
        {
            flight.Add(10);
            flight.Add(0);
        }

        var beats = new[]
        {
            0, GameRoomWire.CraterBeatLaunched, shooter.X, shooter.Y - 40, 0, 0,
            10, GameRoomWire.CraterBeatExploded, Centimetres(crater.X), Centimetres(crater.Y), 120, -1,
            10, GameRoomWire.CraterBeatDamaged, target.X, target.Y, 40, 1,
        };
        var move = new[] { target.X, target.Y, 10, -10, 10, 0, 10, 0, 10, 0, 10, 10, 10, 10 };
        return new CraterShotDto(0, 0, 0, 1, 600, 500, 3, 4, 20, 0, 1690,
            new[] { new CraterFlightDto(0, 0, flight.ToArray()) },
            new[] { new CraterMoveDto(1, 10, false, move) }, beats);
    }

    private static Vector2 Spawn(int plateau)
    {
        var random = GameRandom.FromSeed((uint)Seed);
        var style = (TerrainStyle)random.Next(3);
        var terrain = new TerrainMask(CraterRules.Columns, CraterRules.Rows, CraterRules.MetresPerCell);
        terrain.Generate(ref random, style);
        return terrain.SpawnPoints(3)[plateau];
    }

    private static int Centimetres(float metres) => (int)MathF.Round(metres * 100f);
}
