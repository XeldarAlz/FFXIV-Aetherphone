using Aetherphone.Core;
using Aetherphone.Core.Home;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Windows.Widgets;

internal static class WidgetControls
{
    private const float GlyphFraction = 0.42f;
    private const float CapsuleGlyphFraction = 0.46f;
    private const float HoverBoost = 1.5f;
    private const float LinkHoverAlpha = 0.55f;
    private const float LinkPressAlpha = 0.9f;
    private const float DisabledFillAlpha = 0.5f;

    public static bool Button(in WidgetContext context, in WidgetInk ink, int controlId, Vector2 center,
        float diameterUnits, FontAwesomeIcon icon, Vector4 accent = default, bool enabled = true)
    {
        var key = WidgetHits.Key(context, controlId);
        var radius = Math.Clamp(diameterUnits, WidgetMetrics.ControlSmall, WidgetMetrics.ControlLarge) * 0.5f *
                     context.Scale;
        var hitRect = new Rect(center - new Vector2(radius), center + new Vector2(radius));
        if (!enabled)
        {
            Inert(context, key, hitRect);
            context.DrawList.AddCircleFilled(center, radius,
                ImGui.GetColorU32(ink.Fill with { W = ink.Fill.W * DisabledFillAlpha }), 40);
            ProgressRing.CenterIcon(context.DrawList, center, icon, ink.Tertiary, radius * 2f * GlyphFraction);
            return false;
        }

        var fired = Interact(context, key, hitRect, out var hovered);
        var drawRadius = radius * WidgetHits.PressScale(key);
        var prominent = accent.W > 0f;
        var fill = prominent ? ink.Accent(accent) : Hovered(ink.Fill, hovered);
        context.DrawList.AddCircleFilled(center, drawRadius, ImGui.GetColorU32(fill), 40);
        ProgressRing.CenterIcon(context.DrawList, center, icon, prominent ? ink.OnAccent : ink.Primary,
            drawRadius * 2f * GlyphFraction);
        return fired;
    }

    public static bool Pressable(in WidgetContext context, int controlId, Rect rect, out bool hovered,
        out float pressScale)
    {
        var key = WidgetHits.Key(context, controlId);
        pressScale = WidgetHits.PressScale(key);
        return Interact(context, key, rect, out hovered);
    }

    public static Rect Scaled(Rect rect, float factor)
    {
        if (factor == 1f)
        {
            return rect;
        }

        var center = rect.Center;
        var half = rect.Size * 0.5f * factor;
        return new Rect(center - half, center + half);
    }

    public static bool Button(in WidgetContext context, in WidgetInk ink, int controlId, Rect rect,
        FontAwesomeIcon icon, string label, Vector4 accent = default) =>
        Capsule(context, ink, controlId, rect, true, icon, label, accent);

    public static bool Button(in WidgetContext context, in WidgetInk ink, int controlId, Rect rect, string label,
        Vector4 accent = default) =>
        Capsule(context, ink, controlId, rect, false, default, label, accent);

    private static bool Capsule(in WidgetContext context, in WidgetInk ink, int controlId, Rect rect, bool hasIcon,
        FontAwesomeIcon icon, string label, Vector4 accent)
    {
        var key = WidgetHits.Key(context, controlId);
        var fired = Interact(context, key, rect, out var hovered);
        var drawRect = Scaled(rect, WidgetHits.PressScale(key));
        var prominent = accent.W > 0f;
        var fill = prominent ? ink.Accent(accent) : Hovered(ink.Fill, hovered);
        var content = prominent ? ink.OnAccent : ink.Primary;
        var height = drawRect.Height;
        Squircle.Fill(context.DrawList, drawRect.Min, drawRect.Max, height * 0.5f, ImGui.GetColorU32(fill));
        var glyphSize = hasIcon ? height * CapsuleGlyphFraction : 0f;
        var labelText = label ?? string.Empty;
        var padding = height * 0.5f;
        var gap = hasIcon && labelText.Length > 0 ? WidgetMetrics.RowGap * 2f * context.Scale : 0f;
        var maxLabel = MathF.Max(0f, drawRect.Width - padding * 2f - glyphSize - gap);
        var labelScale = WidgetType.Headline.Scale;
        var fitted = string.Empty;
        if (labelText.Length > 0)
        {
            fitted = WidgetText.Fit(labelText, maxLabel, WidgetType.Headline, out labelScale);
        }

        var labelSize = fitted.Length > 0
            ? Typography.Measure(fitted, labelScale, WidgetType.Headline.Weight)
            : Vector2.Zero;
        var total = glyphSize + gap + labelSize.X;
        var left = drawRect.Center.X - total * 0.5f;
        if (hasIcon)
        {
            ProgressRing.CenterIcon(context.DrawList, new Vector2(left + glyphSize * 0.5f, drawRect.Center.Y), icon,
                content, glyphSize);
        }

        if (fitted.Length > 0)
        {
            Typography.Draw(context.DrawList,
                new Vector2(left + glyphSize + gap, drawRect.Center.Y - labelSize.Y * 0.5f), fitted, content,
                labelScale, WidgetType.Headline.Weight);
        }

        return fired;
    }

    public static bool Toggle(in WidgetContext context, in WidgetInk ink, int controlId, Vector2 center,
        float diameterUnits, FontAwesomeIcon icon, bool value, Vector4 accent)
    {
        var key = WidgetHits.Key(context, controlId);
        var radius = Math.Clamp(diameterUnits, WidgetMetrics.ControlSmall, WidgetMetrics.ControlLarge) * 0.5f *
                     context.Scale;
        var hitRect = new Rect(center - new Vector2(radius), center + new Vector2(radius));
        var fired = Interact(context, key, hitRect, out var hovered);
        var next = fired ? !value : value;
        var drawRadius = radius * WidgetHits.PressScale(key);
        var fill = next ? ink.Accent(accent) : Hovered(ink.Fill, hovered);
        context.DrawList.AddCircleFilled(center, drawRadius, ImGui.GetColorU32(fill), 40);
        ProgressRing.CenterIcon(context.DrawList, center, icon, next ? ink.OnAccent : ink.Primary,
            drawRadius * 2f * GlyphFraction);
        return next;
    }

    public static bool Link(in WidgetContext context, in WidgetInk ink, int controlId, Rect rect,
        in WidgetRoute route)
    {
        if (!context.Interactive)
        {
            return false;
        }

        var key = WidgetHits.Key(context, controlId);
        WidgetHits.RegisterLink(key, rect, route);
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        if (!hovered && !WidgetHits.IsPressed(key))
        {
            return false;
        }

        ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        var alpha = WidgetHits.IsPressed(key) ? LinkPressAlpha : LinkHoverAlpha;
        Squircle.Fill(context.DrawList, rect.Min, rect.Max, WidgetMetrics.InnerRadius(context),
            ImGui.GetColorU32(ink.Fill with { W = ink.Fill.W * alpha }));
        return hovered;
    }

    private static bool Interact(in WidgetContext context, int key, Rect hitRect, out bool hovered)
    {
        hovered = false;
        if (!context.Interactive)
        {
            return false;
        }

        WidgetHits.Register(key, hitRect);
        hovered = UiInteract.Hover(hitRect.Min, hitRect.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return WidgetHits.ConsumeFired(key);
    }

    private static Vector4 Hovered(Vector4 fill, bool hovered) =>
        hovered ? fill with { W = MathF.Min(1f, fill.W * HoverBoost) } : fill;

    private static void Inert(in WidgetContext context, int key, Rect hitRect)
    {
        if (!context.Interactive)
        {
            return;
        }

        WidgetHits.Register(key, hitRect);
    }
}
