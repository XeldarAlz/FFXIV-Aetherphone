namespace Aetherphone.Apps.Games.Pinball;

internal static class PinballTable
{
    public const float Width = 6f;
    public const float Height = 12f;
    public const float BallRadius = 0.15f;
    public const float DrainY = 11.95f;
    public const float CabinetBottom = 12.3f;
    public const float LaneInnerX = 5.4f;
    public const float LaneOuterX = 6f;
    public const float LaneCenterX = 5.7f;
    public const float PlungerFloorY = 11.6f;
    public const float PlungerRestY = PlungerFloorY - BallRadius - 0.01f;
    public const float FlipperLength = 1f;
    public const float FlipperHalfThickness = 0.085f;
    public const float FlipperSwing = 0.97f;
    public const float BumperRadius = 0.33f;
    public const float SaucerRadius = 0.24f;
    public const float SaucerCatchRadius = 0.05f;
    public const float PostRadius = 0.07f;
    public const float SpinnerHalfWidth = 0.28f;
    public const float LaneGuideTop = 1.2f;
    public const float LaneGuideBottom = 1.85f;
    public const int LeftFlipper = 0;
    public const int RightFlipper = 1;
    public const int FlipperCount = 2;
    public const int BumperCount = 3;
    public const int SlingCount = 2;
    public const int BankSize = 3;
    public const int TargetCount = BankSize * 2;
    public const int TopLaneCount = 3;
    public const int RolloverCount = 4;
    public const int LeftOutlane = 0;
    public const int RightOutlane = 3;
    private const float LaneTopY = 3f;
    private const float GateHighY = 2.62f;
    private const float LaneClearY = 2.4f;
    private const float FlipperRestAngle = 0.52f;
    private const float RampHalfWidth = 0.25f;
    private const float ArchCenterY = 3f;
    private const float ArchRadiusY = 2.9f;
    private const int ArchSegments = 32;
    private const int RampArcSegments = 12;

    public static readonly Vector2 LeftPivot = new(1.55f, 10.55f);
    public static readonly Vector2 RightPivot = new(3.85f, 10.55f);
    public static readonly Vector2 LanePlunger = new(LaneCenterX, PlungerRestY);
    public static readonly Vector2 Saucer = new(1.45f, 3.05f);
    public static readonly Vector2 SaucerKick = new(1.6f, 3f);
    public static readonly Vector2 Spinner = new(0.3f, 4.6f);
    public static readonly Vector2 SpinnerHalfExtents = new(SpinnerHalfWidth, 0.05f);
    public static readonly Vector2 TargetHalfExtents = new(0.06f, 0.2f);
    public static readonly Vector2 LaneSensorHalfExtents = new(0.12f, 0.1f);
    public static readonly Vector2 RolloverHalfExtents = new(0.12f, 0.08f);
    public static readonly Vector2 RampSensorHalfExtents = new(0.23f, 0.08f);
    public static readonly Vector2 RampEntrance = new(4.6f, 5.25f);
    public static readonly Vector2 RampMade = new(5.7f, 3.4f);
    public static readonly Vector2 RampExit = new(4.66f, 7.95f);
    public static readonly Vector2 GateStart = new(LaneInnerX, LaneTopY);
    public static readonly Vector2 GateEnd = new(LaneOuterX, GateHighY);
    public static readonly Vector2 PlungerFloorStart = new(LaneInnerX, PlungerFloorY);
    public static readonly Vector2 PlungerFloorEnd = new(LaneOuterX, PlungerFloorY);

    public static readonly Vector2[] Bumpers = { new(2.35f, 2.8f), new(3.35f, 2.8f), new(2.85f, 3.65f) };

    public static readonly Vector2[] Targets =
    {
        new(0.92f, 4.45f), new(0.92f, 4.95f), new(0.92f, 5.45f),
        new(4.02f, 5f), new(4.02f, 5.5f), new(4.02f, 6f),
    };

    public static readonly Vector2[] TargetFacing =
    {
        new(1f, 0f), new(1f, 0f), new(1f, 0f),
        new(-1f, 0f), new(-1f, 0f), new(-1f, 0f),
    };

    public static readonly Vector2[] TopLanes = { new(2.25f, 1.55f), new(2.85f, 1.55f), new(3.45f, 1.55f) };

    public static readonly float[] LaneGuides = { 1.95f, 2.55f, 3.15f, 3.75f };

    public static readonly Vector2[] Rollovers =
    {
        new(0.25f, 8.9f), new(0.74f, 8.9f), new(4.66f, 8.9f), new(5.15f, 8.9f),
    };

    public static readonly Vector2[] Posts = { new(0.5f, 8.15f), new(4.9f, 8.15f) };

    public static readonly Vector2[] LaneWall = { new(LaneInnerX, CabinetBottom), new(LaneInnerX, LaneTopY) };

    public static readonly Vector2[] LeftOrbitWall = { new(0.6f, 2.75f), new(0.6f, 6f) };

    public static readonly Vector2[] LeftDeflector = { new(0f, 6.55f), new(0.5f, 7.05f) };

    public static readonly Vector2[] RightOrbitWall = { new(4.85f, 2.9f), new(4.85f, 5.75f) };

    public static readonly Vector2[] RampMouthRail = { new(4.35f, 4.45f), new(4.35f, 5.75f) };

    public static readonly Vector2[] RightDeflector = { new(LaneInnerX, 6.35f), new(4.98f, 6.85f) };

    public static readonly Vector2[] LeftDivider = { new(0.5f, 8.15f), new(0.5f, 9.6f), new(1.42f, 10.38f) };

    public static readonly Vector2[] RightDivider = { new(4.9f, 8.15f), new(4.9f, 9.6f), new(3.98f, 10.38f) };

    public static readonly Vector2[] LeftSling = { new(0.98f, 8.45f), new(0.98f, 9.45f), new(1.55f, 9.9f) };

    public static readonly Vector2[] RightSling = { new(4.42f, 8.45f), new(4.42f, 9.45f), new(3.85f, 9.9f) };

    public static readonly Vector2[] Cabinet = BuildCabinet();

    private static readonly Vector2 RampStart = new(4.6f, 5.75f);

    private static readonly Vector2 RampTurnCenter = new(5.15f, 2.9f);

    private static readonly Vector2[] RampTail =
    {
        new(5.7f, 6.75f), new(5.55f, 7.15f), new(5.25f, 7.42f), new(4.9f, 7.58f), new(4.72f, 7.75f), new(4.66f, 8f),
        new(4.66f, 8.2f),
    };

    public static readonly Vector2[] RampPath = BuildRampPath();

    public static readonly Vector2[] RampOuterRail = Offset(RampPath, -RampHalfWidth);

    public static readonly Vector2[] RampInnerRail = Offset(RampPath, RampHalfWidth);

    public static Vector2 Pivot(int flipper) => flipper == LeftFlipper ? LeftPivot : RightPivot;

    public static float RestAngle(int flipper) =>
        flipper == LeftFlipper ? FlipperRestAngle : MathF.PI - FlipperRestAngle;

    public static Vector2 FlipperDirection(int flipper, float angle) => new(MathF.Cos(angle), MathF.Sin(angle));

    public static ReadOnlySpan<Vector2> Sling(int sling) => sling == 0 ? LeftSling : RightSling;

    public static Vector2 SlingFaceNormal(int sling)
    {
        var points = Sling(sling);
        var along = Vector2.Normalize(points[2] - points[0]);
        var normal = new Vector2(along.Y, -along.X);
        return sling == 0 ? -normal : normal;
    }

    public static bool InLane(Vector2 position) =>
        position.X > LaneInnerX && position.Y > LaneClearY && position.Y < CabinetBottom;

    private static Vector2[] BuildCabinet()
    {
        var points = new Vector2[ArchSegments + 3];
        points[0] = new Vector2(0f, CabinetBottom);
        for (var segment = 0; segment <= ArchSegments; segment++)
        {
            var angle = MathF.PI + MathF.PI * segment / ArchSegments;
            points[segment + 1] = new Vector2(Width * 0.5f + Width * 0.5f * MathF.Cos(angle),
                ArchCenterY + ArchRadiusY * MathF.Sin(angle));
        }

        points[^1] = new Vector2(Width, CabinetBottom);
        return points;
    }

    private static Vector2[] BuildRampPath()
    {
        var turnRadius = RampTurnCenter.X - RampStart.X;
        var points = new Vector2[1 + RampArcSegments + 1 + RampTail.Length];
        points[0] = RampStart;
        var cursor = 1;
        for (var segment = 0; segment <= RampArcSegments; segment++)
        {
            var angle = MathF.PI + MathF.PI * segment / RampArcSegments;
            points[cursor++] = RampTurnCenter + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * turnRadius;
        }

        for (var index = 0; index < RampTail.Length; index++)
        {
            points[cursor++] = RampTail[index];
        }

        return points;
    }

    private static Vector2[] Offset(Vector2[] path, float distance)
    {
        var rail = new Vector2[path.Length];
        for (var index = 0; index < path.Length; index++)
        {
            var before = index > 0 ? Vector2.Normalize(path[index] - path[index - 1]) : Vector2.Zero;
            var after = index < path.Length - 1 ? Vector2.Normalize(path[index + 1] - path[index]) : Vector2.Zero;
            var tangent = Vector2.Normalize(before + after);
            var normal = new Vector2(-tangent.Y, tangent.X);
            var reference = index > 0 ? before : after;
            var miter = MathF.Max(0.5f, Vector2.Dot(normal, new Vector2(-reference.Y, reference.X)));
            rail[index] = path[index] + normal * (distance / miter);
        }

        return rail;
    }
}
