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
        var scale = ImGuiHelpers.GlobalScale;
        var width = Metrics.Size.ToggleWidth * scale;
        var height = Metrics.Size.ToggleHeight * scale;
        var min = new Vector2(row.Max.X - width, row.Center.Y - height * 0.5f);
        var labelMaxWidth = MathF.Max(1f, min.X - 10f * scale - row.Min.X);
        var labelSize = Typography.Measure(label, TextStyles.BodyEmphasized);
        Marquee.DrawLeftAuto(label, label, row.Min.X, row.Center.Y - labelSize.Y * 0.5f, labelMaxWidth,
            TextStyles.BodyEmphasized, theme.TextStrong);
        return Toggle.Draw(label, new Rect(min, min + new Vector2(width, height)), value, theme);
    }

    public static void Info(Rect row, string label, string value, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var gap = 12f * scale;
        var available = row.Width - gap;
        var labelCap = MathF.Max(1f, available * 0.5f);
        var labelSize = Typography.Measure(label, TextStyles.BodyEmphasized);
        var labelY = row.Center.Y - labelSize.Y * 0.5f;
        var labelHovered = ImGui.IsMouseHoveringRect(new Vector2(row.Min.X, row.Min.Y),
            new Vector2(row.Min.X + labelCap, row.Max.Y));
        var labelWidth = Marquee.DrawLeft(label, label, row.Min.X, labelY, labelCap, TextStyles.BodyEmphasized,
            theme.TextStrong, labelHovered);
        var valueMaxWidth = MathF.Max(1f, available - labelWidth);
        var valueSize = Typography.Measure(value, TextStyles.Body);
        var valueY = row.Center.Y - valueSize.Y * 0.5f;
        var valueHovered = ImGui.IsMouseHoveringRect(new Vector2(row.Max.X - valueMaxWidth, row.Min.Y),
            new Vector2(row.Max.X, row.Max.Y));
        Marquee.DrawRight(label + ":infoValue", value, row.Max.X, valueY, valueMaxWidth, TextStyles.Body,
            theme.TextMuted, valueHovered);
    }

    public static bool Link(Rect row, FontAwesomeIcon icon, Vector4 tint, string label, string value, PhoneTheme theme,
        bool badge = false)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var hovered = UiInteract.Hover(row.Min, row.Max);
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

        var labelStartX = tileMax.X + Metrics.Space.Md * scale;
        var chevronWidth = Metrics.Space.Xs * scale;
        var chevronTip = new Vector2(row.Max.X, row.Center.Y);
        var chevronGap = 12f * scale;
        var midGap = 8f * scale;
        var available = chevronTip.X - chevronWidth - chevronGap - labelStartX;
        DrawTwoColumnText(row, label, value, theme, labelStartX, chevronTip.X - chevronWidth - chevronGap, available,
            midGap);
        DrawChevronRight(chevronTip, chevronWidth, 2.2f * scale, theme.TextMuted);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    public static bool AppLink(Rect row, string appId, Vector4 tint, string label, string value, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var hovered = UiInteract.Hover(row.Min, row.Max);
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

        var labelStartX = tileMax.X + Metrics.Space.Md * scale;
        var chevronWidth = Metrics.Space.Xs * scale;
        var chevronTip = new Vector2(row.Max.X, row.Center.Y);
        var chevronGap = 12f * scale;
        var midGap = 8f * scale;
        var available = chevronTip.X - chevronWidth - chevronGap - labelStartX;
        DrawTwoColumnText(row, label, value, theme, labelStartX, chevronTip.X - chevronWidth - chevronGap, available,
            midGap);
        DrawChevronRight(chevronTip, chevronWidth, 2.2f * scale, theme.TextMuted);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    public static bool Disclosure(Rect row, string label, string value, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            DrawRowHighlight(row, theme);
        }

        var chevronWidth = 6f * scale;
        var chevronTip = new Vector2(row.Max.X, row.Center.Y);
        var chevronGap = 12f * scale;
        var midGap = 8f * scale;
        var available = chevronTip.X - chevronWidth - chevronGap - row.Min.X;
        DrawTwoColumnText(row, label, value, theme, row.Min.X, chevronTip.X - chevronWidth - chevronGap, available,
            midGap);
        DrawChevronRight(chevronTip, chevronWidth, 2.2f * scale, theme.TextMuted);

        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    private static void DrawTwoColumnText(Rect row, string label, string value, PhoneTheme theme, float labelStartX,
        float valueBoxRight, float available, float midGap)
    {
        var labelCap = string.IsNullOrEmpty(value) ? available : MathF.Max(1f, (available - midGap) * 0.5f);
        var labelSize = Typography.Measure(label, TextStyles.BodyEmphasized);
        var labelY = row.Center.Y - labelSize.Y * 0.5f;
        var labelHovered = ImGui.IsMouseHoveringRect(new Vector2(labelStartX, row.Min.Y),
            new Vector2(labelStartX + labelCap, row.Max.Y));
        var labelWidth = Marquee.DrawLeft(label, label, labelStartX, labelY, labelCap, TextStyles.BodyEmphasized,
            theme.TextStrong, labelHovered);

        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var valueMaxWidth = MathF.Max(1f, valueBoxRight - labelStartX - labelWidth - midGap);
        var valueSize = Typography.Measure(value, TextStyles.Body);
        var valueY = row.Center.Y - valueSize.Y * 0.5f;
        var valueHovered = ImGui.IsMouseHoveringRect(new Vector2(valueBoxRight - valueMaxWidth, row.Min.Y),
            new Vector2(valueBoxRight, row.Max.Y));
        Marquee.DrawRight(label + ":value", value, valueBoxRight, valueY, valueMaxWidth, TextStyles.Body,
            theme.TextMuted, valueHovered);
    }

    public static bool Selectable(Rect row, string label, bool selected, PhoneTheme theme)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var hovered = UiInteract.Hover(row.Min, row.Max);
        if (hovered)
        {
            DrawRowHighlight(row, theme);
        }

        var checkWidth = 21f * scale;
        var labelMaxWidth = MathF.Max(1f, row.Width - checkWidth);
        var labelSize = Typography.Measure(label, TextStyles.BodyEmphasized);
        Marquee.DrawLeft(label, label, row.Min.X, row.Center.Y - labelSize.Y * 0.5f, labelMaxWidth,
            TextStyles.BodyEmphasized, theme.TextStrong, hovered);

        if (selected)
        {
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

        return UiInteract.Click(row.Min, row.Max, hovered);
    }

    public static bool Action(Rect row, string label, Vector4 color, PhoneTheme theme)
    {
        var hovered = UiInteract.Hover(row.Min, row.Max);
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

        return UiInteract.Click(row.Min, row.Max, hovered);
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

    private static void DrawChevronRight(Vector2 tip, float size, float thickness, Vector4 color)
    {
        var dl = ImGui.GetWindowDrawList();
        var packed = ImGui.GetColorU32(color);
        dl.AddLine(new Vector2(tip.X - size, tip.Y - size), tip, packed, thickness);
        dl.AddLine(tip, new Vector2(tip.X - size, tip.Y + size), packed, thickness);
    }
}
