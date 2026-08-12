using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Shell.Home;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Core.Shell;

internal sealed class ShellScreenPainter
{
    public const float ImmersiveInset = 8f;
    private readonly ThemeProvider themes;
    private readonly NavigationStack navigation;
    private readonly HomeScreen home;

    public ShellScreenPainter(ThemeProvider themes, NavigationStack navigation, HomeScreen home)
    {
        this.themes = themes;
        this.navigation = navigation;
        this.home = home;
    }

    public void PaintCurrent(Rect screen, float screenRadius, PhoneTheme theme, in HomeMotion motion)
    {
        if (navigation.AtHome)
        {
            PaintHome(screen, screenRadius, theme, motion);
            return;
        }

        using (ImRaii.PushId(navigation.Current!.Id))
        {
            PaintApp(screen, screenRadius, theme, navigation.Current!);
        }
    }

    public void PaintHome(Rect screen, float screenRadius, PhoneTheme theme, in HomeMotion motion)
    {
        DeviceChrome.DrawWallpaper(screen, screenRadius, theme, motion);
        DeviceChrome.DrawHomeScrim(screen, screenRadius, theme);
        home.Draw(screen, ContentRect(screen, theme), theme, navigation, motion);
    }

    public void PaintApp(Rect screen, float screenRadius, PhoneTheme theme, IPhoneApp app)
    {
        var content = themes.ForApp(app.WantsSystemTheme);
        if (!app.WantsTransparentScreen)
        {
            DeviceChrome.FillScreen(screen, screenRadius, content.AppBackground);
        }

        var contentRect = app.WantsImmersiveContent
            ? ImmersiveContentRect(screen)
            : ContentRect(screen, theme);
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

    private static void DrawAppFailure(Rect content, PhoneTheme theme)
    {
        var draw = ImGui.GetWindowDrawList();
        var text = Loc.T(L.Common.AppDrawFailure);
        var size = ImGui.CalcTextSize(text);
        var position = new Vector2(content.Center.X - size.X * 0.5f, content.Center.Y - size.Y * 0.5f);
        draw.AddText(position, ImGui.ColorConvertFloat4ToU32(theme.TextMuted), text);
    }

    public static Rect ImmersiveContentRect(Rect screen)
    {
        var inset = ImmersiveInset * UiScale.Current;
        return screen.Inset(inset);
    }

    public static Rect ContentRect(Rect screen, PhoneTheme theme)
    {
        var scale = UiScale.Current;
        var min = new Vector2(screen.Min.X + theme.SidePadding * scale, screen.Min.Y + theme.TopZoneHeight * scale);
        var max = new Vector2(screen.Max.X - theme.SidePadding * scale, screen.Max.Y - theme.BottomZoneHeight * scale);
        return new Rect(min, max);
    }
}
