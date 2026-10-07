using System.Numerics;
using Aetherphone.Apps.Games.Framework.Physics;
using Xunit;

namespace Aetherphone.Tests;

public sealed class PhysicsWorldTests
{
    private const float Gravity = 10f;
    private static readonly PhysicsMaterial Solid = new(1f, 0f, 0.6f);

    [Fact]
    public void TenBoxStackSettlesAndSleeps()
    {
        var world = DownwardWorld();
        CreateGround(world);
        var boxes = new int[10];
        for (var index = 0; index < boxes.Length; index++)
        {
            boxes[index] = world.CreateBox(BodyType.Dynamic, new Vector2(0f, -0.5f - index), new Vector2(0.5f), 0f,
                Solid);
        }

        Run(world, 600);

        for (var index = 0; index < boxes.Length; index++)
        {
            Assert.False(world.IsAwake(boxes[index]));
        }

        var top = world.Position(boxes[^1]);
        Assert.InRange(top.X, -0.05f, 0.05f);
        Assert.InRange(top.Y, -9.6f, -9.4f);
        Assert.InRange(world.Angle(boxes[^1]), -0.02f, 0.02f);
    }

    [Fact]
    public void PostAndPlankTowerSettlesAndSleeps()
    {
        var world = DownwardWorld();
        CreateGround(world);
        var wood = new PhysicsMaterial(0.6f, 0.05f, 0.7f);
        var blocks = new int[18];
        for (var level = 0; level < 6; level++)
        {
            var baseY = -level * 1.1f;
            blocks[level * 3] = world.CreateBox(BodyType.Dynamic, new Vector2(0f, baseY - 0.5f), new Vector2(0.1f, 0.5f),
                0f, wood);
            blocks[level * 3 + 1] = world.CreateBox(BodyType.Dynamic, new Vector2(1f, baseY - 0.5f),
                new Vector2(0.1f, 0.5f), 0f, wood);
            blocks[level * 3 + 2] = world.CreateBox(BodyType.Dynamic, new Vector2(0.5f, baseY - 1.05f),
                new Vector2(0.65f, 0.05f), 0f, wood);
        }

        Run(world, 240);

        for (var index = 0; index < blocks.Length; index++)
        {
            Assert.False(world.IsAwake(blocks[index]));
        }

        Assert.InRange(world.Position(blocks[^1]).X, 0.49f, 0.51f);
        Assert.InRange(world.Angle(blocks[^1]), -0.005f, 0.005f);
    }

    [Fact]
    public void PinballBallStaysOnTheTableForAMinute()
    {
        var world = new PhysicsWorld { Gravity = new Vector2(0f, 6f) };
        world.CreatePolyline(new[]
        {
            new Vector2(0f, 0f), new Vector2(6f, 0f), new Vector2(6f, 9f), new Vector2(4f, 10f),
            new Vector2(4f, 12f), new Vector2(2f, 12f), new Vector2(2f, 10f), new Vector2(0f, 9f),
        }, new PhysicsMaterial(1f, 0.3f, 0.2f), true);
        var bumper = new PhysicsMaterial(1f, 0.9f, 0.1f);
        world.CreateCircle(BodyType.Static, new Vector2(2f, 3f), 0.4f, bumper);
        world.CreateCircle(BodyType.Static, new Vector2(4f, 3f), 0.4f, bumper);
        world.CreateCircle(BodyType.Static, new Vector2(3f, 4.5f), 0.4f, bumper);
        var flipperMaterial = new PhysicsMaterial(4f, 0.1f, 0.5f);
        var left = world.CreateBox(BodyType.Dynamic, new Vector2(2.4f, 9.7f), new Vector2(0.5f, 0.07f), 0f,
            flipperMaterial);
        var right = world.CreateBox(BodyType.Dynamic, new Vector2(3.6f, 9.7f), new Vector2(0.5f, 0.07f), 0f,
            flipperMaterial);
        var leftHinge = world.CreateHinge(PhysicsWorld.Ground, left, new Vector2(1.9f, 9.7f));
        var rightHinge = world.CreateHinge(PhysicsWorld.Ground, right, new Vector2(4.1f, 9.7f));
        world.SetHingeLimits(leftHinge, -0.5f, 0.5f);
        world.SetHingeLimits(rightHinge, -0.5f, 0.5f);
        var ball = world.CreateCircle(BodyType.Dynamic, new Vector2(3f, 1f), 0.15f, new PhysicsMaterial(2f, 0.2f, 0.2f),
            BodyFlags.Bullet);
        world.SetVelocity(ball, new Vector2(7f, 3f));
        var fastest = 0f;

        for (var frame = 0; frame < 3600; frame++)
        {
            var pressed = frame % 50 < 10;
            world.SetHingeMotor(leftHinge, pressed ? -30f : 20f, 300f);
            world.SetHingeMotor(rightHinge, pressed ? 30f : -20f, 300f);
            world.Step(1f / 60f);
            var position = world.Position(ball);
            fastest = MathF.Max(fastest, world.Velocity(ball).Length());
            Assert.InRange(position.X, 0f, 6f);
            Assert.InRange(position.Y, 0f, 12f);
            if (position.Y > 11f)
            {
                world.SetTransform(ball, new Vector2(3f, 1f), 0f);
                world.SetVelocity(ball, new Vector2(frame % 7 - 3f, 0f));
            }
        }

        Assert.True(fastest > 15f);
    }

    [Fact]
    public void RestitutionBounceHeightStaysWithinFivePercent()
    {
        const float dropHeight = 2f;
        const float restitution = 0.8f;
        const float radius = 0.25f;
        var world = DownwardWorld();
        CreateGround(world);
        var ball = world.CreateCircle(BodyType.Dynamic, new Vector2(0f, -radius - dropHeight), radius,
            new PhysicsMaterial(1f, restitution, 0.6f));

        var bounced = false;
        var peak = float.MaxValue;
        for (var step = 0; step < 360; step++)
        {
            world.Tick();
            var velocity = world.Velocity(ball).Y;
            if (!bounced)
            {
                bounced = velocity < 0f && world.Position(ball).Y > -radius - 0.5f;
                continue;
            }

            peak = MathF.Min(peak, world.Position(ball).Y);
            if (velocity > 0f)
            {
                break;
            }
        }

        var rise = -peak - radius;
        var expected = restitution * restitution * dropHeight;
        Assert.True(bounced);
        Assert.InRange(rise, expected * 0.95f, expected * 1.05f);
    }

    [Fact]
    public void BoxOnTwentyDegreeSlopeHoldsWithHalfFrictionAndSlidesWithATenth()
    {
        Assert.True(SlopeSlide(0.5f) < 0.05f);
        Assert.True(SlopeSlide(0.1f) > 1f);
    }

    [Fact]
    public void BulletAtSixtyMetresPerSecondDoesNotTunnelAThinWall()
    {
        var world = new PhysicsWorld { Gravity = Vector2.Zero };
        world.CreateBox(BodyType.Static, new Vector2(5f, 0f), new Vector2(0.025f, 2f), 0f, Solid);
        world.CreateSegment(new Vector2(-5f, -2f), new Vector2(-5f, 2f), Solid);
        var right = world.CreateCircle(BodyType.Dynamic, Vector2.Zero, 0.1f, Solid, BodyFlags.Bullet);
        var left = world.CreateCircle(BodyType.Dynamic, new Vector2(0f, 1f), 0.1f, Solid, BodyFlags.Bullet);
        world.SetVelocity(right, new Vector2(60f, 0f));
        world.SetVelocity(left, new Vector2(-60f, 0f));

        for (var step = 0; step < 120; step++)
        {
            world.Tick();
            Assert.True(world.Position(right).X < 5f - 0.025f);
            Assert.True(world.Position(left).X > -5f);
        }
    }

    [Fact]
    public void RopeKeepsItsLengthWithinTwoPercent()
    {
        const float length = 2f;
        var world = DownwardWorld();
        var weight = world.CreateCircle(BodyType.Dynamic, new Vector2(length, 0f), 0.2f,
            new PhysicsMaterial(5f, 0f, 0.5f));
        var rope = world.CreateRope(Vector2.Zero, weight, 10, length);

        for (var step = 0; step < 360; step++)
        {
            world.Tick();
            Assert.True(world.Position(weight).Length() <= length * 1.02f);
            Assert.True(ChainLength(world, rope) <= length * 1.02f);
        }

        Assert.InRange(world.Position(weight).Length(), length * 0.98f, length * 1.02f);
    }

    [Fact]
    public void CuttingARopeFreesTheBody()
    {
        var world = DownwardWorld();
        var weight = world.CreateCircle(BodyType.Dynamic, new Vector2(0f, 2f), 0.2f, Solid);
        var rope = world.CreateRope(Vector2.Zero, weight, 8, 2f);
        Run(world, 120);
        var settled = world.Position(weight);
        Assert.True(world.RopeHolds(rope));
        Assert.InRange(settled.Y, 1.96f, 2.04f);

        Assert.True(world.CutRope(rope, 4));
        Assert.False(world.RopeHolds(rope));
        Assert.True(world.IsRopeSegmentCut(rope, 4));
        Assert.False(world.CutRope(rope, 4));
        Run(world, 120);

        Assert.True(world.Position(weight).Y - settled.Y > 3f);
    }

    [Fact]
    public void SwipeAcrossARopeCutsIt()
    {
        var world = DownwardWorld();
        var weight = world.CreateCircle(BodyType.Dynamic, new Vector2(0f, 2f), 0.2f, Solid);
        var rope = world.CreateRope(Vector2.Zero, weight, 8, 2f);
        Run(world, 30);

        Assert.Equal(0, world.CutRopes(new Vector2(1f, 0.5f), new Vector2(2f, 0.5f)));
        Assert.Equal(1, world.CutRopes(new Vector2(-1f, 1f), new Vector2(1f, 1f)));
        Assert.False(world.RopeHolds(rope));
    }

    [Fact]
    public void HingeMotorReachesItsLimit()
    {
        var world = DownwardWorld();
        var flipper = world.CreateBox(BodyType.Dynamic, new Vector2(0.6f, 0f), new Vector2(0.6f, 0.08f), 0f,
            new PhysicsMaterial(2f, 0f, 0.5f));
        var hinge = world.CreateHinge(PhysicsWorld.Ground, flipper, Vector2.Zero);
        world.SetHingeLimits(hinge, -0.5f, 0.5f);
        world.SetHingeMotor(hinge, -20f, 50f);

        var lowest = 0f;
        for (var step = 0; step < 60; step++)
        {
            world.Tick();
            lowest = MathF.Min(lowest, world.HingeAngle(hinge));
        }

        Assert.InRange(world.HingeAngle(hinge), -0.52f, -0.48f);
        Assert.True(lowest > -0.55f);
        Assert.True(Vector2.Distance(world.WorldPoint(flipper, new Vector2(-0.6f, 0f)), Vector2.Zero) < 0.01f);

        world.SetHingeMotor(hinge, 20f, 50f);
        Run(world, 60);

        Assert.InRange(world.HingeAngle(hinge), 0.48f, 0.52f);
    }

    [Fact]
    public void FastFlipperLaunchesTheBallInsteadOfTunnelling()
    {
        var world = DownwardWorld();
        var flipper = world.CreateBox(BodyType.Dynamic, new Vector2(0.6f, 0f), new Vector2(0.6f, 0.05f), 0f,
            new PhysicsMaterial(4f, 0f, 0.5f));
        var hinge = world.CreateHinge(PhysicsWorld.Ground, flipper, Vector2.Zero);
        world.SetHingeLimits(hinge, -0.6f, 0f);
        var ball = world.CreateCircle(BodyType.Dynamic, new Vector2(1f, -0.2f), 0.15f,
            new PhysicsMaterial(1f, 0.3f, 0.3f), BodyFlags.Bullet);
        Run(world, 30);

        world.SetHingeMotor(hinge, -45f, 400f);
        for (var step = 0; step < 30; step++)
        {
            world.Tick();
            var local = world.LocalPoint(flipper, world.Position(ball));
            Assert.True(local.Y < 0f || local.X > 0.6f);
        }

        Assert.True(world.Velocity(ball).Y < -3f);
    }

    [Fact]
    public void SameInputsReplayIdentically()
    {
        var first = BuildMixedScene();
        var second = BuildMixedScene();

        for (var step = 0; step < 100; step++)
        {
            first.Step(1f / 60f);
            second.Step(1f / 60f);
        }

        for (var body = 1; body <= first.BodyCapacity; body++)
        {
            Assert.Equal(first.IsAlive(body), second.IsAlive(body));
            if (!first.IsAlive(body))
            {
                continue;
            }

            Assert.Equal(first.Position(body), second.Position(body));
            Assert.Equal(first.Angle(body), second.Angle(body));
            Assert.Equal(first.Velocity(body), second.Velocity(body));
            Assert.Equal(first.AngularVelocity(body), second.AngularVelocity(body));
        }
    }

    [Fact]
    public void BoxRestingOnABoxKeepsTwoContactPointsAndSleeps()
    {
        var world = DownwardWorld();
        CreateGround(world);
        var box = world.CreateBox(BodyType.Dynamic, new Vector2(0.2f, -0.6f), new Vector2(0.5f), 0f, Solid);
        Run(world, 240);

        Assert.Equal(2, world.Debug.ContactCount);
        Assert.False(world.IsAwake(box));
        Assert.InRange(world.Position(box).Y, -0.5f, -0.49f);
        Assert.InRange(world.Angle(box), -0.001f, 0.001f);
        Assert.InRange(world.Position(box).X, 0.19f, 0.21f);
    }

    [Fact]
    public void RaycastHitsTheNearestShapeWithItsNormal()
    {
        var world = new PhysicsWorld { Gravity = Vector2.Zero };
        var circle = world.CreateCircle(BodyType.Static, new Vector2(3f, 0f), 0.5f, Solid);
        var box = world.CreateBox(BodyType.Dynamic, new Vector2(6f, 0f), new Vector2(0.5f), 0f, Solid);
        var segment = world.CreateSegment(new Vector2(9f, -1f), new Vector2(9f, 1f), Solid);
        var polyline = world.CreatePolyline(new[] { new Vector2(-4f, -1f), new Vector2(-3f, 0f), new Vector2(-4f, 1f) },
            Solid);

        Assert.True(world.Raycast(Vector2.Zero, new Vector2(2f, 0f), 20f, out var hit));
        Assert.Equal(circle, hit.Body);
        Assert.Equal(2.5f, hit.Distance, 4);
        Assert.Equal(-1f, hit.Normal.X, 4);
        Assert.False(world.Raycast(Vector2.Zero, Vector2.UnitX, 2f, out _));

        world.SetCollisionFilter(circle, 2, PhysicsWorld.AllCategories);
        Assert.True(world.Raycast(Vector2.Zero, Vector2.UnitX, 20f, out hit, PhysicsWorld.DefaultCategory));
        Assert.Equal(box, hit.Body);
        Assert.Equal(5.5f, hit.Distance, 4);

        Assert.True(world.Raycast(new Vector2(8f, 0.5f), Vector2.UnitX, 5f, out hit));
        Assert.Equal(segment, hit.Body);
        Assert.Equal(1f, hit.Distance, 4);
        Assert.Equal(-1f, hit.Normal.X, 4);

        Assert.True(world.Raycast(Vector2.Zero, -Vector2.UnitX, 10f, out hit));
        Assert.Equal(polyline, hit.Body);
        Assert.Equal(3f, hit.Distance, 4);
        Assert.Equal(new Vector2(-3f, 0f), hit.Point);
    }

    [Fact]
    public void SensorReportsEnterAndExitWithoutResponse()
    {
        var world = DownwardWorld();
        var reference = DownwardWorld();
        var sensor = world.CreateBox(BodyType.Static, new Vector2(0f, 2f), new Vector2(1f, 0.25f), 0f, Solid,
            BodyFlags.Sensor);
        var ball = world.CreateCircle(BodyType.Dynamic, Vector2.Zero, 0.1f, Solid);
        var free = reference.CreateCircle(BodyType.Dynamic, Vector2.Zero, 0.1f, Solid);

        var entered = -1;
        var exited = -1;
        for (var step = 0; step < 120; step++)
        {
            world.ClearEvents();
            world.Tick();
            reference.Tick();
            for (var index = 0; index < world.EventCount; index++)
            {
                ref readonly var contact = ref world.Event(index);
                Assert.True(contact.Involves(sensor));
                Assert.Equal(ball, contact.Other(sensor));
                if (contact.Kind == ContactEventKind.SensorEnter)
                {
                    entered = step;
                }

                if (contact.Kind == ContactEventKind.SensorExit)
                {
                    exited = step;
                }
            }

            Assert.Equal(reference.Position(free), world.Position(ball));
            Assert.Equal(reference.Velocity(free), world.Velocity(ball));
        }

        Assert.True(entered > 0);
        Assert.True(exited > entered);
    }

    [Fact]
    public void LandingReportsAHitWithItsImpulse()
    {
        var world = DownwardWorld();
        var ground = CreateGround(world);
        var ball = world.CreateCircle(BodyType.Dynamic, new Vector2(0f, -1.25f), 0.25f,
            new PhysicsMaterial(1f, 0.5f, 0.6f));
        var mass = world.Mass(ball);

        for (var step = 0; step < 120; step++)
        {
            world.ClearEvents();
            var speed = world.Velocity(ball).Y;
            world.Tick();
            if (world.EventCount == 0)
            {
                continue;
            }

            ref readonly var hit = ref world.Event(0);
            Assert.Equal(ContactEventKind.Hit, hit.Kind);
            Assert.True(hit.Involves(ground) && hit.Involves(ball));
            Assert.InRange(hit.Impulse, mass * 1.5f * speed * 0.85f, mass * 1.5f * speed * 1.15f);
            return;
        }

        Assert.Fail("The ball never landed.");
    }

    [Fact]
    public void DestroyingTheSupportWakesTheBodyAbove()
    {
        var world = DownwardWorld();
        CreateGround(world);
        var support = world.CreateBox(BodyType.Dynamic, new Vector2(0f, -0.5f), new Vector2(0.5f), 0f, Solid);
        var top = world.CreateBox(BodyType.Dynamic, new Vector2(0f, -1.5f), new Vector2(0.5f), 0f, Solid);
        Run(world, 240);
        Assert.False(world.IsAwake(top));

        world.DestroyBody(support);
        Assert.True(world.IsAwake(top));
        Run(world, 120);

        Assert.InRange(world.Position(top).Y, -0.52f, -0.48f);
    }

    [Fact]
    public void CircleRollsAcrossPolylineVerticesWithoutBumping()
    {
        var world = DownwardWorld();
        var floor = new Vector2[12];
        for (var index = 0; index < floor.Length; index++)
        {
            floor[index] = new Vector2(index - 1f, 0f);
        }

        world.CreatePolyline(floor, Solid);
        var ball = world.CreateCircle(BodyType.Dynamic, new Vector2(0.5f, -0.25f), 0.25f, Solid);
        world.SetVelocity(ball, new Vector2(3f, 0f));
        Run(world, 10);

        for (var step = 0; step < 200; step++)
        {
            world.Tick();
            Assert.InRange(world.Velocity(ball).Y, -0.1f, 0.1f);
            Assert.InRange(world.Position(ball).Y, -0.26f, -0.24f);
        }

        Assert.True(world.Position(ball).X > 3.5f);
    }

    [Fact]
    public void PredictPathMatchesTheFlight()
    {
        var world = DownwardWorld();
        var ball = world.CreateCircle(BodyType.Dynamic, Vector2.Zero, 0.1f, Solid);
        world.SetDamping(ball, 0.3f, 0f);
        var launch = new Vector2(4f, -6f);
        world.SetVelocity(ball, launch);
        var path = new Vector2[60];
        world.PredictPath(Vector2.Zero, launch, 1f, 0.3f, 1, path);

        for (var index = 0; index < path.Length; index++)
        {
            world.Tick();
            Assert.Equal(path[index], world.Position(ball));
        }
    }

    [Fact]
    public void OverlapCircleFindsTouchingBodies()
    {
        var world = new PhysicsWorld { Gravity = Vector2.Zero };
        var near = world.CreateCircle(BodyType.Static, new Vector2(1f, 0f), 0.5f, Solid);
        var box = world.CreateBox(BodyType.Dynamic, new Vector2(0f, 1.2f), new Vector2(0.5f), 0f, Solid);
        world.CreateCircle(BodyType.Static, new Vector2(5f, 0f), 0.5f, Solid);
        world.CreateBox(BodyType.Static, new Vector2(0f, -1f), new Vector2(0.5f), 0f, Solid, BodyFlags.Sensor);
        Span<int> found = stackalloc int[8];

        var count = world.OverlapCircle(Vector2.Zero, 0.8f, found);

        Assert.Equal(2, count);
        Assert.Contains(near, found[..count].ToArray());
        Assert.Contains(box, found[..count].ToArray());
    }

    [Fact]
    public void KinematicPlatformCarriesARestingBox()
    {
        var world = DownwardWorld();
        var platform = world.CreateBox(BodyType.Kinematic, new Vector2(0f, 0.25f), new Vector2(2f, 0.25f), 0f, Solid);
        var box = world.CreateBox(BodyType.Dynamic, new Vector2(0f, -0.25f), new Vector2(0.25f), 0f, Solid);
        Run(world, 60);

        world.SetVelocity(platform, new Vector2(1f, 0f));
        Run(world, 120);

        Assert.InRange(world.Position(platform).X, 0.99f, 1.01f);
        Assert.InRange(world.Position(box).X, 0.9f, 1.05f);
        Assert.True(world.IsAwake(box));
    }

    [Fact]
    public void StepDoesNotAllocate()
    {
        var world = BuildMixedScene();
        Span<int> found = stackalloc int[16];
        for (var frame = 0; frame < 60; frame++)
        {
            world.Step(1f / 60f);
            world.Raycast(Vector2.Zero, Vector2.UnitY, 10f, out _);
            world.OverlapCircle(Vector2.Zero, 2f, found);
        }

        var cleanest = long.MaxValue;
        for (var window = 0; window < 5; window++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var frame = 0; frame < 120; frame++)
            {
                world.Step(1f / 60f);
                world.Raycast(Vector2.Zero, Vector2.UnitY, 10f, out _);
                world.OverlapCircle(Vector2.Zero, 2f, found);
            }

            cleanest = Math.Min(cleanest, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.Equal(0, cleanest);
    }

    private static PhysicsWorld DownwardWorld() => new() { Gravity = new Vector2(0f, Gravity) };

    private static int CreateGround(PhysicsWorld world) =>
        world.CreateBox(BodyType.Static, new Vector2(0f, 0.5f), new Vector2(20f, 0.5f), 0f, Solid);

    private static void Run(PhysicsWorld world, int steps)
    {
        for (var step = 0; step < steps; step++)
        {
            world.Tick();
        }
    }

    private static float SlopeSlide(float friction)
    {
        var world = DownwardWorld();
        var angle = 20f * MathF.PI / 180f;
        var material = new PhysicsMaterial(1f, 0f, friction);
        world.CreateBox(BodyType.Static, Vector2.Zero, new Vector2(10f, 0.5f), angle, material);
        var up = PhysicsMath.Rotate(PhysicsMath.Rotation(angle), new Vector2(0f, -1f));
        var start = up * 0.751f;
        var box = world.CreateBox(BodyType.Dynamic, start, new Vector2(0.25f), angle, material);
        Run(world, 240);
        return Vector2.Distance(world.Position(box), start);
    }

    private static float ChainLength(PhysicsWorld world, int rope)
    {
        var total = 0f;
        for (var index = 0; index < world.RopeSegments(rope); index++)
        {
            total += Vector2.Distance(world.RopePoint(rope, index), world.RopePoint(rope, index + 1));
        }

        return total;
    }

    private static PhysicsWorld BuildMixedScene()
    {
        var world = DownwardWorld();
        CreateGround(world);
        world.CreatePolyline(new[] { new Vector2(-6f, -4f), new Vector2(-5f, 0f), new Vector2(5f, 0f), new Vector2(6f, -4f) },
            Solid);
        for (var index = 0; index < 5; index++)
        {
            world.CreateBox(BodyType.Dynamic, new Vector2(-2f + index * 0.3f, -1f - index * 1.1f), new Vector2(0.4f, 0.3f),
                index * 0.2f, Solid);
            world.CreateCircle(BodyType.Dynamic, new Vector2(2f + index * 0.1f, -1f - index), 0.3f,
                new PhysicsMaterial(1f, 0.4f, 0.3f));
        }

        var flipper = world.CreateBox(BodyType.Dynamic, new Vector2(0.6f, -3f), new Vector2(0.6f, 0.08f), 0f, Solid);
        var hinge = world.CreateHinge(PhysicsWorld.Ground, flipper, new Vector2(0f, -3f));
        world.SetHingeLimits(hinge, -0.5f, 0.5f);
        world.SetHingeMotor(hinge, -15f, 80f);
        var weight = world.CreateCircle(BodyType.Dynamic, new Vector2(4f, -6f), 0.2f, Solid);
        world.CreateRope(new Vector2(3f, -8f), weight, 6, 2.5f);
        var bullet = world.CreateCircle(BodyType.Dynamic, new Vector2(-3f, -6f), 0.1f, Solid, BodyFlags.Bullet);
        world.SetVelocity(bullet, new Vector2(40f, 25f));
        world.CreateBox(BodyType.Static, new Vector2(0f, -2f), new Vector2(1f, 0.2f), 0f, Solid, BodyFlags.Sensor);
        return world;
    }
}
