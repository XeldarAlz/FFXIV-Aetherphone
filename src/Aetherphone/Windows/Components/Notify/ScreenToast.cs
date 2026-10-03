using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal readonly record struct ScreenToastStyle(Vector4 Panel, Vector4 Stroke, Vector4 Ink)
{
    private const float LightInkLuminance = 0.5f;

    public GlassTone Tone => Palette.Luminance(Ink) >= LightInkLuminance ? GlassTone.Dark : GlassTone.Light;

    public static ScreenToastStyle From(PhoneTheme theme) => new(
        Palette.WithAlpha(Palette.Lighten(theme.AppBackground, 0.10f), 0.92f),
        Palette.WithAlpha(theme.TextStrong, 0.12f),
        theme.TextStrong);

    public static ScreenToastStyle From(AppSkin ui) => new(
        Palette.WithAlpha(Palette.Lighten(ui.Palette.BackdropTop, 0.10f), 0.92f),
        Palette.WithAlpha(ui.TitleInk, 0.12f),
        ui.TitleInk);
}

internal sealed class ScreenToast
{
    private const float LifetimeSeconds = 1.7f;
    private const float FadeSeconds = 0.22f;
    private const float BottomOffset = 96f;
    private const float RiseDistance = 14f;
    private const float MaxFrameSeconds = 0.1f;
    private const float CapsuleHeight = 44f;
    private const float PadX = 18f;
    private const float SideInset = 24f;
    private const float GrowFrom = 0.9f;

    private static readonly TextStyle LabelStyle = TextStyles.SubheadlineEmphasized;

    private string label = string.Empty;
    private float elapsed = LifetimeSeconds;
    private Spring popSpring;

    public void Show(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        label = text;
        elapsed = 0f;
        popSpring.SnapTo(0f);
    }

    public void Draw(Rect screen, in ScreenToastStyle style)
    {
        if (elapsed >= LifetimeSeconds)
        {
            return;
        }

        var delta = MathF.Min(ImGui.GetIO().DeltaTime, MaxFrameSeconds);
        elapsed += delta;
        var pop = Math.Clamp(popSpring.Step(1f, Motion.Appear, delta), 0f, 1f);
        var fadeStart = LifetimeSeconds - FadeSeconds;
        var fade = elapsed > fadeStart ? 1f - Math.Clamp((elapsed - fadeStart) / FadeSeconds, 0f, 1f) : 1f;
        var alpha = pop * fade;
        if (alpha <= 0.001f)
        {
            return;
        }

        var scale = UiScale.Current;
        var grow = GrowFrom + (1f - GrowFrom) * pop;
        var padX = PadX * scale;
        var maxWidth = MathF.Max(1f, screen.Width - (SideInset * 2f + PadX * 2f) * scale);
        var fitted = Typography.FitText(label, maxWidth, LabelStyle);
        var textSize = Typography.Measure(fitted, LabelStyle);
        var width = (textSize.X + padX * 2f) * grow;
        var height = CapsuleHeight * scale * grow;
        var rise = (1f - pop) * RiseDistance * scale;
        var center = new Vector2(screen.Center.X, screen.Max.Y - BottomOffset * scale + rise);
        var min = new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f);
        var max = new Vector2(center.X + width * 0.5f, center.Y + height * 0.5f);
        var drawList = ImGui.GetForegroundDrawList();
        drawList.PushClipRect(screen.Min, screen.Max, false);
        Material.LiquidGlass(drawList, min, max, height * 0.5f, scale, style.Tone, 0f, alpha);
        Typography.DrawCentered(drawList, center, fitted, Palette.WithAlpha(style.Ink, style.Ink.W * alpha),
            LabelStyle);
        drawList.PopClipRect();
    }
}
