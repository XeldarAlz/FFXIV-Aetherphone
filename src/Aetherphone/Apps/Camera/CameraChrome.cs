using Aetherphone.Core;
using Aetherphone.Core.Onboarding;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;

namespace Aetherphone.Apps.Camera;

internal static class CameraChrome
{
    public const float ShutterRadius = 34f;
    public const float ControlRadius = 18f;
    public const float WellHalf = 23f;
    public const float WellRounding = 8f;

    private const float ShutterRingStroke = 3.5f;
    private const float ShutterGap = 5f;
    private const float ShutterPressedScale = 0.88f;
    private const float StopFraction = 0.42f;
    private const float StopRounding = 5f;
    private const float GlyphExtent = 8f;
    private const float GridGlyphExtent = 8.5f;
    private const float ReticleGrowSeconds = 0.22f;
    private const float ReticleHoldSeconds = 0.7f;
    private const float ReticleStartHalf = 40f;
    private const float ReticleShrink = 8f;
    private const float ReticleTick = 6f;
    private const float ReticleStroke = 1.6f;
    private const float CountdownShadowDrop = 3f;
    private const float FlashPeakAlpha = 0.85f;
    private const float CornerLength = 28f;
    private const float CornerInset = 14f;
    private const float CornerStroke = 2f;
    private const float GripStroke = 3f;
    private const float NoteWidthShare = 0.78f;

    public static readonly Vector4 Yellow = new(0.98f, 0.79f, 0.20f, 1f);
    public static readonly Vector4 Dark = new(0.08f, 0.07f, 0.04f, 1f);
    public static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 Danger = new(1f, 0.27f, 0.23f, 1f);

    private static readonly Vector4 BarTint = new(0f, 0f, 0f, 0.55f);
    private static readonly Vector4 TrayTint = new(0f, 0f, 0f, 0.88f);
    private static readonly Vector4 Crop = new(0f, 0f, 0f, 0.9f);
    private static readonly Vector4 GridInk = new(1f, 1f, 1f, 0.28f);
    private static readonly Vector4 ShutterHover = new(0.88f, 0.88f, 0.90f, 1f);
    private static readonly Vector4 WellEmpty = new(0.16f, 0.17f, 0.20f, 1f);
    private static readonly Vector4 WellStroke = new(1f, 1f, 1f, 0.22f);
    private static readonly Vector4 Shadow = new(0f, 0f, 0f, 0.5f);
    private static readonly Vector4 NoteBody = new(1f, 1f, 1f, 0.75f);
    private static readonly Vector4 FrameInk = new(1f, 1f, 1f, 0.8f);
    private static readonly Vector4 YellowHover = new(1f, 0.86f, 0.38f, 1f);
    private static readonly Vector4 ActionFill = new(0.07f, 0.07f, 0.09f, 0.88f);
    private static readonly Vector4 ActionHover = new(0.15f, 0.15f, 0.17f, 0.88f);

    public static void Band(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, ImDrawFlags corners,
        bool tray) =>
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(tray ? TrayTint : BarTint), rounding, corners);

    public static void Viewfinder(ImDrawListPtr drawList, Rect viewfinder, Rect captureRect, bool gridEnabled,
        float scale)
    {
        Shade(drawList, viewfinder, captureRect, ImGui.GetColorU32(Crop));
        if (gridEnabled)
        {
            DrawThirds(drawList, captureRect, ImGui.GetColorU32(GridInk), Metrics.Stroke.Hairline * scale);
        }
    }

    public static void CropFrame(ImDrawListPtr drawList, Rect screen, Rect box, bool gridEnabled, float scale)
    {
        Shade(drawList, screen, box, ImGui.GetColorU32(BarTint));
        if (gridEnabled)
        {
            DrawThirds(drawList, box, ImGui.GetColorU32(GridInk), Metrics.Stroke.Hairline * scale);
        }

        drawList.AddRect(box.Min, box.Max, ImGui.GetColorU32(FrameInk), 0f, ImDrawFlags.None,
            Metrics.Stroke.Hairline * scale);
        var length = MathF.Min(CornerLength * scale, MathF.Min(box.Width, box.Height) * 0.5f);
        Corners(drawList, box.Min, box.Max, length, ImGui.GetColorU32(White), GripStroke * scale);
    }

    public static void CropAction(ImDrawListPtr drawList, Rect pill, string label, bool primary, bool hovered)
    {
        var fill = primary ? hovered ? YellowHover : Yellow : hovered ? ActionHover : ActionFill;
        Squircle.Fill(drawList, pill.Min, pill.Max, pill.Height * 0.5f, ImGui.GetColorU32(fill));
        Typography.DrawCentered(drawList, pill.Center, label, primary ? Dark : White,
            TextStyles.SubheadlineEmphasized);
    }

    private static void Shade(ImDrawListPtr drawList, Rect frame, Rect hole, uint color)
    {
        if (hole.Min.Y > frame.Min.Y + 0.5f)
        {
            drawList.AddRectFilled(frame.Min, new Vector2(frame.Max.X, hole.Min.Y), color);
        }

        if (hole.Max.Y < frame.Max.Y - 0.5f)
        {
            drawList.AddRectFilled(new Vector2(frame.Min.X, hole.Max.Y), frame.Max, color);
        }

        if (hole.Min.X > frame.Min.X + 0.5f)
        {
            drawList.AddRectFilled(new Vector2(frame.Min.X, hole.Min.Y), new Vector2(hole.Min.X, hole.Max.Y), color);
        }

        if (hole.Max.X < frame.Max.X - 0.5f)
        {
            drawList.AddRectFilled(new Vector2(hole.Max.X, hole.Min.Y), new Vector2(frame.Max.X, hole.Max.Y), color);
        }
    }

    public static void ScreenNote(ImDrawListPtr drawList, Rect viewfinder, string title, string body, float scale)
    {
        drawList.AddRectFilled(viewfinder.Min, viewfinder.Max, ImGui.GetColorU32(BarTint));
        var width = viewfinder.Width * NoteWidthShare;
        var titleHeight = Typography.MeasureWrappedBlock(title, TextStyles.Headline, width).Y;
        var bodyHeight = Typography.MeasureWrappedBlock(body, TextStyles.Subheadline, width).Y;
        var gap = Metrics.Space.Sm * scale;
        var top = viewfinder.Center.Y - (titleHeight + gap + bodyHeight) * 0.5f;
        Typography.DrawWrappedCentered(drawList, title, TextStyles.Headline, White,
            new Vector2(viewfinder.Center.X, top), width);
        Typography.DrawWrappedCentered(drawList, body, TextStyles.Subheadline, NoteBody,
            new Vector2(viewfinder.Center.X, top + titleHeight + gap), width);
    }

    public static void ScreenCorners(ImDrawListPtr drawList, Rect screen, float scale)
    {
        var inset = new Vector2(CornerInset * scale, CornerInset * scale);
        Corners(drawList, screen.Min + inset, screen.Max - inset, CornerLength * scale, ImGui.GetColorU32(White),
            CornerStroke * scale);
    }

    private static void Corners(ImDrawListPtr drawList, Vector2 min, Vector2 max, float length, uint color,
        float stroke)
    {
        Corner(drawList, min, new Vector2(1f, 1f), length, color, stroke);
        Corner(drawList, new Vector2(max.X, min.Y), new Vector2(-1f, 1f), length, color, stroke);
        Corner(drawList, new Vector2(min.X, max.Y), new Vector2(1f, -1f), length, color, stroke);
        Corner(drawList, max, new Vector2(-1f, -1f), length, color, stroke);
    }

    private static void Corner(ImDrawListPtr drawList, Vector2 point, Vector2 direction, float length, uint color,
        float stroke)
    {
        drawList.AddLine(point, point + new Vector2(direction.X * length, 0f), color, stroke);
        drawList.AddLine(point, point + new Vector2(0f, direction.Y * length), color, stroke);
    }

    public static void Reticle(ImDrawListPtr drawList, float age, float duration, Vector2 position, float scale)
    {
        if (age > duration)
        {
            return;
        }

        var grow = Math.Clamp(age / ReticleGrowSeconds, 0f, 1f);
        var fade = age < ReticleHoldSeconds
            ? 1f
            : MathF.Max(0f, 1f - (age - ReticleHoldSeconds) / MathF.Max(0.01f, duration - ReticleHoldSeconds));
        var half = (ReticleStartHalf - ReticleShrink * grow) * scale;
        var color = ImGui.GetColorU32(Yellow with { W = 0.9f * fade });
        var stroke = ReticleStroke * scale;
        var min = position - new Vector2(half, half);
        var max = position + new Vector2(half, half);
        drawList.AddRect(min, max, color, 2f * scale, ImDrawFlags.RoundCornersAll, stroke);
        var tick = ReticleTick * scale;
        drawList.AddLine(new Vector2(position.X, min.Y), new Vector2(position.X, min.Y + tick), color, stroke);
        drawList.AddLine(new Vector2(position.X, max.Y), new Vector2(position.X, max.Y - tick), color, stroke);
        drawList.AddLine(new Vector2(min.X, position.Y), new Vector2(min.X + tick, position.Y), color, stroke);
        drawList.AddLine(new Vector2(max.X, position.Y), new Vector2(max.X - tick, position.Y), color, stroke);
    }

    public static void Countdown(ImDrawListPtr drawList, Vector2 center, string digits, float scale)
    {
        var shadowCenter = center + new Vector2(0f, CountdownShadowDrop * scale);
        Typography.DrawCentered(drawList, shadowCenter, digits, Shadow, TextStyles.Hero);
        Typography.DrawCentered(drawList, center, digits, White, TextStyles.Hero);
    }

    public static bool Shutter(ImDrawListPtr drawList, Vector2 center, bool counting, float scale)
    {
        var outer = ShutterRadius * scale;
        var extent = new Vector2(outer, outer);
        UiAnchors.Report("camera.shutter", new Rect(center - extent, center + extent));
        var hovered = UiInteract.Hover(center - extent, center + extent);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(ImGui.GetID("camera.shutter"), down, ShutterPressedScale);
        drawList.AddCircle(center, outer - ShutterRingStroke * scale * 0.5f, ImGui.GetColorU32(White), 64,
            ShutterRingStroke * scale);
        var inner = (outer - (ShutterRingStroke + ShutterGap) * scale) * press;
        if (counting)
        {
            var half = inner * StopFraction;
            Squircle.Fill(drawList, center - new Vector2(half, half), center + new Vector2(half, half),
                StopRounding * scale, ImGui.GetColorU32(Danger));
        }
        else
        {
            drawList.AddCircleFilled(center, inner, ImGui.GetColorU32(hovered ? ShutterHover : White), 64);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(center - extent, center + extent, hovered);
    }

    public static Rect WellRect(Vector2 center, float scale)
    {
        var half = new Vector2(WellHalf * scale, WellHalf * scale);
        return new Rect(center - half, center + half);
    }

    public static bool Well(ImDrawListPtr drawList, Rect well, IDalamudTextureWrap? texture, string tooltip,
        float scale)
    {
        UiAnchors.Report("camera.lastShot", well);
        var hovered = texture is not null && UiInteract.Hover(well.Min, well.Max);
        var down = hovered && ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var press = PressFx.Scale(ImGui.GetID("camera.well"), down, PressFx.ControlPressedScale);
        var half = well.Size * 0.5f * press;
        var min = well.Center - half;
        var max = well.Center + half;
        var rounding = WellRounding * scale;
        if (texture is null)
        {
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(WellEmpty));
        }
        else
        {
            var (uv0, uv1) = ImageFit.Cover(texture.Width, texture.Height, max.X - min.X, max.Y - min.Y);
            Squircle.FillImage(drawList, min, max, rounding, texture.Handle, 0xFFFFFFFFu, uv0, uv1);
        }

        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(WellStroke), Metrics.Stroke.Hairline * scale);
        if (!hovered)
        {
            return false;
        }

        HoverTooltip.Show(well, tooltip, HoverLabelSide.Above);
        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        return UiInteract.Click(well.Min, well.Max, hovered);
    }

    public static void Flight(ImDrawListPtr drawList, IDalamudTextureWrap texture, Rect from, Rect to, float progress,
        float scale)
    {
        var eased = Math.Clamp(progress, 0f, 1f);
        var min = Vector2.Lerp(from.Min, to.Min, eased);
        var max = Vector2.Lerp(from.Max, to.Max, eased);
        var rounding = WellRounding * scale * eased;
        var (uv0, uv1) = ImageFit.Cover(texture.Width, texture.Height, max.X - min.X, max.Y - min.Y);
        Squircle.FillImage(drawList, min, max, rounding, texture.Handle, 0xFFFFFFFFu, uv0, uv1);
    }

    public static void Flash(ImDrawListPtr drawList, Rect screen, float age, float duration, float rounding)
    {
        if (age > duration)
        {
            return;
        }

        var alpha = FlashPeakAlpha * (1f - age / duration);
        drawList.AddRectFilled(screen.Min, screen.Max, ImGui.GetColorU32(White with { W = alpha }), rounding);
    }

    public static void BoltGlyph(ImDrawListPtr drawList, Vector2 center, float grow, bool enabled, bool active,
        float scale)
    {
        var extent = GlyphExtent * scale * grow;
        var color = ImGui.GetColorU32(active ? Dark : White);
        Span<Vector2> bolt = stackalloc Vector2[6]
        {
            new Vector2(center.X + extent * 0.35f, center.Y - extent),
            new Vector2(center.X - extent * 0.55f, center.Y + extent * 0.2f),
            new Vector2(center.X - extent * 0.02f, center.Y + extent * 0.2f),
            new Vector2(center.X - extent * 0.35f, center.Y + extent),
            new Vector2(center.X + extent * 0.55f, center.Y - extent * 0.2f),
            new Vector2(center.X + extent * 0.02f, center.Y - extent * 0.2f),
        };
        drawList.PathClear();
        for (var index = 0; index < bolt.Length; index++)
        {
            drawList.PathLineTo(bolt[index]);
        }

        if (enabled)
        {
            drawList.PathFillConvex(color);
            return;
        }

        drawList.PathStroke(color, ImDrawFlags.Closed, 1.3f * scale);
        drawList.AddLine(center + new Vector2(-extent, -extent), center + new Vector2(extent, extent), color,
            1.4f * scale);
    }

    public static void HudGlyph(ImDrawListPtr drawList, Vector2 center, float grow, bool uiPresent, bool active,
        float scale)
    {
        var extent = GlyphExtent * scale * grow;
        var color = ImGui.GetColorU32(active ? Dark : White);
        var frameHalf = new Vector2(extent, extent * 0.76f);
        drawList.AddRect(center - frameHalf, center + frameHalf, color, extent * 0.24f, ImDrawFlags.RoundCornersAll,
            MathF.Max(1f, extent * 0.16f));
        if (!uiPresent)
        {
            return;
        }

        var barSize = new Vector2(extent * 0.53f, extent * 0.19f);
        var inset = extent * 0.31f;
        var topLeft = center - frameHalf + new Vector2(inset, inset);
        drawList.AddRectFilled(topLeft, topLeft + barSize, color);
        var bottomRight = center + frameHalf - new Vector2(inset, inset) - barSize;
        drawList.AddRectFilled(bottomRight, bottomRight + barSize, color);
    }

    public static void RotateGlyph(ImDrawListPtr drawList, Vector2 center, float grow, bool landscape, bool active,
        float scale)
    {
        var unit = scale * grow;
        var color = ImGui.GetColorU32(active ? Dark : White);
        var phoneHalf = landscape ? new Vector2(6.5f, 4f) * unit : new Vector2(4f, 6.5f) * unit;
        drawList.AddRect(center - phoneHalf, center + phoneHalf, color, 2f * unit, ImDrawFlags.RoundCornersAll,
            1.4f * unit);
        var radius = 11f * unit;
        const float startAngle = -2.35f;
        const float endAngle = -0.75f;
        drawList.PathClear();
        drawList.PathArcTo(center, radius, startAngle, endAngle, 10);
        drawList.PathStroke(color, ImDrawFlags.None, 1.4f * unit);
        var normal = new Vector2(MathF.Cos(endAngle), MathF.Sin(endAngle));
        var tangent = new Vector2(-normal.Y, normal.X);
        var end = center + radius * normal;
        drawList.AddTriangleFilled(end + tangent * 4f * unit, end + normal * 2.6f * unit, end - normal * 2.6f * unit,
            color);
    }

    public static void GridGlyph(ImDrawListPtr drawList, Vector2 center, float grow, bool active, float scale)
    {
        var extent = GridGlyphExtent * scale * grow;
        var area = new Rect(center - new Vector2(extent, extent), center + new Vector2(extent, extent));
        var color = ImGui.GetColorU32(active ? Dark : White);
        DrawThirds(drawList, area, color, 1.3f * scale);
        drawList.AddRect(area.Min, area.Max, color, 2f * scale, ImDrawFlags.RoundCornersAll, 1.3f * scale);
    }

    public static void TimerGlyph(ImDrawListPtr drawList, Vector2 center, float grow, string seconds, float scale)
    {
        if (seconds.Length == 0)
        {
            PhoneIcon.Draw(drawList, center, PhoneIcons.Clock, White, GlyphExtent * 2.3f * scale * grow);
            return;
        }

        Typography.DrawCentered(drawList, center, seconds, Dark, TextStyles.FootnoteEmphasized);
    }

    private static void DrawThirds(ImDrawListPtr drawList, Rect area, uint color, float thickness)
    {
        var thirdX = area.Width / 3f;
        var thirdY = area.Height / 3f;
        for (var step = 1; step <= 2; step++)
        {
            var x = area.Min.X + thirdX * step;
            drawList.AddLine(new Vector2(x, area.Min.Y), new Vector2(x, area.Max.Y), color, thickness);
            var y = area.Min.Y + thirdY * step;
            drawList.AddLine(new Vector2(area.Min.X, y), new Vector2(area.Max.X, y), color, thickness);
        }
    }
}
