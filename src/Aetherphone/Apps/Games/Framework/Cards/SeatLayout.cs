using Aetherphone.Core;

namespace Aetherphone.Apps.Games.Framework.Cards;

internal static class SeatLayout
{
    public const int MinSeats = 2;
    public const int MaxSeats = GameSeats.Max;
    private const float BottomAngle = MathF.PI * 0.5f;

    public static float Angle(int seat, int seats, int bottomSeat = 0)
    {
        var count = Math.Clamp(seats, 1, MaxSeats);
        var slot = ((seat - bottomSeat) % count + count) % count;
        return BottomAngle + MathF.Tau * slot / count;
    }

    public static Vector2 Seat(int seat, int seats, Rect rect, int bottomSeat = 0)
    {
        var angle = Angle(seat, seats, bottomSeat);
        var center = rect.Center;
        return new Vector2(center.X + MathF.Cos(angle) * rect.Width * 0.5f,
            center.Y + MathF.Sin(angle) * rect.Height * 0.5f);
    }

    public static void Ring(int seats, Rect rect, Span<Vector2> centers, int bottomSeat = 0)
    {
        var limit = Math.Min(Math.Clamp(seats, 1, MaxSeats), centers.Length);
        for (var seat = 0; seat < limit; seat++)
        {
            centers[seat] = Seat(seat, seats, rect, bottomSeat);
        }
    }

    public static Vector2 Inward(Vector2 seat, Rect rect, float distance)
    {
        var toward = rect.Center - seat;
        var length = toward.Length();
        return length <= distance || length <= 0.0001f ? rect.Center : seat + toward / length * distance;
    }
}
