using System.Numerics;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Notifications;
using Aetherphone.Core.Playback;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Core.Shell;

internal sealed class PhoneShell : IDisposable
{
    private const ImGuiWindowFlags ChromeFlags =
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs;

    private readonly ThemeProvider themes;
    private readonly IReadOnlyList<IPhoneApp> apps;
    private readonly NavigationStack navigation;
    private readonly NotificationBanner banner;
    private readonly NowPlayingIsland nowPlaying;
    private readonly BootSequence boot = new();

    public PhoneShell(ThemeProvider themes, IReadOnlyList<IPhoneApp> apps, NotificationService notifications, PlaybackHub playback)
    {
        this.themes = themes;
        this.apps = apps;
        navigation = new NavigationStack(apps);
        banner = new NotificationBanner(notifications);
        nowPlaying = new NowPlayingIsland(playback);
    }

    public void OnOpened() => boot.Begin(!Plugin.Cfg.WelcomeShown);

    public void OnClosed() => boot.Cancel();

    public void OpenApp(string appId)
    {
        if (navigation.Current?.Id == appId)
        {
            return;
        }

        navigation.Open(appId);
    }

    public void Draw(Rect device)
    {
        var theme = themes.Current;
        var screen = DeviceChrome.DrawBody(device, theme, !TransparencyActive());

        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        boot.Advance(delta);
        navigation.Advance(delta);
        banner.Advance(delta);

        var islandCaptures = !boot.IsActive && nowPlaying.CapturesPointer(screen);

        using (InputShield.Engage(boot.IsActive || islandCaptures))
        {
            DrawContent(screen, theme);
            DrawChrome(screen, theme);
        }

        if (boot.IsActive)
        {
            BootScreen.Draw(screen, theme, boot);
        }
        else
        {
            banner.Draw(screen, theme);
            nowPlaying.Draw(screen, theme, navigation);
        }
    }

    private bool TransparencyActive()
    {
        if (navigation.IsTransitioning)
        {
            return navigation.MotionOver.WantsTransparentScreen || (navigation.MotionUnder?.WantsTransparentScreen ?? false);
        }

        return !navigation.AtHome && (navigation.Current?.WantsTransparentScreen ?? false);
    }

    private void DrawContent(Rect screen, PhoneTheme theme)
    {
        if (navigation.IsTransitioning)
        {
            DrawTransition(screen, theme);
        }
        else if (navigation.AtHome)
        {
            PaintHome(screen, theme);
        }
        else
        {
            using (ImRaii.PushId(navigation.Current!.Id))
            {
                PaintApp(screen, theme, navigation.Current!);
            }
        }
    }

    private void DrawChrome(Rect screen, PhoneTheme theme)
    {
        ImGui.SetCursorScreenPos(screen.Min);
        using (ImRaii.Child("chrome", screen.Size, false, ChromeFlags))
        {
            StatusBar.Draw(screen, theme);
            DrawHomeIndicator(screen, theme);
            DrawLockButton(screen, theme);
        }
    }

    private static void DrawLockButton(Rect screen, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var radius = 13f * scale;
        var center = new Vector2(screen.Max.X - 30f * scale, screen.Max.Y - 28f * scale);

        if (LockButton.Draw(center, radius, Plugin.Cfg.LockPosition, theme))
        {
            Plugin.Cfg.LockPosition = !Plugin.Cfg.LockPosition;
            Plugin.Cfg.Save();
        }
    }

    private void DrawTransition(Rect screen, PhoneTheme theme)
    {
        var cover = navigation.MotionProgress;
        var height = screen.Height;
        var over = navigation.MotionOver;
        var under = navigation.MotionUnder;

        var overOffset = new Vector2(0f, (1f - cover) * height);
        var underDim = cover * TransitionTiming.ShellDimMax;

        LayerPainter underPaint = under is null ? target => PaintHome(target, theme) : target => PaintApp(target, theme, under);
        LayerPainter overPaint = target => PaintApp(target, theme, over);

        if (over.WantsTransparentScreen || (under?.WantsTransparentScreen ?? false))
        {
            var band = new Rect(screen.Min, new Vector2(screen.Max.X, screen.Min.Y + overOffset.Y));
            SceneCompositor.DrawClipped(band, screen, underDim, underPaint);
            SceneCompositor.DrawLayer(screen, new SceneCompositor.Layer(over.Id, overOffset, 0f, overPaint, default, true));
            return;
        }

        var underLayer = new SceneCompositor.Layer(under?.Id ?? "home", Vector2.Zero, underDim, underPaint, default, true);
        var overLayer = new SceneCompositor.Layer(over.Id, overOffset, 0f, overPaint, default, true);

        SceneCompositor.Composite(screen, underLayer, overLayer);
    }

    private void PaintHome(Rect screen, PhoneTheme theme)
    {
        DeviceChrome.DrawWallpaper(screen, theme);
        HomeScreen.Draw(ContentRect(screen, theme), theme, apps, navigation);
    }

    private void PaintApp(Rect screen, PhoneTheme theme, IPhoneApp app)
    {
        if (!app.WantsTransparentScreen)
        {
            DeviceChrome.FillScreen(screen, theme, theme.AppBackground);
        }

        app.Draw(new PhoneContext(ContentRect(screen, theme), theme, navigation));
    }

    private static Rect ContentRect(Rect screen, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var min = new Vector2(screen.Min.X + theme.SidePadding * scale, screen.Min.Y + theme.TopZoneHeight * scale);
        var max = new Vector2(screen.Max.X - theme.SidePadding * scale, screen.Max.Y - theme.BottomZoneHeight * scale);
        return new Rect(min, max);
    }

    private void DrawHomeIndicator(Rect screen, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = 112f * scale;
        var height = 5f * scale;
        var center = new Vector2(screen.Center.X, screen.Max.Y - 14f * scale);
        var min = new Vector2(center.X - width * 0.5f, center.Y - height * 0.5f);
        var max = new Vector2(center.X + width * 0.5f, center.Y + height * 0.5f);

        var hitMin = new Vector2(min.X - 24f * scale, min.Y - 16f * scale);
        var hitMax = new Vector2(max.X + 24f * scale, max.Y + 16f * scale);
        var actionable = !navigation.AtHome && !navigation.IsTransitioning && ImGui.IsMouseHoveringRect(hitMin, hitMax);

        var color = actionable ? theme.TextStrong : Palette.WithAlpha(theme.TextStrong, 0.55f);
        ImGui.GetWindowDrawList().AddRectFilled(min, max, ImGui.GetColorU32(color), height * 0.5f);

        if (!actionable)
        {
            return;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            navigation.GoHome();
        }
    }

    public void Dispose()
    {
        banner.Dispose();
        for (var index = 0; index < apps.Count; index++)
        {
            apps[index].Dispose();
        }
    }
}
