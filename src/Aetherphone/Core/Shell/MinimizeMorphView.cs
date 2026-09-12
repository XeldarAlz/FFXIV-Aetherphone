using Aetherphone.Core.Animation;
using Aetherphone.Core.Shell.Home;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell;

internal sealed class MinimizeMorphView
{
    private const float FaceFadeStart = 0.55f;
    private const float FaceFadeEnd = 0.95f;
    private const float RailFadeEnd = 0.55f;
    private const string VeilLayerId = "morphveil";

    private readonly ThemeProvider themes;
    private readonly MinimizeTransition minimize;
    private readonly MinimizedPhone minimizedPhone;
    private readonly ShellScreenPainter painter;
    private readonly Configuration configuration;

    public MinimizeMorphView(ThemeProvider themes, MinimizeTransition minimize, MinimizedPhone minimizedPhone,
        ShellScreenPainter painter, Configuration configuration)
    {
        this.themes = themes;
        this.minimize = minimize;
        this.minimizedPhone = minimizedPhone;
        this.painter = painter;
        this.configuration = configuration;
    }

    public void Draw(Rect device, float delta)
    {
        if (minimize.MorphActive)
        {
            DrawMorph(device, delta);
            return;
        }

        if (minimizedPhone.Draw(device, themes.Chrome, delta))
        {
            minimize.BeginExpand();
        }
    }

    private void DrawMorph(Rect device, float delta)
    {
        var scale = UiScale.Current;
        var theme = themes.Chrome;
        var startBody = DeviceChrome.BodyRect(device, theme);
        var endBody = MinimizedRect(device);
        var eased = minimize.EasedProgress;
        var body = new Rect(Vector2.Lerp(startBody.Min, endBody.Min, eased),
            Vector2.Lerp(startBody.Max, endBody.Max, eased));
        var geometry = ChassisGeometry.Morph(body, theme, scale, eased);

        var shell = ImGui.GetWindowDrawList();
        DeviceChrome.DrawShell(shell, geometry, scale, theme, 1f, true);
        DrawRailButtons(shell, geometry, theme, scale, eased);
        RevealMorphContent(DeviceChrome.Chassis(device, theme), theme, geometry, eased, device.IsLandscape());

        var faceAlpha = Easing.SmoothStep(Easing.Segment(eased, FaceFadeStart, FaceFadeEnd));
        minimizedPhone.DrawFace(ImGui.GetForegroundDrawList(), geometry, theme, delta, false, faceAlpha);
    }

    private void DrawRailButtons(ImDrawListPtr dl, in ChassisGeometry geometry, PhoneTheme theme, float scale,
        float eased)
    {
        var rail = theme.RailWidth * scale * (1f - Easing.Segment(eased, 0f, RailFadeEnd));
        if (rail < 0.5f || geometry.Body.IsLandscape())
        {
            return;
        }

        var body = geometry.Body;
        var window = new Rect(new Vector2(body.Min.X - rail, body.Min.Y), new Vector2(body.Max.X + rail, body.Max.Y));
        var sideButton = DeviceChrome.SideButtonRect(window, geometry, out var sideButtonSide);
        HardwareButton.Draw(dl, sideButton, theme, sideButtonSide, false, 0f, 0f);
        var muteButton = DeviceChrome.MuteButtonRect(window, geometry, out var muteSide);
        HardwareButton.Draw(dl, muteButton, theme, muteSide, false, 0f, configuration.DoNotDisturb ? 1f : 0f);
        var lockButton = DeviceChrome.LockButtonRect(window, geometry, out var lockSide);
        HardwareButton.Draw(dl, lockButton, theme, lockSide, false, 0f, configuration.LockPosition ? 1f : 0f);
    }

    private void RevealMorphContent(in ChassisGeometry device, PhoneTheme theme, in ChassisGeometry geometry,
        float eased, bool landscape)
    {
        var screen = geometry.Screen;
        if (screen.Height <= 0.5f || screen.Width <= 0.5f)
        {
            return;
        }

        var fullScreen = device.Screen;
        var transform = LayerTransform.Cover(fullScreen, screen, screen);
        painter.PaintCurrentTransformed(fullScreen, device.ScreenRadius, theme, HomeMotion.Still, landscape,
            in transform);
        using (ScreenLayer.BeginPassive(VeilLayerId, screen))
        {
            var drawList = ImGui.GetWindowDrawList();
            Squircle.Fill(drawList, screen.Min, screen.Max, geometry.ScreenRadius,
                ImGui.GetColorU32(Palette.WithAlpha(theme.ScreenBase, eased)));
            DeviceChrome.MaskScreenCorners(drawList, geometry, theme, UiScale.Current);
        }
    }

    private Rect MinimizedRect(Rect device) => new(device.Min, device.Min + minimizedPhone.Measure());
}
