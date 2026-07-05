using System.Numerics;
using Aetherphone.Core;
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

    public bool PillButton(Rect rect, string label, bool filled)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var radius = rect.Height * 0.5f;
        var fill = filled
            ? (hovered ? Core.Theme.Palette.Mix(Palette.Accent, Theme.TextStrong, 0.12f) : Palette.Accent)
            : (hovered ? HoverFill : Palette.FieldSurface);
        var ink = filled ? White : Palette.TitleInk;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill));
        var textSize = Typography.Measure(label, 0.9f, FontWeight.SemiBold);
        Typography.Draw(rect.Center - textSize * 0.5f, label, ink, 0.9f, FontWeight.SemiBold);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public bool DangerPillButton(Rect rect, string label)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var radius = rect.Height * 0.5f;
        var fill = hovered ? Core.Theme.Palette.Mix(Theme.Danger, Theme.TextStrong, 0.12f) : Theme.Danger;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill));
        var textSize = Typography.Measure(label, 0.9f, FontWeight.SemiBold);
        Typography.Draw(rect.Center - textSize * 0.5f, label, White, 0.9f, FontWeight.SemiBold);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public bool GhostButton(Rect rect, string label)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var radius = rect.Height * 0.5f;
        if (hovered)
        {
            Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(GhostHover));
        }

        Squircle.Stroke(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(GhostStroke), 1f);
        var textSize = Typography.Measure(label, 0.9f, FontWeight.SemiBold);
        Typography.Draw(rect.Center - textSize * 0.5f, label, Palette.TitleInk, 0.9f, FontWeight.SemiBold);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public bool IconButton(Vector2 center, float hitRadius, string glyph, Vector4 color, Vector4 background,
        float glyphScale, string tooltip = "")
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(center - new Vector2(hitRadius, hitRadius),
            center + new Vector2(hitRadius, hitRadius));
        if (background.W > 0f)
        {
            drawList.AddCircleFilled(center, hitRadius,
                ImGui.GetColorU32(hovered ? Core.Theme.Palette.Mix(background, Theme.TextStrong, 0.08f) : background),
                24);
        }

        Icon(center, glyph, hovered ? Core.Theme.Palette.Mix(color, Theme.TextStrong, 0.2f) : color, glyphScale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (tooltip.Length > 0)
            {
                DrawActionTooltip(center, hitRadius, tooltip);
            }
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public bool Chip(Rect rect, string label, bool active)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsMouseHoveringRect(rect.Min, rect.Max);
        var radius = rect.Height * 0.5f;
        var fill = active ? Core.Theme.Palette.WithAlpha(Palette.Accent, 0.28f) : ChipInactive;
        Squircle.Fill(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(fill));
        if (active)
        {
            Squircle.Stroke(drawList, rect.Min, rect.Max, radius, ImGui.GetColorU32(Palette.Accent), 1.4f);
        }

        var ink = active ? ChipActiveInk : Palette.BodyInk;
        Typography.DrawCentered(rect.Center, label, ink, 0.85f, FontWeight.Medium);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
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

    public void Field(string label, string id, ref string value, int maxLength, bool multiline)
    {
        var scale = ImGuiHelpers.GlobalScale;
        using (ImRaii.PushColor(ImGuiCol.Text, Palette.MutedInk))
        {
            ImGui.TextUnformatted(label);
        }

        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = (multiline ? Metrics.Size.FieldMultiline : Metrics.Size.FieldHeight) * scale;
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
                ImGui.InputTextMultiline(id, ref value, maxLength,
                    new Vector2(width - Metrics.Space.Md * 2f * scale, height - Metrics.Space.Lg * scale),
                    ImGuiInputTextFlags.None);
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
            ImGui.TextUnformatted(label);
        }

        ImGui.PushTextWrapPos(0f);
        using (ImRaii.PushColor(ImGuiCol.Text, Theme.TextStrong))
        {
            ImGui.TextWrapped(value);
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
            ImGui.TextUnformatted(label.ToUpperInvariant());
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
            ImGui.TextWrapped(text);
        }

        ImGui.PopTextWrapPos();
    }

    public void DrawActionTooltip(Vector2 iconCenter, float hitRadius, string text)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetForegroundDrawList();
        var textSize = Typography.Measure(text, 0.78f, FontWeight.Medium);
        var padX = 9f * scale;
        var padY = 5f * scale;
        var bubbleSize = new Vector2(textSize.X + padX * 2f, textSize.Y + padY * 2f);
        var gap = 9f * scale;
        var windowMin = ImGui.GetWindowPos();
        var windowMax = windowMin + ImGui.GetWindowSize();
        var minBoundX = windowMin.X + 4f * scale;
        var maxBoundX = windowMax.X - bubbleSize.X - 4f * scale;
        if (minBoundX > maxBoundX)
        {
            return;
        }

        var minX = Math.Clamp(iconCenter.X - bubbleSize.X * 0.5f, minBoundX, maxBoundX);
        var minY = iconCenter.Y - hitRadius - gap - bubbleSize.Y;
        if (minY < windowMin.Y + 4f * scale)
        {
            minY = iconCenter.Y + hitRadius + gap;
        }

        var min = new Vector2(minX, minY);
        var max = min + bubbleSize;
        var bubble = Core.Theme.Palette.WithAlpha(Core.Theme.Palette.Mix(Theme.AppBackground, Theme.TextStrong, 0.9f),
            0.97f);
        Squircle.Fill(drawList, min, max, bubbleSize.Y * 0.5f, ImGui.GetColorU32(bubble));
        Typography.Draw(drawList, new Vector2(min.X + padX, min.Y + padY), text, Theme.AppBackground, 0.78f,
            FontWeight.Medium);
    }

    public static void Icon(Vector2 center, string glyph, Vector4 color, float scale)
    {
        float fontSize;
        Vector2 size;
        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            fontSize = ImGui.GetFontSize() * scale;
            size = ImGui.CalcTextSize(glyph) * scale;
        }

        ImGui.GetWindowDrawList().AddText(UiBuilder.IconFont, fontSize, center - size * 0.5f, ImGui.GetColorU32(color),
            glyph, 0f);
    }
}
