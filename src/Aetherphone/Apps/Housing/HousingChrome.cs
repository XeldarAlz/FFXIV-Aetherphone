using Aetherphone.Core;
using Aetherphone.Core.Animation;
using Aetherphone.Core.Housing;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Theme;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Apps.Housing;

internal static class HousingChrome
{
    public const float ChipHeight = 22f;
    public const float StatRowHeight = 26f;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 Transparent = new(0f, 0f, 0f, 0f);

    public static bool Hover(Vector2 min, Vector2 max, bool overlay) =>
        overlay ? UiInteract.HoverWindowOnly(min, max) : UiInteract.Hover(min, max);

    public static float SelectorHeight(float scale) =>
        Typography.LineHeight(TextStyles.Caption2) + Typography.LineHeight(TextStyles.SubheadlineEmphasized) +
        11f * scale;
    public static bool Selector(Rect rect, string label, string value, AppSkin ui, bool overlay = false)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var hovered = Hover(rect.Min, rect.Max, overlay);
        var rounding = Metrics.Radius.Sm * scale;
        Squircle.Fill(drawList, rect.Min, rect.Max, rounding,
            ImGui.GetColorU32(hovered ? ui.HoverTint : ui.FieldSurface));
        Squircle.Stroke(drawList, rect.Min, rect.Max, rounding,
            ImGui.GetColorU32(Palette.WithAlpha(ui.Accent, hovered ? 0.55f : 0.22f)), Metrics.Stroke.Hairline);
        var caretWidth = 13f * scale;
        var left = rect.Min.X + 9f * scale;
        var textWidth = MathF.Max(1f, rect.Width - 17f * scale - caretWidth);
        var labelStyle = TextStyles.Caption2;
        var valueStyle = TextStyles.SubheadlineEmphasized;
        var labelHeight = Typography.LineHeight(labelStyle);
        var valueHeight = Typography.LineHeight(valueStyle);
        var stackHeight = labelHeight + valueHeight + 1f * scale;
        var top = rect.Center.Y - stackHeight * 0.5f;
        Typography.Draw(drawList, new Vector2(left, top),
            Typography.FitText(Loc.Culture.TextInfo.ToUpper(label), textWidth, labelStyle), ui.MutedInk, labelStyle);
        Typography.Draw(drawList, new Vector2(left, top + labelHeight + 1f * scale),
            Typography.FitText(value, textWidth, valueStyle), ui.TitleInk, valueStyle);
        Caret(drawList, new Vector2(rect.Max.X - caretWidth * 0.62f, rect.Center.Y), 3.6f * scale, 1.5f * scale,
            hovered ? ui.Accent : ui.MutedInk);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public static void Chip(ImDrawListPtr drawList, Vector2 topLeft, string label, Vector4 hue, bool solid)
    {
        var scale = UiScale.Current;
        var height = ChipHeight * scale;
        var width = MeasureChip(label);
        var min = topLeft;
        var max = new Vector2(topLeft.X + width, topLeft.Y + height);
        var radius = height * 0.5f;
        if (solid)
        {
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(Palette.WithAlpha(hue, 0.95f)));
        }
        else
        {
            Squircle.Fill(drawList, min, max, radius, ImGui.GetColorU32(Palette.WithAlpha(hue, 0.16f)));
            Squircle.Stroke(drawList, min, max, radius, ImGui.GetColorU32(Palette.WithAlpha(hue, 0.42f)),
                Metrics.Stroke.Hairline);
        }

        var ink = solid
            ? Palette.Luminance(hue) > 0.62f ? new Vector4(0.06f, 0.07f, 0.06f, 1f) : White
            : Palette.Mix(hue, White, 0.55f);
        Typography.DrawCentered(drawList, new Vector2((min.X + max.X) * 0.5f, (min.Y + max.Y) * 0.5f), label, ink,
            TextStyles.Caption1);
    }

    public static float MeasureChip(string label) =>
        Typography.Measure(label, TextStyles.Caption1).X + 16f * UiScale.Current;

    public static Vector4 FreshnessHue(HousingDataFreshness freshness, Vector4 accent) => freshness switch
    {
        HousingDataFreshness.Live => accent,
        HousingDataFreshness.Recent => AppPalettes.HousingBrass,
        HousingDataFreshness.Stale => AppPalettes.HousingResults,
        HousingDataFreshness.Cached => AppPalettes.HousingParchment,
        _ => AppPalettes.HousingClosed,
    };

    public static void StatRow(ImDrawListPtr drawList, Rect row, string label, string value, AppSkin ui,
        bool emphasise = false)
    {
        var scale = UiScale.Current;
        var labelStyle = TextStyles.Footnote;
        var valueStyle = emphasise ? TextStyles.SubheadlineEmphasized : TextStyles.Subheadline;
        var labelWidth = MathF.Min(row.Width * 0.52f, Typography.Measure(label, labelStyle).X);
        Typography.Draw(drawList,
            new Vector2(row.Min.X, row.Center.Y - Typography.LineHeight(labelStyle) * 0.5f),
            Typography.FitText(label, labelWidth, labelStyle), ui.MutedInk, labelStyle);
        var valueMax = MathF.Max(1f, row.Width - labelWidth - 10f * scale);
        var fitted = Typography.FitText(value, valueMax, valueStyle);
        var valueSize = Typography.Measure(fitted, valueStyle);
        Typography.Draw(drawList, new Vector2(row.Max.X - valueSize.X, row.Center.Y - valueSize.Y * 0.5f), fitted,
            emphasise ? ui.TitleInk : ui.BodyInk, valueStyle);
    }

    public static bool PillButton(Rect rect, string label, bool filled, AppSkin ui, bool overlay = false,
        bool enabled = true)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var hovered = enabled && Hover(rect.Min, rect.Max, overlay);
        var radius = rect.Height * 0.5f;
        var accent = ui.Accent;
        var fill = filled
            ? enabled
                ? hovered ? Palette.Mix(accent, White, 0.14f) : accent
                : Palette.WithAlpha(accent, 0.38f)
            : enabled
                ? hovered ? ui.HoverTint : ui.FieldSurface
                : Palette.WithAlpha(ui.FieldSurface, ui.FieldSurface.W * 0.5f);
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill));
        if (!filled)
        {
            Squircle.Stroke(drawList, rect.Min, rect.Max, radius,
                ImGui.GetColorU32(Palette.WithAlpha(White, enabled ? 0.16f : 0.08f)), Metrics.Stroke.Hairline);
        }

        var ink = filled
            ? new Vector4(0.05f, 0.09f, 0.07f, 1f)
            : enabled
                ? hovered ? ui.TitleInk : ui.BodyInk
                : ui.MutedInk;
        var labelMaxWidth = MathF.Max(1f, rect.Width - rect.Height - 6f * scale);
        var style = TextStyles.SubheadlineEmphasized;
        var fitted = Typography.FitText(label, labelMaxWidth, style);
        Typography.DrawCentered(drawList, rect.Center, fitted, ink, style);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public static float MeasurePill(string label, float height) =>
        Typography.Measure(label, TextStyles.SubheadlineEmphasized).X + height + 14f * UiScale.Current;

    public static void LayoutPills(Rect row, ReadOnlySpan<string> labels, float gap, Span<Rect> into)
    {
        if (labels.Length == 0 || into.Length < labels.Length)
        {
            return;
        }

        var available = row.Width - gap * (labels.Length - 1);
        var total = 0f;
        Span<float> widths = stackalloc float[labels.Length];
        for (var index = 0; index < labels.Length; index++)
        {
            widths[index] = MeasurePill(labels[index], row.Height);
            total += widths[index];
        }

        var factor = total > 0f ? available / total : 1f;
        var x = row.Min.X;
        for (var index = 0; index < labels.Length; index++)
        {
            var width = index == labels.Length - 1 ? row.Max.X - x : widths[index] * factor;
            into[index] = new Rect(new Vector2(x, row.Min.Y), new Vector2(x + width, row.Max.Y));
            x += width + gap;
        }
    }

    public static bool DangerPillButton(Rect rect, string label, AppSkin ui, PhoneTheme theme, bool overlay = false)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = Hover(rect.Min, rect.Max, overlay);
        var radius = rect.Height * 0.5f;
        var danger = theme.Danger;
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, radius,
                ImGui.GetColorU32(Palette.WithAlpha(danger, 0.18f)));
        }

        Squircle.Stroke(drawList, rect.Min, rect.Max, radius,
            ImGui.GetColorU32(Palette.WithAlpha(danger, 0.55f)), Metrics.Stroke.Thin);
        Typography.DrawCentered(drawList, rect.Center, label, Palette.Mix(danger, White, 0.2f),
            TextStyles.SubheadlineEmphasized);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public static int Segment(Rect rect, string first, string second, int selected, AppSkin ui, bool overlay = false)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var radius = rect.Height * 0.5f;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(ui.FieldSurface));
        var half = rect.Width * 0.5f;
        var inset = 2f * scale;
        var thumbMin = new Vector2(rect.Min.X + inset + (selected == 1 ? half : 0f), rect.Min.Y + inset);
        var thumbMax = new Vector2(thumbMin.X + half - inset * 2f, rect.Max.Y - inset);
        Squircle.Fill(drawList, thumbMin, thumbMax, radius - inset, ImGui.GetColorU32(ui.Accent));
        var result = selected;
        for (var index = 0; index < 2; index++)
        {
            var min = new Vector2(rect.Min.X + index * half, rect.Min.Y);
            var max = new Vector2(min.X + half, rect.Max.Y);
            var hovered = Hover(min, max, overlay);
            var active = index == selected;
            var ink = active ? new Vector4(0.05f, 0.09f, 0.07f, 1f) : hovered ? ui.TitleInk : ui.BodyInk;
            var label = index == 0 ? first : second;
            Typography.DrawCentered(drawList, new Vector2((min.X + max.X) * 0.5f, rect.Center.Y),
                Typography.FitText(label, half - 12f * scale, TextStyles.SubheadlineEmphasized), ink,
                TextStyles.SubheadlineEmphasized);
            if (hovered)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (UiInteract.Click(min, max, hovered))
            {
                result = index;
            }
        }

        return result;
    }

    public static bool MapButton(Vector2 center, float radius, FontAwesomeIcon icon, AppSkin ui, string tooltip,
        bool active = false, bool overlay = false)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var hit = new Vector2(radius, radius);
        var hovered = Hover(center - hit, center + hit, overlay);
        var fill = active
            ? Palette.WithAlpha(ui.Accent, 0.92f)
            : new Vector4(0.08f, 0.10f, 0.09f, hovered ? 0.94f : 0.80f);
        Elevation.Icon(drawList, center - hit, center + hit, radius, radius, 0.8f);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(fill), 28);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(White, hovered ? 0.30f : 0.16f)), 28,
            1f * scale);
        var ink = active ? new Vector4(0.05f, 0.09f, 0.07f, 1f) : hovered ? ui.TitleInk : ui.BodyInk;
        AppSkin.Icon(drawList, center, icon.ToIconString(), ink, 0.78f);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(new Rect(center - hit, center + hit), tooltip, HoverLabelSide.Above);
        return UiInteract.Click(center - hit, center + hit, hovered);
    }

    public static bool RefreshButton(Vector2 center, float radius, AppSkin ui, bool busy, string tooltip,
        bool overlay = false)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var hit = new Vector2(radius, radius);
        var hovered = !busy && Hover(center - hit, center + hit, overlay);
        var fill = new Vector4(0.08f, 0.10f, 0.09f, hovered ? 0.94f : 0.80f);
        Elevation.Icon(drawList, center - hit, center + hit, radius, radius, 0.8f);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(fill), 28);
        drawList.AddCircle(center, radius, ImGui.GetColorU32(Palette.WithAlpha(White, hovered ? 0.30f : 0.16f)), 28,
            1f * scale);
        var ink = busy ? ui.Accent : hovered ? ui.TitleInk : ui.BodyInk;
        var rotation = busy ? Pulse.Phase(900.0) * MathF.Tau : 0f;
        HousingGlyphs.RefreshArrow(drawList, center, radius * 0.50f, ink, 1.7f * scale, rotation);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        HoverTooltip.Show(new Rect(center - hit, center + hit), tooltip, HoverLabelSide.Above);
        return !busy && UiInteract.Click(center - hit, center + hit, hovered);
    }

    public static bool CloseButton(Vector2 center, float radius, AppSkin ui, bool overlay = false)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var hit = new Vector2(radius, radius);
        var hovered = Hover(center - hit, center + hit, overlay);
        drawList.AddCircleFilled(center, radius, ImGui.GetColorU32(hovered ? ui.HoverTint : ui.FieldSurface), 24);
        var ink = ImGui.GetColorU32(hovered ? ui.TitleInk : ui.MutedInk);
        var arm = radius * 0.42f;
        drawList.AddLine(center - new Vector2(arm, arm), center + new Vector2(arm, arm), ink, 1.7f * scale);
        drawList.AddLine(center + new Vector2(-arm, arm), center + new Vector2(arm, -arm), ink, 1.7f * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(center - hit, center + hit, hovered);
    }

    private static readonly Dictionary<string, string> NumberBuffers = new(StringComparer.Ordinal);

    public static int NumberStepper(Rect rect, string id, int value, int minimum, int maximum, int step, string suffix,
        AppSkin ui, bool overlay = false)
    {
        var scale = UiScale.Current;
        var drawList = ImGui.GetWindowDrawList();
        var buttonRadius = rect.Height * 0.5f;
        var minusCenter = new Vector2(rect.Min.X + buttonRadius, rect.Center.Y);
        var plusCenter = new Vector2(rect.Max.X - buttonRadius, rect.Center.Y);
        var fieldMin = new Vector2(minusCenter.X + buttonRadius + 5f * scale, rect.Min.Y);
        var fieldMax = new Vector2(plusCenter.X - buttonRadius - 5f * scale, rect.Max.Y);
        var result = value;
        if (RoundButton(drawList, minusCenter, buttonRadius, "-", ui, value > minimum, overlay, scale))
        {
            result = value - step;
        }

        if (RoundButton(drawList, plusCenter, buttonRadius, "+", ui, value < maximum, overlay, scale))
        {
            result = value + step;
        }

        Squircle.Fill(drawList, fieldMin, fieldMax, Metrics.Radius.Sm * scale, ImGui.GetColorU32(ui.FieldSurface));
        var typing = NumberBuffers.TryGetValue(id, out var buffer) && buffer is not null;
        var text = typing ? buffer! : value.ToString(Loc.Culture);
        var suffixWidth = suffix.Length > 0
            ? Typography.Measure(suffix, TextStyles.Caption1).X + 4f * scale
            : 0f;
        var inputWidth = MathF.Max(18f * scale, fieldMax.X - fieldMin.X - 10f * scale - suffixWidth);
        ImGui.SetCursorScreenPos(new Vector2(fieldMin.X + 5f * scale,
            rect.Center.Y - ImGui.GetFrameHeight() * 0.5f));
        ImGui.SetNextItemWidth(inputWidth);
        using (ImRaii.PushColor(ImGuiCol.FrameBg, Transparent))
        using (ImRaii.PushColor(ImGuiCol.Text, ui.TitleInk))
        {
            ImGui.InputText(id, ref text, 6,
                ImGuiInputTextFlags.CharsDecimal | ImGuiInputTextFlags.AutoSelectAll);
        }

        if (ImGui.IsItemActive())
        {
            NumberBuffers[id] = text;
        }
        else if (typing)
        {
            NumberBuffers.Remove(id);
            if (int.TryParse(text, System.Globalization.NumberStyles.Integer, Loc.Culture, out var typed))
            {
                result = typed;
            }
        }

        if (suffix.Length > 0)
        {
            var suffixSize = Typography.Measure(suffix, TextStyles.Caption1);
            Typography.Draw(drawList, new Vector2(fieldMax.X - 5f * scale - suffixSize.X,
                rect.Center.Y - suffixSize.Y * 0.5f), suffix, ui.MutedInk, TextStyles.Caption1);
        }

        return Math.Clamp(result, minimum, maximum);
    }

    private static bool RoundButton(ImDrawListPtr drawList, Vector2 center, float radius, string glyph, AppSkin ui,
        bool enabled, bool overlay, float scale)
    {
        var hit = new Vector2(radius, radius);
        var hovered = enabled && Hover(center - hit, center + hit, overlay);
        drawList.AddCircleFilled(center, radius,
            ImGui.GetColorU32(hovered ? Palette.WithAlpha(ui.Accent, 0.85f) : ui.FieldSurface), 24);
        var ink = hovered
            ? new Vector4(0.05f, 0.09f, 0.07f, 1f)
            : enabled
                ? ui.TitleInk
                : ui.MutedInk;
        var arm = radius * 0.40f;
        var packed = ImGui.GetColorU32(ink);
        drawList.AddLine(new Vector2(center.X - arm, center.Y), new Vector2(center.X + arm, center.Y), packed,
            1.8f * scale);
        if (glyph == "+")
        {
            drawList.AddLine(new Vector2(center.X, center.Y - arm), new Vector2(center.X, center.Y + arm), packed,
                1.8f * scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return enabled && UiInteract.Click(center - hit, center + hit, hovered);
    }

    public static void SectionLabel(ImDrawListPtr drawList, Vector2 topLeft, float width, string label, AppSkin ui)
    {
        var scale = UiScale.Current;
        var text = Loc.Culture.TextInfo.ToUpper(label);
        Typography.Draw(drawList, topLeft, Typography.FitText(text, width, TextStyles.Caption1), ui.HeaderInk,
            TextStyles.Caption1);
        var ruleY = topLeft.Y + Typography.LineHeight(TextStyles.Caption1) + 3f * scale;
        drawList.AddLine(new Vector2(topLeft.X, ruleY), new Vector2(topLeft.X + width, ruleY),
            ImGui.GetColorU32(Palette.WithAlpha(AppPalettes.HousingBrass, 0.24f)), 1f * scale);
    }

    public static void Caret(ImDrawListPtr drawList, Vector2 center, float size, float thickness, Vector4 color)
    {
        var packed = ImGui.GetColorU32(color);
        var tip = new Vector2(center.X, center.Y + size * 0.62f);
        drawList.AddLine(new Vector2(center.X - size, center.Y - size * 0.35f), tip, packed, thickness);
        drawList.AddLine(tip, new Vector2(center.X + size, center.Y - size * 0.35f), packed, thickness);
    }

    public static void SheetSurface(ImDrawListPtr drawList, Rect sheet, Rect behind, float progress, AppSkin ui)
    {
        var scale = UiScale.Current;
        Material.Veil(drawList, behind.Min, behind.Max, 0.30f * progress);
        var rounding = Metrics.Radius.Lg * scale;
        Elevation.Floating(drawList, sheet.Min, sheet.Max, rounding, scale, progress);
        Squircle.Fill(drawList, sheet.Min, sheet.Max, rounding,
            ImGui.GetColorU32(new Vector4(0.09f, 0.12f, 0.11f, 0.985f * progress)));
        Material.EdgeSquircle(drawList, sheet.Min, sheet.Max, rounding, scale, progress);
        Material.TopGlow(drawList, sheet.Min, sheet.Max, rounding, ui.Accent, 0.22f, 0.10f * progress);
        var handleWidth = 34f * scale;
        var handleY = sheet.Min.Y + 7f * scale;
        drawList.AddLine(new Vector2(sheet.Center.X - handleWidth * 0.5f, handleY),
            new Vector2(sheet.Center.X + handleWidth * 0.5f, handleY),
            ImGui.GetColorU32(Palette.WithAlpha(White, 0.24f * progress)), 3f * scale);
    }
}
