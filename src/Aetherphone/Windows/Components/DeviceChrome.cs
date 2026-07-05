using System.Numerics;
using Aetherphone.Core;
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

    public static Rect DrawBody(Rect window, PhoneTheme theme, bool fillScreen = true)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var device = BodyRect(window);
        var deviceRounding = theme.DeviceRounding * scale;
        var screen = device.Inset(theme.BezelThickness * scale);
        if (fillScreen)
        {
            dl.AddRectFilled(device.Min, device.Max, ImGui.GetColorU32(theme.BezelOuter), deviceRounding);
            dl.AddRect(device.Min, device.Max, ImGui.GetColorU32(theme.BezelRim), deviceRounding);
            dl.AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(theme.ScreenBase), theme.ScreenRounding * scale);
            return screen;
        }

        DrawBezelFrame(dl, device, screen, deviceRounding, ImGui.GetColorU32(theme.BezelOuter),
            ImGui.GetColorU32(theme.BezelRim));
        return screen;
    }

    private static void DrawBezelFrame(ImDrawListPtr dl, Rect device, Rect screen, float deviceRounding, uint bezel,
        uint rim)
    {
        dl.AddRectFilled(device.Min, new Vector2(device.Max.X, screen.Min.Y), bezel, deviceRounding,
            ImDrawFlags.RoundCornersTop);
        dl.AddRectFilled(new Vector2(device.Min.X, screen.Max.Y), device.Max, bezel, deviceRounding,
            ImDrawFlags.RoundCornersBottom);
        dl.AddRectFilled(new Vector2(device.Min.X, screen.Min.Y), new Vector2(screen.Min.X, screen.Max.Y), bezel, 0f,
            ImDrawFlags.RoundCornersNone);
        dl.AddRectFilled(new Vector2(screen.Max.X, screen.Min.Y), new Vector2(device.Max.X, screen.Max.Y), bezel, 0f,
            ImDrawFlags.RoundCornersNone);
        dl.AddRect(device.Min, device.Max, rim, deviceRounding);
    }

    public static void FillScreen(Rect screen, PhoneTheme theme, Vector4 color)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.GetWindowDrawList()
            .AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(color), theme.ScreenRounding * scale);
    }

    public static void DrawWallpaper(Rect screen, PhoneTheme theme)
    {
        var rounding = theme.ScreenRounding * ImGuiHelpers.GlobalScale;
        var library = Plugin.Wallpapers;
        library.CurrentTargetAspect = screen.Height > 0f ? screen.Width / screen.Height : 0.5f;
        var light = library.Resolve(theme.LightWallpaperId);
        var dark = library.Resolve(theme.DarkWallpaperId);
        WallpaperRenderer.Draw(ImGui.GetWindowDrawList(), screen, rounding, light, dark, library.CurrentTargetAspect,
            library.Darkness, theme.ScreenBase);
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
        ImGui.GetForegroundDrawList()
            .AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, dim)), rounding);
    }

    public static void DrawIsland(Rect island, PhoneTheme theme)
    {
        ImGui.GetWindowDrawList().AddRectFilled(island.Min, island.Max, ImGui.GetColorU32(theme.BezelOuter),
            island.Height * 0.5f);
    }
}
