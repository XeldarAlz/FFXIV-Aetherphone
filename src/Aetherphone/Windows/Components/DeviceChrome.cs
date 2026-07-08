using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Shell.Home;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class DeviceChrome
{
    public const float RailWidth = 7f;

    public static Rect BodyRect(Rect window)
    {
        var rail = RailWidth * ImGuiHelpers.GlobalScale;
        return new Rect(new Vector2(window.Min.X + rail, window.Min.Y), new Vector2(window.Max.X - rail, window.Max.Y));
    }

    public static Rect SideButtonRect(Rect window)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var device = BodyRect(window);
        var top = device.Min.Y + device.Height * 0.255f;
        var height = device.Height * 0.092f;
        return new Rect(new Vector2(device.Max.X - 2f * scale, top), new Vector2(window.Max.X, top + height));
    }

    public static Rect MuteButtonRect(Rect window)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var device = BodyRect(window);
        var top = device.Min.Y + device.Height * 0.155f;
        var height = device.Height * 0.048f;
        return new Rect(new Vector2(window.Min.X, top), new Vector2(device.Min.X + 2f * scale, top + height));
    }

    public static Rect LockButtonRect(Rect window)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var device = BodyRect(window);
        var top = device.Min.Y + device.Height * 0.255f;
        var height = device.Height * 0.092f;
        return new Rect(new Vector2(window.Min.X, top), new Vector2(device.Min.X + 2f * scale, top + height));
    }

    public static Rect ScreenRect(Rect window, PhoneTheme theme) =>
        BodyRect(window).Inset(theme.BezelThickness * ImGuiHelpers.GlobalScale);

    public static Rect DrawBody(Rect window, PhoneTheme theme, Rect? transparentBand = null)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var device = BodyRect(window);
        var deviceRounding = theme.DeviceRounding * scale;
        var screenRounding = theme.ScreenRounding * scale;
        var screen = device.Inset(theme.BezelThickness * scale);
        var bezel = ImGui.GetColorU32(theme.BezelOuter);
        var rim = ImGui.GetColorU32(theme.BezelRim);
        var screenBase = ImGui.GetColorU32(theme.ScreenBase);
        if (transparentBand is not { } band)
        {
            Squircle.Fill(dl, device.Min, device.Max, deviceRounding, bezel);
            Squircle.Stroke(dl, device.Min, device.Max, deviceRounding, rim, 1f);
            Squircle.Fill(dl, screen.Min, screen.Max, screenRounding, screenBase);
            return screen;
        }

        DrawViewportBody(dl, device, screen, band, deviceRounding, screenRounding, bezel, rim, screenBase);
        return screen;
    }

    private static void DrawViewportBody(ImDrawListPtr dl, Rect device, Rect screen, Rect band, float deviceRounding,
        float screenRounding, uint bezel, uint rim, uint screenBase)
    {
        var top = Math.Clamp(band.Min.Y, screen.Min.Y, screen.Max.Y);
        var bottom = Math.Clamp(band.Max.Y, top, screen.Max.Y);
        Squircle.FillCap(dl, device.Min, new Vector2(device.Max.X, top), deviceRounding, bezel, true);
        Squircle.FillCap(dl, new Vector2(device.Min.X, bottom), device.Max, deviceRounding, bezel, false);
        dl.AddRectFilled(new Vector2(device.Min.X, top), new Vector2(screen.Min.X, bottom), bezel);
        dl.AddRectFilled(new Vector2(screen.Max.X, top), new Vector2(device.Max.X, bottom), bezel);
        Squircle.FillCap(dl, screen.Min, new Vector2(screen.Max.X, top), screenRounding, screenBase, true);
        Squircle.FillCap(dl, new Vector2(screen.Min.X, bottom), screen.Max, screenRounding, screenBase, false);
        Squircle.Stroke(dl, device.Min, device.Max, deviceRounding, rim, 1f);
    }

    public static void FillScreen(Rect screen, PhoneTheme theme, Vector4 color)
    {
        var scale = ImGuiHelpers.GlobalScale;
        Squircle.Fill(ImGui.GetWindowDrawList(), screen.Min, screen.Max, theme.ScreenRounding * scale,
            ImGui.GetColorU32(color));
    }

    public static void MaskScreenCorners(Rect screen, PhoneTheme theme)
    {
        var radius = theme.ScreenRounding * ImGuiHelpers.GlobalScale;
        if (radius <= 0.5f)
        {
            return;
        }

        Squircle.FillOutsideCorners(ImGui.GetWindowDrawList(), screen.Min, screen.Max, radius,
            ImGui.GetColorU32(theme.BezelOuter));
    }

    public static void DrawWallpaper(Rect screen, PhoneTheme theme)
    {
        DrawWallpaperInto(screen, screen, theme, 1f);
    }

    public static void DrawWallpaper(Rect screen, PhoneTheme theme, in HomeMotion motion)
    {
        if (motion.Zoom == 1f)
        {
            DrawWallpaperInto(screen, screen, theme, 1f);
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(screen.Min, screen.Max, true);
        DrawWallpaperInto(motion.Warp(screen), screen, theme, motion.Zoom);
        drawList.PopClipRect();
    }

    private static void DrawWallpaperInto(Rect destination, Rect screen, PhoneTheme theme, float zoom)
    {
        var rounding = theme.ScreenRounding * ImGuiHelpers.GlobalScale * zoom;
        var library = Plugin.Wallpapers;
        library.CurrentTargetAspect = screen.Height > 0f ? screen.Width / screen.Height : 0.5f;
        var light = library.Resolve(theme.LightWallpaperId);
        var dark = library.Resolve(theme.DarkWallpaperId);
        WallpaperRenderer.Draw(ImGui.GetWindowDrawList(), destination, rounding, light, dark,
            library.CurrentTargetAspect, library.Darkness, theme.ScreenBase);
    }

    public static void DrawHomeScrim(Rect screen, PhoneTheme theme)
    {
        const float calmDim = 0.08f;
        const float harshDim = 0.30f;
        var dim = calmDim + (harshDim - calmDim) * WallpaperLegibility.Strength(theme);
        var rounding = theme.ScreenRounding * ImGuiHelpers.GlobalScale;
        ImGui.GetWindowDrawList()
            .AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, dim)), rounding);
    }

    public static void DrawBrightnessVeil(Rect screen, PhoneTheme theme, float brightness)
    {
        const float MaxDim = 0.88f;
        var dim = (1f - Math.Clamp(brightness, 0f, 1f)) * MaxDim;
        if (dim <= 0.001f)
        {
            return;
        }

        var rounding = theme.ScreenRounding * ImGuiHelpers.GlobalScale;
        Squircle.Fill(ImGui.GetForegroundDrawList(), screen.Min, screen.Max, rounding,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, dim)));
    }

    public static void DrawIsland(Rect island, PhoneTheme theme)
    {
        ImGui.GetWindowDrawList().AddRectFilled(island.Min, island.Max, ImGui.GetColorU32(theme.BezelOuter),
            island.Height * 0.5f);
    }
}
