using System.Numerics;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Shell.Home;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Core.Shell;

internal sealed class MinimizeMorphView
{
    private readonly ThemeProvider themes;
    private readonly MinimizeTransition minimize;
    private readonly MinimizedPhone minimizedView;
    private readonly NotificationService notifications;
    private readonly ShellScreenPainter painter;

    public MinimizeMorphView(ThemeProvider themes, MinimizeTransition minimize, MinimizedPhone minimizedView,
        NotificationService notifications, ShellScreenPainter painter)
    {
        this.themes = themes;
        this.minimize = minimize;
        this.minimizedView = minimizedView;
        this.notifications = notifications;
        this.painter = painter;
    }

    public bool Draw(Rect device, float delta)
    {
        if (minimize.MorphActive)
        {
            DrawMorph(device);
            return false;
        }

        return DrawFace(device, delta);
    }

    private void DrawMorph(Rect device)
    {
        minimizedView.IsShowing = false;
        var scale = ImGuiHelpers.GlobalScale;
        var theme = themes.Chrome;
        var startBody = DeviceChrome.BodyRect(device);
        var endBody = MinimizedRect(device, scale).Inset(scale);
        var eased = minimize.EasedProgress;
        var body = new Rect(Vector2.Lerp(startBody.Min, endBody.Min, eased),
            Vector2.Lerp(startBody.Max, endBody.Max, eased));
        var bezel = Easing.Lerp(theme.BezelThickness * scale, endBody.Width * 0.09f, eased);
        var rounding = Easing.Lerp(theme.DeviceRounding * scale, endBody.Width * 0.30f, eased);
        var geometry = MinimizedPhone.Geometry.Lerp(body, bezel, rounding);

        var shell = ImGui.GetWindowDrawList();
        Elevation.Floating(shell, geometry.Body.Min, geometry.Body.Max, geometry.Rounding, scale, eased);
        MinimizedPhone.DrawShell(shell, geometry, theme);
        RevealMorphContent(device, theme, geometry, eased);

        var raw = Math.Clamp((eased - 0.5f) / 0.4f, 0f, 1f);
        var glyphAlpha = raw * raw * (3f - 2f * raw);
        MinimizedPhone.DrawFace(ImGui.GetForegroundDrawList(), geometry, theme, scale, glyphAlpha,
            notifications.UnreadCount);
    }

    private void RevealMorphContent(Rect device, PhoneTheme theme, in MinimizedPhone.Geometry geometry, float eased)
    {
        var screen = geometry.Screen;
        if (screen.Height <= 0.5f)
        {
            return;
        }

        var fullScreen = DeviceChrome.ScreenRect(device, theme);
        var rounding = geometry.ScreenRounding;
        var veil = ImGui.GetColorU32(Palette.WithAlpha(theme.ScreenBase, eased));
        var shrink = ShrinkMotion(fullScreen, screen);
        SceneCompositor.DrawClipped(screen, fullScreen, 0f, target =>
        {
            painter.PaintCurrent(target, theme, shrink);
            Squircle.Fill(ImGui.GetWindowDrawList(), screen.Min, screen.Max, rounding, veil);
        });
    }

    private static HomeMotion ShrinkMotion(Rect fullScreen, Rect target)
    {
        var zoom = fullScreen.Width > 0f ? target.Width / fullScreen.Width : 1f;
        if (zoom >= 0.999f)
        {
            return new HomeMotion(1f, default, 0f, false);
        }

        var pivot = (target.Min - fullScreen.Min * zoom) / (1f - zoom);
        return new HomeMotion(zoom, pivot, 0f, false);
    }

    private bool DrawFace(Rect device, float delta)
    {
        minimizedView.IsShowing = true;
        var mini = MinimizedRect(device, ImGuiHelpers.GlobalScale);
        switch (minimizedView.Draw(mini, themes.Chrome, delta))
        {
            case MinimizedAction.Expand:
                minimize.BeginExpand();
                break;
            case MinimizedAction.Close:
                return true;
        }

        return false;
    }

    private static Rect MinimizedRect(Rect device, float scale) =>
        new(device.Min, device.Min + MinimizeTransition.MinimizedSize * scale);
}
