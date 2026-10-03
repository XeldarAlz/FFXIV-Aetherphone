using Aetherphone.Core.Home;
using Aetherphone.Core.Theme;

namespace Aetherphone.Windows.Widgets;

internal readonly struct WidgetInk
{
    private const float DarkAccentBrightness = 0.85f;
    private const float DarkImageBrightness = 0.8f;
    private const float ClearImageAlpha = 0.75f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Black = new(0f, 0f, 0f, 1f);
    private static readonly Vector4 LightSecondary = new(60f / 255f, 60f / 255f, 67f / 255f, 0.6f);
    private static readonly Vector4 LightTertiary = new(60f / 255f, 60f / 255f, 67f / 255f, 0.3f);
    private static readonly Vector4 LightSeparator = new(60f / 255f, 60f / 255f, 67f / 255f, 0.18f);
    private static readonly Vector4 LightFill = new(120f / 255f, 120f / 255f, 128f / 255f, 0.16f);
    private static readonly Vector4 DarkSecondary = new(235f / 255f, 235f / 255f, 245f / 255f, 0.6f);
    private static readonly Vector4 DarkTertiary = new(235f / 255f, 235f / 255f, 245f / 255f, 0.3f);
    private static readonly Vector4 DarkSeparator = new(1f, 1f, 1f, 0.12f);
    private static readonly Vector4 DarkFill = new(120f / 255f, 120f / 255f, 128f / 255f, 0.32f);

    public readonly WidgetMode Mode;
    public readonly bool Light;
    public readonly float Opacity;
    public readonly Vector4 Tint;
    public readonly Vector4 Primary;
    public readonly Vector4 Secondary;
    public readonly Vector4 Tertiary;
    public readonly Vector4 Separator;
    public readonly Vector4 Fill;
    public readonly Vector4 ImageTint;
    public readonly Vector4 OnAccent;

    private WidgetInk(WidgetMode mode, bool light, float opacity, Vector4 tint)
    {
        Mode = mode;
        Light = light;
        Opacity = opacity;
        Tint = tint with { W = 1f };
        switch (mode)
        {
            case WidgetMode.Tinted:
                Primary = Faded(Palette.Mix(Tint, White, 0.72f), opacity);
                Secondary = Faded(Palette.Mix(Tint, White, 0.45f) with { W = 0.8f }, opacity);
                Tertiary = Faded(Palette.Mix(Tint, White, 0.45f) with { W = 0.45f }, opacity);
                Separator = Faded(Tint with { W = 0.22f }, opacity);
                Fill = Faded(Tint with { W = 0.2f }, opacity);
                ImageTint = Faded(Palette.Mix(Tint, White, 0.45f), opacity);
                OnAccent = Faded(Palette.Mix(Tint, Black, 0.7f), opacity);
                return;
            case WidgetMode.Clear:
                Primary = Faded(White, opacity);
                Secondary = Faded(White with { W = 0.62f }, opacity);
                Tertiary = Faded(White with { W = 0.38f }, opacity);
                Separator = Faded(White with { W = 0.2f }, opacity);
                Fill = Faded(White with { W = 0.2f }, opacity);
                ImageTint = Faded(White with { W = ClearImageAlpha }, opacity);
                OnAccent = Faded(Black with { W = 0.85f }, opacity);
                return;
            case WidgetMode.Dark:
                Primary = Faded(White, opacity);
                Secondary = Faded(DarkSecondary, opacity);
                Tertiary = Faded(DarkTertiary, opacity);
                Separator = Faded(DarkSeparator, opacity);
                Fill = Faded(DarkFill, opacity);
                ImageTint = Faded(new Vector4(DarkImageBrightness, DarkImageBrightness, DarkImageBrightness, 1f),
                    opacity);
                OnAccent = Faded(White, opacity);
                return;
        }

        Primary = Faded(light ? Black : White, opacity);
        Secondary = Faded(light ? LightSecondary : DarkSecondary, opacity);
        Tertiary = Faded(light ? LightTertiary : DarkTertiary, opacity);
        Separator = Faded(light ? LightSeparator : DarkSeparator, opacity);
        Fill = Faded(light ? LightFill : DarkFill, opacity);
        ImageTint = Faded(White, opacity);
        OnAccent = Faded(White, opacity);
    }

    public static WidgetInk From(in WidgetContext context) =>
        new(context.Mode, IsLightTheme(context.Theme), context.Opacity, context.Tint);

    public static WidgetInk OnImage(in WidgetContext context) =>
        new(context.Mode, false, context.Opacity, context.Tint);

    public static bool IsLightTheme(PhoneTheme theme) => Palette.Luminance(theme.AppBackground) >= 0.5f;

    public bool KeepsOwnColors => Mode == WidgetMode.FullColor;

    public Vector4 Accent(Vector4 color)
    {
        switch (Mode)
        {
            case WidgetMode.Tinted:
                var luminance = Math.Clamp(Palette.Luminance(color), 0f, 1f);
                var mapped = Palette.Mix(Palette.Mix(Tint, Black, 0.25f), Palette.Mix(Tint, White, 0.6f), luminance);
                return Faded(mapped with { W = color.W }, Opacity);
            case WidgetMode.Clear:
                return Faded(White with { W = color.W }, Opacity);
            case WidgetMode.Dark:
                return Faded(new Vector4(color.X * DarkAccentBrightness, color.Y * DarkAccentBrightness,
                    color.Z * DarkAccentBrightness, color.W), Opacity);
            default:
                return Faded(color, Opacity);
        }
    }

    public Vector4 Own(Vector4 ownColor, Vector4 replacement) =>
        KeepsOwnColors ? Faded(ownColor, Opacity) : replacement;

    public Vector4 Fade(Vector4 color) => Faded(color, Opacity);

    public Vector4 Fade(Vector4 color, float alpha) => color with { W = color.W * alpha * Opacity };

    private static Vector4 Faded(Vector4 color, float opacity) => color with { W = color.W * opacity };
}
