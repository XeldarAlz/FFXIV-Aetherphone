using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class SettingsRow
{
    private static readonly Vector4 GlyphInk = new(1f, 1f, 1f, 1f);

    public static bool Bool(Rect row, string label, bool value, PhoneTheme theme)
    {
        DrawLabel(row, label, theme.TextStrong);
        var scale = ImGuiHelpers.GlobalScale;
        var width = Metrics.Size.ToggleWidth * scale;
        var height = Metrics.Size.ToggleHeight * scale;
        var min = new Vector2(row.Max.X - width, row.Center.Y - height * 0.5f);
        return Toggle.Draw(label, new Rect(min, min + new Vector2(width, height)), value, theme);
    }

    public static void Info(Rect row, string label, string value, PhoneTheme theme)
    {
        DrawLabel(row, label, theme.TextStrong);
        var valueSize = Typography.Measure(value);
        Typography.Draw(new Vector2(row.Max.X - valueSize.X, row.Center.Y - valueSize.Y * 0.5f), value,
            theme.TextMuted);
    }

    public static bool Link(Rect row, FontAwesomeIcon icon, Vector4 tint, string label, string value, PhoneTheme theme,
        bool badge = false)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var hovered = ImGui.IsMouseHoveringRect(row.Min, row.Max);
        var dl = ImGui.GetWindowDrawList();
        if (hovered)
        {
            DrawRowHighlight(row, theme);
        }

        var tileSize = Metrics.Size.IconTile * scale;
        var tileMin = new Vector2(row.Min.X, row.Center.Y - tileSize * 0.5f);
        var tileMax = tileMin + new Vector2(tileSize, tileSize);
        var surface = hovered ? Palette.Lighten(tint, 0.08f) : tint;
        IconTile.FillShaded(dl, tileMin, tileMax, tileSize * Metrics.Radius.TileFactor, surface);
        ProgressRing.CenterIcon(dl, new Vector2(tileMin.X + tileSize * 0.5f, row.Center.Y), icon, GlyphInk,
            tileSize * 0.5f);
        if (badge)
        {
            AppBadge.DrawDot(new Vector2(tileMax.X, tileMin.Y), theme, scale);
        }
        DrawLabel(new Rect(new Vector2(tileMax.X + Metrics.Space.Md * scale, row.Min.Y), row.Max), label,
            theme.TextStrong);
        var chevronWidth = Metrics.Space.Xs * scale;
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

    public static bool AppLink(Rect row, string appId, Vector4 tint, string label, string value, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var hovered = ImGui.IsMouseHoveringRect(row.Min, row.Max);
        var dl = ImGui.GetWindowDrawList();
        if (hovered)
        {
            DrawRowHighlight(row, theme);
        }

        var tileSize = 30f * scale;
        var tileMin = new Vector2(row.Min.X, row.Center.Y - tileSize * 0.5f);
        var tileMax = tileMin + new Vector2(tileSize, tileSize);
        var tileFill = hovered ? Palette.Mix(tint, theme.TextStrong, 0.14f) : tint;
        Squircle.Fill(dl, tileMin, tileMax, tileSize * Metrics.Radius.TileFactor, ImGui.GetColorU32(tileFill));
        var iconCenter = (tileMin + tileMax) * 0.5f;
        var hole = Palette.Mix(tint, new Vector4(0f, 0f, 0f, 1f), 0.25f);
        if (!AppIconArt.TryDraw(dl, appId, iconCenter, tileSize * 0.98f, theme.TextStrong, hole))
        {
            dl.AddCircleFilled(iconCenter, 4f * scale, ImGui.GetColorU32(theme.TextStrong), 16);
        }

        DrawLabel(new Rect(new Vector2(tileMax.X + Metrics.Space.Md * scale, row.Min.Y), row.Max), label,
            theme.TextStrong);
        var chevronWidth = Metrics.Space.Xs * scale;
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
        var hovered = ImGui.IsMouseHoveringRect(row.Min, row.Max);
        if (hovered)
        {
            DrawRowHighlight(row, theme);
        }

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

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public static bool Selectable(Rect row, string label, bool selected, PhoneTheme theme)
    {
        var hovered = ImGui.IsMouseHoveringRect(row.Min, row.Max);
        if (hovered)
        {
            DrawRowHighlight(row, theme);
        }

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

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    public static bool Action(Rect row, string label, Vector4 color, PhoneTheme theme)
    {
        var hovered = ImGui.IsMouseHoveringRect(row.Min, row.Max);
        if (hovered)
        {
            DrawRowHighlight(row, theme);
        }

        var labelSize = Typography.Measure(label, TextStyles.BodyEmphasized);
        Typography.Draw(new Vector2(row.Center.X - labelSize.X * 0.5f, row.Center.Y - labelSize.Y * 0.5f), label,
            color, TextStyles.BodyEmphasized);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static void DrawRowHighlight(Rect row, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var pressed = ImGui.IsMouseDown(ImGuiMouseButton.Left);
        var min = new Vector2(row.Min.X - 10f * scale, row.Min.Y + 3f * scale);
        var max = new Vector2(row.Max.X + 10f * scale, row.Max.Y - 3f * scale);
        var alpha = pressed ? 0.10f : 0.05f;
        Squircle.Fill(ImGui.GetWindowDrawList(), min, max, 8f * scale,
            ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, alpha)));
    }

    private static void DrawLabel(Rect row, string label, Vector4 color)
    {
        var labelSize = Typography.Measure(label, TextStyles.BodyEmphasized);
        Typography.Draw(new Vector2(row.Min.X, row.Center.Y - labelSize.Y * 0.5f), label, color,
            TextStyles.BodyEmphasized);
    }

    private static void DrawChevronRight(Vector2 tip, float size, float thickness, Vector4 color)
    {
        var dl = ImGui.GetWindowDrawList();
        var packed = ImGui.GetColorU32(color);
        dl.AddLine(new Vector2(tip.X - size, tip.Y - size), tip, packed, thickness);
        dl.AddLine(tip, new Vector2(tip.X - size, tip.Y + size), packed, thickness);
    }
}
