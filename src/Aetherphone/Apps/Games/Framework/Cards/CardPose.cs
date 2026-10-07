namespace Aetherphone.Apps.Games.Framework.Cards;

internal readonly struct CardPose
{
    public const float Aspect = 1.4f;

    public readonly Vector2 Center;
    public readonly float Width;
    public readonly float Angle;
    public readonly bool FaceUp;
    public readonly float Squash;

    public CardPose(Vector2 center, float width, float angle = 0f, bool faceUp = true, float squash = 1f)
    {
        Center = center;
        Width = width;
        Angle = angle;
        FaceUp = faceUp;
        Squash = squash;
    }

    public float Height => Width * Aspect;

    public Vector2 Up => new(MathF.Sin(Angle), -MathF.Cos(Angle));

    public CardPose Lifted(float distance) => new(Center + Up * distance, Width, Angle, FaceUp, Squash);

    public CardPose Moved(Vector2 offset) => new(Center + offset, Width, Angle, FaceUp, Squash);

    public CardPose Turned(bool faceUp) => new(Center, Width, Angle, faceUp, Squash);

    public CardPose Sized(float width) => new(Center, width, Angle, FaceUp, Squash);

    public bool Contains(Vector2 point)
    {
        var offset = point - Center;
        var cosine = MathF.Cos(Angle);
        var sine = MathF.Sin(Angle);
        var localX = offset.X * cosine + offset.Y * sine;
        var localY = -offset.X * sine + offset.Y * cosine;
        return MathF.Abs(localX) <= Width * 0.5f * MathF.Abs(Squash) && MathF.Abs(localY) <= Height * 0.5f;
    }

    public static CardPose Lerp(in CardPose from, in CardPose to, float amount) =>
        new(Vector2.Lerp(from.Center, to.Center, amount), from.Width + (to.Width - from.Width) * amount,
            from.Angle + (to.Angle - from.Angle) * amount, amount < 0.5f ? from.FaceUp : to.FaceUp,
            from.Squash + (to.Squash - from.Squash) * amount);
}
