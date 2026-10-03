using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Shell.Home;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell;

internal sealed class ShellTransitionRenderer
{
    private const string ShadowLayerId = "cardshadow";
    private const string CardLayerId = "cardtop";
    private const float MinimumCardSize = 2f;
    private const float IconRadiusFactor = 0.26f;
    private const float IconRadiusCapUnits = 24f;
    private const float ShadowRise = 2.4f;
    private const float SurfaceFadeStart = 0.12f;
    private const float SurfaceFadeEnd = 0.5f;
    private const float VeilSettleEnd = 0.5f;
    private const float VeilFadeStart = 0.45f;
    private const float VeilFadeEnd = 0.78f;
    private const float GlyphFadeStart = 0.06f;
    private const float GlyphFadeEnd = 0.4f;
    private const float EdgeFadeStart = 0.85f;
    private const float CenterOriginHalfUnits = 30f;

    private readonly ThemeProvider themes;
    private readonly NavigationStack navigation;
    private readonly HomeScreen home;
    private readonly ShellScreenPainter painter;
    private string? zoomPreparedFor;

    public ShellTransitionRenderer(ThemeProvider themes, NavigationStack navigation, HomeScreen home,
        ShellScreenPainter painter)
    {
        this.themes = themes;
        this.navigation = navigation;
        this.home = home;
        this.painter = painter;
    }

    public float StatusBarAlpha { get; private set; } = 1f;

    public void ResetPrepared() => zoomPreparedFor = null;

    public void Draw(Rect screen, float screenRadius, PhoneTheme theme)
    {
        var over = navigation.MotionOver;
        var under = navigation.MotionUnder;
        if (under is null && !over.WantsTransparentScreen)
        {
            DrawZoom(screen, screenRadius, theme, over);
            return;
        }

        StatusBarAlpha = 1f;
        DrawSlide(screen, screenRadius, theme, over, under);
    }

    private void DrawSlide(Rect screen, float screenRadius, PhoneTheme theme, IPhoneApp over, IPhoneApp? under)
    {
        var cover = Easing.Clamp01(navigation.MotionProgress);
        var overOffset = new Vector2(0f, MathF.Round((1f - cover) * screen.Height));
        var transparent = over.WantsTransparentScreen || (under?.WantsTransparentScreen ?? false);
        var underClip = transparent
            ? new Rect(screen.Min, new Vector2(screen.Max.X, screen.Min.Y + overOffset.Y))
            : screen;
        using (var underLayer = ScreenLayer.Begin(under?.Id ?? ShellScreenPainter.HomeLayerId, screen, true))
        {
            if (under is null)
            {
                painter.PaintHome(screen, screenRadius, theme, HomeMotion.Still);
            }
            else
            {
                painter.PaintApp(screen, screenRadius, theme, under);
            }

            underLayer.Veil(ImGui.GetColorU32(new Vector4(0f, 0f, 0f, cover * TransitionTiming.ShellDimMax)));
            underLayer.Transform(LayerTransform.Identity(underClip));
        }

        var overTransform = LayerTransform.Translate(overOffset, screen);
        using var overLayer = OpenScreen(over.Id, screen, in overTransform);
        painter.PaintApp(screen, screenRadius, theme, over);
        overLayer.Transform(in overTransform);
    }

    private ScreenLayer OpenScreen(string id, Rect screen, in LayerTransform transform)
    {
        var interactive = navigation.Motion == ShellMotion.Present &&
                          navigation.MotionProgress >= TransitionTiming.InteractiveProgress;
        return interactive ? ScreenLayer.BeginWarped(id, screen, in transform) : ScreenLayer.Begin(id, screen, true);
    }

    private void DrawZoom(Rect screen, float screenRadius, PhoneTheme theme, IPhoneApp over)
    {
        var raw = Easing.Clamp01(navigation.MotionProgress);
        var content = ShellScreenPainter.ContentRect(screen, theme);
        PrepareRevealOnce(over);
        var kind = navigation.MotionOriginKind;
        var rest = navigation.MotionOrigin ?? RestRect(over, content, out kind);
        var zoom = 1f + TransitionTiming.HomeZoomDepth * raw;
        var homeTransform = LayerTransform.ScaleAbout(rest.Center, zoom, screen);
        var homeMotion = HomeMotion.Recede(raw, kind == LaunchOrigin.Icon ? over.Id : null, zoom, rest.Center);
        StatusBarAlpha = MathF.Max(1f - homeMotion.Recession, Easing.Segment(raw, VeilFadeEnd, 1f));
        using (var homeLayer = ScreenLayer.Begin(ShellScreenPainter.HomeLayerId, screen, true))
        {
            painter.PaintHome(screen, screenRadius, theme, homeMotion);
            homeLayer.Veil(ImGui.GetColorU32(new Vector4(0f, 0f, 0f, TransitionTiming.HomeRecedeDim * raw)));
            homeLayer.Transform(in homeTransform);
        }

        var warped = homeTransform.Map(rest);
        var card = KeepInside(CardRect(warped, screen, raw).Translate(navigation.MotionDrift * raw), screen);
        var scale = UiScale.Current;
        var iconRadius = MathF.Min(MathF.Min(rest.Width, rest.Height) * IconRadiusFactor, IconRadiusCapUnits * scale);
        var rounding = iconRadius + (screenRadius - iconRadius) * raw;
        var surfaceFade = kind == LaunchOrigin.Surface
            ? Easing.Segment(raw, SurfaceFadeStart, SurfaceFadeEnd)
            : 1f;
        var elevation = Easing.Clamp01(raw * ShadowRise) * surfaceFade;
        using (ScreenLayer.BeginPassive(ShadowLayerId, screen))
        {
            var shadowDrawList = ImGui.GetWindowDrawList();
            if (kind == LaunchOrigin.Icon)
            {
                Elevation.IconRest(shadowDrawList, card.Min, card.Max, rounding, scale, 1f - elevation);
            }

            Elevation.Squircle(shadowDrawList, card.Min, card.Max, rounding, scale, elevation);
        }

        var appTransform = LayerTransform.Fit(screen, card, card, surfaceFade);
        using (var appLayer = OpenScreen(over.Id, screen, in appTransform))
        {
            painter.PaintApp(screen, screenRadius, theme, over);
            appLayer.Transform(in appTransform);
        }

        using (ScreenLayer.BeginPassive(CardLayerId, screen))
        {
            var cardDrawList = ImGui.GetWindowDrawList();
            if (kind == LaunchOrigin.Icon)
            {
                DrawIconVeil(cardDrawList, over, card, rounding, raw);
            }

            Material.EdgeSquircle(cardDrawList, card.Min, card.Max, rounding, scale,
                elevation * (1f - Easing.Segment(raw, EdgeFadeStart, 1f)));
        }
    }

    private void PrepareRevealOnce(IPhoneApp over)
    {
        if (navigation.Motion != ShellMotion.Present || navigation.MotionOrigin is not null ||
            string.Equals(zoomPreparedFor, over.Id, StringComparison.Ordinal))
        {
            return;
        }

        zoomPreparedFor = over.Id;
        home.PrepareReveal(over.Id);
    }

    private Rect RestRect(IPhoneApp over, Rect content, out LaunchOrigin kind)
    {
        if (home.RevealRect(over.Id, content, out kind) is { } rect)
        {
            return rect;
        }

        kind = LaunchOrigin.Icon;
        return CenterOrigin(content);
    }

    private static Rect CardRect(Rect warped, Rect screen, float raw)
    {
        var min = Vector2.Lerp(warped.Min, screen.Min, raw);
        var max = Vector2.Lerp(warped.Max, screen.Max, raw);
        if (max.X - min.X < MinimumCardSize)
        {
            max.X = min.X + MinimumCardSize;
        }

        if (max.Y - min.Y < MinimumCardSize)
        {
            max.Y = min.Y + MinimumCardSize;
        }

        return new Rect(min, max);
    }

    private static Rect KeepInside(Rect card, Rect screen)
    {
        var shift = Vector2.Zero;
        if (card.Min.X < screen.Min.X)
        {
            shift.X = screen.Min.X - card.Min.X;
        }
        else if (card.Max.X > screen.Max.X)
        {
            shift.X = screen.Max.X - card.Max.X;
        }

        if (card.Min.Y < screen.Min.Y)
        {
            shift.Y = screen.Min.Y - card.Min.Y;
        }
        else if (card.Max.Y > screen.Max.Y)
        {
            shift.Y = screen.Max.Y - card.Max.Y;
        }

        return shift == Vector2.Zero ? card : card.Translate(shift);
    }

    private void DrawIconVeil(ImDrawListPtr drawList, IPhoneApp over, Rect card, float rounding, float raw)
    {
        var veilAlpha = 1f - Easing.Segment(raw, VeilFadeStart, VeilFadeEnd);
        if (veilAlpha > 0.001f)
        {
            var surface = IconTile.Surface(over.Accent);
            var background = themes.ForApp(over.WantsSystemTheme).AppBackground;
            var settle = Easing.Segment(raw, 0f, VeilSettleEnd);
            IconTile.FillShaded(drawList, card.Min, card.Max, rounding, surface, veilAlpha * (1f - settle));
            Squircle.Fill(drawList, card.Min, card.Max, rounding,
                ImGui.GetColorU32(background with { W = background.W * veilAlpha * settle }));
        }

        var glyphAlpha = 1f - Easing.Segment(raw, GlyphFadeStart, GlyphFadeEnd);
        if (glyphAlpha > 0.001f)
        {
            DrawZoomGlyph(drawList, over, card, rounding, glyphAlpha);
        }
    }

    private static void DrawZoomGlyph(ImDrawListPtr drawList, IPhoneApp over, Rect card, float rounding, float alpha)
    {
        if (AppIconTile.TryDraw(drawList, over.Id, over.Accent, card.Min, card.Max, rounding, alpha, false))
        {
            return;
        }

        var size = card.Width;
        var center = card.Center;
        var surface = IconTile.Surface(over.Accent);
        var ink = AppAccents.InkFor(over.Id) with { W = alpha };
        if (!AppIconArt.TryDraw(drawList, over.Id, center, size, ink,
                Palette.WithAlpha(Palette.Mix(surface, ink, 0.28f), alpha)))
        {
            var glyphHeight = Typography.Measure(over.Glyph).Y;
            var glyphScale = glyphHeight > 0f ? size * 0.5f / glyphHeight : 1f;
            Typography.DrawCentered(drawList, center, over.Glyph, ink, glyphScale, FontWeight.Regular);
        }
    }

    private static Rect CenterOrigin(Rect content)
    {
        var half = CenterOriginHalfUnits * UiScale.Current;
        return new Rect(content.Center - new Vector2(half, half), content.Center + new Vector2(half, half));
    }
}
