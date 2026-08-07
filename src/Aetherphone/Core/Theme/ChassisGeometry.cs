using Aetherphone.Core.Animation;

namespace Aetherphone.Core.Theme;

internal readonly struct ChassisGeometry
{
    private const float PuckRoundingFraction = 0.300f;
    private const float PuckMetalFraction = 0.030f;
    private const float PuckGlassFraction = 0.060f;

    public readonly Rect Body;
    public readonly Rect Glass;
    public readonly Rect Screen;
    public readonly float BodyRadius;
    public readonly float GlassRadius;
    public readonly float ScreenRadius;

    private ChassisGeometry(Rect body, float bodyRadius, float metalWidth, float glassWidth)
    {
        var snapped = Snap(body);
        var limit = MathF.Max(MathF.Min(snapped.Width, snapped.Height) * 0.5f, 0f);
        var metal = SnapBand(metalWidth, limit);
        var glass = SnapBand(glassWidth, limit - metal);
        Body = snapped;
        Glass = snapped.Inset(metal);
        Screen = Glass.Inset(glass);
        BodyRadius = Math.Clamp(bodyRadius, 0f, limit);
        GlassRadius = MathF.Max(BodyRadius - metal, 0f);
        ScreenRadius = MathF.Max(GlassRadius - glass, 0f);
    }

    public static ChassisGeometry Device(Rect window, PhoneTheme theme, float scale)
    {
        var body = BodyRect(window, theme, scale);
        return new ChassisGeometry(body, theme.DeviceRounding * scale, theme.MetalWidth * scale,
            theme.GlassWidth * scale);
    }

    public static Rect BodyRect(Rect window, PhoneTheme theme, float scale)
    {
        var rail = theme.RailWidth * scale;
        if (window.IsLandscape())
        {
            return new Rect(new Vector2(window.Min.X, window.Min.Y + rail),
                new Vector2(window.Max.X, window.Max.Y - rail));
        }

        return new Rect(new Vector2(window.Min.X + rail, window.Min.Y),
            new Vector2(window.Max.X - rail, window.Max.Y));
    }

    public static ChassisGeometry Puck(Rect body) =>
        new(body, body.Width * PuckRoundingFraction, body.Width * PuckMetalFraction,
            body.Width * PuckGlassFraction);

    public static ChassisGeometry Morph(Rect body, PhoneTheme theme, float scale, float eased) =>
        new(body, Easing.Lerp(theme.DeviceRounding * scale, body.Width * PuckRoundingFraction, eased),
            Easing.Lerp(theme.MetalWidth * scale, body.Width * PuckMetalFraction, eased),
            Easing.Lerp(theme.GlassWidth * scale, body.Width * PuckGlassFraction, eased));

    public static ChassisGeometry Preview(Rect body, PhoneCaseKind kind)
    {
        var metrics = ChassisMetrics.ForBody(kind, body.Width);
        return new ChassisGeometry(body, metrics.DeviceRounding, metrics.MetalWidth, metrics.GlassWidth);
    }

    private static float SnapBand(float width, float limit)
    {
        if (width <= 0f || limit <= 0f)
        {
            return 0f;
        }

        return MathF.Min(MathF.Max(MathF.Round(width), 1f), limit);
    }

    private static Rect Snap(Rect rect) =>
        new(new Vector2(MathF.Round(rect.Min.X), MathF.Round(rect.Min.Y)),
            new Vector2(MathF.Round(rect.Max.X), MathF.Round(rect.Max.Y)));
}
