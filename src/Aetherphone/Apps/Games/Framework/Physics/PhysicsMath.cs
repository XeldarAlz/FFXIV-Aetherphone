namespace Aetherphone.Apps.Games.Framework.Physics;

internal static class PhysicsMath
{
    public static float Cross(Vector2 left, Vector2 right) => left.X * right.Y - left.Y * right.X;

    public static Vector2 Cross(float scalar, Vector2 vector) => new(-scalar * vector.Y, scalar * vector.X);

    public static Vector2 Rotation(float angle) => new(MathF.Cos(angle), MathF.Sin(angle));

    public static Vector2 Rotate(Vector2 rotation, Vector2 vector) =>
        new(rotation.X * vector.X - rotation.Y * vector.Y, rotation.Y * vector.X + rotation.X * vector.Y);

    public static Vector2 InverseRotate(Vector2 rotation, Vector2 vector) =>
        new(rotation.X * vector.X + rotation.Y * vector.Y, rotation.X * vector.Y - rotation.Y * vector.X);

    public static Vector2 RightPerpendicular(Vector2 vector) => new(vector.Y, -vector.X);

    public static Vector2 SolveSymmetric(float massXX, float massXY, float massYY, Vector2 right)
    {
        var determinant = massXX * massYY - massXY * massXY;
        if (determinant != 0f)
        {
            determinant = 1f / determinant;
        }

        return new Vector2(determinant * (massYY * right.X - massXY * right.Y),
            determinant * (massXX * right.Y - massXY * right.X));
    }

    public static bool SegmentsCross(Vector2 startA, Vector2 endA, Vector2 startB, Vector2 endB)
    {
        var spanA = endA - startA;
        var spanB = endB - startB;
        var denominator = Cross(spanA, spanB);
        if (MathF.Abs(denominator) < 1e-9f)
        {
            return false;
        }

        var offset = startB - startA;
        var alongA = Cross(offset, spanB) / denominator;
        var alongB = Cross(offset, spanA) / denominator;
        return alongA >= 0f && alongA <= 1f && alongB >= 0f && alongB <= 1f;
    }
}
