namespace Aetherphone.Windows.Components;

internal readonly struct PointerState
{
    public readonly float Scale;
    public readonly float Lift;
    public readonly Vector2 Tilt;
    public readonly float Dim;

    public PointerState(float scale, float lift, Vector2 tilt, float dim)
    {
        Scale = scale;
        Lift = lift;
        Tilt = tilt;
        Dim = dim;
    }

    public static PointerState Rest => new(1f, 0f, Vector2.Zero, 0f);

    public bool Hovered => Lift > 0.001f;
}
