namespace Aetherphone.Core.Shell;

internal readonly struct SwitcherLayout
{
    public readonly Rect Screen;
    public readonly float CardWidth;
    public readonly float CardHeight;
    public readonly float Pitch;
    public readonly float CenterY;
    public readonly float MaxScroll;

    public SwitcherLayout(Rect screen, float cardWidth, float cardHeight, float pitch, float centerY, float maxScroll)
    {
        Screen = screen;
        CardWidth = cardWidth;
        CardHeight = cardHeight;
        Pitch = pitch;
        CenterY = centerY;
        MaxScroll = maxScroll;
    }
}

internal static class SwitcherGeometry
{
    public const float CardHeightFraction = 0.52f;
    public const float CardCenterFraction = 0.46f;
    public const float CardGapUnits = 16f;
    public const float OverscrollResistance = 0.35f;
    public const float CloseCommitFraction = 0.30f;
    public const float FlingProjectSeconds = 0.12f;
    public const float ParallaxFactor = 0.15f;
    public const float ParallaxMarginSlots = 1f;

    public static SwitcherLayout Layout(Rect screen, float scale, int cardCount)
    {
        var cardHeight = screen.Height * CardHeightFraction;
        var cardWidth = cardHeight * (screen.Width / MathF.Max(1f, screen.Height));
        var pitch = cardWidth + CardGapUnits * scale;
        var centerY = screen.Min.Y + screen.Height * CardCenterFraction;
        var maxScroll = MathF.Max(0f, cardCount - 1);
        return new SwitcherLayout(screen, cardWidth, cardHeight, pitch, centerY, maxScroll);
    }

    public static Rect CardRest(in SwitcherLayout layout, float slot, float scroll)
    {
        var half = new Vector2(layout.CardWidth, layout.CardHeight) * 0.5f;
        var center = new Vector2(layout.Screen.Center.X + (slot - scroll) * layout.Pitch, layout.CenterY);
        return new Rect(center - half, center + half);
    }

    public static Rect Scaled(Rect rect, float factor)
    {
        var center = rect.Center;
        var half = rect.Size * (0.5f * factor);
        return new Rect(center - half, center + half);
    }

    public static float ParallaxReachSlots(int cardCount) =>
        MathF.Max(0f, cardCount - 1) + 2f * ParallaxMarginSlots;

    public static Rect ParallaxQuad(Rect screen, float scroll, float pitch, float reachSlots)
    {
        var maxScroll = MathF.Max(0f, reachSlots - 2f * ParallaxMarginSlots);
        var clamped = Math.Clamp(scroll, -ParallaxMarginSlots, maxScroll + ParallaxMarginSlots);
        var shift = ParallaxFactor * pitch;
        var reach = shift * reachSlots;
        var zoom = screen.Width > 0f ? (screen.Width + reach) / screen.Width : 1f;
        var height = screen.Height * zoom;
        var top = screen.Center.Y - height * 0.5f;
        var left = screen.Min.X - shift * (ParallaxMarginSlots + clamped);
        return new Rect(new Vector2(left, top), new Vector2(left + screen.Width + reach, top + height));
    }

    public static float RubberBand(float raw, float maxScroll)
    {
        if (raw < 0f)
        {
            return raw * OverscrollResistance;
        }

        if (raw > maxScroll)
        {
            return maxScroll + (raw - maxScroll) * OverscrollResistance;
        }

        return raw;
    }

    public static int SnapSlot(float projected, int cardCount)
    {
        if (cardCount <= 0)
        {
            return 0;
        }

        return (int)Math.Clamp(MathF.Round(projected), 0f, cardCount - 1);
    }

    public static float ProjectedScroll(float panStart, float travelX, float velocityX, float pitch)
    {
        if (pitch <= 0f)
        {
            return panStart;
        }

        return panStart - (travelX + velocityX * FlingProjectSeconds) / pitch;
    }

    public static float CardRounding(float cardWidth, float screenWidth, float screenRadius) =>
        screenRadius * (cardWidth / MathF.Max(1f, screenWidth));

    public static bool ClosesOnRelease(float lift, float cardHeight, float velocityY, float flingUnitsPerSecond) =>
        lift > cardHeight * CloseCommitFraction || -velocityY > flingUnitsPerSecond;
}
