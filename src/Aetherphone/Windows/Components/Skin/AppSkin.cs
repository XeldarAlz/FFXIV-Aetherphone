using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;

namespace Aetherphone.Windows.Components;

internal sealed class AppSkin
{
    public static readonly Vector4 Transparent = new(0f, 0f, 0f, 0f);
    public const float PillHeight = Metrics.Size.Pill;

    private static readonly Vector4 White = new(1f, 1f, 1f, 1f);
    private const float PillLabelMinScale = 0.70f;
    private const float StackedPillInsetFraction = 0.5f;
    private const float SubLabelAlpha = 0.72f;
    private const float GlyphHoverMix = 0.2f;

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

    public Vector4 Hairline => Palette.Hairline;

    public Vector4 HoverWash => Palette.HoverWash;

    public Vector4 BackdropColor
    {
        get
        {
            var body = Palette.BackdropBottom with { W = 1f };
            var bloom = Palette.BloomBottom;
            return Vector4.Lerp(body, bloom with { W = 1f }, Math.Clamp(bloom.W, 0f, 1f));
        }
    }

    public void Backdrop(Rect screen)
    {
        var scale = UiScale.Current;
        AppSurface.ScrollbarInk = Palette.TitleInk;
        PaintGradient(ImGui.GetWindowDrawList(), screen, screen, Theme.ScreenRounding * scale);
        WallpaperBackdrop.RecordAppGround(screen, GroundColor(Palette.BackdropTop, Palette.BloomTop),
            GroundColor(Palette.BackdropBottom, Palette.BloomBottom));
    }

    private static Vector4 GroundColor(Vector4 body, Vector4 bloom) =>
        Vector4.Lerp(body with { W = 1f }, bloom with { W = 1f }, Math.Clamp(bloom.W, 0f, 1f));

    public void Body(Rect area)
    {
        var frame = SceneChrome.ScreenFrom(area, Theme, UiScale.Current);
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

    public void Card(ImDrawListPtr drawList, Vector2 min, Vector2 max, float rounding)
    {
        Squircle.Fill(drawList, min, max, rounding, ImGui.GetColorU32(Palette.CardFill));
        Squircle.Stroke(drawList, min, max, rounding, ImGui.GetColorU32(Palette.CardStroke), 1f);
    }

    public ControlInk Ink => ControlInk.From(this);

    public bool PillButton(Rect rect, string label, bool filled, string? id = null) =>
        Button.Draw(rect, label, Ink, filled ? ButtonStyle.Prominent : ButtonStyle.Gray, id: id);

    public bool PillButton(Rect rect, string label, bool filled, bool enabled, bool overlay = false) =>
        Button.Draw(rect, label, Ink, filled ? ButtonStyle.Prominent : ButtonStyle.Gray, enabled: enabled,
            overlay: overlay);

    public static bool PillButton(Rect rect, string label, bool filled, PhoneTheme theme) =>
        Button.Draw(rect, label, ControlInk.From(theme), filled ? ButtonStyle.Prominent : ButtonStyle.Gray);

    public static bool PillButton(Rect rect, string label, bool filled, bool enabled, PhoneTheme theme,
        bool overlay = false) =>
        Button.Draw(rect, label, ControlInk.From(theme), filled ? ButtonStyle.Prominent : ButtonStyle.Gray,
            enabled: enabled, overlay: overlay);

    public static bool StackedPillButton(Rect rect, string label, string subLabel, bool filled, bool enabled,
        PhoneTheme theme) =>
        StackedPillButton(rect, label, subLabel, filled, enabled, ControlInk.From(theme));

    public static bool StackedPillButton(Rect rect, string label, string subLabel, bool filled, bool enabled,
        in ControlInk ink)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = enabled && UiInteract.Hover(rect.Min, rect.Max);
        var face = Button.Surface(drawList, rect, ink,
            filled ? ButtonStyle.Prominent : ButtonStyle.Gray, ButtonRole.Normal, enabled, hovered,
            ImGui.GetID(label));
        var labelInk = face.LabelInk;
        var area = face.Face;
        var maxLabelWidth = MathF.Max(1f, area.Width - area.Height * StackedPillInsetFraction);
        var labelStyle = Button.LabelStyle(area.Height);
        var labelScale = Typography.FitScale(label, maxLabelWidth, labelStyle.Scale, PillLabelMinScale,
            labelStyle.Weight);
        var fittedLabel = Typography.FitText(label, maxLabelWidth, labelScale, labelStyle.Weight);
        var labelSize = Typography.Measure(fittedLabel, labelScale, labelStyle.Weight);
        if (subLabel.Length == 0)
        {
            Typography.Draw(drawList, area.Center - labelSize * 0.5f, fittedLabel, labelInk, labelScale, labelStyle.Weight);
        }
        else
        {
            var fittedSub = Typography.FitText(subLabel, maxLabelWidth, TextStyles.Caption1);
            var subSize = Typography.Measure(fittedSub, TextStyles.Caption1);
            var top = area.Center.Y - (labelSize.Y + subSize.Y) * 0.5f;
            Typography.Draw(drawList, new Vector2(area.Center.X - labelSize.X * 0.5f, top), fittedLabel, labelInk,
                labelScale, labelStyle.Weight);
            Typography.Draw(drawList, new Vector2(area.Center.X - subSize.X * 0.5f, top + labelSize.Y), fittedSub,
                Core.Theme.Palette.WithAlpha(labelInk, labelInk.W * SubLabelAlpha), TextStyles.Caption1);
        }

        return enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public bool FlowChip(ref float cursorX, float centerY, float gap, string label, bool active) =>
        FlowChipCore(ref cursorX, centerY, gap, label, active, Ink);

    public static bool FlowChip(ref float cursorX, float centerY, float gap, string label, bool active,
        PhoneTheme theme) =>
        FlowChipCore(ref cursorX, centerY, gap, label, active, ControlInk.From(theme));

    private static bool FlowChipCore(ref float cursorX, float centerY, float gap, string label, bool active,
        in ControlInk ink)
    {
        var scale = UiScale.Current;
        var height = ChipRail.ChipHeight * scale;
        var width = Typography.Measure(label, ChipRail.LabelStyle).X + ChipRail.ChipPadX * 2f * scale;
        var rect = new Rect(new Vector2(cursorX, centerY - height * 0.5f),
            new Vector2(cursorX + width, centerY + height * 0.5f));
        cursorX = rect.Max.X + gap;
        return ChipCore(rect, label, active, ink);
    }

    public bool ActionPill(Rect rect, string label, bool enabled, in TextStyle style) =>
        Button.Draw(rect, label, Ink, enabled: enabled);

    public bool AccentPill(Rect rect, string label, bool enabled, in TextStyle style) =>
        Button.Draw(rect, label, Ink, enabled: enabled);

    public void PaintAccentPill(Rect rect, string label, bool enabled, bool hovered, in TextStyle style)
    {
        var drawList = ImGui.GetWindowDrawList();
        var face = Button.Surface(drawList, rect, Ink, ButtonStyle.Prominent, ButtonRole.Normal, enabled, hovered,
            ImGui.GetID(label));
        Button.DrawLabel(drawList, face, label);
    }

    public bool DangerPillButton(Rect rect, string label) => DangerPillButton(rect, label, Theme);

    public static bool DangerPillButton(Rect rect, string label, PhoneTheme theme) =>
        Button.Draw(rect, label, ControlInk.From(theme), ButtonStyle.Prominent, ButtonRole.Destructive);

    public bool DangerGhostButton(Rect rect, string label) =>
        Button.Draw(rect, label, Ink, ButtonStyle.Tinted, ButtonRole.Destructive);

    public bool GhostButton(Rect rect, string label) => Button.Draw(rect, label, Ink, ButtonStyle.Gray);

    public static bool GhostButton(Rect rect, string label, PhoneTheme theme) =>
        Button.Draw(rect, label, ControlInk.From(theme), ButtonStyle.Gray);

    public bool IconButton(Vector2 center, float hitRadius, string glyph, Vector4 color, Vector4 background,
        float glyphScale, string tooltip = "", HoverLabelSide tooltipSide = HoverLabelSide.Above) =>
        IconButtonCore(center, hitRadius, glyph, color, background, glyphScale, Ink, tooltip, tooltipSide);

    public static bool IconButton(Vector2 center, float hitRadius, string glyph, Vector4 color, Vector4 background,
        float glyphScale, PhoneTheme theme, string tooltip = "", HoverLabelSide tooltipSide = HoverLabelSide.Above) =>
        IconButtonCore(center, hitRadius, glyph, color, background, glyphScale, ControlInk.From(theme), tooltip,
            tooltipSide);

    private static bool IconButtonCore(Vector2 center, float hitRadius, string glyph, Vector4 color,
        Vector4 background, float glyphScale, in ControlInk ink, string tooltip, HoverLabelSide tooltipSide)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hit = new Vector2(hitRadius, hitRadius);
        var rect = new Rect(center - hit, center + hit);
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        var grow = 1f;
        var glyphColor = hovered ? Core.Theme.Palette.Mix(color, ink.Ink, GlyphHoverMix) : color;
        if (background.W > 0f)
        {
            var face = RoundButton.Surface(drawList, rect, ink, ButtonStyle.Gray, true, hovered,
                ImGui.GetID(tooltip.Length > 0 ? tooltip : glyph), background);
            grow = face.Face.Width / MathF.Max(rect.Width, 0.0001f);
            glyphColor = color;
        }
        else if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        Icon(drawList, center, glyph, glyphColor, glyphScale * grow);
        HoverTooltip.Show(rect, tooltip, tooltipSide);
        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public bool Chip(Rect rect, string label, bool active) => ChipCore(rect, label, active, Ink);

    public static bool Chip(Rect rect, string label, bool active, PhoneTheme theme) =>
        ChipCore(rect, label, active, ControlInk.From(theme));

    private static bool ChipCore(Rect rect, string label, bool active, in ControlInk ink)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = UiInteract.Hover(rect.Min, rect.Max);
        ChipRail.PaintChip(drawList, rect, label, active, hovered, ink);
        return UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public void ToggleRow(string label, ref bool value)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = 34f * scale;
        var trackWidth = Metrics.Size.ToggleWidth * scale;
        var trackHeight = Metrics.Size.ToggleHeight * scale;
        var labelMaxWidth = width - trackWidth - Metrics.Space.Md * scale;
        var labelHeight = Typography.LineHeight(TextStyles.Body);
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(origin.X, origin.Y + (height - labelHeight) * 0.5f),
            Typography.FitText(label, labelMaxWidth, TextStyles.Body), TitleInk, TextStyles.Body);
        var trackMin = new Vector2(origin.X + width - trackWidth, origin.Y + (height - trackHeight) * 0.5f);
        Toggle.Draw(label, new Rect(trackMin, trackMin + new Vector2(trackWidth, trackHeight)), value, Theme, 1f,
            false);
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

    public void Field(string label, string id, ref string value, int maxLength, bool multiline, float heightUnscaled,
        ImGuiInputTextFlags extraFlags = ImGuiInputTextFlags.None)
    {
        var scale = UiScale.Current;
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
                ImGui.InputText(id, ref value, maxLength, extraFlags);
            }
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, height));
    }

    public static float PillWidthFor(string label, float height) =>
        Typography.Measure(label, Button.LabelStyle(height)).X + height;

    public static float HeaderActionWidth(string label)
    {
        var scale = UiScale.Current;
        return PillWidthFor(label, Button.SmallHeight * scale) + Metrics.Space.Xs * scale;
    }

    public bool HeaderAction(Rect area, string label, bool enabled)
    {
        var scale = UiScale.Current;
        var height = Button.SmallHeight * scale;
        var width = HeaderActionWidth(label);
        var max = new Vector2(area.Max.X - Metrics.Space.Md * scale,
            area.Min.Y + AppHeader.Height * scale * 0.5f + height * 0.5f);
        var min = new Vector2(max.X - width, max.Y - height);
        return Button.Draw(new Rect(min, max), label, Ink, ButtonStyle.Tinted, enabled: enabled);
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

    public void SectionLabel(string label) => SectionLabel(label, TextStyles.FootnoteEmphasized, Metrics.Space.Xxs);

    public void SectionLabel(string label, in TextStyle style, float gapPixels)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        ListSection.PaintOverline(ImGui.GetWindowDrawList(), origin, label, Palette.MutedInk, width);
        ImGui.Dummy(new Vector2(width, ListSection.OverlineHeight + gapPixels * scale));
    }

    public void SectionHeading(string label, float topPadPixels = 0f)
    {
        var scale = UiScale.Current;
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var top = origin.Y + topPadPixels * scale;
        Typography.Draw(ImGui.GetWindowDrawList(), new Vector2(origin.X, top),
            Typography.FitText(label, width, TextStyles.Headline), Palette.HeadingInk, TextStyles.Headline);
        ImGui.Dummy(new Vector2(width,
            topPadPixels * scale + Typography.LineHeight(TextStyles.Headline) + Metrics.Space.Sm * scale));
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
        float targetSize;
        using (Plugin.Fonts.PushDalamudIcon())
        {
            targetSize = ImGui.GetFontSize() * scale;
        }

        using (Plugin.Fonts.PushIcon(targetSize, glyph))
        {
            var font = ImGui.GetFont();
            var ratio = targetSize / ImGui.GetFontSize();
            var size = ImGui.CalcTextSize(glyph) * ratio;
            drawList.AddText(font, targetSize, center - size * 0.5f, ImGui.GetColorU32(color), glyph, 0f);
        }
    }
}
