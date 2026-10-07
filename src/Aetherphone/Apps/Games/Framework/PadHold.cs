namespace Aetherphone.Apps.Games.Framework;

internal enum HoldEdge : byte
{
    None,
    Pressed,
    Released,
}

internal struct HeldPadState
{
    public PadDirection Held { get; private set; }

    public bool Engaged { get; private set; }

    public PadDirection Update(Vector2 pointer, bool down, bool pressedInside, Vector2 center, float deadZone)
    {
        if (!down)
        {
            Release();
            return PadDirection.None;
        }

        if (pressedInside)
        {
            Engaged = true;
        }

        if (!Engaged)
        {
            return PadDirection.None;
        }

        var next = Sector(pointer - center, deadZone);
        var pressed = next != PadDirection.None && next != Held ? next : PadDirection.None;
        Held = next;
        return pressed;
    }

    public void Release()
    {
        Engaged = false;
        Held = PadDirection.None;
    }

    public static PadDirection Sector(Vector2 offset, float deadZone)
    {
        if (offset.LengthSquared() < deadZone * deadZone)
        {
            return PadDirection.None;
        }

        if (MathF.Abs(offset.X) > MathF.Abs(offset.Y))
        {
            return offset.X > 0f ? PadDirection.Right : PadDirection.Left;
        }

        return offset.Y > 0f ? PadDirection.Down : PadDirection.Up;
    }
}

internal struct HoldLatch
{
    public bool Held { get; private set; }

    public HoldEdge Update(bool pressedInside, bool down)
    {
        if (!Held)
        {
            if (!pressedInside || !down)
            {
                return HoldEdge.None;
            }

            Held = true;
            return HoldEdge.Pressed;
        }

        if (down)
        {
            return HoldEdge.None;
        }

        Held = false;
        return HoldEdge.Released;
    }

    public void Release()
    {
        Held = false;
    }
}
