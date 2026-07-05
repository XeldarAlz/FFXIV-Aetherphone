using System.Numerics;
using Aetherphone.Core;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Home;
using Aetherphone.Core.Theme;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace Aetherphone.Windows.Components;

internal static class HomeTileView
{
    public static void DrawApp(Vector2 center, float size, IPhoneApp app, PhoneTheme theme, float drawScale,
        float labelAlpha)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var drawHalf = size * 0.5f * drawScale;
        var drawMin = new Vector2(center.X - drawHalf, center.Y - drawHalf);
        var drawMax = new Vector2(center.X + drawHalf, center.Y + drawHalf);
        Squircle.Fill(dl, drawMin, drawMax, size * 0.26f * drawScale, ImGui.GetColorU32(app.Accent));
        if (!AppIconArt.TryDraw(app.Id, center, size * drawScale, theme.TextStrong, app.Accent))
        {
            var glyphHeight = Typography.Measure(app.Glyph).Y;
            var glyphScale = glyphHeight > 0f ? size * drawScale * 0.5f / glyphHeight : 1f;
            Typography.DrawCentered(center, app.Glyph, theme.TextStrong, glyphScale);
        }

        DrawLabel(center, size, app.DisplayName, theme, scale, labelAlpha);
        if (app.BadgeCount > 0)
        {
            AppBadge.Draw(new Vector2(center.X + size * 0.5f - 5f * scale, center.Y - size * 0.5f + 5f * scale),
                app.BadgeCount, theme, scale);
        }
    }

    public static void DrawFolder(Vector2 center, float size, HomeTile folder, PhoneTheme theme, float drawScale,
        float labelAlpha, string fallbackName)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var dl = ImGui.GetWindowDrawList();
        var drawHalf = size * 0.5f * drawScale;
        var min = new Vector2(center.X - drawHalf, center.Y - drawHalf);
        var max = new Vector2(center.X + drawHalf, center.Y + drawHalf);
        var radius = size * 0.26f * drawScale;
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
            Squircle.Fill(dl, miniMin, miniMax, mini * 0.3f, ImGui.GetColorU32(appItem.Accent));
            AppIconArt.TryDraw(appItem.Id, cellCenter, mini, theme.TextStrong, appItem.Accent);
        }

        var name = string.IsNullOrEmpty(folder.FolderName) ? fallbackName : folder.FolderName;
        DrawLabel(center, size, name, theme, scale, labelAlpha);
    }

    private static void DrawLabel(Vector2 center, float size, string label, PhoneTheme theme, float scale,
        float labelAlpha)
    {
        if (labelAlpha <= 0.01f)
        {
            return;
        }

        var labelCenter = new Vector2(center.X, center.Y + size * 0.5f + 11f * scale);
        var shadowOffset = new Vector2(0f, 1f * scale);
        Typography.DrawCentered(labelCenter + shadowOffset, label,
            Palette.WithAlpha(new Vector4(0f, 0f, 0f, 1f), 0.45f * labelAlpha), 0.85f, FontWeight.Medium);
        Typography.DrawCentered(labelCenter, label, Palette.WithAlpha(theme.TextStrong, 0.95f * labelAlpha), 0.85f,
            FontWeight.Medium);
    }
}
