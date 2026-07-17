using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Shell.Home;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Core.Shell;

internal sealed class ShellTransitionRenderer
{
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

    public void ResetPrepared() => zoomPreparedFor = null;

    public void Draw(Rect screen, PhoneTheme theme)
    {
        var cover = navigation.MotionProgress;
        var height = screen.Height;
        var over = navigation.MotionOver;
        var under = navigation.MotionUnder;
        if (under is null && !over.WantsTransparentScreen)
        {
            DrawZoomTransition(screen, theme, over);
            return;
        }

        var overOffset = new Vector2(0f, (1f - cover) * height);
        var underDim = cover * TransitionTiming.ShellDimMax;
        LayerPainter underPaint = under is null
            ? target => painter.PaintHome(target, theme, new HomeMotion(1f, default, 0f, false))
            : target => painter.PaintApp(target, theme, under);
        LayerPainter overPaint = target => painter.PaintApp(target, theme, over);
        if (over.WantsTransparentScreen || (under?.WantsTransparentScreen ?? false))
        {
            var band = new Rect(screen.Min, new Vector2(screen.Max.X, screen.Min.Y + overOffset.Y));
            SceneCompositor.DrawClipped(band, screen, underDim, underPaint);
            SceneCompositor.DrawLayer(screen,
                new SceneCompositor.Layer(over.Id, overOffset, 0f, overPaint, default, true));
            return;
        }

        var underLayer =
            new SceneCompositor.Layer(under?.Id ?? "home", Vector2.Zero, underDim, underPaint, default, true);
        var overLayer = new SceneCompositor.Layer(over.Id, overOffset, 0f, overPaint, default, true);
        SceneCompositor.Composite(screen, underLayer, overLayer);
    }

    private void DrawZoomTransition(Rect screen, PhoneTheme theme, IPhoneApp over)
    {
        var raw = Math.Clamp(navigation.MotionProgress, 0f, 1f);
        var content = ShellScreenPainter.ContentRect(screen, theme);
        if (navigation.Motion == ShellMotion.Present && navigation.MotionOrigin is null &&
            !string.Equals(zoomPreparedFor, over.Id, StringComparison.Ordinal))
        {
            zoomPreparedFor = over.Id;
            home.PrepareReveal(over.Id);
        }

        var recede = Easing.SmoothStep(raw);
        var rest = navigation.MotionOrigin ?? home.RevealRect(over.Id, content) ?? CenterOrigin(content);
        var motion = new HomeMotion(1f + TransitionTiming.HomeZoomDepth * recede, rest.Center, recede, false);
        SceneCompositor.DrawLayer(screen, new SceneCompositor.Layer("home", Vector2.Zero,
            TransitionTiming.HomeRecedeDim * recede, target => painter.PaintHome(target, theme, motion), default,
            true));
        var warped = motion.Warp(rest);
        var card = new Rect(Vector2.Lerp(warped.Min, screen.Min, raw), Vector2.Lerp(warped.Max, screen.Max, raw));
        if (card.Width < 4f || card.Height < 4f)
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale;
        var iconRadius = MathF.Min(MathF.Min(rest.Width, rest.Height) * 0.26f, 24f * scale);
        var rounding = iconRadius + (theme.ScreenRounding * scale - iconRadius) * raw;
        var shellDrawList = ImGui.GetWindowDrawList();
        var elevation = Easing.Clamp01(raw * 2.4f);
        Elevation.Floating(shellDrawList, card.Min, card.Max, rounding, scale, elevation);
        DrawZoomCard(screen, theme, over, rest, card, rounding, raw);
        Material.EdgeSquircle(ImGui.GetWindowDrawList(), card.Min, card.Max, rounding, scale, elevation);
    }

    private void DrawZoomCard(Rect screen, PhoneTheme theme, IPhoneApp over, Rect rest, Rect card, float rounding,
        float raw)
    {
        const ImGuiWindowFlags cardFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse |
                                           ImGuiWindowFlags.NoBackground;
        var appReady = navigation.Motion == ShellMotion.Present && raw >= TransitionTiming.AppInteractiveProgress;
        ImGui.SetCursorScreenPos(card.Min);
        using (ImRaii.PushId("zoomcard"))
        using (ImRaii.Child("card", card.Size, false, cardFlags))
        using (InputShield.Engage(!appReady))
        {
            var reveal = Easing.SmootherStep(Easing.Segment(raw, 0.45f, 0.85f));
            if (reveal > 0.001f)
            {
                var offset = card.Center - screen.Center;
                var rise = (1f - reveal) * 8f * ImGuiHelpers.GlobalScale;
                var target = new Rect(screen.Min + offset + new Vector2(0f, rise),
                    screen.Max + offset + new Vector2(0f, rise));
                using (ImRaii.PushId(over.Id))
                {
                    painter.PaintApp(target, theme, over);
                }
            }

            var cardDrawList = ImGui.GetWindowDrawList();
            var veilAlpha = 1f - reveal;
            if (veilAlpha > 0.001f)
            {
                var surface = IconTile.Surface(over.Accent);
                var background = themes.Current.AppBackground;
                var settle = Easing.SmootherStep(Easing.Segment(raw, 0f, 0.4f));
                var veil = Vector4.Lerp(surface, background, settle);
                Squircle.Fill(cardDrawList, card.Min, card.Max, rounding,
                    ImGui.GetColorU32(veil with { W = veil.W * veilAlpha }));
            }

            var glyphAlpha = 1f - Easing.Segment(raw, 0.12f, 0.5f);
            if (glyphAlpha > 0.001f)
            {
                DrawZoomGlyph(cardDrawList, over, card, rest, raw, Easing.SmootherStep(glyphAlpha));
            }
        }
    }

    private static void DrawZoomGlyph(ImDrawListPtr drawList, IPhoneApp over, Rect card, Rect rest, float raw,
        float alpha)
    {
        var size = rest.Width * (1f + 0.4f * raw);
        var center = card.Center;
        var surface = IconTile.Surface(over.Accent);
        var ink = new Vector4(1f, 1f, 1f, alpha);
        if (!AppIconArt.TryDraw(drawList, over.Id, center, size, ink,
                Palette.WithAlpha(Palette.Darken(surface, 0.25f), alpha)))
        {
            var glyphHeight = Typography.Measure(over.Glyph).Y;
            var glyphScale = glyphHeight > 0f ? size * 0.5f / glyphHeight : 1f;
            Typography.DrawCentered(drawList, center, over.Glyph, ink, glyphScale, FontWeight.Regular);
        }
    }

    private static Rect CenterOrigin(Rect content)
    {
        var half = 30f * ImGuiHelpers.GlobalScale;
        return new Rect(content.Center - new Vector2(half, half), content.Center + new Vector2(half, half));
    }
}
