using System.Numerics;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Apps;
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
    private readonly BootSequence boot = new();

    public PhoneShell(ThemeProvider themes, IReadOnlyList<IPhoneApp> apps)
    {
        this.themes = themes;
        this.apps = apps;
        navigation = new NavigationStack(apps);
    }

    public void OnOpened() => boot.Begin(!Plugin.Cfg.WelcomeShown);

    public void OnClosed() => boot.Cancel();

    public void Draw(Rect device)
    {
        var theme = themes.Current;
        var screen = DeviceChrome.DrawBody(device, theme);

        var delta = MathF.Min(ImGui.GetIO().DeltaTime, TransitionTiming.MaxFrameSeconds);
        boot.Advance(delta);
        navigation.Advance(delta);

        using (InputShield.Engage(boot.IsActive))
        {
            DrawContent(screen, theme);
            DrawChrome(screen, theme);
        }

        if (boot.IsActive)
        {
            BootScreen.Draw(screen, theme, boot);
        }
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
        var progress = navigation.MotionProgress;
        var height = screen.Height;
        var over = navigation.MotionOver;
        var under = navigation.MotionUnder;

        Vector2 overOffset;
        float underDim;
        if (navigation.Motion == ShellMotion.Present)
        {
            overOffset = new Vector2(0f, (1f - progress) * height);
            underDim = progress * TransitionTiming.ShellDimMax;
        }
        else
        {
            overOffset = new Vector2(0f, progress * height);
            underDim = (1f - progress) * TransitionTiming.ShellDimMax;
        }

        var underLayer = under is null
            ? new SceneCompositor.Layer("home", Vector2.Zero, underDim, target => PaintHome(target, theme), default, true)
            : new SceneCompositor.Layer(under.Id, Vector2.Zero, underDim, target => PaintApp(target, theme, under), default, true);
        var overLayer = new SceneCompositor.Layer(over.Id, overOffset, 0f, target => PaintApp(target, theme, over), default, true);

        SceneCompositor.Composite(screen, underLayer, overLayer);
    }

    private void PaintHome(Rect screen, PhoneTheme theme)
    {
        DeviceChrome.DrawWallpaper(screen, theme);
        HomeScreen.Draw(ContentRect(screen, theme), theme, apps, navigation);
    }

    private void PaintApp(Rect screen, PhoneTheme theme, IPhoneApp app)
    {
        DeviceChrome.FillScreen(screen, theme, theme.AppBackground);
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
        for (var index = 0; index < apps.Count; index++)
        {
            apps[index].Dispose();
        }
    }
}
