using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Maps;
using Aetherphone.Core.Shell;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Components;

internal readonly struct MinimapZoomResult
{
    public readonly int Step;
    public readonly bool Hovered;

    public MinimapZoomResult(int step, bool hovered)
    {
        Step = step;
        Hovered = hovered;
    }
}

internal static class MinimapFace
{
    private const float ScrimHeight = 20f;
    private const float LabelInset = 6f;
    private const float LabelGap = 3f;
    private const float HeaderTop = 7f;
    private const float HeaderFade = 14f;
    private const float ZoneScale = 0.62f;
    private const float CoordinateScale = 0.58f;
    private const float DotRadius = 3.2f;
    private const float DotOutline = 1.4f;
    private const float ConeLength = 17f;
    private const float ConeHalfAngle = 0.40f;
    private const float ConeTipStretch = 1.08f;
    private const float EmptyIconSize = 22f;
    private const float EmptyGap = 7f;
    private const float ZoomRadius = 9f;
    private const float ZoomGap = 5f;
    private const float ZoomInset = 7f;
    private const float ZoomIconScale = 0.84f;
    private const float ZoomRevealFloor = 0.05f;
    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Ink = new(0f, 0f, 0f, 1f);

    public static bool Draw(ImDrawListPtr drawList, Rect screen, MinimapReader reader, PhoneTheme theme, float alpha,
        float scale, float span)
    {
        drawList.AddRectFilled(screen.Min, screen.Max,
            ImGui.GetColorU32(Palette.WithAlpha(theme.ScreenBase, alpha)));
        if (reader.Texture is not { } texture || !reader.HasPlayer)
        {
            DrawEmpty(drawList, screen, reader, theme, alpha, scale);
            return false;
        }

        var halfU = span * 0.5f * reader.PixelsPerYalm / MapPixelMath.FullCanvasSize;
        var halfV = halfU * screen.Height / MathF.Max(screen.Width, 1f);
        var uv0 = new Vector2(CenterUv(reader.PlayerU, halfU) - halfU, CenterUv(reader.PlayerV, halfV) - halfV);
        var uv1 = new Vector2(uv0.X + halfU * 2f, uv0.Y + halfV * 2f);
        drawList.AddImage(texture.Handle, screen.Min, screen.Max, uv0, uv1,
            ImGui.GetColorU32(Palette.WithAlpha(White, alpha)));
        var player = new Vector2(screen.Min.X + (reader.PlayerU - uv0.X) / (uv1.X - uv0.X) * screen.Width,
            screen.Min.Y + (reader.PlayerV - uv0.Y) / (uv1.Y - uv0.Y) * screen.Height);
        DrawPlayer(drawList, player, reader.Facing, theme, alpha, scale);
        return true;
    }

    public static MinimapZoomResult DrawZoom(ImDrawListPtr drawList, Rect screen, PhoneTheme theme, float alpha,
        float scale, int zoom, bool active)
    {
        if (alpha <= ZoomRevealFloor)
        {
            return new MinimapZoomResult(0, false);
        }

        var radius = ZoomRadius * scale;
        var x = screen.Max.X - ZoomInset * scale - radius;
        var offset = ZoomGap * 0.5f * scale + radius;
        var step = 0;
        var hovered = false;
        if (ZoomButton(drawList, new Vector2(x, screen.Center.Y - offset), radius, FontAwesomeIcon.Plus, theme, alpha,
                scale, active, zoom < MinimizedShapes.MapZoomCount - 1, ref hovered))
        {
            step = 1;
        }

        if (ZoomButton(drawList, new Vector2(x, screen.Center.Y + offset), radius, FontAwesomeIcon.Minus, theme, alpha,
                scale, active, zoom > 0, ref hovered))
        {
            step = -1;
        }

        return new MinimapZoomResult(step, hovered);
    }

    private static bool ZoomButton(ImDrawListPtr drawList, Vector2 center, float radius, FontAwesomeIcon icon,
        PhoneTheme theme, float alpha, float scale, bool active, bool enabled, ref bool hovered)
    {
        var corner = new Vector2(radius, radius);
        var over = active && UiInteract.Hover(center - corner, center + corner);
        hovered |= over;
        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(Palette.WithAlpha(Ink, (over ? 0.74f : 0.52f) * alpha)), 24);
        drawList.AddCircle(center, radius,
            ImGui.GetColorU32(Palette.WithAlpha(White, (over ? 0.34f : 0.18f) * alpha)), 24, 1f * scale);
        var tint = over && enabled ? theme.Accent : White;
        var strength = enabled ? over ? 1f : 0.88f : 0.32f;
        ProgressRing.CenterIcon(drawList, center, icon, Palette.WithAlpha(tint, strength * alpha),
            radius * ZoomIconScale);
        if (over)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return enabled && UiInteract.Click(center - corner, center + corner, over);
    }

    private static void DrawEmpty(ImDrawListPtr drawList, Rect screen, MinimapReader reader, PhoneTheme theme,
        float alpha, float scale)
    {
        var label = reader.ZoneName.Length > 0 ? reader.ZoneName : Loc.T(L.Minimized.NoMap);
        var style = new TextStyle(Text(ZoneScale), FontWeight.Medium);
        var labelHeight = Typography.Measure(label, style).Y;
        var icon = EmptyIconSize * scale;
        var block = icon + EmptyGap * scale + labelHeight;
        var top = screen.Center.Y - block * 0.5f;
        ProgressRing.CenterIcon(drawList, new Vector2(screen.Center.X, top + icon * 0.5f),
            FontAwesomeIcon.MapMarkedAlt, Palette.WithAlpha(theme.TextMuted, alpha), icon);
        Marquee.DrawCenteredAuto(drawList, "minimized.map.empty", label, screen.Center.X,
            top + icon + EmptyGap * scale, screen.Width - LabelInset * 2f * scale, style,
            Palette.WithAlpha(theme.TextMuted, alpha));
    }

    private static void DrawPlayer(ImDrawListPtr drawList, Vector2 center, float facing, PhoneTheme theme, float alpha,
        float scale)
    {
        var forward = new Vector2(MathF.Sin(facing), MathF.Cos(facing));
        var length = ConeLength * scale;
        drawList.PathLineTo(center);
        drawList.PathLineTo(center + Rotate(forward, -ConeHalfAngle) * length);
        drawList.PathLineTo(center + forward * length * ConeTipStretch);
        drawList.PathLineTo(center + Rotate(forward, ConeHalfAngle) * length);
        drawList.PathFillConvex(ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, 0.34f * alpha)));
        var radius = DotRadius * scale;
        drawList.AddCircleFilled(center, radius + DotOutline * scale,
            ImGui.GetColorU32(Palette.WithAlpha(Ink, 0.55f * alpha)), 20);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(theme.Accent, alpha)), 20);
    }

    public static void DrawHeader(ImDrawListPtr drawList, Rect screen, string time, float clockScale, string zone,
        float alpha, float scale)
    {
        var clockSize = Typography.Measure(time, clockScale, FontWeight.Bold);
        var zoneStyle = new TextStyle(Text(ZoneScale), FontWeight.SemiBold);
        var zoneHeight = zone.Length > 0 ? Typography.Measure(zone, zoneStyle).Y : 0f;
        var top = screen.Min.Y + HeaderTop * scale;
        var scrimBottom = top + clockSize.Y + zoneHeight + HeaderFade * scale;
        var clear = ImGui.GetColorU32(Palette.WithAlpha(Ink, 0f));
        var dark = ImGui.GetColorU32(Palette.WithAlpha(Ink, 0.62f * alpha));
        drawList.AddRectFilledMultiColor(screen.Min, new Vector2(screen.Max.X, scrimBottom), dark, dark, clear, clear);
        Typography.Draw(drawList, new Vector2(screen.Center.X - clockSize.X * 0.5f, top), time,
            Palette.WithAlpha(White, alpha), clockScale, FontWeight.Bold);
        if (zone.Length == 0)
        {
            return;
        }

        Marquee.DrawCenteredAuto(drawList, "minimized.map.zone", zone, screen.Center.X, top + clockSize.Y,
            screen.Width - LabelInset * 2f * scale, zoneStyle, Palette.WithAlpha(White, 0.88f * alpha));
    }

    public static void DrawCoordinates(ImDrawListPtr drawList, Rect screen, string coordinates, float bottomInset,
        float alpha, float scale)
    {
        if (coordinates.Length == 0 || alpha <= 0.01f)
        {
            return;
        }

        var scrim = ScrimHeight * scale + bottomInset;
        var clear = ImGui.GetColorU32(Palette.WithAlpha(Ink, 0f));
        var dark = ImGui.GetColorU32(Palette.WithAlpha(Ink, 0.58f * alpha));
        drawList.AddRectFilledMultiColor(new Vector2(screen.Min.X, screen.Max.Y - scrim), screen.Max, clear, clear,
            dark, dark);
        var coordinateStyle = new TextStyle(Text(CoordinateScale), FontWeight.Medium);
        var size = Typography.Measure(coordinates, coordinateStyle);
        Typography.Draw(drawList, new Vector2(screen.Center.X - size.X * 0.5f,
                screen.Max.Y - bottomInset - LabelGap * scale - size.Y), coordinates,
            Palette.WithAlpha(White, 0.88f * alpha), coordinateStyle);
    }

    private static float CenterUv(float value, float half) =>
        half >= 0.5f ? 0.5f : Math.Clamp(value, half, 1f - half);

    private static Vector2 Rotate(Vector2 direction, float angle)
    {
        var sine = MathF.Sin(angle);
        var cosine = MathF.Cos(angle);
        return new Vector2(direction.X * cosine - direction.Y * sine, direction.X * sine + direction.Y * cosine);
    }

    private static float Text(float scale) => UiScale.MinimizedText(scale);
}
