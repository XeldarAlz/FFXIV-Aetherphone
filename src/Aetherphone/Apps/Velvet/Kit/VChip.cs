using System.Numerics;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Apps.Velvet.Kit;

internal enum VChipStyle
{
    Solid,
    Tint,
    Outline,
    Ghost,
}

internal readonly record struct VChipModel(
    string Label,
    VChipStyle Style,
    Vector4 Tone,
    FontAwesomeIcon? Icon = null,
    bool Removable = false);

internal static class VChip
{
    public const float Height = 30f;

    public static float Width(string label, bool hasIcon, bool removable, float scale)
    {
        var textSize = Typography.Measure(label, TextStyles.Footnote);
        var width = textSize.X + Metrics.Space.Md * 2f * scale;
        if (hasIcon)
        {
            width += 20f * scale;
        }

        if (removable)
        {
            width += 16f * scale;
        }

        return width;
    }

    public static bool Draw(Vector2 min, float height, in VChipModel chip, float scale)
    {
        var drawList = ImGui.GetWindowDrawList();
        var width = Width(chip.Label, chip.Icon.HasValue, chip.Removable, scale);
        var max = new Vector2(min.X + width, min.Y + height);
        var radius = height * 0.5f;
        var centerY = (min.Y + max.Y) * 0.5f;
        var hovered = UiInteract.Hover(min, max);

        Vector4 fill;
        Vector4 ink;
        switch (chip.Style)
        {
            case VChipStyle.Solid:
                fill = hovered ? Vector4.Lerp(chip.Tone, VelvetTheme.OnAccent, 0.12f) : chip.Tone;
                ink = VelvetTheme.OnAccent;
                break;
            case VChipStyle.Tint:
                fill = VelvetTheme.Alpha(chip.Tone, hovered ? 0.24f : 0.16f);
                ink = Vector4.Lerp(chip.Tone, VelvetTheme.OnAccent, 0.55f);
                break;
            case VChipStyle.Outline:
                fill = VelvetTheme.Alpha(chip.Tone, hovered ? 0.14f : 0.06f);
                ink = Vector4.Lerp(chip.Tone, VelvetTheme.OnAccent, 0.45f);
                break;
            default:
                fill = hovered ? VelvetTheme.Alpha(VelvetTheme.Moonlight, 0.10f) : VelvetTheme.PlumWell;
                ink = hovered ? VelvetTheme.TitleInk : VelvetTheme.BodyInk;
                break;
        }

        Squircle.Fill(drawList, min, max, radius, fill.Packed());
        if (chip.Style == VChipStyle.Outline)
        {
            Squircle.Stroke(drawList, min, max, radius, chip.Tone.Packed(), Metrics.Stroke.Thin * scale);
        }

        var cursorX = min.X + Metrics.Space.Md * scale;
        if (chip.Icon.HasValue)
        {
            AppSkin.Icon(new Vector2(cursorX + 6f * scale, centerY), chip.Icon.Value.ToIconString(), ink, 0.7f);
            cursorX += 20f * scale;
        }

        var textSize = Typography.Measure(chip.Label, TextStyles.Footnote);
        Typography.Draw(new Vector2(cursorX, centerY - textSize.Y * 0.5f), chip.Label, ink, TextStyles.Footnote);

        if (chip.Removable)
        {
            AppSkin.Icon(new Vector2(max.X - 12f * scale, centerY), FontAwesomeIcon.Times.ToIconString(), ink, 0.62f);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }
}

internal static class VChipFlow
{
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
            var width = VChip.Width(chip.Label, chip.Icon.HasValue, chip.Removable, scale);
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
