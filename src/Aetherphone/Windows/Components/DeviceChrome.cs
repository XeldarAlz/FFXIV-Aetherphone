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
            dl.AddRectFilled(device.Min, device.Max, bezel, deviceRounding);
            dl.AddRect(device.Min, device.Max, rim, deviceRounding);
            dl.AddRectFilled(screen.Min, screen.Max, screenBase, screenRounding);
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
        dl.AddRectFilled(device.Min, new Vector2(device.Max.X, top), bezel, deviceRounding, ImDrawFlags.RoundCornersTop);
        dl.AddRectFilled(new Vector2(device.Min.X, bottom), device.Max, bezel, deviceRounding,
            ImDrawFlags.RoundCornersBottom);
        dl.AddRectFilled(new Vector2(device.Min.X, top), new Vector2(screen.Min.X, bottom), bezel, 0f,
            ImDrawFlags.RoundCornersNone);
        dl.AddRectFilled(new Vector2(screen.Max.X, top), new Vector2(device.Max.X, bottom), bezel, 0f,
            ImDrawFlags.RoundCornersNone);
        dl.AddRectFilled(screen.Min, new Vector2(screen.Max.X, top), screenBase, screenRounding,
            ImDrawFlags.RoundCornersTop);
        dl.AddRectFilled(new Vector2(screen.Min.X, bottom), screen.Max, screenBase, screenRounding,
            ImDrawFlags.RoundCornersBottom);
        dl.AddRect(device.Min, device.Max, rim, deviceRounding);
    }

    public static void FillScreen(Rect screen, PhoneTheme theme, Vector4 color)
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.GetWindowDrawList()
            .AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(color), theme.ScreenRounding * scale);
    }

    public static void MaskScreenCorners(Rect screen, PhoneTheme theme)
    {
        var radius = theme.ScreenRounding * ImGuiHelpers.GlobalScale;
        if (radius <= 0.5f)
        {
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        var bezel = ImGui.GetColorU32(theme.BezelOuter);
        NotchCorner(drawList, screen.Min, new Vector2(screen.Min.X + radius, screen.Min.Y + radius), radius, bezel,
            MathF.PI, MathF.PI * 1.5f);
        NotchCorner(drawList, new Vector2(screen.Max.X, screen.Min.Y),
            new Vector2(screen.Max.X - radius, screen.Min.Y + radius), radius, bezel, MathF.PI * 1.5f, MathF.PI * 2f);
        NotchCorner(drawList, screen.Max, new Vector2(screen.Max.X - radius, screen.Max.Y - radius), radius, bezel, 0f,
            MathF.PI * 0.5f);
        NotchCorner(drawList, new Vector2(screen.Min.X, screen.Max.Y),
            new Vector2(screen.Min.X + radius, screen.Max.Y - radius), radius, bezel, MathF.PI * 0.5f, MathF.PI);
    }

    private static void NotchCorner(ImDrawListPtr drawList, Vector2 corner, Vector2 center, float radius, uint color,
        float angleMin, float angleMax)
    {
        var segments = Math.Clamp((int)MathF.Ceiling(radius * 0.5f), 12, 64);
        var previous = ArcPoint(center, radius, angleMin);
        for (var index = 1; index <= segments; index++)
        {
            var angle = angleMin + (angleMax - angleMin) * index / segments;
            var next = ArcPoint(center, radius, angle);
            drawList.AddTriangleFilled(corner, previous, next, color);
            previous = next;
        }
    }

    private static Vector2 ArcPoint(Vector2 center, float radius, float angle) =>
        new(center.X + MathF.Cos(angle) * radius, center.Y + MathF.Sin(angle) * radius);

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
        ImGui.GetForegroundDrawList()
            .AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, dim)), rounding);
    }

    public static void DrawIsland(Rect island, PhoneTheme theme)
    {
        ImGui.GetWindowDrawList().AddRectFilled(island.Min, island.Max, ImGui.GetColorU32(theme.BezelOuter),
            island.Height * 0.5f);
    }
}
