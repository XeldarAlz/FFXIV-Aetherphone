using Aetherphone.Core;
using Aetherphone.Core.Home;
using Aetherphone.Core.Localization;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Widgets;

internal enum WidgetRowLead : byte
{
    None,
    Thumbnail,
    Avatar,
}

internal enum WidgetGlyph : byte
{
    None,
    Icon,
    App,
}

internal static class WidgetChrome
{
    private const float RedactedTitleUnits = 10f;
    private const float RedactedBodyFraction = 0.8f;
    private const float RedactedLeadUnits = 32f;
    private const float MessageGlyphUnits = 22f;
    private const float MessageGapUnits = 6f;
    private const int MessageTitleLines = 3;
    private const int MessageDetailLines = 2;
    private const float RingTop = -MathF.PI / 2f;
    private const int RingTrackSegments = 72;
    private const float RingSegmentsPerRadian = 11f;
    private const float RingHeadShadowAlpha = 0.38f;
    private const float RingHeadOverlayRadians = 1.4f;
    private const float RingTrackAlpha = 0.2f;
    private const float RingThicknessFactor = 0.14f;
    private const float RingThicknessMinimum = 3f;
    private const float PillPaddingX = 6f;
    private const float PillPaddingY = 5f;
    private const float CheckStrokeUnits = 1.6f;
    private const float CheckMarkUnits = 1.8f;
    private const float OwnBackgroundDim = 0.35f;
    private const float OwnBackgroundVeil = 0.08f;
    private const float TintWash = 0.12f;
    private const float ClearVeil = 0.08f;
    private const float ShimmerSeconds = 1.4f;
    private const float ShimmerSpread = 0.004f;
    private const float ShimmerFloor = 0.65f;
    private static readonly Vector4 LightCard = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 DarkCard = new(28f / 255f, 28f / 255f, 30f / 255f, 1f);
    private static readonly Vector4 NearBlackCard = new(20f / 255f, 20f / 255f, 22f / 255f, 1f);

    public static float Radius(float scale) => WidgetMetrics.ContainerRadius(scale);

    public static void Container(in WidgetContext context)
    {
        var drawList = context.DrawList;
        var bounds = context.Bounds;
        var scale = context.Scale;
        var opacity = context.Opacity;
        var radius = Radius(scale);
        switch (context.Mode)
        {
            case WidgetMode.Tinted:
                Material.LiquidGlass(drawList, bounds.Min, bounds.Max, radius, scale, GlassTone.Dark, 0f, opacity);
                Squircle.Fill(drawList, bounds.Min, bounds.Max, radius,
                    ImGui.GetColorU32(context.Tint with { W = TintWash * opacity }));
                return;
            case WidgetMode.Clear:
                Material.LiquidGlass(drawList, bounds.Min, bounds.Max, radius, scale, GlassTone.Light,
                    AppIconCache.GlassBrightness, opacity);
                Material.Veil(drawList, bounds.Min, bounds.Max, ClearVeil * opacity, radius);
                return;
            case WidgetMode.Dark:
                Opaque(drawList, bounds, radius, scale, NearBlackCard, opacity);
                return;
            default:
                Opaque(drawList, bounds, radius, scale, WidgetInk.IsLightTheme(context.Theme) ? LightCard : DarkCard,
                    opacity);
                return;
        }
    }

    public static void Container(in WidgetContext context, Vector4 top, Vector4 bottom)
    {
        if (context.Mode is WidgetMode.Tinted or WidgetMode.Clear)
        {
            Container(context);
            return;
        }

        var drawList = context.DrawList;
        var bounds = context.Bounds;
        var scale = context.Scale;
        var opacity = context.Opacity;
        var radius = Radius(scale);
        if (context.Mode == WidgetMode.Dark)
        {
            top = Dimmed(top);
            bottom = Dimmed(bottom);
        }

        Squircle.FillVerticalGradient(drawList, bounds.Min, bounds.Max, radius,
            ImGui.GetColorU32(top with { W = top.W * opacity }),
            ImGui.GetColorU32(bottom with { W = bottom.W * opacity }));
        Material.Veil(drawList, bounds.Min, bounds.Max, OwnBackgroundVeil * opacity, radius);
        Material.EdgeSquircle(drawList, bounds.Min, bounds.Max, radius, scale, opacity);
    }

    public static void Edge(in WidgetContext context) =>
        Material.EdgeSquircle(context.DrawList, context.Bounds.Min, context.Bounds.Max, Radius(context.Scale),
            context.Scale, context.Opacity);

    public static float Header(in WidgetContext context, in WidgetInk ink, string appId, LocString label,
        Vector4 accent) =>
        Header(context, ink, appId, Loc.T(label), accent, string.Empty, default);

    public static float Header(in WidgetContext context, in WidgetInk ink, string appId, string label,
        Vector4 accent) =>
        Header(context, ink, appId, label, accent, string.Empty, default);

    public static float Header(in WidgetContext context, in WidgetInk ink, string appId, string label,
        Vector4 accent, string trailing, Vector4 trailingColor)
    {
        var content = WidgetMetrics.Content(context);
        var scale = context.Scale;
        var drawList = context.DrawList;
        var glyphSize = WidgetMetrics.GlyphSmall * scale;
        var tint = ink.Accent(accent);
        var eyebrowHeight = WidgetText.EyebrowHeight();
        var rowHeight = MathF.Max(glyphSize, eyebrowHeight);
        var centerY = content.Min.Y + rowHeight * 0.5f;
        var left = content.Min.X;
        var right = content.Max.X;
        if (trailing.Length > 0)
        {
            var trailingSize = Typography.Measure(trailing, WidgetType.Caption);
            Typography.Draw(drawList, new Vector2(right - trailingSize.X, centerY - trailingSize.Y * 0.5f), trailing,
                trailingColor, WidgetType.Caption);
            right -= trailingSize.X + WidgetMetrics.Gutter * scale;
        }

        if (appId.Length > 0)
        {
            AppIconTile.TryDrawGlyph(drawList, appId, new Vector2(left + glyphSize * 0.5f, centerY), glyphSize, tint);
            left += glyphSize + WidgetMetrics.Gutter * 0.5f * scale;
        }

        if (label.Length > 0)
        {
            WidgetText.EyebrowFit(drawList, new Vector2(left, centerY - eyebrowHeight * 0.5f), label,
                MathF.Max(1f, right - left), tint, scale);
        }

        return content.Min.Y + rowHeight;
    }

    public static void Redacted(ImDrawListPtr drawList, Rect rect, in WidgetInk ink) =>
        Redacted(drawList, rect, ink, rect.Height * 0.5f);

    public static void Redacted(ImDrawListPtr drawList, Rect rect, in WidgetInk ink, float radius)
    {
        if (ink.Opacity <= 0f || rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        var phase = (float)(ImGui.GetTime() / ShimmerSeconds) - rect.Min.X * ShimmerSpread;
        var wave = 0.5f + 0.5f * MathF.Cos(phase * MathF.Tau);
        var alpha = ink.Fill.W * (ShimmerFloor + (1f - ShimmerFloor) * wave);
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(ink.Fill with { W = alpha }));
    }

    public static void RedactedRows(in WidgetContext context, in WidgetInk ink, Rect area, int rows,
        WidgetRowLead lead)
    {
        if (rows <= 0 || area.Height <= 0f)
        {
            return;
        }

        var drawList = context.DrawList;
        var scale = context.Scale;
        var rowHeight = area.Height / rows;
        var titleHeight = MathF.Min(RedactedTitleUnits * scale, rowHeight * 0.3f);
        var bodyHeight = titleHeight * RedactedBodyFraction;
        var gap = WidgetMetrics.RowGap * 1.5f * scale;
        for (var rowIndex = 0; rowIndex < rows; rowIndex++)
        {
            var middle = area.Min.Y + rowHeight * (rowIndex + 0.5f);
            var left = area.Min.X;
            if (lead != WidgetRowLead.None)
            {
                var side = MathF.Min(RedactedLeadUnits * scale, rowHeight - 4f * scale);
                var leadRect = new Rect(new Vector2(left, middle - side * 0.5f),
                    new Vector2(left + side, middle + side * 0.5f));
                Redacted(drawList, leadRect, ink, lead == WidgetRowLead.Avatar ? side * 0.5f : side * 0.3f);
                left += side + WidgetMetrics.Gutter * scale;
            }

            var width = MathF.Max(1f, area.Max.X - left);
            var titleWidth = width * (rowIndex % 2 == 0 ? 0.62f : 0.48f);
            var top = middle - (titleHeight + gap + bodyHeight) * 0.5f;
            Redacted(drawList, new Rect(new Vector2(left, top), new Vector2(left + titleWidth, top + titleHeight)), ink);
            var bodyTop = top + titleHeight + gap;
            Redacted(drawList,
                new Rect(new Vector2(left, bodyTop), new Vector2(left + width * 0.72f, bodyTop + bodyHeight)), ink);
        }
    }

    public static void Message(in WidgetContext context, in WidgetInk ink, Rect area, string title, string detail) =>
        Message(context, area, ink.Primary, ink.Secondary, default, WidgetGlyph.None, default, string.Empty, title,
            detail);

    public static void Message(in WidgetContext context, Rect area, Vector4 primary, Vector4 secondary, string title,
        string detail) =>
        Message(context, area, primary, secondary, default, WidgetGlyph.None, default, string.Empty, title, detail);

    public static void Message(in WidgetContext context, in WidgetInk ink, Rect area, FontAwesomeIcon icon,
        Vector4 accent, string title, string detail) =>
        Message(context, area, ink.Primary, ink.Secondary, accent.W > 0f ? ink.Accent(accent) : ink.Secondary,
            WidgetGlyph.Icon, icon, string.Empty, title, detail);

    public static void Message(in WidgetContext context, in WidgetInk ink, Rect area, string appId, string title,
        string detail) =>
        Message(context, area, ink.Primary, ink.Secondary, ink.Secondary, WidgetGlyph.App, default, appId, title,
            detail);

    public static void Separator(in WidgetContext context, in WidgetInk ink, float left, float right, float y) =>
        context.DrawList.AddLine(new Vector2(left, y), new Vector2(right, y), ImGui.GetColorU32(ink.Separator),
            MathF.Max(1f, 0.5f * context.Scale));

    public static float RingThickness(float radius, float scale) =>
        MathF.Max(RingThicknessMinimum * scale, radius * RingThicknessFactor);

    public static void Ring(ImDrawListPtr drawList, in WidgetInk ink, Vector2 center, float radius, float thickness,
        float fraction, Vector4 accent)
    {
        var color = ink.Accent(accent);
        Ring(drawList, center, radius, thickness, Math.Clamp(fraction, 0f, 1f), color,
            color with { W = color.W * RingTrackAlpha });
    }

    public static void Ring(ImDrawListPtr drawList, Vector2 center, float radius, float thickness, float fraction,
        Vector4 color, Vector4 track)
    {
        drawList.AddCircle(center, radius, ImGui.GetColorU32(track), RingTrackSegments, thickness);
        if (fraction <= 0.002f)
        {
            return;
        }

        var packed = ImGui.GetColorU32(color);
        var lap = MathF.Min(fraction, 1f);
        RingArc(drawList, center, radius, thickness, RingTop, RingTop + lap * MathF.Tau, packed);
        if (fraction <= 1f)
        {
            return;
        }

        var overflow = fraction - 1f;
        overflow -= MathF.Floor(overflow);
        var head = RingTop + overflow * MathF.Tau;
        var cap = thickness * 0.5f;
        var direction = new Vector2(MathF.Cos(head), MathF.Sin(head));
        var tangent = new Vector2(-direction.Y, direction.X);
        drawList.AddCircleFilled(center + direction * radius + tangent * cap * 0.35f, cap * 1.12f,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, RingHeadShadowAlpha * color.W)), 24);
        RingArc(drawList, center, radius, thickness,
            head - MathF.Min(RingHeadOverlayRadians, overflow * MathF.Tau + 0.01f), head, packed);
    }

    public static void Bar(ImDrawListPtr drawList, Rect rect, float fraction, Vector4 track, Vector4 fill)
    {
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        var radius = rect.Height * 0.5f;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(track));
        var clamped = float.IsNaN(fraction) ? 0f : Math.Clamp(fraction, 0f, 1f);
        if (clamped <= 0f)
        {
            return;
        }

        var width = MathF.Min(rect.Width, MathF.Max(rect.Height, rect.Width * clamped));
        Squircle.Fill(drawList, rect.Min, new Vector2(rect.Min.X + width, rect.Max.Y), radius,
            ImGui.GetColorU32(fill));
    }

    public static float PillWidth(string label, in TextStyle style, float scale)
    {
        var size = Typography.Measure(label, style);
        return MathF.Max(size.Y + PillPaddingY * scale, size.X + PillPaddingX * 2f * scale);
    }

    public static float Pill(ImDrawListPtr drawList, float right, float centerY, string label, Vector4 fill,
        Vector4 text, in TextStyle style, float scale)
    {
        if (label.Length == 0)
        {
            return right;
        }

        var size = Typography.Measure(label, style);
        var height = size.Y + PillPaddingY * scale;
        var width = MathF.Max(height, size.X + PillPaddingX * 2f * scale);
        var min = new Vector2(right - width, centerY - height * 0.5f);
        var max = new Vector2(right, centerY + height * 0.5f);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(fill));
        Typography.Draw(drawList, new Vector2((min.X + max.X - size.X) * 0.5f, centerY - size.Y * 0.5f), label, text,
            style);
        return min.X;
    }

    public static void CheckCircle(ImDrawListPtr drawList, in WidgetInk ink, Vector2 center, float radius,
        float fill, Vector4 accent, float scale)
    {
        var stroke = CheckStrokeUnits * scale;
        if (fill <= 0.01f)
        {
            drawList.AddCircle(center, radius, ImGui.GetColorU32(ink.Tertiary), 32, stroke);
            return;
        }

        var amount = MathF.Min(1f, fill);
        var tint = ink.Accent(accent);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(ink.Tertiary with { W = ink.Tertiary.W * (1f - amount) }),
            32, stroke);
        drawList.AddCircleFilled(center, radius * (0.55f + 0.45f * amount),
            ImGui.GetColorU32(tint with { W = tint.W * amount }), 32);
        var check = ImGui.GetColorU32(ink.OnAccent with { W = ink.OnAccent.W * amount });
        var thickness = CheckMarkUnits * scale;
        var start = center + new Vector2(-radius * 0.42f, 0f);
        var corner = center + new Vector2(-radius * 0.1f, radius * 0.34f);
        var end = center + new Vector2(radius * 0.44f, -radius * 0.32f);
        drawList.AddLine(start, corner, check, thickness);
        drawList.AddLine(corner, end, check, thickness);
    }

    private static void Message(in WidgetContext context, Rect area, Vector4 primary, Vector4 secondary,
        Vector4 glyphTint, WidgetGlyph glyphKind, FontAwesomeIcon icon, string appId, string title, string detail)
    {
        var drawList = context.DrawList;
        var scale = context.Scale;
        var maxWidth = MathF.Max(1f, area.Width);
        var glyph = MessageGlyphUnits * scale;
        var gap = MessageGapUnits * scale;
        var titleHeight = WidgetText.LineHeight(WidgetType.Headline);
        var detailHeight = WidgetText.LineHeight(WidgetType.Caption);
        var hasGlyph = glyphKind != WidgetGlyph.None;
        var titleBudget = Math.Clamp((int)(area.Height / MathF.Max(1f, titleHeight)), 1, MessageTitleLines);
        var lines = WidgetText.Clamp(title, WidgetType.Headline, maxWidth, titleBudget);
        var showGlyph = hasGlyph && area.Height >= glyph + gap + lines.Length * titleHeight;
        var total = lines.Length * titleHeight + (showGlyph ? glyph + gap : 0f);
        var detailLines = detail.Length == 0
            ? WidgetText.NoLines
            : WidgetText.Clamp(detail, WidgetType.Caption, maxWidth, MessageDetailLines);
        if (detailLines.Length > 0 && total + gap * 0.5f + detailLines.Length * detailHeight <= area.Height)
        {
            total += gap * 0.5f + detailLines.Length * detailHeight;
        }
        else
        {
            detailLines = WidgetText.NoLines;
        }

        var top = MathF.Max(area.Min.Y, area.Center.Y - total * 0.5f);
        if (showGlyph)
        {
            var glyphCenter = new Vector2(area.Center.X, top + glyph * 0.5f);
            if (glyphKind == WidgetGlyph.App)
            {
                AppIconTile.TryDrawGlyph(drawList, appId, glyphCenter, glyph, glyphTint);
            }
            else
            {
                ProgressRing.CenterIcon(drawList, glyphCenter, icon, glyphTint, glyph);
            }

            top += glyph + gap;
        }

        top = WidgetText.LinesCentered(drawList, lines, new Vector2(area.Center.X, top), primary,
            WidgetType.Headline, titleHeight);
        if (detailLines.Length == 0)
        {
            return;
        }

        WidgetText.LinesCentered(drawList, detailLines, new Vector2(area.Center.X, top + gap * 0.5f), secondary,
            WidgetType.Caption, detailHeight);
    }

    private static void RingArc(ImDrawListPtr drawList, Vector2 center, float radius, float thickness, float from,
        float to, uint color)
    {
        var segments = Math.Max(4, (int)MathF.Ceiling((to - from) * RingSegmentsPerRadian));
        drawList.PathClear();
        drawList.PathArcTo(center, radius, from, to, segments);
        drawList.PathStroke(color, ImDrawFlags.None, thickness);
        var cap = thickness * 0.5f;
        drawList.AddCircleFilled(center + new Vector2(MathF.Cos(from), MathF.Sin(from)) * radius, cap, color, 20);
        drawList.AddCircleFilled(center + new Vector2(MathF.Cos(to), MathF.Sin(to)) * radius, cap, color, 20);
    }

    private static void Opaque(ImDrawListPtr drawList, Rect bounds, float radius, float scale, Vector4 fill,
        float opacity)
    {
        Squircle.Fill(drawList, bounds.Min, bounds.Max, radius, ImGui.GetColorU32(fill with { W = opacity }));
        Material.EdgeSquircle(drawList, bounds.Min, bounds.Max, radius, scale, opacity);
    }

    private static Vector4 Dimmed(Vector4 color) =>
        new(color.X * OwnBackgroundDim, color.Y * OwnBackgroundDim, color.Z * OwnBackgroundDim, color.W);
}
