using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Shell.Home;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Core.Shell;

internal sealed class ShellScreenPainter
{
    public const string HomeLayerId = "home";
    private const float LaunchIconUnits = 72f;

    private readonly ThemeProvider themes;
    private readonly NavigationStack navigation;
    private readonly HomeScreen home;

    public ShellScreenPainter(ThemeProvider themes, NavigationStack navigation, HomeScreen home)
    {
        this.themes = themes;
        this.navigation = navigation;
        this.home = home;
    }

    public PhoneTheme SurfaceTheme(PhoneTheme wallpaperTheme) =>
        navigation.Current is { } app ? themes.ForApp(app.WantsSystemTheme) : wallpaperTheme;

    public void PaintCurrent(Rect screen, float screenRadius, PhoneTheme theme, in HomeMotion motion)
    {
        using var stage = ScreenLayer.Begin(CurrentLayerId, screen, false);
        PaintCurrentInside(screen, screenRadius, theme, motion);
    }

    public void PaintCurrentTransformed(Rect screen, float screenRadius, PhoneTheme theme, in HomeMotion motion,
        bool landscape, in LayerTransform transform)
    {
        using var stage = ScreenLayer.Begin(CurrentLayerId, screen, true);
        PaintCurrentInside(screen, screenRadius, theme, motion);
        using (ScreenLayer.BeginPassive("chrome", screen))
        {
            var ink = SurfaceTheme(theme);
            StatusBar.Draw(screen, ink, landscape);
            HomeIndicator.Draw(ImGui.GetWindowDrawList(), HomeIndicator.Bounds(screen, UiScale.Current), ink,
                false);
        }

        stage.Transform(in transform);
    }

    private string CurrentLayerId => navigation.Current?.Id ?? HomeLayerId;

    private void PaintCurrentInside(Rect screen, float screenRadius, PhoneTheme theme, in HomeMotion motion)
    {
        if (navigation.Current is { } app)
        {
            PaintApp(screen, screenRadius, theme, app);
            return;
        }

        PaintHome(screen, screenRadius, theme, motion);
    }

    public void PaintHome(Rect screen, float screenRadius, PhoneTheme theme, in HomeMotion motion)
    {
        DeviceChrome.DrawWallpaper(screen, screenRadius, theme, motion);
        PaintHomeContent(screen, screenRadius, theme, motion);
    }

    public void PaintHomeContent(Rect screen, float screenRadius, PhoneTheme theme, in HomeMotion motion)
    {
        DeviceChrome.DrawHomeScrim(screen, screenRadius, theme);
        HomeHaunt.Draw(screen);
        home.Draw(screen, ContentRect(screen, theme), theme, navigation, motion);
        HomeHaunt.DrawTreats(screen);
    }

    public void PaintApp(Rect screen, float screenRadius, PhoneTheme theme, IPhoneApp app)
    {
        var underBackdrop = WallpaperBackdrop.Snapshot();
        WallpaperBackdrop.Clear();
        try
        {
            PaintAppSurface(screen, screenRadius, theme, app);
        }
        finally
        {
            WallpaperBackdrop.Restore(underBackdrop);
        }
    }

    private void PaintAppSurface(Rect screen, float screenRadius, PhoneTheme theme, IPhoneApp app)
    {
        var content = themes.ForApp(app.WantsSystemTheme);
        if (!app.WantsTransparentScreen)
        {
            DeviceChrome.FillScreen(screen, screenRadius, content.AppBackground);
            WallpaperBackdrop.RecordAppGround(screen, content.AppBackground, content.AppBackground);
        }

        Plugin.LiveBackdrop.Record(screen, content.AppBackground);
        var contentRect = ContentRect(screen, theme);
        try
        {
            using (AppVisits.Enter(app.Id))
            {
                app.Draw(new PhoneContext(contentRect, content, navigation));
            }
        }
        catch (Exception exception)
        {
            AepLog.Error(exception, $"[shell] app-draw {app.Id} threw");
            DrawAppFailure(contentRect, content);
        }
    }

    public void PaintLaunchCard(Rect screen, float screenRadius, IPhoneApp app)
    {
        var underBackdrop = WallpaperBackdrop.Snapshot();
        WallpaperBackdrop.Clear();
        try
        {
            var content = themes.ForApp(app.WantsSystemTheme);
            DeviceChrome.FillScreen(screen, screenRadius, content.AppBackground);
            WallpaperBackdrop.RecordAppGround(screen, content.AppBackground, content.AppBackground);
            var scale = UiScale.Current;
            var drawList = ImGui.GetWindowDrawList();
            var iconSize = LaunchIconUnits * scale;
            var iconCenter = new Vector2(screen.Center.X, screen.Center.Y - iconSize * 0.5f);
            DrawAppIcon(drawList, app, iconCenter, iconSize, 1f);
            var name = Typography.FitText(app.DisplayName, screen.Width - 2f * Metrics.Space.Xl * scale,
                TextStyles.Title3);
            var nameHeight = Typography.Measure(name, TextStyles.Title3).Y;
            var nameCenterY = iconCenter.Y + iconSize * 0.5f + Metrics.Space.Md * scale + nameHeight * 0.5f;
            Typography.DrawCentered(drawList, new Vector2(screen.Center.X, nameCenterY), name, content.TextStrong,
                TextStyles.Title3);
        }
        finally
        {
            WallpaperBackdrop.Restore(underBackdrop);
        }
    }

    public static void DrawAppIcon(ImDrawListPtr drawList, IPhoneApp app, Vector2 center, float size, float alpha)
    {
        var half = new Vector2(size, size) * 0.5f;
        var accent = app.Accent;
        if (AppIconTile.TryDraw(drawList, app.Id, accent, center - half, center + half,
                size * Metrics.Radius.TileFactor, alpha, false))
        {
            return;
        }

        var surface = IconTile.Surface(accent);
        Squircle.Fill(drawList, center - half, center + half, size * Metrics.Radius.TileFactor,
            ImGui.GetColorU32(Palette.WithAlpha(surface, alpha)));
        var ink = AppAccents.InkFor(app.Id);
        if (AppIconArt.TryDraw(drawList, app.Id, center, size * 0.9f, Palette.WithAlpha(ink, alpha),
                Palette.WithAlpha(Palette.Mix(surface, ink, 0.28f), alpha)))
        {
            return;
        }

        drawList.AddCircleFilled(center, size * 0.16f, ImGui.GetColorU32(Palette.WithAlpha(ink, alpha)), 12);
    }

    private static void DrawAppFailure(Rect content, PhoneTheme theme)
    {
        var draw = ImGui.GetWindowDrawList();
        var text = Loc.T(L.Common.AppDrawFailure);
        var size = ImGui.CalcTextSize(text);
        var position = new Vector2(content.Center.X - size.X * 0.5f, content.Center.Y - size.Y * 0.5f);
        draw.AddText(position, ImGui.ColorConvertFloat4ToU32(theme.TextMuted), text);
    }

    public static Rect ContentRect(Rect screen, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var min = new Vector2(screen.Min.X + theme.SidePadding * scale, screen.Min.Y + theme.TopZoneHeight * scale);
        var max = new Vector2(screen.Max.X - theme.SidePadding * scale, screen.Max.Y - theme.BottomZoneHeight * scale);
        return new Rect(min, max);
    }
}
