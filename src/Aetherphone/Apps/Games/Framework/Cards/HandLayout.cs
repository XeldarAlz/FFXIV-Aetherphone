namespace Aetherphone.Apps.Games.Framework.Cards;

internal readonly struct FanSlot
{
    public readonly Vector2 Center;
    public readonly float Angle;

    public FanSlot(Vector2 center, float angle)
    {
        Center = center;
        Angle = angle;
    }

    public CardPose Pose(float cardWidth, bool faceUp = true) => new(Center, cardWidth, Angle, faceUp);
}

internal static class HandLayout
{
    private const float FlatAngle = 0.0001f;

    public static FanSlot Fan(int index, int count, Vector2 center, float width, float maxAngle,
        float maxStep = float.PositiveInfinity)
    {
        if (count <= 1)
        {
            return new FanSlot(center, 0f);
        }

        var spread = MathF.Min(width, (count - 1) * maxStep);
        var along = index / (float)(count - 1) * 2f - 1f;
        if (MathF.Abs(maxAngle) < FlatAngle || width <= 0f)
        {
            return new FanSlot(new Vector2(center.X + along * spread * 0.5f, center.Y), 0f);
        }

        var radius = width * 0.5f / MathF.Sin(MathF.Abs(maxAngle));
        var halfAngle = MathF.Asin(Math.Clamp(spread * 0.5f / radius, -1f, 1f)) * MathF.Sign(maxAngle);
        var angle = along * halfAngle;
        return new FanSlot(new Vector2(center.X + radius * MathF.Sin(angle), center.Y + radius * (1f - MathF.Cos(angle))),
            angle);
    }

    public static void Fan(int count, Vector2 center, float width, float maxAngle, Span<FanSlot> slots,
        float maxStep = float.PositiveInfinity)
    {
        var limit = Math.Min(count, slots.Length);
        for (var index = 0; index < limit; index++)
        {
            slots[index] = Fan(index, count, center, width, maxAngle, maxStep);
        }
    }

    public static int HitTest(ReadOnlySpan<CardPose> poses, Vector2 point)
    {
        for (var index = poses.Length - 1; index >= 0; index--)
        {
            if (poses[index].Contains(point))
            {
                return index;
            }
        }

        return -1;
    }
}
