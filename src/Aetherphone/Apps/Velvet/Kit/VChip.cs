using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Velvet.Kit;

internal enum VChipStyle
{
    Solid,
    Tint,
    Ghost,
    Match,
}

internal readonly record struct VChipModel(
    string Label,
    VChipStyle Style,
    Vector4 Tone,
    string? Glyph = null,
    bool Removable = false);

internal static class VChip
{
    public const float Height = 32f;

    private const float PadX = 13f;
    private const float GlyphSlot = 20f;
    private const float GlyphInset = 6f;
    private const float RemoveSlot = 16f;
    private const float RemoveGlyph = 12f;
    private const float RemoveInset = 12f;
    private const float SolidHoverMix = 0.12f;
    private const float TintFill = 0.14f;
    private const float TintFillHover = 0.22f;
    private const float TintStroke = 0.32f;
    private const float GhostFill = 0.10f;
    private const float GhostFillHover = 0.16f;
    private const float GhostStroke = 0.16f;
    private const float MatchFill = 0.22f;
    private const float MatchFillHover = 0.30f;
    private const float MatchStroke = 0.85f;

    private static readonly TextStyle LabelStyle = TextStyles.Subheadline;

    public static float Width(string label, bool hasIcon, bool removable, float scale)
    {
        var textSize = Typography.Measure(label, LabelStyle);
        var width = textSize.X + PadX * 2f * scale;
        if (hasIcon)
        {
            width += GlyphSlot * scale;
        }

        if (removable)
        {
            width += RemoveSlot * scale;
        }

        return width;
    }

    public static bool Draw(Vector2 min, float height, in VChipModel chip, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = Width(chip.Label, chip.Glyph is not null, chip.Removable, scale);
        var max = new Vector2(min.X + width, min.Y + height);
        var radius = height * 0.5f;
        var centerY = (min.Y + max.Y) * 0.5f;
        var hovered = UiInteract.Hover(min, max);

        Vector4 fill;
        Vector4 ink;
        var stroke = VelvetTheme.Hairline;
        switch (chip.Style)
        {
            case VChipStyle.Solid:
                fill = hovered ? Vector4.Lerp(chip.Tone, VelvetTheme.OnAccent, SolidHoverMix) : chip.Tone;
                ink = VelvetTheme.OnAccent;
                break;
            case VChipStyle.Tint:
                fill = VelvetTheme.Alpha(chip.Tone, hovered ? TintFillHover : TintFill);
                stroke = VelvetTheme.Alpha(chip.Tone, TintStroke);
                ink = VelvetTheme.ToneInk(chip.Tone);
                break;
            case VChipStyle.Match:
                fill = VelvetTheme.Alpha(chip.Tone, hovered ? MatchFillHover : MatchFill);
                stroke = VelvetTheme.Alpha(chip.Tone, MatchStroke);
                ink = VelvetTheme.ToneInk(chip.Tone);
                break;
            default:
                fill = VelvetTheme.Alpha(VelvetTheme.Moonlight, hovered ? GhostFillHover : GhostFill);
                stroke = VelvetTheme.Alpha(VelvetTheme.Moonlight, GhostStroke);
                ink = hovered ? VelvetTheme.TitleInk : VelvetTheme.BodyInk;
                break;
        }

        Squircle.Fill(drawList, min, max, radius, fill.Packed());
        if (chip.Style != VChipStyle.Solid)
        {
            var strokeWidth = chip.Style == VChipStyle.Match ? Metrics.Stroke.Ring : Metrics.Stroke.Hairline;
            Squircle.Stroke(drawList, min, max, radius, stroke.Packed(), strokeWidth * scale);
        }

        var cursorX = min.X + PadX * scale;
        if (chip.Glyph is { } glyph)
        {
            PhoneIcon.Draw(drawList, new Vector2(cursorX + GlyphInset * scale, centerY), glyph, ink,
                VIcon.Chip * scale);
            cursorX += GlyphSlot * scale;
        }

        var textSize = Typography.Measure(chip.Label, LabelStyle);
        Typography.Draw(drawList, new Vector2(cursorX, centerY - textSize.Y * 0.5f), chip.Label, ink, LabelStyle);

        if (chip.Removable)
        {
            PhoneIcon.Draw(drawList, new Vector2(max.X - RemoveInset * scale, centerY), PhoneIcons.X, ink,
                RemoveGlyph * scale);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(min, max, hovered);
    }
}

internal static class VChipFlow
{
    public static float Measure(ReadOnlySpan<VChipModel> chips, float availableWidth, float scale)
    {
        var height = VChip.Height * scale;
        var rowGap = Metrics.Space.Sm * scale;
        var chipGap = Metrics.Space.Sm * scale;
        var x = 0f;
        var rows = 1;
        for (var index = 0; index < chips.Length; index++)
        {
            var chip = chips[index];
            var width = VChip.Width(chip.Label, chip.Glyph is not null, chip.Removable, scale);
            if (x + width > availableWidth && x > 0f)
            {
                x = 0f;
                rows++;
            }

            x += width + chipGap;
        }

        return chips.Length == 0 ? 0f : rows * height + (rows - 1) * rowGap;
    }

    public static int Draw(ReadOnlySpan<VChipModel> chips, float availableWidth, float scale)
    {
        var origin = ImGui.GetCursorScreenPos();
        var height = VChip.Height * scale;
        var rowGap = Metrics.Space.Sm * scale;
        var chipGap = Metrics.Space.Sm * scale;
        var x = origin.X;
        var y = origin.Y;
        var clicked = -1;

        for (var index = 0; index < chips.Length; index++)
        {
            var chip = chips[index];
            var width = VChip.Width(chip.Label, chip.Glyph is not null, chip.Removable, scale);
            if (x + width > origin.X + availableWidth && x > origin.X)
            {
                x = origin.X;
                y += height + rowGap;
            }

            if (VChip.Draw(new Vector2(x, y), height, chip, scale))
            {
                clicked = index;
            }

            x += width + chipGap;
        }

        var totalHeight = y + height - origin.Y;
        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(availableWidth, totalHeight));
        return clicked;
    }
}
