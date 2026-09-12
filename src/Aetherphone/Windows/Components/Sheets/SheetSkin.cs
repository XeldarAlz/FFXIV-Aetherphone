using Aetherphone.Core;
using Aetherphone.Core.Theme;

namespace Aetherphone.Windows.Components;

internal readonly record struct SheetSkin(Vector4 Panel, Vector4 Stroke, Vector4 Grabber, Vector4 Title)
{
    private const float ThemeStrokeAlpha = 0.08f;
    private const float GrabberAlpha = 0.35f;

    public static SheetSkin From(PhoneTheme theme) => new(theme.Surface,
        Palette.WithAlpha(theme.TextStrong, ThemeStrokeAlpha), Palette.WithAlpha(theme.TextMuted, GrabberAlpha),
        theme.TextStrong);

    public static SheetSkin From(in AppPalette palette, SocialInk ink) => new(palette.CardFill, ink.GlassStroke,
        Palette.WithAlpha(ink.MutedInk, GrabberAlpha), ink.TitleInk);
}
