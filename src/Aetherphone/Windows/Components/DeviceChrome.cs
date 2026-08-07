using Aetherphone.Core;
using Aetherphone.Core.Shell.Home;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Windows.Components;

internal static class DeviceChrome
{
    private const float SideButtonStartFraction = 0.250f;
    private const float SideButtonLengthFraction = 0.108f;
    private const float MuteButtonStartFraction = 0.205f;
    private const float LockButtonStartFraction = 0.315f;
    private const float ShortButtonLengthFraction = 0.082f;
    private const float ChamferFraction = 0.4f;

    private const float MaskGrow = 0.5f;

    public static Rect BodyRect(Rect window, PhoneTheme theme)
        => ChassisGeometry.BodyRect(window, theme, UiScale.Current);

    public static ChassisGeometry Chassis(Rect window, PhoneTheme theme) =>
        ChassisGeometry.Device(window, theme, UiScale.Current);

    public static Rect SideButtonRect(Rect window, in ChassisGeometry chassis, out RailSide side)
    {
        var scale = UiScale.Current;
        var device = chassis.Body;
        if (device.IsLandscape())
        {
            side = RailSide.Top;
            var left = device.Min.X + device.Width * SideButtonStartFraction;
            var width = device.Width * SideButtonLengthFraction;
            return new Rect(new Vector2(left, window.Min.Y), new Vector2(left + width, device.Min.Y + 2f * scale));
        }

        side = RailSide.Right;
        var top = device.Min.Y + device.Height * SideButtonStartFraction;
        var height = device.Height * SideButtonLengthFraction;
        return new Rect(new Vector2(device.Max.X - 2f * scale, top), new Vector2(window.Max.X, top + height));
    }

    public static Rect MuteButtonRect(Rect window, in ChassisGeometry chassis, out RailSide side)
    {
        var scale = UiScale.Current;
        var device = chassis.Body;
        if (device.IsLandscape())
        {
            side = RailSide.Bottom;
            var left = device.Min.X + device.Width * MuteButtonStartFraction;
            var width = device.Width * ShortButtonLengthFraction;
            return new Rect(new Vector2(left, device.Max.Y - 2f * scale), new Vector2(left + width, window.Max.Y));
        }

        side = RailSide.Left;
        var top = device.Min.Y + device.Height * MuteButtonStartFraction;
        var height = device.Height * ShortButtonLengthFraction;
        return new Rect(new Vector2(window.Min.X, top), new Vector2(device.Min.X + 2f * scale, top + height));
    }

    public static Rect LockButtonRect(Rect window, in ChassisGeometry chassis, out RailSide side)
    {
        var scale = UiScale.Current;
        var device = chassis.Body;
        if (device.IsLandscape())
        {
            side = RailSide.Bottom;
            var left = device.Min.X + device.Width * LockButtonStartFraction;
            var width = device.Width * ShortButtonLengthFraction;
            return new Rect(new Vector2(left, device.Max.Y - 2f * scale), new Vector2(left + width, window.Max.Y));
        }

        side = RailSide.Left;
        var top = device.Min.Y + device.Height * LockButtonStartFraction;
        var height = device.Height * ShortButtonLengthFraction;
        return new Rect(new Vector2(window.Min.X, top), new Vector2(device.Min.X + 2f * scale, top + height));
    }

    public static Rect DrawBody(in ChassisGeometry chassis, PhoneTheme theme, Rect? transparentBand = null)
    {
        var scale = UiScale.Current;
        var dl = ImGui.GetWindowDrawList();
        if (transparentBand is not { } band)
        {
            DrawShell(dl, chassis, scale, theme, 1f);
            return chassis.Screen;
        }

        var glass = ImGui.GetColorU32(theme.Glass);
        var screenBase = ImGui.GetColorU32(theme.ScreenBase);
        var frame = ImGui.GetColorU32(theme.FrameMetal);
        var paintMetal = true;
        if (theme.WantsCaseArt && PhoneCaseTextures.Skin(theme.CaseTextureId) is { } bandTexture)
        {
            CaseArt.QuadExcluding(dl, bandTexture, CaseArt.RectFor(chassis.Body), band,
                chassis.Body.IsLandscape());
            paintMetal = false;
        }

        if (band.Min.Y <= chassis.Screen.Min.Y + 0.5f && band.Max.Y >= chassis.Screen.Max.Y - 0.5f)
        {
            DrawViewportBodySideways(dl, chassis, band, frame, glass, screenBase, paintMetal);
        }
        else
        {
            DrawViewportBody(dl, chassis, band, frame, glass, screenBase, paintMetal);
        }

        if (paintMetal)
        {
            RailFinish(dl, chassis, scale, theme.Case);
            return chassis.Screen;
        }

        ScreenRecess(dl, chassis, scale);
        return chassis.Screen;
    }

    public static void DrawShell(ImDrawListPtr dl, in ChassisGeometry chassis, float scale, in CaseFinish finish,
        Vector4 screenBase)
    {
        Squircle.Fill(dl, chassis.Body.Min, chassis.Body.Max, chassis.BodyRadius, ImGui.GetColorU32(finish.Frame));
        Squircle.Fill(dl, chassis.Glass.Min, chassis.Glass.Max, chassis.GlassRadius, ImGui.GetColorU32(finish.Glass));
        Squircle.Fill(dl, chassis.Screen.Min, chassis.Screen.Max, chassis.ScreenRadius, ImGui.GetColorU32(screenBase));
        RailFinish(dl, chassis, scale, finish);
    }

    public static void DrawShell(ImDrawListPtr dl, in ChassisGeometry chassis, float scale, PhoneTheme theme,
        float artAlpha)
    {
        var skin = artAlpha > 0.001f && theme.WantsCaseArt ? PhoneCaseTextures.Skin(theme.CaseTextureId) : null;
        if (skin is not { } texture)
        {
            DrawShell(dl, chassis, scale, theme.Case, theme.ScreenBase);
            return;
        }

        if (artAlpha < 0.999f)
        {
            Squircle.Fill(dl, chassis.Body.Min, chassis.Body.Max, chassis.BodyRadius,
                ImGui.GetColorU32(theme.FrameMetal));
        }

        CaseArt.Quad(dl, texture, CaseArt.RectFor(chassis.Body), chassis.Body.IsLandscape(),
            CaseArt.Tint(artAlpha));
        Squircle.Fill(dl, chassis.Glass.Min, chassis.Glass.Max, chassis.GlassRadius, ImGui.GetColorU32(theme.Glass));
        Squircle.Fill(dl, chassis.Screen.Min, chassis.Screen.Max, chassis.ScreenRadius,
            ImGui.GetColorU32(theme.ScreenBase));
        var step = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.55f));
        Squircle.Stroke(dl, chassis.Glass.Min, chassis.Glass.Max, chassis.GlassRadius, step, 1f * scale);
        ScreenRecess(dl, chassis, scale);
    }

    private static void DrawViewportBody(ImDrawListPtr dl, in ChassisGeometry chassis, Rect band, uint frame,
        uint glass, uint screenBase, bool paintMetal)
    {
        var screen = chassis.Screen;
        var top = Math.Clamp(band.Min.Y, screen.Min.Y, screen.Max.Y);
        var bottom = Math.Clamp(band.Max.Y, top, screen.Max.Y);
        if (paintMetal)
        {
            CapPair(dl, chassis.Body, chassis.BodyRadius, top, bottom, frame);
            dl.AddRectFilled(new Vector2(chassis.Body.Min.X, top), new Vector2(chassis.Glass.Min.X, bottom), frame);
            dl.AddRectFilled(new Vector2(chassis.Glass.Max.X, top), new Vector2(chassis.Body.Max.X, bottom), frame);
        }

        CapPair(dl, chassis.Glass, chassis.GlassRadius, top, bottom, glass);
        CapPair(dl, screen, chassis.ScreenRadius, top, bottom, screenBase);
        dl.AddRectFilled(new Vector2(chassis.Glass.Min.X, top), new Vector2(screen.Min.X, bottom), glass);
        dl.AddRectFilled(new Vector2(screen.Max.X, top), new Vector2(chassis.Glass.Max.X, bottom), glass);
    }

    private static void DrawViewportBodySideways(ImDrawListPtr dl, in ChassisGeometry chassis, Rect band, uint frame,
        uint glass, uint screenBase, bool paintMetal)
    {
        var screen = chassis.Screen;
        var left = Math.Clamp(band.Min.X, screen.Min.X, screen.Max.X);
        var right = Math.Clamp(band.Max.X, left, screen.Max.X);
        if (paintMetal)
        {
            SideCapPair(dl, chassis.Body, chassis.BodyRadius, left, right, frame);
            dl.AddRectFilled(new Vector2(left, chassis.Body.Min.Y), new Vector2(right, chassis.Glass.Min.Y), frame);
            dl.AddRectFilled(new Vector2(left, chassis.Glass.Max.Y), new Vector2(right, chassis.Body.Max.Y), frame);
        }

        SideCapPair(dl, chassis.Glass, chassis.GlassRadius, left, right, glass);
        SideCapPair(dl, screen, chassis.ScreenRadius, left, right, screenBase);
        dl.AddRectFilled(new Vector2(left, chassis.Glass.Min.Y), new Vector2(right, screen.Min.Y), glass);
        dl.AddRectFilled(new Vector2(left, screen.Max.Y), new Vector2(right, chassis.Glass.Max.Y), glass);
    }

    private static void CapPair(ImDrawListPtr dl, Rect band, float radius, float top, float bottom, uint color)
    {
        Squircle.FillCap(dl, band.Min, new Vector2(band.Max.X, top), radius, color, true);
        Squircle.FillCap(dl, new Vector2(band.Min.X, bottom), band.Max, radius, color, false);
    }

    private static void SideCapPair(ImDrawListPtr dl, Rect band, float radius, float left, float right, uint color)
    {
        Squircle.FillSideCap(dl, band.Min, new Vector2(left, band.Max.Y), radius, color, true);
        Squircle.FillSideCap(dl, new Vector2(right, band.Min.Y), band.Max, radius, color, false);
    }

    internal static void RailFinish(ImDrawListPtr dl, in ChassisGeometry chassis, float scale, in CaseFinish finish)
    {
        Chamfer(dl, chassis, scale, finish);
        var step = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.55f));
        Squircle.Stroke(dl, chassis.Glass.Min, chassis.Glass.Max, chassis.GlassRadius, step, 1f * scale);
        ScreenRecess(dl, chassis, scale);
    }

    private static void ScreenRecess(ImDrawListPtr dl, in ChassisGeometry chassis, float scale)
    {
        var recess = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.5f));
        Squircle.Stroke(dl, chassis.Screen.Min, chassis.Screen.Max, chassis.ScreenRadius, recess, 1.4f * scale);
    }

    private static void Chamfer(ImDrawListPtr dl, in ChassisGeometry chassis, float scale, in CaseFinish finish)
    {
        var metal = (chassis.Body.Width - chassis.Glass.Width) * 0.5f;
        var inset = MathF.Max(MathF.Round(metal * ChamferFraction), 1f);
        var min = new Vector2(chassis.Body.Min.X + inset, chassis.Body.Min.Y + inset);
        var max = new Vector2(chassis.Body.Max.X - inset, chassis.Body.Max.Y - inset);
        if (max.X - min.X <= 0f || max.Y - min.Y <= 0f)
        {
            return;
        }

        var radius = MathF.Max(chassis.BodyRadius - inset, 0f);
        Squircle.Stroke(dl, min, max, radius, ImGui.GetColorU32(finish.Rim), 1.4f * scale);
        var brightColor = finish.EdgeBright;
        var dimColor = finish.EdgeDim;
        var bright = ImGui.GetColorU32(brightColor);
        var dim = ImGui.GetColorU32(dimColor);
        if (max.X - min.X > 2f * radius)
        {
            dl.AddLine(new Vector2(min.X + radius, min.Y), new Vector2(max.X - radius, min.Y), bright, 1.5f * scale);
            dl.AddLine(new Vector2(min.X + radius, max.Y), new Vector2(max.X - radius, max.Y), dim, 1.2f * scale);
        }

        if (max.Y - min.Y > 2f * radius)
        {
            dl.AddLine(new Vector2(min.X, min.Y + radius), new Vector2(min.X, max.Y - radius), bright, 1.2f * scale);
            dl.AddLine(new Vector2(max.X, min.Y + radius), new Vector2(max.X, max.Y - radius), dim, 1.2f * scale);
        }

        Squircle.StrokeCorner(dl, min, max, radius, 0, brightColor, brightColor, 1.35f * scale);
        Squircle.StrokeCorner(dl, min, max, radius, 1, dimColor, brightColor, 1.35f * scale);
        Squircle.StrokeCorner(dl, min, max, radius, 2, dimColor, dimColor, 1.35f * scale);
        Squircle.StrokeCorner(dl, min, max, radius, 3, brightColor, dimColor, 1.35f * scale);
    }

    public static void FillScreen(Rect screen, float radius, Vector4 color)
    {
        Squircle.Fill(ImGui.GetWindowDrawList(), screen.Min, screen.Max, radius, ImGui.GetColorU32(color));
    }

    public static void MaskScreenCorners(ImDrawListPtr dl, in ChassisGeometry chassis, PhoneTheme theme, float scale)
    {
        Squircle.FillOutsideCorners(dl, chassis.Screen.Min, chassis.Screen.Max, chassis.ScreenRadius,
            ImGui.GetColorU32(theme.Glass), MaskGrow * scale);
    }

    public static void SealScreen(in ChassisGeometry chassis, PhoneTheme theme, float brightness)
    {
        var scale = UiScale.Current;
        var dl = ImGui.GetForegroundDrawList();
        MaskScreenCorners(dl, chassis, theme, scale);
        DrawBrightnessVeil(dl, chassis, brightness);
    }

    public static void DrawWallpaper(Rect screen, float screenRadius, PhoneTheme theme, in HomeMotion motion)
    {
        var quad = motion.Zoom == 1f ? screen : motion.Warp(screen);
        var shape = motion.Zoom >= 1f ? screen : quad;
        var radius = motion.Zoom >= 1f ? screenRadius : screenRadius * motion.Zoom;
        var library = Plugin.Wallpapers;
        library.CurrentTargetAspect = screen.Height > 0f ? screen.Width / screen.Height : 0.5f;
        var light = library.Resolve(theme.LightWallpaperId);
        var dark = library.Resolve(theme.DarkWallpaperId);
        WallpaperRenderer.Draw(ImGui.GetWindowDrawList(), shape, quad, radius, light, dark,
            library.CurrentTargetAspect, library.ThemeDarkness, theme.ScreenBase);
    }

    public static void DrawHomeScrim(Rect screen, float radius, PhoneTheme theme)
    {
        const float calmDim = 0.08f;
        const float harshDim = 0.30f;
        var dim = calmDim + (harshDim - calmDim) * WallpaperLegibility.Strength(theme);
        Squircle.Fill(ImGui.GetWindowDrawList(), screen.Min, screen.Max, radius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, dim)));
    }

    public static void DrawBrightnessVeil(ImDrawListPtr dl, in ChassisGeometry chassis, float brightness)
    {
        const float MaxDim = 0.88f;
        var dim = (1f - Math.Clamp(brightness, 0f, 1f)) * MaxDim;
        if (dim <= 0.001f)
        {
            return;
        }

        Squircle.Fill(dl, chassis.Screen.Min, chassis.Screen.Max, chassis.ScreenRadius,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, dim)));
    }

    public static void DrawIsland(Rect island, PhoneTheme theme)
    {
        ImGui.GetWindowDrawList().AddRectFilled(island.Min, island.Max, ImGui.GetColorU32(theme.Glass),
            island.Height * 0.5f);
    }
}
