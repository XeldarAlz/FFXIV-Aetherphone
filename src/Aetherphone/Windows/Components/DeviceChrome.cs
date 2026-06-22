using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class DeviceChrome
{
    public static Rect DrawBody(Rect device, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();

        var deviceRounding = theme.DeviceRounding * scale;
        dl.AddRectFilled(device.Min, device.Max, ImGui.GetColorU32(theme.BezelOuter), deviceRounding);
        dl.AddRect(device.Min, device.Max, ImGui.GetColorU32(theme.BezelRim), deviceRounding);

        var screen = device.Inset(theme.BezelThickness * scale);
        dl.AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(theme.ScreenBase), theme.ScreenRounding * scale);
        return screen;
    }

    public static void FillScreen(Rect screen, PhoneTheme theme, Vector4 color)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.GetWindowDrawList().AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(color), theme.ScreenRounding * scale);
    }

    public static void DrawWallpaper(Rect screen, PhoneTheme theme)
    {
        var rounding = theme.ScreenRounding * ImGuiHelpers.GlobalScale;
        var handle = Plugin.Wallpapers.Handle(theme.Wallpaper);
        ImGui.GetWindowDrawList().AddImageRounded(handle, screen.Min, screen.Max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, rounding, ImDrawFlags.RoundCornersAll);
    }

    public static void DrawIsland(Rect island, PhoneTheme theme)
    {
        ImGui.GetWindowDrawList().AddRectFilled(island.Min, island.Max, ImGui.GetColorU32(theme.BezelOuter), island.Height * 0.5f);
    }
}
