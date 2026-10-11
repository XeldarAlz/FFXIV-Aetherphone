using Aetherphone.Core;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace Aetherphone.Apps.Casino.Stage;

internal readonly record struct IconPillResult(bool Pressed, Vector2 IconCenter, Vector4 Ink);

internal static class DeckActions
{
    public const float Pad = 12f;
    public const float Gap = 8f;
    public const float PrimaryHeight = 56f;
    public const float SecondaryHeight = 44f;
    public const float PillPadX = 14f;
    public const float PillMinWidth = 44f;
    public const float PrimaryMinWidth = 140f;
    public const float PrimaryFloorWidth = 88f;
    public const float IconSize = 18f;
    public const float IconGap = 6f;
    public const int MaxSecondary = 2;

    private const string PrimaryId = "casino.deck.primary";

    public static Rect Row(Rect deck, float scale)
    {
        var pad = Pad * scale;
        var bottom = deck.Max.Y - pad;
        var top = MathF.Max(deck.Min.Y + pad, bottom - PrimaryHeight * scale);
        return new Rect(new Vector2(deck.Min.X + pad, top), new Vector2(deck.Max.X - pad, bottom));
    }

    public static Rect Above(Rect row, float height, float scale)
    {
        var bottom = row.Min.Y - Gap * scale;
        return new Rect(new Vector2(row.Min.X, bottom - height), new Vector2(row.Max.X, bottom));
    }

    public static Rect Slice(Rect row, int index, int count, float scale)
    {
        var gap = Gap * scale;
        var width = (row.Width - gap * (count - 1)) / count;
        var left = row.Min.X + index * (width + gap);
        return new Rect(new Vector2(left, row.Min.Y), new Vector2(left + width, row.Max.Y));
    }

    public static bool DrawPrimary(Rect rect, string label, in ControlInk ink, bool enabled, string id) =>
        Button.Draw(rect, label, ink, ButtonStyle.Prominent, enabled: enabled, id: id);

    public static bool DrawSecondary(Rect rect, string label, in ControlInk ink, bool enabled, string id) =>
        Button.Draw(rect, label, ink, ButtonStyle.Gray, enabled: enabled, id: id);

    public static float PillWidth(float labelWidth, float scale) =>
        MathF.Max(labelWidth + PillPadX * 2f * scale, PillMinWidth * scale);

    public static float PillsWidth(ReadOnlySpan<float> labelWidths, float scale)
    {
        var total = 0f;
        for (var index = 0; index < labelWidths.Length; index++)
        {
            total += PillWidth(labelWidths[index], scale) + (index > 0 ? Gap * scale : 0f);
        }

        return total;
    }

    public static Rect Secondary(Rect row, float cursor, float width, float scale)
    {
        var left = Math.Clamp(cursor, row.Min.X, row.Max.X);
        var limit = MathF.Max(left, row.Max.X - (PrimaryFloorWidth + Gap) * scale);
        var right = MathF.Min(left + width, limit);
        return new Rect(new Vector2(left, row.Min.Y), new Vector2(right, row.Max.Y));
    }

    public static float Advance(Rect taken, float scale) => taken.Max.X + Gap * scale;

    public static Rect Primary(Rect row, float cursor) =>
        new(new Vector2(MathF.Min(MathF.Max(row.Min.X, cursor), row.Max.X), row.Min.Y), row.Max);

    public static bool DrawSecondary(Rect row, ref float cursor, ref int count, string label, bool enabled,
        in ControlInk ink, float scale)
    {
        if (count >= MaxSecondary)
        {
            return false;
        }

        var width = PillWidth(Typography.Measure(label, Button.LabelStyle(row.Height)).X, scale);
        var rect = Secondary(row, cursor, width, scale);
        cursor = Advance(rect, scale);
        count++;
        return DrawPill(rect, label, ink, ButtonStyle.Gray, enabled);
    }

    public static bool DrawPrimary(Rect row, float cursor, string label, bool enabled, in ControlInk ink,
        ButtonStyle style = ButtonStyle.Prominent, string? id = null) =>
        Button.Draw(Primary(row, cursor), label, ink, style, enabled: enabled, id: id ?? PrimaryId);

    public static bool DrawPill(Rect rect, string label, in ControlInk ink, ButtonStyle style, bool enabled,
        string? id = null, bool overlay = false)
    {
        var drawList = ImGui.GetWindowDrawList();
        var hovered = enabled && (overlay
            ? UiInteract.HoverWindowOnly(rect.Min, rect.Max)
            : UiInteract.Hover(rect.Min, rect.Max));
        var face = Button.Surface(drawList, rect, ink, style, ButtonRole.Normal, enabled, hovered,
            ImGui.GetID(id ?? label));
        DrawPillLabel(drawList, face, rect, label);
        return enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
    }

    public static void DrawPillLabel(ImDrawListPtr drawList, in ButtonFace face, Rect rest, string label)
    {
        var style = Button.LabelStyle(rest.Height);
        var room = MathF.Max(1f, rest.Width - PillPadX * 2f * UiScale.Current);
        Typography.DrawCentered(drawList, face.Face.Center, Typography.FitText(label, room, style), face.LabelInk,
            style);
    }

    public static float IconPillWidth(float labelWidth, float rowHeight, float scale) =>
        labelWidth > 0f ? (PillPadX * 2f + IconSize + IconGap) * scale + labelWidth : rowHeight;

    public static IconPillResult DrawIconPill(Rect rect, string glyph, string label, in ControlInk ink,
        ButtonStyle style, bool enabled, bool interactive, string id, string tooltip)
    {
        var drawList = ImGui.GetWindowDrawList();
        var scale = UiScale.Current;
        var hovered = interactive && enabled && UiInteract.Hover(rect.Min, rect.Max);
        var face = Button.Surface(drawList, rect, ink, style, ButtonRole.Normal, enabled, hovered, ImGui.GetID(id));
        var labelStyle = Button.LabelStyle(rect.Height);
        var icon = IconSize * scale;
        var labelSize = label.Length > 0 ? Typography.Measure(label, labelStyle) : Vector2.Zero;
        var content = label.Length > 0 ? icon + IconGap * scale + labelSize.X : icon;
        var center = face.Face.Center;
        var iconCenter = new Vector2(center.X - content * 0.5f + icon * 0.5f, center.Y);
        PhoneIcon.Draw(drawList, iconCenter, glyph, face.LabelInk, icon);
        if (label.Length > 0)
        {
            Typography.Draw(drawList,
                new Vector2(iconCenter.X + icon * 0.5f + IconGap * scale, center.Y - labelSize.Y * 0.5f), label,
                face.LabelInk, labelStyle);
        }

        if (hovered && tooltip.Length > 0)
        {
            HoverTooltip.Show(rect, tooltip, HoverLabelSide.Above);
        }

        var pressed = interactive && enabled && UiInteract.Click(rect.Min, rect.Max, hovered);
        return new IconPillResult(pressed, iconCenter, face.LabelInk);
    }

    public static int Rail(ChipRail rail, Rect row, AppSkin ui, ReadOnlySpan<string> labels,
        ReadOnlySpan<bool> active, bool interactive = true, bool overlay = false) =>
        rail.Draw(row, ui, labels, active, overlay, null, PillPadX * 2f, false, interactive,
            row.Height / MathF.Max(UiScale.Current, 0.0001f));
}
