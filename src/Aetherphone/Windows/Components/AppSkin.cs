using Aetherphone.Core;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class AppSkin
{
    public static readonly Vector4 Transparent = new(0f, 0f, 0f, 0f);

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 HoverFill = new(1f, 1f, 1f, 0.16f);
    private static readonly Vector4 GhostHover = new(1f, 1f, 1f, 0.08f);
    private static readonly Vector4 GhostStroke = new(1f, 1f, 1f, 0.28f);
    private static readonly Vector4 ChipInactive = new(1f, 1f, 1f, 0.08f);
    private static readonly Vector4 ChipActiveInk = new(0.99f, 0.85f, 0.91f, 1f);
    private static readonly TextStyle SectionLabelStyle = new(0.78f, FontWeight.SemiBold);

    public AppPalette Palette { get; set; }

    public PhoneTheme Theme { get; set; } = PhoneTheme.Default;

    public AppSkin(AppPalette palette)
    {
        Palette = palette;
    }

    public Vector4 Accent => Palette.Accent;

    public Vector4 TitleInk => Palette.TitleInk;

    public Vector4 BodyInk => Palette.BodyInk;

    public Vector4 MutedInk => Palette.MutedInk;

    public Vector4 HeaderInk => Palette.HeaderInk;

    public Vector4 FieldSurface => Palette.FieldSurface;

    public Vector4 HoverTint => Palette.HoverTint;

    public void Backdrop(Rect screen)
    {
        var scale = ImGuiHelpers.GlobalScale;
        PaintGradient(ImGui.GetWindowDrawList(), screen, screen, Theme.ScreenRounding * scale);
    }

    public void Body(Rect area)
    {
        var frame = SceneChrome.ScreenFrom(area, Theme, ImGuiHelpers.GlobalScale);
        PaintGradient(ImGui.GetWindowDrawList(), area, frame, 0f);
    }

    public void PaintGradient(ImDrawListPtr drawList, Rect target, Rect frame, float rounding)
    {
        var topFraction = frame.Height <= 0f ? 0f : (target.Min.Y - frame.Min.Y) / frame.Height;
        var bottomFraction = frame.Height <= 0f ? 1f : (target.Max.Y - frame.Min.Y) / frame.Height;
        Squircle.FillVerticalGradient(drawList, target.Min, target.Max, rounding,
            ImGui.GetColorU32(Vector4.Lerp(Palette.BackdropTop, Palette.BackdropBottom, topFraction)),
            ImGui.GetColorU32(Vector4.Lerp(Palette.BackdropTop, Palette.BackdropBottom, bottomFraction)));
        Squircle.FillVerticalGradient(drawList, target.Min, target.Max, rounding,
            ImGui.GetColorU32(Vector4.Lerp(Palette.BloomTop, Palette.BloomBottom, topFraction)),
            ImGui.GetColorU32(Vector4.Lerp(Palette.BloomTop, Palette.BloomBottom, bottomFraction)));
    }

    public void Card(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding, bool elevated = false)
    {
        if (elevated)
        {
            var scale = ImGuiHelpers.GlobalScale;
            var shadow = new Vector2(0f, 2f * scale);
            drawList.AddRectFilled(min + shadow, max + shadow, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.24f)),
                rounding);
            Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(Palette.CardFill));
            Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(Palette.CardStroke), 1f);
            Material.EdgeSquircle(drawList, min, max, rounding, scale, 0.7f);
            return;
        }

        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(Palette.CardFill));
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(Palette.CardStroke), 1f);
    }

    public bool PillButton(Rect rect, string label, bool filled) =>
        PillButtonCore(rect, label, filled, Palette.Accent, Palette.FieldSurface, Palette.TitleInk, Theme);

    public static bool PillButton(Rect rect, string label, bool filled, PhoneTheme theme) =>
        PillButtonCore(rect, label, filled, theme.Accent, theme.SurfaceMuted, theme.TextStrong, theme);

    public static bool PillButton(Rect rect, string label, bool filled, bool enabled, PhoneTheme theme)
    {
        if (enabled)
        {
            return PillButtonCore(rect, label, filled, theme.Accent, theme.GroupedCard, theme.TextStrong, theme);
        }

        var drawList = ImGui.GetWindowDrawList();
        var fill = Core.Theme.Palette.WithAlpha(filled ? theme.Accent : theme.GroupedCard, 0.45f);
        Squircle.Fill(drawList, rect.Min, rect.Max, rect.Height * 0.5f, ImGui.GetColorU32(fill));
        var textSize = Typography.Measure(label, 0.9f, FontWeight.SemiBold);
        Typography.Draw(rect.Center - textSize * 0.5f, label, theme.TextMuted, 0.9f, FontWeight.SemiBold);
        return false;
    }

    public bool FlowChip(ref float cursorX, float centerY, float gap, string label, bool active)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var textSize = Typography.Measure(label, 0.85f, FontWeight.Medium);
        var height = 32f * scale;
        var width = textSize.X + 26f * scale;
        var min = new Vector2(cursorX, centerY - height * 0.5f);
        var max = new Vector2(cursorX + width, centerY + height * 0.5f);
        var hovered = UiInteract.Hover(min, max);
        var fill = active
            ? (hovered ? Core.Theme.Palette.Mix(Palette.Accent, White, 0.10f) : Palette.Accent)
            : (hovered ? HoverFill : Palette.FieldSurface);
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(fill));
        var ink = active ? White : hovered ? Palette.TitleInk : Palette.BodyInk;
        Typography.Draw(new Vector2(min.X + (width - textSize.X) * 0.5f, centerY - textSize.Y * 0.5f), label, ink,
            0.85f, FontWeight.Medium);
        cursorX = max.X + gap;
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    public static bool FlowChip(ref float cursorX, float centerY, float gap, string label, bool active,
        PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var textSize = Typography.Measure(label, 0.8f, FontWeight.Medium);
        var height = 28f * scale;
        var width = textSize.X + 22f * scale;
        var min = new Vector2(cursorX, centerY - height * 0.5f);
        var max = new Vector2(cursorX + width, centerY + height * 0.5f);
        var hovered = UiInteract.Hover(min, max);
        var fill = active ? Core.Theme.Palette.WithAlpha(theme.Accent, 0.92f) : theme.GroupedCard;
        Squircle.Fill(drawList, min, max, height * 0.5f, ImGui.GetColorU32(fill));
        var ink = active || hovered ? theme.TextStrong : theme.TextMuted;
        Typography.Draw(new Vector2(min.X + (width - textSize.X) * 0.5f, centerY - textSize.Y * 0.5f), label, ink,
            0.8f, FontWeight.Medium);
        cursorX = max.X + gap;
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }

    private static bool PillButtonCore(Rect rect, string label, bool filled, Vector4 accent, Vector4 surface,
        Vector4 titleInk, PhoneTheme theme)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var radius = rect.Height * 0.5f;
        var fill = filled
            ? (hovered ? Core.Theme.Palette.Mix(accent, theme.TextStrong, 0.12f) : accent)
            : (hovered ? HoverFill : surface);
        var ink = filled ? White : titleInk;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill));
        var textSize = Typography.Measure(label, 0.9f, FontWeight.SemiBold);
        Typography.Draw(rect.Center - textSize * 0.5f, label, ink, 0.9f, FontWeight.SemiBold);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public bool DangerPillButton(Rect rect, string label) => DangerPillButton(rect, label, Theme);

    public static bool DangerPillButton(Rect rect, string label, PhoneTheme theme)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var radius = rect.Height * 0.5f;
        var fill = hovered ? Core.Theme.Palette.Mix(theme.Danger, theme.TextStrong, 0.12f) : theme.Danger;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill));
        var textSize = Typography.Measure(label, 0.9f, FontWeight.SemiBold);
        Typography.Draw(rect.Center - textSize * 0.5f, label, White, 0.9f, FontWeight.SemiBold);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public bool DangerGhostButton(Rect rect, string label)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var radius = rect.Height * 0.5f;
        var danger = Theme.Danger;
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, radius,
                ImGui.GetColorU32(Core.Theme.Palette.WithAlpha(danger, 0.14f)));
        }

        Squircle.Stroke(drawList, rect.Min, rect.Max, radius,
            ImGui.GetColorU32(Core.Theme.Palette.WithAlpha(danger, 0.55f)), 1.4f);
        var ink = Core.Theme.Palette.Mix(danger, White, 0.18f);
        var textSize = Typography.Measure(label, 0.9f, FontWeight.SemiBold);
        Typography.Draw(rect.Center - textSize * 0.5f, label, ink, 0.9f, FontWeight.SemiBold);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public bool GhostButton(Rect rect, string label) => GhostButtonCore(rect, label, Palette.TitleInk);

    public static bool GhostButton(Rect rect, string label, PhoneTheme theme) =>
        GhostButtonCore(rect, label, theme.TextStrong);

    private static bool GhostButtonCore(Rect rect, string label, Vector4 titleInk)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var radius = rect.Height * 0.5f;
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(GhostHover));
        }

        Squircle.Stroke(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(GhostStroke), 1f);
        var textSize = Typography.Measure(label, 0.9f, FontWeight.SemiBold);
        Typography.Draw(rect.Center - textSize * 0.5f, label, titleInk, 0.9f, FontWeight.SemiBold);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public bool IconButton(Vector2 center, float hitRadius, string glyph, Vector4 color, Vector4 background,
        float glyphScale, string tooltip = "", HoverLabelSide tooltipSide = HoverLabelSide.Above) =>
        IconButton(center, hitRadius, glyph, color, background, glyphScale, Theme, tooltip, tooltipSide);

    public static bool IconButton(Vector2 center, float hitRadius, string glyph, Vector4 color, Vector4 background,
        float glyphScale, PhoneTheme theme, string tooltip = "", HoverLabelSide tooltipSide = HoverLabelSide.Above)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hit = new Vector2(hitRadius, hitRadius);
        var hovered = UiInteract.Hover(center - hit, center + hit);
        if (background.W > 0f)
        {
            drawList.AddCircleFilled(center, hitRadius,
                ImGui.GetColorU32(hovered ? Core.Theme.Palette.Mix(background, theme.TextStrong, 0.08f) : background),
                24);
        }

        Icon(center, glyph, hovered ? Core.Theme.Palette.Mix(color, theme.TextStrong, 0.2f) : color, glyphScale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(new Rect(center - hit, center + hit), tooltip, tooltipSide);
        return UiInteract.Click(center - hit, center + hit, hovered);
    }

    public bool Chip(Rect rect, string label, bool active) =>
        ChipCore(rect, label, active, Palette.Accent, ChipActiveInk, Palette.BodyInk);

    public static bool Chip(Rect rect, string label, bool active, PhoneTheme theme) =>
        ChipCore(rect, label, active, theme.Accent, theme.TextStrong, theme.TextStrong);

    private static bool ChipCore(Rect rect, string label, bool active, Vector4 accent, Vector4 activeInk,
        Vector4 bodyInk)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var radius = rect.Height * 0.5f;
        var fill = active ? Core.Theme.Palette.WithAlpha(accent, 0.28f) : ChipInactive;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill));
        if (active)
        {
            Squircle.Stroke(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(accent), 1.4f);
        }

        var ink = active ? activeInk : bodyInk;
        Typography.DrawCentered(rect.Center, label, ink, 0.85f, FontWeight.Medium);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public void ToggleRow(string label, ref bool value)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 34f * scale;
        Typography.Draw(new Vector2(origin.X, origin.Y + height * 0.5f - 8f * scale), label, Theme.TextStrong, 0.95f);
        var trackWidth = 44f * scale;
        var trackHeight = 24f * scale;
        var trackMin = new Vector2(origin.X + width - trackWidth, origin.Y + height * 0.5f - trackHeight * 0.5f);
        var trackMax = new Vector2(trackMin.X + trackWidth, trackMin.Y + trackHeight);
        var drawList = ImGui.GetWindowDrawList();
        Squircle.Fill(drawList, trackMin, trackMax, trackHeight * 0.5f,
            ImGui.GetColorU32(value ? Palette.Accent : new Vector4(1f, 1f, 1f, 0.16f)));
        var knobX = value ? trackMax.X - trackHeight * 0.5f : trackMin.X + trackHeight * 0.5f;
        drawList.AddCircleFilled(new Vector2(knobX, (trackMin.Y + trackMax.Y) * 0.5f), trackHeight * 0.5f - 3f * scale,
            ImGui.GetColorU32(White), 24);
        ImGui.SetCursorScreenPos(origin);
        if (UiInteract.HoverClick(origin, new Vector2(origin.X + width, origin.Y + height)))
        {
            value = !value;
        }

        ImGui.Dummy(new Vector2(width, height));
    }

    public void Field(string label, string id, ref string value, int maxLength, bool multiline) =>
        Field(label, id, ref value, maxLength, multiline,
            multiline ? Metrics.Size.FieldMultiline : Metrics.Size.FieldHeight);

    public void Field(string label, string id, ref string value, int maxLength, bool multiline, float heightUnscaled)
    {
        var scale = ImGuiHelpers.GlobalScale;
        using (ImRaii.PushColor(ImGuiCol.Text, Palette.MutedInk))
        {
            Typography.Plain(label);
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = heightUnscaled * scale;
        Squircle.Fill(ImGui.GetWindowDrawList(), origin, new Vector2(origin.X + width, origin.Y + height),
            Metrics.Radius.Field * scale, ImGui.GetColorU32(Palette.FieldSurface));
        ImGui.SetCursorScreenPos(new Vector2(origin.X + Metrics.Space.Md * scale,
            origin.Y + (multiline ? Metrics.Space.Sm * scale : height * 0.5f - ImGui.GetFrameHeight() * 0.5f)));
        ImGui.SetNextItemWidth(width - Metrics.Space.Md * 2f * scale);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, Palette.TitleInk))
        {
            if (multiline)
            {
                var fieldSize = new Vector2(width - Metrics.Space.Md * 2f * scale, height - Metrics.Space.Lg * scale);
                var wrapWidth = fieldSize.X - ImGui.GetStyle().FramePadding.X * 2f - 4f * scale;
                SoftWrapField.Multiline(id, ref value, maxLength, fieldSize, wrapWidth);
            }
            else
            {
                ImGui.InputText(id, ref value, maxLength, ImGuiInputTextFlags.None);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    public bool HeaderAction(Rect area, string label, bool enabled)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var height = 28f * scale;
        var width = Typography.Measure(label, 0.9f, FontWeight.SemiBold).X + 26f * scale;
        var max = new Vector2(area.Max.X - 12f * scale, area.Min.Y + AppHeader.Height * scale * 0.5f + height * 0.5f);
        var min = new Vector2(max.X - width, max.Y - height);
        var rect = new Rect(min, max);
        return PillButton(rect, label, enabled) && enabled;
    }

    public void LabelValue(string label, string value)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, Palette.MutedInk))
        {
            Typography.Plain(label);
        }

        ImGui.PushTextWrapPos(0f);
        using (ImRaii.PushColor(ImGuiCol.Text, Theme.TextStrong))
        {
            Typography.Wrapped(value);
        }

        ImGui.PopTextWrapPos();
    }

    public void SectionLabel(string label) => SectionLabel(label, SectionLabelStyle, 4f);

    public void SectionLabel(string label, in TextStyle style, float gapPixels)
    {
        var scale = ImGuiHelpers.GlobalScale;
        using (Plugin.Fonts.Push(style.Scale, style.Weight))
        using (ImRaii.PushColor(ImGuiCol.Text, Palette.HeaderInk))
        {
            Typography.Plain(Loc.Culture.TextInfo.ToUpper(label));
        }

        ImGui.Dummy(new Vector2(0f, gapPixels * scale));
    }

    public void SectionHeading(string label, float topPadPixels = 0f)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var topPad = topPadPixels * scale;
        if (topPad > 0f)
        {
            ImGui.SetCursorScreenPos(new Vector2(origin.X, origin.Y + topPad));
        }

        var barWidth = 3f * scale;
        var barHeight = 14f * scale;
        Squircle.Fill(drawList, new Vector2(origin.X, origin.Y + topPad + 2f * scale),
            new Vector2(origin.X + barWidth, origin.Y + topPad + 2f * scale + barHeight), barWidth * 0.5f,
            ImGui.GetColorU32(Palette.Accent));
        Typography.Draw(new Vector2(origin.X + barWidth + 9f * scale, origin.Y + topPad), label, Palette.HeadingInk,
            0.95f, FontWeight.SemiBold);
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, topPad + barHeight + (topPad > 0f ? 8f : 10f) * scale));
    }

    public void HelpText(string text)
    {
        ImGui.PushTextWrapPos(0f);
        using (ImRaii.PushColor(ImGuiCol.Text, Palette.MutedInk))
        using (Plugin.Fonts.Push(0.82f))
        {
            Typography.Wrapped(text);
        }

        ImGui.PopTextWrapPos();
    }

    public static void Icon(Vector2 center, string glyph, Vector4 color, float scale) =>
        Icon(ImGui.GetWindowDrawList(), center, glyph, color, scale);

    public static void Icon(ImDrawListPtr drawList, Vector2 center, string glyph, Vector4 color, float scale)
    {
        float fontSize;
        Vector2 size;
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            fontSize = ImGui.GetFontSize() * scale;
            size = ImGui.CalcTextSize(glyph) * scale;
        }

        drawList.AddText(UiBuilder.IconFont, fontSize, center - size * 0.5f, ImGui.GetColorU32(color), glyph, 0f);
    }
}
