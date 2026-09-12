namespace Aetherphone.Core.GameChat;

internal enum PopoutPlacement : byte
{
    BesidePhone,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

internal static class PopoutPlacements
{
    public static Vector2 Resolve(PopoutPlacement mode, Rect viewport, Rect? phone, Vector2 size, float margin,
        float stagger)
    {
        var origin = mode switch
        {
            PopoutPlacement.TopLeft => viewport.Min + new Vector2(margin + stagger, margin + stagger),
            PopoutPlacement.TopRight => new Vector2(viewport.Max.X - margin - size.X - stagger,
                viewport.Min.Y + margin + stagger),
            PopoutPlacement.BottomLeft => new Vector2(viewport.Min.X + margin + stagger,
                viewport.Max.Y - margin - size.Y - stagger),
            PopoutPlacement.BottomRight => viewport.Max - size - new Vector2(margin + stagger, margin + stagger),
            _ => BesidePhone(viewport, phone, size, margin, stagger),
        };
        return Clamp(origin, viewport, size);
    }

    private static Vector2 BesidePhone(Rect viewport, Rect? phone, Vector2 size, float margin, float stagger)
    {
        if (phone is not { } frame || frame.Width <= 0f)
        {
            return viewport.Center - size * 0.5f + new Vector2(stagger, stagger);
        }

        var left = frame.Min.X - margin - size.X;
        if (left >= viewport.Min.X)
        {
            return new Vector2(left - stagger, frame.Min.Y + stagger);
        }

        var right = frame.Max.X + margin;
        if (right + size.X <= viewport.Max.X)
        {
            return new Vector2(right + stagger, frame.Min.Y + stagger);
        }

        return viewport.Center - size * 0.5f + new Vector2(stagger, stagger);
    }

    private static Vector2 Clamp(Vector2 origin, Rect viewport, Vector2 size)
    {
        var maxX = MathF.Max(viewport.Min.X, viewport.Max.X - size.X);
        var maxY = MathF.Max(viewport.Min.Y, viewport.Max.Y - size.Y);
        return new Vector2(Math.Clamp(origin.X, viewport.Min.X, maxX), Math.Clamp(origin.Y, viewport.Min.Y, maxY));
    }
}
