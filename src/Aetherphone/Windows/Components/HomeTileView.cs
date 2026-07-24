using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class HomeTileView
{
    private static readonly Vector4 GlyphInk = new(1f, 1f, 1f, 1f);

    public static void DrawApp(Vector2 center, float size, IPhoneApp app, PhoneTheme theme, float drawScale,
        float labelAlpha, bool showLabels, float labelWidth, float zoom = 1f)
    {
        var scale = ImGuiHelpers.GlobalScale * zoom;
        var dl = ImGui.GetWindowDrawList();
        var drawHalf = size * 0.5f * drawScale;
        var drawMin = new Vector2(center.X - drawHalf, center.Y - drawHalf);
        var drawMax = new Vector2(center.X + drawHalf, center.Y + drawHalf);
        var radius = size * 0.26f * drawScale;
        var surface = IconTile.Surface(app.Accent);
        Elevation.IconRest(dl, drawMin, drawMax, radius, scale);
        IconTile.FillShaded(dl, drawMin, drawMax, radius, surface);
        Material.EdgeSquircle(dl, drawMin, drawMax, radius, scale);
        if (!AppIconArt.TryDraw(app.Id, center, size * drawScale, GlyphInk, Palette.Darken(surface, 0.25f)))
        {
            var glyphHeight = Typography.Measure(app.Glyph).Y;
            var glyphScale = glyphHeight > 0f ? size * drawScale * 0.5f / glyphHeight : 1f;
            Typography.DrawCentered(center, app.Glyph, GlyphInk, glyphScale);
        }

        DrawLabel(center, size, app.DisplayName, theme, scale, labelAlpha, showLabels, labelWidth, zoom);
        if (app.BadgeCount > 0)
        {
            DrawBadge(center, size, app.BadgeCount, app.BadgeAsDot, theme, scale);
        }
    }

    public static void DrawFolder(Vector2 center, float size, HomeTile folder, PhoneTheme theme, float drawScale,
        float labelAlpha, bool showLabels, string fallbackName, float labelWidth, float zoom = 1f)
    {
        var scale = ImGuiHelpers.GlobalScale * zoom;
        var dl = ImGui.GetWindowDrawList();
        var drawHalf = size * 0.5f * drawScale;
        var min = new Vector2(center.X - drawHalf, center.Y - drawHalf);
        var max = new Vector2(center.X + drawHalf, center.Y + drawHalf);
        var radius = size * 0.26f * drawScale;
        Elevation.IconRest(dl, min, max, radius, scale);
        Squircle.Fill(dl, min, max, radius, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.16f)));
        Material.EdgeSquircle(dl, min, max, radius, scale);
        var pad = drawHalf * 0.28f;
        var inner = drawHalf * 2f - pad * 2f;
        var cell = inner / 3f;
        var mini = cell * 0.78f;
        var count = Math.Min(9, folder.Apps.Count);
        for (var index = 0; index < count; index++)
        {
            var col = index % 3;
            var row = index / 3;
            var cellCenter = new Vector2(min.X + pad + (col + 0.5f) * cell, min.Y + pad + (row + 0.5f) * cell);
            var miniMin = new Vector2(cellCenter.X - mini * 0.5f, cellCenter.Y - mini * 0.5f);
            var miniMax = new Vector2(cellCenter.X + mini * 0.5f, cellCenter.Y + mini * 0.5f);
            var appItem = folder.Apps[index];
            var surface = IconTile.Surface(appItem.Accent);
            Squircle.Fill(dl, miniMin, miniMax, mini * 0.3f, ImGui.GetColorU32(surface));
            AppIconArt.TryDraw(appItem.Id, cellCenter, mini, GlyphInk, Palette.Darken(surface, 0.25f));
        }

        var name = string.IsNullOrEmpty(folder.FolderName) ? fallbackName : folder.FolderName;
        DrawLabel(center, size, name, theme, scale, labelAlpha, showLabels, labelWidth, zoom);
        var badgeTotal = 0;
        var badgeHasDot = false;
        for (var appIndex = 0; appIndex < folder.Apps.Count; appIndex++)
        {
            var folderApp = folder.Apps[appIndex];
            if (folderApp.BadgeCount <= 0)
            {
                continue;
            }

            if (folderApp.BadgeAsDot)
            {
                badgeHasDot = true;
            }
            else
            {
                badgeTotal += folderApp.BadgeCount;
            }
        }

        if (badgeTotal > 0)
        {
            DrawBadge(center, size, badgeTotal, false, theme, scale);
        }
        else if (badgeHasDot)
        {
            DrawBadge(center, size, 1, true, theme, scale);
        }
    }

    private static void DrawBadge(Vector2 center, float size, int count, bool asDot, PhoneTheme theme, float scale)
    {
        var badgeCenter = new Vector2(center.X + size * 0.5f - 5f * scale, center.Y - size * 0.5f + 5f * scale);
        if (asDot)
        {
            AppBadge.DrawDot(badgeCenter, theme, scale);
        }
        else
        {
            AppBadge.Draw(badgeCenter, count, theme, scale);
        }
    }

    public static bool RemoveBadge(Vector2 center, float scale, PhoneTheme theme)
    {
        var radius = 9f * scale;
        var dl = ImGui.GetWindowDrawList();
        var hovered =
            ImGui.IsMouseHoveringRect(center - new Vector2(radius, radius), center + new Vector2(radius, radius));
        dl.AddCircleFilled(center, radius, ImGui.GetColorU32(Palette.WithAlpha(theme.TextStrong, hovered ? 1f : 0.88f)),
            24);
        var arm = radius * 0.4f;
        var ink = ImGui.GetColorU32(new Vector4(0.1f, 0.1f, 0.12f, 1f));
        dl.AddLine(new Vector2(center.X - arm, center.Y), new Vector2(center.X + arm, center.Y), ink, 1.8f * scale);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
    }

    private static void DrawLabel(Vector2 center, float size, string label, PhoneTheme theme, float scale,
        float labelAlpha, bool showLabels, float labelWidth, float zoom)
    {
        if (!showLabels || labelAlpha <= 0.01f)
        {
            return;
        }

        var labelCenter = new Vector2(center.X, center.Y + size * 0.5f + 11f * scale);
        var strength = WallpaperLegibility.Strength(theme);
        var halo = Palette.WithAlpha(new Vector4(0f, 0f, 0f, 1f), (0.22f + 0.30f * strength) * labelAlpha);
        var text = Palette.WithAlpha(theme.TextStrong, 0.98f * labelAlpha);
        var style = zoom == 1f
            ? TextStyles.IconLabel
            : new TextStyle(TextStyles.IconLabel.Scale * zoom, TextStyles.IconLabel.Weight);
        Typography.DrawCenteredHalo(labelCenter, label, text, halo, (1.3f + 0.5f * strength) * scale,
            labelWidth * 0.92f, style);
    }
}
