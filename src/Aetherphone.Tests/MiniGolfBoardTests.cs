using System.Globalization;
using System.Numerics;
using System.Text;
using Aetherphone.Apps.Games.Framework;
using Aetherphone.Apps.Games.Framework.Physics;
using Aetherphone.Apps.Games.MiniGolf;
using Xunit;

namespace Aetherphone.Tests;

public sealed class MiniGolfBoardTests
{
    private const float Frame = 1f / 60f;
    private const int WindmillHole = 6;
    private const int WaterHole = 5;
    private const int TunnelHole = 7;
    private const int SandHole = 3;
    private const int SlopeHole = 4;
    private const string OpenGreen = "par 2; course 0 0 6 0 6 12 0 12; tee 3 10; cup 3 2";

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var first = Trace(11);
        var second = Trace(11);
        var other = Trace(12);

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public void EveryHoleHasAValidTeeAndCupInsideItsCourse()
    {
        Assert.Equal(MiniGolfCourse.HoleCount, MiniGolfCourse.Sources.Length);
        for (var index = 0; index < MiniGolfCourse.HoleCount; index++)
        {
            Assert.True(MiniGolfHole.TryParse(MiniGolfCourse.Sources[index], out var hole, out var error),
                $"hole {index + 1}: {error}");
            Assert.InRange(hole.Par, 2, 5);
            AssertPlayable(hole, hole.Tee, MiniGolfBoard.BallRadius, index, "tee");
            AssertPlayable(hole, hole.Cup, MiniGolfBoard.CupRadius, index, "cup");
            Assert.True(Vector2.Distance(hole.Tee, hole.Cup) > 2f, $"hole {index + 1} puts the tee next to the cup");
            for (var tunnel = 0; tunnel < hole.Tunnels.Length; tunnel++)
            {
                Assert.True(hole.OnCourse(hole.Tunnels[tunnel].Entry), $"hole {index + 1} tunnel {tunnel} entry is off course");
                AssertPlayable(hole, hole.Tunnels[tunnel].Exit, MiniGolfBoard.BallRadius, index, "tunnel exit");
            }

            Assert.Equal(PolygonArea(hole.Course), TriangleArea(hole.Course, hole.CourseTriangles), 3);
        }
    }

    [Fact]
    public void BrokenHoleSourcesAreRejected()
    {
        Assert.False(MiniGolfHole.TryParse("par 2; course 0 0 6 0 6 12; cup 3 2", out _, out _));
        Assert.False(MiniGolfHole.TryParse("par 2; course 0 0 6 0 6 12 0 12; tee 3 10", out _, out _));
        Assert.False(MiniGolfHole.TryParse("par two; course 0 0 6 0 6 12 0 12; tee 3 10; cup 3 2", out _, out _));
        Assert.False(MiniGolfHole.TryParse(OpenGreen + "; hoop 1 1", out _, out _));
        Assert.True(MiniGolfHole.TryParse(OpenGreen, out _, out _));
    }

    [Fact]
    public void ABallBouncesOffAWallAndStaysOnTheCourse()
    {
        var board = Load(OpenGreen, 1);
        board.PlaceBall(new Vector2(4.5f, 6f));
        board.Shoot(new Vector2(1f, 0f), 0.6f);
        var sawWall = false;
        var reversed = false;
        for (var frame = 0; frame < 120; frame++)
        {
            board.BeginFrame();
            board.Step(Frame);
            sawWall |= (board.Events & GolfEvents.Wall) != 0;
            reversed |= board.BallVelocity.X < -1f;
        }

        Assert.True(sawWall);
        Assert.True(reversed);
        Assert.True(board.Hole.OnCourse(board.BallPosition));
    }

    [Fact]
    public void WaterResetsTheBallToItsLastSpotWithAPenaltyStroke()
    {
        var hole = MiniGolfCourse.Get(WaterHole);
        var board = new MiniGolfBoard();
        board.Load(hole, GameRandom.FromSeed(1));
        var water = hole.Water[0].Center;
        var events = GolfEvents.None;
        board.BeginFrame();
        Assert.True(board.Shoot(water - hole.Tee, 0.8f));
        for (var frame = 0; frame < 300 && !(board.Phase == GolfPhase.Aiming && frame > 5); frame++)
        {
            board.BeginFrame();
            board.Step(Frame);
            events |= board.Events;
        }

        Assert.True((events & GolfEvents.Splash) != 0);
        Assert.Equal(GolfPhase.Aiming, board.Phase);
        Assert.Equal(2, board.Strokes);
        Assert.Equal(1, board.Penalties);
        Assert.True(Vector2.Distance(hole.Tee, board.BallPosition) < 0.01f);
    }

    [Fact]
    public void TheCupCapturesOnlyUnderTheSpeedThreshold()
    {
        var slow = Load(OpenGreen, 1);
        slow.PlaceBall(new Vector2(3f, 3f));
        slow.Shoot(new Vector2(0f, -1f), 2f / MiniGolfBoard.MaxShotSpeed);
        Run(slow, 3f);
        Assert.Equal(GolfPhase.Holed, slow.Phase);
        Assert.Equal(1, slow.Strokes);

        var fast = Load(OpenGreen, 1);
        fast.PlaceBall(new Vector2(3f, 3f));
        fast.Shoot(new Vector2(0f, -1f), 1f);
        var events = GolfEvents.None;
        for (var frame = 0; frame < 30; frame++)
        {
            fast.BeginFrame();
            fast.Step(Frame);
            events |= fast.Events;
        }

        Assert.True((events & GolfEvents.LipOut) != 0);
        Assert.NotEqual(GolfPhase.Sinking, fast.Phase);
        Assert.NotEqual(GolfPhase.Holed, fast.Phase);
    }

    [Fact]
    public void ATunnelCarriesTheBallToItsExit()
    {
        var hole = MiniGolfCourse.Get(TunnelHole);
        var board = new MiniGolfBoard();
        board.Load(hole, GameRandom.FromSeed(1));
        board.Shoot(hole.Tunnels[0].Entry - hole.Tee, 0.5f);
        var events = GolfEvents.None;
        var exitY = float.MaxValue;
        for (var frame = 0; frame < 240; frame++)
        {
            board.BeginFrame();
            board.Step(Frame);
            events |= board.Events;
            if ((board.Events & GolfEvents.Tunnel) != 0)
            {
                exitY = board.BallPosition.Y;
            }
        }

        Assert.True((events & GolfEvents.Tunnel) != 0);
        Assert.True(Math.Abs(exitY - hole.Tunnels[0].Exit.Y) < 0.01f);
        Assert.True(board.BallPosition.Y < hole.Walls[0][0].Y);
    }

    [Fact]
    public void SandStopsTheBallSoonerThanTheGreen()
    {
        var hole = MiniGolfCourse.Get(SandHole);
        var sand = hole.Sand[0];
        var start = new Vector2(sand.Min.X + 0.3f, sand.Max.Y + 1.5f);
        var through = RestDistance(hole, start, new Vector2(0f, -1f), 0.5f);
        var beside = RestDistance(hole, new Vector2(sand.Max.X + 0.6f, start.Y), new Vector2(0f, -1f), 0.5f);

        Assert.True(through < beside * 0.8f, $"sand {through} green {beside}");
    }

    [Fact]
    public void ASlopePushesARestingBallDownhill()
    {
        var hole = MiniGolfCourse.Get(SlopeHole);
        var slope = hole.Slopes[0];
        var board = new MiniGolfBoard();
        board.Load(hole, GameRandom.FromSeed(1));
        var spot = slope.Zone.Center;
        board.PlaceBall(spot);
        board.Shoot(new Vector2(0f, -1f), 0.01f);
        Run(board, 2f);

        Assert.True(board.BallPosition.X > spot.X + 0.4f);
    }

    [Fact]
    public void AWindmillBladeTurnsAndKnocksTheBall()
    {
        var hole = MiniGolfCourse.Get(WindmillHole);
        var board = new MiniGolfBoard();
        board.Load(hole, GameRandom.FromSeed(3));
        var body = board.MillBody(0);
        var start = board.World.Angle(body);
        var spot = hole.Mills[0].Hub + new Vector2(hole.Mills[0].Reach * 0.75f, 0f);
        board.PlaceBall(spot);
        Run(board, 4f);

        Assert.NotEqual(start, board.World.Angle(body));
        Assert.True(Vector2.Distance(spot, board.BallPosition) > 0.1f);
    }

    [Fact]
    public void TheAimPreviewStartsAtTheBallAndBouncesOffWalls()
    {
        var board = Load(OpenGreen, 1);
        board.PlaceBall(new Vector2(5f, 6f));
        Span<Vector2> path = stackalloc Vector2[MiniGolfRenderer.MaxAimPoints];
        var count = board.PredictAim(new Vector2(1f, 0f), 1f, path);

        Assert.True(count >= 3);
        Assert.Equal(new Vector2(5f, 6f), path[0]);
        Assert.True(path[1].X > 5.7f);
        Assert.True(path[2].X < path[1].X);
    }

    [Fact]
    public void AHoleEndsAfterTheStrokeLimit()
    {
        var board = Load(OpenGreen, 1);
        for (var stroke = 0; stroke < MiniGolfBoard.MaxStrokes; stroke++)
        {
            Assert.True(board.Shoot(new Vector2(1f, 0f), 0.02f));
            Run(board, 1f);
        }

        Assert.Equal(MiniGolfBoard.MaxStrokes, board.Strokes);
        Assert.False(board.CanShoot);
        Assert.False(board.Shoot(new Vector2(1f, 0f), 0.5f));
    }

    [Fact]
    public void EveryHoleCanBeHoledByASimpleSearch()
    {
        for (var index = 0; index < MiniGolfCourse.HoleCount; index++)
        {
            var hole = MiniGolfCourse.Get(index);
            var position = hole.Tee;
            var holed = false;
            for (var stroke = 0; stroke < 6 && !holed; stroke++)
            {
                var bestScore = float.MaxValue;
                var bestDirection = Vector2.Zero;
                var bestPower = 0f;
                for (var directionIndex = 0; directionIndex < 36 && bestScore > -1f; directionIndex++)
                {
                    var angle = directionIndex * MathF.Tau / 36f;
                    var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                    for (var powerIndex = 1; powerIndex <= 8; powerIndex++)
                    {
                        var power = powerIndex / 8f;
                        var sunk = Simulate(hole, position, direction, power, out var rest);
                        var score = sunk ? -1f : Vector2.Distance(rest, hole.Cup) + (Blocked(hole, rest) ? 6f : 0f);
                        if (score >= bestScore)
                        {
                            continue;
                        }

                        bestScore = score;
                        bestDirection = direction;
                        bestPower = power;
                    }
                }

                holed = Simulate(hole, position, bestDirection, bestPower, out position);
            }

            Assert.True(holed, $"hole {index + 1} was not holed in six strokes");
        }
    }

    [Fact]
    public void PlayersTakeTurnsOnEveryHoleBeforeTheRoundMovesOn()
    {
        var round = new MiniGolfRound();
        round.Reset(2, 3);
        var steps = new List<RoundStep>();
        var seen = new StringBuilder();
        for (var turn = 0; turn < 6; turn++)
        {
            seen.Append(round.Hole).Append(round.Player).Append(';');
            round.Record(round.Player + 2);
            steps.Add(round.Advance());
        }

        Assert.Equal("00;01;02;10;11;12;", seen.ToString());
        Assert.Equal(new[]
        {
            RoundStep.NextPlayer, RoundStep.NextPlayer, RoundStep.NextHole, RoundStep.NextPlayer, RoundStep.NextPlayer,
            RoundStep.Finished,
        }, steps);
        Assert.True(round.Finished);
        Assert.Equal(4, round.Total(0));
        Assert.Equal(8, round.Total(2));
        Assert.Equal(0, round.Winner());
        Assert.Equal(4 - MiniGolfCourse.Par(2), round.ToPar(0));
    }

    [Fact]
    public void ScorecardResultsAndStandingsFollowTheStrokes()
    {
        Assert.Equal(HoleResult.HoleInOne, MiniGolfRound.Classify(1, 2));
        Assert.Equal(HoleResult.Eagle, MiniGolfRound.Classify(2, 4));
        Assert.Equal(HoleResult.Birdie, MiniGolfRound.Classify(2, 3));
        Assert.Equal(HoleResult.Par, MiniGolfRound.Classify(3, 3));
        Assert.Equal(HoleResult.Bogey, MiniGolfRound.Classify(4, 3));
        Assert.Equal(HoleResult.DoubleBogey, MiniGolfRound.Classify(5, 3));
        Assert.Equal(HoleResult.Worse, MiniGolfRound.Classify(7, 3));

        var round = new MiniGolfRound();
        round.Reset(1, 3);
        round.Record(5);
        round.Advance();
        round.Record(3);
        round.Advance();
        round.Record(3);
        round.Advance();
        Span<int> order = stackalloc int[3];
        Assert.Equal(3, round.Standings(order));
        Assert.Equal(1, order[0]);
        Assert.Equal(2, order[1]);
        Assert.Equal(0, order[2]);
        Assert.Equal(-1, round.Winner());
    }

    private static string Trace(ulong seed)
    {
        var hole = MiniGolfCourse.Get(WindmillHole);
        var board = new MiniGolfBoard();
        board.Load(hole, GameRandom.FromSeed(seed));
        var trace = new StringBuilder();
        board.Shoot(hole.Mills[0].Hub - hole.Tee, 0.7f);
        for (var frame = 0; frame < 360; frame++)
        {
            board.BeginFrame();
            board.Step(Frame);
            if (board.CanShoot && board.Strokes < 3)
            {
                board.Shoot(hole.Cup - board.BallPosition, 0.6f);
            }

            var position = board.BallPosition;
            trace.Append(position.X.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                .Append(position.Y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                .Append(board.World.Angle(board.MillBody(0)).ToString("R", CultureInfo.InvariantCulture)).Append(';');
        }

        return trace.ToString();
    }

    private static void AssertPlayable(MiniGolfHole hole, Vector2 point, float clearance, int index, string what)
    {
        Assert.True(hole.OnCourse(point), $"hole {index + 1} {what} is off the course");
        Assert.True(Geometry2D.ChainDistance(point, hole.Course, true) > clearance,
            $"hole {index + 1} {what} touches the border");
        for (var wall = 0; wall < hole.Walls.Length; wall++)
        {
            Assert.True(Geometry2D.ChainDistance(point, hole.Walls[wall], false) > clearance,
                $"hole {index + 1} {what} touches a wall");
        }

        for (var block = 0; block < hole.Blocks.Length; block++)
        {
            Assert.True(Geometry2D.ChainDistance(point, hole.Blocks[block], true) > clearance,
                $"hole {index + 1} {what} touches a block");
        }

        for (var post = 0; post < hole.Posts.Length; post++)
        {
            Assert.True(Vector2.Distance(point, hole.Posts[post].Center) > hole.Posts[post].Radius + clearance,
                $"hole {index + 1} {what} sits on a post");
        }

        for (var water = 0; water < hole.Water.Length; water++)
        {
            Assert.False(hole.Water[water].Contains(point), $"hole {index + 1} {what} sits in water");
        }

        for (var mill = 0; mill < hole.Mills.Length; mill++)
        {
            Assert.True(Vector2.Distance(point, hole.Mills[mill].Hub) > hole.Mills[mill].Reach + clearance,
                $"hole {index + 1} {what} sits under a windmill");
        }
    }

    private static float PolygonArea(Vector2[] polygon)
    {
        var area = 0f;
        for (var index = 0; index < polygon.Length; index++)
        {
            var current = polygon[index];
            var next = polygon[(index + 1) % polygon.Length];
            area += current.X * next.Y - next.X * current.Y;
        }

        return MathF.Abs(area * 0.5f);
    }

    private static float TriangleArea(Vector2[] points, int[] triangles)
    {
        var area = 0f;
        for (var index = 0; index + 2 < triangles.Length; index += 3)
        {
            var a = points[triangles[index]];
            var b = points[triangles[index + 1]];
            var c = points[triangles[index + 2]];
            area += MathF.Abs((b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y)) * 0.5f;
        }

        return area;
    }

    private static MiniGolfBoard Load(string source, ulong seed)
    {
        Assert.True(MiniGolfHole.TryParse(source, out var hole, out var error), error);
        var board = new MiniGolfBoard();
        board.Load(hole, GameRandom.FromSeed(seed));
        return board;
    }

    private static void Run(MiniGolfBoard board, float seconds)
    {
        for (var elapsed = 0f; elapsed < seconds; elapsed += Frame)
        {
            board.BeginFrame();
            board.Step(Frame);
        }
    }

    private static float RestDistance(MiniGolfHole hole, Vector2 start, Vector2 direction, float power)
    {
        Simulate(hole, start, direction, power, out var rest);
        return Vector2.Distance(start, rest);
    }

    private static bool Blocked(MiniGolfHole hole, Vector2 from)
    {
        var board = new MiniGolfBoard();
        board.Load(hole, GameRandom.FromSeed(1));
        var toCup = hole.Cup - from;
        var distance = toCup.Length();
        if (distance < 0.01f)
        {
            return false;
        }

        var unit = toCup / distance;
        for (var side = -1; side <= 1; side += 2)
        {
            var offset = new Vector2(-unit.Y, unit.X) * (MiniGolfBoard.BallRadius * side);
            if (board.World.Raycast(from + offset, unit, distance, out var hit) &&
                board.World.Shape(hit.Body) != ShapeKind.Circle)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Simulate(MiniGolfHole hole, Vector2 start, Vector2 direction, float power, out Vector2 rest)
    {
        var board = new MiniGolfBoard();
        board.Load(hole, GameRandom.FromSeed(1));
        board.PlaceBall(start);
        board.Shoot(direction, power);
        for (var frame = 0; frame < 900; frame++)
        {
            board.BeginFrame();
            board.Step(Frame);
            if (board.Phase is GolfPhase.Holed or GolfPhase.Sinking)
            {
                rest = hole.Cup;
                return true;
            }

            if (board.Phase == GolfPhase.Aiming && frame > 2)
            {
                break;
            }
        }

        rest = board.BallPosition;
        return false;
    }
}
