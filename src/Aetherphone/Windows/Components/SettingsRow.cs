using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class SettingsRow
{
    public static bool Bool(Rect row, string label, bool value, PhoneTheme theme)
    {
        DrawLabel(row, label, theme.TextStrong);

        var scale = ImGuiHelpers.GlobalScale;
        var width = 46f * scale;
        var height = 28f * scale;
        var min = new Vector2(row.Max.X - width, row.Center.Y - height * 0.5f);
        return Toggle.Draw(new Rect(min, min + new Vector2(width, height)), value, theme);
    }

    public static void Info(Rect row, string label, string value, PhoneTheme theme)
    {
        DrawLabel(row, label, theme.TextStrong);

        var valueSize = Typography.Measure(value);
        Typography.Draw(new Vector2(row.Max.X - valueSize.X, row.Center.Y - valueSize.Y * 0.5f), value, theme.TextMuted);
    }

    public static bool Link(Rect row, string glyph, Vector4 tint, string label, string value, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var hovered = ImGui.IsMouseHoveringRect(row.Min, row.Max);
        var dl = ImGui.GetWindowDrawList();

        var tileSize = 28f * scale;
        var tileMin = new Vector2(row.Min.X, row.Center.Y - tileSize * 0.5f);
        var tileFill = hovered ? Palette.Mix(tint, theme.TextStrong, 0.14f) : tint;
        Squircle.Fill(dl, tileMin, tileMin + new Vector2(tileSize, tileSize), tileSize * 0.28f, ImGui.GetColorU32(tileFill));

        var glyphHeight = Typography.Measure(glyph).Y;
        var glyphScale = glyphHeight > 0f ? tileSize * 0.5f / glyphHeight : 1f;
        Typography.DrawCentered(new Vector2(tileMin.X + tileSize * 0.5f, row.Center.Y), glyph, theme.TextStrong, glyphScale);

        DrawLabel(new Rect(new Vector2(tileMin.X + tileSize + 12f * scale, row.Min.Y), row.Max), label, theme.TextStrong);

        var chevronWidth = 6f * scale;
        var chevronTip = new Vector2(row.Max.X, row.Center.Y);
        DrawChevronRight(chevronTip, chevronWidth, 2.2f * scale, theme.TextMuted);

        if (!string.IsNullOrEmpty(value))
        {
            var valueSize = Typography.Measure(value);
            var valueX = chevronTip.X - chevronWidth - 12f * scale - valueSize.X;
            Typography.Draw(new Vector2(valueX, row.Center.Y - valueSize.Y * 0.5f), value, theme.TextMuted);
        }

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public static bool Disclosure(Rect row, string label, string value, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        DrawLabel(row, label, theme.TextStrong);

        var chevronWidth = 6f * scale;
        var chevronTip = new Vector2(row.Max.X, row.Center.Y);
        DrawChevronRight(chevronTip, chevronWidth, 2.2f * scale, theme.TextMuted);

        if (!string.IsNullOrEmpty(value))
        {
            var valueSize = Typography.Measure(value);
            var valueX = chevronTip.X - chevronWidth - 12f * scale - valueSize.X;
            Typography.Draw(new Vector2(valueX, row.Center.Y - valueSize.Y * 0.5f), value, theme.TextMuted);
        }

        var hovered = ImGui.IsMouseHoveringRect(row.Min, row.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public static bool Selectable(Rect row, string label, bool selected, PhoneTheme theme)
    {
        DrawLabel(row, label, theme.TextStrong);

        if (selected)
        {
            var scale = ImGuiHelpers.GlobalScale;
            var dl = ImGui.GetWindowDrawList();
            var tip = new Vector2(row.Max.X - 12f * scale, row.Center.Y + 5f * scale);
            var color = ImGui.GetColorU32(theme.Accent);
            dl.AddLine(tip - new Vector2(5f * scale, 5f * scale), tip, color, 2f * scale);
            dl.AddLine(tip, new Vector2(tip.X + 9f * scale, tip.Y - 11f * scale), color, 2f * scale);
        }

        var hovered = ImGui.IsMouseHoveringRect(row.Min, row.Max);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static void DrawLabel(Rect row, string label, Vector4 color)
    {
        var labelSize = Typography.Measure(label);
        Typography.Draw(new Vector2(row.Min.X, row.Center.Y - labelSize.Y * 0.5f), label, color);
    }

    private static void DrawChevronRight(Vector2 tip, float size, float thickness, Vector4 color)
    {
        var dl = ImGui.GetWindowDrawList();
        var packed = ImGui.GetColorU32(color);
        dl.AddLine(new Vector2(tip.X - size, tip.Y - size), tip, packed, thickness);
        dl.AddLine(tip, new Vector2(tip.X - size, tip.Y + size), packed, thickness);
    }
}
