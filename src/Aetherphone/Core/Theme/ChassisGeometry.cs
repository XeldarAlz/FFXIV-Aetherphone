using Aetherphone.Core.Animation;

namespace Aetherphone.Core.Theme;

internal readonly struct ChassisGeometry
{
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

    public static ChassisGeometry Puck(Rect body, PhoneCaseKind kind)
    {
        var metrics = ChassisMetrics.ForPuck(kind, body.Width);
        return new ChassisGeometry(body, metrics.DeviceRounding, metrics.MetalWidth, metrics.GlassWidth);
    }

    public static float PuckBand(float width, PhoneCaseKind kind)
    {
        var metrics = ChassisMetrics.ForPuck(kind, width);
        return (MathF.Max(MathF.Round(metrics.MetalWidth), 1f) + MathF.Max(MathF.Round(metrics.GlassWidth), 1f)) * 2f;
    }

    public static ChassisGeometry Morph(Rect body, PhoneTheme theme, float scale, float eased)
    {
        if (theme.CaseKind == PhoneCaseKind.Art)
        {
            return Puck(body, PhoneCaseKind.Art);
        }

        var puck = ChassisMetrics.ForPuck(theme.CaseKind, body.Width);
        return new ChassisGeometry(body, Easing.Lerp(theme.DeviceRounding * scale, puck.DeviceRounding, eased),
            Easing.Lerp(theme.MetalWidth * scale, puck.MetalWidth, eased),
            Easing.Lerp(theme.GlassWidth * scale, puck.GlassWidth, eased));
    }

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
