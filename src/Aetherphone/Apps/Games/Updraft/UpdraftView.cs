using Aetherphone.Core;

namespace Aetherphone.Apps.Games.Updraft;

internal readonly struct UpdraftView
{
    public readonly Rect Body;
    public readonly float Left;
    public readonly float Bottom;
    public readonly float Unit;
    public readonly float Camera;

    private UpdraftView(Rect body, float left, float bottom, float unit, float camera)
    {
        Body = body;
        Left = left;
        Bottom = bottom;
        Unit = unit;
        Camera = camera;
    }

    public float FieldPixels => UpdraftBoard.FieldWidth * Unit;

    public float Metres => (Camera + UpdraftBoard.ViewHeight * 0.5f) * UpdraftBoard.MetresPerUnit;

    public static UpdraftView Fit(Rect body, float camera, Vector2 shake)
    {
        var unit = MathF.Max(1f, MathF.Min(body.Width / UpdraftBoard.FieldWidth, body.Height / UpdraftBoard.ViewHeight));
        var left = body.Center.X - UpdraftBoard.FieldWidth * unit * 0.5f + shake.X;
        return new UpdraftView(body, left, body.Max.Y + shake.Y, unit, camera);
    }

    public Vector2 ToScreen(float x, float y) => new(Left + x * Unit, Bottom - (y - Camera) * Unit);

    public float ScreenY(float y) => Bottom - (y - Camera) * Unit;

    public float WorldX(float screenX) => UpdraftBoard.Wrap((screenX - Left) / Unit);

    public int FirstCopy(float x, float margin) =>
        (int)MathF.Ceiling((Body.Min.X - margin - (Left + x * Unit)) / FieldPixels);

    public int LastCopy(float x, float margin) =>
        (int)MathF.Floor((Body.Max.X + margin - (Left + x * Unit)) / FieldPixels);
}

internal readonly struct ShapeFrame
{
    public readonly Vector2 Origin;
    public readonly Vector2 AxisX;
    public readonly Vector2 AxisY;

    public ShapeFrame(Vector2 origin, Vector2 axisX, Vector2 axisY)
    {
        Origin = origin;
        AxisX = axisX;
        AxisY = axisY;
    }

    public static ShapeFrame Rotated(Vector2 origin, float scaleX, float scaleY, float rotation)
    {
        var cosine = MathF.Cos(rotation);
        var sine = MathF.Sin(rotation);
        return new ShapeFrame(origin, new Vector2(cosine, sine) * scaleX, new Vector2(-sine, cosine) * scaleY);
    }

    public bool Mirrored => AxisX.X * AxisY.Y - AxisX.Y * AxisY.X < 0f;

    public Vector2 Point(float x, float y) => Origin + AxisX * x + AxisY * y;

    public Vector2 Point(Vector2 local) => Origin + AxisX * local.X + AxisY * local.Y;
}
