using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Theme;

namespace Aetherphone.Windows.Components;

internal readonly record struct NavBarButton(string Glyph, string Tooltip);

internal readonly record struct NavBarStyle(Vector4 Ink, Vector4 Accent, Vector4 Background)
{
    public static NavBarStyle From(PhoneTheme theme) => new(theme.TextStrong, theme.Accent, theme.AppBackground);

    public static NavBarStyle From(AppSkin ui) => new(ui.TitleInk, ui.Theme.Accent, ui.Palette.BackdropTop);
}

internal readonly struct NavBarFrame
{
    public readonly Rect Content;
    public readonly Rect Body;
    public readonly float Scale;
    public readonly float TitleBandTop;

    internal NavBarFrame(Rect content, Rect body, float scale, float titleBandTop)
    {
        Content = content;
        Body = body;
        Scale = scale;
        TitleBandTop = titleBandTop;
    }
}

internal static class NavBarMetrics
{
    public const float ExpandedHeight = 82f;
    public const float InlineHeight = 44f;
    public const float CollapseDistance = 60f;
    public const float GlassFadeDistance = 20f;
    public const float EdgeFadeHeight = 14f;
    public const float TitleGap = 10f;
    public const float BarInsetY = 2f;
    public const float ChevronSize = 10f;
    public const float BackHitHeight = 44f;
    public const float ButtonGap = 8f;
    public const float ButtonPad = 6f;
    public const int MaxButtons = 2;
    private const float MinimumScale = 0.0001f;

    public static float BandHeight => ExpandedHeight - InlineHeight;

    public static float Progress(float scrollY, float scale) =>
        Easing.Clamp01(scrollY / (CollapseDistance * MathF.Max(scale, MinimumScale)));

    public static float GlassOpacity(float scrollY, float scale) =>
        Easing.Clamp01(scrollY / (GlassFadeDistance * MathF.Max(scale, MinimumScale)));

    public static float LargeTitleAlpha(float progress) => 1f - Easing.Clamp01(progress);

    public static float InlineTitleAlpha(float progress) => Easing.Clamp01(progress);

    public static float ButtonsWidth(int count, float scale) =>
        count <= 0 ? 0f : count * Metrics.Size.GlassButton * scale + (count - 1) * ButtonGap * scale;

    public static float ButtonCenterX(float contentRight, int index, int count, float scale)
    {
        var diameter = Metrics.Size.GlassButton * scale;
        var pitch = diameter + ButtonGap * scale;
        var last = contentRight - (Metrics.Space.GlassInset + ButtonPad) * scale - diameter * 0.5f;
        return last - (count - 1 - index) * pitch;
    }
}
