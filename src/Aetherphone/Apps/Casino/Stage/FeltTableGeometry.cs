using Aetherphone.Core;

namespace Aetherphone.Apps.Casino.Stage;

internal readonly struct FeltTableGeometry
{
    public const int MaxSeats = 7;
    public const float DealerFraction = 0.12f;
    public const float ArcStart = 0.08f;
    public const float ArcEnd = 0.92f;
    public const float SeatSpacing = 0.92f;
    public const float MaximumSeatRadius = 40f;
    public const float CircleShare = 0.72f;
    public const float CircleGap = 10f;
    public const float MinimumCircleRadius = 22f;
    public const float PrintShare = 0.5f;

    public readonly Rect Table;
    public readonly Vector2 DealerAnchor;
    public readonly Vector2 ArcRadii;
    public readonly int SeatCount;
    public readonly float SeatRadius;
    public readonly float CircleRadius;

    private FeltTableGeometry(Rect table, Vector2 dealerAnchor, Vector2 arcRadii, int seatCount, float seatRadius,
        float circleRadius)
    {
        Table = table;
        DealerAnchor = dealerAnchor;
        ArcRadii = arcRadii;
        SeatCount = seatCount;
        SeatRadius = seatRadius;
        CircleRadius = circleRadius;
    }

    public Vector2 PrintRadii => ArcRadii * PrintShare;

    public static FeltTableGeometry Compute(Rect table, int seatCount, float scale)
    {
        var seats = Math.Clamp(seatCount, 1, MaxSeats);
        var dealer = new Vector2(table.Center.X, table.Min.Y + table.Height * DealerFraction);
        var minimum = SeatSpot.MinimumRadius * scale;
        var maximum = MaximumSeatRadius * scale;
        var seatRadius = maximum;
        var radii = RadiiFor(table, dealer, seatRadius);
        var spacing = BottomSpacing(radii.X, seats);
        if (spacing < seatRadius * 2f)
        {
            seatRadius = Math.Clamp(spacing * 0.5f * SeatSpacing, minimum, maximum);
            radii = RadiiFor(table, dealer, seatRadius);
        }

        var circle = MathF.Max(MinimumCircleRadius * scale, seatRadius * CircleShare);
        return new FeltTableGeometry(table, dealer, radii, seats, seatRadius, circle);
    }

    public float SeatAngle(int index)
    {
        if (SeatCount <= 1)
        {
            return MathF.PI * 0.5f;
        }

        var share = index / (float)(SeatCount - 1);
        return MathF.PI * (ArcEnd - (ArcEnd - ArcStart) * share);
    }

    public Vector2 Seat(int index) => PointOn(ArcRadii, SeatAngle(index));

    public Vector2 BettingCircle(int index)
    {
        var inset = SeatRadius + CircleGap + CircleRadius;
        var radii = new Vector2(MathF.Max(0f, ArcRadii.X - inset), MathF.Max(0f, ArcRadii.Y - inset));
        return PointOn(radii, SeatAngle(index));
    }

    public Vector2 PointOn(Vector2 radii, float angle) =>
        DealerAnchor + new Vector2(MathF.Cos(angle) * radii.X, MathF.Sin(angle) * radii.Y);

    private static Vector2 RadiiFor(Rect table, Vector2 dealer, float seatRadius) =>
        new(MathF.Max(0f, table.Width * 0.5f - seatRadius), MathF.Max(0f, table.Max.Y - seatRadius - dealer.Y));

    private static float BottomSpacing(float radiusX, int seats)
    {
        if (seats <= 1)
        {
            return float.MaxValue;
        }

        var step = MathF.PI * (ArcEnd - ArcStart) / (seats - 1);
        return 2f * radiusX * MathF.Sin(step * 0.5f);
    }
}
