using System.Numerics;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Shell.Home;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Core.Shell;

internal sealed class ShellScreenPainter
{
    private readonly ThemeProvider themes;
    private readonly NavigationStack navigation;
    private readonly HomeScreen home;

    public ShellScreenPainter(ThemeProvider themes, NavigationStack navigation, HomeScreen home)
    {
        this.themes = themes;
        this.navigation = navigation;
        this.home = home;
    }

    public void PaintCurrent(Rect screen, PhoneTheme theme, in HomeMotion motion)
    {
        if (navigation.AtHome)
        {
            PaintHome(screen, theme, motion);
            return;
        }

        using (ImRaii.PushId(navigation.Current!.Id))
        {
            PaintApp(screen, theme, navigation.Current!);
        }
    }

    public void PaintHome(Rect screen, PhoneTheme theme, in HomeMotion motion)
    {
        DeviceChrome.DrawWallpaper(screen, theme, motion);
        DeviceChrome.DrawHomeScrim(screen, theme);
        home.Draw(screen, ContentRect(screen, theme), theme, navigation, motion);
    }

    public void PaintApp(Rect screen, PhoneTheme theme, IPhoneApp app)
    {
        var content = themes.Current;
        if (!app.WantsTransparentScreen)
        {
            DeviceChrome.FillScreen(screen, theme, content.AppBackground);
        }

        var contentRect = ContentRect(screen, theme);
        try
        {
            app.Draw(new PhoneContext(contentRect, content, navigation));
        }
        catch (Exception exception)
        {
            AepLog.Error($"[shell] app-draw {app.Id} threw: {exception.Message}");
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

    public static Rect ContentRect(Rect screen, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var min = new Vector2(screen.Min.X + theme.SidePadding * scale, screen.Min.Y + theme.TopZoneHeight * scale);
        var max = new Vector2(screen.Max.X - theme.SidePadding * scale, screen.Max.Y - theme.BottomZoneHeight * scale);
        return new Rect(min, max);
    }
}
