using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.Camera;

/// <summary>
/// Pure rendering for the Camera surface. Owns the viewfinder chrome (top bar, viewfinder overlays,
/// tray controls, flash). State and input decisions stay in <see cref="CameraApp"/>; each interactive
/// method returns whether it was clicked so the app can mutate its own state.
/// </summary>
internal enum CameraBarAction
{
    None,
    ToggleFlash,
    ToggleLandscape,
}

internal static class CameraChrome
{
    public const float ShutterRadius = 34f;

    private const float CarouselGap = 26f;

    private static readonly Vector4 SelectedMode = new(0.98f, 0.79f, 0.20f, 1f);
    private static readonly Vector4 ShutterRing = new(0.98f, 0.98f, 0.98f, 1f);
    private static readonly Vector4 BarTint = new(0f, 0f, 0f, 0.42f);
    private static readonly Vector4 TrayTint = new(0f, 0f, 0f, 0.88f);

    public static CameraBarAction TopBar(Rect screen, float topBarHeight, bool flashEnabled, bool landscapeEnabled,
        float scale, float rounding)
    {
        var dl = ImGui.GetWindowDrawList();
        var barMax = new Vector2(screen.Max.X, screen.Min.Y + topBarHeight * scale);
        dl.AddRectFilled(screen.Min, barMax, ImGui.GetColorU32(BarTint), rounding, ImDrawFlags.RoundCornersTop);
        var rowCenterY = barMax.Y + Metrics.Space.Lg * 3f * scale;
        if (FlashChip(new Vector2(screen.Min.X + 34f * scale, rowCenterY), flashEnabled, scale))
        {
            return CameraBarAction.ToggleFlash;
        }

        return RotateChip(new Vector2(screen.Max.X - 34f * scale, rowCenterY), landscapeEnabled, scale)
            ? CameraBarAction.ToggleLandscape
            : CameraBarAction.None;
    }

    public static CameraBarAction SideBar(Rect screen, float barWidth, bool flashEnabled, bool landscapeEnabled,
        float scale, float rounding)
    {
        var dl = ImGui.GetWindowDrawList();
        var barMax = new Vector2(screen.Min.X + barWidth * scale, screen.Max.Y);
        dl.AddRectFilled(screen.Min, barMax, ImGui.GetColorU32(BarTint), rounding, ImDrawFlags.RoundCornersLeft);
        var columnCenterX = screen.Min.X + barWidth * 0.5f * scale;
        var flashCenterY = screen.Min.Y + 64f * scale;
        if (FlashChip(new Vector2(columnCenterX, flashCenterY), flashEnabled, scale))
        {
            return CameraBarAction.ToggleFlash;
        }

        return RotateChip(new Vector2(columnCenterX, flashCenterY + 52f * scale), landscapeEnabled, scale)
            ? CameraBarAction.ToggleLandscape
            : CameraBarAction.None;
    }

    private static bool FlashChip(Vector2 center, bool flashEnabled, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        var half = new Vector2(16f * scale, 16f * scale);
        var min = center - half;
        var max = center + half;
        UiAnchors.Report("camera.flash", new Rect(min, max));
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        var rounding = Metrics.Radius.Field * scale;
        var fill = flashEnabled
            ? SelectedMode
            : new Vector4(0f, 0f, 0f, hovered ? 0.44f : 0.30f);
        dl.AddRectFilled(min, max, ImGui.GetColorU32(fill), rounding);
        var boltColor = flashEnabled
            ? new Vector4(0.08f, 0.07f, 0.04f, 1f)
            : new Vector4(0.95f, 0.95f, 0.97f, 0.95f);
        DrawBolt(dl, center, 8.5f * scale, ImGui.GetColorU32(boltColor));
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static bool RotateChip(Vector2 center, bool landscapeEnabled, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        var half = new Vector2(16f * scale, 16f * scale);
        var min = center - half;
        var max = center + half;
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        var rounding = Metrics.Radius.Field * scale;
        var fill = landscapeEnabled
            ? SelectedMode
            : new Vector4(0f, 0f, 0f, hovered ? 0.44f : 0.30f);
        dl.AddRectFilled(min, max, ImGui.GetColorU32(fill), rounding);
        var iconTint = landscapeEnabled
            ? new Vector4(0.08f, 0.07f, 0.04f, 1f)
            : new Vector4(0.95f, 0.95f, 0.97f, 0.95f);
        DrawRotateGlyph(dl, center, scale, ImGui.GetColorU32(iconTint), landscapeEnabled);
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static void DrawRotateGlyph(ImDrawListPtr dl, Vector2 center, float scale, uint color,
        bool landscapeEnabled)
    {
        var phoneHalf = landscapeEnabled ? new Vector2(6.5f, 4f) * scale : new Vector2(4f, 6.5f) * scale;
        dl.AddRect(center - phoneHalf, center + phoneHalf, color, 2f * scale, ImDrawFlags.RoundCornersAll,
            1.4f * scale);
        var radius = 11f * scale;
        const float startAngle = -2.35f;
        const float endAngle = -0.75f;
        dl.PathClear();
        dl.PathArcTo(center, radius, startAngle, endAngle, 10);
        dl.PathStroke(color, ImDrawFlags.None, 1.4f * scale);
        var normal = new Vector2(MathF.Cos(endAngle), MathF.Sin(endAngle));
        var tangent = new Vector2(-normal.Y, normal.X);
        var end = center + radius * normal;
        dl.AddTriangleFilled(end + tangent * 4f * scale, end + normal * 2.6f * scale, end - normal * 2.6f * scale,
            color);
    }

    private static void DrawBolt(ImDrawListPtr dl, Vector2 center, float extent, uint color)
    {
        Span<Vector2> bolt = stackalloc Vector2[6]
        {
            new Vector2(center.X + extent * 0.35f, center.Y - extent),
            new Vector2(center.X - extent * 0.55f, center.Y + extent * 0.2f),
            new Vector2(center.X - extent * 0.02f, center.Y + extent * 0.2f),
            new Vector2(center.X - extent * 0.35f, center.Y + extent),
            new Vector2(center.X + extent * 0.55f, center.Y - extent * 0.2f),
            new Vector2(center.X + extent * 0.02f, center.Y - extent * 0.2f),
        };
        dl.PathClear();
        for (var index = 0; index < bolt.Length; index++)
        {
            dl.PathLineTo(bolt[index]);
        }

        dl.PathFillConvex(color);
    }

    public static void Viewfinder(Rect viewfinder, Rect captureRect, bool gridEnabled, float reticleAge,
        float reticleDuration, Vector2 reticlePos, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        var crop = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.9f));
        if (captureRect.Min.Y > viewfinder.Min.Y + 0.5f)
        {
            dl.AddRectFilled(viewfinder.Min, new Vector2(viewfinder.Max.X, captureRect.Min.Y), crop);
            dl.AddRectFilled(new Vector2(viewfinder.Min.X, captureRect.Max.Y), viewfinder.Max, crop);
        }

        if (captureRect.Min.X > viewfinder.Min.X + 0.5f)
        {
            dl.AddRectFilled(viewfinder.Min, new Vector2(captureRect.Min.X, viewfinder.Max.Y), crop);
            dl.AddRectFilled(new Vector2(captureRect.Max.X, viewfinder.Min.Y), viewfinder.Max, crop);
        }

        if (gridEnabled)
        {
            DrawGrid(dl, captureRect, scale);
        }

        DrawReticle(dl, reticleAge, reticleDuration, reticlePos, scale);
    }

    private static void DrawGrid(ImDrawListPtr dl, Rect area, float scale)
    {
        var line = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.28f));
        var thickness = Metrics.Stroke.Hairline * scale;
        var thirdX = area.Width / 3f;
        var thirdY = area.Height / 3f;
        for (var step = 1; step <= 2; step++)
        {
            var x = area.Min.X + thirdX * step;
            dl.AddLine(new Vector2(x, area.Min.Y), new Vector2(x, area.Max.Y), line, thickness);
            var y = area.Min.Y + thirdY * step;
            dl.AddLine(new Vector2(area.Min.X, y), new Vector2(area.Max.X, y), line, thickness);
        }
    }

    private static void DrawReticle(ImDrawListPtr dl, float reticleAge, float reticleDuration, Vector2 reticlePos,
        float scale)
    {
        if (reticleAge > reticleDuration)
        {
            return;
        }

        var grow = Math.Clamp(reticleAge / 0.22f, 0f, 1f);
        var fade = reticleAge < 0.7f ? 1f : MathF.Max(0f, 1f - (reticleAge - 0.7f) / (reticleDuration - 0.7f));
        var half = (40f - 8f * grow) * scale;
        var color = ImGui.GetColorU32(new Vector4(0.98f, 0.79f, 0.20f, 0.9f * fade));
        var min = reticlePos - new Vector2(half, half);
        var max = reticlePos + new Vector2(half, half);
        dl.AddRect(min, max, color, 2f * scale, ImDrawFlags.RoundCornersAll, 1.6f * scale);
        var tick = 6f * scale;
        dl.AddLine(new Vector2(reticlePos.X, min.Y), new Vector2(reticlePos.X, min.Y + tick), color, 1.6f * scale);
        dl.AddLine(new Vector2(reticlePos.X, max.Y), new Vector2(reticlePos.X, max.Y - tick), color, 1.6f * scale);
        dl.AddLine(new Vector2(min.X, reticlePos.Y), new Vector2(min.X + tick, reticlePos.Y), color, 1.6f * scale);
        dl.AddLine(new Vector2(max.X, reticlePos.Y), new Vector2(max.X - tick, reticlePos.Y), color, 1.6f * scale);
    }

    public static void TrayBackground(Rect screen, float trayTop, float rounding)
    {
        ImGui.GetWindowDrawList()
            .AddRectFilled(new Vector2(screen.Min.X, trayTop), screen.Max, ImGui.GetColorU32(TrayTint), rounding,
                ImDrawFlags.RoundCornersBottom);
    }

    public static void SideTrayBackground(Rect screen, float trayLeft, float rounding)
    {
        ImGui.GetWindowDrawList()
            .AddRectFilled(new Vector2(trayLeft, screen.Min.Y), screen.Max, ImGui.GetColorU32(TrayTint), rounding,
                ImDrawFlags.RoundCornersRight);
    }

    public static int ModeCarousel(Rect screen, float rowCenterY, LocString[] modes, int modeIndex, float scale)
    {
        var gap = CarouselGap * scale;
        Span<float> widths = stackalloc float[modes.Length];
        var total = 0f;
        for (var index = 0; index < modes.Length; index++)
        {
            var modeScale = index == modeIndex ? 0.78f : 0.72f;
            widths[index] = Typography.Measure(Loc.T(modes[index]), modeScale).X;
            total += widths[index];
            if (index > 0)
            {
                total += gap;
            }
        }

        var cursorX = screen.Center.X - total * 0.5f;
        UiAnchors.Report("camera.modes",
            new Rect(new Vector2(cursorX - gap * 0.4f, rowCenterY - 14f * scale),
                new Vector2(cursorX + total + gap * 0.4f, rowCenterY + 14f * scale)));
        var result = modeIndex;
        for (var index = 0; index < modes.Length; index++)
        {
            var selected = index == modeIndex;
            var modeScale = selected ? 0.78f : 0.72f;
            var color = selected ? SelectedMode : new Vector4(0.82f, 0.82f, 0.85f, 0.75f);
            var labelCenter = new Vector2(cursorX + widths[index] * 0.5f, rowCenterY);
            Typography.DrawCentered(labelCenter, Loc.T(modes[index]), color, modeScale);
            var hitMin = new Vector2(cursorX - gap * 0.4f, rowCenterY - 14f * scale);
            var hitMax = new Vector2(cursorX + widths[index] + gap * 0.4f, rowCenterY + 14f * scale);
            if (!selected && ImGui.IsMouseHoveringRect(hitMin, hitMax))
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    result = index;
                }
            }

            cursorX += widths[index] + gap;
        }

        return result;
    }

    public static int ModeColumn(float columnCenterX, float topLimit, float bottomLimit, LocString[] modes,
        int modeIndex, float scale)
    {
        var span = MathF.Max(bottomLimit - topLimit, 0f);
        var rowHeight = modes.Length > 0 ? MathF.Min(30f * scale, span / modes.Length) : span;
        var total = rowHeight * modes.Length;
        var halfWidth = 44f * scale;
        var cursorY = (topLimit + bottomLimit - total) * 0.5f + rowHeight * 0.5f;
        UiAnchors.Report("camera.modes",
            new Rect(new Vector2(columnCenterX - halfWidth, cursorY - rowHeight * 0.5f),
                new Vector2(columnCenterX + halfWidth, cursorY - rowHeight * 0.5f + total)));
        var result = modeIndex;
        for (var index = 0; index < modes.Length; index++)
        {
            var selected = index == modeIndex;
            var modeScale = selected ? 0.78f : 0.72f;
            var color = selected ? SelectedMode : new Vector4(0.82f, 0.82f, 0.85f, 0.75f);
            Typography.DrawCentered(new Vector2(columnCenterX, cursorY), Loc.T(modes[index]), color, modeScale);
            var hitMin = new Vector2(columnCenterX - halfWidth, cursorY - rowHeight * 0.5f);
            var hitMax = new Vector2(columnCenterX + halfWidth, cursorY + rowHeight * 0.5f);
            if (!selected && ImGui.IsMouseHoveringRect(hitMin, hitMax))
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    result = index;
                }
            }

            cursorY += rowHeight;
        }

        return result;
    }

    public static bool Shutter(Vector2 center, float shutterPress, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        var outerRadius = ShutterRadius * scale;
        UiAnchors.Report("camera.shutter",
            new Rect(center - new Vector2(outerRadius, outerRadius), center + new Vector2(outerRadius, outerRadius)));
        var hovered = ImGui.IsMouseHoveringRect(center - new Vector2(outerRadius, outerRadius),
            center + new Vector2(outerRadius, outerRadius));
        dl.AddCircle(center, outerRadius, ImGui.GetColorU32(ShutterRing), 48, 3f * scale);
        var innerRadius = (outerRadius - Metrics.Space.Xs * scale) * (1f - 0.16f * shutterPress);
        var innerTint = hovered ? new Vector4(0.86f, 0.86f, 0.88f, 1f) : ShutterRing;
        dl.AddCircleFilled(center, innerRadius, ImGui.GetColorU32(innerTint), 48);
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public static bool ThumbnailWell(Vector2 center, IDalamudTextureWrap? lastShot, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        var half = 22f * scale;
        var min = center - new Vector2(half, half);
        var max = center + new Vector2(half, half);
        var hovered = ImGui.IsMouseHoveringRect(min, max);
        var rounding = Metrics.Radius.Sm * scale;
        if (lastShot is { } shot)
        {
            dl.AddImageRounded(shot.Handle, min, max, Vector2.Zero, Vector2.One, 0xFFFFFFFFu, rounding,
                ImDrawFlags.RoundCornersAll);
        }
        else
        {
            dl.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0.18f, 0.19f, 0.23f, 1f)), rounding);
            dl.AddRectFilled(min, new Vector2(max.X, center.Y), ImGui.GetColorU32(new Vector4(0.30f, 0.33f, 0.40f, 1f)),
                rounding, ImDrawFlags.RoundCornersTop);
        }

        dl.AddRect(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.18f)), rounding, ImDrawFlags.RoundCornersAll,
            Metrics.Stroke.Hairline * scale);
        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public static bool GridToggle(Vector2 center, bool gridEnabled, float scale)
    {
        var dl = ImGui.GetWindowDrawList();
        var radius = 19f * scale;
        var hovered =
            ImGui.IsMouseHoveringRect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        if (gridEnabled || hovered)
        {
            var bg = gridEnabled ? new Vector4(1f, 1f, 1f, 0.16f) : new Vector4(1f, 1f, 1f, 0.08f);
            dl.AddCircleFilled(center, radius, ImGui.GetColorU32(bg), 28);
        }

        var color = ImGui.GetColorU32(gridEnabled ? SelectedMode : new Vector4(0.9f, 0.9f, 0.92f, 0.85f));
        var extent = 9f * scale;
        var third = extent * 2f / 3f;
        for (var step = 1; step <= 2; step++)
        {
            var offset = -extent + third * step;
            dl.AddLine(new Vector2(center.X + offset, center.Y - extent),
                new Vector2(center.X + offset, center.Y + extent), color, 1.3f * scale);
            dl.AddLine(new Vector2(center.X - extent, center.Y + offset),
                new Vector2(center.X + extent, center.Y + offset), color, 1.3f * scale);
        }

        if (!hovered)
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public static void Flash(Rect screen, float flashAge, float flashDuration, float rounding)
    {
        if (flashAge > flashDuration)
        {
            return;
        }

        var alpha = 0.85f * (1f - flashAge / flashDuration);
        ImGui.GetWindowDrawList()
            .AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, alpha)), rounding);
    }
}
