using System.Numerics;

namespace Aetherphone.Core;

internal readonly record struct Rect(Vector2 Min, Vector2 Max)
{
    public float Width => Max.X - Min.X;
    public float Height => Max.Y - Min.Y;
    public Vector2 Size => Max - Min;
    public Vector2 Center => (Min + Max) * 0.5f;
    public Rect Inset(float amount) => new(Min + new Vector2(amount, amount), Max - new Vector2(amount, amount));
    public Rect Translate(Vector2 offset) => new(Min + offset, Max + offset);
    public bool Contains(Vector2 point) => point.X >= Min.X && point.X <= Max.X && point.Y >= Min.Y && point.Y <= Max.Y;
}
